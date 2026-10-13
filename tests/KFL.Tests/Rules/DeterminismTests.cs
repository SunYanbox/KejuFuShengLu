using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Services;
using KFL.Rules.Config;
using KFL.Rules.Settlement;
using KFL.Rules.Start;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// T060 / quickstart S7、SC-006：同种子 + 同初始状态 + 同月份序列 → 逐位相同的结算序列；
/// 随机消费次序固定为「米价 → 储蓄利率（仅 1 月）→ 贷款计息利率」，且**未命中的计息节点不消耗随机数**
/// （契约「月度结算」§4 条款 2、3；SC-006）。
/// </summary>
/// <remarks>
/// 期望值一律经 <c>KFL.Rules/Config</c> 的成员取得（SC-008）；本文件 MUST NOT 另存规则数值。
/// </remarks>
public class DeterminismTests
{
    private static readonly Guid StateId = Guid.Parse("2b4d6f80-1c3e-4a5b-8c7d-9e0f1a2b3c4d");

    /// <summary>固定种子：本文件断言的是「同种子 → 同结果」，取值本身不重要。</summary>
    private const int Seed = 20261006;

    /// <summary>复跑月数：跨两个年度，覆盖 1 月利率 roll、12 月年度项与贷款计息节点。</summary>
    private const int RunMonths = 24;

    /// <summary>非 1 月的月份：该月没有储蓄利率 roll，随机槽位最少。</summary>
    private static readonly GameDate December = new(80, 12);

    [Fact]
    public void 同种子同初始状态同月份序列两次运行逐位相同()
    {
        var first = Run(RunMonths, Origin.Artisan);
        var second = Run(RunMonths, Origin.Artisan);

        AssertSameRun(first, second);
    }

    [Fact]
    public void 相同出身姓氏难度与种子两次开局逐字段完全相同()
    {
        // SC-008 的开局半边：按契约六 §3 的消费次序（①姓氏 →②家主 →③配偶 →④孩子 i →⑤存档标识），
        // 同入参 + 同种子 MUST 得到逐字段相同的存档。
        foreach (var origin in new[] { Origin.Farmer, Origin.Artisan, Origin.Merchant, Origin.Scholar })
        {
            AssertSameOpening(Opened(origin), Opened(origin));
        }
    }

    [Fact]
    public void 不同种子开局至少一处不同()
    {
        var first = Opened(Origin.Scholar);
        var second = Opened(Origin.Scholar, Seed + 1);

        var firstFacts = Describe(first);
        var secondFacts = Describe(second);

        Assert.NotEqual(firstFacts, secondFacts);
    }

    /// <summary>按契约六 §3 的次序跑一次开局（固定姓名来源，随机只来自固定种子）。</summary>
    private static NewGameSetupResult Opened(Origin origin, int seed = Seed)
    {
        var request = new NewGameRequest(origin, Difficulty.Normal, "测", new GameDate(1, 1));

        return NewGameSetup.Create(request, new SeededRandomService(seed), new FixedNameGenerator("测"));
    }

