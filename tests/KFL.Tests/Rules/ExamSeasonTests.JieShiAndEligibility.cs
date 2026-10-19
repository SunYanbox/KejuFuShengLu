using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Career;
using KFL.Rules.Config;
using KFL.Rules.Exam;
using Xunit;

namespace KFL.Tests.Rules;

public partial class ExamSeasonTests
{
    [Fact]
    public void 非考试月零随机零变更()
    {
        var candidate = BaiShen(1, study: 60);
        var state = NewState(RulesHarness.FamilyOf(candidate), PlainMonth);
        var random = new RecordingRandom();

        var snapshot = ExamSeasonRunner.Run(state, random);

        Assert.Equal(PlainMonth, snapshot.Month);
        Assert.Null(snapshot.Level);
        Assert.Empty(snapshot.AutoApplications);
        Assert.Empty(snapshot.Absences);
        Assert.Empty(snapshot.Exemptions);
        Assert.Empty(snapshot.Outcomes);
        Assert.Empty(snapshot.Appointments);
        Assert.Empty(snapshot.SpecialTributeInquiries);
        Assert.False(snapshot.HasShiStatusBecomesTrue);

        // 随机消费 0 次、账本 0 条、功名史与失败数不变。
        Assert.Equal(0, random.NextDoubleCalls);
        Assert.Empty(random.NextCalls);
        Assert.Empty(state.Economy.Ledger.Entries);
        Assert.Empty(candidate.DegreeHistory);
        Assert.Equal(0, candidate.JieShiFailureCount);
        Assert.Equal(0, candidate.ShengShiFailureCount);
    }

    // ------------------------------------------------------------------ 解试

    [Fact]
    public void 解试中第追加举人功名并清除报名标记()
    {
        var candidate = Registered(BaiShen(1, study: 60), ExamLevel.JieShi);
        var state = NewState(RulesHarness.FamilyOf(candidate), JieShiMonth);
        var random = new RecordingRandom([0.0d]);

        var snapshot = ExamSeasonRunner.Run(state, random);

        Assert.Equal(ExamLevel.JieShi, snapshot.Level);
        Assert.Equal(1, random.NextDoubleCalls);

        var record = Assert.Single(candidate.DegreeHistory);
        Assert.Equal(DegreeLevel.JuRen, record.Level);
        Assert.Equal(DegreeChangeCause.ExamPass, record.Cause);
        Assert.Equal(JieShiMonth, record.ChangedAt);
        Assert.Null(record.Class);
        Assert.Equal(DegreeLevel.JuRen, candidate.CurrentDegree);

        var outcome = Assert.Single(snapshot.Outcomes);
        Assert.True(outcome.Passed);
        Assert.False(outcome.Exempted);
        Assert.Equal(DegreeLevel.JuRen, outcome.DegreeAfter);
        Assert.Equal(0, outcome.FailureCountAfter);

        // 报名标记是当月态：本月结算结束后必为 null。
        Assert.Null(candidate.AppliedExam);
    }

    /// <summary>
    /// 玩家反馈 8 / 16（规格书 §8.2 的 2026-10-19 裁决）：中举**即授 L17 官职**，
    /// 且 MUST NOT 设「待阙」——待阙会把该成员的省试资格一并挡掉（<c>AwaitingPost</c> 是失格条件）。
    /// </summary>
    [Fact]
    public void 解试中第即授举人官阶且不待阙()
    {
        var candidate = Registered(BaiShen(7, study: 60), ExamLevel.JieShi);
        var state = NewState(RulesHarness.FamilyOf(candidate), JieShiMonth);
        var random = new RecordingRandom([0.0d]);

        var snapshot = ExamSeasonRunner.Run(state, random);

        var rank = candidate.Rank ?? throw new InvalidOperationException("测试前提失败：中举即授官。");
        Assert.Equal(17, rank.Level);
        Assert.Equal(OfficialCareerPolicy.InitialRankOf(AppointmentTrack.JuRen), rank.Level);
        Assert.Equal(0, candidate.MonthsInOffice);

        // 「举人即到任」不是「及第入仕」：没有 AwaitingPost、没有待阙入口轨迹。
        Assert.False(candidate.Status.HasFlag(StatusFlag.AwaitingPost));
        Assert.Null(candidate.EntryTrack);

        var appointment = Assert.Single(snapshot.Appointments);
        Assert.Equal(candidate.Id, appointment.PersonId);
        Assert.Equal(AppointmentTrack.JuRen, appointment.Track);
        Assert.Equal(0, appointment.AwaitingPostMonths);
    }

    /// <summary>玩家反馈 8 / 16：已受官的举人仍能应省试——官阶不是失格条件。</summary>
    [Fact]
    public void 已受官举人仍可被判定省试()
    {
        var candidate = JuRen(8, study: 60);
        Assert.True(AppointmentEntry.BeginForJuRen(candidate));
        Registered(candidate, ExamLevel.ShengShi);
        var state = NewState(RulesHarness.FamilyOf(candidate), ShengShiMonth, cash: 20m);
        var random = new RecordingRandom([0.0d]);

        var snapshot = ExamSeasonRunner.Run(state, random);

        var outcome = Assert.Single(snapshot.Outcomes);
        Assert.True(outcome.Passed);
        Assert.Equal(DegreeLevel.GongShi, candidate.CurrentDegree);

        // 官阶不动：省试中第只是功名升了一级。
        var rank = candidate.Rank ?? throw new InvalidOperationException("测试前提失败：举人应已有官阶。");
        Assert.Equal(17, rank.Level);
    }

    [Fact]
    public void 解试落第累计失败数且不追加功名()
    {
        var candidate = Registered(BaiShen(2, study: 60), ExamLevel.JieShi);
        var state = NewState(RulesHarness.FamilyOf(candidate), JieShiMonth);
        var random = new RecordingRandom([0.99d]);

        var snapshot = ExamSeasonRunner.Run(state, random);

        Assert.Empty(candidate.DegreeHistory);
        Assert.Equal(DegreeLevel.BaiShen, candidate.CurrentDegree);
        Assert.Equal(1, candidate.JieShiFailureCount);
        Assert.Equal(0, candidate.ShengShiFailureCount);
        Assert.Null(candidate.AppliedExam);

        var outcome = Assert.Single(snapshot.Outcomes);
        Assert.False(outcome.Passed);
        Assert.Equal(1, outcome.FailureCountAfter);
    }

    [Fact]
    public void 资格不符者在判定前被跳过且不累计失败数()
    {
        // 报名后失去资格：被禁考（条件 ④）。2026-10-19 需求 8/21 后「官阶在身」不再是失格条件。
        var candidate = Registered(BaiShen(3, study: 60), ExamLevel.JieShi);
        candidate.Status |= StatusFlag.ExamBanned;
        var state = NewState(RulesHarness.FamilyOf(candidate), JieShiMonth);
        var random = new RecordingRandom();

        var snapshot = ExamSeasonRunner.Run(state, random);

        Assert.Equal(0, random.NextDoubleCalls);
        Assert.Empty(snapshot.Outcomes);

        var absence = Assert.Single(snapshot.Absences);
        Assert.Equal(candidate.Id, absence.PersonId);
        Assert.Equal(ExamAbsenceReason.IneligibleBeforeJudgement, absence.Reason);

        // 跳过 ≠ 落第：失败数 MUST NOT 变化。
        Assert.Equal(0, candidate.JieShiFailureCount);
        Assert.Null(candidate.AppliedExam);
    }

    // ------------------------------------------------------------------ 省试与路费（SC-006）
}
