using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;
using KFL.Rules.Exam;
using Xunit;

namespace KFL.Tests.Rules;

public partial class ExamSeasonTests
{
    [Fact]
    public void 免解者不掷骰直接中第并在快照标记()
    {
        var candidate = BaiShen(18, study: 0);
        RecordFailures(candidate, ExamLevel.JieShi, GameConfig.Exam.JieShiExemptionFailureThreshold);
        Registered(candidate, ExamLevel.JieShi);

        var state = NewState(RulesHarness.FamilyOf(candidate), JieShiMonth);
        var random = new RecordingRandom();

        var snapshot = ExamSeasonRunner.Run(state, random);

        // 免解 MUST NOT 消耗任何随机（SC-004）。
        Assert.Equal(0, random.NextDoubleCalls);
        Assert.Empty(random.NextCalls);

        Assert.Equal(DegreeLevel.JuRen, candidate.CurrentDegree);
        Assert.Contains(candidate.Id, snapshot.Exemptions);

        var outcome = Assert.Single(snapshot.Outcomes);
        Assert.True(outcome.Passed);
        Assert.True(outcome.Exempted);

        // 免解月的失败数不增长。
        Assert.Equal(GameConfig.Exam.JieShiExemptionFailureThreshold, candidate.JieShiFailureCount);
    }

    [Fact]
    public void 已越级者持解试标记时按资格不符跳过且不重复追加记录()
    {
        // 结构保证（契约十三 §2 ∧ §5 条款 3）：末条功名已达举人 ⇒ TargetLevelOf 升为省试，
        // 于是「判定前复核」先按「报名级别 ≠ 本月级别」跳过该成员，免解通道根本不会执行，
        // 因此**不可能**追加重复功名记录。§5 条款 3 的防御分支由 T021 的单元测试单独覆盖。
        var candidate = JuRen(19, study: 0);
        RecordFailures(candidate, ExamLevel.JieShi, GameConfig.Exam.JieShiExemptionFailureThreshold);
        Registered(candidate, ExamLevel.JieShi);

        var historyBefore = candidate.DegreeHistory.Count;
        var state = NewState(RulesHarness.FamilyOf(candidate), JieShiMonth);
        var random = new RecordingRandom();

        var snapshot = ExamSeasonRunner.Run(state, random);

        // 跳过 ⇒ 0 随机、0 中第记录、0 免解记录、0 功名变更。
        Assert.Equal(0, random.NextDoubleCalls);
        Assert.Empty(random.NextCalls);
        Assert.Empty(snapshot.Exemptions);
        Assert.Empty(snapshot.Outcomes);
        Assert.Equal(historyBefore, candidate.DegreeHistory.Count);

        var absence = Assert.Single(snapshot.Absences);
        Assert.Equal(candidate.Id, absence.PersonId);
        Assert.Equal(ExamAbsenceReason.IneligibleBeforeJudgement, absence.Reason);

        // 失败数不因跳过而增长；报名标记仍按 FR-005 清除（当月态）。
        Assert.Equal(GameConfig.Exam.JieShiExemptionFailureThreshold, candidate.JieShiFailureCount);
        Assert.Null(candidate.AppliedExam);
    }

    [Fact]
    public void 免解不跨级省试仍照常扣路费并掷骰()
    {
        var candidate = JuRen(20, study: 60);
        RecordFailures(candidate, ExamLevel.JieShi, GameConfig.Exam.JieShiExemptionFailureThreshold);
        Registered(candidate, ExamLevel.ShengShi);

        var state = NewState(RulesHarness.FamilyOf(candidate), ShengShiMonth, cash: 100);
        var random = new RecordingRandom([0.0d]);

        var snapshot = ExamSeasonRunner.Run(state, random);

        Assert.Equal(1, random.NextDoubleCalls);
        Assert.Empty(snapshot.Exemptions);
        Assert.Single(state.Economy.Ledger.Entries);
        Assert.Equal(DegreeLevel.GongShi, candidate.CurrentDegree);
    }

    // ------------------------------------------------------------------ 特奏名询问

