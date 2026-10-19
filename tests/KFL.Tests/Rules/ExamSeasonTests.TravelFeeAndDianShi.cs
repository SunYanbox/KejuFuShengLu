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
}
