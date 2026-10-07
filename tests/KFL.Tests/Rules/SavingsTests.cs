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
/// US3 / quickstart S4：当年储蓄利率（1 月 roll、当年不变、次年重 roll）、12 月计息并入本金
/// （复利）与工出身 bonus 的基数与系数口径（FR-010、FR-012、FR-014；§5.2、§5.4）。
/// </summary>
/// <remarks>
/// 期望值一律经 <c>KFL.Rules/Config</c> 的成员取得，本文件 MUST NOT 另存规则数值（SC-008）。
/// </remarks>
public class SavingsTests
{
    private static readonly Guid StateId = Guid.Parse("4c6e8a0b-2d4f-4a6c-9e8d-1a2b3c4d5e6f");
    private static readonly GameDate January = new(80, 1);
    private static readonly GameDate December = new(80, 12);

    [Fact]
    public void 一月roll出当年利率且当年不变次年重新roll()
    {
        var state = NewState(January, Origin.Scholar, cash: Guan(1000m), savings: Guan(100m));

        // 随机槽位：米价（1 月）→ 储蓄利率（1 月）→ 米价（2~12 月）→ 米价（次年 1 月）→ 储蓄利率（次年 1 月）。
        var values = new List<double> { 0.0d, 0.0d };

        for (var index = 0; index < 12; index++)
        {
            values.Add(0.5d);  // arch-guard:allow 夹具随机取值（非规则数值副本）
        }

        values.Add(double.MaxValue);

        var engine = new MonthlySettlementEngine(new FixedRandomService(values.ToArray()), new GameStateClock(state));
        var first = engine.Settle(state);

        // 米价先被消费：若次序颠倒，本月米价拿到的会是储蓄利率的那个取值。
        Assert.Equal(GrainPricePolicy.Walk(GrainPricePolicy.Initial, 0.0d), first.GrainPriceIndexAfter);
        Assert.Equal(InterestPolicy.SavingsRate.Min, state.Economy.Treasury.SavingsRate);
        Assert.Equal(January.Year, state.Economy.Treasury.SavingsRateYear);

        // 当年内不变（含 12 月计息月）。
        for (var month = 2; month <= 12; month++)
        {
            var step = engine.Settle(state);

            Assert.Equal(InterestPolicy.SavingsRate.Min, state.Economy.Treasury.SavingsRate);
            Assert.Equal(January.Year, state.Economy.Treasury.SavingsRateYear);

            if (month == 12)
            {
                Assert.True(step.SavingsInterest.IsPositive);
            }
        }

        // 次年 1 月重新 roll，与上年取值无关（上一个取值是极大值 → 区间上限）。
        engine.Settle(state);

        Assert.Equal(InterestPolicy.SavingsRate.Max, state.Economy.Treasury.SavingsRate);
        Assert.Equal(January.Year + 1, state.Economy.Treasury.SavingsRateYear);
    }

    [Fact]
    public void 十二月利息按当年利率并入储蓄本金且次年以并入后本金为基数()
    {
        var savings = Guan(100m);
        var rate80 = InterestPolicy.SavingsRate.Max;
        var state = NewState(January, Origin.Scholar, cash: Guan(1000m), savings: savings);

        // 第 1 个取值给米价，第 2 个给 1 月的储蓄利率 roll（极大值 → 区间上限）；其余月份回落中值。
        var engine = new MonthlySettlementEngine(new FixedRandomService(0.5d, 1.0d), new GameStateClock(state));  // arch-guard:allow 夹具随机取值（非规则数值副本）

        var interest80 = DecemberInterest(engine, state);

        Assert.Equal(savings * rate80, interest80);
        Assert.Equal(savings + interest80, state.Economy.Treasury.Savings);

        var rate81 = InterestPolicy.RateFor(0.5d);  // arch-guard:allow 夹具随机取值（非规则数值副本）
        var interest81 = DecemberInterest(engine, state);

        // 复利：次年的计息基数是「本金 + 上年并入的利息」。
        Assert.Equal((savings + interest80) * rate81, interest81);
        Assert.Equal(savings + interest80 + interest81, state.Economy.Treasury.Savings);
        Assert.Equal(rate81, state.Economy.Treasury.SavingsRate);

        static Money DecemberInterest(MonthlySettlementEngine runner, GameState target)
        {
            var interest = Money.Zero;

            for (var index = 0; index < 12; index++)
            {
                var step = runner.Settle(target);

                if (step.Month.Month == 12)
                {
                    interest = step.SavingsInterest;
                }
            }

            return interest;
        }
    }

