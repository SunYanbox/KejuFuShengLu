using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Services;
using KFL.Rules.Career;
using KFL.Rules.Config;
using KFL.Rules.Settlement;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// quickstart S5、SC-007：70 岁致仕、半俸、政绩与在职计时停摆、幂等与同月边界。
/// </summary>
/// <remarks>
/// 期望值一律经 <c>KFL.Rules/Config</c> 的成员取得（SC-009）。结算月取 <c>(80, 2)</c>，
/// 官员的出生月与之相同 ⇒ 该月**恰好满 70 岁**（生日当月即计入）。
/// </remarks>
public class RetirementTests
{
    private static readonly Guid StateId = Guid.Parse("5e2f3a41-6b7c-4d8e-9f01-1a2b3c4d5e6f");

    /// <summary>结算月：官员在该月满 70 岁。</summary>
    private static readonly GameDate Month = new(80, 2);

    [Fact]
    public void 满七十岁当月致仕且官阶保留()
    {
        var person = Turning70(1);
        person.Rank = new OfficialRank(15);
        person.Merit = 10;
        person.MonthsInOffice = 20;

        var state = NewState(RulesHarness.FamilyOf(person));
        var result = Settle(state);

        Assert.Equal(OfficialCareerPolicy.RetirementAge, person.AgeAt(Month));
        Assert.True(person.Status.HasFlag(StatusFlag.Retired));
        Assert.Equal(15, person.Rank!.Value.Level);
        Assert.Contains(person.Id, result.Career!.Retirements);

        // ③-b 先于 ③-c：满 70 岁当月仍有一次政绩 +1；③-c 先于 ③-d：该月不再推进在职计时。
        Assert.Equal(11, person.Merit);
        Assert.Contains(person.Id, result.Career.MeritGains.Keys);
        Assert.Equal(20, person.MonthsInOffice);
    }

    [Fact]
    public void 满七十岁当月起按半俸计且含难度系数与士出身加成()
    {
        var person = Turning70(2);
        person.Rank = new OfficialRank(15);
        person.Merit = 0;

        var state = NewState(RulesHarness.FamilyOf(person));
        var result = Settle(state);

        Assert.Equal(SalaryMode.Retired, SalaryModePolicy.Of(person));

        var expected = RulesHarness.Guan(
            SalaryTable.MonthlySalaryGuan(15)
            * DifficultyRates.RevenueFactor(Difficulty.Normal)
            * SalaryTable.ScholarOriginMultiplier
            * OfficialCareerPolicy.RetirementSalaryRatio);

        var line = Assert.Single(
            result.Incomes, I => I.Category == LedgerCategory.OfficialSalary && I.PersonId == person.Id);

        Assert.Equal(expected, line.Amount);
    }

    [Fact]
    public void 致仕后政绩与在职计时都不再变化()
    {
        var person = Turning70(3);
        person.Rank = new OfficialRank(15);
        person.Merit = 5;
        person.MonthsInOffice = 20;

        var state = NewState(RulesHarness.FamilyOf(person));
        Settle(state);

        var meritAfterRetirement = person.Merit;
        var officeMonthsAfterRetirement = person.MonthsInOffice;

        var second = Settle(state);

        Assert.Equal(meritAfterRetirement, person.Merit);
        Assert.Equal(officeMonthsAfterRetirement, person.MonthsInOffice);
        Assert.DoesNotContain(person.Id, second.Career!.MeritGains.Keys);
        Assert.Empty(second.Career.Retirements);

        // 幂等：再判无变化，且半俸 MUST NOT 累乘成 25%。
        Assert.Equal(SalaryMode.Retired, SalaryModePolicy.Of(person));
    }

    [Fact]
    public void 致仕半俸不累乘且等于在任态乘半俸比例()
    {
        var person = Turning70(4);
        person.Rank = new OfficialRank(15);
        person.Merit = 0;

        var state = NewState(RulesHarness.FamilyOf(person));
        var retirementMonth = Settle(state);
        var nextMonth = Settle(state);

        var retired = Amount(retirementMonth, person.Id);

        // 半俸**只乘一次**：次月与当月相同，且恰为在任全俸 × RetirementSalaryRatio。
        Assert.Equal(retired, Amount(nextMonth, person.Id));
        Assert.Equal(
            RulesHarness.Guan(
                SalaryTable.MonthlySalaryGuan(15)
                * DifficultyRates.RevenueFactor(Difficulty.Normal)
                * SalaryTable.ScholarOriginMultiplier),
            RulesHarness.Guan(retired.Guan / OfficialCareerPolicy.RetirementSalaryRatio));
    }

    [Fact]
    public void 从未有官职的七十岁成员不致仕()
    {
        var person = Turning70(5);
        person.Merit = 0;

        var state = NewState(RulesHarness.FamilyOf(person));
        var result = Settle(state);

        Assert.Equal(OfficialCareerPolicy.RetirementAge, person.AgeAt(Month));
        Assert.False(person.Status.HasFlag(StatusFlag.Retired));
        Assert.Empty(result.Career!.Retirements);
        Assert.DoesNotContain(result.Incomes, I => I.Category == LedgerCategory.OfficialSalary);
    }

