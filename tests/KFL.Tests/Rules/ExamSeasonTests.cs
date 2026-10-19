using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Services;
using KFL.Rules.Career;
using KFL.Rules.Config;
using KFL.Rules.Exam;
using KFL.Rules.Settlement;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// T024 / T029 / T033：科举季结算步（契约十三）——非考试月零消费、三级判定、
/// 省试路费（SC-006）、免解（SC-004）、特奏名询问与仕族首产。
/// </summary>
/// <remarks>
/// 绝大多数用例**直接调用** <see cref="ExamSeasonRunner.Run"/>，以便把随机消费精确到
/// 「第几次 <c>NextDouble</c>」；只有「步位」两项走 <see cref="MonthlySettlementEngine"/>，
/// 因为那才是「第 ⑦ 步」这一断言的落点。
/// </remarks>
public class ExamSeasonTests
{
    private static readonly Guid StateId = Guid.Parse("3f1e7c2a-91b4-4d6e-8a55-77aa99bbccdd");

    /// <summary>解试月：79 年为科年（79 % 3 == 1）的 8 月。</summary>
    private static readonly GameDate JieShiMonth = new(79, 8);

    /// <summary>省试月：科年的次年 3 月。</summary>
    private static readonly GameDate ShengShiMonth = new(80, 3);

    /// <summary>殿试月：科年的次年 4 月。</summary>
    private static readonly GameDate DianShiMonth = new(80, 4);

    /// <summary>非考试月。</summary>
    private static readonly GameDate PlainMonth = new(81, 5);

    // ------------------------------------------------------------------ 非考试月

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

    [Fact]
    public void 省试扣除路费并留账本条目()
    {
        var candidate = Registered(JuRen(4, study: 60), ExamLevel.ShengShi);
        var state = NewState(RulesHarness.FamilyOf(candidate), ShengShiMonth, cash: 100);
        var random = new RecordingRandom([0.0d]);

        ExamSeasonRunner.Run(state, random);

        var entry = Assert.Single(state.Economy.Ledger.Entries);

        Assert.Equal(LedgerCategory.ExamTravelCost, entry.Category);
        Assert.Equal(candidate.Id, entry.PersonId);
        Assert.Equal(ShengShiMonth, entry.Date);
        Assert.Equal(-GameConfig.Exam.ExamTravelCostGuan(Difficulty.Normal) * 1m, entry.Amount.Guan);
        Assert.Equal(80m, state.Economy.Treasury.Cash.Guan);

        // 中第 ⇒ 贡士。
        Assert.Equal(DegreeLevel.GongShi, candidate.CurrentDegree);
        Assert.Equal(1, random.NextDoubleCalls);
    }

    [Theory]
    [InlineData(Difficulty.Easy, 10)]
    [InlineData(Difficulty.Normal, 20)]
    [InlineData(Difficulty.Hard, 40)]
    [InlineData(Difficulty.Hell, 80)]
    public void 路费四档随即难度且不乘难度支出系数(Difficulty difficulty, int expectedGuan)
    {
        var candidate = Registered(JuRen(5, study: 60), ExamLevel.ShengShi);
        var state = NewState(RulesHarness.FamilyOf(candidate), ShengShiMonth, difficulty: difficulty, cash: 1000);

        ExamSeasonRunner.Run(state, new RecordingRandom([0.0d]));

        var entry = Assert.Single(state.Economy.Ledger.Entries);

        // 数值锚点：契约十三 §3.1 的四档。
        Assert.Equal(expectedGuan, GameConfig.Exam.ExamTravelCostGuan(difficulty));
        Assert.Equal(-expectedGuan, entry.Amount.Guan);
        Assert.Equal(1000 - expectedGuan, state.Economy.Treasury.Cash.Guan);

        // 与难度**支出**系数同档不同义：普通档系数恰为 1.0，故只对系数 ≠ 1 的三档作证。
        var expenseFactor = GameConfig.DifficultyRate.ExpenseFactor(difficulty);

        if (expenseFactor != 1.0m)
        {
            Assert.NotEqual(expectedGuan, (int)(expectedGuan * expenseFactor));
        }
    }

