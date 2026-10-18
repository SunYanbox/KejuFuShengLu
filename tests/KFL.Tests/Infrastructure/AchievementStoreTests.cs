using System.Text.Json;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Achievements;
using KFL.Infrastructure.Save;
using Xunit;

namespace KFL.Tests.Infrastructure;

/// <summary>
/// T034：全局成就档案的落盘/读回口径——固定文件名、按 (成就, 难度) 合并保留**最早**日期、
/// 损坏隔离编号来自「首个空位」而**不是**时间戳、兼容性宽松（未知属性忽略 / 缺版本视作 1 / 高版本不判失败）。
/// </summary>
/// <remarks>
/// 全部文件访问都经 <see cref="SaveTestSandbox"/>（G-07 的扫描面内只有它是豁免行）。
/// </remarks>
public class AchievementStoreTests
{
    private static readonly Guid SaveA = Guid.Parse("11111111-2222-4333-8444-555555555555");
    private static readonly Guid SaveB = Guid.Parse("66666666-7777-4888-8999-aaaaaaaaaaaa");

    [Fact]
    public void 档案路径恒为存档根下的固定文件名()
    {
        using var sandbox = new SaveTestSandbox();
        var store = new AchievementStore(sandbox.CreateService());

        Assert.Equal(
            SaveCatalog.Combine(sandbox.Root, "achievements.json"),
            store.FilePath);

        // 是 JSON 而不是二进制：与存档主档的两种格式无关，成就档案只有一种落盘形态。
        Assert.EndsWith(SaveFileFormat.JsonExtension, store.FilePath, StringComparison.Ordinal);
    }

    [Fact]
    public void 档案不存在时读回空档且无诊断()
    {
        using var sandbox = new SaveTestSandbox();
        var store = new AchievementStore(sandbox.CreateService());

        var archive = store.Read();

        Assert.Empty(archive.Achievements);
        Assert.Empty(archive.Origins);
        Assert.False(archive.Diagnostics.WasQuarantined);
        Assert.Null(archive.Diagnostics.QuarantinePath);
        Assert.Empty(sandbox.RootFiles());

        // 「文件不存在」不是损坏：MUST NOT 顺手造一个空档出来。
        Assert.Equal(AchievementArchiveJson.Version, archive.Version);
    }

    [Fact]
    public void 落盘后原样读回且来源标识来自解锁项()
    {
        using var sandbox = new SaveTestSandbox();
        var store = new AchievementStore(sandbox.CreateService());

        var unlockedAt = new GameDate(83, 7);
        var result = store.Flush(
            [new PendingUnlock(AchievementId.GoldenList, Difficulty.Hell, unlockedAt, SaveA)],
            [new OriginRecord(Origin.Merchant, OriginMilestone.CenturyFamily, new GameDate(102, 3), SaveA)]);

        Assert.True(result.IsSuccess);
        Assert.Null(result.FailureReason);

        var archive = store.Read();
        var record = Assert.Single(archive.Achievements);

        Assert.Equal(AchievementId.GoldenList, record.Achievement);
        Assert.Equal(Difficulty.Hell, record.Difficulty);
        Assert.Equal(unlockedAt, record.CompletedAt);

        // 来源是解锁项携带的存档标识，不是档案文件名（同一个文件会被多次覆盖写）。
        Assert.Equal(SaveA, record.SourceSaveId);

        var origin = Assert.Single(archive.Origins);
        Assert.Equal(Origin.Merchant, origin.Origin);
        Assert.Equal(OriginMilestone.CenturyFamily, origin.Milestone);
        Assert.Equal(new GameDate(102, 3), origin.GameDate);
        Assert.Equal(SaveA, origin.SourceSaveId);

        // 原子替换：根下只留最终档案，没有临时残留。
        Assert.Equal([store.FilePath], sandbox.RootFiles());
    }

    [Fact]
    public void 重复落盘同一成就保留最早日期与那一天的来源()
    {
        using var sandbox = new SaveTestSandbox();
        var store = new AchievementStore(sandbox.CreateService());

        // 先写「晚」的日期，再写「早」的日期：合并必须收敛到最早那一次。
        Assert.True(store.Flush(
            [new PendingUnlock(AchievementId.Longevity, Difficulty.Normal, new GameDate(85, 5), SaveB)],
            []).IsSuccess);
        Assert.True(store.Flush(
            [new PendingUnlock(AchievementId.Longevity, Difficulty.Normal, new GameDate(84, 2), SaveA)],
            []).IsSuccess);

        var archive = store.Read();
        var record = Assert.Single(archive.Achievements);

        Assert.Equal(new GameDate(84, 2), record.CompletedAt);
        Assert.Equal(SaveA, record.SourceSaveId);

        // 反向再写一次更晚的日期：MUST NOT 覆盖已有的更早日期。
        Assert.True(store.Flush(
            [new PendingUnlock(AchievementId.Longevity, Difficulty.Normal, new GameDate(90, 11), SaveB)],
            []).IsSuccess);

        var reRead = Assert.Single(store.Read().Achievements);
        Assert.Equal(new GameDate(84, 2), reRead.CompletedAt);
        Assert.Equal(SaveA, reRead.SourceSaveId);
    }

