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
