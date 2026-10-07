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
/// US1 / quickstart S1、SC-001：六步顺序、米价 clamp、次月生效、净利润、跨年与部分支付。
/// </summary>
public class SettlementEngineTests
{
    private static readonly Guid StateId = Guid.Parse("7ac1f5c9-6f2b-4c8d-9e10-112233445566");

    [Fact]
    public void 收入先于生活费故本月俸禄能先用于口粮()
    {
        var official = RulesHarness.Member(61, Gender.Male, 40);
        official.Rank = new OfficialRank(SalaryTable.LowestLevel);

        var state = NewState(RulesHarness.FamilyOf(official));
        var result = Settle(state);

        var salary = RulesHarness.Guan(
            SalaryTable.MonthlySalaryGuan(SalaryTable.LowestLevel)
            * DifficultyRates.RevenueFactor(Difficulty.Normal));

        Assert.Equal(salary, result.Incomes.Single(l => l.Category == LedgerCategory.OfficialSalary).Amount);
        Assert.True(result.LivingCostPayable > Money.Zero);
        Assert.True(salary > result.LivingCostPayable);

        // 若生活费判定发生在收入之前，现金为 0 的家庭只能部分支付。
        Assert.Equal(result.LivingCostPayable, result.LivingCostPaid);
        Assert.Equal(FamineStage.None, result.FamineAfter.Stage);
        Assert.Equal(result.Month, RulesHarness.Date);
        Assert.Equal(new GameDate(RulesHarness.Date.Year, RulesHarness.Date.Month + 1), state.CurrentDate);
    }

    [Fact]
    public void 米价系数游走并clamp且连续越界以边界为起点()
    {
        var state = NewState(RulesHarness.FamilyOf(RulesHarness.Member(62, Gender.Male, 40)));

        // US3 接入后，1 月的结算在「米价」之后还要消费 1 次「当年储蓄利率」roll
        // （契约三 §4 的固定次序：米价 → 储蓄利率（仅 1 月）→ 贷款计息利率），
        // 故首个取值的槽位依次是：米价（1 月）、储蓄利率（1 月）、米价（2 月）、米价（3 月）。
        var engine = new MonthlySettlementEngine(
            new FixedRandomService(0.0d, 0.5d, 0.999_999d, 0.0d), new GameStateClock(state));  // arch-guard:allow 夹具随机取值（非规则数值副本）

        var first = engine.Settle(state);

        Assert.Equal(
            GrainPricePolicy.Clamp(GrainPricePolicy.Initial * (1m - (GrainPricePolicy.WalkAmplitude / 2m))),
            first.GrainPriceIndexAfter);
        Assert.Equal(first.GrainPriceIndexAfter, state.Economy.GrainPriceIndex.Value);
        Assert.True(first.GrainPriceIndexAfter >= GrainPricePolicy.Min);
        Assert.True(first.GrainPriceIndexAfter <= GrainPricePolicy.Max);

        // 连续上行至越界：以边界为起点回弹，而不是以「被截断前的假想值」为起点。
        state.Economy.GrainPriceIndex = new GrainPriceIndex(GrainPricePolicy.Max);
        var atMax = engine.Settle(state);

        Assert.Equal(GrainPricePolicy.Max, atMax.GrainPriceIndexAfter);

        var down = engine.Settle(state);

        Assert.True(down.GrainPriceIndexAfter < GrainPricePolicy.Max);
        Assert.Equal(GrainPricePolicy.Min, GrainPricePolicy.Clamp(GrainPricePolicy.Min / 2m));
    }

    [Fact]
    public void 米价系数当月生效并进入生活费应付额()
    {
        var family = RulesHarness.FamilyOf(RulesHarness.Member(63, Gender.Male, 40));
        var state = NewState(family);

        var result = Settle(state);

        var expected = LivingCostCalculator.Compute(
            CountedMembers.Counted(family),
            result.Month,
            state.Economy.LivingStandard,
            result.GrainPriceIndexAfter,
            state.Difficulty,
            state.Origin,
            FamineStage.None);

        Assert.Equal(expected.Payable, result.LivingCostPayable);
        Assert.Equal(expected.Lines, result.LivingCosts);
    }

    [Fact]
    public void 生活费档位与难度次月生效()
    {
        var family = RulesHarness.FamilyOf(RulesHarness.Member(64, Gender.Male, 40));
        var state = NewState(family);

        var first = Settle(state);

        Assert.Equal(
            LivingCostTable.DailyCost(LivingStandard.Normal, AgeBracket.Adult),
            first.LivingCosts.Single(l => l.AgeBracket == AgeBracket.Adult).DailyCost);
        Assert.Equal(LivingStandard.Normal, state.Economy.LivingStandard);

        state.Economy.PendingLivingStandard = LivingStandard.Frugal;
        state.PendingDifficulty = Difficulty.Hard;

        var second = Settle(state);

        Assert.Equal(
            LivingCostTable.DailyCost(LivingStandard.Frugal, AgeBracket.Adult),
            second.LivingCosts.Single(l => l.AgeBracket == AgeBracket.Adult).DailyCost);

        var expected = LivingCostCalculator.Compute(
            CountedMembers.Counted(family),
            second.Month,
            LivingStandard.Frugal,
            second.GrainPriceIndexAfter,
            Difficulty.Hard,
            state.Origin,
            FamineStage.None);

        Assert.Equal(expected.Payable, second.LivingCostPayable);
        Assert.Equal(LivingStandard.Frugal, state.Economy.LivingStandard);
        Assert.Null(state.Economy.PendingLivingStandard);
        Assert.Equal(Difficulty.Hard, state.Difficulty);
        Assert.Null(state.PendingDifficulty);
    }

