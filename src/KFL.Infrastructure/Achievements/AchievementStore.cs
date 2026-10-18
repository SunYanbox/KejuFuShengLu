using System.Text;
using System.Text.Json;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save;

namespace KFL.Infrastructure.Achievements;

/// <summary>
/// 成就全局档案的文件实现（008-achievements-extinction；契约二十三 §1~§5）。
/// </summary>
/// <remarks>
/// <para>
/// <b>落点恒为</b> <c>&lt;ISaveService.RootDirectory&gt;/achievements.json</c>（「条款 4」），
/// 格式恒为 JSON（「条款 5」）——与当前存档的格式无关。
/// </para>
/// <para>
/// <b>本类型 MUST NOT 抛</b>（「条款 2」）：<see cref="Read"/> 与 <see cref="Flush"/> 的全部
/// 可预期失败（目录不可写、文件被占用、JSON 语法错、结构非法、版本过高）都以返回值表达。
/// 唯一的例外是 <c>null</c> 入参——那是编程错误，不是可预期失败。
/// </para>
/// <para>
/// <b>文件系统访问点复用既有两处</b>：路径拼接/存在性/读字节走 <see cref="SaveCatalog"/>，
/// 原子替换与隔离改名走 <see cref="AtomicFileWriter"/>。本文件因此**不新增**任何带
/// <c>// arch-guard:allow</c> 的裸 <c>File.</c>/<c>Path.</c> 调用（契约九 §4.1 的单点原则）。
/// </para>
/// </remarks>
public sealed class AchievementStore : IAchievementStore
{
    /// <summary>全局成就文件名（**恒**为此名，不随档名变化）。</summary>
    public const string FileName = "achievements.json";

    /// <summary>隔离文件名的词干（<c>achievements</c>）；隔离名为 <c>achievements.corrupt-&lt;n&gt;.json</c>。</summary>
    private const string QuarantineStem = "achievements.corrupt-";

    /// <summary>隔离文件名的扩展名（与主文件同为 <c>.json</c>）。</summary>
    private const string QuarantineExtension = ".json";

    private readonly ISaveService _saveService;
    private readonly AtomicFileWriter _writer;

    /// <summary>以存档服务的根目录为落点建一个成就档案存储（写入器取默认实现）。</summary>
    /// <param name="saveService">存档服务（只用于取 <see cref="ISaveService.RootDirectory"/>）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="saveService"/> 为 <c>null</c>。</exception>
    public AchievementStore(ISaveService saveService)
        : this(saveService, new AtomicFileWriter())
    {
    }

    /// <summary>以存档服务的根目录为落点建一个成就档案存储（写入器可注入，供分块失败模拟）。</summary>
    /// <param name="saveService">存档服务（只用于取根目录）。</param>
    /// <param name="writer">原子写入器。</param>
    /// <exception cref="ArgumentNullException">任一参数为 <c>null</c>。</exception>
    public AchievementStore(ISaveService saveService, AtomicFileWriter writer)
    {
        ArgumentNullException.ThrowIfNull(saveService);
        ArgumentNullException.ThrowIfNull(writer);

        _saveService = saveService;
        _writer = writer;
    }

    /// <inheritdoc />
    public string FilePath => SaveCatalog.Combine(_saveService.RootDirectory, FileName);

    /// <inheritdoc />
    public AchievementArchive Read()
    {
        var path = FilePath;

        // 「缺失不是损坏」（条款 17）：文件不在 ⇒ 空档案、**零**诊断。
        if (!SaveCatalog.FileExists(path))
        {
            return AchievementArchive.Empty;
        }

        string json;
        try
        {
            json = Encoding.UTF8.GetString(SaveCatalog.ReadAllBytes(path));
        }
        catch (Exception ex) when (ex is SaveAccessException or IOException or UnauthorizedAccessException)
        {
            // 读不动（被独占锁住、权限不足）**不是**「不可解析」，MUST NOT 拿它当损坏去改名：
            // 改不了也不会更好，反而会销毁证据。以空档案继续并把原因放进诊断。
            return AchievementArchive.Empty with
            {
                Diagnostics = new AchievementArchiveDiagnostics(false, null, $"成就档案不可读：{ex.Message}"),
            };
        }

        if (json.Length > 0 && json[0] == '\uFEFF')
        {
            json = json[1..];   // 手工编辑过的文件可能带 BOM；它不是 JSON 语法的一部分。
        }

        if (AchievementArchiveJson.TryDeserialize(json, out var archive))
        {
            return archive;
        }

        // 不可解析 ⇒ 先隔离再以空档案继续（条款 15）。
        var (quarantinePath, quarantineFailure) = TryQuarantine();
        var reason = quarantineFailure is null
            ? "成就档案不可解析，已隔离并重置为空档案。"
            : $"成就档案不可解析且隔离失败：{quarantineFailure}";

        return AchievementArchive.Empty with
        {
            Diagnostics = new AchievementArchiveDiagnostics(quarantineFailure is null, quarantinePath, reason),
        };
    }

