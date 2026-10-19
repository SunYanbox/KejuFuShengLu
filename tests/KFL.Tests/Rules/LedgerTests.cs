using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Services;
using KFL.Rules.Config;
using KFL.Rules.Settlement;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// US5 / quickstart S6、SC-005：流水账的四个聚合查询与「每一文钱都有出处」
/// （FR-021；contracts/ledger.md §4 不变量 1~6；data-model §3.4）。
/// </summary>
/// <remarks>
/// <para>
/// **期望值一律经 <c>KFL.Rules/Config</c> 的成员取得**（SC-008）；夹具取值只作为**用例输入**
/// （契约三清单条款 ⑤），故本文件 MUST NOT 把规则数值抄成第二处出处。
/// </para>
/// <para>
/// **两个维度是同一批条目的两个查询方向**（US5 AS4）：本文件既断言「角色维度之和 + 家族级之和
/// = 家族维度之和」，也断言逐类别、逐月两个方向的合计与它一致——不存在第二份汇总存储。
/// </para>
/// </remarks>
public partial class LedgerTests
{
    private static readonly Guid StateId = Guid.Parse("d4b7e5f1-3a9c-4c2e-9f8b-1c2d3e4f5a6b");

    /// <summary>固定随机种子：本文件只关心「条目 ↔ 资金池」的等式，不关心取到的具体数值。</summary>
    private const int Seed = 20261006;

    /// <summary>长跑月数：跨两个年度，覆盖 1 月的利率 roll 与 12 月的年度项（储蓄利息、工 bonus）。</summary>
    private const int RunMonths = 24;

    /// <summary>「条目合计 ↔ 资金池变动」等式允许的 ulp 倍数（见 <c>AssertMoneyIdentity</c> 的说明）。</summary>
    private const decimal UlpAllowance = 256m;

    /// <summary>十二月的参照年月（年度项当月）。</summary>
    private static readonly GameDate December = new(80, 12);

    /// <summary>
    /// 金额恒等断言：差额 MUST 为 0（SC-005 与「两个维度同一批条目」共用）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 唯一被容忍的是 <see cref="decimal"/> **尾数饱和**带来的若干 ulp：「资金池 = 期初 + 逐条累加」与
    /// 「条目合计 = 从 0 逐条累加」是两次**数量级不同**的定点累加，尾数为 28~29 位有效数字，
    /// 当累计金额超出尾数上限时两侧会相差若干个 ulp（本夹具 300 个区间里最大差额 ≈ 1.2e-21 文，
    /// 即 10 ulp 量级）。这是 decimal 的表示精度下限，**不是**「只动资金池而不落条目」——
    /// 后者表现为条目量级的差额（≥ 0.001 文），会被下面这条断言直接判红。
    /// </para>
    /// <para>
    /// 允许的上界取 <see cref="UlpAllowance"/> 倍 ulp：两次累加各含数百次加法，每次加法至多引入
    /// 1 ulp 的舍入，故「几十 ulp」是正常量级，而 3000 万倍以内的余量仍远小于最小货币单位 1 文。
    /// </para>
    /// </remarks>
    private static void AssertMoneyIdentity(Money expected, Money actual)
    {
        var difference = expected - actual;

        if (difference.Wen == 0m)
        {
            return;
        }

        var magnitude = Absolute(expected.Wen);
        var actualMagnitude = Absolute(actual.Wen);

        if (actualMagnitude > magnitude)
        {
            magnitude = actualMagnitude;
        }

        Assert.True(
            Absolute(difference.Wen) <= Ulp(magnitude) * UlpAllowance,
            FormattableString.Invariant(
                $"差额 {difference.Wen} 文（期望 {expected.Wen}、实际 {actual.Wen}）超出 decimal 尾数饱和的 {UlpAllowance} ulp。"));
    }

    /// <summary>给定数量级下 <see cref="decimal"/> 的 1 ulp（28 位有效数字）。</summary>
    private static decimal Ulp(decimal magnitude)    {
        var scaled = magnitude;
        var ulp = 1E-28m;

        while (scaled >= 1m)
        {
            scaled /= 10m;
            ulp *= 10m;
        }

        return ulp;
    }

    private static decimal Absolute(decimal value) => value < 0m ? -value : value;

    private static bool IsTreasury(LedgerEntry entry) =>
        LedgerCategoryMetadata.KindOf(entry.Category) == LedgerEntryKind.Treasury;

