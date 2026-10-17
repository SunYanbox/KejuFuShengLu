using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;
using KFL.Rules.FamilyLifecycle;
using KFL.Rules.Settlement;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// 寿终与夭折（SC-004、SC-015；契约二十 §7）——⑧-f 的判定口径与存活阈值金色表。
/// </summary>
/// <remarks>
/// 金色表由阈值方程 <c>p(c) = q(c) / L</c> 派生：健康月每月的 <c>+1</c> 体质增益
/// 与患病期 <c>−L</c> 的期望损耗相抵，其中 <c>L = SeverityLossBase + SeverityLossSpan × 二分之一</c>
/// （程度 <c>s</c> 在 <c>[0,1]</c> 上均匀）、<c>q</c> 为清除概率且**掷骰前钳到 1**；
/// 宽限月（确诊当月）既无增益也无损耗，故不进入方程。
/// </remarks>
public sealed class LifespanTests
{
    /// <summary>固定的游戏状态标识。</summary>
    private static readonly Guid StateId = Guid.Parse("8f2c6d15-4b93-4a07-8e61-2d5f7a9c0b34");

    /// <summary>中值掷骰（不命中）。</summary>
    private const double MidRoll = 0.5d;  // arch-guard:allow 夹具随机取值（非规则数值副本）

    /// <summary>判定分界点两侧的极小偏移。</summary>
    private const double Epsilon = 1e-9d;

    /// <summary>金色表容差（SC-015：±0.5）。</summary>
    private const double ThresholdTolerance = 0.5d;  // arch-guard:allow 夹具随机取值（非规则数值副本）

    /// <summary>无天赋乘数（金色表的「无天赋」行：感染与清除乘数均为 1）。</summary>
    private const decimal NeutralMultiplier = 1m;

    /// <summary>青年段无天赋且不治疗的存活阈值（SC-015 金色表第 2 行；恰与女性寿命均值同值）。</summary>
    private const double GoldenYouthNoTalentNoTreatment = 62.3d;  // arch-guard:allow 金色表期望值（SC-015，非规则数值副本）

    // ------------------------------------------------------------------ 寿终
    /// <summary>到龄当月即死：服刑、患病、外嫁三态均不豁免，且不消费随机。</summary>
    /// <param name="status">死亡前的状态位。</param>
    [Theory]
    [InlineData(StatusFlag.None)]
    [InlineData(StatusFlag.ServingSentence)]
    [InlineData(StatusFlag.Ill)]
    [InlineData(StatusFlag.MarriedOut)]
    public void 到龄当月死亡且不豁免任何状态(StatusFlag status)
    {
        var person = Oldest();
        var (state, _) = StateOf(person);
        Apply(person, status);

        var deaths = new DeathRegister();
        var random = new ScriptedRandom();

        LifespanAdvance.Run(state, RulesHarness.Date, random, deaths);

        Assert.True(deaths.Contains(person));
        Assert.Equal(DeathCause.OldAge, Assert.Single(deaths.Ordered).Cause);
        Assert.Equal(0, random.DoublesRequested);
    }

    /// <summary>寿数不重掷、不设上限：⑧-f 之后 <c>Lifespan</c> 原样保留。</summary>
    [Fact]
    public void 寿数不重掷()
    {
        var person = Oldest();
        var lifespan = person.Lifespan;
        var (state, _) = StateOf(person);

        LifespanAdvance.Run(state, RulesHarness.Date, new ScriptedRandom(), new DeathRegister());

        Assert.Equal(lifespan, person.Lifespan);
    }

    /// <summary>外嫁成员承载寿终判定，但死亡后 MUST NOT 回到在册与计口。</summary>
    [Fact]
    public void 外嫁成员死亡后不回到在册与计口()
    {
        var person = Oldest();
        var (state, family) = StateOf(person);
        person.Status |= StatusFlag.MarriedOut;

        var deaths = new DeathRegister();
        LifespanAdvance.Run(state, RulesHarness.Date, new ScriptedRandom(), deaths);
        var facts = DeathSettlement.Run(state, deaths, RulesHarness.Date);

        Assert.Single(facts);
        Assert.True(person.Status.HasFlag(StatusFlag.Deceased));
        Assert.DoesNotContain(person, family.RegisteredMembers);
        Assert.DoesNotContain(person, CountedMembers.Counted(family));
        Assert.Contains(person, family.ArchivedMembers);
    }

