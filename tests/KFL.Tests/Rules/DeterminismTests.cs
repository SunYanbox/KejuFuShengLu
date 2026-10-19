using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Services;
using KFL.Rules.Config;
using KFL.Rules.Settlement;
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
public partial class DeterminismTests
{
    private static readonly Guid StateId = Guid.Parse("2b4d6f80-1c3e-4a5b-8c7d-9e0f1a2b3c4d");

    /// <summary>固定种子：本文件断言的是「同种子 → 同结果」，取值本身不重要。</summary>
    private const int Seed = 20261006;

    /// <summary>复跑月数：跨两个年度，覆盖 1 月利率 roll、12 月年度项与贷款计息节点。</summary>
    private const int RunMonths = 120;

    /// <summary>非 1 月的月份：该月没有储蓄利率 roll，随机槽位最少。</summary>
    private static readonly GameDate December = new(80, 12);

    /// <summary>
    /// 中性随机取值 0.5：同时高于提亲概率上限（`ProposalBaseChance × NegativeEventFactor ≤ 0.07`）
    /// 与感染率上限（体质 1 时 `0.40 × m_p ≤ 0.44`）⇒ ⑧-a 与 ⑧-c 都**不命中**、不额外消费随机数。
    /// </summary>
    private const double NeutralRoll = 0.5d;  // arch-guard:allow 夹具随机取值（非规则数值副本）

    [Fact]
    public void 同种子同初始状态同月份序列两次运行逐位相同()
    {
        var first = Run(RunMonths, Origin.Artisan);
        var second = Run(RunMonths, Origin.Artisan);

        AssertSameRun(first, second);
        AssertLifecycleActuallyRan(first);
    }

    /// <summary>
    /// 断言 ⑧ 在 <see cref="RunMonths"/> 个月里**真的执行过**（SC-002）：否则逐位比对可能集体假绿。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 断言的是 ⑧-c（患病）、⑧-f/⑧-g（身故与丧偶终止）与 ⑧-g 的丧偶不变量：身故者 MUST 已无配偶
    /// （<c>SpouseId == null</c>、<c>FormerSpouseIds</c> 非空），见 FR-021 / T094。
    /// </para>
    /// <para>
    /// **不**断言的三种事实及其原因：成婚与分娩恒为 0 是**正确**的——托媒、应答提亲与生育申请都是
    /// 玩家动作，无头运行不会触发（它们由 <c>MarriageTests</c> / <c>FamilyLifecycleTests</c> 覆盖）；
    /// 待应答提议（⑧-a）虽每月掷 1 次，但本夹具家族在第 7 个月即被患病潮灭门（实证：80-07 起在册为 0），
    /// 命中月是否落在存活期完全取决于种子，故此处不作断言。跨年龄档（⑧-d）同理可以合法为 0。
    /// </para>
    /// </remarks>
    /// <param name="run">一次运行。</param>
    private static void AssertLifecycleActuallyRan(RunRecord run)
    {
        var illnesses = run.Results.Sum(result => result.Lifecycle.Illnesses.Count);
        var deaths = run.Results.Sum(result => result.Lifecycle.Deaths.Count);
        var crossings = run.Results.Sum(result => result.Lifecycle.Crossings.Count);
        var proposals = run.Results.Count(result => result.Lifecycle.PendingProposal is not null);

        Assert.True(
            illnesses >= 1,
            $"⑧-c 未真正执行：{RunMonths} 个月里 0 次患病（身故 {deaths}、跨档 {crossings}、提议 {proposals}）。");
        Assert.True(
            deaths >= 1,
            $"⑧-f/⑧-g 未真正执行：{RunMonths} 个月里 0 次身故（患病 {illnesses}、跨档 {crossings}、提议 {proposals}）。");

        // FR-021：丧偶 MUST 经 Family.EndMarriage——身故者一律已无配偶，且幸存配偶留下前配偶记录。
        var members = run.State.Family.Members.ToList();
        Assert.False(
            members.Any(member => member.Status.HasFlag(StatusFlag.Deceased) && member.SpouseId is not null),
            "⑧-g 未终止身故者的婚姻：存在既已身故又仍有配偶的成员（FR-021）。");
        Assert.True(
            members.Any(member => member.FormerSpouseIds.Count > 0),
            $"⑧-g 未真正执行丧偶终止：{RunMonths} 个月里 0 名成员留下前配偶记录（身故 {deaths}）。");
    }