    [Fact]
    public void 同一成就的不同难度各算一条()
    {
        using var sandbox = new SaveTestSandbox();
        var store = new AchievementStore(sandbox.CreateService());

        Assert.True(store.Flush(
            [
                new PendingUnlock(AchievementId.FoundationLaid, Difficulty.Normal, new GameDate(80, 1), SaveA),
                new PendingUnlock(AchievementId.FoundationLaid, Difficulty.Hell, new GameDate(80, 1), SaveA),
            ],
            []).IsSuccess);

        Assert.Equal(2, store.Read().Achievements.Count);
    }

    [Fact]
    public void 落盘是全量合并而不是覆盖增量()
    {
        using var sandbox = new SaveTestSandbox();
        var store = new AchievementStore(sandbox.CreateService());

        Assert.True(store.Flush(
            [new PendingUnlock(AchievementId.FoundationLaid, Difficulty.Normal, new GameDate(80, 1), SaveA)],
            []).IsSuccess);
        Assert.True(store.Flush(
            [new PendingUnlock(AchievementId.FirstJuRen, Difficulty.Normal, new GameDate(81, 4), SaveA)],
            []).IsSuccess);

        var archive = store.Read();

        Assert.Equal(2, archive.Achievements.Count);
        Assert.Contains(archive.Achievements, item => item.Achievement == AchievementId.FoundationLaid);
        Assert.Contains(archive.Achievements, item => item.Achievement == AchievementId.FirstJuRen);
    }

    [Fact]
    public void 损坏档案隔离为首个空位编号且不删原档()
    {
        using var sandbox = new SaveTestSandbox();
        var store = new AchievementStore(sandbox.CreateService());
        sandbox.EnsureRoot();
        SaveTestSandbox.WriteBytes(store.FilePath, "这不是 JSON"u8.ToArray());

        var archive = store.Read();

        Assert.Empty(archive.Achievements);
        Assert.True(archive.Diagnostics.WasQuarantined);
        Assert.NotNull(archive.Diagnostics.QuarantinePath);

        // 隔离名是「首个空位」的下标 1，且 MUST NOT 带时间戳。
        Assert.Equal(
            SaveCatalog.Combine(sandbox.Root, "achievements.corrupt-1.json"),
            archive.Diagnostics.QuarantinePath);

        // 损坏原档被改名留存（隔离不是删除），因此根下同时有隔离档与（读失败后未建的）主档。
        var files = sandbox.RootFiles();
        Assert.Contains(archive.Diagnostics.QuarantinePath!, files);
    }

    [Fact]
    public void 连续损坏占用递增的空位编号()
    {
        using var sandbox = new SaveTestSandbox();
        var store = new AchievementStore(sandbox.CreateService());

        sandbox.EnsureRoot();

        SaveTestSandbox.WriteBytes(store.FilePath, "坏档一"u8.ToArray());
        Assert.Equal(
            SaveCatalog.Combine(sandbox.Root, "achievements.corrupt-1.json"),
            store.Read().Diagnostics.QuarantinePath);

        SaveTestSandbox.WriteBytes(store.FilePath, "坏档二"u8.ToArray());
        Assert.Equal(
            SaveCatalog.Combine(sandbox.Root, "achievements.corrupt-2.json"),
            store.Read().Diagnostics.QuarantinePath);

        // 两个隔离档都在，原档未被删除。
        Assert.Equal(2, sandbox.RootFiles().Count);
    }

    [Fact]
    public void 损坏后仍可正常落盘()
    {
        using var sandbox = new SaveTestSandbox();
        var store = new AchievementStore(sandbox.CreateService());
        sandbox.EnsureRoot();
        SaveTestSandbox.WriteBytes(store.FilePath, "{ 半截"u8.ToArray());

        Assert.True(store.Read().Diagnostics.WasQuarantined);

        // 隔离后重新开始写：档案从空起算，新解锁项照常入档。
        Assert.True(store.Flush(
            [new PendingUnlock(AchievementId.FirstJinShi, Difficulty.Normal, new GameDate(82, 9), SaveA)],
            []).IsSuccess);

        var record = Assert.Single(store.Read().Achievements);
        Assert.Equal(AchievementId.FirstJinShi, record.Achievement);
    }

