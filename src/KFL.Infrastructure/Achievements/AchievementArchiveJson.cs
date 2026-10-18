using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Infrastructure.Achievements;

/// <summary>
/// 全局成就档案的 JSON 形状与解析（008-achievements-extinction；契约二十三「条款 5」~「条款 10」）。
/// </summary>
/// <remarks>
/// <para>
/// <b>恒为 JSON，无二进制侧</b>（契约二十三「条款 5」，<c>research.md</c> R-11）：
/// 「跟随当前存档格式」是**禁止**的——同一个玩家的成就面 MUST 在两种存档格式下可互认。
/// </para>
/// <para>
/// <b>版本不联动</b>（「条款 10」）：本文件自带 <see cref="Version"/>，
/// MUST NOT 复用 <c>SaveFileFormat.Version</c>——两者生命周期完全不同（存档格式一自增，
/// 旧档就该被拒绝；成就面自增不该让任何东西失效）。
/// </para>
/// <para>
/// <b>向后兼容（FR-019，条款 7）</b>：未知属性**忽略**、缺失属性**取默认**
/// （缺 <c>Achievements</c> ⇒ 空数组、缺 <c>Version</c> ⇒ 视作 <see cref="Version"/>）、
/// <c>Version &gt; 当前</c> ⇒ **不失败**。因此本文件的 DTO 成员
/// **MUST NOT 标 <c>[JsonRequired]</c>**（尤其不得标在 <c>Achievements</c> / <c>Origins</c>
/// 的**元素**上，否则缺失元素键会被当成致命错误）。
/// </para>
/// </remarks>
public static class AchievementArchiveJson
{
    /// <summary>归档文件自身的版本；首版为 <c>1</c>（**与 <c>SaveFileFormat.Version</c> 无关**）。</summary>
    public const int Version = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // 与 SaveFileFormat.JsonOptions 同款：让中文家族名/成就名保持可读，而不是 \uXXXX。
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        // 读向宽容：大小写不同不构成语法错（写向恒为 PascalCase）。
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>把一个档案序列化为 JSON 文本。</summary>
    /// <param name="archive">要序列化的档案。</param>
    /// <returns>JSON 文本（缩进、可读）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="archive"/> 为 <c>null</c>。</exception>
    public static string Serialize(AchievementArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

        var document = new ArchiveDocumentDto
        {
            Version = archive.Version,
            Achievements = [.. archive.Achievements.Select(static record => new ArchiveAchievementDto
            {
                Achievement = (int)record.Achievement,
                Difficulty = (int)record.Difficulty,
                CompletedAt = ArchiveDateDto.From(record.CompletedAt),
                SourceSaveId = record.SourceSaveId,
            })],
            Origins = [.. archive.Origins.Select(static record => new ArchiveOriginDto
            {
                Origin = (int)record.Origin,
                Milestone = (int)record.Milestone,
                GameDate = ArchiveDateDto.From(record.GameDate),
                SourceSaveId = record.SourceSaveId,
            })],
        };

        return JsonSerializer.Serialize(document, Options);
    }

    /// <summary>尝试解析 JSON 文本；**任何**解析失败都以 <c>false</c> 表达（MUST NOT 抛）。</summary>
    /// <remarks>
    /// 返回 <c>false</c> 的语义是「文件不可解析」——调用方据此走损坏隔离（契约二十三「条款 15」）。
    /// 空文本、<c>null</c> 字面量、语法错、结构不匹配都在此列。
    /// </remarks>
    /// <param name="json">JSON 文本。</param>
    /// <param name="archive">解析出的档案；失败时为 <see cref="AchievementArchive.Empty"/>。</param>
    /// <returns>解析成功 ⇒ <c>true</c>。</returns>
    public static bool TryDeserialize(string json, out AchievementArchive archive)
    {
        archive = AchievementArchive.Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        ArchiveDocumentDto? document;
        try
        {
            document = JsonSerializer.Deserialize<ArchiveDocumentDto>(json, Options);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            // 结构虽合法却无法映射（例如数组套到了对象位上）——同样按「不可解析」处理。
            return false;
        }

        if (document is null)
        {
            return false;
        }

        // 缺 Version ⇒ 视作首版（条款 7 第 2 点）；Version 更高 ⇒ **不失败**（条款 7 第 4 点），
        // 直接把原值带出去，让上层知道这是高版本文件即可。
        var version = document.Version ?? Version;

        var achievements = new List<ArchiveAchievementRecord>();
        foreach (var record in document.Achievements ?? [])
        {
            if (record is null || record.CompletedAt is null)
            {
                // 元素缺必需子对象 ⇒ 该元素不可信，跳过而不是整体报废（宽容读向）。
                continue;
            }

            achievements.Add(new ArchiveAchievementRecord(
                (AchievementId)record.Achievement,
                (Difficulty)record.Difficulty,
                record.CompletedAt.ToGameDate(),
                record.SourceSaveId));
        }

        var origins = new List<OriginRecord>();
        foreach (var record in document.Origins ?? [])
        {
            if (record is null || record.GameDate is null)
            {
                continue;
            }

            origins.Add(new OriginRecord(
                (Origin)record.Origin,
                (OriginMilestone)record.Milestone,
                record.GameDate.ToGameDate(),
                record.SourceSaveId));
        }

        archive = new AchievementArchive
        {
            Version = version,
            Achievements = achievements,
            Origins = origins,
            Diagnostics = AchievementArchiveDiagnostics.None,
        };

        return true;
    }

    /// <summary>根对象（契约二十三「条款 5」的示例形状）。</summary>
    /// <remarks>成员一律**不标** <c>[JsonRequired]</c>：缺失即默认（条款 7）。</remarks>
    private sealed class ArchiveDocumentDto
    {
        [JsonPropertyName("Version")]
        public int? Version { get; set; }

        [JsonPropertyName("Achievements")]
        public List<ArchiveAchievementDto?>? Achievements { get; set; }

        [JsonPropertyName("Origins")]
        public List<ArchiveOriginDto?>? Origins { get; set; }
    }

    /// <summary>一条达成记录。</summary>
    private sealed class ArchiveAchievementDto
    {
        [JsonPropertyName("Achievement")]
        public int Achievement { get; set; }

        [JsonPropertyName("Difficulty")]
        public int Difficulty { get; set; }

        [JsonPropertyName("CompletedAt")]
        public ArchiveDateDto? CompletedAt { get; set; }

        [JsonPropertyName("SourceSaveId")]
        public Guid SourceSaveId { get; set; }
    }

    /// <summary>一条出身达成记录。</summary>
    private sealed class ArchiveOriginDto
    {
        [JsonPropertyName("Origin")]
        public int Origin { get; set; }

        [JsonPropertyName("Milestone")]
        public int Milestone { get; set; }

        [JsonPropertyName("GameDate")]
        public ArchiveDateDto? GameDate { get; set; }

        [JsonPropertyName("SourceSaveId")]
        public Guid SourceSaveId { get; set; }
    }

    /// <summary>年月（照存档约定用两个 <c>int</c>，MUST NOT 引入新的日期表示）。</summary>
    private sealed class ArchiveDateDto
    {
        [JsonPropertyName("Year")]
        public int Year { get; set; }

        [JsonPropertyName("Month")]
        public int Month { get; set; }

        public static ArchiveDateDto From(GameDate date) => new() { Year = date.Year, Month = date.Month };

        public GameDate ToGameDate() => new(Year, Month);
    }
}
