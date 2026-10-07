using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Core;

/// <summary>
/// T026：经济实体与存档级状态的边界（data-model §3.1~§3.3、§3.5、§3.7）。
/// </summary>
/// <remarks>
/// 只钉自身不变量与写入通道：金额非负、内部转账不落条目且不透支、<c>Loan</c> 的先本后息与
/// 自然月计时、资产移除不得为负、饥馑「<c>None</c> ⇔ 计时 0」、时间只能经 <c>AdvanceMonth</c> 推进。
/// </remarks>
public class EconomyStateTests
{
    // ------------------------------------------------------------------ Treasury

    [Fact]
    public void Treasury三个金额不得为负()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Treasury(Money.FromGuan(-1m), Money.Zero, Money.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Treasury(Money.Zero, Money.FromGuan(-1m), Money.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Treasury(Money.Zero, Money.Zero, Money.FromGuan(-1m)));

        var treasury = new Treasury(Money.FromGuan(1m), Money.FromGuan(2m), Money.FromGuan(3m));

        Assert.Equal(Money.FromGuan(1m), treasury.Cash);
        Assert.Equal(Money.FromGuan(2m), treasury.Savings);
        Assert.Equal(Money.FromGuan(3m), treasury.MerchantCapital);
        Assert.Equal(Money.Zero, treasury.Loan.Total);
        Assert.True(treasury.Loan.IsSettled);

        var empty = new Treasury();

        Assert.Equal(Money.Zero, empty.Cash);
        Assert.Equal(Money.Zero, empty.Savings);
        Assert.Equal(Money.Zero, empty.MerchantCapital);
        Assert.Null(empty.SavingsRate);
        Assert.Null(empty.SavingsRateYear);
    }

    [Fact]
    public void 内部转账不落条目且无手续费()
    {
        var economy = NewEconomy(Money.FromGuan(10m), Money.FromGuan(2m), Money.FromGuan(3m));
        var treasury = economy.Treasury;
        var poolBefore = economy.TreasuryPool;

        treasury.TransferToSavings(Money.FromGuan(4m));

        Assert.Equal(Money.FromGuan(6m), treasury.Cash);
        Assert.Equal(Money.FromGuan(6m), treasury.Savings);
        Assert.Equal(poolBefore, economy.TreasuryPool);
        Assert.Empty(economy.Ledger.Entries);

        treasury.TransferToCash(Money.FromGuan(1m));

        Assert.Equal(Money.FromGuan(7m), treasury.Cash);
        Assert.Equal(Money.FromGuan(5m), treasury.Savings);

        treasury.InjectMerchantCapital(Money.FromGuan(7m));

        Assert.Equal(Money.Zero, treasury.Cash);
        Assert.Equal(Money.FromGuan(10m), treasury.MerchantCapital);
        Assert.Equal(poolBefore, economy.TreasuryPool);

        treasury.WithdrawMerchantCapital(Money.FromGuan(10m));

        Assert.Equal(Money.FromGuan(10m), treasury.Cash);
        Assert.Equal(Money.Zero, treasury.MerchantCapital);
        Assert.Equal(poolBefore, economy.TreasuryPool);
        Assert.Empty(economy.Ledger.Entries);
    }

    [Fact]
    public void 内部转账超支或负额被拒且不改变任何池()
    {
        var treasury = new Treasury(Money.FromGuan(1m), Money.FromGuan(1m), Money.FromGuan(1m));

        Assert.Throws<InvalidOperationException>(() => treasury.TransferToSavings(Money.FromGuan(2m)));
        Assert.Throws<InvalidOperationException>(() => treasury.TransferToCash(Money.FromGuan(2m)));
        Assert.Throws<InvalidOperationException>(() => treasury.InjectMerchantCapital(Money.FromGuan(2m)));
        Assert.Throws<InvalidOperationException>(() => treasury.WithdrawMerchantCapital(Money.FromGuan(2m)));

        Assert.Throws<ArgumentOutOfRangeException>(() => treasury.TransferToSavings(Money.FromGuan(-1m)));
        Assert.Throws<ArgumentOutOfRangeException>(() => treasury.TransferToCash(Money.FromGuan(-1m)));
        Assert.Throws<ArgumentOutOfRangeException>(() => treasury.InjectMerchantCapital(Money.FromGuan(-1m)));
        Assert.Throws<ArgumentOutOfRangeException>(() => treasury.WithdrawMerchantCapital(Money.FromGuan(-1m)));

        Assert.Equal(Money.FromGuan(1m), treasury.Cash);
        Assert.Equal(Money.FromGuan(1m), treasury.Savings);
        Assert.Equal(Money.FromGuan(1m), treasury.MerchantCapital);
    }

