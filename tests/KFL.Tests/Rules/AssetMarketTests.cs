using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Services;
using KFL.Rules.Config;
using KFL.Rules.Economy;
using KFL.Rules.Settlement;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// T057：资产买卖入口（FR-027；规格书 §5.3；research R-15）——购售同价、资金池与持有量同步、
/// 卖出后对应收入来源退化、资产卖光后结算不中断、资金不足即拒绝且不产生贷款。
/// </summary>
/// <remarks>
/// 期望值一律经 <c>KFL.Rules/Config</c> 的成员取得（单价一律走
/// <see cref="AssetPriceTable.UnitPrice"/>），本文件 MUST NOT 另存规则数值（SC-008）。
/// </remarks>
public class AssetMarketTests
{
    private static readonly Guid StateId = Guid.Parse("9f1c3d5e-7a2b-4c8d-9e0f-1a2b3c4d5e6f");

    /// <summary>十一月之外唯一有年度项的月份（工 bonus 基数要用它）。</summary>
    private static readonly GameDate December = new(80, 12);

    /// <summary>四种资产（次序固定，便于「卖光」时逐一处理）。</summary>
    private static readonly AssetKind[] Assets =
        [AssetKind.Farmland, AssetKind.RuralHouse, AssetKind.UrbanHouse, AssetKind.Shop];

    [Fact]
    public void 买入与售出同价且资金池与持有量同步变动()
    {
        var fixture = Fixture();
        var state = NewState(fixture);
        var ledger = state.Economy.Ledger;
        var poolBefore = state.Economy.TreasuryPool;
        var farmlandMu = state.Economy.Holdings.FarmlandMu;

        // 买入：金额 = 数量 × 单价，落一条 AssetPurchase（资金类、家族级、归属当月）。
        var paid = AssetMarket.Buy(state, AssetKind.Farmland, 5);
        var unit = AssetPriceTable.UnitPrice(AssetKind.Farmland);

        Assert.Equal(unit * 5, paid);
        Assert.Equal(poolBefore - paid, state.Economy.TreasuryPool);
        Assert.Equal(farmlandMu + 5, state.Economy.Holdings.FarmlandMu);

        var purchase = Assert.Single(ledger.Entries);

        Assert.Equal(LedgerCategory.AssetPurchase, purchase.Category);
        Assert.Equal(-paid, purchase.Amount);
        Assert.Equal(state.CurrentDate, purchase.Date);
        Assert.Null(purchase.PersonId);
        Assert.Equal(LedgerEntryKind.Treasury, LedgerCategoryMetadata.KindOf(purchase.Category));

        // 售出：**同一单价**入账，落一条 AssetSale。
        var shops = state.Economy.Holdings.Shops;
        var received = AssetMarket.Sell(state, AssetKind.Shop, 1);

        Assert.Equal(AssetPriceTable.UnitPrice(AssetKind.Shop), received);
        Assert.Equal(poolBefore - paid + received, state.Economy.TreasuryPool);
        Assert.Equal(shops - 1, state.Economy.Holdings.Shops);

        var sale = ledger.Entries[^1];

        Assert.Equal(LedgerCategory.AssetSale, sale.Category);
        Assert.Equal(received, sale.Amount);
        Assert.Equal(state.CurrentDate, sale.Date);
        Assert.Null(sale.PersonId);

        // 两条买卖条目正是本月资金池的全部变动（SC-005 的等式在资产买卖路径上同样成立）。
        Assert.Equal(received - paid, ledger.TreasuryDeltaIn(state.CurrentDate, state.CurrentDate));
    }

    [Fact]
    public void 卖出后对应收入来源退化到零()
    {
        var state = AssetState();
        var before = Income(state);
        var craftingBefore = before.Lines.Count(line => line.Category == LedgerCategory.CraftingIncome);
        var selfFarmingBefore = before.Lines.Count(line => line.Category == LedgerCategory.SelfFarmingIncome);

        Assert.Contains(before.Lines, line => line.Category == LedgerCategory.LandRentIncome);
        Assert.Contains(before.Lines, line => line.Category == LedgerCategory.ShopRentIncome);
        Assert.True(selfFarmingBefore > 0);
        Assert.True(craftingBefore > 1, "持有城市宅且有人做工时 SHOULD 既做工钱又有城市宅加成。");
        Assert.True(AssetPriceTable.MarketValue(state.Economy.Holdings).IsPositive);

        SellAll(state);

        var after = Income(state);

        // 自耕、田租与铺面租消失；城市宅加成消失（做工月额本身仍在）。
        Assert.DoesNotContain(after.Lines, line => line.Category == LedgerCategory.SelfFarmingIncome);
        Assert.DoesNotContain(after.Lines, line => line.Category == LedgerCategory.LandRentIncome);
        Assert.DoesNotContain(after.Lines, line => line.Category == LedgerCategory.ShopRentIncome);
        Assert.True(after.Lines.Count(line => line.Category == LedgerCategory.CraftingIncome) < craftingBefore);

        // 工 bonus 基数里的市值项退化为 0。
        Assert.Equal(Money.Zero, AssetPriceTable.MarketValue(state.Economy.Holdings));

        // 无田可耕（E-17）→ 务农收入出现：这是「卖出后收入来源退化」的另一面，MUST NOT 被当成收入消失。
        Assert.Contains(after.Lines, line => line.Category == LedgerCategory.FarmingWageIncome);
    }