    /// <inheritdoc />
    public AchievementFlushResult Flush(
        IReadOnlyList<PendingUnlock> unlocks,
        IReadOnlyList<OriginRecord> originAdditions)
    {
        ArgumentNullException.ThrowIfNull(unlocks);
        ArgumentNullException.ThrowIfNull(originAdditions);

        // 先把旧档案读进来（损坏则由 Read 完成隔离）；合并 MUST 以全量为基底（条款 23）。
        var archive = Read();
        var quarantinePath = archive.Diagnostics.QuarantinePath;

        if (!archive.Diagnostics.WasQuarantined && archive.Diagnostics.Reason is not null)
        {
            // 读取失败且**未**隔离：此刻写盘等于用空档案盖掉一份可能完好的旧档。
            // 宁可这次不落盘（队列保留，下次重试），也不做不可逆的破坏。
            return AchievementFlushResult.Failure(archive.Diagnostics.Reason, quarantinePath);
        }

        var merged = new AchievementArchive
        {
            // 写回我们**能写**的那个版本：文件的其它版本对整个档案而言只是可忽略的额外键，
            // 但写一个自己并不生产的高版本号等于撒谎（条款 7 第 4 点 / 条款 10）。
            Version = AchievementArchiveJson.Version,
            Achievements = MergeAchievements(archive.Achievements, unlocks),
            Origins = MergeOrigins(archive.Origins, originAdditions),
            Diagnostics = archive.Diagnostics,
        };

        byte[] payload;
        try
        {
            payload = Encoding.UTF8.GetBytes(AchievementArchiveJson.Serialize(merged));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return AchievementFlushResult.Failure($"成就档案序列化失败：{ex.Message}", quarantinePath);
        }

        try
        {
            // 档案自带根目录：`Read` 只需「文件在不在」，但写盘必须先有目录——
            // 复用 004 的建目录单点（`SaveCatalog` 已带 arch-guard 豁免），MUST NOT 自建目录访问调用。
            SaveCatalog.EnsureDirectory(_saveService.RootDirectory);
            _writer.Write(FilePath, payload);
        }
        catch (Exception ex) when (ex is SaveAccessException or IOException or UnauthorizedAccessException)
        {
            return AchievementFlushResult.Failure($"成就档案写入失败：{ex.Message}", quarantinePath);
        }

        return AchievementFlushResult.Success(quarantinePath);
    }

    /// <summary>把本次成就增量合并进既有记录（契约二十三「条款 11」）。</summary>
    /// <remarks>
    /// 键为 <c>(Achievement, Difficulty)</c>；键已存在时**仅当**新日期更早才替换日期与来源。
    /// 既有记录的**次序**不动（合并只就地替换或追加，MUST NOT 排序）。
    /// </remarks>
    private static List<ArchiveAchievementRecord> MergeAchievements(
        IReadOnlyList<ArchiveAchievementRecord> existing,
        IReadOnlyList<PendingUnlock> unlocks)
    {
        var merged = new List<ArchiveAchievementRecord>(existing);

        foreach (var unlock in unlocks)
        {
            var index = -1;
            for (var i = 0; i < merged.Count; i++)
            {
                if (merged[i].Achievement == unlock.Achievement && merged[i].Difficulty == unlock.Difficulty)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                merged.Add(new ArchiveAchievementRecord(
                    unlock.Achievement,
                    unlock.Difficulty,
                    unlock.UnlockedAt,
                    unlock.SourceSaveId));
                continue;
            }

            if (unlock.UnlockedAt < merged[index].CompletedAt)
            {
                merged[index] = merged[index] with
                {
                    CompletedAt = unlock.UnlockedAt,
                    SourceSaveId = unlock.SourceSaveId,
                };
            }
        }

        return merged;
    }

    /// <summary>把本次出身达成段增量合并进既有记录（契约二十三「条款 12」）。</summary>
    /// <remarks>键为 <c>(Origin, Milestone)</c>，规则与成就段同。</remarks>
    private static List<OriginRecord> MergeOrigins(
        IReadOnlyList<OriginRecord> existing,
        IReadOnlyList<OriginRecord> additions)
    {
        var merged = new List<OriginRecord>(existing);

        foreach (var addition in additions)
        {
            var index = -1;
            for (var i = 0; i < merged.Count; i++)
            {
                if (merged[i].Origin == addition.Origin && merged[i].Milestone == addition.Milestone)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                merged.Add(addition);
                continue;
            }

            if (addition.GameDate < merged[index].GameDate)
            {
                merged[index] = merged[index] with
                {
                    GameDate = addition.GameDate,
                    SourceSaveId = addition.SourceSaveId,
                };
            }
        }

        return merged;
    }

    /// <summary>把不可解析的档案改名为第一个未占用的隔离名（契约二十三「条款 15」「条款 16」）。</summary>
    /// <remarks>
    /// 序号从 <c>1</c> 起、取**第一个未占用**者；MUST NOT 用时间戳——墙钟取值（<c>DateTime</c> 的
    /// <c>Now</c> / <c>UtcNow</c> / <c>Today</c> 家族）在 G-07 的禁用 token 清单里，且扫描范围含
    /// <c>src\KFL.Infrastructure\</c>。
    /// 隔离失败也**不删**原文件：「无法隔离」不等于「可以销毁」。
    /// </remarks>
    /// <returns>隔离后的路径（成功）与失败原因（失败）。</returns>
    private (string? QuarantinePath, string? Failure) TryQuarantine()
    {
        var source = FilePath;

        for (var n = 1; ; n++)
        {
            var candidate = SaveCatalog.Combine(
                _saveService.RootDirectory,
                $"{QuarantineStem}{n}{QuarantineExtension}");

            if (SaveCatalog.FileExists(candidate))
            {
                continue;   // 该序号已被占用（可能来自更早的一次隔离）
            }

            try
            {
                _writer.Move(source, candidate);
            }
            catch (Exception ex) when (ex is SaveAccessException or IOException or UnauthorizedAccessException)
            {
                return (null, ex.Message);
            }

            return (candidate, null);
        }
    }
}
