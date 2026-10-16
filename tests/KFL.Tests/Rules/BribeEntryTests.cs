using System.Reflection;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Bribery;
using KFL.Rules.Config;
using KFL.Rules.Exam;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// US1 / SC-002、quickstart 场景 1、2：行贿固定次序、逐步累进、窗口边界、被抓不清零、触顶与重置。
/// </summary>
/// <remarks>
/// 期望值一律经 <c>KFL.Rules/Config</c> 的成员与 <see cref="DifficultyRates"/> 现算，
/// 本文件 MUST NOT 另存规则数值副本（SC-008）。
/// </remarks>
public class BribeEntryTests
{
    private static readonly Guid StateId = Guid.Parse("5b2f41a7-8c3d-4e56-9f70-0a1b2c3d4e5f");

    /// <summary>解试行贿窗口内的月份（79 年为科举年，解试月 = (79, 8)）。</summary>
    private static readonly GameDate JieShiWindowLastMonth = new(79, 8);

    /// <summary>解试行贿窗口的首月（考试月的前一月）。</summary>
    private static readonly GameDate JieShiWindowFirstMonth = new(79, 7);

    /// <summary>解试行贿窗口**之外**的月份（考试月的前两月）。</summary>
    private static readonly GameDate OutsideJieShiWindow = new(79, 6);

    [Fact]
    public void 解试行贿三次时金额与概率按零点二步长累进且每次一条账本()
    {
        var examinee = RulesHarness.Member(11, Gender.Male, 20);
        var state = NewState(RulesHarness.FamilyOf(examinee), JieShiWindowFirstMonth, cash: RulesHarness.Guan(10_000));
        var random = new CountingRandomService(0.999d);

        var expectedMultiplier = BribePolicy.UnitMultiplier;

        for (var nth = 1; nth <= 3; nth++)
        {
            var expectedAmount = Money.FromGuan(
                BribePolicy.BaseAmountGuan(ExamLevel.JieShi)
                * (1m + (BribePolicy.ProgressionStep * (nth - 1)))
                * DifficultyRates.ExpenseFactor(Difficulty.Normal));

            var expectedProbability = Math.Min(
                BribePolicy.BaseCaughtRate(ExamLevel.JieShi)
                * (1m + (BribePolicy.ProgressionStep * (nth - 1)))
                * DifficultyRates.BriberyRiskFactor(Difficulty.Normal),
                BribePolicy.CaughtProbabilityCap);

            var entryCountBefore = state.Economy.Ledger.Entries.Count;

            var outcome = BribeEntry.Bribe(state, examinee, ExamLevel.JieShi, random);

            expectedMultiplier *= BribePolicy.EffectMultiplierK(ExamLevel.JieShi);

            Assert.True(outcome.Accepted);
            Assert.Equal(BribeRejectReason.None, outcome.RejectReason);
            Assert.Equal(nth, outcome.Nth);
            Assert.Equal(expectedAmount, outcome.Amount);
            Assert.False(outcome.Caught);
            Assert.Equal(expectedMultiplier, outcome.MultiplierAfter);
            Assert.Equal(expectedMultiplier, BribeRules.MultiplierOf(ExamLevel.JieShi, examinee));
            Assert.False(outcome.CapReached);

            Assert.Equal(expectedAmount, BribeRules.PayableAmount(ExamLevel.JieShi, nth, Difficulty.Normal));
            Assert.Equal(expectedProbability, BribeRules.CaughtProbability(ExamLevel.JieShi, nth, Difficulty.Normal));

            // ③ 恰 1 条资金类条目，恒为负，归属当事人。
            var entries = state.Economy.Ledger.Entries;
            Assert.Equal(entryCountBefore + 1, entries.Count);

            var added = entries[^1];
            Assert.Equal(LedgerCategory.BribeExpense, added.Category);
            Assert.Equal(examinee.Id, added.PersonId);
            Assert.True(added.Amount.IsNegative);

            // ⑤ 每次行贿都递增计数（无论是否被抓）。
            Assert.Equal(nth, BribeRules.NthOf(examinee, ExamLevel.JieShi) - 1);

            // ⑥ 每次恰 1 行事实。
            Assert.Equal(nth, state.Family.PunishmentJournal.Bribes.Count);
        }

        // ④ 三次行贿恰消费 3 次随机。
        Assert.Equal(3, random.Doubles);

        // ⑤+⑥ 逐字段与投影一致。
        Assert.Equal(3, examinee.JieShiBribeCount);
        Assert.Equal(0, examinee.ShengShiBribeCount);
        Assert.Equal(0, examinee.DianShiBribeCount);

        Assert.All(
            state.Family.PunishmentJournal.Bribes,
            fact => Assert.Equal(ExamLevel.JieShi, fact.Level));

        // 录取率入口收到的乘数等于 kⁿ（本类只转调 005 的公式）。
        Assert.Equal(
            ExamAdmission.AdmissionRate(
                ExamLevel.JieShi, examinee, state.Family.HasShiStatus, expectedMultiplier),
            BribeRules.AdmissionRateWith(state, examinee, ExamLevel.JieShi));
    }

