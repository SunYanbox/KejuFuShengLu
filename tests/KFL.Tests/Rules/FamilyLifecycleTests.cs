using System.Reflection;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Services;
using KFL.Rules.Config;
using KFL.Rules.Exam;
using KFL.Rules.FamilyLifecycle;
using KFL.Rules.Settlement;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// 007 家族生命周期（月度结算第 ⑧ 步）的测试基座：
/// ① 八子步「进入前 / 离开后」快照断言辅助；
/// ② 空事实段的形状与 <see cref="LifecycleSnapshot.None(GameDate)"/> 的语义；
/// ③ data-model §6 不变量 11~13 的可证面（死亡原因只承载于当月事实段、⑧-g 之后才改状态）。
/// </summary>
/// <remarks>
/// <para>
/// 第 ⑧ 步的执行器（<c>PopulationAdvance</c> 及各子步）是 <c>KFL.Rules</c> 的 <c>internal</c> 类型，
/// 测试经「公开面」观察：一次 <see cref="MonthlySettlementEngine.Settle"/> 后取
/// <see cref="SettlementResult.Lifecycle"/>。故本基座提供 <see cref="SettleMonth"/> 与
/// <see cref="SettleMonths"/> 两个入口，后续相（T041/T047/T050/T059/T063）在其上断言差值。
/// </para>
/// <para>
/// 随机来源一律用常量 0.5（<see cref="FixedRandomService"/> 用尽后的回退值）：它高于感染率上限、
/// 低于提亲概率，使 ⑧ 的消费次数只由「在册且 <c>AgeAt &gt;= 3</c> 的人数」决定，逐月可比。
/// </para>
/// <para>
/// 期望值一律经 <c>KFL.Rules/Config</c> 的成员取得（SC-008）；本文件 MUST NOT 另存规则数值。
/// </para>
/// </remarks>
public class FamilyLifecycleTests
{
    private static readonly Guid StateId = Guid.Parse("6a1f9c24-8d3b-4e57-9a02-1b2c3d4e5f60");

    [Fact]
    public void 空事实段如实为空()
    {
        var snapshot = LifecycleSnapshot.None(RulesHarness.Date);

        Assert.Empty(snapshot.Births);
        Assert.Empty(snapshot.Deaths);
        Assert.Empty(snapshot.Marriages);
        Assert.Empty(snapshot.MarriedOut);
        Assert.Empty(snapshot.Purchases);
        Assert.Empty(snapshot.Illnesses);
        Assert.Empty(snapshot.Crossings);
        Assert.Null(snapshot.PendingProposal);
        Assert.Null(snapshot.NewHeadId);
        Assert.False(snapshot.HeadBecameVacant);
    }

