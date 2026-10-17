using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Services;
using KFL.Rules.Config;
using KFL.Rules.Economy;
using KFL.Rules.FamilyLifecycle;
using KFL.Rules.Settlement;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// US2 生育的验收测试（契约十九 §7 / 契约二十 §7 / FR-012、FR-013 / T043）：
/// ① 申请资格、频率与费用（拒绝时 0 随机、0 条目、不转贷款）；
/// ② 申请当月不分娩、次月 ⑧-b 消费队列；
/// ③ 分娩内固定次序（死亡骰 → 性别 → 遗传 → 寿数 → 姓名 → 入族 → 事件条目 → 解耦婚姻）；
/// ④ 分娩死亡只登记、由 ⑧-g 统一置 <c>Deceased</c> 并归档。
/// </summary>
/// <remarks>
/// 期望金额一律经 <see cref="ChildbirthPolicy"/> 与 <see cref="DifficultyRates"/> 的成员算出（SC-008）；
/// 随机源用允许脚本化的替身，便于逐槽断言消费次序与次数。
/// </remarks>
public class ChildbirthTests
{
    private static readonly Guid StateId = Guid.Parse("5e2b7c41-3a86-4d95-b0f2-6c8d1e4a7b39");

    /// <summary>分娩内随机槽位总数：死亡 1 + 天赋 8 + 学业 2 + 体质 2 + 寿数 2。</summary>
    private const int BirthDrawCount = 15;

    /// <summary>第一次 <c>NextDouble</c> 的夹具取值（Box–Muller 的 u1，0.25 ⇒ <c>cos(2π·0.25) = 0</c>）。</summary>
    private const double FirstRoll = 0.25d;  // arch-guard:allow 夹具随机取值（非规则数值副本）

    /// <summary>中值夹具取值（0.5 ⇒ 不命中任何低于半数的概率骰）。</summary>
    private const double MidRoll = 0.5d;  // arch-guard:allow 夹具随机取值（非规则数值副本）

    // ------------------------------------------------------------------ ① 申请与费用
    /// <summary>申请成功：恰扣一次分娩费用、恰 1 条资金类条目、排入次月队列且当年标记为已申请。</summary>
    [Fact]
    public void 申请成功时扣费一次并排入次月队列()
    {
        var (state, husband, wife) = Couple();
        var couple = new CoupleKey(husband.Id, wife.Id);
        var cashBefore = state.Economy.Treasury.Cash;

        var outcome = LifecycleEntry.ApplyChildbirth(state, couple);

        var cost = LifecycleQueries.ChildbirthCost(state);
        Assert.True(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.None, outcome.RejectReason);
        Assert.Equal(cost, outcome.Amount);
        Assert.NotNull(outcome.Entry);
        Assert.Single(state.Economy.Ledger.Entries);
        Assert.Equal(LedgerCategory.ChildbirthExpense, state.Economy.Ledger.Entries[0].Category);
        Assert.Equal(cost, cashBefore - state.Economy.Treasury.Cash);

        Assert.Single(state.Family.Lifecycle.PendingBirths);
        Assert.True(state.Family.Lifecycle.HasAppliedForBirth(couple, state.CurrentDate.Year));
        Assert.Equal(RulesHarness.Date, state.Family.Lifecycle.PendingBirths[0].AppliedAt);
    }

    /// <summary>本年已申请：拒绝、0 次随机、现金与账本不变。</summary>
    [Fact]
    public void 本年已申请时拒绝且不消费随机()
    {
        var (state, husband, wife) = Couple();
        var couple = new CoupleKey(husband.Id, wife.Id);
        LifecycleEntry.ApplyChildbirth(state, couple);

        var random = new ScriptedRandom();
        var cashBefore = state.Economy.Treasury.Cash;
        var entriesBefore = state.Economy.Ledger.Entries.Count;

        var outcome = LifecycleEntry.ApplyChildbirth(state, couple);

        Assert.False(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.AlreadyAppliedThisYear, outcome.RejectReason);
        Assert.Equal(0, random.DoublesRequested);
        Assert.Equal(cashBefore, state.Economy.Treasury.Cash);
        Assert.Equal(entriesBefore, state.Economy.Ledger.Entries.Count);
    }