    private static void AssertSameEntries(List<LedgerEntry> expected, IReadOnlyList<LedgerEntry> actual)
    {
        Assert.Equal(expected.Count, actual.Count);

        for (var index = 0; index < expected.Count; index++)
        {
            Assert.Equal(expected[index], actual[index]);
        }
    }

    private static int IndexOf(Ledger ledger, List<LedgerEntry> slice)
    {
        if (slice.Count == 0)
        {
            return ledger.Entries.Count;
        }

        for (var index = 0; index + slice.Count <= ledger.Entries.Count; index++)
        {
            var same = true;

            for (var offset = 0; offset < slice.Count; offset++)
            {
                if (ledger.Entries[index + offset] != slice[offset])
                {
                    same = false;
                    break;
                }
            }

            if (same)
            {
                return index;
            }
        }

        return -1;
    }

    private static Person MemberOf(Family family, PersonId id) =>
        family.TryGet(id) ?? throw new InvalidOperationException($"夹具成员 {id} MUST 存在于家族中。");

    /// <summary>按夹具与固定种子跑满 <paramref name="months"/> 个结算月，并逐月留痕。</summary>
    private static FixtureRun RunFixture(
        int months, Origin origin = Origin.Scholar, EconomyFixtureSettings? settings = null)
    {
        var fixture = EconomyFixtures.Create(settings ?? new EconomyFixtureSettings());
        var state = new GameState(
            StateId, fixture.Date, Difficulty.Normal, origin, fixture.Family, fixture.Economy);
        var engine = new MonthlySettlementEngine(new SeededRandomService(Seed), new GameStateClock(state), new FixedNameGenerator());
        var steps = new List<MonthStep>();

        for (var index = 0; index < months; index++)
        {
            steps.Add(Settle(engine, state));
        }

        return new FixtureRun(state, steps, fixture.ServingOfficialId, fixture.YouthId);
    }

    /// <summary>大额本金 + 计息计时恰好差 1 个月 → 首个结算月必触发一次计息（阈值取配置成员）。</summary>
    private static FixtureRun RunLoanInterestMonth() => RunFixture(
        1,
        settings: new EconomyFixtureSettings
        {
            LoanPrincipal = Money.FromGuan(2000m),
            LoanMonthsSinceInterest = InterestPolicy.InterestPeriodMonths - 1,
        });

    /// <summary>只含一名儿童的家族：无任何收入来源、无资产、零资金 → 必进入饥馑且不落生活费条目。</summary>
    /// <remarks>
    /// 成员按 <see cref="AttributePolicy.HealthMean"/> 补上正常体质：<see cref="RulesHarness.Member"/> 默认不设
    /// <see cref="Person.Health"/>（为 0），而 007 起 ⑧-e 会在饥馑严重期每人每月扣 10 点体质、归零即登记死亡，
    /// 于是「0 体质成员 + 严重期」当月即死 ⇒ 家族无计口成员 ⇒ 饥馑状态机次月直接解除，
    /// 本用例关注的是阶段迁移与事件类条目，故夹具 MUST 让成员活着走完时间线。
    /// </remarks>
    private static GameState NewFamineState()
    {
        var child = RulesHarness.Member(91, Gender.Male, 8);
        child.Health = (int)AttributePolicy.HealthMean;
        var family = RulesHarness.FamilyOf(child);
        var economy = new FamilyEconomy(
            LivingCostTable.InitialStandard,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury());

        return new GameState(StateId, RulesHarness.Date, Difficulty.Normal, Origin.Scholar, family, economy);
    }

    private static MonthStep Settle(MonthlySettlementEngine engine, GameState state)
    {
        var first = state.Economy.Ledger.Entries.Count;
        var result = engine.Settle(state);
        var entries = state.Economy.Ledger.Entries;
        var appended = new List<LedgerEntry>(entries.Count - first);

        for (var index = first; index < entries.Count; index++)
        {
            appended.Add(entries[index]);
        }

        return new MonthStep(
            result.Month, result.TreasuryPoolBefore, result.TreasuryPoolAfter, result, appended);
    }

    private static Money Guan(decimal value) => Money.FromGuan(value);

    /// <summary>一次结算的留痕：年月、资金池前后、快照，以及本次追加的那一段条目。</summary>
    private sealed record MonthStep(
        GameDate Month,
        Money PoolBefore,
        Money PoolAfter,
        SettlementResult Result,
        List<LedgerEntry> Appended);

    /// <summary>一个夹具长跑：存档（就地推进后）、逐月留痕与两名归档用例成员的标识。</summary>
    private sealed record FixtureRun(
        GameState State,
        List<MonthStep> Steps,
        PersonId OfficialId,
        PersonId YouthId);
}