    [Fact]
    public void 死亡原因只承载于当月事实段()
    {
        // data-model §6 不变量 12：DeathCause 只出现在 LifecycleSnapshot.Deaths 的 DeathFact 上；
        // MUST NOT 落 Person 字段、MUST NOT 落账本（006 的处死路径也不补取值）。
        // 以「类型 × 属性」的反射扫描表达：整个公开面上恰有一处承载它。
        var carriers = typeof(Person).Assembly.GetTypes()
            .Concat(typeof(LifecycleSnapshot).Assembly.GetTypes())
            .Where(type => type.IsPublic && !type.IsNested)
            .SelectMany(type => type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.PropertyType == typeof(DeathCause))
                .Select(property => $"{type.Name}.{property.Name}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["DeathFact.Cause"], carriers);
    }

    [Fact]
    public void 死亡事实与归档状态一一对应()
    {
        var run = SettleMonth();

        // data-model §6 不变量 11：死亡登记表在 ⑧-g 之前 MUST NOT 改 Person.Status；
        // 可证面是「凡是出现在当月事实段 Deaths 里的人，结算结束时必已置 Deceased 并归档」——
        // ⑧-g 是唯一的置位通道（上一条反射断言的结论），故不早不晚。
        foreach (var death in run.Lifecycle.Deaths)
        {
            var person = run.State.Family.TryGet(death.PersonId);

            Assert.NotNull(person);
            Assert.True(person.Status.HasFlag(StatusFlag.Deceased));
            Assert.DoesNotContain(person, run.State.Family.RegisteredMembers);
            Assert.Contains(person, run.State.Family.ArchivedMembers);
        }
    }

    [Fact]
    public void 结算一个月必产出一份生命周期事实段()
    {
        var run = SettleMonth();

        // Snapshot 属性恒非 null（即使空段），且当月无婚配 / 提亲时各段为空
        // ——常量随机源下 ⑧-a 提亲骰必不命中。
        Assert.NotNull(run.Result.Lifecycle);
        Assert.Null(run.Result.Lifecycle.PendingProposal);
        Assert.Equal(RulesHarness.Date, run.Result.Month);
    }

    // ------------------------------------------------------------------ T050：八子步的集成面

    /// <summary>
    /// SC-001：⑧ 严格发生在 ⑨ 之前——在 ⑧ 内死亡者 MUST NOT 出现在同月 ⑨ 的考生集合里。
    /// 取 80 年（<c>80 % 3 == 2</c>）的 3 月省试月，令唯一在册考生在该月到龄，则
    /// 「若 ⑨ 先跑，他必被自动报名」与「实际未被报名」构成次序的可证面。
    /// </summary>
    [Fact]
    public void 人口推进严格发生在科举之前()
    {
        var examinee = FamilyFixtures.NewPerson(1, "陈应试", Gender.Male, 50, 3, generation: 0, lifespan: 30);
        examinee.AppendDegree(new DegreeRecord(DegreeLevel.JuRen, null, new GameDate(60, 3), DegreeChangeCause.ExamPass));
        examinee.Occupation = Occupation.Studying;

        var family = RulesHarness.FamilyOf(examinee);
        family.AutoExamApplication = true;
        var state = State(family);

        var result = Settle(state, new FixedRandomService(), months: 3);

        Assert.Equal(new GameDate(RulesHarness.Date.Year, 3), result.Month);
        Assert.Equal(ExamLevel.ShengShi, result.ExamSeason.Level);
        Assert.Contains(result.Lifecycle.Deaths, death => death.PersonId == examinee.Id);
        Assert.DoesNotContain(result.ExamSeason.AutoApplications, line => line.PersonId == examinee.Id);
        Assert.DoesNotContain(result.ExamSeason.Outcomes, line => line.PersonId == examinee.Id);
    }

    /// <summary>
    /// ⑧-d（成长）、⑧-f（寿终判定）、⑧-g（死亡结算）、⑧-h（家主继任）**零随机消费**：
    /// 控制组（家主长生）与处置组（家主到龄、当月完成登记→落状态→传位）的随机次数必须完全相同。
    /// </summary>
    [Fact]
    public void 成长与死亡结算不消费随机()
    {
        var controlFamily = Household(lifespan: 70, out _);
        var deathFamily = Household(lifespan: 30, out var heir);
        var control = new CountingRandomService(new FixedRandomService());
        var deceased = new CountingRandomService(new FixedRandomService());

        var controlResult = Settle(State(controlFamily), control, months: 1);
        var deathResult = Settle(State(deathFamily), deceased, months: 1);

        Assert.Empty(controlResult.Lifecycle.Deaths);
        Assert.Single(deathResult.Lifecycle.Deaths);
        Assert.Equal(control.DoublesRequested, deceased.DoublesRequested);
    }

    /// <summary>
    /// 体质变化的三条通道各扣一次：⑧-d 自然恢复 <c>+1</c>、⑧-c 患病扣 <c>2 + 8s</c>、
    /// ⑧-e 饥馑严重期扣 <c>−10</c>；救济期不减、不掷、不恢复。
    /// </summary>
    [Fact]
    public void 体质按自然恢复患病扣减与饥馑损失()
    {
        var healthy = RulesHarness.Member(1, Gender.Male, 30);
        healthy.Health = 50;
        GrowthAdvance.Run(State(RulesHarness.FamilyOf(healthy)), RulesHarness.Date);
        Assert.Equal(50 + GrowthPolicy.HealthRecoverPerMonth, healthy.Health);

        var ill = RulesHarness.Member(1, Gender.Male, 30);
        ill.Health = 50;
        var illState = State(RulesHarness.FamilyOf(ill));
        ill.Status |= StatusFlag.Ill;
        ill.Timers = new StatusTimers(
            illnessSeverityBasisPoints: IllnessPolicy.SeverityBasisPointsScale,
            treatmentRemainingMonths: null);

        GrowthAdvance.Run(illState, RulesHarness.Date);
        Assert.Equal(50, ill.Health);

        IllnessAdvance.Run(illState, new FixedRandomService(), new DeathRegister());
        Assert.Equal(50 - (IllnessPolicy.SeverityLossBase + IllnessPolicy.SeverityLossSpan), ill.Health);

        var starving = RulesHarness.Member(1, Gender.Male, 30);
        starving.Health = 5;
        var famineState = State(RulesHarness.FamilyOf(starving));
        famineState.Economy.Famine.TransitionTo(FamineStage.Severe);
        var famineDeaths = new DeathRegister();
        FamineHealth.Run(famineState, new FixedRandomService(), famineDeaths);

        Assert.Equal(0, starving.Health);
        Assert.True(famineDeaths.Contains(starving));

        var relieved = RulesHarness.Member(1, Gender.Male, 30);
        relieved.Health = 50;
        var reliefState = State(RulesHarness.FamilyOf(relieved));
        reliefState.Economy.Famine.TransitionTo(FamineStage.Relief);
        var reliefCounting = new CountingRandomService(new FixedRandomService());
        FamineHealth.Run(reliefState, reliefCounting, new DeathRegister());
        GrowthAdvance.Run(reliefState, RulesHarness.Date);

        Assert.Equal(50, relieved.Health);
        Assert.Equal(0, reliefCounting.DoublesRequested);
    }

    /// <summary>⑧-e 的死亡判定恰为 <c>5%</c>，MUST NOT 随难度变化。</summary>
    /// <param name="difficulty">难度。</param>
    [Theory]
    [InlineData(Difficulty.Easy)]
    [InlineData(Difficulty.Normal)]
    [InlineData(Difficulty.Hard)]
    [InlineData(Difficulty.Hell)]
    public void 饥馑严重期死亡判定不随难度变化(Difficulty difficulty)
    {
        Assert.True(Starve(difficulty, (double)GrowthPolicy.FamineDeathChance - 0.001d));
        Assert.False(Starve(difficulty, (double)GrowthPolicy.FamineDeathChance));
    }

    /// <summary>学业成长：读书与名师加成、付不起名师费退回普通速度、16 岁不遗忘、17 岁起遗忘、患病减半。</summary>
    [Fact]
    public void 学业成长按配置生效且名师费不足时按普通速度()
    {
        var student = Student(RulesHarness.Guan(1000));
        GrowthAdvance.Run(student.State, RulesHarness.Date);
        Assert.Equal(30 + Rounded(GrowthPolicy.StudyPerMonth), student.Person.Study);

        var tutored = Tutee(RulesHarness.Guan(1000));
        GrowthAdvance.Run(tutored.State, RulesHarness.Date);
        Assert.Equal(30 + Rounded(GrowthPolicy.StudyPerMonth + GrowthPolicy.TutorStudyBonusPerMonth), tutored.Person.Study);
        Assert.Single(tutored.State.Economy.Ledger.Entries, entry => entry.Category == LedgerCategory.TutorFee);

        var poor = Tutee(Money.Zero);
        GrowthAdvance.Run(poor.State, RulesHarness.Date);
        Assert.Equal(30 + Rounded(GrowthPolicy.StudyPerMonth), poor.Person.Study);
        Assert.True(poor.Person.IsTutoredByMaster);
        Assert.DoesNotContain(poor.State.Economy.Ledger.Entries, entry => entry.Category == LedgerCategory.TutorFee);
        Assert.Equal(0, poor.State.Economy.Treasury.Loan.Principal.Guan);

        var sixteen = RulesHarness.Member(1, Gender.Male, GrowthPolicy.ForgetAgeThreshold);
        sixteen.Study = 30;
        GrowthAdvance.Run(State(RulesHarness.FamilyOf(sixteen)), RulesHarness.Date);
        Assert.Equal(30, sixteen.Study);

        var seventeen = RulesHarness.Member(1, Gender.Male, GrowthPolicy.ForgetAgeThreshold + 1);
        seventeen.Study = 30;
        GrowthAdvance.Run(State(RulesHarness.FamilyOf(seventeen)), RulesHarness.Date);
        Assert.Equal(29, seventeen.Study);

        var illStudent = Student(RulesHarness.Guan(1000));
        illStudent.Person.Status |= StatusFlag.Ill;
        illStudent.Person.Timers = new StatusTimers(
            illnessSeverityBasisPoints: IllnessPolicy.SeverityBasisPointsScale,
            treatmentRemainingMonths: 1);
        GrowthAdvance.Run(illStudent.State, RulesHarness.Date);
        Assert.Equal(30 + Rounded(GrowthPolicy.StudyPerMonth / GrowthPolicy.IllnessStudyPenaltyDivisor), illStudent.Person.Study);
    }

    /// <summary>防守：学业变化按 <see cref="MidpointRounding.AwayFromZero"/> 取整（C# 的 <c>(int)</c> 是截断，不是取整）。</summary>
    /// <param name="delta">十进制增量。</param>
    private static int Rounded(decimal delta) => (int)Math.Round(delta, MidpointRounding.AwayFromZero);

    // ------------------------------------------------------------------ 基座：T050 的装配入口

    /// <summary>家主（至龄则由 ⑧-f 登记）与一名两岁男性子嗣。</summary>
    /// <param name="lifespan">家主寿数。</param>
    /// <param name="heir">返回的子嗣。</param>
    private static Family Household(int lifespan, out Person heir)
    {
        var head = FamilyFixtures.NewPerson(1, "陈家主", Gender.Male, 50, 1, generation: 0, lifespan: lifespan);
        heir = FamilyFixtures.NewPerson(2, "陈幼子", Gender.Male, 78, 11, generation: 1, fatherId: head.Id);
        var family = RulesHarness.FamilyOf(head);
        family.AddChild(heir);
        return family;
    }

    /// <summary>饥馑严重期的一次死亡判定，返回是否登记死亡。</summary>
    /// <param name="difficulty">难度。</param>
    /// <param name="roll">脚本随机值。</param>
    private static bool Starve(Difficulty difficulty, double roll)
    {
        var person = RulesHarness.Member(1, Gender.Male, 30);
        person.Health = 50;
        var state = State(RulesHarness.FamilyOf(person), difficulty);
        state.Economy.Famine.TransitionTo(FamineStage.Severe);
        var deaths = new DeathRegister();

        FamineHealth.Run(state, new FixedRandomService(roll), deaths);

        return deaths.Contains(person);
    }

    /// <summary>一名在读书的成员。</summary>
    /// <param name="cash">初始现金。</param>
    private static (GameState State, Person Person) Student(Money cash)
    {
        var student = RulesHarness.Member(1, Gender.Male, 20);
        student.Study = 30;
        student.Occupation = Occupation.Studying;
        return (State(RulesHarness.FamilyOf(student), cash: cash), student);
    }

    /// <summary>一名在读书且已延名师的成员。</summary>
    /// <param name="cash">初始现金。</param>
    private static (GameState State, Person Person) Tutee(Money cash)
    {
        var (state, person) = Student(cash);
        person.IsTutoredByMaster = true;
        return (state, person);
    }

    /// <summary>装配一个状态（默认资金充裕、普通难度、工出身）。</summary>
    /// <param name="family">家族。</param>
    /// <param name="difficulty">难度。</param>
    /// <param name="origin">出身。</param>
    /// <param name="cash">初始现金。</param>
    private static GameState State(
        Family family,
        Difficulty difficulty = Difficulty.Normal,
        Origin origin = Origin.Artisan,
        Money? cash = null)
    {
        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(cash ?? RulesHarness.Guan(1000)),
            new Holdings());

        return new GameState(StateId, RulesHarness.Date, difficulty, origin, family, economy);
    }

