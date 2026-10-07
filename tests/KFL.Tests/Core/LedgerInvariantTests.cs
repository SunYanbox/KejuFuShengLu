using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Core;

/// <summary>
/// T025：账本与资金流动的不变量（data-model §2.1、§3.4、§3.6；contracts/ledger.md §1~§4）。
/// </summary>
/// <remarks>
/// 只钉结构性事实：条目形状、类别元数据的逐行取值、追加式、<c>Apply</c> / <c>RepayLoan</c> /
/// <c>AccrueLoanInterest</c> 与资金池的对应关系。**US5 的四个聚合查询本阶段未实现，故不测**。
/// </remarks>
public class LedgerInvariantTests
{
    /// <summary>夹具日期；与运行环境无关（MUST NOT 取系统时钟）。</summary>
    private static readonly GameDate AnyDate = new(5, 3);

    // ------------------------------------------------------------------ LedgerEntry

    [Fact]
    public void 资金类条目金额为零被拒而事件类允许为零()
    {
        Assert.Throws<ArgumentException>(
            () => new LedgerEntry(AnyDate, null, LedgerCategory.LivingCost, Money.Zero));
        Assert.Throws<ArgumentException>(
            () => new LedgerEntry(AnyDate, FamilyFixtures.Id(1), LedgerCategory.OfficialSalary, Money.Zero));
        Assert.Throws<ArgumentException>(
            () => new LedgerEntry(AnyDate, null, LedgerCategory.AssetPurchase, Money.Zero));

        var eventEntry = new LedgerEntry(AnyDate, null, LedgerCategory.FamineEntered, Money.Zero);

        Assert.Equal(Money.Zero, eventEntry.Amount);
        Assert.Equal(LedgerEntryKind.Event, LedgerCategoryMetadata.KindOf(eventEntry.Category));
    }

    [Fact]
    public void 条目字段被如实保存()
    {
        var personId = FamilyFixtures.Id(7);
        var amount = Money.FromGuan(-3m);

        var entry = new LedgerEntry(AnyDate, personId, LedgerCategory.LivingCost, amount);

        Assert.Equal(AnyDate, entry.Date);
        Assert.Equal(personId, entry.PersonId);
        Assert.Equal(LedgerCategory.LivingCost, entry.Category);
        Assert.Equal(amount, entry.Amount);

        // 家族级条目（铺面租、储蓄利息、工 bonus 等）的 PersonId 为 null（data-model §1.3）。
        Assert.Null(new LedgerEntry(AnyDate, null, LedgerCategory.ShopRentIncome, Money.FromGuan(1m)).PersonId);
    }

    // ------------------------------------------------------------------ 类别元数据

