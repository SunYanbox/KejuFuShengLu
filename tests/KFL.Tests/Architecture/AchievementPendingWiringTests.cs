using System.Text.RegularExpressions;

using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Achievements;
using KFL.Rules.Achievements;
using KFL.Rules.Config;
using KFL.Tests.Fixtures;
using KFL.Tests.Infrastructure;
using KFL.Tests.Rules;

using Xunit;

namespace KFL.Tests.Architecture;

/// <summary>
/// T071 / T072（<c>specs/008-achievements-extinction/tasks.md:303-304</c>，US5 / SC-002）：
/// 「待接线成就清单」的双向一致性、零自造规则与零调试控制台唤醒路径，
/// 以及调试唤醒（ACH-064~068）的档位边界与存档往返。
/// </summary>
public sealed class AchievementPendingWiringTests
{
    private static readonly Guid StateId = Guid.Parse("8a1c2f10-4b5d-4e6f-8a9b-0c1d2e3f4a5b");

    private static readonly AchievementMark[] NoMarks = [];
    private static readonly OriginRecord[] NoOrigins = [];

    /// <summary>登记清单的六条待接线成就（<c>tasks.md</c> 「待接线成就清单」表，按成就号升序）。</summary>
    private static readonly AchievementId[] PendingIds =
    [
        AchievementId.TradeLoss,
        AchievementId.DebugWakeOne,
        AchievementId.DebugWakeTen,
        AchievementId.DebugWakeHundred,
        AchievementId.DebugWakeThousand,
        AchievementId.DebugWakeTenThousand,
    ];

    private static readonly string[] PendingCodes =
    [
        "ACH-041",
        "ACH-064",
        "ACH-065",
        "ACH-066",
        "ACH-067",
        "ACH-068",
    ];

    private static readonly string[] PendingNames =
    [
        "商海浮沉",
        "天机泄露",
        "窥探虚实",
        "篡改命数",
        "虚空主宰",
        "代码飞升",
    ];

    private static readonly AchievementId[] DebugWakeIds =
    [
        AchievementId.DebugWakeOne,
        AchievementId.DebugWakeTen,
        AchievementId.DebugWakeHundred,
        AchievementId.DebugWakeThousand,
        AchievementId.DebugWakeTenThousand,
    ];

    private static readonly int[] DebugWakeThresholds = [1, 10, 100, 1_000, 10_000];

    private static readonly (int Threshold, int Delta)[] DebugWakeSteps =
    [
        (1, 1),
        (10, 9),
        (100, 90),
        (1_000, 900),
        (10_000, 9_000),
    ];

    /// <summary>`TradeLoss` 只允许出现在这些接线点：枚举成员、目录行、判据占位、来源枚举注解、两个待接线常量及其透传，外加界面层的成就描述文案表。</summary>
    private static readonly string[] TradeLossWiringFiles =
    [
        @"src\KFL.Core\Enums\AchievementId.cs",
        @"src\KFL.Presentation\AchievementDescriptions.cs",
        @"src\KFL.Rules\Achievements\AchievementCatalog.cs",
        @"src\KFL.Rules\Achievements\AchievementPredicates.cs",
        @"src\KFL.Rules\Achievements\AchievementSource.cs",
        @"src\KFL.Rules\Config\AchievementPolicy.cs",
        @"src\KFL.Rules\Config\GameConfig.cs",
    ];

    private static readonly string[] TradeLossCommentFiles =
    [
        @"src\KFL.Presentation\AchievementDescriptions.cs",
        @"src\KFL.Rules\Achievements\AchievementPredicates.cs",
        @"src\KFL.Rules\Config\AchievementPolicy.cs",
        @"src\KFL.Rules\Config\GameConfig.cs",
    ];

    /// <summary>上报唤醒的唯一接缝：状态累加器 + 计数器门面；界面层只能经门面（U4 调试控制台）的复数包装取用。</summary>
    private static readonly string[] DebugWakeSeamFiles =
    [
        @"src\KFL.Core\Entities\AchievementState.cs",
        @"src\KFL.Rules\Achievements\AchievementCounters.cs",
    ];

