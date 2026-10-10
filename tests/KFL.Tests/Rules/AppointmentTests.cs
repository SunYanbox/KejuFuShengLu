using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Services;
using KFL.Rules.Career;
using KFL.Rules.Config;
using KFL.Rules.Settlement;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// quickstart S3、SC-003、SC-004：待阙（6~24、无俸、计时归零即授官）、四档授官映射与拒绝矩阵。
/// </summary>
/// <remarks>
/// 期望值一律经 <c>KFL.Rules/Config</c> 的成员取得（SC-009）；随机来自 <see cref="FixedRandomService"/>
/// 或 <see cref="SeededRandomService"/>。
/// </remarks>
public class AppointmentTests
{
    private static readonly Guid StateId = Guid.Parse("4d1e2f30-5a6b-4c7d-8e9f-0a1b2c3d4e5f");

    private static readonly GameDate Date = RulesHarness.Date;

    [Fact]
    public void 待阙时长百分之百落在六到二十四个月含端点()
    {
        for (var seed = 1; seed <= 200; seed++)
        {
            var person = Graduate(seed, ImperialClass.FirstClass);

            AppointmentEntry.BeginForImperialGraduate(person, Date, new SeededRandomService(seed));

            Assert.InRange(
                person.Timers.AwaitingPostRemainingMonths!.Value,
                GameConfig.Career.AwaitingPostMinMonths,
                GameConfig.Career.AwaitingPostMaxMonths);
        }
    }

    [Fact]
    public void 待阙时长的下界恰好是六()
    {
        var person = Graduate(1, ImperialClass.SecondClass);

        // FixedRandomService 的 Next 恒返回下界 ⇒ 恰好 6。
        AppointmentEntry.BeginForImperialGraduate(person, Date, new FixedRandomService(0.0d));

        Assert.Equal(GameConfig.Career.AwaitingPostMinMonths, person.Timers.AwaitingPostRemainingMonths.GetValueOrDefault(-1));
    }

    [Fact]
    public void 待阙时长的上界恰好是二十四()
    {
        var person = Graduate(2, ImperialClass.ThirdClass);

        // 恒返回上界的随机替身 ⇒ 恰好 24（证明 Next 的上界是开区间：取到了 max 而非 max - 1）。
        AppointmentEntry.BeginForImperialGraduate(person, Date, new UpperBoundRandomService());

        Assert.Equal(GameConfig.Career.AwaitingPostMaxMonths, person.Timers.AwaitingPostRemainingMonths.GetValueOrDefault(-1));
    }

    [Theory]
    [InlineData(AppointmentTrack.FirstClass)]
    [InlineData(AppointmentTrack.SecondClass)]
    [InlineData(AppointmentTrack.ThirdClass)]
    [InlineData(AppointmentTrack.SpecialTribute)]
    public void 待阙期满的当月授官且当月开始领俸(AppointmentTrack track)
    {
        var person = MemberFor(track, 10);
        var state = NewState(RulesHarness.FamilyOf(person));

        AppointmentEntry.Begin(person, track, Date, new FixedRandomService(0.0d));

        var remaining = person.Timers.AwaitingPostRemainingMonths.GetValueOrDefault(-1);
        Assert.Equal(GameConfig.Career.AwaitingPostMinMonths, remaining);

        // 待阙期内：无官阶、无俸禄条目、计时逐月递减。
        for (var index = 1; index < remaining; index++)
        {
            var during = Settle(state);

            Assert.Null(person.Rank);
            Assert.DoesNotContain(
                during.Incomes, I => I.Category == LedgerCategory.OfficialSalary);
        }

        // 递减到 0 的当月：授官 + 当月起领俸（③ 在收入之前，FR-019）。
        var appointed = Settle(state);

        Assert.NotNull(person.Rank);
        Assert.Equal(GameConfig.Career.InitialRankOf(track), person.Rank.GetValueOrDefault().Level);
        Assert.InRange(
            person.Rank.GetValueOrDefault().Level, SalaryTable.HighestLevel, SalaryTable.LowestLevel);

        Assert.False(person.Status.HasFlag(StatusFlag.AwaitingPost));
        Assert.Null(person.Timers.AwaitingPostRemainingMonths);

        // 授官当月即在职月数记 1（契约七 §4 条款 3：授官当月记 1）。
        Assert.Equal(1, person.MonthsInOffice);

        Assert.Contains(
            appointed.Incomes,
            I => I.Category == LedgerCategory.OfficialSalary && I.PersonId == person.Id);
        Assert.Single(appointed.Career!.Appointments);
        Assert.Equal(person.Id, appointed.Career.Appointments[0].PersonId);
        Assert.Equal(track, appointed.Career.Appointments[0].Track);
    }

    [Fact]
    public void 重复触发及第入仕被拒且剩余月数不被重置()
    {
        var person = Graduate(20, ImperialClass.FirstClass);

        AppointmentEntry.BeginForImperialGraduate(person, Date, new FixedRandomService(0.0d));
        var before = person.Timers.AwaitingPostRemainingMonths.GetValueOrDefault(-1);

        // 用「恒返回上界」的替身再次触发：若实现重掷，剩余月数会变成 24（这里断言它没变）。
        Assert.Throws<InvalidOperationException>(
            () => AppointmentEntry.BeginForImperialGraduate(person, Date, new UpperBoundRandomService()));

        Assert.Equal(before, person.Timers.AwaitingPostRemainingMonths.GetValueOrDefault(-1));
        Assert.Equal(GameConfig.Career.AwaitingPostMinMonths, before);
    }