    [Theory]
    [InlineData(LedgerCategory.LivingCost, LedgerEntryKind.Treasury, null)]
    [InlineData(LedgerCategory.SelfFarmingIncome, LedgerEntryKind.Treasury, LedgerCreditTarget.Cash)]
    [InlineData(LedgerCategory.LandRentIncome, LedgerEntryKind.Treasury, LedgerCreditTarget.Cash)]
    [InlineData(LedgerCategory.FarmingWageIncome, LedgerEntryKind.Treasury, LedgerCreditTarget.Cash)]
    [InlineData(LedgerCategory.CraftingIncome, LedgerEntryKind.Treasury, LedgerCreditTarget.Cash)]
    [InlineData(LedgerCategory.TradeIncome, LedgerEntryKind.Treasury, LedgerCreditTarget.Cash)]
    [InlineData(LedgerCategory.OfficialSalary, LedgerEntryKind.Treasury, LedgerCreditTarget.Cash)]
    [InlineData(LedgerCategory.ShopRentIncome, LedgerEntryKind.Treasury, LedgerCreditTarget.Cash)]
    [InlineData(LedgerCategory.ArtisanBonus, LedgerEntryKind.Treasury, LedgerCreditTarget.Cash)]
    [InlineData(LedgerCategory.SavingsInterest, LedgerEntryKind.Treasury, LedgerCreditTarget.Savings)]
    [InlineData(LedgerCategory.LoanPrincipalRepaid, LedgerEntryKind.Treasury, null)]
    [InlineData(LedgerCategory.LoanInterestRepaid, LedgerEntryKind.Treasury, null)]
    [InlineData(LedgerCategory.AssetPurchase, LedgerEntryKind.Treasury, null)]
    [InlineData(LedgerCategory.AssetSale, LedgerEntryKind.Treasury, LedgerCreditTarget.Cash)]
    [InlineData(LedgerCategory.LoanInterestAccrued, LedgerEntryKind.Event, null)]
    [InlineData(LedgerCategory.FamineEntered, LedgerEntryKind.Event, null)]
    [InlineData(LedgerCategory.FamineReliefEntered, LedgerEntryKind.Event, null)]
    [InlineData(LedgerCategory.FamineSevereEntered, LedgerEntryKind.Event, null)]
    [InlineData(LedgerCategory.FamineResolved, LedgerEntryKind.Event, null)]
    public void 类别元数据逐行一致(
        LedgerCategory category, LedgerEntryKind kind, LedgerCreditTarget? creditTarget)
    {
        Assert.Equal(kind, LedgerCategoryMetadata.KindOf(category));
        Assert.Equal(creditTarget, LedgerCategoryMetadata.CreditTargetOf(category));
    }

    [Fact]
    public void 类别全集恰为十九个取值且无买人口聘礼贿赂()
    {
        string[] expected =
        [
            "ArtisanBonus", "AssetPurchase", "AssetSale", "CraftingIncome",
            "FamineEntered", "FamineReliefEntered", "FamineResolved", "FamineSevereEntered",
            "FarmingWageIncome", "LandRentIncome", "LivingCost",
            "LoanInterestAccrued", "LoanInterestRepaid", "LoanPrincipalRepaid",
            "OfficialSalary", "SavingsInterest", "SelfFarmingIncome", "ShopRentIncome",
            "TradeIncome",
        ];

        var actual = Enum.GetNames<LedgerCategory>()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);

        // FR-028：本阶段 MUST NOT 存在「买人口 / 聘礼 / 贿赂」一类的类别（以「无该类别」断言）。
        string[] forbiddenFragments = ["Buy", "Slave", "Bride", "Dowry", "Betroth", "Bribe"];