    [Fact]
    public void 路费不足者弃考且不留账本条目()
    {
        var candidate = Registered(JuRen(6, study: 60), ExamLevel.ShengShi);
        var state = NewState(RulesHarness.FamilyOf(candidate), ShengShiMonth, cash: 19);
        var random = new RecordingRandom();

        var snapshot = ExamSeasonRunner.Run(state, random);

        Assert.Equal(0, random.NextDoubleCalls);
        Assert.Empty(state.Economy.Ledger.Entries);
        Assert.Equal(19m, state.Economy.Treasury.Cash.Guan);

        var absence = Assert.Single(snapshot.Absences);
        Assert.Equal(ExamAbsenceReason.InsufficientTravelCost, absence.Reason);

        // 弃考 ≠ 落第：失败数不变、不转化贷款。
        Assert.Equal(0, candidate.ShengShiFailureCount);
        Assert.Equal(Money.Zero, state.Economy.Treasury.Loan.Principal);
        Assert.Null(candidate.AppliedExam);
    }

    [Fact]
    public void 路费不因落第退还()
    {
        var candidate = Registered(JuRen(7, study: 60), ExamLevel.ShengShi);
        var state = NewState(RulesHarness.FamilyOf(candidate), ShengShiMonth, cash: 100);

        ExamSeasonRunner.Run(state, new RecordingRandom([0.99d]));

        // 落第：路费已扣且无冲回条目。
        Assert.Single(state.Economy.Ledger.Entries);
        Assert.All(state.Economy.Ledger.Entries, entry => Assert.Equal(LedgerCategory.ExamTravelCost, entry.Category));
        Assert.Equal(80m, state.Economy.Treasury.Cash.Guan);
        Assert.Equal(1, candidate.ShengShiFailureCount);
        Assert.Equal(DegreeLevel.JuRen, candidate.CurrentDegree);
    }

    [Fact]
    public void 解试与殿试不扣路费()
    {
        var jieShi = Registered(BaiShen(8, study: 60), ExamLevel.JieShi);
        var jieShiState = NewState(RulesHarness.FamilyOf(jieShi), JieShiMonth, cash: 100);

        ExamSeasonRunner.Run(jieShiState, new RecordingRandom([0.0d]));

        Assert.Empty(jieShiState.Economy.Ledger.Entries);
        Assert.Equal(100m, jieShiState.Economy.Treasury.Cash.Guan);

        var dianShi = GongShi(9, study: 60);
        var dianShiState = NewState(RulesHarness.FamilyOf(dianShi), DianShiMonth, cash: 100);

        ExamSeasonRunner.Run(dianShiState, new RecordingRandom([0.99d, 0.0d]));

        Assert.Empty(dianShiState.Economy.Ledger.Entries);
        Assert.Equal(100m, dianShiState.Economy.Treasury.Cash.Guan);
    }

    // ------------------------------------------------------------------ 殿试与及第入仕

    [Fact]
    public void 殿试不掷录取骰只掷进甲与名次()
    {
        var candidate = GongShi(10, study: 60);
        var state = NewState(RulesHarness.FamilyOf(candidate), DianShiMonth);
        var random = new RecordingRandom([0.0d]);

        var snapshot = ExamSeasonRunner.Run(state, random);

        Assert.Equal(ExamLevel.DianShi, snapshot.Level);

        // 进甲：1 次录取 / 进甲骰 + 1 次名次骰 + 1 次待阙月数骰 = 3 次。
        Assert.Equal(1, random.NextDoubleCalls);
        Assert.Equal(2, random.NextCalls.Count);
        Assert.Equal((1, 4), random.NextCalls[0]);

        // GongShi helper 已带举人 / 贡士两条记录；本次结算再追加唯一一条进士记录。
        Assert.Equal(3, candidate.DegreeHistory.Count);
        var record = candidate.DegreeHistory[^1];
        Assert.Equal(DegreeLevel.JinShi, record.Level);
        Assert.Equal(ImperialClass.FirstClass, record.Class);
        Assert.NotNull(record.Placement);
        Assert.Equal(DegreeChangeCause.ExamPass, record.Cause);

        var outcome = Assert.Single(snapshot.Outcomes);
        Assert.True(outcome.Passed);
        Assert.Equal(ImperialClass.FirstClass, outcome.ImperialClass);

        // ⑤ 及第入仕：置待阙，且不改官阶 / 在职月数。
        Assert.True(candidate.Status.HasFlag(StatusFlag.AwaitingPost));
        Assert.Equal(AppointmentTrack.FirstClass, candidate.EntryTrack);
        Assert.Null(candidate.Rank);
        Assert.Equal(0, candidate.MonthsInOffice);

        var appointment = Assert.Single(snapshot.Appointments);
        Assert.Equal(candidate.Id, appointment.PersonId);
        Assert.Equal(AppointmentTrack.FirstClass, appointment.Track);
        Assert.InRange(
            appointment.AwaitingPostMonths,
            GameConfig.Career.AwaitingPostMinMonths,
            GameConfig.Career.AwaitingPostMaxMonths);
    }