    [Fact]
    public void 省试落第且满足门槛者产出特奏名询问且不改状态()
    {
        var candidate = JuRen(21, study: 60, age: 50);
        RecordFailures(candidate, ExamLevel.ShengShi, GameConfig.Exam.SpecialTributeFailureThreshold);
        Registered(candidate, ExamLevel.ShengShi);

        var state = NewState(RulesHarness.FamilyOf(candidate), ShengShiMonth, cash: 100);
        var historyBefore = candidate.DegreeHistory.Count;
        var random = new RecordingRandom([0.99d]);

        var snapshot = ExamSeasonRunner.Run(state, random);

        Assert.Contains(candidate.Id, snapshot.SpecialTributeInquiries);

        // 询问 MUST NOT 改变任何状态：不追加功名、不置任何位。
        Assert.Equal(historyBefore, candidate.DegreeHistory.Count);
        Assert.Null(candidate.Rank);
        Assert.False(candidate.Status.HasFlag(StatusFlag.AwaitingPost));
        Assert.Null(candidate.EntryTrack);
    }

    [Fact]
    public void 省试中第者不产出特奏名询问()
    {
        var candidate = JuRen(22, study: 60, age: 50);
        RecordFailures(candidate, ExamLevel.ShengShi, GameConfig.Exam.SpecialTributeFailureThreshold);
        Registered(candidate, ExamLevel.ShengShi);

        var state = NewState(RulesHarness.FamilyOf(candidate), ShengShiMonth, cash: 100);

        var snapshot = ExamSeasonRunner.Run(state, new RecordingRandom([0.0d]));

        Assert.Empty(snapshot.SpecialTributeInquiries);
    }

    [Fact]
    public void 解试与殿试月不产出特奏名询问()
    {
        var jieShi = BaiShen(23, study: 60, age: 50);
        var jieShiState = NewState(RulesHarness.FamilyOf(jieShi), JieShiMonth);

        Assert.Empty(ExamSeasonRunner.Run(jieShiState, new RecordingRandom([0.99d])).SpecialTributeInquiries);

        var dianShi = GongShi(24, study: 60, age: 50);
        var dianShiState = NewState(RulesHarness.FamilyOf(dianShi), DianShiMonth);

        Assert.Empty(ExamSeasonRunner.Run(dianShiState, new RecordingRandom([0.99d])).SpecialTributeInquiries);
    }

    // ------------------------------------------------------------------ 连续报考（FR-017）

    [Fact]
    public void 连续报考开关开启时同月自动报名并判定()
    {
        var candidate = BaiShen(25, study: 60);
        var family = RulesHarness.FamilyOf(candidate);
        family.AutoExamApplication = true;

        var state = NewState(family, JieShiMonth);
        var random = new RecordingRandom([0.0d]);

        var snapshot = ExamSeasonRunner.Run(state, random);

        var application = Assert.Single(snapshot.AutoApplications);

        Assert.Equal(candidate.Id, application.PersonId);
        Assert.Equal(ExamLevel.JieShi, application.Level);

        // 自动报名 0 随机，且同月直接判定。
        Assert.Equal(1, random.NextDoubleCalls);
        Assert.Equal(DegreeLevel.JuRen, candidate.CurrentDegree);
    }

    /// <summary>
    /// 玩家反馈 21：已经是举人的 MUST NOT 再被自动报考解试——举人的下一级是省试
    /// （<see cref="ExamRegistration.TargetLevelOf"/> 是唯一口径，自动报名按它筛）。
    /// </summary>
    [Fact]
    public void 已是举人者不被自动报考解试()
    {
        var juRen = JuRen(27, study: 60);
        var family = RulesHarness.FamilyOf(juRen);
        family.AutoExamApplication = true;

        var state = NewState(family, JieShiMonth);
        var random = new RecordingRandom();

        var snapshot = ExamSeasonRunner.Run(state, random);

        Assert.Empty(snapshot.AutoApplications);
        Assert.Empty(snapshot.Outcomes);
        Assert.Null(juRen.AppliedExam);
        Assert.Equal(0, random.NextDoubleCalls);
    }