    /// <summary>次年起可再次申请（年内一次、跨年重置）。</summary>
    [Fact]
    public void 跨年后可再次申请()
    {
        var (state, husband, wife) = Couple();
        var couple = new CoupleKey(husband.Id, wife.Id);
        LifecycleEntry.ApplyChildbirth(state, couple);

        for (var month = 0; month < 12; month++)
        {
            state.AdvanceMonth();
        }

        var outcome = LifecycleEntry.ApplyChildbirth(state, couple);

        Assert.True(outcome.Accepted);
        Assert.Equal(RulesHarness.Date.Year + 1, state.CurrentDate.Year);
        Assert.True(state.Family.Lifecycle.HasAppliedForBirth(couple, state.CurrentDate.Year));
    }

    /// <summary>女方年龄在 16~45 之外：拒绝、0 次随机、无条目。</summary>
    /// <param name="wifeAge">女方年龄。</param>
    [Theory]
    [InlineData(ChildbirthPolicy.FertilityAgeMin - 1)]
    [InlineData(ChildbirthPolicy.FertilityAgeMax + 1)]
    public void 女方超出生育窗口时拒绝且不消费随机(int wifeAge)
    {
        var (state, husband, wife) = Couple(wifeAge: wifeAge);
        var random = new ScriptedRandom();

        var outcome = LifecycleEntry.ApplyChildbirth(state, new CoupleKey(husband.Id, wife.Id));

        Assert.False(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.FertilityWindowClosed, outcome.RejectReason);
        Assert.Equal(0, random.DoublesRequested);
        Assert.Equal(Money.Zero, outcome.Amount);
        Assert.Null(outcome.Entry);
        Assert.Empty(state.Economy.Ledger.Entries);
    }

    /// <summary>资金不足：拒绝、0 次随机、0 条 0 金额条目、MUST NOT 转贷款。</summary>
    [Fact]
    public void 资金不足时拒绝且不转贷款()
    {
        var (state, husband, wife) = Couple(cash: Money.Zero);
        var random = new ScriptedRandom();

        var outcome = LifecycleEntry.ApplyChildbirth(state, new CoupleKey(husband.Id, wife.Id));

        Assert.False(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.InsufficientFunds, outcome.RejectReason);
        Assert.Equal(0, random.DoublesRequested);
        Assert.Null(outcome.Entry);
        Assert.Empty(state.Economy.Ledger.Entries);
        Assert.Equal(Money.Zero, state.Economy.Treasury.Loan.Principal);
    }

    /// <summary>丧偶（<c>SpouseId</c> 已置空）后不可申请：拒绝且 0 次随机。</summary>
    [Fact]
    public void 丧偶后不可申请生育()
    {
        var (state, husband, wife) = Couple();
        state.Family.EndMarriage(wife.Id);
        var random = new ScriptedRandom();

        var outcome = LifecycleEntry.ApplyChildbirth(state, new CoupleKey(husband.Id, wife.Id));

        Assert.False(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.NotMarriageable, outcome.RejectReason);
        Assert.Equal(0, random.DoublesRequested);
        Assert.Null(outcome.Entry);
        Assert.Empty(state.Economy.Ledger.Entries);
    }

