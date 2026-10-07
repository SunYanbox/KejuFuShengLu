using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Services;
using KFL.Rules.Config;
using KFL.Rules.Settlement;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// US4 / quickstart S5、SC-003、规格书 §16 必测三项之一：饥馑四阶段的流转与计时边界（E-14）、
/// 「先解除、后升级」、救济期减免进一般乘区与事件条目口径（FR-018、FR-019；§5.4）。
/// </summary>
/// <remarks>
/// 期望值一律经 <c>KFL.Rules/Config</c> 的成员取得，本文件 MUST NOT 另存规则数值（SC-008）。
/// </remarks>
public class FamineTimelineTests
{
    private static readonly Guid StateId = Guid.Parse("6d8f0a2c-4e6a-4c8e-8a1b-3c4d5e6f7a8b");
    private static readonly GameDate Date = RulesHarness.Date;

    [Fact]
    public void 饥馑四阶段按转移表逐月推进()
    {
        var state = NewState(FamilyOfChildren(1));
        var engine = NewEngine(state);

        Assert.Equal(FamineStage.None, state.Economy.Famine.Stage);

        for (var month = 1; month <= 20; month++)
        {
            var result = engine.Settle(state);
            var (stage, elapsed, transition) = Expected(month);

            Assert.Equal(stage, result.FamineAfter.Stage);
            Assert.Equal(elapsed, result.FamineAfter.ElapsedMonths);

            if (transition is { } category)
            {
                // 每次迁移恰好落一条**事件类**条目，金额语义为 0。
                var entry = Assert.Single(result.Entries, e => e.Category == category);

                Assert.Equal(Money.Zero, entry.Amount);
                Assert.Equal(LedgerEntryKind.Event, LedgerCategoryMetadata.KindOf(category));
            }
            else
            {
                Assert.DoesNotContain(result.Entries, e => IsFamine(e.Category));
            }
        }

        static (FamineStage Stage, int Elapsed, LedgerCategory? Transition) Expected(int month) => month switch
        {
            1 => (FamineStage.Famine, 1, LedgerCategory.FamineEntered),
            2 => (FamineStage.Famine, 2, null),
            3 => (FamineStage.Relief, 1, LedgerCategory.FamineReliefEntered),
            >= 4 and <= 13 => (FamineStage.Relief, month - 2, null),
            14 => (FamineStage.Severe, 1, LedgerCategory.FamineSevereEntered),
            _ => (FamineStage.Severe, month - 13, null),
        };
    }

    [Fact]
    public void 计时边界恰为第三个饥馑月与第十二个救济月()
    {
        var state = NewState(FamilyOfChildren(1));
        var engine = NewEngine(state);
        var steps = new List<SettlementResult>();

        for (var month = 0; month < 14; month++)
        {
            steps.Add(engine.Settle(state));
        }

        // 第 1 个饥馑月进入、第 2 个饥馑月 MUST NOT 触发迁移（计时在判定前推进的口径）。
        Assert.Equal(FamineStage.Famine, steps[0].FamineAfter.Stage);
        Assert.Equal(1, steps[0].FamineAfter.ElapsedMonths);
        Assert.Equal(FamineStage.Famine, steps[1].FamineAfter.Stage);
        Assert.Equal(2, steps[1].FamineAfter.ElapsedMonths);
        Assert.DoesNotContain(steps[1].Entries, e => e.Category == LedgerCategory.FamineReliefEntered);

        // 恰好第 3 个饥馑月当月转救济，计时重算为 1。
        Assert.Equal(FamineStage.Relief, steps[2].FamineAfter.Stage);
        Assert.Equal(1, steps[2].FamineAfter.ElapsedMonths);

        // 第 11 个救济月 MUST NOT 触发升级。
        Assert.Equal(FamineStage.Relief, steps[12].FamineAfter.Stage);
        Assert.Equal(11, steps[12].FamineAfter.ElapsedMonths);
        Assert.DoesNotContain(steps[12].Entries, e => e.Category == LedgerCategory.FamineSevereEntered);

        // 恰好第 12 个救济月当月转第三阶段，计时重算为 1。
        Assert.Equal(FamineStage.Severe, steps[13].FamineAfter.Stage);
        Assert.Equal(1, steps[13].FamineAfter.ElapsedMonths);
        Assert.Single(steps[13].Entries, e => e.Category == LedgerCategory.FamineSevereEntered);

        // 第三阶段保持，不再迁移。
        Assert.Equal(FamineStage.Severe, engine.Settle(state).FamineAfter.Stage);
    }