    [Fact]
    public void 致仕与患病禁考等状态位并存互不覆盖()
    {
        var person = Turning70(6);
        person.Rank = new OfficialRank(15);
        person.Status |= StatusFlag.Ill | StatusFlag.ExamBanned;

        var state = NewState(RulesHarness.FamilyOf(person));
        Settle(state);

        Assert.True(person.Status.HasFlag(StatusFlag.Retired));
        Assert.True(person.Status.HasFlag(StatusFlag.Ill));
        Assert.True(person.Status.HasFlag(StatusFlag.ExamBanned));
    }

    [Fact]
    public void 同月待阙期满且满七十岁先授官后致仕并按新官阶计半俸()
    {
        var person = Turning70(7);

        // 一甲进士：待阙期满授 L11，随后当月致仕 ⇒ 按 L11 的半俸计。
        person.AppendDegree(new DegreeRecord(
            DegreeLevel.JinShi,
            ImperialPlacement.ZhuangYuan,
            Month,
            DegreeChangeCause.ExamPass,
            ImperialClass.FirstClass));

        RulesHarness.Awaiting(person, remainingMonths: 1);

        var state = NewState(RulesHarness.FamilyOf(person));
        var result = Settle(state);

        var expectedLevel = GameConfig.Career.InitialRankOf(AppointmentTrack.FirstClass);

        Assert.Equal(expectedLevel, person.Rank!.Value.Level);
        Assert.True(person.Status.HasFlag(StatusFlag.Retired));
        Assert.Single(result.Career!.Appointments);
        Assert.Contains(person.Id, result.Career.Retirements);

        Assert.Equal(
            RulesHarness.Guan(
                SalaryTable.MonthlySalaryGuan(expectedLevel)
                * DifficultyRates.RevenueFactor(Difficulty.Normal)
                * SalaryTable.ScholarOriginMultiplier
                * OfficialCareerPolicy.RetirementSalaryRatio),
            Amount(result, person.Id));
    }

    [Fact]
    public void 考课成功同月满七十岁时不晋升()
    {
        var person = Turning70(8);
        person.Rank = new OfficialRank(15);
        person.Merit = 0;
        person.MonthsInOffice = GameConfig.Career.AppraisalPeriodMonths - 1;

        var state = NewState(RulesHarness.FamilyOf(person));

        // 随机取值 0：若该月参与考课必晋升；③-c 先于 ③-d ⇒ MUST NOT 晋升。
        var result = new MonthlySettlementEngine(new FixedRandomService(0.0d), new GameStateClock(state)).Settle(state);

        Assert.True(person.Status.HasFlag(StatusFlag.Retired));
        Assert.Equal(15, person.Rank!.Value.Level);
        Assert.Empty(result.Career!.Promotions);
        Assert.Equal(GameConfig.Career.AppraisalPeriodMonths - 1, person.MonthsInOffice);
    }

    [Fact]
    public void 服刑者的政绩与在职计时暂停且计时不重置()
    {
        var paused = RulesHarness.ServingSentence(RulesHarness.Official(11, level: 15, monthsInOffice: 5, merit: 10), 60);
        var dueAtAppraisal = RulesHarness.ServingSentence(
            RulesHarness.Official(
                12, level: 15, monthsInOffice: GameConfig.Career.AppraisalPeriodMonths, merit: 10),
            60);

        var family = RulesHarness.FamilyOf(paused, dueAtAppraisal);
        var result = OfficialCareerAdvance.Run(family, RulesHarness.Date, new FixedRandomService(0.0d));

        // 不推进、也不重置（Q5：刑满后从暂停处继续）。
        Assert.Equal(10, paused.Merit);
        Assert.Equal(5, paused.MonthsInOffice);
        Assert.Equal(10, dueAtAppraisal.Merit);
        Assert.Equal(GameConfig.Career.AppraisalPeriodMonths, dueAtAppraisal.MonthsInOffice);

        // 「暂停」与「跳过」是两种语义：只有服刑者进 AppraisalPaused，且不进 AppraisalSkipped。
        Assert.Contains(dueAtAppraisal.Id, result.AppraisalPaused);
        Assert.Empty(result.AppraisalSkipped);
    }

    private static Money Amount(SettlementResult result, PersonId personId)
    {
        var total = Money.Zero;

        foreach (var line in result.Incomes)
        {
            if (line.Category == LedgerCategory.OfficialSalary && line.PersonId == personId)
            {
                total += line.Amount;
            }
        }

        return total;
    }

    /// <summary>在该结算月**恰好**满 70 岁的成员（出生月 = 结算月 ⇒ 生日当月即计入）。</summary>
    private static Person Turning70(int index) => FamilyFixtures.NewPerson(
        index,
        $"P{index}",
        Gender.Male,
        Month.Year - OfficialCareerPolicy.RetirementAge,
        Month.Month,
        generation: 0,
        lifespan: 100);

    private static GameState NewState(Family family) => new(
        StateId,
        Month,
        Difficulty.Normal,
        Origin.Scholar,
        family,
        new FamilyEconomy(
            LivingCostTable.InitialStandard,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(Money.FromGuan(500m)),
            new Holdings()));

    private static SettlementResult Settle(GameState state) =>
        new MonthlySettlementEngine(new FixedRandomService(0.0d), new GameStateClock(state)).Settle(state);
}