    [Fact]
    public void 储蓄利率与年份必须同时非空且赋值次序固定()
    {
        var treasury = new Treasury();

        // 先利率后年份 MUST 被拒（否则会出现「有利率无年份」的中间态）。
        Assert.Throws<ArgumentException>(() => treasury.SavingsRate = 0.01m);

        treasury.SavingsRateYear = 3;
        treasury.SavingsRate = 0.01m;

        Assert.Equal(0.01m, treasury.SavingsRate.GetValueOrDefault());
        Assert.Equal(3, treasury.SavingsRateYear.GetValueOrDefault());

        // 清空次序相反：年份先清 MUST 被拒。
        Assert.Throws<ArgumentException>(() => treasury.SavingsRateYear = null);

        treasury.SavingsRate = null;
        treasury.SavingsRateYear = null;

        Assert.Null(treasury.SavingsRate);
        Assert.Null(treasury.SavingsRateYear);
    }

    // ------------------------------------------------------------------ Loan

    [Fact]
    public void 贷款先本后息且封顶为债务总额()
    {
        var loan = new Loan { Principal = Money.FromGuan(10m) };
        loan.AccrueInterest(Money.FromGuan(4m));

        Assert.Equal(Money.FromGuan(14m), loan.Total);
        Assert.False(loan.IsSettled);

        // 本金优先冲减，不足部分才冲欠息。
        var first = loan.Repay(Money.FromGuan(6m));

        Assert.Equal(Money.FromGuan(6m), first.PrincipalPart);
        Assert.Equal(Money.Zero, first.InterestPart);
        Assert.Equal(Money.FromGuan(4m), loan.Principal);
        Assert.Equal(Money.FromGuan(4m), loan.AccruedInterest);

        var second = loan.Repay(Money.FromGuan(6m));

        Assert.Equal(Money.FromGuan(4m), second.PrincipalPart);
        Assert.Equal(Money.FromGuan(2m), second.InterestPart);
        Assert.Equal(Money.FromGuan(2m), loan.AccruedInterest);
        Assert.False(loan.IsSettled);

        loan.Repay(Money.FromGuan(2m));

        Assert.True(loan.IsSettled);
        Assert.Equal(Money.Zero, loan.Total);
    }