    [Fact]
    public void 未命中的计息节点不消耗随机数()
    {
        // 取值一律走中性常量（固定替身用尽后恒返 NeutralRoll）：⑧-a 提亲骰与 ⑧-c 感染骰都不命中、
        // 不额外消费，于是两次同构运行的逐月消费次数只差那一个「计息 roll」。
        var due = RunMonthsFrom(December, 2, InterestPolicy.InterestPeriodMonths - 1);
        var missed = RunMonthsFrom(December, 2, 0);

        // 首月：两侧的米价都取同一个中性取值；只有命中侧计息（本金 × 区间上限）。
        Assert.Equal(GrainPricePolicy.Walk(GrainPricePolicy.Initial, NeutralRoll), due.Results[0].GrainPriceIndexAfter);
        Assert.Equal(GrainPricePolicy.Walk(GrainPricePolicy.Initial, NeutralRoll), missed.Results[0].GrainPriceIndexAfter);
        Assert.True(due.Results[0].LoanInterestAccrued.IsPositive);
        Assert.Equal(Money.Zero, missed.Results[0].LoanInterestAccrued);

        // 未命中的节点 MUST 消费 0 次：命中侧首月恰多 1 次，次月两侧回到相同槽位。
        Assert.Equal(missed.DoublesPerMonth[0] + 1, due.DoublesPerMonth[0]);
        Assert.Equal(missed.DoublesPerMonth[1], due.DoublesPerMonth[1]);

        // ⚠️ 旧口径「同月序 + 同计时 ⇒ 不同种子消耗的随机数总个数相同」在 007 起**作废**：
        // ⑧-c 的感染命中会额外消费一次「程度骰」，不同种子会走进不同的命中路径。
        // 「计息与否只由 Loan.MonthsSinceInterest 决定」已由上面的「命中侧恰多 1 次」直接证明
        // （常量取值下命中侧与未命中侧的其余槽位逐月相同）。
    }

    [Fact]
    public void 随机消费次序固定为米价储蓄利率再计息()
    {
        // 007 起 ⑧（人口与健康推进）会按在册成员数量消费随机数，故本用例一律用**常量随机源**
        // （FixedRandomService 用尽后恒返回 0.5）消去取值相关性：0.5 高于感染率上限、高于提亲概率，
        // 于是 ⑧ 的消费次数只由「在册且 AgeAt>=3 的人数」决定，逐月可比。
        // 家族刻意取「单成员 + 寿数远超窗口 + 正常体质」：⑧-f 的寿终与 ⑧-c 的因病归零都不会在窗口内
        // 减员——否则成员数变化会盖住本用例要测的零随机子步（年龄档跨越每年都在发生）。
        var settled = SingleMemberCounts(January, 12, due: false);

        for (var index = 1; index < settled.Count; index++)
        {
            // 相对口径：MUST NOT 写死绝对次数（007 起 ⑧ 的槽位随成员数变化）；
            // 只断言「1 月恰比其余月份多一次储蓄利率 roll」。
            // 这也一并覆盖 ⑧-d（成长）/ ⑧-g（死亡落账）/ ⑧-h（家主继任）三类**零随机**子步：
            // 它们若消费了随机数，12 个月里逐月的消费次数就不再相等。
            Assert.Equal(settled[0] - 1, settled[index]);
        }

        // 首次结算月即命中计息节点：该月比同月未命中者恰多消费一次
        // （次序为米价 → 储蓄利率 → 计息利率，故计息利率排在 ⑧ 之前）。
        var due = SingleMemberCounts(January, 2, due: true);

        Assert.Equal(settled[0] + 1, due[0]);

        // 次月未命中计息节点，两侧回到相同槽位。
        Assert.Equal(settled[1], due[1]);
    }

    /// <summary>
    /// 单成员家族（40 岁、寿数 70、体质 = <see cref="AttributePolicy.HealthMean"/>）在常量随机源下的逐月
    /// <c>NextDouble()</c> 次数；<paramref name="due"/> 决定首月是否恰逢贷款计息节点。
    /// </summary>
    private static List<int> SingleMemberCounts(GameDate from, int months, bool due)
    {
        var member = RulesHarness.Member(1, Gender.Male, 40);
        member.Health = (int)AttributePolicy.HealthMean;
        var family = RulesHarness.FamilyOf(member);
        var treasury = new Treasury(Money.FromGuan(2_000m));

        if (due)
        {
            treasury.Loan.Principal = Money.FromGuan(1_000m);
            treasury.Loan.MonthsSinceInterest = InterestPolicy.InterestPeriodMonths - 1;
        }

        var economy = new FamilyEconomy(
            LivingCostTable.InitialStandard,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            treasury,
            new Holdings());
        var state = new GameState(StateId, from, Difficulty.Normal, Origin.Scholar, family, economy);
        var random = new CountingRandomService(new FixedRandomService());
        var engine = new MonthlySettlementEngine(random, new GameStateClock(state), new FixedNameGenerator());
        var counts = new List<int>();

        for (var index = 0; index < months; index++)
        {
            var before = random.DoublesRequested;
            engine.Settle(state);
            counts.Add(random.DoublesRequested - before);
        }

        return counts;
    }

