using KFL.Core.Config;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;
using KFL.Rules.FamilyLifecycle;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// 患病与病程（SC-014、SC-015；契约二十 §4~§6）——⑧-c 的判定口径：
/// 体质线性插值、主导天赋乘数、免病年龄、宽限月、先扣体质后掷清除、疗程与药费。
/// </summary>
public sealed class IllnessTests
{
    /// <summary>固定的游戏状态标识。</summary>
    private static readonly Guid StateId = Guid.Parse("3d7a9f21-6b04-4c58-9e73-1a2b8c5d6e90");

    /// <summary>成人年龄（≥ <see cref="IllnessPolicy.Adult"/>，无年龄增益）。</summary>
    private const int AdultAge = 30;

    /// <summary>中值掷骰（不命中清除）。</summary>
    private const double MidRoll = 0.5d;  // arch-guard:allow 夹具随机取值（非规则数值副本）

    /// <summary>定疾病程度的掷骰（命中感染后紧接的那一掷；取值本身不是规则数值）。</summary>
    private const double SeverityRoll = 0.25d;  // arch-guard:allow 夹具随机取值（非规则数值副本）

    /// <summary>判定分界点两侧的极小偏移。</summary>
    private const double Epsilon = 1e-9d;

    // ------------------------------------------------------------------ 感染概率
    /// <summary>体质端点处的患病概率按配置成员取值，且判定为严格小于。</summary>
    [Fact]
    public void 患病概率在体质端点上按配置成员取值()
    {
        var full = InfectionChance(IllnessPolicy.HealthCeiling, IllnessPolicy.TalentMultiplierFarmer);
        Assert.True(Infected(IllnessPolicy.HealthCeiling, full - Epsilon, agriculture: 50));
        Assert.False(Infected(IllnessPolicy.HealthCeiling, full + Epsilon, agriculture: 50));

        var floor = InfectionChance(IllnessPolicy.HealthFloor, IllnessPolicy.TalentMultiplierArtisan);
        Assert.True(Infected(IllnessPolicy.HealthFloor, floor - Epsilon, craft: 50));
        Assert.False(Infected(IllnessPolicy.HealthFloor, floor + Epsilon, craft: 50));
    }

    /// <summary>患病概率按体质在九十九格上线性插值（无年龄档、无二次封顶）。</summary>
    /// <param name="health">当月初体质。</param>
    [Theory]
    [InlineData(100)]
    [InlineData(75)]
    [InlineData(50)]
    [InlineData(25)]
    [InlineData(1)]
    public void 患病概率按体质线性插值(int health)
    {
        var chance = InfectionChance(health, IllnessPolicy.TalentMultiplierFarmer);

        Assert.True(Infected(health, chance - Epsilon, agriculture: 50));
        Assert.False(Infected(health, chance + Epsilon, agriculture: 50));
    }

    /// <summary>主导天赋取四项最高值；并列按 农 → 商 → 仕 → 工 取先者。</summary>
    [Fact]
    public void 主导天赋乘数按四项最高值且并列取农优先()
    {
        AssertMultiplier(IllnessPolicy.TalentMultiplierFarmer, agriculture: 50);
        AssertMultiplier(IllnessPolicy.TalentMultiplierMerchant, commerce: 50);
        AssertMultiplier(IllnessPolicy.TalentMultiplierScholar, officialdom: 50);
        AssertMultiplier(IllnessPolicy.TalentMultiplierArtisan, craft: 50);
        AssertMultiplier(IllnessPolicy.TalentMultiplierFarmer, agriculture: 50, commerce: 50, officialdom: 50, craft: 50);
        AssertMultiplier(IllnessPolicy.TalentMultiplierMerchant, agriculture: 10, commerce: 50, officialdom: 20, craft: 30);
    }

    /// <summary>患病概率不引用难度负面事件系数（SC-014）：四难度同种子逐字段完全相同。</summary>
    [Fact]
    public void 患病概率不随难度变化()
    {
        var results = Enum.GetValues<Difficulty>().Select(Probe).ToList();

        Assert.All(results, result => Assert.Equal(results[0], result));

        static (int Health, int BasisPoints, StatusFlag Status) Probe(Difficulty difficulty)
        {
            var (state, member) = Patient(50, difficulty: difficulty);
            IllnessAdvance.Run(state, new ScriptedRandom(0.0d, SeverityRoll), new DeathRegister());

            return (member.Health, member.Timers.IllnessSeverityBasisPoints ?? -1, member.Status);
        }
    }

