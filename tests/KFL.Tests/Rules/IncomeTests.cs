using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;
using KFL.Rules.Settlement;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// US1 / quickstart S2、S8：全部收入来源的逐项口径（FR-006~FR-012、FR-031；E-15/E-16/E-17/E-18）。
/// </summary>
public class IncomeTests
{
    private static readonly Difficulty Normal = Difficulty.Normal;

    [Fact]
    public void 自耕四十亩为亩数乘零点五除以十二而非两贯()
    {
        var first = RulesHarness.Member(31, Gender.Male, 40);
        var second = RulesHarness.Member(32, Gender.Male, 30);

        var computation = Compute(
            RulesHarness.FamilyOf(first, second), RulesHarness.HoldingsOf(farmlandMu: 40), Money.Zero);

        var each = RulesHarness.Guan(
            IncomeRateTable.FarmlandPerCapitaMu
            * IncomeRateTable.SelfFarmingGuanPerMuPerYear
            / IncomeRateTable.MonthsPerYear);

        Assert.Equal(each, Amount(computation, LedgerCategory.SelfFarmingIncome, first.Id));
        Assert.Equal(each, Amount(computation, LedgerCategory.SelfFarmingIncome, second.Id));
        Assert.Equal(each + each, computation.Total);
        Assert.NotEqual(RulesHarness.Guan(IncomeRateTable.FarmingWageGuanPerMonth), computation.Total);
    }

    [Fact]
    public void 每人二十亩为上限第二十一亩起转田租()
    {
        var sole = RulesHarness.Member(33, Gender.Male, 40);

        var computation = Compute(
            RulesHarness.FamilyOf(sole), RulesHarness.HoldingsOf(farmlandMu: 40), Money.Zero);

        Assert.Equal(
            RulesHarness.Guan(
                IncomeRateTable.FarmlandPerCapitaMu
                * IncomeRateTable.SelfFarmingGuanPerMuPerYear
                / IncomeRateTable.MonthsPerYear),
            Amount(computation, LedgerCategory.SelfFarmingIncome, sole.Id));

        Assert.Equal(
            RulesHarness.Guan(
                IncomeRateTable.FarmlandPerCapitaMu
                * IncomeRateTable.LandRentGuanPerMuPerYear
                / IncomeRateTable.MonthsPerYear),
            Amount(computation, LedgerCategory.LandRentIncome, null));

        Assert.DoesNotContain(computation.Lines, l => l.Category == LedgerCategory.FarmingWageIncome);
    }

    [Fact]
    public void 自耕亩数按耕作效率降序分配()
    {
        var strong = RulesHarness.Member(34, Gender.Male, 30, agriculture: 80);
        var weak = RulesHarness.Member(35, Gender.Male, 50);

        var computation = Compute(
            RulesHarness.FamilyOf(strong, weak), RulesHarness.HoldingsOf(farmlandMu: 20), Money.Zero);

        Assert.Equal(
            RulesHarness.Guan(
                IncomeRateTable.FarmlandPerCapitaMu
                * IncomeRateTable.SelfFarmingGuanPerMuPerYear
                / IncomeRateTable.MonthsPerYear
                * IncomeRateTable.FarmingEfficiency(strong.Talents)),
            Amount(computation, LedgerCategory.SelfFarmingIncome, strong.Id));

        Assert.DoesNotContain(
            computation.Lines,
            l => l.Category == LedgerCategory.SelfFarmingIncome && l.PersonId == weak.Id);
    }

    [Fact]
    public void 效率并列时按年龄降序分配()
    {
        var older = RulesHarness.Member(36, Gender.Male, 50);
        var younger = RulesHarness.Member(37, Gender.Male, 30);

        var computation = Compute(
            RulesHarness.FamilyOf(younger, older), RulesHarness.HoldingsOf(farmlandMu: 20), Money.Zero);

        Assert.DoesNotContain(
            computation.Lines,
            l => l.Category == LedgerCategory.SelfFarmingIncome && l.PersonId == younger.Id);
        Assert.Equal(
            RulesHarness.Guan(
                IncomeRateTable.FarmlandPerCapitaMu
                * IncomeRateTable.SelfFarmingGuanPerMuPerYear
                / IncomeRateTable.MonthsPerYear),
            Amount(computation, LedgerCategory.SelfFarmingIncome, older.Id));
    }