    [Fact]
    public void 窗口外行贿被拒且资金计数乘数账本与随机逐字段不变()
    {
        var examinee = RulesHarness.Member(12, Gender.Male, 20);
        var state = NewState(RulesHarness.FamilyOf(examinee), OutsideJieShiWindow, cash: RulesHarness.Guan(1_000));
        var random = new CountingRandomService(0.0d);

        Assert.False(BribeRules.IsWithinWindow(state.CurrentDate, ExamLevel.JieShi));
        Assert.False(BribeRules.IsWithinWindow(state.CurrentDate, ExamLevel.ShengShi));
        Assert.False(BribeRules.IsWithinWindow(state.CurrentDate, ExamLevel.DianShi));

        var before = Fingerprint(state, examinee);

        var outcome = BribeEntry.Bribe(state, examinee, ExamLevel.JieShi, random);

        Assert.False(outcome.Accepted);
        Assert.Equal(BribeRejectReason.OutsideWindow, outcome.RejectReason);
        Assert.Equal(Money.Zero, outcome.FromCash);
        Assert.Equal(Money.Zero, outcome.FromSavings);
        Assert.Equal(Money.Zero, outcome.ToLoan);
        Assert.Equal(BribePolicy.UnitMultiplier, outcome.MultiplierAfter);

        Assert.Equal(before, Fingerprint(state, examinee));
        Assert.Equal(0, random.Doubles);
        Assert.Empty(state.Family.PunishmentJournal.Bribes);
        Assert.Empty(state.Economy.Ledger.Entries);
    }

    [Fact]
    public void 解试窗口的两个月都受理而前一月不受理()
    {
        var examinee = RulesHarness.Member(13, Gender.Male, 20);
        var state = NewState(RulesHarness.FamilyOf(examinee), JieShiWindowFirstMonth, cash: RulesHarness.Guan(1_000));
        var random = new FixedRandomService(0.999d, 0.999d);

        Assert.True(BribeRules.IsWithinWindow(JieShiWindowFirstMonth, ExamLevel.JieShi));
        Assert.True(BribeRules.IsWithinWindow(JieShiWindowLastMonth, ExamLevel.JieShi));

        Assert.True(BribeEntry.Bribe(state, examinee, ExamLevel.JieShi, random).Accepted);
        Assert.Equal(JieShiWindowFirstMonth, BribeRules.WindowStartOf(JieShiWindowFirstMonth, ExamLevel.JieShi));

        state.AdvanceMonth();
        Assert.Equal(JieShiWindowLastMonth, state.CurrentDate);
        Assert.True(BribeEntry.Bribe(state, examinee, ExamLevel.JieShi, random).Accepted);
        Assert.Equal(JieShiWindowFirstMonth, BribeRules.WindowStartOf(JieShiWindowLastMonth, ExamLevel.JieShi));
    }

    [Fact]
    public void 殿试行贿窗口覆盖省试月本身且紧随省试窗口()
    {
        var shengShiMonth = new GameDate(80, 3);
        var dianShiMonth = new GameDate(80, 4);

        Assert.True(BribeRules.IsWithinWindow(dianShiMonth, ExamLevel.DianShi));
        Assert.True(BribeRules.IsWithinWindow(shengShiMonth, ExamLevel.DianShi));
        Assert.True(BribeRules.IsWithinWindow(shengShiMonth, ExamLevel.ShengShi));
        Assert.False(BribeRules.IsWithinWindow(new GameDate(80, 2), ExamLevel.DianShi));

        Assert.Equal(shengShiMonth, BribeRules.WindowStartOf(shengShiMonth, ExamLevel.DianShi));
        Assert.Equal(shengShiMonth, BribeRules.WindowStartOf(dianShiMonth, ExamLevel.DianShi));

        // 不在窗口内时取其后最近窗口的首月。
        Assert.Equal(new GameDate(80, 2), BribeRules.WindowStartOf(new GameDate(80, 1), ExamLevel.ShengShi));
        Assert.Equal(new GameDate(82, 7), BribeRules.WindowStartOf(new GameDate(80, 1), ExamLevel.JieShi));
    }

