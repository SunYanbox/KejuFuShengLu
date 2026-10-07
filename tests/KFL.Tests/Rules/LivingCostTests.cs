using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;
using KFL.Rules.Settlement;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// US1 / quickstart S1、S8：生活费四个乘区、年龄档边界、派生关系与计口口径（FR-002~FR-005、
/// FR-029/FR-030、SC-009）。
/// </summary>
public class LivingCostTests
{
    private static readonly decimal Grain = GrainPricePolicy.Initial;

    [Fact]
    public void 逐年龄档明细与四个乘区可分别读出()
    {
        var fixture = EconomyFixtures.Create();
        var counted = CountedMembers.Counted(fixture.Family);

        Assert.Equal(10, counted.Count);

        var computation = LivingCostCalculator.Compute(
            counted, fixture.Date, LivingStandard.Normal, Grain, Difficulty.Normal, Origin.Farmer, FamineStage.None);

        AssertBracket(computation, AgeBracket.Adult, 6, LivingStandard.Normal, Origin.Farmer, Difficulty.Normal, FamineStage.None);
        AssertBracket(computation, AgeBracket.Youth, 1, LivingStandard.Normal, Origin.Farmer, Difficulty.Normal, FamineStage.None);
        AssertBracket(computation, AgeBracket.Elder, 2, LivingStandard.Normal, Origin.Farmer, Difficulty.Normal, FamineStage.None);
        AssertBracket(computation, AgeBracket.Child, 1, LivingStandard.Normal, Origin.Farmer, Difficulty.Normal, FamineStage.None);

        Assert.Equal(4, computation.Lines.Count);
        Assert.Equal(SumOfLines(computation), computation.Payable);
    }

    [Fact]
    public void 三档四档的日耗逐格可读且明细顺序固定()
    {
        var counted = CountedMembers.Counted(EconomyFixtures.Create().Family);
        var computation = LivingCostCalculator.Compute(
            counted, RulesHarness.Date, LivingStandard.Normal, Grain, Difficulty.Normal, Origin.Farmer, FamineStage.None);

        Assert.Equal(LivingCostCalculator.BracketOrder, computation.Lines.Select(line => line.AgeBracket).ToArray());

        foreach (var standard in Enum.GetValues<LivingStandard>())
        {
            foreach (var bracket in Enum.GetValues<AgeBracket>())
            {
                Assert.True(LivingCostTable.DailyCost(standard, bracket) > 0m);
            }
        }
    }

    [Fact]
    public void 老人档等于成人档乘比值且儿童档不随档位缩放()
    {
        foreach (var standard in Enum.GetValues<LivingStandard>())
        {
            var adult = LivingCostTable.DailyCost(standard, AgeBracket.Adult);

            Assert.Equal(
                adult * LivingCostTable.ElderRatioOfAdult,
                LivingCostTable.DailyCost(standard, AgeBracket.Elder));
            Assert.Equal(LivingCostTable.ChildDailyCost, LivingCostTable.DailyCost(standard, AgeBracket.Child));
        }
    }