    [Fact]
    public void 贷款拒绝负额与超过总额的还款()
    {
        var loan = new Loan { Principal = Money.FromGuan(1m) };

        Assert.Throws<ArgumentOutOfRangeException>(() => loan.Repay(Money.FromGuan(-1m)));
        Assert.Throws<ArgumentOutOfRangeException>(() => loan.Repay(Money.FromGuan(2m)));
        Assert.Throws<ArgumentOutOfRangeException>(() => loan.Principal = Money.FromGuan(-1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => loan.MonthsSinceInterest = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => loan.AccrueInterest(Money.FromGuan(-1m)));

        Assert.Equal(Money.FromGuan(1m), loan.Principal);
        Assert.Equal(Money.Zero, loan.AccruedInterest);
    }

    [Fact]
    public void 计息只增欠息不改本金()
    {
        var loan = new Loan { Principal = Money.FromGuan(10m) };

        loan.AccrueInterest(Money.FromGuan(3m));

        Assert.Equal(Money.FromGuan(10m), loan.Principal);
        Assert.Equal(Money.FromGuan(3m), loan.AccruedInterest);
        Assert.Equal(Money.FromGuan(13m), loan.Total);
    }

    [Fact]
    public void 计息计时按自然月且不因还本或计息自动归零()
    {
        var loan = new Loan { Principal = Money.FromGuan(10m) };

        for (var month = 0; month < 11; month++)
        {
            loan.MonthsSinceInterest++;
        }

        Assert.Equal(11, loan.MonthsSinceInterest);

        // 计时只随自然月推进：还本、计息都不清零，清零是计息后的**显式**动作（ResetInterestClock）。
        loan.Repay(Money.FromGuan(1m));
        Assert.Equal(11, loan.MonthsSinceInterest);

        loan.AccrueInterest(Money.FromGuan(1m));
        Assert.Equal(11, loan.MonthsSinceInterest);

        loan.ResetInterestClock();
        Assert.Equal(0, loan.MonthsSinceInterest);

        loan.MonthsSinceInterest = 0;
        Assert.Equal(0, loan.MonthsSinceInterest);
    }

    // ------------------------------------------------------------------ Holdings

    [Fact]
    public void 资产按种类读写且移除至负数被拒()
    {
        var holdings = new Holdings();

        Assert.Equal(0, holdings.CountOf(AssetKind.Farmland));
        Assert.Equal(0, holdings.CountOf(AssetKind.RuralHouse));
        Assert.Equal(0, holdings.CountOf(AssetKind.UrbanHouse));
        Assert.Equal(0, holdings.CountOf(AssetKind.Shop));

        holdings.Add(AssetKind.Farmland, 30);
        holdings.Add(AssetKind.RuralHouse, 1);
        holdings.Add(AssetKind.UrbanHouse, 2);
        holdings.Add(AssetKind.Shop, 3);

        Assert.Equal(30, holdings.CountOf(AssetKind.Farmland));
        Assert.Equal(1, holdings.CountOf(AssetKind.RuralHouse));
        Assert.Equal(2, holdings.CountOf(AssetKind.UrbanHouse));
        Assert.Equal(3, holdings.CountOf(AssetKind.Shop));

        // 派生属性与 CountOf 同一口径。
        Assert.Equal(holdings.FarmlandMu, holdings.CountOf(AssetKind.Farmland));
        Assert.Equal(holdings.RuralHouses, holdings.CountOf(AssetKind.RuralHouse));
        Assert.Equal(holdings.UrbanHouses, holdings.CountOf(AssetKind.UrbanHouse));
        Assert.Equal(holdings.Shops, holdings.CountOf(AssetKind.Shop));

        holdings.Remove(AssetKind.Shop, 2);

        Assert.Equal(1, holdings.CountOf(AssetKind.Shop));

        Assert.Throws<InvalidOperationException>(() => holdings.Remove(AssetKind.Shop, 2));
        Assert.Equal(1, holdings.CountOf(AssetKind.Shop));

        Assert.Throws<ArgumentOutOfRangeException>(() => holdings.Remove(AssetKind.Shop, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => holdings.Add(AssetKind.Shop, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => holdings.FarmlandMu = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => holdings.RuralHouses = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => holdings.UrbanHouses = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => holdings.Shops = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => holdings.CountOf((AssetKind)99));
    }

    // ------------------------------------------------------------------ FamineState

    [Fact]
    public void 饥馑初始为无且计时为零()
    {
        var state = new FamineState();

        Assert.Equal(FamineStage.None, state.Stage);
        Assert.Equal(0, state.ElapsedMonths);
    }

    [Fact]
    public void 饥馑转入当月记一且Tick与Clear维护不变量()
    {
        var state = new FamineState();

        state.TransitionTo(FamineStage.Famine);

        Assert.Equal(FamineStage.Famine, state.Stage);
        Assert.Equal(1, state.ElapsedMonths);

        state.Tick();
        Assert.Equal(2, state.ElapsedMonths);

        // 换阶段时计时从 1 起重算（不是累加）。
        state.TransitionTo(FamineStage.Relief);

        Assert.Equal(FamineStage.Relief, state.Stage);
        Assert.Equal(1, state.ElapsedMonths);

        state.Clear();

        Assert.Equal(FamineStage.None, state.Stage);
        Assert.Equal(0, state.ElapsedMonths);

        // None 阶段 Tick 不改变任何一侧。
        state.Tick();

        Assert.Equal(FamineStage.None, state.Stage);
        Assert.Equal(0, state.ElapsedMonths);

        // TransitionTo(None) 按 Clear 的语义处置（否则会与「None ⇔ 0」冲突）。
        state.TransitionTo(FamineStage.Severe);
        state.TransitionTo(FamineStage.None);

        Assert.Equal(FamineStage.None, state.Stage);
        Assert.Equal(0, state.ElapsedMonths);
    }

    [Fact]
    public void 饥馑阶段与计时零等价()
    {
        var state = new FamineState();

        foreach (var stage in Enum.GetValues<FamineStage>())
        {
            state.TransitionTo(stage);

            Assert.Equal(stage == FamineStage.None, state.ElapsedMonths == 0);
        }
    }

    [Fact]
    public void 饥馑快照复制独立于原状态()
    {
        var source = new FamineState();
        source.TransitionTo(FamineStage.Famine);

        var snapshot = new FamineState(source);

        source.Tick();

        Assert.Equal(FamineStage.Famine, snapshot.Stage);
        Assert.Equal(1, snapshot.ElapsedMonths);
        Assert.Equal(2, source.ElapsedMonths);

        Assert.Throws<ArgumentNullException>(() => new FamineState(null!));
    }

    // ------------------------------------------------------------------ GameState

    [Fact]
    public void 经济聚合只读且时间无公开写入通道()
    {
        var fixture = EconomyFixtures.Create();
        var state = NewState(new GameDate(1, 1), fixture);

        Assert.Same(fixture.Economy, state.Economy);

        var economyProperty = typeof(GameState).GetProperty(nameof(GameState.Economy))
            ?? throw new InvalidOperationException("GameState.Economy MUST 存在。");
        var dateProperty = typeof(GameState).GetProperty(nameof(GameState.CurrentDate))
            ?? throw new InvalidOperationException("GameState.CurrentDate MUST 存在。");

        Assert.True(economyProperty.CanRead);
        Assert.Null(economyProperty.SetMethod);
        Assert.False(dateProperty.CanWrite);
        Assert.Null(dateProperty.SetMethod);
    }

    [Fact]
    public void 推进一月且十二月进位到次年一月()
    {
        var fixture = EconomyFixtures.Create();
        var state = NewState(new GameDate(3, 11), fixture);

        state.AdvanceMonth();

        Assert.Equal(new GameDate(3, 12), state.CurrentDate);

        state.AdvanceMonth();

        Assert.Equal(new GameDate(4, 1), state.CurrentDate);

        for (var month = 0; month < 12; month++)
        {
            state.AdvanceMonth();
        }

        Assert.Equal(new GameDate(5, 1), state.CurrentDate);
    }

    [Fact]
    public void 待生效难度可空可写()
    {
        var fixture = EconomyFixtures.Create();
        var state = NewState(new GameDate(1, 1), fixture);

        Assert.Null(state.PendingDifficulty);

        state.PendingDifficulty = Difficulty.Hard;

        Assert.Equal(Difficulty.Hard, state.PendingDifficulty.GetValueOrDefault());
        Assert.Equal(Difficulty.Normal, state.Difficulty);

        state.PendingDifficulty = null;

        Assert.Null(state.PendingDifficulty);
    }

    /// <summary>存档标识固定字面量（MUST NOT 取全局随机标识源，G-07）。</summary>
    private static readonly Guid AnyId = Guid.Parse("5f1d0a3e-6f2b-4c8d-9e10-112233445566");

    private static GameState NewState(GameDate date, EconomyFixture fixture) => new(
        AnyId, date, Difficulty.Normal, Origin.Farmer, fixture.Family, fixture.Economy);

    /// <summary>
    /// 只给一个合法初值的经济聚合：档位与米价系数是**夹具取值**，产品初值单点在 Rules
    /// （<c>LivingCostTable.InitialStandard</c> / <c>GrainPricePolicy</c>，SC-008）。
    /// </summary>
    private static FamilyEconomy NewEconomy(Money cash, Money savings, Money merchantCapital) =>
        new(LivingStandard.Normal, new GrainPriceIndex(1m), new Treasury(cash, savings, merchantCapital));
}