    [Fact]
    public void 同月多次行贿各自累进且各消费一次随机()
    {
        var examinee = RulesHarness.Member(14, Gender.Male, 20);
        var state = NewState(RulesHarness.FamilyOf(examinee), JieShiWindowLastMonth, cash: RulesHarness.Guan(10_000));
        var random = new CountingRandomService(0.999d);

        var first = BribeEntry.Bribe(state, examinee, ExamLevel.JieShi, random);
        var second = BribeEntry.Bribe(state, examinee, ExamLevel.JieShi, random);

        Assert.Equal(1, first.Nth);
        Assert.Equal(2, second.Nth);
        Assert.True(second.Amount > first.Amount);
        Assert.Equal(
            Money.FromGuan(
                BribePolicy.BaseAmountGuan(ExamLevel.JieShi)
                * (1m + (BribePolicy.ProgressionStep * 1m))
                * DifficultyRates.ExpenseFactor(Difficulty.Normal)),
            second.Amount);

        Assert.Equal(2, random.Doubles);
        Assert.Equal(2, examinee.JieShiBribeCount);
        Assert.Equal(2, state.Family.PunishmentJournal.Bribes.Count);
        Assert.All(
            state.Economy.Ledger.Entries,
            entry => Assert.Equal(LedgerCategory.BribeExpense, entry.Category));
    }

    [Fact]
    public void 被抓不清零计数与乘数且不退贿款也不落补偿条目()
    {
        var examinee = RulesHarness.Member(15, Gender.Male, 20);
        var state = NewState(RulesHarness.FamilyOf(examinee), JieShiWindowLastMonth, cash: RulesHarness.Guan(10_000));
        var random = new CountingRandomService(0.0d);

        var first = BribeEntry.Bribe(state, examinee, ExamLevel.JieShi, random);
        var second = BribeEntry.Bribe(state, examinee, ExamLevel.JieShi, random);

        Assert.True(first.Caught);
        Assert.True(second.Caught);

        Assert.Equal(2, second.Nth);
        Assert.Equal(2, examinee.JieShiBribeCount);

        var expectedMultiplier = BribePolicy.UnitMultiplier;
        expectedMultiplier *= BribePolicy.EffectMultiplierK(ExamLevel.JieShi);
        expectedMultiplier *= BribePolicy.EffectMultiplierK(ExamLevel.JieShi);
        Assert.Equal(expectedMultiplier, second.MultiplierAfter);

        // 两次被抓：账本恰 2 条行贿支出，没有任何退款 / 补偿类别。
        var bribeEntries = state.Economy.Ledger.Entries
            .Where(entry => entry.Category == LedgerCategory.BribeExpense)
            .ToArray();
        Assert.Equal(2, bribeEntries.Length);
        Assert.True(bribeEntries.All(entry => entry.Amount.IsNegative));

        // 行贿款 MUST NOT 被退回：全部条目都是支出（恒为负），且类别只允许行贿支出与被抓当次命中的罚金。
        Assert.True(state.Economy.Ledger.Entries.All(entry => entry.Amount.IsNegative));
        Assert.True(state.Economy.Ledger.Entries.All(
            entry => entry.Category is LedgerCategory.BribeExpense or LedgerCategory.Fine));

        // 被抓当次同样落 1 行事实，Caught 为真。
        Assert.Equal(2, state.Family.PunishmentJournal.Bribes.Count);
        Assert.All(state.Family.PunishmentJournal.Bribes, fact => Assert.True(fact.Caught));
    }

    [Fact]
    public void 触顶后调用行贿入口被拒且随机消费为零()
    {
        var examinee = RulesHarness.Member(16, Gender.Male, 20, officialdom: 100);
        examinee.Study = 100;

        var state = NewState(RulesHarness.FamilyOf(examinee), JieShiWindowLastMonth, cash: RulesHarness.Guan(1_000_000));

        while (!BribeRules.IsLevelCapReached(state, examinee, ExamLevel.JieShi))
        {
            examinee.RecordBribe(ExamLevel.JieShi);
        }

        var random = new CountingRandomService(0.0d);
        var before = Fingerprint(state, examinee);

        Assert.False(BribeRules.CanBribe(state, examinee, ExamLevel.JieShi, out var feasibility));
        Assert.Equal(BribeRejectReason.CapReached, feasibility.RejectReason);
        Assert.True(feasibility.IsCapReached);

        var outcome = BribeEntry.Bribe(state, examinee, ExamLevel.JieShi, random);

        Assert.False(outcome.Accepted);
        Assert.Equal(BribeRejectReason.CapReached, outcome.RejectReason);
        Assert.True(outcome.CapReached);
        Assert.Equal(0, random.Doubles);
        Assert.Equal(before, Fingerprint(state, examinee));
        Assert.Empty(state.Economy.Ledger.Entries);
    }