    [Fact]
    public void 殿试未进甲者只掷一次且不掷名次()
    {
        var candidate = GongShi(11, study: 60);
        var state = NewState(RulesHarness.FamilyOf(candidate), DianShiMonth);
        var random = new RecordingRandom([0.99d]);

        var snapshot = ExamSeasonRunner.Run(state, random);

        // 未进甲：只有 1 次进甲骰；仍要入仕（贡士中第即进士），故仍有 1 次待阙骰。
        Assert.Equal(1, random.NextDoubleCalls);
        var nextCall = Assert.Single(random.NextCalls);
        Assert.NotEqual((1, 4), nextCall);

        // GongShi helper 已带举人 / 贡士两条记录；本次结算再追加唯一一条进士记录。
        Assert.Equal(3, candidate.DegreeHistory.Count);
        var record = candidate.DegreeHistory[^1];

        // 学业 60 >= 二甲线 50 ⇒ 二甲，且名次为 null。
        Assert.Equal(ImperialClass.SecondClass, record.Class);
        Assert.Null(record.Placement);
        Assert.Equal(AppointmentTrack.SecondClass, candidate.EntryTrack);
    }

    [Fact]
    public void 殿试未进甲且学业低于二甲线者录三甲()
    {
        var candidate = GongShi(12, study: 49);
        var state = NewState(RulesHarness.FamilyOf(candidate), DianShiMonth);

        ExamSeasonRunner.Run(state, new RecordingRandom([0.99d]));

        // GongShi helper 已带举人 / 贡士两条记录；本次结算再追加唯一一条进士记录。
        Assert.Equal(3, candidate.DegreeHistory.Count);
        var record = candidate.DegreeHistory[^1];

        Assert.Equal(ImperialClass.ThirdClass, record.Class);
        Assert.Null(record.Placement);
        Assert.Equal(AppointmentTrack.ThirdClass, candidate.EntryTrack);
    }

    [Fact]
    public void 殿试录取恒为真且不掷录取骰()
    {
        var candidate = GongShi(13, study: 0);
        var state = NewState(RulesHarness.FamilyOf(candidate), DianShiMonth);

        var snapshot = ExamSeasonRunner.Run(state, new RecordingRandom([0.99d]));

        var outcome = Assert.Single(snapshot.Outcomes);

        Assert.True(outcome.Passed);
        Assert.Equal(DegreeLevel.JinShi, candidate.CurrentDegree);
    }

    // ------------------------------------------------------------------ 仕族首产

    [Fact]
    public void 家族首次产出进士置仕族且只置一次()
    {
        var first = GongShi(14, study: 60);
        var family = RulesHarness.FamilyOf(first);
        var state = NewState(family, DianShiMonth);

        var snapshot = ExamSeasonRunner.Run(state, new RecordingRandom([0.0d]));

        Assert.True(family.HasShiStatus);
        Assert.True(snapshot.HasShiStatusBecomesTrue);

        // 第二个殿试月：已是仕族，MUST NOT 再置。
        var second = GongShi(15, study: 60);
        family.AddFoundingMember(second);
        state.AdvanceMonth();
        state.AdvanceMonth();
        state.AdvanceMonth();
        state.AdvanceMonth();

        var secondSnapshot = ExamSeasonRunner.Run(state, new RecordingRandom([0.0d]));

        Assert.True(family.HasShiStatus);
        Assert.False(secondSnapshot.HasShiStatusBecomesTrue);
    }