    /// <summary>结算若干个月并返回最后一次快照。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="random">随机源。</param>
    /// <param name="months">月数。</param>
    private static SettlementResult Settle(GameState state, IRandomService random, int months)
    {
        var engine = new MonthlySettlementEngine(random, new GameStateClock(state), new FixedNameGenerator());
        SettlementResult? last = null;

        for (var index = 0; index < months; index++)
        {
            last = engine.Settle(state);
        }

        Assert.NotNull(last);

        return last;
    }

    /// <summary>统计 <see cref="IRandomService.NextDouble"/> 调用次数的替身。</summary>
    /// <param name="inner">被包裹的随机源。</param>
    private sealed class CountingRandomService(IRandomService inner) : IRandomService
    {
        /// <summary>累计的 <see cref="NextDouble"/> 次数。</summary>
        public int DoublesRequested { get; private set; }

        /// <inheritdoc />
        public double NextDouble()
        {
            DoublesRequested++;
            return inner.NextDouble();
        }

        /// <inheritdoc />
        public int Next(int minInclusive, int maxExclusive) => inner.Next(minInclusive, maxExclusive);

        /// <inheritdoc />
        public void NextBytes(Span<byte> buffer) => inner.NextBytes(buffer);
    }

    // ------------------------------------------------------------------ 基座：一次结算