    [Fact]
    public void 任一月付得起即全部解除且不残留任何计时()
    {
        var state = NewState(FamilyOfChildren(1));
        var engine = NewEngine(state);

        engine.Settle(state);
        engine.Settle(state);

        Assert.Equal(FamineStage.Famine, state.Economy.Famine.Stage);
        Assert.Equal(2, state.Economy.Famine.ElapsedMonths);

        // 补足资金（经公开的资金流动通道进账），次月即应全部解除。
        state.Economy.Apply(LedgerCategory.LandRentIncome, null, Guan(100m), state.CurrentDate);

        var resolved = engine.Settle(state);

        Assert.Equal(FamineStage.None, resolved.FamineAfter.Stage);
        Assert.Equal(0, resolved.FamineAfter.ElapsedMonths);
        Assert.Single(resolved.Entries, e => e.Category == LedgerCategory.FamineResolved);

        // 解除后 MUST NOT 留任何阶段性残留：其后月份不再出现任何饥馑事件。
        var after = engine.Settle(state);

        Assert.Equal(FamineStage.None, after.FamineAfter.Stage);
        Assert.DoesNotContain(after.Entries, e => IsFamine(e.Category));
    }

    [Fact]
    public void 付不起时部分支付且缺口不入账不转贷款()
    {
        var family = FamilyOfChildren(10);
        var cash = Guan(0.5m);  // arch-guard:allow 夹具金额与随机取值（非规则数值副本）
        var state = NewState(family, cash: cash, savings: Money.Zero);
        var result = NewEngine(state).Settle(state);

        Assert.True(result.LivingCostPayable > cash);
        Assert.Equal(cash, result.LivingCostPaid);
        Assert.Equal(Money.Zero, state.Economy.Treasury.Cash);
        Assert.Equal(Money.Zero, state.Economy.Treasury.Savings);

        // 条目金额 = 实付额（**不是**应付额）：缺口不入账。
        Assert.Equal(-cash, Assert.Single(result.Entries, e => e.Category == LedgerCategory.LivingCost).Amount);
        Assert.Equal(result.TreasuryPoolBefore - result.LivingCostPaid, result.TreasuryPoolAfter);

        // 不出现负余额，也 MUST NOT 转成贷款（§5.4「不存在主动借贷」）。
        Assert.False(result.TreasuryPoolAfter.IsNegative);
        Assert.True(state.Economy.Treasury.Loan.IsSettled);
        Assert.DoesNotContain(
            result.Entries,
            e => e.Category is LedgerCategory.LoanPrincipalRepaid or LedgerCategory.LoanInterestRepaid);

        // 未足额 → 进入饥馑（第一阶段的迁移同时被记录）。
        Assert.Equal(FamineStage.Famine, result.FamineAfter.Stage);
        Assert.Single(result.Entries, e => e.Category == LedgerCategory.FamineEntered);
    }

    [Fact]
    public void 救济期减免进一般乘区且未成年人乘区为加算结果()
    {
        var relief = Settle(FamineStage.Relief, cash: Guan(100m));
        var normal = Settle(FamineStage.None, cash: Guan(100m));

        var reliefFactor = LivingCostTable.GeneralZoneFactor(isMinor: true, inRelief: true);
        var normalFactor = LivingCostTable.GeneralZoneFactor(isMinor: true, inRelief: false);

        // 救济期折扣与未成年修正在**同一乘区**内**加算**：0.5 → 0.3。
        Assert.Equal(normalFactor - FamineTimeline.ReliefExpenseDiscount, reliefFactor);
        Assert.True(reliefFactor < normalFactor);

        // 减免体现在**条目金额**上（而不是事后修正）。
        Assert.Equal(-relief.LivingCostPayable, Assert.Single(relief.Entries, e => e.Category == LedgerCategory.LivingCost).Amount);
        Assert.True(relief.LivingCostPayable < normal.LivingCostPayable);
        Assert.Equal(normal.LivingCostPayable * (reliefFactor / normalFactor), relief.LivingCostPayable);

        // 付得起 → 解除优先（不升级）。
        Assert.Equal(FamineStage.None, relief.FamineAfter.Stage);
        Assert.Equal(0, relief.FamineAfter.ElapsedMonths);
        Assert.Single(relief.Entries, e => e.Category == LedgerCategory.FamineResolved);
    }

    [Fact]
    public void 每次迁移的事件条目不参与资金类求和()
    {
        var state = NewState(FamilyOfChildren(1));
        var engine = NewEngine(state);

        // 四个阶段的迁移类别皆为事件类，MUST NOT 因阶段迁移而改动资金池。
        Assert.Equal(LedgerEntryKind.Event, LedgerCategoryMetadata.KindOf(LedgerCategory.FamineEntered));
        Assert.Equal(LedgerEntryKind.Event, LedgerCategoryMetadata.KindOf(LedgerCategory.FamineReliefEntered));
        Assert.Equal(LedgerEntryKind.Event, LedgerCategoryMetadata.KindOf(LedgerCategory.FamineSevereEntered));
        Assert.Equal(LedgerEntryKind.Event, LedgerCategoryMetadata.KindOf(LedgerCategory.FamineResolved));

        for (var month = 0; month < 14; month++)
        {
            var result = engine.Settle(state);

            // SC-005：池变动 = 资金类条目之和（事件类条目被排除在外）。
            Assert.Equal(result.TreasuryPoolAfter - result.TreasuryPoolBefore, TreasuryDelta(result.Entries));
        }
    }