    // ------------------------------------------------------------------ ② 申请当月与次月
    /// <summary>申请发生在当月结算之后：当月不再分娩，次月 ⑧-b 才产出婴儿。</summary>
    [Fact]
    public void 申请当月不产子而次月分娩()
    {
        var (state, husband, wife) = Couple();
        var couple = new CoupleKey(husband.Id, wife.Id);
        var random = new FixedRandomService();

        var current = Settle(state, random);
        Assert.Empty(current.Lifecycle.Births);
        Assert.Equal(new GameDate(RulesHarness.Date.Year, RulesHarness.Date.Month + 1), state.CurrentDate);

        Assert.True(LifecycleEntry.ApplyChildbirth(state, couple).Accepted);
        Assert.Single(state.Family.Lifecycle.PendingBirths);

        // 申请当月的那次结算：申请未到期，不产子、申请仍在队列。
        var applicationMonth = Settle(state, random);
        Assert.Empty(applicationMonth.Lifecycle.Births);
        Assert.Single(state.Family.Lifecycle.PendingBirths);

        // 次月的结算：⑧-b 消费待产记录。
        var next = Settle(state, random);

        var birth = Assert.Single(next.Lifecycle.Births);
        Assert.Equal(husband.Id, birth.FatherId);
        Assert.Equal(wife.Id, birth.MotherId);
        Assert.Equal(Gender.Male, birth.Gender);
        Assert.Empty(state.Family.Lifecycle.PendingBirths);

        var newborn = state.Family.TryGet(birth.PersonId);
        Assert.NotNull(newborn);
        Assert.Equal(wife.Id, newborn.MotherId);
        Assert.Equal(husband.Generation + 1, newborn.Generation);
        Assert.Equal(new GameDate(RulesHarness.Date.Year, RulesHarness.Date.Month + 2), newborn.BirthDate);
        Assert.Contains(newborn, state.Family.RegisteredMembers);
    }

    // ------------------------------------------------------------------ ③ 分娩内固定次序
    /// <summary>分娩的随机消费次序与次数固定，新生儿冠家族姓并入族。</summary>
    [Fact]
    public void 分娩按固定次序消费随机并产出新生儿()
    {
        var (state, husband, wife) = Couple();
        var couple = new CoupleKey(husband.Id, wife.Id);
        state.Family.Lifecycle.ScheduleBirth(new PendingBirth(couple, PreviousMonth(state.CurrentDate)));

        // ① 死亡骰（0.5 未命中）→ ② 性别（Next 取下界 = 男）→ ③ 遗传 12 次 → ④ 寿数 2 次。
        var random = new ScriptedRandom();
        var deaths = new DeathRegister();

        var births = Childbirth.Run(state, random, new FixedNameGenerator(), deaths);

        var birth = Assert.Single(births);
        Assert.Equal(BirthDrawCount, random.DoublesRequested);
        Assert.Equal(1, random.IntsRequested);
        Assert.Empty(deaths.Ordered);
        Assert.Empty(state.Family.Lifecycle.PendingBirths);

        var newborn = state.Family.TryGet(birth.PersonId);
        Assert.NotNull(newborn);
        Assert.Equal("测文", newborn.Name);
        Assert.Equal(husband.Id, newborn.FatherId);
        Assert.Equal(wife.Id, newborn.MotherId);
        Assert.True(newborn.Health >= ChildbirthPolicy.InheritedHealthFloor);
        Assert.Single(state.Economy.Ledger.Entries);
        Assert.Equal(LedgerCategory.ChildBorn, state.Economy.Ledger.Entries[0].Category);
        Assert.Equal(Money.Zero, state.Economy.Ledger.Entries[0].Amount);
    }

    /// <summary>四项天赋按「农、商、仕、工」次序各自独立取样。</summary>
    [Fact]
    public void 四项天赋按农商仕工次序独立取样()
    {
        var first = Birth(agriculture: 0, commerce: 100, officialdom: 0, craft: 100);
        var second = Birth(agriculture: 100, commerce: 0, officialdom: 100, craft: 0);

        Assert.Equal(first.Talents.Agriculture, second.Talents.Commerce);
        Assert.Equal(first.Talents.Commerce, second.Talents.Agriculture);
        Assert.Equal(first.Talents.Officialdom, second.Talents.Craft);
        Assert.Equal(first.Talents.Craft, second.Talents.Officialdom);
        Assert.NotEqual(first.Talents.Agriculture, first.Talents.Commerce);
    }