    [Fact]
    public void 不在册或级别与功名不匹配者被拒且随机消费为零()
    {
        var examinee = RulesHarness.Member(17, Gender.Male, 20);
        var family = RulesHarness.FamilyOf(examinee);
        var random = new CountingRandomService(0.0d);

        // 级别与功名不匹配：在省试行贿窗口内，白身的合格级别却是解试。
        var shengShiState = NewState(family, new GameDate(80, 3), cash: RulesHarness.Guan(1_000));
        var mismatched = BribeEntry.Bribe(shengShiState, examinee, ExamLevel.ShengShi, random);
        Assert.False(mismatched.Accepted);
        Assert.Equal(BribeRejectReason.LevelMismatch, mismatched.RejectReason);

        // 不在册：已亡当事人。
        examinee.Status |= StatusFlag.Deceased;
        var jieShiState = NewState(family, JieShiWindowLastMonth, cash: RulesHarness.Guan(1_000));
        var deceased = BribeEntry.Bribe(jieShiState, examinee, ExamLevel.JieShi, random);
        Assert.False(deceased.Accepted);
        Assert.Equal(BribeRejectReason.NotRegistered, deceased.RejectReason);

        Assert.Equal(0, random.Doubles);
        Assert.Empty(jieShiState.Economy.Ledger.Entries);
        Assert.Empty(shengShiState.Economy.Ledger.Entries);
        Assert.Empty(family.PunishmentJournal.Bribes);
    }

    [Fact]
    public void 重置行贿计数按级别或全清且重置后可再次从第一次起算()
    {
        var examinee = RulesHarness.Member(18, Gender.Male, 20);
        var state = NewState(RulesHarness.FamilyOf(examinee), JieShiWindowLastMonth, cash: RulesHarness.Guan(10_000));

        examinee.RecordBribe(ExamLevel.JieShi);
        examinee.RecordBribe(ExamLevel.JieShi);
        examinee.RecordBribe(ExamLevel.ShengShi);

        BribeEntry.ResetBribeCounts(examinee, ExamLevel.JieShi);

        Assert.Equal(0, examinee.JieShiBribeCount);
        Assert.Equal(1, examinee.ShengShiBribeCount);
        Assert.Equal(BribePolicy.UnitMultiplier, BribeRules.MultiplierOf(ExamLevel.JieShi, examinee));
        Assert.Equal(1, BribeRules.NthOf(examinee, ExamLevel.JieShi));

        BribeEntry.ResetBribeCounts(examinee, null);

        Assert.Equal(0, examinee.JieShiBribeCount);
        Assert.Equal(0, examinee.ShengShiBribeCount);
        Assert.Equal(0, examinee.DianShiBribeCount);

        // 重置不是越限：金额回到基础值、序号回到 1。
        var outcome = BribeEntry.Bribe(state, examinee, ExamLevel.JieShi, new FixedRandomService(0.999d));
        Assert.True(outcome.Accepted);
        Assert.Equal(1, outcome.Nth);
        Assert.Equal(
            Money.FromGuan(
                BribePolicy.BaseAmountGuan(ExamLevel.JieShi) * DifficultyRates.ExpenseFactor(Difficulty.Normal)),
            outcome.Amount);
    }