    [Fact]
    public void 未知属性被忽略()
    {
        using var sandbox = new SaveTestSandbox();
        var store = new AchievementStore(sandbox.CreateService());
        sandbox.EnsureRoot();
        SaveTestSandbox.WriteBytes(
            store.FilePath,
            """
            {
              "Version": 1,
              "Achievements": [
                {
                  "Achievement": 3,
                  "Difficulty": 1,
                  "CompletedAt": { "Year": 82, "Month": 9 },
                  "SourceSaveId": "11111111-2222-4333-8444-555555555555",
                  "未来字段": "不该让读回失败"
                }
              ],
              "Origins": [],
              "未来顶层": 42
            }
            """u8.ToArray());

        var archive = store.Read();
        var record = Assert.Single(archive.Achievements);

        Assert.Equal(AchievementId.FirstJinShi, record.Achievement);
        Assert.False(archive.Diagnostics.WasQuarantined);
    }

    [Fact]
    public void 缺版本字段视作第一版()
    {
        using var sandbox = new SaveTestSandbox();
        var store = new AchievementStore(sandbox.CreateService());
        sandbox.EnsureRoot();
        SaveTestSandbox.WriteBytes(
            store.FilePath,
            """
            { "Achievements": [], "Origins": [] }
            """u8.ToArray());

        var archive = store.Read();

        Assert.Equal(AchievementArchiveJson.Version, archive.Version);
        Assert.False(archive.Diagnostics.WasQuarantined);
    }

    [Fact]
    public void 更高版本号不判失败()
    {
        using var sandbox = new SaveTestSandbox();
        var store = new AchievementStore(sandbox.CreateService());
        sandbox.EnsureRoot();
        SaveTestSandbox.WriteBytes(
            store.FilePath,
            """
            { "Version": 99, "Achievements": [], "Origins": [] }
            """u8.ToArray());

        var archive = store.Read();

        // 与存档主档的「版本必须相等」相反：成就档案是**元进度**，读到未来版本也照常可用。
        Assert.False(archive.Diagnostics.WasQuarantined);
        Assert.Empty(archive.Achievements);
    }

    [Fact]
    public void 落盘档案的字段名稳定()
    {
        using var sandbox = new SaveTestSandbox();
        var store = new AchievementStore(sandbox.CreateService());

        Assert.True(store.Flush(
            [new PendingUnlock(AchievementId.Longevity, Difficulty.Easy, new GameDate(86, 2), SaveA)],
            [new OriginRecord(Origin.Farmer, OriginMilestone.Extinction, new GameDate(90, 1), SaveB)]).IsSuccess);

        using var document = JsonDocument.Parse(SaveTestSandbox.ReadBytes(store.FilePath));

        Assert.Equal(AchievementArchiveJson.Version, document.RootElement.GetProperty("Version").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("Achievements").GetArrayLength());
        Assert.Equal(1, document.RootElement.GetProperty("Origins").GetArrayLength());

        var achievement = document.RootElement.GetProperty("Achievements")[0];

        // 枚举按**整数**落盘（与存档主体同一约定），不是名字。
        Assert.Equal((int)AchievementId.Longevity, achievement.GetProperty("Achievement").GetInt32());
        Assert.Equal((int)Difficulty.Easy, achievement.GetProperty("Difficulty").GetInt32());
        Assert.Equal(86, achievement.GetProperty("CompletedAt").GetProperty("Year").GetInt32());
        Assert.Equal(2, achievement.GetProperty("CompletedAt").GetProperty("Month").GetInt32());

        var origin = document.RootElement.GetProperty("Origins")[0];

        Assert.Equal((int)Origin.Farmer, origin.GetProperty("Origin").GetInt32());
        Assert.Equal((int)OriginMilestone.Extinction, origin.GetProperty("Milestone").GetInt32());
    }

    [Fact]
    public void 出身达成只会有绝嗣与百年世家两个里程碑()
    {
        using var sandbox = new SaveTestSandbox();
        var store = new AchievementStore(sandbox.CreateService());

        // OriginMilestone 的枚举面上只有这两个成员：千秋万代（存续年数）**不是**出身里程碑，
        // 所以存储层在类型上就无法写出「千秋万代入 Origins」这一违约形态。
        Assert.Equal(
            [OriginMilestone.Extinction, OriginMilestone.CenturyFamily],
            Enum.GetValues<OriginMilestone>());
    }
}