    /// <summary>
    /// 分娩死亡命中：母亲被登记 <see cref="DeathCause.Childbirth"/>、新生儿照常出生、
    /// 其配偶 <c>SpouseId</c> 置空；<b>但此刻仍未置 <c>Deceased</c></b>（只有 ⑧-g 才置）。
    /// </summary>
    [Fact]
    public void 分娩死亡命中时母亲登记死因且新生儿照常出生()
    {
        var (state, husband, wife) = Couple();
        var couple = new CoupleKey(husband.Id, wife.Id);
        state.Family.Lifecycle.ScheduleBirth(new PendingBirth(couple, PreviousMonth(state.CurrentDate)));

        // ① 死亡骰命中（0.0 < 5% × 系数）→ ② 性别男 → ③~④ 属性骰。
        var random = new ScriptedRandom(0.0d);
        var deaths = new DeathRegister();

        var births = Childbirth.Run(state, random, new FixedNameGenerator(), deaths);

        Assert.Single(births);
        Assert.True(deaths.Contains(wife));
        Assert.Equal(DeathCause.Childbirth, deaths.Ordered[0].Cause);
        Assert.Null(husband.SpouseId);
        Assert.Null(wife.SpouseId);
        Assert.Equal(StatusFlag.None, wife.Status & StatusFlag.Deceased);
        Assert.Contains(wife, state.Family.RegisteredMembers);
    }

    /// <summary>⑧-g 才把登记表落到状态与账本：置 <c>Deceased</c>、进归档、落 1 条事件条目。</summary>
    [Fact]
    public void 死亡结算把登记表落到状态与账本()
    {
        var (state, husband, wife) = Couple();
        var couple = new CoupleKey(husband.Id, wife.Id);
        state.Family.Lifecycle.ScheduleBirth(new PendingBirth(couple, PreviousMonth(state.CurrentDate)));
        var deaths = new DeathRegister();
        Childbirth.Run(state, new ScriptedRandom(0.0d), new FixedNameGenerator(), deaths);
        var entriesBefore = state.Economy.Ledger.Entries.Count;

        var facts = DeathSettlement.Run(state, deaths, state.CurrentDate);

        var fact = Assert.Single(facts);
        Assert.Equal(wife.Id, fact.PersonId);
        Assert.Equal(DeathCause.Childbirth, fact.Cause);
        Assert.Equal(StatusFlag.Deceased, wife.Status & StatusFlag.Deceased);
        Assert.DoesNotContain(wife, state.Family.RegisteredMembers);
        Assert.Contains(wife, state.Family.ArchivedMembers);
        Assert.Equal(entriesBefore + 1, state.Economy.Ledger.Entries.Count);
        Assert.Equal(LedgerCategory.MemberDeceased, state.Economy.Ledger.Entries[^1].Category);
        Assert.Equal(Money.Zero, state.Economy.Ledger.Entries[^1].Amount);
    }

    /// <summary>⑧-g 幂等：已亡者不再重复落条目。</summary>
    [Fact]
    public void 死亡结算对已亡成员幂等()
    {
        var (state, _, wife) = Couple();
        wife.Status |= StatusFlag.Deceased;

        var deaths = new DeathRegister();
        deaths.Add(wife, DeathCause.OldAge);
        var entriesBefore = state.Economy.Ledger.Entries.Count;

        var facts = DeathSettlement.Run(state, deaths, state.CurrentDate);

        Assert.Empty(facts);
        Assert.Equal(entriesBefore, state.Economy.Ledger.Entries.Count);
    }

    // ------------------------------------------------------------------ 基座
    /// <summary>建一对已婚夫妇（男方年龄大于女方，女方 16~45）。</summary>
    /// <param name="wifeAge">女方年龄。</param>
    /// <param name="husbandAge">男方年龄。</param>
    /// <param name="difficulty">难度（决定支出系数与负面事件系数）。</param>
    /// <param name="cash">现银。</param>
    /// <returns>游戏状态与夫妇。</returns>
    private static (GameState State, Person Husband, Person Wife) Couple(
        int wifeAge = 30,
        int husbandAge = 40,
        Difficulty difficulty = Difficulty.Normal,
        Money? cash = null)
    {
        var husband = RulesHarness.Member(1, Gender.Male, husbandAge);
        var wife = RulesHarness.Member(2, Gender.Female, wifeAge);
        var family = RulesHarness.FamilyOf(husband, wife);
        family.Marry(husband.Id, wife.Id);

        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(cash ?? Money.FromGuan(1000)),
            new Holdings());
        var state = new GameState(StateId, RulesHarness.Date, difficulty, Origin.Farmer, family, economy);

