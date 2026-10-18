using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Services;
using KFL.Rules.Achievements;
using KFL.Rules.Config;
using KFL.Rules.Economy;
using KFL.Rules.Settlement;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// T031 / FR-004、FR-032：回合末判定的**固定次序**（累计量 → 68 条 → 绝嗣补判）、
/// 幂等性（重复命中只留一条且保留首次完成时点）、难度口径与结束态冻结。
/// </summary>
/// <remarks>
/// 期望值一律经 <c>KFL.Rules/Config</c> 与 <c>KFL.Rules/Achievements</c> 的成员取得，本文件
/// MUST NOT 另存规则数值（SC-008）。判定的「月事实」一律来自真实的
/// <see cref="MonthlySettlementEngine.Settle"/>，MUST NOT 手搓 <see cref="SettlementResult"/>。
/// </remarks>
public class AchievementAdjudicatorTests
{
    private static readonly Guid StateId = Guid.Parse("8a1c2f10-4b5d-4e6f-8a9b-0c1d2e3f4a5b");

    /// <summary>恒成立的成就（<c>ACH-001</c> 开基立业）：用作幂等性断言的稳定样本。</summary>
    private static readonly AchievementId AlwaysOn = AchievementId.FoundationLaid;

    [Fact]
    public void 判定先吸收当月事实再判成就()
    {
        var state = StateOf(Money.FromGuan(50_000m), Difficulty.Normal);
        var settlement = Settle(state);

        AchievementAdjudicator.Judge(state, settlement, [], []);

        // ① 先于 ②：判定的同一次调用里累计量已被吸收（编年史 ③ 与 ACH-033/050~053 都读它）。
        Assert.Equal(TotalAssetsCalculator.Of(state.Economy), state.Achievements.PeakTotalAssets);
        Assert.True(state.Achievements.PeakTotalAssets.IsPositive);
    }

    [Fact]
    public void 同月重复判定只留一条且完成时点保持首次()
    {
        var state = StateOf(Money.FromGuan(50_000m), Difficulty.Normal);
        var settlement = Settle(state);

        var first = AchievementAdjudicator.Judge(state, settlement, [], []);
        var firstAt = MarkOf(state, AlwaysOn).CompletedAt;

        // 同月再判一次（编排上每回合只判一次；此处只探**标记**的幂等，不探累计量）。
        var second = AchievementAdjudicator.Judge(state, settlement, [], []);

        Assert.Contains(AlwaysOn, first.NewlyUnlocked);
        Assert.DoesNotContain(AlwaysOn, second.NewlyUnlocked);
        Assert.Contains(AlwaysOn, second.AlreadyCompleted);
        Assert.Equal(firstAt, MarkOf(state, AlwaysOn).CompletedAt);

        var marked = 0;
        foreach (var mark in state.Achievements.Completed)
        {
            if (mark.Achievement == AlwaysOn)
            {
                marked++;
            }
        }

        Assert.Equal(1, marked);
    }

    [Fact]
    public void 跨月重复命中进入已达成并保留首次完成时点()
    {
        var state = StateOf(Money.FromGuan(50_000m), Difficulty.Normal);

        AchievementAdjudicator.Judge(state, Settle(state), [], []);

        // 首次记录的完成时点 = 判定当刻的 `CurrentDate`（结算第⑩步已推进过月份）。
        var firstMonth = state.CurrentDate;

        state.AdvanceMonth();

        var later = AchievementAdjudicator.Judge(state, Settle(state), [], []);

        Assert.NotEqual(firstMonth, state.CurrentDate);
        Assert.DoesNotContain(AlwaysOn, later.NewlyUnlocked);
        Assert.Contains(AlwaysOn, later.AlreadyCompleted);
        Assert.Equal(firstMonth, MarkOf(state, AlwaysOn).CompletedAt);
    }