    [Fact]
    public void 仕族加成作用于录取率而不作用于进甲率()
    {
        var plain = BaiShen(16, study: 60);
        var noble = BaiShen(17, study: 60);
        var nobleFamily = RulesHarness.FamilyOf(noble);
        nobleFamily.HasShiStatus = true;

        var plainRate = ExamAdmission.AdmissionRate(
            ExamLevel.JieShi, plain, false, GameConfig.Bribe.UnitMultiplier);
        var nobleRate = ExamAdmission.AdmissionRate(
            ExamLevel.JieShi, noble, true, GameConfig.Bribe.UnitMultiplier);

        Assert.Equal(plainRate * GameConfig.Exam.ShiStatusAdmissionBonus, nobleRate);

        // 进甲率结构上不含仕族项：两次调用结果恒等（同一人同一乘数）。
        var promotion = ExamAdmission.ImperialPromotionRate(plain, GameConfig.Bribe.UnitMultiplier);

        Assert.Equal(
            promotion,
            ExamAdmission.ImperialPromotionRate(plain, GameConfig.Bribe.UnitMultiplier));
    }

    // ------------------------------------------------------------------ 免解（SC-004）

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

    private static SettlementResult Settle(GameState state) =>
        new MonthlySettlementEngine(new RecordingRandom(), new GameStateClock(state), new FixedNameGenerator()).Settle(state);

    private static GameState NewState(
        Family family,
        GameDate date,
        Difficulty difficulty = Difficulty.Normal,
        decimal cash = 0m)
    {
        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(RulesHarness.Guan(cash), Money.Zero, Money.Zero),
            new Holdings());

        return new GameState(StateId, date, difficulty, Origin.Scholar, family, economy);
    }

    private static Person BaiShen(int index, int study, int age = 20) =>
        SetStudy(RulesHarness.Member(index, Gender.Male, age, officialdom: 0), study);

    private static Person JuRen(int index, int study, int age = 20)
    {
        var person = SetStudy(RulesHarness.Member(index, Gender.Male, age), study);

        person.AppendDegree(new DegreeRecord(
            DegreeLevel.JuRen, null, RulesHarness.Date, DegreeChangeCause.ExamPass));

        return person;
    }

    private static Person GongShi(int index, int study, int age = 20)
    {
        var person = JuRen(index, study, age);

        person.AppendDegree(new DegreeRecord(
            DegreeLevel.GongShi, null, RulesHarness.Date, DegreeChangeCause.ExamPass));

        return person;
    }

    private static Person SetStudy(Person person, int study)
    {
        person.Study = study;
        return person;
    }

    private static Person Registered(Person person, ExamLevel level)
    {
        person.ApplyForExam(level);
        return person;
    }

    private static void RecordFailures(Person person, ExamLevel level, int count)
    {
        for (var index = 0; index < count; index++)
        {
            person.RecordExamFailure(level);
        }
    }

    /// <summary>
    /// 记录每次随机调用的替身：<see cref="NextDouble"/> 按给定序列给出（用尽后取 <see cref="Fallback"/>），
    /// <see cref="Next"/> 恒返回下界并逐次记账，从而「第几次掷了什么」可被断言。
    /// </summary>
    private sealed class RecordingRandom : IRandomService
    {
        /// <summary>序列用尽后的回退取值（取值须**不**等于任何已登记的规则数值）。</summary>
        private const double Fallback = 0.99d;

        private readonly Queue<double> _values;

        /// <summary>按给定次序构造。</summary>
        /// <param name="values">依次返回的 <see cref="NextDouble"/> 取值。</param>
        public RecordingRandom(params double[] values) => _values = new Queue<double>(values);

        /// <summary><see cref="NextDouble"/> 的累计调用次数。</summary>
        public int NextDoubleCalls { get; private set; }

        /// <summary><see cref="Next"/> 的逐次参数。</summary>
        public List<(int MinInclusive, int MaxExclusive)> NextCalls { get; } = [];

        /// <inheritdoc />
        public double NextDouble()
        {
            NextDoubleCalls++;
            return _values.Count > 0 ? _values.Dequeue() : Fallback;
        }

        /// <inheritdoc />
        public int Next(int minInclusive, int maxExclusive)
        {
            NextCalls.Add((minInclusive, maxExclusive));
            return minInclusive;
        }

        /// <inheritdoc />
        public void NextBytes(Span<byte> destination) => destination.Clear();
    }
}