    /// <summary>玩家反馈 21 的另一半：到了省试月，同一个举人会被正常自动报名并判定。</summary>
    [Fact]
    public void 已是举人者到省试月被自动报名()
    {
        var juRen = JuRen(28, study: 60);
        var family = RulesHarness.FamilyOf(juRen);
        family.AutoExamApplication = true;

        var state = NewState(family, ShengShiMonth, cash: 20m);
        var random = new RecordingRandom([0.0d]);

        var snapshot = ExamSeasonRunner.Run(state, random);

        var application = Assert.Single(snapshot.AutoApplications);
        Assert.Equal(ExamLevel.ShengShi, application.Level);
        Assert.Equal(DegreeLevel.GongShi, juRen.CurrentDegree);
    }

    [Fact]
    public void 连续报考开关关闭时不自动报名()
    {
        var candidate = BaiShen(26, study: 60);
        var family = RulesHarness.FamilyOf(candidate);

        var state = NewState(family, JieShiMonth);
        var random = new RecordingRandom();

        var snapshot = ExamSeasonRunner.Run(state, random);

        Assert.Empty(snapshot.AutoApplications);
        Assert.Empty(snapshot.Outcomes);
        Assert.Equal(0, random.NextDoubleCalls);
        Assert.Equal(DegreeLevel.BaiShen, candidate.CurrentDegree);
    }

    [Fact]
    public void 自动报名不覆盖已有标记()
    {
        var candidate = BaiShen(27, study: 60);
        candidate.ApplyForExam(ExamLevel.JieShi);

        var family = RulesHarness.FamilyOf(candidate);
        family.AutoExamApplication = true;

        var state = NewState(family, JieShiMonth);

        var snapshot = ExamSeasonRunner.Run(state, new RecordingRandom([0.0d]));

        Assert.Empty(snapshot.AutoApplications);
        Assert.Single(snapshot.Outcomes);
    }

    // ------------------------------------------------------------------ 快照契约

    [Fact]
    public void 快照集合按成员编号升序且为只读()
    {
        var low = BaiShen(31, study: 60);
        var high = BaiShen(32, study: 60);
        Registered(low, ExamLevel.JieShi);
        Registered(high, ExamLevel.JieShi);

        var state = NewState(RulesHarness.FamilyOf(high, low), JieShiMonth);

        var snapshot = ExamSeasonRunner.Run(state, new RecordingRandom([0.0d, 0.0d]));

        Assert.Equal(
            [low.Id, high.Id],
            snapshot.Outcomes.Select(outcome => outcome.PersonId));

        Assert.IsNotType<List<ExamOutcomeLine>>(snapshot.Outcomes);
    }

    // ------------------------------------------------------------------ 步位（第 ⑦ 步）

    [Fact]
    public void 结算引擎把科举季排在时间推进之前()
    {
        var candidate = GongShi(41, study: 60);
        var state = NewState(RulesHarness.FamilyOf(candidate), DianShiMonth);

        var result = Settle(state);

        // 快照月份 = 考试当月（尚未推进），而状态已经推进到次月。
        Assert.Equal(DianShiMonth, result.Month);
        Assert.Equal(ExamLevel.DianShi, result.ExamSeason.Level);
        Assert.Equal(DianShiMonth, result.ExamSeason.Month);
        Assert.Equal(new GameDate(80, 5), state.CurrentDate);
        Assert.Equal(DegreeLevel.JinShi, candidate.CurrentDegree);
    }

    [Fact]
    public void 结算引擎在非考试月给出空科举快照()
    {
        var candidate = BaiShen(42, study: 60);
        var state = NewState(RulesHarness.FamilyOf(candidate), PlainMonth);

        var result = Settle(state);

        Assert.Null(result.ExamSeason.Level);
        Assert.Empty(result.ExamSeason.Outcomes);
        Assert.Equal(PlainMonth, result.ExamSeason.Month);
    }

    // ------------------------------------------------------------------ 夹具
}