    [Fact]
    public void 无田可耕时每名计口成年成员各得一份务农且与自耕互斥()
    {
        var first = RulesHarness.Member(38, Gender.Male, 40);
        var second = RulesHarness.Member(39, Gender.Male, 30);

        var computation = Compute(
            RulesHarness.FamilyOf(first, second), RulesHarness.HoldingsOf(farmlandMu: 0), Money.Zero);

        foreach (var person in new[] { first, second })
        {
            Assert.Equal(
                RulesHarness.Guan(IncomeRateTable.FarmingWageGuanPerMonth),
                Amount(computation, LedgerCategory.FarmingWageIncome, person.Id));
        }

        Assert.DoesNotContain(computation.Lines, l => l.Category == LedgerCategory.SelfFarmingIncome);
    }

    [Fact]
    public void 有田但未占满容量时不得发放务农()
    {
        var sole = RulesHarness.Member(40, Gender.Male, 40);

        var computation = Compute(
            RulesHarness.FamilyOf(sole), RulesHarness.HoldingsOf(farmlandMu: 40), Money.Zero);

        Assert.DoesNotContain(computation.Lines, l => l.Category == LedgerCategory.FarmingWageIncome);
    }

    [Fact]
    public void 做工需指派且每名被指派者各得一份()
    {
        var idle = RulesHarness.Member(41, Gender.Male, 40);

        var unassigned = Compute(RulesHarness.FamilyOf(idle), RulesHarness.HoldingsOf(), Money.Zero);

        Assert.DoesNotContain(unassigned.Lines, l => l.Category == LedgerCategory.CraftingIncome);

        var first = RulesHarness.Member(42, Gender.Male, 40, craft: 40);
        var second = RulesHarness.Member(43, Gender.Male, 35, craft: 20);
        first.Occupation = Occupation.Crafting;
        second.Occupation = Occupation.Crafting;

        var assigned = Compute(RulesHarness.FamilyOf(first, second), RulesHarness.HoldingsOf(), Money.Zero);

        Assert.Equal(
            RulesHarness.Guan(IncomeRateTable.CraftingGuanPerMonth * IncomeRateTable.CraftFactor(first.Talents)),
            Amount(assigned, LedgerCategory.CraftingIncome, first.Id));
        Assert.Equal(
            RulesHarness.Guan(IncomeRateTable.CraftingGuanPerMonth * IncomeRateTable.CraftFactor(second.Talents)),
            Amount(assigned, LedgerCategory.CraftingIncome, second.Id));
    }

    [Fact]
    public void 城市宅加成份数为做工人数与宅数的较小者且按本人工乘数降序逐人归属()
    {
        var top = RulesHarness.Member(44, Gender.Male, 40, craft: 80);
        var middle = RulesHarness.Member(45, Gender.Male, 35, craft: 40);
        var bottom = RulesHarness.Member(46, Gender.Male, 30, craft: 20);
        top.Occupation = Occupation.Crafting;
        middle.Occupation = Occupation.Crafting;
        bottom.Occupation = Occupation.Crafting;

        var withoutHouse = Compute(
            RulesHarness.FamilyOf(top, middle, bottom), RulesHarness.HoldingsOf(), Money.Zero);

        Assert.Equal(
            3,
            withoutHouse.Lines.Count(l => l.Category == LedgerCategory.CraftingIncome));

        var withOneHouse = Compute(
            RulesHarness.FamilyOf(top, middle, bottom), RulesHarness.HoldingsOf(urbanHouses: 1), Money.Zero);

        var baseBonuses = withOneHouse.Lines
            .Where(l => l.Category == LedgerCategory.CraftingIncome)
            .ToArray();

        Assert.Equal(4, baseBonuses.Length);
        Assert.Equal(
            RulesHarness.Guan(
                (IncomeRateTable.CraftingGuanPerMonth + IncomeRateTable.UrbanHouseCraftingBonusGuanPerMonth)
                * IncomeRateTable.CraftFactor(top.Talents)),
            Amount(withOneHouse, LedgerCategory.CraftingIncome, top.Id));
        Assert.Equal(
            RulesHarness.Guan(IncomeRateTable.CraftingGuanPerMonth * IncomeRateTable.CraftFactor(middle.Talents)),
            Amount(withOneHouse, LedgerCategory.CraftingIncome, middle.Id));
        Assert.Equal(
            RulesHarness.Guan(IncomeRateTable.CraftingGuanPerMonth * IncomeRateTable.CraftFactor(bottom.Talents)),
            Amount(withOneHouse, LedgerCategory.CraftingIncome, bottom.Id));
    }