    /// <summary>不满三岁不掷感染判定且当月零随机消费。</summary>
    [Fact]
    public void 不满三岁不掷感染且零随机消费()
    {
        var (state, member) = Patient(IllnessPolicy.HealthFloor, age: IllnessPolicy.IllnessExemptAge - 1);
        var random = new ScriptedRandom(0.0d);

        IllnessAdvance.Run(state, random, new DeathRegister());

        Assert.Equal(StatusFlag.None, member.Status & StatusFlag.Ill);
        Assert.Equal(0, random.DoublesRequested);
    }

    /// <summary>满三岁起参与感染判定（一掷定是否患病、紧接一掷定程度）。</summary>
    [Fact]
    public void 满三岁起参与感染判定()
    {
        var (state, member) = Patient(IllnessPolicy.HealthFloor, age: IllnessPolicy.IllnessExemptAge);
        var random = new ScriptedRandom(0.0d, SeverityRoll);
        var facts = IllnessAdvance.Run(state, random, new DeathRegister());

        Assert.Equal(StatusFlag.Ill, member.Status & StatusFlag.Ill);
        Assert.Equal(2, random.DoublesRequested);
        Assert.Equal(IllnessPolicy.ToBasisPoints(SeverityRoll), member.Timers.IllnessSeverityBasisPoints);
        Assert.Null(member.Timers.TreatmentRemainingMonths);
        Assert.Contains(facts, fact => fact.PersonId == member.Id && fact.Contracted && !fact.Recovered);
    }

    /// <summary>确诊当月是宽限月：不扣体质、不付药费、不掷清除。</summary>
    [Fact]
    public void 确诊当月不扣体质不掷清除()
    {
        var (state, member) = Patient(50, age: IllnessPolicy.IllnessExemptAge);
        var random = new ScriptedRandom(0.0d, SeverityRoll);

        IllnessAdvance.Run(state, random, new DeathRegister());

        Assert.Equal(50, member.Health);
        Assert.Equal(2, random.DoublesRequested);
        Assert.Empty(state.Economy.Ledger.Entries);
    }

    // ------------------------------------------------------------------ 病程推进
    /// <summary>患病次月起按程度扣减体质（<c>基数 + 系数 × s</c>）。</summary>
    /// <param name="basisPoints">疾病程度（万分之）。</param>
    /// <param name="expectedLoss">期望体质扣减。</param>
    [Theory]
    [InlineData(0, IllnessPolicy.SeverityLossBase)]
    [InlineData(IllnessPolicy.SeverityBasisPointsScale, IllnessPolicy.SeverityLossBase + IllnessPolicy.SeverityLossSpan)]
    public void 患病次月按程度扣减体质(int basisPoints, int expectedLoss)
    {
        var (state, member) = Patient(20);
        Illness(member, basisPoints, treatment: null);
        var random = new ScriptedRandom(MidRoll);

        IllnessAdvance.Run(state, random, new DeathRegister());

        Assert.Equal(20 - expectedLoss, member.Health);
        Assert.Equal(1, random.DoublesRequested);
        Assert.Equal(basisPoints, member.Timers.IllnessSeverityBasisPoints);
    }

    /// <summary>清除概率按**扣减之后**的体质计算（次序：先扣体质、再掷清除）。</summary>
    [Fact]
    public void 清除概率按扣减之后的体质计算()
    {
        // 农、成人（b = 0）：体质 50、程度 0.5 ⇒ 扣减 6 ⇒ 44 ⇒ q = 0.44 × 1.20 = 0.528。
        var postDeduction = 50 - Loss(5000);
        var chance = ClearanceChance(postDeduction, IllnessPolicy.FarmerRecoveryMultiplier, bonus: 0d, treated: false);

        Assert.True(Recovers(50, 5000, chance - Epsilon, agriculture: 50));
        Assert.False(Recovers(50, 5000, chance + Epsilon, agriculture: 50));

        // 若误用扣减前的体质，则 0.50 × 1.20 = 0.60 处的掷骰会被判为痊愈。
        var wrong = ClearanceChance(50, IllnessPolicy.FarmerRecoveryMultiplier, bonus: 0d, treated: false);
        Assert.False(Recovers(50, 5000, (chance + wrong) / 2d, agriculture: 50));
    }