    [Fact]
    public void 行贿计数只增不减且三个级别的计数彼此独立()
    {
        var examinee = RulesHarness.Member(19, Gender.Male, 40);
        examinee.AppendDegree(new DegreeRecord(
            DegreeLevel.GongShi, null, RulesHarness.Date, DegreeChangeCause.ExamPass));

        examinee.RecordBribe(ExamLevel.JieShi);
        examinee.RecordBribe(ExamLevel.ShengShi);
        examinee.RecordBribe(ExamLevel.ShengShi);
        examinee.RecordBribe(ExamLevel.DianShi);
        examinee.RecordBribe(ExamLevel.DianShi);
        examinee.RecordBribe(ExamLevel.DianShi);

        Assert.Equal(1, examinee.JieShiBribeCount);
        Assert.Equal(2, examinee.ShengShiBribeCount);
        Assert.Equal(3, examinee.DianShiBribeCount);
        Assert.Equal(4, BribeRules.NthOf(examinee, ExamLevel.DianShi));

        // 连坐降级等降级动作 MUST NOT 清零计数（001 的 remarks 口径）。
        examinee.AppendDegree(new DegreeRecord(
            DegreeLevel.JuRen, null, RulesHarness.Date, DegreeChangeCause.PunishmentDemotion));

        Assert.Equal(1, examinee.JieShiBribeCount);
        Assert.Equal(2, examinee.ShengShiBribeCount);
        Assert.Equal(3, examinee.DianShiBribeCount);
    }

    [Fact]
    public void 零五次占位常量已作废且零次幂只由单位乘数给出()
    {
        const BindingFlags Static = BindingFlags.Public | BindingFlags.Static;

        Assert.Null(typeof(ExamPolicy).GetProperty("NeutralBribeMultiplier", Static));
        Assert.Null(typeof(ExamPolicy).GetField("NeutralBribeMultiplier", Static));
        Assert.Null(typeof(GameConfig.Exam).GetProperty("NeutralBribeMultiplier", Static));
        Assert.Null(typeof(GameConfig.Exam).GetField("NeutralBribeMultiplier", Static));

        var neverBribed = RulesHarness.Member(20, Gender.Male, 20);

        Assert.Equal(BribePolicy.UnitMultiplier, BribeRules.MultiplierOf(ExamLevel.JieShi, neverBribed));
        Assert.Equal(BribePolicy.UnitMultiplier, GameConfig.Bribe.UnitMultiplier);
        Assert.Equal(1m, BribePolicy.UnitMultiplier);
    }

    private static GameState NewState(
        Family family,
        GameDate date,
        Origin origin = Origin.Scholar,
        Difficulty difficulty = Difficulty.Normal,
        Money cash = default)
    {
        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(cash),
            new Holdings());

        return new GameState(StateId, date, difficulty, origin, family, economy);
    }

    /// <summary>把「行贿可能触碰的每一处」压成一个元组，用于「逐字段不变」断言。</summary>
    private static (
        int JieShi,
        int ShengShi,
        int DianShi,
        string Timers,
        StatusFlag Status,
        int LedgerCount,
        decimal Cash,
        decimal Savings,
        decimal LoanPrincipal,
        int JournalBribes,
        int JournalSentences,
        int JournalFines,
        int JournalRelativeBans,
        int JournalCollective,
        int JournalReleases) Fingerprint(GameState state, Person person) =>
        (
            person.JieShiBribeCount,
            person.ShengShiBribeCount,
            person.DianShiBribeCount,
            person.Timers.ToString() ?? string.Empty,
            person.Status,
            state.Economy.Ledger.Entries.Count,
            state.Economy.Treasury.Cash.Guan,
            state.Economy.Treasury.Savings.Guan,
            state.Economy.Treasury.Loan.Principal.Guan,
            state.Family.PunishmentJournal.Bribes.Count,
            state.Family.PunishmentJournal.Sentences.Count,
            state.Family.PunishmentJournal.Fines.Count,
            state.Family.PunishmentJournal.RelativeBans.Count,
            state.Family.PunishmentJournal.CollectivePunishments.Count,
            state.Family.PunishmentJournal.Releases.Count);

    /// <summary>计数随机替身：统计 <see cref="KFL.Infrastructure.Abstractions.IRandomService.NextDouble"/> 的调用次数。</summary>
    private sealed class CountingRandomService : KFL.Infrastructure.Abstractions.IRandomService
    {
        private readonly double _value;
        private readonly FixedRandomService _next = new();

        /// <summary>构造：<c>NextDouble</c> 恒返回给定取值。</summary>
        /// <param name="value">返回值。</param>
        public CountingRandomService(double value) => _value = value;

        /// <summary>至今被请求的 <c>NextDouble</c> 次数。</summary>
        public int Doubles { get; private set; }

        /// <inheritdoc />
        public double NextDouble()
        {
            Doubles++;
            return _value;
        }

        /// <inheritdoc />
        public int Next(int minInclusive, int maxExclusive) => _next.Next(minInclusive, maxExclusive);

        /// <inheritdoc />
        public void NextBytes(Span<byte> destination) => _next.NextBytes(destination);
    }
}