    [Fact]
    public void 经商门槛含边界商出身乘一点一倍且条目归属被采用的成员()
    {
        var trader = RulesHarness.Member(47, Gender.Male, 40, commerce: 60);
        trader.Occupation = Occupation.Trading;

        var below = Compute(
            RulesHarness.FamilyOf(trader),
            RulesHarness.HoldingsOf(),
            RulesHarness.Guan(IncomeRateTable.TradeCapitalThresholdGuan - 1m));

        Assert.DoesNotContain(below.Lines, l => l.Category == LedgerCategory.TradeIncome);

        var atThreshold = Compute(
            RulesHarness.FamilyOf(trader),
            RulesHarness.HoldingsOf(),
            RulesHarness.Guan(IncomeRateTable.TradeCapitalThresholdGuan));

        var line = Assert.Single(atThreshold.Lines, l => l.Category == LedgerCategory.TradeIncome);

        Assert.Equal(trader.Id, line.PersonId);
        Assert.Equal(
            RulesHarness.Guan(
                IncomeRateTable.TradeCapitalThresholdGuan
                * IncomeRateTable.TradeProfitRate
                * IncomeRateTable.CommerceFactor(trader.Talents)
                * IncomeRateTable.CraftFactor(trader.Talents)),
            line.Amount);

        var merchant = Compute(
            RulesHarness.FamilyOf(trader),
            RulesHarness.HoldingsOf(),
            RulesHarness.Guan(IncomeRateTable.TradeCapitalThresholdGuan),
            Origin.Merchant);

        var merchantLine = Assert.Single(
            merchant.Lines, l => l.Category == LedgerCategory.TradeIncome);

        Assert.Equal(
            RulesHarness.Guan(line.Amount.Guan * IncomeRateTable.MerchantOriginMultiplier),
            merchantLine.Amount);

        var idle = RulesHarness.Member(60, Gender.Male, 40);
        var nobody = Compute(RulesHarness.FamilyOf(idle), RulesHarness.HoldingsOf(), RulesHarness.Guan(250m));
        Assert.DoesNotContain(nobody.Lines, l => l.Category == LedgerCategory.TradeIncome);
    }

    [Fact]
    public void 经商取乘数最大者且不按人头重复()
    {
        var stronger = RulesHarness.Member(48, Gender.Male, 30, commerce: 80);
        var weaker = RulesHarness.Member(49, Gender.Male, 50, commerce: 20);
        stronger.Occupation = Occupation.Trading;
        weaker.Occupation = Occupation.Trading;

        var computation = Compute(
            RulesHarness.FamilyOf(stronger, weaker),
            RulesHarness.HoldingsOf(),
            RulesHarness.Guan(IncomeRateTable.TradeCapitalThresholdGuan));

        var line = Assert.Single(computation.Lines, l => l.Category == LedgerCategory.TradeIncome);

        Assert.Equal(stronger.Id, line.PersonId);
    }

    [Fact]
    public void 铺面月摊为单价乘年租率除以十二()
    {
        var sole = RulesHarness.Member(50, Gender.Male, 40);

        var computation = Compute(RulesHarness.FamilyOf(sole), RulesHarness.HoldingsOf(shops: 1), Money.Zero);

        Assert.Equal(
            RulesHarness.Guan(
                AssetPriceTable.ShopGuan * AssetPriceTable.ShopRentRate / IncomeRateTable.MonthsPerYear),
            AssetPriceTable.ShopMonthlyRent(1));

        Assert.Equal(AssetPriceTable.ShopMonthlyRent(1), Amount(computation, LedgerCategory.ShopRentIncome, null));
    }

    [Fact]
    public void 田租在家主为空或非计口时乘数取一()
    {
        var head = RulesHarness.Member(51, Gender.Male, 40, agriculture: 80);
        var family = RulesHarness.FamilyOf(head);
        var holdings = RulesHarness.HoldingsOf(farmlandMu: 40);

        var withHead = Compute(family, holdings, Money.Zero);

        Assert.Equal(
            RulesHarness.Guan(
                IncomeRateTable.FarmlandPerCapitaMu
                * IncomeRateTable.LandRentGuanPerMuPerYear
                / IncomeRateTable.MonthsPerYear
                * IncomeRateTable.FarmingEfficiency(head.Talents)),
            Amount(withHead, LedgerCategory.LandRentIncome, null));

        family.SetHead(null);

        var withoutHead = Compute(family, holdings, Money.Zero);

        Assert.Equal(
            RulesHarness.Guan(
                IncomeRateTable.FarmlandPerCapitaMu
                * IncomeRateTable.LandRentGuanPerMuPerYear
                / IncomeRateTable.MonthsPerYear),
            Amount(withoutHead, LedgerCategory.LandRentIncome, null));

        family.SetHead(head.Id);
        head.Status = StatusFlag.ServingSentence;

        var jailedHead = Compute(family, holdings, Money.Zero);

        Assert.Equal(
            RulesHarness.Guan(
                holdings.FarmlandMu
                * IncomeRateTable.LandRentGuanPerMuPerYear
                / IncomeRateTable.MonthsPerYear),
            Amount(jailedHead, LedgerCategory.LandRentIncome, null));
    }