    [Fact]
    public void 储蓄利息不乘难度收益系数而工出身bonus乘()
    {
        var easy = SettleDecember(Difficulty.Easy, cash: Guan(100m), savings: Guan(100m));
        var hell = SettleDecember(Difficulty.Hell, cash: Guan(100m), savings: Guan(100m));

        // 储蓄利息：四难度同值（§5.2「储蓄利息除外」）。
        Assert.True(easy.SavingsInterest.IsPositive);
        Assert.Equal(easy.SavingsInterest, hell.SavingsInterest);

        // 工出身 bonus：乘难度收益系数，故两难度不同。
        Assert.NotEqual(easy.ArtisanBonus, hell.ArtisanBonus);
        Assert.True(easy.ArtisanBonus > hell.ArtisanBonus);
    }

    [Fact]
    public void 工出身bonus仅在总资产为正时发放()
    {
        // 基数为正：现金 100 贯（无资产、无负债）。
        var positive = SettleDecember(Difficulty.Normal, cash: Guan(100m));

        Assert.Equal(
            Guan(100m) * IncomeRateTable.ArtisanBonusRate * DifficultyRates.RevenueFactor(Difficulty.Normal),
            positive.ArtisanBonus);
        Assert.Single(positive.Entries, e => e.Category == LedgerCategory.ArtisanBonus);

        // 市值计入基数：只有一间铺面时，基数 = 铺面市值 + 本月铺面租（收入在第③步先于 bonus 入账）。
        var byAssets = SettleDecember(Difficulty.Normal, shops: 1);

        Assert.Equal(
            (AssetPriceTable.MarketValue(RulesHarness.HoldingsOf(shops: 1)) + AssetPriceTable.ShopMonthlyRent(1))
                * IncomeRateTable.ArtisanBonusRate
                * DifficultyRates.RevenueFactor(Difficulty.Normal),
            byAssets.ArtisanBonus);

        // 基数为 0 → 本年不发，且不落 0 元条目。
        var zero = SettleDecember(Difficulty.Normal);

        Assert.Equal(Money.Zero, zero.ArtisanBonus);
        Assert.DoesNotContain(zero.Entries, e => e.Category == LedgerCategory.ArtisanBonus);

        // 基数为负（负债 50 贯）→ 本年不发。
        var negative = SettleDecember(Difficulty.Normal, loanPrincipal: Guan(50m));

        Assert.Equal(Money.Zero, negative.ArtisanBonus);
        Assert.DoesNotContain(negative.Entries, e => e.Category == LedgerCategory.ArtisanBonus);

        // 非工出身 → 任何情形都不发（即使基数为正）。
        var farmer = SettleDecember(Difficulty.Normal, origin: Origin.Farmer, cash: Guan(100m));

        Assert.Equal(Money.Zero, farmer.ArtisanBonus);
        Assert.DoesNotContain(farmer.Entries, e => e.Category == LedgerCategory.ArtisanBonus);
    }