    /// <summary>清除概率在掷骰前钳到 1：必愈且零随机消费。</summary>
    [Fact]
    public void 清除概率钳到一时必愈且不消费随机()
    {
        var (state, member) = Patient(IllnessPolicy.HealthCeiling);
        Illness(member, 0, treatment: null);
        var random = new ScriptedRandom();
        var facts = IllnessAdvance.Run(state, random, new DeathRegister());

        Assert.Equal(StatusFlag.None, member.Status & StatusFlag.Ill);
        Assert.Null(member.Timers.IllnessSeverityBasisPoints);
        Assert.Null(member.Timers.TreatmentRemainingMonths);
        Assert.Equal(0, random.DoublesRequested);
        Assert.Contains(facts, fact => fact.PersonId == member.Id && !fact.Contracted && fact.Recovered);
    }

    /// <summary>年龄增益三段边界：3~11 岁 +0.60、12~23 岁 +0.30、24 岁起 +0。</summary>
    /// <param name="age">年龄。</param>
    /// <param name="bonus">期望增益。</param>
    [Theory]
    [InlineData(IllnessPolicy.ClearBonusBoundaryChild, 0.60d)]      // arch-guard:allow 夹具随机取值（非规则数值副本）
    [InlineData(IllnessPolicy.Youth - 1, 0.60d)]                    // arch-guard:allow 夹具随机取值（非规则数值副本）
    [InlineData(IllnessPolicy.Youth, 0.30d)]                        // arch-guard:allow 夹具随机取值（非规则数值副本）
    [InlineData(IllnessPolicy.Adult - 1, 0.30d)]                    // arch-guard:allow 夹具随机取值（非规则数值副本）
    [InlineData(IllnessPolicy.Adult, 0d)]
    public void 清除概率的年龄增益按三段边界取值(int age, double bonus)
    {
        const int Health = 20;
        var postDeduction = Health - Loss(5000);
        var chance = ClearanceChance(postDeduction, IllnessPolicy.MerchantRecoveryMultiplier, bonus, treated: false);

        Assert.True(Recovers(Health, 5000, chance - Epsilon, age: age, commerce: 50));
        Assert.False(Recovers(Health, 5000, chance + Epsilon, age: age, commerce: 50));
    }

    /// <summary>归零体质由本子步登记 Illness 死亡，且登记后不再掷清除。</summary>
    [Fact]
    public void 体质归零时登记病亡且不掷清除()
    {
        var (state, member) = Patient(3);
        Illness(member, IllnessPolicy.SeverityBasisPointsScale, treatment: null);
        var deaths = new DeathRegister();
        var random = new ScriptedRandom(MidRoll);

        IllnessAdvance.Run(state, random, deaths);

        Assert.True(deaths.Contains(member));
        Assert.Equal(AttributeLimits.Min, member.Health);
        Assert.Equal(0, random.DoublesRequested);
        Assert.Equal(StatusFlag.Ill, member.Status & StatusFlag.Ill);
    }

    // ------------------------------------------------------------------ 疗程与药费
    /// <summary>疗程恰三个月：每月扣一次药费并递减剩余月数，结束后不再产生药费。</summary>
    [Fact]
    public void 疗程三个月逐月递减且药费只在疗程月产生()
    {
        var (state, member) = Patient(20);
        Illness(member, 5000, treatment: IllnessPolicy.TreatmentMonths);

        for (var month = IllnessPolicy.TreatmentMonths; month >= 1; month--)
        {
            Assert.Equal(month, member.Timers.TreatmentRemainingMonths);

            IllnessAdvance.Run(state, new ScriptedRandom(MidRoll), new DeathRegister());
        }

        Assert.Equal(0, member.Timers.TreatmentRemainingMonths);

        IllnessAdvance.Run(state, new ScriptedRandom(MidRoll), new DeathRegister());

        Assert.Equal(0, member.Timers.TreatmentRemainingMonths);
        Assert.Equal(
            IllnessPolicy.TreatmentMonths,
            state.Economy.Ledger.Entries.Count(entry => entry.Category == LedgerCategory.MedicineExpense));
    }