    [Fact]
    public void 净利润为全部收入减实付生活费()
    {
        var fixture = EconomyFixtures.Create();
        var state = NewState(fixture.Family, holdings: fixture.Economy.Holdings);
        var result = Settle(state);

        var incomeTotal = Money.Zero;

        foreach (var line in result.Incomes)
        {
            incomeTotal += line.Amount;
        }

        Assert.Equal(incomeTotal - result.LivingCostPaid, result.NetProfit);
    }

    [Fact]
    public void 十二月结算后跨年进位至次年一月()
    {
        var december = new GameDate(80, 12);
        var state = NewState(RulesHarness.FamilyOf(RulesHarness.Member(65, Gender.Male, 40)), date: december);

        var result = Settle(state);

        Assert.Equal(december, result.Month);
        Assert.Equal(new GameDate(81, 1), state.CurrentDate);
    }

    [Fact]
    public void 本次条目与账本新增逐条相同且池变动等于资金类条目之和()
    {
        var fixture = EconomyFixtures.Create();
        var state = NewState(fixture.Family, holdings: fixture.Economy.Holdings, cash: Money.FromGuan(250m));

        var engine = new MonthlySettlementEngine(
            new FixedRandomService(0.5d, 0.5d, 0.5d), new GameStateClock(state));  // arch-guard:allow 夹具随机取值（非规则数值副本）

        for (var month = 0; month < 3; month++)
        {
            var firstIndex = state.Economy.Ledger.Entries.Count;
            var result = engine.Settle(state);

            var appended = state.Economy.Ledger.Entries.Skip(firstIndex).ToArray();

            Assert.Equal(result.Entries, appended);
            Assert.Equal(result.TreasuryPoolAfter - result.TreasuryPoolBefore, TreasuryDelta(result.Entries));
            Assert.False(result.TreasuryPoolAfter.IsNegative);
        }
    }

    [Fact]
    public void 付不起时部分支付且不出现负余额也不转贷款()
    {
        var child = RulesHarness.Member(66, Gender.Male, 8);
        var state = NewState(RulesHarness.FamilyOf(child));

        var result = Settle(state);

        Assert.True(result.LivingCostPayable > Money.Zero);
        Assert.Equal(Money.Zero, result.LivingCostPaid);
        Assert.DoesNotContain(result.Entries, e => e.Category == LedgerCategory.LivingCost);
        Assert.Equal(Money.Zero, state.Economy.Treasury.Cash);
        Assert.Equal(Money.Zero, state.Economy.Treasury.Savings);
        Assert.True(state.Economy.Treasury.Loan.IsSettled);
        Assert.Equal(result.TreasuryPoolBefore, result.TreasuryPoolAfter);
    }

    [Fact]
    public void 现金不足应付额时扣尽现金与储蓄且缺口不入账()
    {
        var child = RulesHarness.Member(67, Gender.Male, 8);
        var family = RulesHarness.FamilyOf(child);

        var payable = LivingCostCalculator.Compute(
            CountedMembers.Counted(family),
            RulesHarness.Date,
            LivingStandard.Normal,
            GrainPricePolicy.Initial,
            Difficulty.Normal,
            Origin.Artisan,
            FamineStage.None).Payable;

        var partial = RulesHarness.Guan(0.05m);

        Assert.True(partial < payable);

        var state = NewState(family, cash: partial);
        var result = Settle(state);

        Assert.Equal(result.LivingCostPayable, payable);
        Assert.Equal(partial, result.LivingCostPaid);
        Assert.Equal(Money.Zero, state.Economy.Treasury.Cash);
        Assert.Equal(Money.Zero, state.Economy.Treasury.Savings);
        Assert.True(state.Economy.Treasury.Loan.IsSettled);

        var livingCostEntry = Assert.Single(result.Entries, e => e.Category == LedgerCategory.LivingCost);

        Assert.Equal(-partial, livingCostEntry.Amount);
    }

    private static GameState NewState(
        Family family,
        Origin origin = Origin.Artisan,
        Difficulty difficulty = Difficulty.Normal,
        LivingStandard standard = LivingStandard.Normal,
        decimal grain = GrainPricePolicy.Initial,
        Money cash = default,
        Money savings = default,
        Money merchant = default,
        Holdings? holdings = null,
        GameDate? date = null)
    {
        var economy = new FamilyEconomy(
            standard,
            new GrainPriceIndex(grain),
            new Treasury(cash, savings, merchant),
            holdings ?? new Holdings());

        return new GameState(StateId, date ?? RulesHarness.Date, difficulty, origin, family, economy);
    }

    private static SettlementResult Settle(GameState state) =>
        new MonthlySettlementEngine(new FixedRandomService(0.5d), new GameStateClock(state)).Settle(state);  // arch-guard:allow 夹具随机取值（非规则数值副本）

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
}
