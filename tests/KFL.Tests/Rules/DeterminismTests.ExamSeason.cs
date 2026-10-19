using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Services;
using KFL.Rules.Config;
using KFL.Rules.Settlement;
using Xunit;

namespace KFL.Tests.Rules;

public partial class DeterminismTests
{
    /// <summary>科举季的起点：科年 79 的 7 月 —— 下一步即 <c>(79, 8)</c> 解试。</summary>
    private static readonly GameDate BeforeJieShi = new(79, 7);

    /// <summary>一个完整科年的结算月数：<c>(79, 7) → (80, 5)</c>，覆盖 8 月解试 / 次年 3 月省试 / 4 月殿试。</summary>
    private const int KeYearMonths = 10;

    [Fact]
    public void 同种子两遍完整科年科举季逐字段差异项为零()
    {
        var first = RunKeYear(Seed);
        var second = RunKeYear(Seed);

        Assert.Equal(DescribeKeYear(first), DescribeKeYear(second));
        AssertSameKeYear(first, second);

        // 至少确实跑到了三个考试月，否则本节的「差异项为零」会静默假绿。
        Assert.Equal(
            new ExamLevel?[] { ExamLevel.JieShi, ExamLevel.ShengShi, ExamLevel.DianShi },
            first.Results.Select(result => result.ExamSeason.Level).Where(level => level is not null).ToArray());
    }

    /// <summary>
    /// 跑一个完整科年：家族同时含白身 / 举人 / 贡士各一名并开启持续报考，
    /// 使 8 月解试、次年 3 月省试、4 月殿试三段都被真实驱动。
    /// </summary>
    private static KeYearRun RunKeYear(int seed)
    {
        var baiShen = RulesHarness.Member(1, Gender.Male, 20);
        baiShen.Study = 60;

        var juRen = RulesHarness.Member(2, Gender.Male, 20);
        juRen.Study = 60;
        juRen.AppendDegree(new DegreeRecord(
            DegreeLevel.JuRen, null, new GameDate(79, 1), DegreeChangeCause.ExamPass, null));

        var gongShi = RulesHarness.Member(3, Gender.Male, 20);
        gongShi.Study = 60;
        gongShi.AppendDegree(new DegreeRecord(
            DegreeLevel.JuRen, null, new GameDate(79, 1), DegreeChangeCause.ExamPass, null));
        gongShi.AppendDegree(new DegreeRecord(
            DegreeLevel.GongShi, null, new GameDate(79, 2), DegreeChangeCause.ExamPass, null));

        var family = RulesHarness.FamilyOf(baiShen, juRen, gongShi);
        family.AutoExamApplication = true;

        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(Money.FromGuan(50m), Money.Zero, Money.Zero),
            new Holdings());

        var state = new GameState(
            StateId, BeforeJieShi, Difficulty.Normal, Origin.Scholar, family, economy);

        var random = new SeededRandomService(seed);
        var engine = new MonthlySettlementEngine(random, new GameStateClock(state), new FixedNameGenerator());
        var results = new List<SettlementResult>();

        for (var index = 0; index < KeYearMonths; index++)
        {
            results.Add(engine.Settle(state));
        }

        return new KeYearRun(state, results);
    }

    /// <summary>把一次科年运行的**全部可断言字段**摊平成一个可比较的字符串。</summary>
    private static string DescribeKeYear(KeYearRun run)
    {
        var lines = new List<string>();

        foreach (var result in run.Results)
        {
            var season = result.ExamSeason;

            lines.Add(string.Join(
                "|",
                season.Month,
                season.Level?.ToString() ?? "none",
                season.AutoApplications.Count,
                season.Absences.Count,
                season.Exemptions.Count,
                season.Outcomes.Count,
                season.Appointments.Count,
                season.SpecialTributeInquiries.Count,
                season.HasShiStatusBecomesTrue));

            lines.AddRange(season.Outcomes.Select(line => "O:" + line));
            lines.AddRange(season.Absences.Select(line => "A:" + line));
            lines.AddRange(season.Appointments.Select(line => "J:" + line));
        }

        foreach (var person in run.State.Family.Members)
        {
            lines.Add(string.Join(
                "|",
                person.Id,
                person.CurrentDegree,
                person.CurrentPlacement?.ToString() ?? "none",
                person.DegreeHistory.Count,
                person.JieShiFailureCount,
                person.ShengShiFailureCount,
                person.AppliedExam?.ToString() ?? "none",
                person.Rank?.Level ?? 0,
                person.MonthsInOffice,
                person.Status,
                person.Timers.AwaitingPostRemainingMonths ?? -1,
                person.EntryTrack?.ToString() ?? "none"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>除摊平比较外逐字段断言 SC-001 点名的科举季产物，避免 Describe 漏项时静默假绿。</summary>
    private static void AssertSameKeYear(KeYearRun first, KeYearRun second)
    {
        Assert.Equal(first.Results.Count, second.Results.Count);

        for (var index = 0; index < first.Results.Count; index++)
        {
            var expected = first.Results[index].ExamSeason;
            var actual = second.Results[index].ExamSeason;

            Assert.Equal(expected.Month, actual.Month);
            Assert.Equal(expected.Level, actual.Level);
            Assert.Equal(expected.AutoApplications, actual.AutoApplications);
            Assert.Equal(expected.Absences, actual.Absences);
            Assert.Equal(expected.Exemptions, actual.Exemptions);
            Assert.Equal(expected.Outcomes, actual.Outcomes);
            Assert.Equal(expected.Appointments, actual.Appointments);
            Assert.Equal(expected.SpecialTributeInquiries, actual.SpecialTributeInquiries);
            Assert.Equal(expected.HasShiStatusBecomesTrue, actual.HasShiStatusBecomesTrue);
        }

        Assert.Equal(first.State.CurrentDate, second.State.CurrentDate);
        Assert.Equal(first.State.Family.HasShiStatus, second.State.Family.HasShiStatus);

        var firstMembers = first.State.Family.Members.ToList();
        var secondMembers = second.State.Family.Members.ToList();

        Assert.Equal(firstMembers.Count, secondMembers.Count);

        for (var index = 0; index < firstMembers.Count; index++)
        {
            Assert.Equal(firstMembers[index].JieShiFailureCount, secondMembers[index].JieShiFailureCount);
            Assert.Equal(firstMembers[index].ShengShiFailureCount, secondMembers[index].ShengShiFailureCount);
            Assert.Equal(firstMembers[index].AppliedExam, secondMembers[index].AppliedExam);
            Assert.Equal(firstMembers[index].DegreeHistory, secondMembers[index].DegreeHistory);
            Assert.Equal(firstMembers[index].CurrentDegree, secondMembers[index].CurrentDegree);
            Assert.Equal(firstMembers[index].CurrentPlacement, secondMembers[index].CurrentPlacement);
            Assert.Equal(firstMembers[index].EntryTrack, secondMembers[index].EntryTrack);
            Assert.Equal(firstMembers[index].Timers, secondMembers[index].Timers);
            Assert.Equal(firstMembers[index].Status, secondMembers[index].Status);
        }

        var firstEntries = first.State.Economy.Ledger.Entries;
        var secondEntries = second.State.Economy.Ledger.Entries;

        Assert.Equal(firstEntries.Count, secondEntries.Count);

        for (var index = 0; index < firstEntries.Count; index++)
        {
            Assert.Equal(firstEntries[index], secondEntries[index]);
        }
    }

    /// <summary>一次科年运行的产物：收尾存档与逐月结算快照。</summary>
    private sealed record KeYearRun(GameState State, List<SettlementResult> Results);
}