        return (state, husband, wife);
    }

    /// <summary>直接跑一次 ⑧-b（不经月度结算），返回新生儿。</summary>
    /// <param name="agriculture">父方农技。</param>
    /// <param name="commerce">父方商才。</param>
    /// <param name="officialdom">父方仕途。</param>
    /// <param name="craft">父方工巧。</param>
    /// <returns>新生儿。</returns>
    private static Person Birth(int agriculture, int commerce, int officialdom, int craft)
    {
        var father = RulesHarness.Member(1, Gender.Male, 40, agriculture, commerce, officialdom, craft);
        var wife = RulesHarness.Member(2, Gender.Female, 30);
        var family = RulesHarness.FamilyOf(father, wife);
        family.Marry(father.Id, wife.Id);

        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(Money.FromGuan(1000)),
            new Holdings());
        var state = new GameState(StateId, RulesHarness.Date, Difficulty.Normal, Origin.Farmer, family, economy);

        family.Lifecycle.ScheduleBirth(new PendingBirth(new CoupleKey(father.Id, wife.Id), PreviousMonth(state.CurrentDate)));
        var births = Childbirth.Run(
            state,
            new ScriptedRandom(MeanScript()),
            new FixedNameGenerator(),
            new DeathRegister());

        return Assert.IsType<Person>(state.Family.TryGet(Assert.Single(births).PersonId));
    }

    /// <summary>
    /// 「均值脚本」：先一个 <see cref="MidRoll"/> 让 ① 分娩死亡骰不命中，
    /// 再给 7 组 <c>(<see cref="MidRoll"/>, <see cref="FirstRoll"/>)</c>——第二值 0.25 使
    /// <c>cos(2π·0.25) = 0</c>，Box–Muller 的 <c>z</c> 恰为 0，每项取值恰为父母均值
    /// （否则 σ 可达 55，负向样本会被 clamp 成 0、抹平次序差异）。
    /// </summary>
    /// <returns>依次返回的取值。</returns>
    private static double[] MeanScript()
    {
        var values = new List<double> { MidRoll };

        for (var i = 0; i < 7; i++)
        {
            values.Add(MidRoll);
            values.Add(FirstRoll);
        }

        return [.. values];
    }

    /// <summary>上一个自然月（跨年回退到 12 月）——待产记录须早于当月才被 ⑧-b 消费。</summary>
    /// <param name="month">当月。</param>
    /// <returns>上一个自然月。</returns>
    private static GameDate PreviousMonth(GameDate month) =>
        month.Month == 1 ? new GameDate(month.Year - 1, 12) : new GameDate(month.Year, month.Month - 1);

    /// <summary>跑一次月度结算。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="random">随机源。</param>
    /// <returns>结算结果。</returns>
    private static SettlementResult Settle(GameState state, IRandomService random) =>
        new MonthlySettlementEngine(random, new GameStateClock(state), new FixedNameGenerator()).Settle(state);

    /// <summary>可计数的固定序列随机源（用尽后回退 0.5；<c>Next</c> 取返回下界）。</summary>
    private sealed class ScriptedRandom : IRandomService
    {
        /// <summary>用尽给定序列后返回的默认中值。</summary>
        private const double DefaultFallback = 0.5d;  // arch-guard:allow 随机替身的取值回退（非规则数值副本）

        private readonly Queue<double> _values;

        /// <summary>按给定次序构造。</summary>
        /// <param name="values">依次返回的取值。</param>
        public ScriptedRandom(params double[] values) => _values = new Queue<double>(values);

        /// <summary><see cref="NextDouble"/> 的调用次数。</summary>
        public int DoublesRequested { get; private set; }

        /// <summary><see cref="Next"/> 的调用次数。</summary>
        public int IntsRequested { get; private set; }

        /// <inheritdoc />
        public double NextDouble()
        {
            DoublesRequested++;
            return _values.Count > 0 ? _values.Dequeue() : DefaultFallback;
        }

        /// <inheritdoc />
        public int Next(int minInclusive, int maxExclusive)
        {
            IntsRequested++;
            return minInclusive;
        }

        /// <inheritdoc />
        public void NextBytes(Span<byte> destination) => destination.Clear();
    }
}