    [Fact]
    public void 生日当月即转档且四档边界各一条()
    {
        Assert.Equal(
            AgeBracket.Child, AgeBracketPolicy.Of(Gender.Male, AgeBracketPolicy.MaleAdulthoodAge - 1));
        Assert.Equal(
            AgeBracket.Youth, AgeBracketPolicy.Of(Gender.Male, AgeBracketPolicy.MaleAdulthoodAge));
        Assert.Equal(
            AgeBracket.Child, AgeBracketPolicy.Of(Gender.Female, AgeBracketPolicy.FemaleAdulthoodAge - 1));
        Assert.Equal(
            AgeBracket.Youth, AgeBracketPolicy.Of(Gender.Female, AgeBracketPolicy.FemaleAdulthoodAge));
        Assert.Equal(AgeBracket.Youth, AgeBracketPolicy.Of(Gender.Male, AgeBracketPolicy.YouthUpperAge));
        Assert.Equal(AgeBracket.Adult, AgeBracketPolicy.Of(Gender.Male, AgeBracketPolicy.YouthUpperAge + 1));
        Assert.Equal(AgeBracket.Adult, AgeBracketPolicy.Of(Gender.Male, AgeBracketPolicy.ElderLowerAge - 1));
        Assert.Equal(AgeBracket.Elder, AgeBracketPolicy.Of(Gender.Male, AgeBracketPolicy.ElderLowerAge));

        var birthMonth = 6;
        var boy = new Person(
            FamilyFixtures.Id(901),
            "生日童",
            Gender.Male,
            new GameDate(RulesHarness.Date.Year - AgeBracketPolicy.MaleAdulthoodAge, birthMonth),
            new TalentSet(0, 0, 0, 0),
            70,
            0);
        var girl = new Person(
            FamilyFixtures.Id(902),
            "生日女",
            Gender.Female,
            new GameDate(RulesHarness.Date.Year - AgeBracketPolicy.FemaleAdulthoodAge, birthMonth),
            new TalentSet(0, 0, 0, 0),
            70,
            0);

        var onBirthday = new GameDate(RulesHarness.Date.Year, birthMonth);
        var beforeBirthday = new GameDate(RulesHarness.Date.Year, birthMonth - 1);

        Assert.Equal(AgeBracket.Youth, AgeBracketPolicy.Of(boy, onBirthday));
        Assert.Equal(AgeBracket.Child, AgeBracketPolicy.Of(boy, beforeBirthday));
        Assert.Equal(AgeBracket.Youth, AgeBracketPolicy.Of(girl, onBirthday));
        Assert.Equal(AgeBracket.Child, AgeBracketPolicy.Of(girl, beforeBirthday));
    }

    [Fact]
    public void 农出身与非农乘成年与未成年四种组合各一条()
    {
        var adult = RulesHarness.Member(11, Gender.Male, 30);
        var child = RulesHarness.Member(12, Gender.Male, 8);

        foreach (var origin in Enum.GetValues<Origin>())
        {
            AssertMultiplier(origin, adult, AgeBracket.Adult);
            AssertMultiplier(origin, child, AgeBracket.Child);
        }
    }

    [Fact]
    public void 救济期一般乘区为未成年与救济两项之和且农出身未成年时最低()
    {
        Assert.Equal(
            1m + LivingCostTable.MinorModifier.Percentage + LivingCostTable.ReliefModifier.Percentage,
            LivingCostTable.GeneralZoneFactor(isMinor: true, inRelief: true));

        var child = RulesHarness.Member(13, Gender.Male, 8);
        var computation = LivingCostCalculator.Compute(
            [child], RulesHarness.Date, LivingStandard.Normal, Grain, Difficulty.Normal, Origin.Farmer, FamineStage.Relief);

        var line = computation.Lines.Single(l => l.AgeBracket == AgeBracket.Child);
        var expected = Money.FromWen(
            LivingCostTable.DaysPerMonth
            * Grain
            * DifficultyRates.ExpenseFactor(Difficulty.Normal)
            * LivingCostTable.DailyCost(LivingStandard.Normal, AgeBracket.Child)
            * LivingCostTable.FarmerMultiplierMinor
            * LivingCostTable.GeneralZoneFactor(isMinor: true, inRelief: true));

        Assert.Equal(expected, line.Subtotal);
        Assert.Equal(expected, computation.Payable);
    }

    [Fact]
    public void 开局档位初值为普通档()
    {
        Assert.Equal(LivingStandard.Normal, LivingCostTable.InitialStandard);
    }