    /// <summary>药费按（<c>基数 + 系数 × s</c>）× 支出系数落到 MedicineExpense。</summary>
    [Fact]
    public void 药费按程度与支出系数落账()
    {
        var (state, member) = Patient(80);
        Illness(member, 5000, treatment: IllnessPolicy.TreatmentMonths);

        IllnessAdvance.Run(state, new ScriptedRandom(MidRoll), new DeathRegister());

        var entry = Assert.Single(state.Economy.Ledger.Entries, item => item.Category == LedgerCategory.MedicineExpense);
        var expected = (IllnessPolicy.MedicineBaseGuan + IllnessPolicy.MedicineSeverityGuan * (decimal)MidRoll)
            * DifficultyRates.ExpenseFactor(Difficulty.Normal);

        Assert.Equal(-expected, entry.Amount.Guan);
        Assert.Equal(member.Id, entry.PersonId);
    }

    /// <summary>付不起药费时疗程中止（置 null）、不落条目、不转贷款，本月按未治疗口径继续。</summary>
    [Fact]
    public void 付不起药费时疗程中止且不落条目()
    {
        var (state, member) = Patient(80, cash: Money.Zero);
        Illness(member, 5000, treatment: IllnessPolicy.TreatmentMonths);
        var random = new ScriptedRandom(MidRoll);

        IllnessAdvance.Run(state, random, new DeathRegister());

        Assert.Null(member.Timers.TreatmentRemainingMonths);
        Assert.Equal(80 - Loss(5000), member.Health);
        Assert.Empty(state.Economy.Ledger.Entries);
        Assert.Equal(0, state.Economy.Treasury.Loan.Principal.Guan);
    }

    /// <summary>未治疗的疾病不过期：高掷下逐月只扣体质，程度不变、状态仍为患病。</summary>
    [Fact]
    public void 未治疗的疾病不过期()
    {
        var (state, member) = Patient(10);
        Illness(member, 1000, treatment: null);
        var random = new ScriptedRandom(MidRoll, MidRoll);

        IllnessAdvance.Run(state, random, new DeathRegister());
        IllnessAdvance.Run(state, random, new DeathRegister());

        Assert.Equal(StatusFlag.Ill, member.Status & StatusFlag.Ill);
        Assert.Equal(1000, member.Timers.IllnessSeverityBasisPoints);
        Assert.Null(member.Timers.TreatmentRemainingMonths);
        Assert.Equal(2, random.DoublesRequested);
    }

    // ------------------------------------------------------------------ 基座
    /// <summary>体质满值时的月患病率（按主导天赋乘数缩放）。</summary>
    /// <param name="health">当月初体质。</param>
    /// <param name="multiplier">主导天赋的感染乘数。</param>
    /// <returns>月患病率。</returns>
    private static double InfectionChance(int health, decimal multiplier)
    {
        var clamped = Math.Clamp(health, IllnessPolicy.HealthFloor, IllnessPolicy.HealthCeiling);
        var baseChance = (double)IllnessPolicy.InfectionChanceAtFullHealth
            + ((double)IllnessPolicy.InfectionChanceAtFloorHealth - (double)IllnessPolicy.InfectionChanceAtFullHealth)
            * (IllnessPolicy.HealthCeiling - clamped) / IllnessPolicy.InterpolationSpan;

        return baseChance * (double)multiplier;
    }

    /// <summary>按程度折算的体质扣减（<c>基数 + 系数 × s</c>）。</summary>
    /// <param name="basisPoints">疾病程度（万分之）。</param>
    /// <returns>体质扣减。</returns>
    private static int Loss(int basisPoints) =>
        (int)Math.Round(
            IllnessPolicy.SeverityLossBase
                + IllnessPolicy.SeverityLossSpan * ((double)basisPoints / IllnessPolicy.SeverityBasisPointsScale),
            MidpointRounding.AwayFromZero);

    /// <summary>清除概率（<c>min(1, …)</c>，掷骰前钳位）。</summary>
    /// <param name="health">扣减之后的体质。</param>
    /// <param name="multiplier">主导天赋的清除乘数。</param>
    /// <param name="bonus">年龄增益。</param>
    /// <param name="treated">是否在疗程中。</param>
    /// <returns>清除概率。</returns>
    private static double ClearanceChance(double health, decimal multiplier, double bonus, bool treated)
    {
        var raw = treated
            ? ((health * (double)IllnessPolicy.TreatedRecoveryCoefficient / 100d)
                + (double)IllnessPolicy.TreatedRecoveryOffset) * (double)multiplier
            : health * (double)IllnessPolicy.BaseRecoveryCoefficient / 100d * (double)multiplier;

        return Math.Min(1d, raw + bonus);
    }