    [Fact]
    public void 工bonus基数中的市值项随资产卖光而消失()
    {
        var withAssets = SettleDecember(NewState(Fixture(new EconomyFixtureSettings
        {
            Date = December,
            Cash = Guan(1000m),
            Savings = Money.Zero,
            MerchantCapital = Money.Zero,
            LoanPrincipal = Money.Zero,
            LoanAccruedInterest = Money.Zero,
        })));

        var soldOutState = NewState(Fixture(new EconomyFixtureSettings
        {
            Date = December,
            Cash = Guan(1000m),
            Savings = Money.Zero,
            MerchantCapital = Money.Zero,
            LoanPrincipal = Money.Zero,
            LoanAccruedInterest = Money.Zero,
            FarmlandMu = 0,
            RuralHouses = 0,
            UrbanHouses = 0,
            Shops = 0,
        }));
        var soldOut = SettleDecember(soldOutState);

        Assert.True(withAssets.ArtisanBonus.IsPositive);
        Assert.True(soldOut.ArtisanBonus.IsPositive);
        Assert.True(withAssets.ArtisanBonus > soldOut.ArtisanBonus);

        // 无资产时基数 = 现金 + 储蓄 − 本金 − 欠息（**不含市值**）。工 bonus 在生活费**之前**发放
        // （契约三 §6 的③-b 早于④），故由结算后的余额反推基数时 MUST 把本月实付生活费加回来。
        var treasury = soldOutState.Economy.Treasury;
        var baseAtBonus = treasury.Cash
            + treasury.Savings
            + soldOut.LivingCostPaid
            - treasury.Loan.Principal
            - treasury.Loan.AccruedInterest
            - soldOut.ArtisanBonus;

        Assert.Equal(
            baseAtBonus * IncomeRateTable.ArtisanBonusRate * DifficultyRates.RevenueFactor(Difficulty.Normal),
            soldOut.ArtisanBonus);
    }

    [Fact]
    public void 资产卖光或从未持有时结算不中断()
    {
        // ① 从未持有：零资产存档照常结算。
        var none = NewState(Fixture(new EconomyFixtureSettings
        {
            FarmlandMu = 0,
            RuralHouses = 0,
            UrbanHouses = 0,
            Shops = 0,
        }));
        var noneEngine = new MonthlySettlementEngine(new FixedRandomService(0.5d), new GameStateClock(none));  // arch-guard:allow 夹具随机取值（非规则数值副本）

        for (var index = 0; index < 3; index++)
        {
            var step = noneEngine.Settle(none);

            Assert.False(step.TreasuryPoolAfter.IsNegative);
            Assert.DoesNotContain(step.Incomes, line => line.Category == LedgerCategory.ShopRentIncome);
        }

        // ② 全部卖光：此后结算照常，且不再产生任何资产相关收入。
        var sold = NewState(Fixture());
        SellAll(sold);

        var soldEngine = new MonthlySettlementEngine(new FixedRandomService(0.5d), new GameStateClock(sold));  // arch-guard:allow 夹具随机取值（非规则数值副本）

        for (var index = 0; index < 3; index++)
        {
            var step = soldEngine.Settle(sold);

            Assert.False(step.TreasuryPoolAfter.IsNegative);
            Assert.DoesNotContain(step.Incomes, line => line.Category == LedgerCategory.LandRentIncome);
            Assert.DoesNotContain(step.Incomes, line => line.Category == LedgerCategory.ShopRentIncome);
        }
    }