    [Fact]
    public void 同月最多迁移一次且计时不重复推进()
    {
        var state = NewState(FamilyOfChildren(1));
        var engine = NewEngine(state);

        // 进入月的计时是「记 1」，且只落一条饥馑事件。
        var entered = engine.Settle(state);

        Assert.Equal(FamineStage.Famine, entered.FamineAfter.Stage);
        Assert.Equal(1, entered.FamineAfter.ElapsedMonths);
        Assert.Single(entered.Entries, e => IsFamine(e.Category));

        // 走到转救济的那一月：同样只落一条事件，且计时**重算为 1**（不是旧计时再 +1）。
        for (var month = 0; month < FamineTimeline.MonthsUntilRelief - 2; month++)
        {
            var step = engine.Settle(state);

            Assert.Equal(FamineStage.Famine, step.FamineAfter.Stage);
            Assert.DoesNotContain(step.Entries, e => IsFamine(e.Category));
        }

        var relief = engine.Settle(state);

        Assert.Equal(FamineStage.Relief, relief.FamineAfter.Stage);
        Assert.Equal(1, relief.FamineAfter.ElapsedMonths);
        Assert.Single(relief.Entries, e => e.Category == LedgerCategory.FamineReliefEntered);
    }

    [Fact]
    public void 阶段与剩余月数可读()
    {
        Assert.Equal(0, FamineTimeline.RemainingMonths(new FamineState()));
        Assert.Equal(FamineTimeline.MonthsUntilRelief, FamineTimeline.LimitMonths(FamineStage.Famine));
        Assert.Equal(FamineTimeline.ReliefMonthsUntilSevere, FamineTimeline.LimitMonths(FamineStage.Relief));
        Assert.Null(FamineTimeline.LimitMonths(FamineStage.None));
        Assert.Null(FamineTimeline.LimitMonths(FamineStage.Severe));

        var famine = new FamineState();
        famine.TransitionTo(FamineStage.Famine);

        Assert.Equal(FamineTimeline.MonthsUntilRelief - 1, FamineTimeline.RemainingMonths(famine));

        famine.Tick();
        famine.Tick();

        Assert.Equal(0, FamineTimeline.RemainingMonths(famine));

        var relief = new FamineState();
        relief.TransitionTo(FamineStage.Relief);

        Assert.Equal(FamineTimeline.ReliefMonthsUntilSevere - 1, FamineTimeline.RemainingMonths(relief));

        for (var index = 0; index < FamineTimeline.ReliefMonthsUntilSevere - 1; index++)
        {
            relief.Tick();
        }

        Assert.Equal(0, FamineTimeline.RemainingMonths(relief));

        var severe = new FamineState();
        severe.TransitionTo(FamineStage.Severe);

        Assert.Equal(0, FamineTimeline.RemainingMonths(severe));
    }

    private static bool IsFamine(LedgerCategory category) => category is
        LedgerCategory.FamineEntered or
        LedgerCategory.FamineReliefEntered or
        LedgerCategory.FamineSevereEntered or
        LedgerCategory.FamineResolved;

    private static SettlementResult Settle(FamineStage famine, Money cash)
    {
        var state = NewState(FamilyOfChildren(1), cash: cash, famine: famine);

        return NewEngine(state).Settle(state);
    }

    private static Family FamilyOfChildren(int count)
    {
        var members = new List<Person>();

        for (var index = 0; index < count; index++)
        {
            // 儿童档：无收入来源、无被指派资格，故生活费之外的现金流为 0。
            members.Add(RulesHarness.Member(95 + index, Gender.Male, 8));
        }

        return RulesHarness.FamilyOf(members.ToArray());
    }

    private static GameState NewState(Family family, Money cash = default, Money savings = default, FamineStage famine = FamineStage.None)
    {
        var economy = new FamilyEconomy(
            LivingCostTable.InitialStandard,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(cash, savings));

        if (famine != FamineStage.None)
        {
            economy.Famine.TransitionTo(famine);
        }

        return new GameState(StateId, Date, Difficulty.Normal, Origin.Scholar, family, economy);
    }

    private static MonthlySettlementEngine NewEngine(GameState state) =>
        new(new FixedRandomService(0.5d), new GameStateClock(state));  // arch-guard:allow 夹具金额与随机取值（非规则数值副本）

    private static Money TreasuryDelta(IReadOnlyList<LedgerEntry> entries)
    {
        var delta = Money.Zero;

        foreach (var entry in entries)
        {
            if (LedgerCategoryMetadata.KindOf(entry.Category) == LedgerEntryKind.Treasury)
            {
                delta += entry.Amount;
            }
        }

        return delta;
    }

    private static Money Guan(decimal value) => Money.FromGuan(value);
}