    [Fact]
    public void 服刑与外嫁不计口但档案与历史条目仍可读且待阙照常计入()
    {
        var fixture = EconomyFixtures.Create();
        var family = fixture.Family;
        var date = fixture.Date;

        var before = Payable(family, date);

        var jailed = family.AddOutsider(new Person(
            FamilyFixtures.Id(903), "服刑者", Gender.Male, new GameDate(date.Year - 30, date.Month),
            new TalentSet(0, 0, 0, 0), 70, 4));
        jailed.Status = StatusFlag.ServingSentence;

        var marriedOut = family.AddOutsider(new Person(
            FamilyFixtures.Id(904), "外嫁者", Gender.Female, new GameDate(date.Year - 25, date.Month),
            new TalentSet(0, 0, 0, 0), 70, 4));
        marriedOut.Status = StatusFlag.MarriedOut;

        Assert.Equal(before, Payable(family, date));

        var counted = CountedMembers.Counted(family);
        var registered = CountedMembers.Registered(family);

        Assert.DoesNotContain(counted, p => p.Id == jailed.Id);
        Assert.DoesNotContain(counted, p => p.Id == marriedOut.Id);
        Assert.Contains(registered, p => p.Id == jailed.Id);
        Assert.DoesNotContain(registered, p => p.Id == marriedOut.Id);

        // 档案与族谱位置保留；历史账目按角色仍可读出（SC-009）。
        Assert.NotNull(family.TryGet(jailed.Id));
        Assert.NotNull(family.TryGet(marriedOut.Id));
        Assert.Contains(family.ArchivedMembers, p => p.Id == marriedOut.Id);

        var ledger = fixture.Economy.Ledger;
        ledger.Append(new LedgerEntry(
            date, jailed.Id, LedgerCategory.CraftingIncome, Money.FromGuan(3m)));

        var readBack = ledger.EntriesIn(date, date);
        Assert.Contains(readBack, entry => entry.PersonId == jailed.Id);
        Assert.DoesNotContain(readBack, entry => entry.PersonId == marriedOut.Id);

        // 待阙仍在在册内且**照常计入**计口（§8.2：无俸、靠积蓄）。
        var awaiting = family.AddOutsider(new Person(
            FamilyFixtures.Id(905), "待阙者", Gender.Male, new GameDate(date.Year - 35, date.Month),
            new TalentSet(0, 0, 0, 0), 70, 4));
        awaiting.Status = StatusFlag.AwaitingPost;

        Assert.Contains(CountedMembers.Counted(family), p => p.Id == awaiting.Id);
        Assert.True(Payable(family, date) > before);
    }

    private static void AssertMultiplier(Origin origin, Person member, AgeBracket bracket)
    {
        var computation = LivingCostCalculator.Compute(
            [member], RulesHarness.Date, LivingStandard.Normal, Grain, Difficulty.Normal, origin, FamineStage.None);

        var line = computation.Lines.Single(l => l.AgeBracket == bracket);
        var expected = Money.FromWen(
            LivingCostTable.DaysPerMonth
            * Grain
            * DifficultyRates.ExpenseFactor(Difficulty.Normal)
            * LivingCostTable.DailyCost(LivingStandard.Normal, bracket)
            * LivingCostTable.FarmerMultiplierOf(origin, bracket)
            * LivingCostTable.GeneralZoneFactor(bracket == AgeBracket.Child, inRelief: false));

        Assert.Equal(expected, line.Subtotal);
    }

    private static void AssertBracket(
        LivingCostComputation computation,
        AgeBracket bracket,
        int expectedCount,
        LivingStandard standard,
        Origin origin,
        Difficulty difficulty,
        FamineStage stage)
    {
        var line = computation.Lines.Single(l => l.AgeBracket == bracket);

        Assert.Equal(expectedCount, line.MemberCount);
        Assert.Equal(LivingCostTable.DailyCost(standard, bracket), line.DailyCost);

        var perMember = LivingCostTable.DaysPerMonth
            * Grain
            * DifficultyRates.ExpenseFactor(difficulty)
            * line.DailyCost
            * LivingCostTable.FarmerMultiplierOf(origin, bracket)
            * LivingCostTable.GeneralZoneFactor(bracket == AgeBracket.Child, stage == FamineStage.Relief);

        Assert.Equal(Money.FromWen(perMember * expectedCount), line.Subtotal);
    }

    private static Money SumOfLines(LivingCostComputation computation)
    {
        var total = Money.Zero;

        foreach (var line in computation.Lines)
        {
            total += line.Subtotal;
        }

        return total;
    }

    private static Money Payable(Family family, GameDate date) =>
        LivingCostCalculator.Compute(
            CountedMembers.Counted(family),
            date,
            LivingStandard.Normal,
            Grain,
            Difficulty.Normal,
            Origin.Farmer,
            FamineStage.None).Payable;
}