    /// <summary>跑一次 ⑧-c，返回该成员是否被判定患病。</summary>
    /// <param name="health">当月初体质。</param>
    /// <param name="roll">第一掷（感染判定）。</param>
    /// <param name="age">年龄。</param>
    /// <param name="agriculture">农技天赋。</param>
    /// <param name="commerce">商才天赋。</param>
    /// <param name="officialdom">仕途天赋。</param>
    /// <param name="craft">工巧天赋。</param>
    /// <returns>是否患病。</returns>
    private static bool Infected(
        int health,
        double roll,
        int age = AdultAge,
        int agriculture = 0,
        int commerce = 0,
        int officialdom = 0,
        int craft = 0)
    {
        var (state, member) = Patient(health, age: age, agriculture: agriculture, commerce: commerce, officialdom: officialdom, craft: craft);

        IllnessAdvance.Run(state, new ScriptedRandom(roll, MidRoll), new DeathRegister());

        return (member.Status & StatusFlag.Ill) == StatusFlag.Ill;
    }

    /// <summary>断言主导天赋乘数分档：分界点两侧的判定结果翻转。</summary>
    /// <param name="multiplier">期望的感染乘数。</param>
    /// <param name="agriculture">农技天赋。</param>
    /// <param name="commerce">商才天赋。</param>
    /// <param name="officialdom">仕途天赋。</param>
    /// <param name="craft">工巧天赋。</param>
    private static void AssertMultiplier(decimal multiplier, int agriculture = 0, int commerce = 0, int officialdom = 0, int craft = 0)
    {
        const int Health = 50;
        var chance = InfectionChance(Health, multiplier);

        Assert.True(Infected(Health, chance - Epsilon, AdultAge, agriculture, commerce, officialdom, craft));
        Assert.False(Infected(Health, chance + Epsilon, AdultAge, agriculture, commerce, officialdom, craft));
    }

    /// <summary>跑一次 ⑧-c，返回该患病成员是否在本月痊愈。</summary>
    /// <param name="health">患病前体质。</param>
    /// <param name="basisPoints">疾病程度（万分之）。</param>
    /// <param name="roll">清除掷骰。</param>
    /// <param name="age">年龄。</param>
    /// <param name="agriculture">农技天赋。</param>
    /// <param name="commerce">商才天赋。</param>
    /// <returns>是否痊愈。</returns>
    private static bool Recovers(
        int health,
        int basisPoints,
        double roll,
        int age = AdultAge,
        int agriculture = 0,
        int commerce = 0)
    {
        var (state, member) = Patient(health, age: age, agriculture: agriculture, commerce: commerce);
        Illness(member, basisPoints, treatment: null);

        IllnessAdvance.Run(state, new ScriptedRandom(roll), new DeathRegister());

        return (member.Status & StatusFlag.Ill) == StatusFlag.None;
    }

    /// <summary>把成员置于患病态。</summary>
    /// <param name="person">成员。</param>
    /// <param name="basisPoints">疾病程度（万分之）。</param>
    /// <param name="treatment">剩余疗程月数。</param>
    private static void Illness(Person person, int basisPoints, int? treatment)
    {
        person.Status |= StatusFlag.Ill;
        person.Timers = new StatusTimers(
            illnessSeverityBasisPoints: basisPoints,
            treatmentRemainingMonths: treatment);
    }

    /// <summary>建一个单成员家族与其游戏状态。</summary>
    /// <param name="health">当月初体质。</param>
    /// <param name="age">年龄。</param>
    /// <param name="difficulty">难度。</param>
    /// <param name="cash">现银。</param>
    /// <param name="agriculture">农技天赋。</param>
    /// <param name="commerce">商才天赋。</param>
    /// <param name="officialdom">仕途天赋。</param>
    /// <param name="craft">工巧天赋。</param>
    /// <returns>游戏状态与成员。</returns>
    private static (GameState State, Person Member) Patient(
        int health,
        int age = AdultAge,
        Difficulty difficulty = Difficulty.Normal,
        Money? cash = null,
        int agriculture = 0,
        int commerce = 0,
        int officialdom = 0,
        int craft = 0)
    {
        var member = RulesHarness.Member(1, Gender.Male, age, agriculture, commerce, officialdom, craft);
        member.Health = health;

        var family = RulesHarness.FamilyOf(member);
        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(cash ?? RulesHarness.Guan(1000)),
            new Holdings());
        var state = new GameState(StateId, RulesHarness.Date, difficulty, Origin.Farmer, family, economy);

        return (state, member);
    }

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