    // ------------------------------------------------------------------ 夭折
    /// <summary>夭折只在本月生日的未满三岁成员上判定；窗口外的成员零随机消费。</summary>
    /// <param name="age">年龄。</param>
    /// <param name="monthOffset">出生月份相对当月的偏移。</param>
    /// <param name="expectedDraws">期望的随机消费次数。</param>
    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(2, 0, 1)]
    [InlineData(3, 0, 0)]
    [InlineData(2, -1, 0)]
    [InlineData(1, -1, 0)]
    public void 夭折仅在本月生日的未满三岁成员上判定(int age, int monthOffset, int expectedDraws)
    {
        var month = RulesHarness.Date.Month + monthOffset;
        var person = Child(age, month <= 0 ? month + 12 : month);
        var (state, _) = StateOf(person);
        var random = new ScriptedRandom(0.0d);

        LifespanAdvance.Run(state, RulesHarness.Date, random, new DeathRegister());

        Assert.Equal(expectedDraws, random.DoublesRequested);
    }

    /// <summary>夭折概率按难度系数与基础值取值（严格小于）。</summary>
    /// <param name="difficulty">难度。</param>
    [Theory]
    [InlineData(Difficulty.Easy)]
    [InlineData(Difficulty.Normal)]
    [InlineData(Difficulty.Hard)]
    [InlineData(Difficulty.Hell)]
    public void 夭折概率按难度系数取值(Difficulty difficulty)
    {
        var chance = (double)ChildbirthPolicy.InfantDeathChance * (double)ChildbirthPolicy.InfantDeathFactor(difficulty);

        Assert.True(InfantDies(difficulty, chance - Epsilon));
        Assert.False(InfantDies(difficulty, chance + Epsilon));
    }

    /// <summary>夭折概率 MUST NOT 引用难度负面事件系数：地狱档为规格书明示的 12%。</summary>
    [Fact]
    public void 夭折概率不引用负面事件系数()
    {
        var hell = (double)ChildbirthPolicy.InfantDeathChance
            * (double)ChildbirthPolicy.InfantDeathFactor(Difficulty.Hell);
        var negative = (double)ChildbirthPolicy.InfantDeathChance
            * (double)DifficultyRates.NegativeEventFactor(Difficulty.Hell);

        Assert.True(hell < negative);
        Assert.False(InfantDies(Difficulty.Hell, negative - Epsilon));
        Assert.True(InfantDies(Difficulty.Hell, hell - Epsilon));
    }

    /// <summary>夭折命中登记 Infancy，且死亡结算把它落到状态与归档。</summary>
    [Fact]
    public void 夭折命中登记夭折死因()
    {
        var person = Child(0, RulesHarness.Date.Month);
        var (state, family) = StateOf(person);
        var deaths = new DeathRegister();

        LifespanAdvance.Run(state, RulesHarness.Date, new ScriptedRandom(0.0d), deaths);
        var facts = DeathSettlement.Run(state, deaths, RulesHarness.Date);

        Assert.Equal(DeathCause.Infancy, Assert.Single(facts).Cause);
        Assert.True(person.Status.HasFlag(StatusFlag.Deceased));
        Assert.Contains(person, family.ArchivedMembers);
        Assert.DoesNotContain(person, family.RegisteredMembers);
    }

    // ------------------------------------------------------------------ SC-015 存活阈值金色表
    /// <summary>成人段（24 岁起，无年龄增益）的存活阈值金色表。</summary>
    [Fact]
    public void 存活阈值金色表成人段()
    {
        AssertThreshold(NeutralMultiplier, NeutralMultiplier, 0d, treated: false, 71.1);
        AssertThreshold(NeutralMultiplier, NeutralMultiplier, 0d, treated: true, 63.4);
        AssertThreshold(IllnessPolicy.TalentMultiplierFarmer, IllnessPolicy.FarmerRecoveryMultiplier, 0d, false, 62.0);
        AssertThreshold(IllnessPolicy.TalentMultiplierFarmer, IllnessPolicy.FarmerRecoveryMultiplier, 0d, true, 52.3);
        AssertThreshold(IllnessPolicy.TalentMultiplierScholar, IllnessPolicy.ScholarRecoveryMultiplier, 0d, false, 68.0);
        AssertThreshold(IllnessPolicy.TalentMultiplierScholar, IllnessPolicy.ScholarRecoveryMultiplier, 0d, true, 59.5);
        AssertThreshold(IllnessPolicy.TalentMultiplierArtisan, IllnessPolicy.ArtisanRecoveryMultiplier, 0d, false, 71.1);
        AssertThreshold(IllnessPolicy.TalentMultiplierArtisan, IllnessPolicy.ArtisanRecoveryMultiplier, 0d, true, 63.4);
        AssertThreshold(IllnessPolicy.TalentMultiplierMerchant, IllnessPolicy.MerchantRecoveryMultiplier, 0d, false, 73.2);
        AssertThreshold(IllnessPolicy.TalentMultiplierMerchant, IllnessPolicy.MerchantRecoveryMultiplier, 0d, true, 65.9);
    }

    /// <summary>青年段（12~23 岁，增益 +0.30）与儿童段（3~11 岁，增益 +0.60）的存活阈值金色表。</summary>
    [Fact]
    public void 存活阈值金色表青年段与儿童段()
    {
        var youth = (double)IllnessPolicy.ClearBonusYouth;
        var child = (double)IllnessPolicy.ClearBonusChild;

        // 62.3 恰与 AttributePolicy.LifespanMeanFemale 同值（金色表取自 SC-015，不是规则数值副本）。
        AssertThreshold(NeutralMultiplier, NeutralMultiplier, youth, false, GoldenYouthNoTalentNoTreatment);
        AssertThreshold(NeutralMultiplier, NeutralMultiplier, youth, true, 59.1);
        AssertThreshold(IllnessPolicy.TalentMultiplierFarmer, IllnessPolicy.FarmerRecoveryMultiplier, youth, false, 52.4);
        AssertThreshold(IllnessPolicy.TalentMultiplierFarmer, IllnessPolicy.FarmerRecoveryMultiplier, youth, true, 48.7);
        AssertThreshold(IllnessPolicy.TalentMultiplierScholar, IllnessPolicy.ScholarRecoveryMultiplier, youth, false, 59.1);
        AssertThreshold(IllnessPolicy.TalentMultiplierScholar, IllnessPolicy.ScholarRecoveryMultiplier, youth, true, 56.9);
        AssertThreshold(IllnessPolicy.TalentMultiplierArtisan, IllnessPolicy.ArtisanRecoveryMultiplier, youth, false, 63.1);
        AssertThreshold(IllnessPolicy.TalentMultiplierArtisan, IllnessPolicy.ArtisanRecoveryMultiplier, youth, true, 62.9);
        AssertThreshold(IllnessPolicy.TalentMultiplierMerchant, IllnessPolicy.MerchantRecoveryMultiplier, youth, false, 64.5);
        AssertThreshold(IllnessPolicy.TalentMultiplierMerchant, IllnessPolicy.MerchantRecoveryMultiplier, youth, true, 61.1);

        AssertThreshold(NeutralMultiplier, NeutralMultiplier, child, false, 59.1);
        AssertThreshold(NeutralMultiplier, NeutralMultiplier, child, true, 59.1);
        AssertThreshold(IllnessPolicy.TalentMultiplierFarmer, IllnessPolicy.FarmerRecoveryMultiplier, child, false, 48.7);
        AssertThreshold(IllnessPolicy.TalentMultiplierScholar, IllnessPolicy.ScholarRecoveryMultiplier, child, false, 56.9);
        AssertThreshold(IllnessPolicy.TalentMultiplierArtisan, IllnessPolicy.ArtisanRecoveryMultiplier, child, false, 62.9);
        AssertThreshold(IllnessPolicy.TalentMultiplierMerchant, IllnessPolicy.MerchantRecoveryMultiplier, child, false, 61.1);
    }

    /// <summary>阈值以上月净变化恒正、阈值以下恒负；年龄增益把儿童段清除概率钳到 1。</summary>
    [Fact]
    public void 存活阈值的净变化方向与钳位()
    {
        var threshold = Threshold(NeutralMultiplier, NeutralMultiplier, 0d, treated: false);

        Assert.True(Drift(threshold + 1d, NeutralMultiplier, NeutralMultiplier, 0d, false) > 0d);
        Assert.True(Drift(threshold - 1d, NeutralMultiplier, NeutralMultiplier, 0d, false) < 0d);

        var child = (double)IllnessPolicy.ClearBonusChild;

        Assert.Equal(1d, ClearanceChance(100d, NeutralMultiplier, child, treated: false));
        Assert.Equal(1d, ClearanceChance(40d, NeutralMultiplier, child, treated: false));
        Assert.Equal(1d, ClearanceChance(34d, IllnessPolicy.FarmerRecoveryMultiplier, child, treated: false));
    }

    /// <summary>存活阈值处的清除概率与 ⑧-c 实现一致（分界点两侧判定翻转）。</summary>
    [Fact]
    public void 存活阈值处的清除概率与实现一致()
    {
        var threshold = Threshold(IllnessPolicy.TalentMultiplierFarmer, IllnessPolicy.FarmerRecoveryMultiplier, 0d, treated: false);

        // 患病成员先扣 <c>SeverityLossBase</c> 体质、再掷清除 ⇒ 抬高质量让扣减后恰好落在阈值上。
        var health = (int)Math.Round(threshold, MidpointRounding.AwayFromZero) + IllnessPolicy.SeverityLossBase;
        var chance = ClearanceChance(health - IllnessPolicy.SeverityLossBase, IllnessPolicy.FarmerRecoveryMultiplier, 0d, treated: false);

        Assert.True(RecoversAt(health, chance - Epsilon));
        Assert.False(RecoversAt(health, chance + Epsilon));
    }

    /// <summary>落险比例：新成员体质 <c>N(85,10)</c> 低于阈值的概率。</summary>
    [Fact]
    public void 落险比例与新成员体质分布一致()
    {
        Assert.Equal(0.011d, AtRisk(IllnessPolicy.TalentMultiplierFarmer, IllnessPolicy.FarmerRecoveryMultiplier), 3);
        Assert.Equal(0.044d, AtRisk(IllnessPolicy.TalentMultiplierScholar, IllnessPolicy.ScholarRecoveryMultiplier), 3);
        Assert.Equal(0.082d, AtRisk(IllnessPolicy.TalentMultiplierArtisan, IllnessPolicy.ArtisanRecoveryMultiplier), 3);
        Assert.Equal(0.118d, AtRisk(IllnessPolicy.TalentMultiplierMerchant, IllnessPolicy.MerchantRecoveryMultiplier), 3);
    }

    // ------------------------------------------------------------------ 基座
    /// <summary>断言阈值与金色表中的一项一致（容差 ±0.5）。</summary>
    /// <param name="multiplierP">主导天赋的感染乘数。</param>
    /// <param name="multiplierQ">主导天赋的清除乘数。</param>
    /// <param name="bonus">年龄增益。</param>
    /// <param name="treated">是否在疗程中。</param>
    /// <param name="golden">金色表数值。</param>
    private static void AssertThreshold(decimal multiplierP, decimal multiplierQ, double bonus, bool treated, double golden) =>
        Assert.Equal(golden, Threshold(multiplierP, multiplierQ, bonus, treated), ThresholdTolerance);

    /// <summary>月患病概率。</summary>
    /// <param name="health">体质。</param>
    /// <param name="multiplier">感染乘数。</param>
    /// <returns>月患病概率。</returns>
    private static double InfectionChance(double health, decimal multiplier)
    {
        var clamped = Math.Clamp(health, IllnessPolicy.HealthFloor, IllnessPolicy.HealthCeiling);
        var baseChance = (double)IllnessPolicy.InfectionChanceAtFullHealth
            + ((double)IllnessPolicy.InfectionChanceAtFloorHealth - (double)IllnessPolicy.InfectionChanceAtFullHealth)
            * (IllnessPolicy.HealthCeiling - clamped) / IllnessPolicy.InterpolationSpan;

        return baseChance * (double)multiplier;
    }

    /// <summary>月清除概率（掷骰前钳到 1）。</summary>
    /// <param name="health">体质。</param>
    /// <param name="multiplier">清除乘数。</param>
    /// <param name="bonus">年龄增益。</param>
    /// <param name="treated">是否在疗程中。</param>
    /// <returns>月清除概率。</returns>
    private static double ClearanceChance(double health, decimal multiplier, double bonus, bool treated)
    {
        var raw = treated
            ? ((health * (double)IllnessPolicy.TreatedRecoveryCoefficient / 100d)
                + (double)IllnessPolicy.TreatedRecoveryOffset) * (double)multiplier
            : health * (double)IllnessPolicy.BaseRecoveryCoefficient / 100d * (double)multiplier;

        return Math.Min(1d, raw + bonus);
    }

    /// <summary>患病期的月期望体质损耗（程度 <c>s</c> 在 <c>[0,1]</c> 上均匀）。</summary>
    private static double ExpectedLoss =>
        IllnessPolicy.SeverityLossBase + (IllnessPolicy.SeverityLossSpan * 0.5d);  // arch-guard:allow 夹具随机取值（非规则数值副本）

    /// <summary>解阈值方程 <c>p(c) = q(c) / L</c>（二分）。</summary>
    /// <param name="multiplierP">感染乘数。</param>
    /// <param name="multiplierQ">清除乘数。</param>
    /// <param name="bonus">年龄增益。</param>
    /// <param name="treated">是否在疗程中。</param>
    /// <returns>体质阈值 <c>c*</c>。</returns>
    private static double Threshold(decimal multiplierP, decimal multiplierQ, double bonus, bool treated)
    {
        var low = (double)IllnessPolicy.HealthFloor;
        var high = (double)IllnessPolicy.HealthCeiling;

        for (var step = 0; step < 200; step++)
        {
            var middle = (low + high) / 2d;

            if (Drift(middle, multiplierP, multiplierQ, bonus, treated) > 0d)
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }

        return (low + high) / 2d;
    }

    /// <summary>阈值处的月期望净体质变化（正 = 向满值收敛）。</summary>
    /// <param name="health">体质。</param>
    /// <param name="multiplierP">感染乘数。</param>
    /// <param name="multiplierQ">清除乘数。</param>
    /// <param name="bonus">年龄增益。</param>
    /// <param name="treated">是否在疗程中。</param>
    /// <returns>月期望净变化。</returns>
    private static double Drift(double health, decimal multiplierP, decimal multiplierQ, double bonus, bool treated)
    {
        var infection = InfectionChance(health, multiplierP);
        var clearance = ClearanceChance(health, multiplierQ, bonus, treated);

        if (infection <= 0d || clearance <= 0d)
        {
            return 1d;
        }

        return (1d / infection) - (ExpectedLoss / clearance);
    }

    /// <summary>新成员体质 <c>N(85,10)</c> 低于阈值的概率。</summary>
    /// <param name="multiplierP">感染乘数。</param>
    /// <param name="multiplierQ">清除乘数。</param>
    /// <returns>落险比例。</returns>
    private static double AtRisk(decimal multiplierP, decimal multiplierQ)
    {
        var threshold = Threshold(multiplierP, multiplierQ, 0d, treated: false);
        var mean = (double)AttributePolicy.HealthMean;
        var sigma = (double)AttributePolicy.HealthSigma;

        return NormalCdf((threshold - mean) / sigma);
    }

    /// <summary>标准正态分布的累积分布函数（Abramowitz–Stegun 7.1.26，误差 <c>&lt; 7.5e-8</c>）。</summary>
    /// <param name="z">标准化取值。</param>
    /// <returns>累积概率。</returns>
    private static double NormalCdf(double z)
    {
        const double A = 0.2316419d;
        const double B1 = 0.319381530d;
        const double B2 = -0.356563782d;
        const double B3 = 1.781477937d;
        const double B4 = -1.821255978d;
        const double B5 = 1.330274429d;

        var t = 1d / (1d + (A * Math.Abs(z)));
        var poly = t * (B1 + (t * (B2 + (t * (B3 + (t * (B4 + (t * B5))))))));
        var density = Math.Exp(-z * z / 2d) / Math.Sqrt(2d * Math.PI);
        var cdf = 1d - (density * poly);

        return z >= 0d ? cdf : 1d - cdf;
    }

    /// <summary>跑一次 ⑧-c，返回该患病成员的清除判定是否命中。</summary>
    /// <param name="health">体质。</param>
    /// <param name="roll">清除掷骰。</param>
    /// <returns>是否痊愈。</returns>
    private static bool RecoversAt(int health, double roll)
    {
        var person = RulesHarness.Member(1, Gender.Male, 30, agriculture: 50);
        person.Health = health;
        person.Status |= StatusFlag.Ill;
        person.Timers = new StatusTimers(illnessSeverityBasisPoints: 0);
        var (state, _) = StateOf(person);

        IllnessAdvance.Run(state, new ScriptedRandom(roll), new DeathRegister());

        return (person.Status & StatusFlag.Ill) == StatusFlag.None;
    }

    /// <summary>按给定概率跑一次夭折判定，返回该幼儿是否夭折。</summary>
    /// <param name="difficulty">难度。</param>
    /// <param name="roll">夭折掷骰。</param>
    /// <returns>是否夭折。</returns>
    private static bool InfantDies(Difficulty difficulty, double roll)
    {
        var person = Child(0, RulesHarness.Date.Month);
        var family = RulesHarness.FamilyOf(person);
        var state = StateOf(family, difficulty);
        var deaths = new DeathRegister();

        LifespanAdvance.Run(state, RulesHarness.Date, new ScriptedRandom(roll), deaths);

        return deaths.Contains(person);
    }

    /// <summary>建一个已到寿数的成人（年龄 = 寿数）。</summary>
    /// <returns>成员。</returns>
    private static Person Oldest() =>
        FamilyFixtures.NewPerson(
            1,
            "P1",
            Gender.Male,
            RulesHarness.Date.Year - (int)AttributePolicy.LifespanMeanMale,
            RulesHarness.Date.Month,
            generation: 0,
            lifespan: (int)AttributePolicy.LifespanMeanMale);

    /// <summary>建一个生日在给定月份、指定年龄的幼儿。</summary>
    /// <param name="age">年龄。</param>
    /// <param name="birthMonth">出生月份。</param>
    /// <returns>成员。</returns>
    private static Person Child(int age, int birthMonth) =>
        FamilyFixtures.NewPerson(
            1,
            "P1",
            Gender.Male,
            RulesHarness.Date.Year - age,
            birthMonth,
            generation: 0,
            lifespan: 60);

    /// <summary>把成员置于指定状态位（患病同时补上计时字段）。</summary>
    /// <param name="person">成员。</param>
    /// <param name="status">状态位。</param>
    private static void Apply(Person person, StatusFlag status)
    {
        if (status == StatusFlag.Ill)
        {
            person.Status |= StatusFlag.Ill;
            person.Timers = new StatusTimers(illnessSeverityBasisPoints: 0);
            return;
        }

        if (status != StatusFlag.None)
        {
            person.Status |= status;
        }
    }

    /// <summary>把单成员家族装配成游戏状态。</summary>
    /// <param name="person">成员。</param>
    /// <returns>游戏状态与家族。</returns>
    private static (GameState State, Family Family) StateOf(Person person)
    {
        var family = RulesHarness.FamilyOf(person);

        return (StateOf(family, Difficulty.Normal), family);
    }

    /// <summary>把家族装配成游戏状态。</summary>
    /// <param name="family">家族。</param>
    /// <param name="difficulty">难度。</param>
    /// <returns>游戏状态。</returns>
    private static GameState StateOf(Family family, Difficulty difficulty) =>
        new(
            StateId,
            RulesHarness.Date,
            difficulty,
            Origin.Farmer,
            family,
            new FamilyEconomy(
                LivingStandard.Normal,
                new GrainPriceIndex(GrainPricePolicy.Initial),
                new Treasury(RulesHarness.Guan(1000)),
                new Holdings()));

    /// <summary>可计数的固定序列随机源（用尽后回退 0.5；<c>Next</c> 取返回下界）。</summary>
    private sealed class ScriptedRandom : IRandomService
    {
        /// <summary>用尽给定序列后返回的默认中值。</summary>
        private const double DefaultFallback = MidRoll;

        private readonly Queue<double> _values;

        /// <summary>按给定次序构造。</summary>
        /// <param name="values">依次返回的取值。</param>
        public ScriptedRandom(params double[] values) => _values = new Queue<double>(values);

        /// <summary><see cref="NextDouble"/> 的调用次数。</summary>
        public int DoublesRequested { get; private set; }

        /// <inheritdoc />
        public double NextDouble()
        {
            DoublesRequested++;
            return _values.Count > 0 ? _values.Dequeue() : DefaultFallback;
        }

        /// <inheritdoc />
        public int Next(int minInclusive, int maxExclusive) => minInclusive;

        /// <inheritdoc />
        public void NextBytes(Span<byte> buffer) => buffer.Clear();
    }
}