        foreach (var fragment in forbiddenFragments)
        {
            Assert.DoesNotContain(actual, name => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void 事件类恰为五个且KindOf与CreditTargetOf对每个取值都可求值()
    {
        LedgerCategory[] expectedEvents =
        [
            LedgerCategory.LoanInterestAccrued,
            LedgerCategory.FamineEntered,
            LedgerCategory.FamineReliefEntered,
            LedgerCategory.FamineSevereEntered,
            LedgerCategory.FamineResolved,
        ];

        var categories = Enum.GetValues<LedgerCategory>();

        var events = categories
            .Where(category => LedgerCategoryMetadata.KindOf(category) == LedgerEntryKind.Event)
            .OrderBy(category => category)
            .ToArray();

        Assert.Equal(expectedEvents.OrderBy(category => category), events);

        // 全覆盖无遗漏：19 个取值逐个可求值（未登记的取值会抛，见下一条）。
        foreach (var category in categories)
        {
            var kind = LedgerCategoryMetadata.KindOf(category);

            Assert.True(Enum.IsDefined(kind));
            _ = LedgerCategoryMetadata.CreditTargetOf(category);
        }

        // 无正额入账目标 = 4 个恒负支出类 + 5 个事件类。
        LedgerCategory[] expectedWithoutCreditTarget =
        [
            LedgerCategory.LivingCost,
            LedgerCategory.LoanPrincipalRepaid,
            LedgerCategory.LoanInterestRepaid,
            LedgerCategory.AssetPurchase,
            LedgerCategory.LoanInterestAccrued,
            LedgerCategory.FamineEntered,
            LedgerCategory.FamineReliefEntered,
            LedgerCategory.FamineSevereEntered,
            LedgerCategory.FamineResolved,
        ];

        var withoutCreditTarget = categories
            .Where(category => LedgerCategoryMetadata.CreditTargetOf(category) is null);

        Assert.Equal(
            expectedWithoutCreditTarget.OrderBy(category => category),
            withoutCreditTarget.OrderBy(category => category));
    }

    [Fact]
    public void 未登记的类别取值被拒()
    {
        var undefined = (LedgerCategory)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => LedgerCategoryMetadata.KindOf(undefined));
        Assert.Throws<ArgumentOutOfRangeException>(() => LedgerCategoryMetadata.CreditTargetOf(undefined));
    }

    // ------------------------------------------------------------------ Ledger 追加式

    [Fact]
    public void Append为追加式且既有条目与顺序不变()
    {
        var ledger = new Ledger();
        var first = new LedgerEntry(new GameDate(1, 1), null, LedgerCategory.OfficialSalary, Money.FromGuan(2m));
        var second = new LedgerEntry(
            new GameDate(1, 2), FamilyFixtures.Id(1), LedgerCategory.LivingCost, Money.FromGuan(-1m));
        var third = new LedgerEntry(new GameDate(1, 3), null, LedgerCategory.AssetSale, Money.FromGuan(9m));

        ledger.Append(first);
        ledger.Append(second);

        var before = ledger.Entries.ToArray();

        ledger.Append(third);

        Assert.Equal(before, ledger.Entries.Take(before.Length));
        Assert.Equal(new[] { first, second, third }, ledger.Entries);

        // 闭区间筛选仍按追加顺序，且既有条目未被改写。
        Assert.Equal(before, ledger.EntriesIn(new GameDate(1, 1), new GameDate(1, 2)));
    }

    // ------------------------------------------------------------------ Apply

    [Fact]
    public void 正额按类别入对应资金池()
    {
        var economy = NewEconomy(Money.FromGuan(10m), Money.FromGuan(4m), Money.FromGuan(3m));

        var entry = economy.Apply(
            LedgerCategory.OfficialSalary, FamilyFixtures.Id(1), Money.FromGuan(2m), AnyDate);

        Assert.Equal(Money.FromGuan(12m), economy.Treasury.Cash);
        Assert.Equal(Money.FromGuan(4m), economy.Treasury.Savings);
        Assert.Equal(entry, economy.Ledger.Entries[^1]);

        // §5.4：储蓄利息并入**储蓄本金**，不入现金。
        economy.Apply(LedgerCategory.SavingsInterest, null, Money.FromGuan(1m), AnyDate);

        Assert.Equal(Money.FromGuan(12m), economy.Treasury.Cash);
        Assert.Equal(Money.FromGuan(5m), economy.Treasury.Savings);
        Assert.Equal(LedgerCreditTarget.Savings, LedgerCategoryMetadata.CreditTargetOf(LedgerCategory.SavingsInterest));
    }

    [Fact]
    public void 负额先扣现金再扣储蓄()
    {
        var economy = NewEconomy(Money.FromGuan(10m), Money.FromGuan(5m), Money.FromGuan(100m));

        economy.Apply(LedgerCategory.LivingCost, null, Money.FromGuan(-3m), AnyDate);

        Assert.Equal(Money.FromGuan(7m), economy.Treasury.Cash);
        Assert.Equal(Money.FromGuan(5m), economy.Treasury.Savings);

        economy.Apply(LedgerCategory.LivingCost, null, Money.FromGuan(-9m), AnyDate);

        Assert.Equal(Money.Zero, economy.Treasury.Cash);
        Assert.Equal(Money.FromGuan(3m), economy.Treasury.Savings);

        // 商本在资金池口径内，但**不是可付来源**：扣付只走现金 → 储蓄。
        Assert.Equal(Money.FromGuan(100m), economy.Treasury.MerchantCapital);
    }

    [Fact]
    public void 资金不足抛异常且不留条目不动池()
    {
        var economy = NewEconomy(Money.FromGuan(1m), Money.Zero, Money.FromGuan(100m));
        var poolBefore = economy.TreasuryPool;

        Assert.Throws<InvalidOperationException>(
            () => economy.Apply(LedgerCategory.LivingCost, null, Money.FromGuan(-2m), AnyDate));

        // 失败原子性：异常路径既不动资金池、也不留条目（FamilyEconomy.Apply 的文档保证）。
        Assert.Empty(economy.Ledger.Entries);
        Assert.Equal(poolBefore, economy.TreasuryPool);
        Assert.Equal(Money.FromGuan(1m), economy.Treasury.Cash);
        Assert.Equal(Money.FromGuan(100m), economy.Treasury.MerchantCapital);
    }

    [Fact]
    public void 无正额入账目标的资金类类别以正额入账被拒()
    {
        var economy = NewEconomy(Money.FromGuan(10m), Money.Zero, Money.Zero);

        LedgerCategory[] alwaysNegative =
        [
            LedgerCategory.LivingCost,
            LedgerCategory.LoanPrincipalRepaid,
            LedgerCategory.LoanInterestRepaid,
            LedgerCategory.AssetPurchase,
        ];

        foreach (var category in alwaysNegative)
        {
            Assert.Null(LedgerCategoryMetadata.CreditTargetOf(category));
            Assert.Throws<InvalidOperationException>(
                () => economy.Apply(category, null, Money.FromGuan(1m), AnyDate));
        }

        Assert.Empty(economy.Ledger.Entries);
        Assert.Equal(Money.FromGuan(10m), economy.Treasury.Cash);
    }

    [Fact]
    public void 事件类条目只追加而不动资金池()
    {
        var economy = NewEconomy(Money.FromGuan(10m), Money.FromGuan(5m), Money.FromGuan(3m));
        var poolBefore = economy.TreasuryPool;

        economy.EnterFamineStage(FamineStage.Famine, Money.Zero, AnyDate);

        var entry = Assert.Single(economy.Ledger.Entries);

        Assert.Equal(LedgerCategory.FamineEntered, entry.Category);
        Assert.Equal(LedgerEntryKind.Event, LedgerCategoryMetadata.KindOf(entry.Category));
        Assert.Null(entry.PersonId);
        Assert.Equal(Money.Zero, entry.Amount);
        Assert.Equal(poolBefore, economy.TreasuryPool);
    }

    // ------------------------------------------------------------------ RepayLoan / AccrueLoanInterest / TreasuryPool

    [Fact]
    public void 偿还贷款先本后息且零金额段不落条目()
    {
        var economy = NewEconomy(Money.FromGuan(50m), Money.Zero, Money.Zero);
        var loan = new Loan { Principal = Money.FromGuan(10m) };
        loan.AccrueInterest(Money.FromGuan(4m));
        economy.Treasury.Loan = loan;

        var first = economy.RepayLoan(Money.FromGuan(12m), AnyDate);

        Assert.Equal(Money.FromGuan(10m), first.PrincipalPart);
        Assert.Equal(Money.FromGuan(2m), first.InterestPart);
        Assert.Equal(Money.Zero, loan.Principal);
        Assert.Equal(Money.FromGuan(2m), loan.AccruedInterest);
        Assert.Equal(Money.FromGuan(38m), economy.Treasury.Cash);
        Assert.Equal(
            new[] { LedgerCategory.LoanPrincipalRepaid, LedgerCategory.LoanInterestRepaid },
            economy.Ledger.Entries.Select(entry => entry.Category));
        Assert.Equal(
            new[] { Money.FromGuan(-10m), Money.FromGuan(-2m) },
            economy.Ledger.Entries.Select(entry => entry.Amount));

        // 本金段为 0 的第二次还款 MUST NOT 落「偿还本金」条目。
        var second = economy.RepayLoan(Money.FromGuan(2m), AnyDate);

        Assert.Equal(Money.Zero, second.PrincipalPart);
        Assert.Equal(Money.FromGuan(2m), second.InterestPart);
        Assert.True(loan.IsSettled);
        Assert.Equal(
            new[]
            {
                LedgerCategory.LoanPrincipalRepaid,
                LedgerCategory.LoanInterestRepaid,
                LedgerCategory.LoanInterestRepaid,
            },
            economy.Ledger.Entries.Select(entry => entry.Category));
        Assert.Single(economy.Ledger.Entries, entry => entry.Category == LedgerCategory.LoanPrincipalRepaid);
        Assert.Equal(Money.FromGuan(36m), economy.Treasury.Cash);
    }

    [Fact]
    public void 计息只欠息不滚本金且落一条事件类条目()
    {
        var economy = NewEconomy(Money.FromGuan(10m), Money.Zero, Money.Zero);
        var loan = new Loan { Principal = Money.FromGuan(10m) };
        economy.Treasury.Loan = loan;
        var poolBefore = economy.TreasuryPool;

        var accrued = economy.AccrueLoanInterest(Money.FromGuan(0.5m), AnyDate);  // arch-guard:allow 夹具借款与利息金额（非规则数值副本）

        Assert.Equal(Money.FromGuan(0.5m), accrued);  // arch-guard:allow 夹具借款与利息金额（非规则数值副本）
        Assert.Equal(Money.FromGuan(10m), loan.Principal);
        Assert.Equal(Money.FromGuan(0.5m), loan.AccruedInterest);  // arch-guard:allow 夹具借款与利息金额（非规则数值副本）
        Assert.Equal(Money.FromGuan(10.5m), loan.Total);
        Assert.Equal(poolBefore, economy.TreasuryPool);

        var entry = Assert.Single(economy.Ledger.Entries);

        Assert.Equal(LedgerCategory.LoanInterestAccrued, entry.Category);
        Assert.Equal(LedgerEntryKind.Event, LedgerCategoryMetadata.KindOf(entry.Category));
        Assert.Equal(Money.FromGuan(0.5m), entry.Amount);  // arch-guard:allow 夹具借款与利息金额（非规则数值副本）
        Assert.Null(entry.PersonId);
    }

    [Fact]
    public void 资金池为现金储蓄商本之和且不含贷款()
    {
        var economy = NewEconomy(Money.FromGuan(10m), Money.FromGuan(5m), Money.FromGuan(3m));
        economy.Treasury.Loan = new Loan { Principal = Money.FromGuan(100m) };

        Assert.Equal(
            economy.Treasury.Cash + economy.Treasury.Savings + economy.Treasury.MerchantCapital,
            economy.TreasuryPool);
        Assert.Equal(Money.FromGuan(18m), economy.TreasuryPool);

        // 贷款是负债（R-05）：计入池后必然更大，故口径 MUST 排除它。
        Assert.True(economy.Treasury.Loan.Total.IsPositive);
        Assert.NotEqual(economy.TreasuryPool + economy.Treasury.Loan.Total, economy.TreasuryPool);
    }

    /// <summary>
    /// 只给一个合法初值的经济聚合：档位与米价系数是**夹具取值**，产品初值单点在 Rules
    /// （<c>LivingCostTable.InitialStandard</c> / <c>GrainPricePolicy</c>，SC-008）。
    /// </summary>
    private static FamilyEconomy NewEconomy(Money cash, Money savings, Money merchantCapital) =>
        new(LivingStandard.Normal, new GrainPriceIndex(1m), new Treasury(cash, savings, merchantCapital));
}