    [Fact]
    public void 收入来源彼此叠加()
    {
        var both = RulesHarness.Member(52, Gender.Male, 40, craft: 40);
        both.Occupation = Occupation.Crafting;

        var computation = Compute(
            RulesHarness.FamilyOf(both), RulesHarness.HoldingsOf(farmlandMu: 20), Money.Zero);

        Assert.Contains(
            computation.Lines,
            l => l.Category == LedgerCategory.SelfFarmingIncome && l.PersonId == both.Id);
        Assert.Contains(
            computation.Lines,
            l => l.Category == LedgerCategory.CraftingIncome && l.PersonId == both.Id);
    }

    [Fact]
    public void 可指派人群为计口且成年故青年与老人可未成年不可()
    {
        var youth = RulesHarness.Member(53, Gender.Male, 15);
        var elder = RulesHarness.Member(54, Gender.Male, 65);
        var child = RulesHarness.Member(55, Gender.Male, 8);
        youth.Occupation = Occupation.Crafting;
        elder.Occupation = Occupation.Crafting;
        child.Occupation = Occupation.Crafting;

        var family = RulesHarness.FamilyOf(youth, elder, child);
        var assignable = CountedMembers.Assignable(family, RulesHarness.Date);

        Assert.Contains(assignable, p => p.Id == youth.Id);
        Assert.Contains(assignable, p => p.Id == elder.Id);
        Assert.DoesNotContain(assignable, p => p.Id == child.Id);

        var computation = Compute(family, RulesHarness.HoldingsOf(), Money.Zero);

        Assert.Contains(
            computation.Lines, l => l.Category == LedgerCategory.CraftingIncome && l.PersonId == youth.Id);
        Assert.Contains(
            computation.Lines, l => l.Category == LedgerCategory.CraftingIncome && l.PersonId == elder.Id);
        Assert.DoesNotContain(
            computation.Lines, l => l.Category == LedgerCategory.CraftingIncome && l.PersonId == child.Id);
    }

    [Fact]
    public void 服刑与外嫁不产生任何收入而待阙照常产生收入但无俸禄()
    {
        var normal = RulesHarness.Member(56, Gender.Male, 40);

        var jailed = RulesHarness.Member(57, Gender.Male, 35, craft: 40);
        jailed.Occupation = Occupation.Crafting;
        jailed.Status = StatusFlag.ServingSentence;

        var marriedOut = RulesHarness.Member(58, Gender.Female, 30, craft: 40);
        marriedOut.Occupation = Occupation.Crafting;
        marriedOut.Status = StatusFlag.MarriedOut;

        var awaiting = RulesHarness.Member(59, Gender.Male, 32, craft: 40);
        awaiting.Occupation = Occupation.Crafting;
        awaiting.Status = StatusFlag.AwaitingPost;

        var computation = Compute(
            RulesHarness.FamilyOf(normal, jailed, marriedOut, awaiting), RulesHarness.HoldingsOf(), Money.Zero);

        Assert.DoesNotContain(computation.Lines, l => l.PersonId == jailed.Id);
        Assert.DoesNotContain(computation.Lines, l => l.PersonId == marriedOut.Id);
        Assert.Contains(
            computation.Lines, l => l.Category == LedgerCategory.CraftingIncome && l.PersonId == awaiting.Id);
        Assert.DoesNotContain(computation.Lines, l => l.Category == LedgerCategory.OfficialSalary);
    }

    private static IncomeComputation Compute(
        Family family, Holdings holdings, Money merchantCapital, Origin origin = Origin.Artisan) =>
        RulesHarness.Income(
            family, holdings, RulesHarness.TreasuryWith(merchantCapital), Normal, origin);

    private static Money Amount(IncomeComputation computation, LedgerCategory category, PersonId? personId)
    {
        var total = Money.Zero;

        foreach (var line in computation.Lines)
        {
            if (line.Category == category && line.PersonId == personId)
            {
                total += line.Amount;
            }
        }

        return total;
    }
}