    [Fact]
    public void 难度口径取当前难度而非待切换难度()
    {
        var state = StateOf(Money.FromGuan(50_000m), Difficulty.Normal);

        // 结算第①步会**消化**待切换难度（`PendingDifficulty` 提升为 `Difficulty`），
        // 故「未生效的切换」只能在结算之后排入——判定的难度口径仍取当时的 `state.Difficulty`。
        var settlement = Settle(state);
        state.PendingDifficulty = Difficulty.Hell;

        AchievementAdjudicator.Judge(state, settlement, [], []);

        // 未生效的难度切换不改变成就归属（契约二十二 §2 条款 2）。
        Assert.Equal(Difficulty.Normal, MarkOf(state, AlwaysOn).Difficulty);
    }

    [Fact]
    public void 结束态判定返回空结果且零状态变更()
    {
        var state = StateOf(Money.FromGuan(50_000m), Difficulty.Normal);
        var settlement = Settle(state);
        var before = Describe(state.Achievements);

        state.Ending.End(state.CurrentDate, ChronicleBuilder.Build(state));

        var outcome = AchievementAdjudicator.Judge(state, settlement, [], []);

        Assert.Empty(outcome.NewlyUnlocked);
        Assert.Empty(outcome.AlreadyCompleted);
        Assert.False(outcome.ExtinctionJustTriggered);
        Assert.Equal(before, Describe(state.Achievements));
    }

    [Fact]
    public void 绝嗣在同月的成就判定之后补判()
    {
        var member = RulesHarness.Member(1, Gender.Male, 40);
        var family = RulesHarness.FamilyOf(member);
        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(Money.FromGuan(50_000m)),
            new Holdings());
        var state = new GameState(StateId, RulesHarness.Date, Difficulty.Hell, Origin.Scholar, family, economy);

        var settlement = Settle(state);

        // 判定之前身故 ⇒ 在册成员归零，绝嗣成立。
        member.Status |= StatusFlag.Deceased;

        var outcome = AchievementAdjudicator.Judge(state, settlement, [], []);

        // 先成就、后绝嗣：同月事实照常参与判定，绝嗣在两段之后补判。
        Assert.True(outcome.ExtinctionJustTriggered);
        Assert.Contains(AchievementId.FamilyExtinct, outcome.NewlyUnlocked);
        Assert.Contains(AchievementId.HellEnding, outcome.NewlyUnlocked);
        Assert.True(state.Ending.IsEnded);
    }

    /// <summary>跑一次真实结算，作为判定的当月事实来源。</summary>
    /// <param name="state">游戏状态。</param>
    private static SettlementResult Settle(GameState state)
    {
        var engine = new MonthlySettlementEngine(
            new FixedRandomService(), new GameStateClock(state), new FixedNameGenerator());

        return engine.Settle(state);
    }

    /// <summary>构造只有一名成年男性成员的状态。</summary>
    /// <param name="cash">现金（贯）。</param>
    /// <param name="difficulty">难度。</param>
    private static GameState StateOf(Money cash, Difficulty difficulty)
    {
        var family = RulesHarness.FamilyOf(RulesHarness.Member(1, Gender.Male, 40));
        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(cash),
            new Holdings());

        return new GameState(StateId, RulesHarness.Date, difficulty, Origin.Scholar, family, economy);
    }

    /// <summary>取指定成就的完成标记（不存在则失败）。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="id">成就标识。</param>
    private static AchievementMark MarkOf(GameState state, AchievementId id)
    {
        foreach (var mark in state.Achievements.Completed)
        {
            if (mark.Achievement == id)
            {
                return mark;
            }
        }

        throw new InvalidOperationException($"未找到 {id} 的完成标记。");
    }

    /// <summary>把 8 个持久字段描述成确定性字符串（用于「零状态变更」断言）。</summary>
    /// <param name="achievements">成就状态。</param>
    private static string Describe(AchievementState achievements)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append(achievements.SuccessfulBribeCount).Append('|')
            .Append(achievements.BirthCount).Append('|')
            .Append(achievements.PeakTotalAssets.Wen).Append('|')
            .Append(achievements.EverHadLoan).Append('|')
            .Append(achievements.DebugWakeCount).Append('|')
            .Append(achievements.Lifespans.Count).Append('|')
            .Append(achievements.Completed.Count).Append('|')
            .Append(achievements.PendingUnlocks.Count);

        return builder.ToString();
    }
}