    /// <summary>结算一个月（常量随机源）并取回状态与生命周期事实段。</summary>
    /// <param name="months">结算月数。</param>
    private static LifecycleRun SettleMonths(int months)
    {
        var child = RulesHarness.Member(1, Gender.Male, 5);
        var family = RulesHarness.FamilyOf(child);
        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(),
            new Holdings());
        var state = new GameState(StateId, RulesHarness.Date, Difficulty.Normal, Origin.Artisan, family, economy);
        var random = new FixedRandomService();
        var engine = new MonthlySettlementEngine(random, new GameStateClock(state), new FixedNameGenerator());
        var lifecycle = LifecycleSnapshot.None(state.CurrentDate);
        SettlementResult? last = null;

        for (var index = 0; index < months; index++)
        {
            last = engine.Settle(state);
            lifecycle = last.Lifecycle;
        }

        Assert.NotNull(last);

        return new LifecycleRun(state, lifecycle, last);
    }

    /// <summary>结算一个月。</summary>
    private static LifecycleRun SettleMonth() => SettleMonths(1);

    /// <summary>一次结算的观测量：结算后状态、本月生命周期事实段、本月完整快照。</summary>
    private sealed record LifecycleRun(GameState State, LifecycleSnapshot Lifecycle, SettlementResult Result);
}