    /// <summary>把一份开局结果的**全部可断言字段**摊平成一个可比较的字符串。</summary>
    private static string Describe(NewGameSetupResult setup)
    {
        var lines = new List<string>
        {
            setup.State.Id.ToString(),
            setup.State.CurrentDate.ToString(),
            setup.State.Difficulty.ToString(),
            setup.State.Origin.ToString(),
            setup.State.Family.Name,
            setup.State.Family.HasShiStatus.ToString(),
            setup.HeadId?.ToString() ?? "null",
            setup.State.Economy.LivingStandard.ToString(),
            setup.State.Economy.GrainPriceIndex.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            setup.State.Economy.Treasury.Cash.Wen.ToString(System.Globalization.CultureInfo.InvariantCulture),
            setup.State.Economy.Treasury.Savings.Wen.ToString(System.Globalization.CultureInfo.InvariantCulture),
            setup.State.Economy.Treasury.MerchantCapital.Wen.ToString(System.Globalization.CultureInfo.InvariantCulture),
            setup.State.Economy.Holdings.FarmlandMu.ToString(System.Globalization.CultureInfo.InvariantCulture),
            setup.State.Economy.Holdings.RuralHouses.ToString(System.Globalization.CultureInfo.InvariantCulture),
            setup.State.Economy.Holdings.UrbanHouses.ToString(System.Globalization.CultureInfo.InvariantCulture),
            setup.State.Economy.Holdings.Shops.ToString(System.Globalization.CultureInfo.InvariantCulture),
            setup.State.Economy.Ledger.Entries.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        foreach (var person in setup.Members)
        {
            lines.Add(string.Join(
                "|",
                person.Id,
                person.Name,
                person.Gender,
                person.BirthDate,
                person.Generation,
                person.FatherId?.ToString() ?? "null",
                person.MotherId?.ToString() ?? "null",
                person.SpouseId?.ToString() ?? "null",
                person.Talents.Agriculture,
                person.Talents.Commerce,
                person.Talents.Officialdom,
                person.Talents.Craft,
                person.Study,
                person.Health,
                person.Lifespan,
                person.Rank?.Level ?? 0,
                person.Merit,
                person.MonthsInOffice,
                person.Status,
                person.Timers.SentenceRemainingMonths ?? -1,
                person.Timers.ExamBanRemainingMonths ?? -1,
                person.Timers.PromotionBanRemainingMonths ?? -1,
                person.Timers.AwaitingPostRemainingMonths ?? -1,
                person.DegreeHistory.Count,
                person.DegreeHistory.Count == 0 ? "none" : person.DegreeHistory[^1].ToString()));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void AssertSameOpening(NewGameSetupResult first, NewGameSetupResult second)
    {
        Assert.Equal(Describe(first), Describe(second));

        // 除摊平比较外，再逐条断言 SC-008 点名的字段，避免 Describe 漏项时静默假绿。
        Assert.Equal(first.Members.Count, second.Members.Count);

        var firstMembers = first.Members.ToList();
        var secondMembers = second.Members.ToList();

        for (var index = 0; index < firstMembers.Count; index++)
        {
            Assert.Equal(firstMembers[index].Id, secondMembers[index].Id);
            Assert.Equal(firstMembers[index].Name, secondMembers[index].Name);
            Assert.Equal(firstMembers[index].Gender, secondMembers[index].Gender);
            Assert.Equal(firstMembers[index].BirthDate, secondMembers[index].BirthDate);
            Assert.Equal(firstMembers[index].Generation, secondMembers[index].Generation);
            Assert.Equal(firstMembers[index].FatherId, secondMembers[index].FatherId);
            Assert.Equal(firstMembers[index].MotherId, secondMembers[index].MotherId);
            Assert.Equal(firstMembers[index].SpouseId, secondMembers[index].SpouseId);
            Assert.Equal(firstMembers[index].Talents, secondMembers[index].Talents);
            Assert.Equal(firstMembers[index].Study, secondMembers[index].Study);
            Assert.Equal(firstMembers[index].Health, secondMembers[index].Health);
            Assert.Equal(firstMembers[index].Lifespan, secondMembers[index].Lifespan);
            Assert.Equal(firstMembers[index].DegreeHistory, secondMembers[index].DegreeHistory);
        }

        Assert.Equal(first.HeadId, second.HeadId);
        Assert.Equal(first.StartDate, second.StartDate);
        Assert.Equal(first.State.Id, second.State.Id);
    }

    [Fact]
    public void 未命中的计息节点不消耗随机数()
    {
        // 同一取值队列：命中计息的那次会取走第 2 个取值，未命中的那次把它留给了下个月的米价。
        var due = RunMonthsFrom(December, 2, InterestPolicy.InterestPeriodMonths - 1, 0.0d, 1.0d, 0.0d);
        var missed = RunMonthsFrom(December, 2, 0, 0.0d, 1.0d, 0.0d);

        // 首月：两侧的米价都取第 1 个取值；只有命中侧计息（本金 × 区间上限）。
        Assert.Equal(GrainPricePolicy.Walk(GrainPricePolicy.Initial, 0.0d), due.Results[0].GrainPriceIndexAfter);
        Assert.Equal(GrainPricePolicy.Walk(GrainPricePolicy.Initial, 0.0d), missed.Results[0].GrainPriceIndexAfter);

        Assert.True(due.Results[0].LoanInterestAccrued.IsPositive);
        Assert.Equal(Money.Zero, missed.Results[0].LoanInterestAccrued);

        // 次月：命中侧消费了第 3 个取值，未命中侧消费的是被让出来的第 2 个取值——两者必然不同。
        Assert.Equal(
            GrainPricePolicy.Walk(due.Results[0].GrainPriceIndexAfter, 0.0d),
            due.Results[1].GrainPriceIndexAfter);
        Assert.Equal(
            GrainPricePolicy.Walk(missed.Results[0].GrainPriceIndexAfter, 1.0d),
            missed.Results[1].GrainPriceIndexAfter);
        Assert.NotEqual(due.Results[1].GrainPriceIndexAfter, missed.Results[1].GrainPriceIndexAfter);

        // 「是否命中」只由 Loan.MonthsSinceInterest 决定，与随机取值无关：
        // 同一个月序列 + 同一计时 → 两次**不同种子**的运行消耗的随机数个数相同。
        Assert.Equal(
            CountDoubles(December, 6, InterestPolicy.InterestPeriodMonths - 1, Seed),
            CountDoubles(December, 6, InterestPolicy.InterestPeriodMonths - 1, Seed + 1));
    }

    [Fact]
    public void 随机消费次序固定为米价储蓄利率再计息()
    {
        // 已结清的贷款：任何月份都不进入计息节点，故随机槽位只有米价与 1 月的储蓄利率。
        var settled = Counts(January, 12, settings: SettledLoan());
        var settledTotal = 0;

        for (var index = 0; index < settled.Count; index++)
        {
            var expected = index == 0 ? 2 : 1;

            Assert.Equal(expected, settled[index]);
            settledTotal += settled[index];
        }

        // 1 月额外消费一次（储蓄利率 roll），全年共 13 次。
        Assert.Equal(12 + 1, settledTotal);

        // 首次结算月即命中计息节点：该月多消费一次（次序为米价 → 储蓄利率 → 计息利率）。
        var due = Counts(January, 2, settings: DueLoan());

        Assert.Equal(3, due[0]);
        Assert.Equal(1, due[1]);
    }

    private static GameDate January => RulesHarness.Date;

    private static EconomyFixtureSettings SettledLoan() => new()
    {
        LoanPrincipal = Money.Zero,
        LoanAccruedInterest = Money.Zero,
    };

    private static EconomyFixtureSettings DueLoan() => new()
    {
        LoanMonthsSinceInterest = InterestPolicy.InterestPeriodMonths - 1,
    };

    /// <summary>逐月 `NextDouble()` 调用次数（次序断言的直接观测口）。</summary>
    private static List<int> Counts(GameDate from, int months, EconomyFixtureSettings settings) =>
        RunFrom(from, months, settings, new CountingRandomService(new SeededRandomService(Seed))).DoublesPerMonth;

    /// <summary>给定计时与种子时，同一月份序列消耗的随机数总数。</summary>
    private static int CountDoubles(GameDate from, int months, int monthsSinceInterest, int seed)
    {
        var run = RunFrom(
            from,
            months,
            new EconomyFixtureSettings { LoanMonthsSinceInterest = monthsSinceInterest },
            new CountingRandomService(new SeededRandomService(seed)));

        return run.Random.DoublesRequested;
    }

    private static RunRecord RunMonthsFrom(GameDate from, int months, int monthsSinceInterest, params double[] values) =>
        RunFrom(
            from,
            months,
            new EconomyFixtureSettings { LoanMonthsSinceInterest = monthsSinceInterest },
            new CountingRandomService(new FixedRandomService(values)));

    private static RunRecord Run(int months, Origin origin) => RunFrom(
        RulesHarness.Date,
        months,
        new EconomyFixtureSettings(),
        new CountingRandomService(new SeededRandomService(Seed)),
        origin);

    private static RunRecord RunFrom(
        GameDate from,
        int months,
        EconomyFixtureSettings settings,
        CountingRandomService random,
        Origin origin = Origin.Artisan)
    {
        var fixture = EconomyFixtures.Create(settings with { Date = from });
        var state = new GameState(StateId, from, Difficulty.Normal, origin, fixture.Family, fixture.Economy);
        var engine = new MonthlySettlementEngine(random, new GameStateClock(state));
        var results = new List<SettlementResult>();
        var doublesPerMonth = new List<int>();

        for (var index = 0; index < months; index++)
        {
            var requestedBefore = random.DoublesRequested;

            results.Add(engine.Settle(state));
            doublesPerMonth.Add(random.DoublesRequested - requestedBefore);
        }

        return new RunRecord(state, results, random, doublesPerMonth);
    }

    private static void AssertSameRun(RunRecord first, RunRecord second)
    {
        Assert.Equal(first.Results.Count, second.Results.Count);

        for (var index = 0; index < first.Results.Count; index++)
        {
            AssertSameResult(first.Results[index], second.Results[index]);
        }

        // 收尾状态同样逐位相同：资金池、米价系数、当年储蓄利率、饥馑阶段与整册账本。
        Assert.Equal(first.State.Economy.TreasuryPool, second.State.Economy.TreasuryPool);
        Assert.Equal(first.State.Economy.Treasury.Cash, second.State.Economy.Treasury.Cash);
        Assert.Equal(first.State.Economy.Treasury.Savings, second.State.Economy.Treasury.Savings);
        Assert.Equal(first.State.Economy.Treasury.SavingsRate, second.State.Economy.Treasury.SavingsRate);
        Assert.Equal(first.State.Economy.GrainPriceIndex.Value, second.State.Economy.GrainPriceIndex.Value);
        Assert.Equal(first.State.Economy.Famine.Stage, second.State.Economy.Famine.Stage);
        Assert.Equal(first.State.Economy.Famine.ElapsedMonths, second.State.Economy.Famine.ElapsedMonths);
        Assert.Equal(first.State.CurrentDate, second.State.CurrentDate);

        var firstEntries = first.State.Economy.Ledger.Entries;
        var secondEntries = second.State.Economy.Ledger.Entries;

        Assert.Equal(firstEntries.Count, secondEntries.Count);

        for (var index = 0; index < firstEntries.Count; index++)
        {
            Assert.Equal(firstEntries[index], secondEntries[index]);
        }
    }

    private static void AssertSameResult(SettlementResult expected, SettlementResult actual)
    {
        Assert.Equal(expected.Month, actual.Month);
        Assert.Equal(expected.GrainPriceIndexBefore, actual.GrainPriceIndexBefore);
        Assert.Equal(expected.GrainPriceIndexAfter, actual.GrainPriceIndexAfter);
        Assert.Equal(expected.LivingCostPayable, actual.LivingCostPayable);
        Assert.Equal(expected.LivingCostPaid, actual.LivingCostPaid);
        Assert.Equal(expected.NetProfit, actual.NetProfit);
        Assert.Equal(expected.LoanInterestAccrued, actual.LoanInterestAccrued);
        Assert.Equal(expected.LoanRepayment.PrincipalPart, actual.LoanRepayment.PrincipalPart);
        Assert.Equal(expected.LoanRepayment.InterestPart, actual.LoanRepayment.InterestPart);
        Assert.Equal(expected.SavingsInterest, actual.SavingsInterest);
        Assert.Equal(expected.ArtisanBonus, actual.ArtisanBonus);
        Assert.Equal(expected.TreasuryPoolBefore, actual.TreasuryPoolBefore);
        Assert.Equal(expected.TreasuryPoolAfter, actual.TreasuryPoolAfter);
        Assert.Equal(expected.FamineBefore.Stage, actual.FamineBefore.Stage);
        Assert.Equal(expected.FamineBefore.ElapsedMonths, actual.FamineBefore.ElapsedMonths);
        Assert.Equal(expected.FamineAfter.Stage, actual.FamineAfter.Stage);
        Assert.Equal(expected.FamineAfter.ElapsedMonths, actual.FamineAfter.ElapsedMonths);

        Assert.Equal(expected.Entries.Count, actual.Entries.Count);

        for (var index = 0; index < expected.Entries.Count; index++)
        {
            Assert.Equal(expected.Entries[index], actual.Entries[index]);
        }

        Assert.Equal(expected.Incomes, actual.Incomes);
        Assert.Equal(expected.LivingCosts, actual.LivingCosts);
    }

    // ------------------------------------------------------------------ 科举季（005 / T025）

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
        var engine = new MonthlySettlementEngine(random, new GameStateClock(state));
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

    /// <summary>一次运行的产物：收尾存档、逐月快照、随机来源替身与逐月随机消费次数。</summary>
    private sealed record RunRecord(
        GameState State,
        List<SettlementResult> Results,
        CountingRandomService Random,
        List<int> DoublesPerMonth);

    /// <summary>包一层计数：只统计 <see cref="NextDouble"/> 的调用次数，取值仍来自内层替身。</summary>
    private sealed class CountingRandomService : IRandomService
    {
        private readonly IRandomService _inner;

        public CountingRandomService(IRandomService inner) => _inner = inner;

        /// <summary>至今被请求的 <see cref="NextDouble"/> 次数。</summary>
        public int DoublesRequested { get; private set; }

        /// <inheritdoc />
        public double NextDouble()
        {
            DoublesRequested++;
            return _inner.NextDouble();
        }

        /// <inheritdoc />
        public int Next(int minInclusive, int maxExclusive) => _inner.Next(minInclusive, maxExclusive);

        /// <inheritdoc />
        public void NextBytes(Span<byte> destination) => _inner.NextBytes(destination);
    }
}