    /// <summary>常量随机源下的逐月 <c>NextDouble()</c> 次数（次序断言的直接观测口，取值无关）。</summary>
    private static List<int> FixedCounts(GameDate from, int months, EconomyFixtureSettings settings) =>
        RunFrom(from, months, settings, new CountingRandomService(new FixedRandomService())).DoublesPerMonth;

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
        var engine = new MonthlySettlementEngine(random, new GameStateClock(state), new FixedNameGenerator());
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

        AssertSameFamily(first.State.Family, second.State.Family);

        var firstEntries = first.State.Economy.Ledger.Entries;
        var secondEntries = second.State.Economy.Ledger.Entries;

        Assert.Equal(firstEntries.Count, secondEntries.Count);

        for (var index = 0; index < firstEntries.Count; index++)
        {
            Assert.Equal(firstEntries[index], secondEntries[index]);
        }
    }

    /// <summary>
    /// 成员级逐字段比对（SC-002）：只比「资金池 + 账本」会漏掉 ⑧ 对 <see cref="Person"/> 与
    /// <c>Family.Lifecycle</c> 的写入——例如患病程度、师承标记、待应答提议与购人计数。
    /// </summary>
    /// <param name="first">第一遍运行的家族。</param>
    /// <param name="second">第二遍运行的家族。</param>
    private static void AssertSameFamily(Family first, Family second)
    {
        Assert.Equal(first.Name, second.Name);
        Assert.Equal(first.HasShiStatus, second.HasShiStatus);
        Assert.Equal(first.AutoExamApplication, second.AutoExamApplication);
        Assert.Equal(first.HeadId, second.HeadId);

        var firstMembers = first.Members.ToList();
        var secondMembers = second.Members.ToList();
        Assert.Equal(firstMembers.Select(member => member.Id), secondMembers.Select(member => member.Id));

        for (var index = 0; index < firstMembers.Count; index++)
        {
            var expected = firstMembers[index];
            var actual = secondMembers[index];

            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Gender, actual.Gender);
            Assert.Equal(expected.BirthDate, actual.BirthDate);
            Assert.Equal(expected.Generation, actual.Generation);
            Assert.Equal(expected.Talents, actual.Talents);
            Assert.Equal(expected.Study, actual.Study);
            Assert.Equal(expected.Health, actual.Health);
            Assert.Equal(expected.Lifespan, actual.Lifespan);
            Assert.Equal(expected.Rank, actual.Rank);
            Assert.Equal(expected.Merit, actual.Merit);
            Assert.Equal(expected.Status, actual.Status);
            Assert.Equal(expected.Timers, actual.Timers);
            Assert.Equal(expected.Timers.IllnessSeverityBasisPoints, actual.Timers.IllnessSeverityBasisPoints);
            Assert.Equal(expected.Timers.TreatmentRemainingMonths, actual.Timers.TreatmentRemainingMonths);
            Assert.Equal(expected.Occupation, actual.Occupation);
            Assert.Equal(expected.MonthsInOffice, actual.MonthsInOffice);
            Assert.Equal(expected.EntryTrack, actual.EntryTrack);
            Assert.Equal(expected.FatherId, actual.FatherId);
            Assert.Equal(expected.MotherId, actual.MotherId);
            Assert.Equal(expected.SpouseId, actual.SpouseId);
            Assert.Equal(expected.FormerSpouseIds, actual.FormerSpouseIds);
            Assert.Equal(expected.DegreeHistory, actual.DegreeHistory);
            Assert.Equal(expected.JieShiFailureCount, actual.JieShiFailureCount);
            Assert.Equal(expected.ShengShiFailureCount, actual.ShengShiFailureCount);
            Assert.Equal(expected.AppliedExam, actual.AppliedExam);
            Assert.Equal(expected.JieShiBribeCount, actual.JieShiBribeCount);
            Assert.Equal(expected.ShengShiBribeCount, actual.ShengShiBribeCount);
            Assert.Equal(expected.DianShiBribeCount, actual.DianShiBribeCount);
            Assert.Equal(expected.IsTutoredByMaster, actual.IsTutoredByMaster);
        }

        // ⑧ 的五组生命周期簿记：只比「成员集合」看不出待应答提议、待产记录与购人名次。
        var firstLifecycle = first.Lifecycle;
        var secondLifecycle = second.Lifecycle;

        Assert.Equal(firstLifecycle.PurchaseCount, secondLifecycle.PurchaseCount);
        Assert.Equal(firstLifecycle.PurchaseYear, secondLifecycle.PurchaseYear);
        Assert.Equal(firstLifecycle.BirthApplicationYear, secondLifecycle.BirthApplicationYear);
        Assert.Equal(firstLifecycle.PendingBirths, secondLifecycle.PendingBirths);
        Assert.Equal(firstLifecycle.AppliedBirthCouples, secondLifecycle.AppliedBirthCouples);
        Assert.Equal(firstLifecycle.MatchmakeApplications, secondLifecycle.MatchmakeApplications);
        Assert.Equal(firstLifecycle.Proposal, secondLifecycle.Proposal);
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