    [Fact]
    public void 资金不足时拒绝且不产生贷款()
    {
        var fixture = Fixture(new EconomyFixtureSettings
        {
            Cash = Money.Zero,
            Savings = Guan(1m),
            MerchantCapital = Money.Zero,
            LoanPrincipal = Money.Zero,
            LoanAccruedInterest = Money.Zero,
        });
        var state = NewState(fixture);
        var ledger = state.Economy.Ledger;
        var shops = state.Economy.Holdings.Shops;

        Assert.Throws<InvalidOperationException>(() => AssetMarket.Buy(state, AssetKind.Shop, 1));

        // 拒绝 = 不动任何状态：池、持有量、账本皆原样，且**不产生贷款**（§9.5/§5.4）。
        Assert.Equal(Guan(1m), state.Economy.TreasuryPool);
        Assert.Equal(shops, state.Economy.Holdings.Shops);
        Assert.Empty(ledger.Entries);
        Assert.True(state.Economy.Treasury.Loan.IsSettled);
        Assert.Equal(Money.Zero, state.Economy.Treasury.Loan.Principal);

        // 数量非法：买 / 卖 0 个都 MUST NOT 产生 0 元条目。
        Assert.Throws<ArgumentOutOfRangeException>(() => AssetMarket.Buy(state, AssetKind.Farmland, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AssetMarket.Sell(state, AssetKind.Farmland, 0));

        // 超卖：持有量不足即拒绝，MUST NOT 有任何资金流入。
        var mu = state.Economy.Holdings.FarmlandMu;

        Assert.Throws<InvalidOperationException>(() => AssetMarket.Sell(state, AssetKind.Farmland, mu + 1));
        Assert.Equal(mu, state.Economy.Holdings.FarmlandMu);
        Assert.Equal(Guan(1m), state.Economy.TreasuryPool);
        Assert.Empty(ledger.Entries);

        // 可付额 = 现金 + 储蓄（R-05）：储蓄足够时买入成立——「资金不足」的拒绝线就是这条口径。
        var bySavings = NewState(Fixture(new EconomyFixtureSettings
        {
            Cash = Money.Zero,
            Savings = Guan(1000m),
            MerchantCapital = Money.Zero,
        }));
        var shopsBefore = bySavings.Economy.Holdings.Shops;
        var unit = AssetPriceTable.UnitPrice(AssetKind.Shop);

        AssetMarket.Buy(bySavings, AssetKind.Shop, 1);

        Assert.Equal(Guan(1000m) - unit, bySavings.Economy.Treasury.Savings);
        Assert.Equal(Money.Zero, bySavings.Economy.Treasury.Cash);
        Assert.Equal(shopsBefore + 1, bySavings.Economy.Holdings.Shops);
    }

    /// <summary>
    /// 两名成年成员的受控存档：田 <c>每人上限 × 2 + 1</c> 亩（故既有自耕又有田租）、
    /// 城市宅 1 座（做工加成）与铺面 1 间（铺面租），其中一名成员指派「做工」。
    /// </summary>
    private static GameState AssetState()
    {
        var worker = RulesHarness.Member(11, Gender.Male, 30, agriculture: 50, craft: 50);
        var other = RulesHarness.Member(12, Gender.Male, 25, agriculture: 50, craft: 50);
        var family = RulesHarness.FamilyOf(worker, other);

        worker.Occupation = Occupation.Crafting;

        var farmlandMu = (IncomeRateTable.FarmlandPerCapitaMu * 2) + 1;
        var economy = new FamilyEconomy(
            LivingCostTable.InitialStandard,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(Guan(1000m)),
            RulesHarness.HoldingsOf(farmlandMu: farmlandMu, urbanHouses: 1, shops: 1));

        return new GameState(
            StateId, RulesHarness.Date, Difficulty.Normal, Origin.Artisan, family, economy);
    }

    private static void SellAll(GameState state)
    {
        foreach (var kind in Assets)
        {
            var count = state.Economy.Holdings.CountOf(kind);

            if (count > 0)
            {
                AssetMarket.Sell(state, kind, count);
            }
        }
    }

    private static IncomeComputation Income(GameState state) => IncomeCalculator.Compute(
        state.Family,
        state.CurrentDate,
        state.Economy.Holdings,
        state.Economy.Treasury,
        state.Difficulty,
        state.Origin);

    private static SettlementResult SettleDecember(GameState state) =>
        new MonthlySettlementEngine(new FixedRandomService(0.5d), new GameStateClock(state)).Settle(state);  // arch-guard:allow 夹具随机取值（非规则数值副本）

    private static EconomyFixture Fixture(EconomyFixtureSettings? settings = null) =>
        EconomyFixtures.Create(settings ?? new EconomyFixtureSettings());

    private static GameState NewState(EconomyFixture fixture) =>
        new(StateId, fixture.Date, Difficulty.Normal, Origin.Artisan, fixture.Family, fixture.Economy);

    private static Money Guan(decimal value) => Money.FromGuan(value);
}