    [Fact]
    public void bonus基数不含商本池且年度条目皆为家族级()
    {
        var withoutMerchant = SettleDecember(Difficulty.Normal, cash: Guan(100m), savings: Guan(50m));
        var withMerchant = SettleDecember(
            Difficulty.Normal, cash: Guan(100m), savings: Guan(50m), merchant: Guan(1000m));

        // 商本池加大而其他不变 → bonus 不变（基数不含商本池）。
        Assert.True(withoutMerchant.ArtisanBonus.IsPositive);
        Assert.Equal(withoutMerchant.ArtisanBonus, withMerchant.ArtisanBonus);

        // 家族级：储蓄利息与工 bonus 的 PersonId 皆为 null（无法归到某一个成员）。
        Assert.All(
            withoutMerchant.Entries.Where(
                e => e.Category is LedgerCategory.SavingsInterest or LedgerCategory.ArtisanBonus),
            e => Assert.Null(e.PersonId));
    }

    [Fact]
    public void 点不动资金池的计息仍使储蓄条目入账且归属储蓄()
    {
        var savings = Guan(100m);
        var state = NewState(January, Origin.Scholar, cash: Guan(1000m), savings: savings);
        var engine = new MonthlySettlementEngine(new FixedRandomService(0.5d), new GameStateClock(state));  // arch-guard:allow 夹具随机取值（非规则数值副本）
        var interest = Money.Zero;

        for (var index = 0; index < 12; index++)
        {
            interest = engine.Settle(state).SavingsInterest;
        }

        var entry = Assert.Single(state.Economy.Ledger.Entries, e => e.Category == LedgerCategory.SavingsInterest);

        Assert.Equal(interest, entry.Amount);
        Assert.Equal(LedgerCreditTarget.Savings, LedgerCategoryMetadata.CreditTargetOf(LedgerCategory.SavingsInterest));
        Assert.Equal(savings + interest, state.Economy.Treasury.Savings);
        Assert.Equal(InterestPolicy.RateFor(0.5d), state.Economy.Treasury.SavingsRate);  // arch-guard:allow 夹具随机取值（非规则数值副本）
    }

    private static SettlementResult SettleDecember(
        Difficulty difficulty,
        Origin origin = Origin.Artisan,
        Money cash = default,
        Money savings = default,
        Money merchant = default,
        int shops = 0,
        Money loanPrincipal = default)
    {
        var holdings = RulesHarness.HoldingsOf(shops: shops);
        var treasury = new Treasury(cash, savings, merchant) { Loan = LoanOf(loanPrincipal) };

        // 12 月不 roll 利率，故直接注入当年利率（赋值次序固定：先年份、再利率）。
        treasury.SavingsRateYear = December.Year;
        treasury.SavingsRate = InterestPolicy.SavingsRate.Min;

        var economy = new FamilyEconomy(
            LivingCostTable.InitialStandard, new GrainPriceIndex(GrainPricePolicy.Initial), treasury, holdings);

        return Settle(NewState(December, origin, economy, difficulty));
    }

    private static GameState NewState(
        GameDate date,
        Origin origin,
        Money cash = default,
        Money savings = default,
        Money merchant = default) =>
        NewState(
            date,
            origin,
            new FamilyEconomy(
                LivingCostTable.InitialStandard,
                new GrainPriceIndex(GrainPricePolicy.Initial),
                new Treasury(cash, savings, merchant)),
            Difficulty.Normal);

    private static GameState NewState(
        GameDate date, Origin origin, FamilyEconomy economy, Difficulty difficulty)
    {
        // 单名儿童成员：无收入来源，故现金与储蓄的变动只可能来自年度项与本月的口粮。
        var family = RulesHarness.FamilyOf(RulesHarness.Member(91, Gender.Male, 8));

        return new GameState(StateId, date, difficulty, origin, family, economy);
    }

    private static SettlementResult Settle(GameState state) =>
        new MonthlySettlementEngine(new FixedRandomService(0.5d), new GameStateClock(state)).Settle(state);  // arch-guard:allow 夹具随机取值（非规则数值副本）

    private static Loan LoanOf(Money principal) => new() { Principal = principal };

    private static Money Guan(decimal value) => Money.FromGuan(value);
}