    private static readonly (SaveFormat Format, string SlotName)[] SaveRoundTrips =
    [
        (SaveFormat.Binary, "甲档"),
        (SaveFormat.Json, "乙档"),
    ];

    [Fact]
    public void 待接线成就恰六条且与登记清单双向一致()
    {
        var pending = AchievementCatalog.All
            .Where(definition => definition.Source == AchievementSource.Pending)
            .ToArray();

        Assert.Equal(PendingIds.Length, pending.Length);
        Assert.Equal(PendingIds, pending.Select(definition => definition.Id).ToArray());
        Assert.Equal(PendingCodes, pending.Select(definition => definition.Code).ToArray());
        Assert.Equal(PendingNames, pending.Select(definition => definition.Name).ToArray());

        // 反方向：登记处的六个成就代号恰是目录里的这六条，不多不少。
        var section = PendingWiringSection();
        var registeredCodes = Regex.Matches(section, "ACH-[0-9]{3}")
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(PendingCodes, registeredCodes);
        foreach (var code in PendingCodes)
        {
            Assert.Contains(code, section, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void 生产代码零自造经商亏损规则且调试控制台仅限界面层()
    {
        var product = ProductSources();

        // 经商亏损（ACH-041）：`TradeLoss` 只出现在登记的接线点。
        var tradeLossFiles = product
            .Where(file => file.Value.Contains("TradeLoss", StringComparison.Ordinal))
            .Select(file => file.Key)
            .ToArray();
        Assert.Equal(TradeLossWiringFiles, tradeLossFiles);

        // 该成就的汉字表述也只出现在既有接线点（均为「待接线」注释）。
        var lossTextFiles = product
            .Where(file => file.Value.Contains("亏损", StringComparison.Ordinal))
            .Select(file => file.Key)
            .ToArray();
        Assert.Equal(TradeLossCommentFiles, lossTextFiles);

        // 幅度常量只有规格书登记的 10/30，未自造概率、频率或其它数值。
        Assert.Equal(10, AchievementPolicy.TradeLossMinPercent);
        Assert.Equal(30, AchievementPolicy.TradeLossMaxPercent);

        // 判据恒不成立：不存在任何经商亏损判定实现。
        Assert.False(AchievementPredicates.IsSatisfied(AchievementId.TradeLoss, ContextOf(NewState())));

        // 调试控制台（规格书 §13）：U 轨 U4 已交付，`Suntion` 唤醒文本只允许出现在界面层 `KFL.App`。
        Assert.Contains(product, file => file.Key.StartsWith(@"src\KFL.App\", StringComparison.OrdinalIgnoreCase)
            && file.Value.Contains("Suntion", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(product, file => !file.Key.StartsWith(@"src\KFL.App\", StringComparison.OrdinalIgnoreCase)
            && file.Value.Contains("Suntion", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(product, file => file.Key.Contains("DebugConsole", StringComparison.OrdinalIgnoreCase));

        // 唤醒只能经状态累加器与计数器门面进入（界面层调用门面，不直接写计数器），别处没有第二条路径。
        var wakeFiles = product
            .Where(file => file.Value.Contains("ReportDebugWake", StringComparison.Ordinal))
            .Select(file => file.Key)
            .ToArray();
        Assert.Equal(DebugWakeSeamFiles, wakeFiles);
    }

    [Fact]
    public void 未上报唤醒时五条调试秘辛恒不解锁()
    {
        var state = NewState();
        Assert.Equal(0, state.Achievements.DebugWakeCount);

        var context = ContextOf(state);
        foreach (var id in DebugWakeIds)
        {
            Assert.False(AchievementPredicates.IsSatisfied(id, context), $"{id} 在零次唤醒时判据 MUST 不成立");
        }

        for (var round = 0; round < 3; round++)
        {
            var outcome = AchievementAdjudicator.Report(state, AchievementTrigger.DebugWakeReported);

            Assert.DoesNotContain(outcome.NewlyUnlocked, DebugWakeIds.Contains);
            Assert.DoesNotContain(outcome.AlreadyCompleted, DebugWakeIds.Contains);
            Assert.DoesNotContain(state.Achievements.Completed, mark => DebugWakeIds.Contains(mark.Achievement));
            Assert.False(AchievementQuery.IsCompleted(state, AchievementId.DebugWakeTenThousand));
        }
    }

    [Fact]
    public void 零次唤醒不解锁而逐档阈值依次解锁()
    {
        AssertExactDebugTiers(NewState(), 0);

        var state = NewState();
        foreach (var (threshold, delta) in DebugWakeSteps)
        {
            // ReportsDebugWake 返回累计次数，并在同一次调用里完成即时判定。
            Assert.Equal(threshold, AchievementCounters.ReportsDebugWake(state, delta));
            Assert.Equal(threshold, state.Achievements.DebugWakeCount);
            AssertExactDebugTiers(state, threshold);
        }
    }

    [Fact]
    public void 九千九百九十九与一万的档位边界()
    {
        var state = NewState();

        Assert.Equal(9_999, AchievementCounters.ReportsDebugWake(state, 9_999));
        Assert.True(AchievementQuery.IsCompleted(state, AchievementId.DebugWakeThousand));
        Assert.False(AchievementQuery.IsCompleted(state, AchievementId.DebugWakeTenThousand));
        Assert.Equal(4, AchievementQuery.SeriesProgress(state, AchievementSeries.DebugSecrets).Completed);

        Assert.Equal(10_000, AchievementCounters.ReportsDebugWake(state, 1));
        Assert.True(AchievementQuery.IsCompleted(state, AchievementId.DebugWakeTenThousand));
        AssertExactDebugTiers(state, 10_000);
    }

    [Fact]
    public void 超过一万不清零不溢出且无更高档位成就()
    {
        var state = NewState();
        Assert.Equal(10_000, AchievementCounters.ReportsDebugWake(state, 10_000));
        var completedAtCap = state.Achievements.Completed.Count;

        var beyond = 10_000 + 12_345;
        Assert.Equal(beyond, AchievementCounters.ReportsDebugWake(state, 12_345));
        Assert.Equal(beyond, state.Achievements.DebugWakeCount);
        Assert.True(state.Achievements.DebugWakeCount > AchievementPolicy.DebugWakeTenThousand);

        // 无更高档位成就，也不清零：再报一次只增不解锁。
        Assert.Equal(completedAtCap, state.Achievements.Completed.Count);
        Assert.Equal(beyond + 1, AchievementCounters.ReportsDebugWake(state, 1));

        var progress = AchievementQuery.SeriesProgress(state, AchievementSeries.DebugSecrets);
        Assert.Equal(progress.Total, progress.Completed);
        Assert.Equal(AchievementCatalog.CountFor(AchievementSeries.DebugSecrets), progress.Total);
    }

    [Fact]
    public void 唤醒计数随存档往返保持不归零()
    {
        foreach (var (format, slotName) in SaveRoundTrips)
        {
            var state = SaveFixtures.PlainState();
            Assert.Equal(1_234, AchievementCounters.ReportsDebugWake(state, 1_234));

            using var sandbox = new SaveTestSandbox();
            var service = sandbox.CreateService();
            var slot = new SaveSlot(slotName, format);
            service.Save(state, slot, overwrite: false);

            var loaded = service.Load(slot);
            Assert.Equal(1_234, loaded.Achievements.DebugWakeCount);
            Assert.True(AchievementQuery.IsCompleted(loaded, AchievementId.DebugWakeThousand));
            Assert.False(AchievementQuery.IsCompleted(loaded, AchievementId.DebugWakeTenThousand));
        }
    }

    private static GameState NewState()
    {
        var family = RulesHarness.FamilyOf(RulesHarness.Member(1, Gender.Male, 40));
        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(RulesHarness.Guan(50m)),
            new Holdings());

        return new GameState(StateId, RulesHarness.Date, Difficulty.Normal, Origin.Scholar, family, economy);
    }

    private static AchievementContext ContextOf(GameState state) =>
        new(state, null, NoMarks, NoOrigins, state.CurrentDate);

    private static void AssertExactDebugTiers(GameState state, int wakes)
    {
        for (var index = 0; index < DebugWakeIds.Length; index++)
        {
            var unlocked = DebugWakeThresholds[index] <= wakes;
            Assert.Equal(unlocked, AchievementQuery.IsCompleted(state, DebugWakeIds[index]));
            Assert.Equal(unlocked, AchievementPredicates.IsSatisfied(DebugWakeIds[index], ContextOf(state)));
        }

        var progress = AchievementQuery.SeriesProgress(state, AchievementSeries.DebugSecrets);
        Assert.Equal(DebugWakeIds.Length, progress.Total);
        Assert.Equal(DebugWakeThresholds.Count(threshold => threshold <= wakes), progress.Completed);
    }

    /// <summary>
    /// T096 / FR-031（U 轨 U3/U4 交付后修正）：界面轨已按规格书 §14.1/§15/§16 消费成就、绝嗣与编年史，
    /// 故本用例反转为**正向接线守卫**——两个界面工程 MUST 出现成就与编年史接线，
    /// 但 MUST NOT 复制规则层判据（`AchievementPredicates`）或三均值算法（`TukeyTrimean`）。
    /// 判据与 <see cref="ProductSources"/> 同源：只看**源码文本**，不加载程序集。
    /// </summary>
    [Fact]
    public void 界面两层已接线成就与编年史且不复制规则判据()
    {
        string[] wired = ["Achievement", "Chronicle", "Ending"];

        var uiSources = ProductSources()
            .Where(file => file.Key.StartsWith(@"src\KFL.Presentation\", StringComparison.OrdinalIgnoreCase)
                || file.Key.StartsWith(@"src\KFL.App\", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(uiSources);
        foreach (var identifier in wired)
        {
            Assert.Contains(uiSources, file => file.Value.Contains(identifier, StringComparison.Ordinal));
        }

        string[] forbidden = ["AchievementPredicates", "TukeyTrimean", "PendingUnlock"];
        var offenders = uiSources
            .Where(file => forbidden.Any(identifier => file.Value.Contains(identifier, StringComparison.Ordinal)))
            .Select(file => file.Key)
            .ToArray();

        Assert.Empty(offenders);
    }

    private static List<KeyValuePair<string, string>> ProductSources()
    {
        var sources = RepositoryLocator.Load().SourceFiles
            .Where(file => file.Key.StartsWith(@"src\", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file.Key, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(sources);
        return sources;
    }

    /// <summary>
    /// 取 <c>tasks.md</c> 里「待接线成就清单」的**表体**（标题后第一行以 <c>|</c> 开头的行起，
    /// 到第一行不再以 <c>|</c> 开头的行为止）。
    /// </summary>
    /// <remarks>
    /// 表体之外的部分 MUST NOT 计入：T074 在表后补的「与契约二十二 §4 的逐行对应」散文会**合法地**
    /// 引用登记处以外的成就（ACH-047 `四民皆历` 的 `Origins` 读面），T082 补的「收尾任务编号」表亦然。
    /// 若把整节都算作登记处，这些交叉引用会被误判成「多登记了一条」。
    /// </remarks>
    /// <returns>登记处表体的行文本。</returns>
    private static string PendingWiringSection()
    {
        var path = Path.Combine(
            RepositoryLocator.LocateRepositoryRoot(),
            "specs",
            "008-achievements-extinction",
            "tasks.md");
        Assert.True(File.Exists(path), $"登记处文件不存在：{path}");

        var lines = File.ReadAllLines(path);
        var heading = Array.FindIndex(lines, line => line.Contains("待接线成就清单", StringComparison.Ordinal));
        Assert.True(heading >= 0, "tasks.md 缺少「待接线成就清单」登记处标题");

        var start = Array.FindIndex(lines, heading, line => line.TrimStart().StartsWith('|'));
        Assert.True(start > heading, "「待接线成就清单」登记处缺少表体");

        var end = Array.FindIndex(lines, start, line => !line.TrimStart().StartsWith('|'));
        Assert.True(end > start, "「待接线成就清单」登记处表体未结束");

        return string.Join("\n", lines[start..end]);
    }
}