    [Fact]
    public void 已有官阶者再次入仕被拒()
    {
        var person = Graduate(21, ImperialClass.FirstClass);
        person.Rank = new OfficialRank(GameConfig.Career.InitialRankOf(AppointmentTrack.FirstClass));

        Assert.Throws<InvalidOperationException>(
            () => AppointmentEntry.BeginForImperialGraduate(person, Date, new FixedRandomService(0.0d)));
        Assert.Throws<InvalidOperationException>(
            () => AppointmentEntry.Begin(person, AppointmentTrack.SpecialTribute, Date, new FixedRandomService(0.0d)));
    }

    [Fact]
    public void 末条功名不是进士时进士入口被拒()
    {
        var person = RulesHarness.Member(22, Gender.Male, 40);
        person.AppendDegree(new DegreeRecord(DegreeLevel.JuRen, null, Date, DegreeChangeCause.Initial));

        Assert.Throws<InvalidOperationException>(
            () => AppointmentEntry.BeginForImperialGraduate(person, Date, new FixedRandomService(0.0d)));
    }

    [Fact]
    public void 进士但甲第缺失时授官入口拒绝而不猜等级()
    {
        var person = RulesHarness.Member(23, Gender.Male, 40);
        person.AppendDegree(new DegreeRecord(DegreeLevel.JinShi, null, Date, DegreeChangeCause.ExamPass));

        Assert.Throws<InvalidOperationException>(
            () => AppointmentEntry.BeginForImperialGraduate(person, Date, new FixedRandomService(0.0d)));
    }

    [Fact]
    public void 名次是状元但甲第写成二甲在构造期被拒()
    {
        Assert.Throws<ArgumentException>(() => new DegreeRecord(
            DegreeLevel.JinShi,
            ImperialPlacement.ZhuangYuan,
            Date,
            DegreeChangeCause.ExamPass,
            ImperialClass.SecondClass));
    }

    [Fact]
    public void 待阙期内已亡或外嫁者不再递减计时也不被授官()
    {
        var head = RulesHarness.Member(32, Gender.Male, 50);
        var deceased = RulesHarness.Awaiting(RulesHarness.Member(30, Gender.Male, 40), remainingMonths: 6);
        var marriedOut = RulesHarness.Awaiting(RulesHarness.Member(31, Gender.Female, 40), remainingMonths: 6);

        var family = RulesHarness.FamilyOf(head, deceased, marriedOut);

        // 归档（已亡 / 外嫁）在入册之后发生，故家主的在册性不变量不受影响。
        deceased.Status |= StatusFlag.Deceased;
        marriedOut.Status |= StatusFlag.MarriedOut;

        for (var index = 0; index < 10; index++)
        {
            OfficialCareerAdvance.Run(family, RulesHarness.Date, new FixedRandomService(0.0d));
        }

        Assert.Null(deceased.Rank);
        Assert.Null(marriedOut.Rank);
        Assert.Equal(6, deceased.Timers.AwaitingPostRemainingMonths.GetValueOrDefault(-1));
        Assert.Equal(6, marriedOut.Timers.AwaitingPostRemainingMonths.GetValueOrDefault(-1));
    }

    private static Person MemberFor(AppointmentTrack track, int index)
    {
        if (track == AppointmentTrack.SpecialTribute)
        {
            return RulesHarness.Member(index, Gender.Male, 40);
        }

        var imperialClass = track switch
        {
            AppointmentTrack.FirstClass => ImperialClass.FirstClass,
            AppointmentTrack.SecondClass => ImperialClass.SecondClass,
            _ => ImperialClass.ThirdClass,
        };

        return Graduate(index, imperialClass);
    }

    /// <summary>造一名带进士功名（含甲第）的成员；一甲者配状元名次。</summary>
    private static Person Graduate(int index, ImperialClass imperialClass)
    {
        var person = RulesHarness.Member(index, Gender.Male, 40);
        var placement = imperialClass == ImperialClass.FirstClass ? ImperialPlacement.ZhuangYuan : (ImperialPlacement?)null;

        person.AppendDegree(new DegreeRecord(
            DegreeLevel.JinShi, placement, RulesHarness.Date, DegreeChangeCause.ExamPass, imperialClass));

        return person;
    }

    private static GameState NewState(Family family) => new(
        StateId,
        Date,
        Difficulty.Normal,
        Origin.Scholar,
        family,
        new FamilyEconomy(
            LivingCostTable.InitialStandard,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(Money.FromGuan(200m)),
            new Holdings()));

    private static SettlementResult Settle(GameState state) =>
        new MonthlySettlementEngine(new FixedRandomService(0.0d), new GameStateClock(state)).Settle(state);

    /// <summary>
    /// 恒返回**上界**的整数随机替身：<see cref="IRandomService.Next"/> 返回 <c>maxExclusive - 1</c>，
    /// 用于证明待阙时长的上界取到的是 24（而非 23）。
    /// </summary>
    private sealed class UpperBoundRandomService : IRandomService
    {
        /// <inheritdoc />
        public double NextDouble() => 0d;

        /// <inheritdoc />
        public int Next(int minInclusive, int maxExclusive) => maxExclusive - 1;

        /// <inheritdoc />
        public void NextBytes(Span<byte> destination) => destination.Clear();
    }
}
