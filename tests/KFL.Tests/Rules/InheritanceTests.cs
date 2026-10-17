using KFL.Core.Config;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;
using KFL.Rules.FamilyLifecycle;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// US2 遗传的验收测试（契约二十 §6 / FR-011 / SC-003 / T042）：
/// ① 天赋、学业、体质三者的正态参数（含学业均值折半、共用同一 σ）；
/// ② 取整与 clamp 0~100；③ 体质下限 20；④ 任一方缺失走「无父母参照者」分布；
/// ⑤ 每次取值恰消耗 2 次 <c>NextDouble</c>。
/// </summary>
/// <remarks>
/// 期望值在测试内按规格书 §4.2 的公式**独立**算一遍（不调用被测代码），
/// 因此能真正钉住 <c>mean</c> 与 <c>sigma</c> 的取法。
/// </remarks>
public class InheritanceTests
{
    /// <summary>第一次 <c>NextDouble</c> 的夹具取值（Box–Muller 的 u1）。</summary>
    private const double FirstRoll = 0.25d;  // arch-guard:allow 夹具随机取值（非规则数值副本）

    /// <summary>第二次 <c>NextDouble</c> 的夹具取值（Box–Muller 的 u2）。</summary>
    private const double SecondRoll = 0.6d;  // arch-guard:allow 夹具随机取值（非规则数值副本）

    /// <summary>取 0.5：使 <c>cos</c> 项为 −1（用于压向下界）。</summary>
    private const double MidRoll = 0.5d;  // arch-guard:allow 夹具随机取值（非规则数值副本）

    /// <summary>取接近 1 的巨大值：使 <c>sqrt(−2 ln(1−u))</c> 显著放大 |z|。</summary>
    private const double HighRoll = 0.999d;

    // ------------------------------------------------------------------ ① 正态参数
    /// <summary>天赋遗传：<c>mean = (父 + 母) / 2</c>、<c>sigma = |父 − 母| / 2 + 5</c>。</summary>
    /// <param name="father">父方天赋。</param>
    /// <param name="mother">母方天赋。</param>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(100, 100)]
    [InlineData(50, 50)]
    [InlineData(50, 60)]
    [InlineData(60, 50)]
    [InlineData(30, 80)]
    [InlineData(80, 30)]
    [InlineData(10, 90)]
    [InlineData(90, 10)]
    [InlineData(45, 55)]
    [InlineData(55, 45)]
    [InlineData(20, 20)]
    [InlineData(70, 30)]
    [InlineData(30, 70)]
    [InlineData(100, 50)]
    [InlineData(50, 100)]
    [InlineData(0, 50)]
    [InlineData(50, 0)]
    [InlineData(33, 77)]
    [InlineData(77, 33)]
    public void 天赋遗传以父母均值与差半加五为正态参数(int father, int mother)
    {
        var random = new ScriptedRandom(FirstRoll, SecondRoll);

        var actual = Inheritance.Attribute(father, mother, random);

        Assert.Equal(Expected(father, mother, meanScale: 1d), actual);
        Assert.Equal(2, random.DoublesRequested);
    }

    /// <summary>学业遗传：均值折半、σ 与天赋**完全相同**。</summary>
    /// <param name="father">父方学业。</param>
    /// <param name="mother">母方学业。</param>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 0)]
    [InlineData(0, 100)]
    [InlineData(100, 100)]
    [InlineData(30, 30)]
    [InlineData(40, 60)]
    [InlineData(60, 40)]
    [InlineData(10, 90)]
    [InlineData(90, 10)]
    [InlineData(25, 75)]
    [InlineData(75, 25)]
    [InlineData(50, 50)]
    [InlineData(5, 95)]
    [InlineData(95, 5)]
    [InlineData(12, 88)]
    [InlineData(88, 12)]
    [InlineData(1, 99)]
    [InlineData(99, 1)]
    [InlineData(20, 40)]
    [InlineData(40, 20)]
    [InlineData(0, 60)]
    [InlineData(60, 0)]
    public void 学业遗传均值折半且与天赋共用同一标准差(int father, int mother)
    {
        var random = new ScriptedRandom(FirstRoll, SecondRoll);

        var actual = Inheritance.Study(father, mother, random);

        Assert.Equal(
            Expected(father, mother, (double)ChildbirthPolicy.StudyMeanHalving),
            actual);
        Assert.Equal(2, random.DoublesRequested);
    }

    /// <summary>体质遗传：参数同天赋，另加下限 20。</summary>
    /// <param name="father">父方体质。</param>
    /// <param name="mother">母方体质。</param>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 100)]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(20, 20)]
    [InlineData(30, 70)]
    [InlineData(70, 30)]
    [InlineData(85, 85)]
    public void 体质遗传不低于二十(int father, int mother)
    {
        var random = new ScriptedRandom(FirstRoll, SecondRoll);

        var actual = Inheritance.Health(father, mother, random);

        Assert.Equal(
            Math.Max(
                Expected(father, mother, meanScale: 1d),
                ChildbirthPolicy.InheritedHealthFloor),
            actual);
        Assert.True(actual >= ChildbirthPolicy.InheritedHealthFloor);
        Assert.Equal(2, random.DoublesRequested);
    }

    /// <summary>父母全为 0 时天赋取到 0，而体质被抬到下限 20。</summary>
    [Fact]
    public void 体质下限在父母属性极低时抬高取值()
    {
        var attribute = Inheritance.Attribute(0, 0, new ScriptedRandom(FirstRoll, SecondRoll));
        var health = Inheritance.Health(0, 0, new ScriptedRandom(FirstRoll, SecondRoll));

        Assert.Equal(AttributeLimits.Min, attribute);
        Assert.Equal(ChildbirthPolicy.InheritedHealthFloor, health);
        Assert.True(health > attribute);
    }

    // ------------------------------------------------------------------ ② 取整与 clamp
    /// <summary>正态取值越界时被夹在 0~100（上界与下界各一例）。</summary>
    [Fact]
    public void 遗传取值被夹在零到一百之间()
    {
        var high = Inheritance.Attribute(100, 100, new ScriptedRandom(HighRoll, 0.0d));
        var low = Inheritance.Attribute(0, 0, new ScriptedRandom(HighRoll, MidRoll));

        Assert.Equal(AttributeLimits.Max, high);
        Assert.Equal(AttributeLimits.Min, low);
    }

    // ------------------------------------------------------------------ ④ 任一方缺失
    /// <summary>父或母缺失（含单亲）⇒ 天赋走无父母参照者分布 N(60, 20)，MUST NOT 用单亲值外推。</summary>
    [Fact]
    public void 任一方缺失时天赋走无父母参照者分布()
    {
        var onlyFather = Inheritance.Attribute(100, null, new ScriptedRandom(FirstRoll, SecondRoll));
        var onlyMother = Inheritance.Attribute(null, 100, new ScriptedRandom(FirstRoll, SecondRoll));
        var neither = Inheritance.Attribute(null, null, new ScriptedRandom(FirstRoll, SecondRoll));
        var reference = AttributePolicy.NextTalent(new ScriptedRandom(FirstRoll, SecondRoll));

        Assert.Equal(reference, onlyFather);
        Assert.Equal(reference, onlyMother);
        Assert.Equal(reference, neither);

        var extrapolated = Inheritance.Attribute(100, 100, new ScriptedRandom(FirstRoll, SecondRoll));
        Assert.NotEqual(extrapolated, onlyFather);
        Assert.NotEqual(AttributeLimits.Max, onlyFather);
    }

    /// <summary>任一方缺失 ⇒ 学业走 N(30, 15)、体质走 N(85, 10)。</summary>
    [Fact]
    public void 任一方缺失时学业与体质走无父母参照者分布()
    {
        Assert.Equal(
            AttributePolicy.NextStudy(new ScriptedRandom(FirstRoll, SecondRoll)),
            Inheritance.Study(90, null, new ScriptedRandom(FirstRoll, SecondRoll)));
        Assert.Equal(
            AttributePolicy.NextHealth(new ScriptedRandom(FirstRoll, SecondRoll)),
            Inheritance.Health(null, 10, new ScriptedRandom(FirstRoll, SecondRoll)));
    }

    /// <summary>测试内独立重算期望值（规格书 §4.2 的公式，逐字对应 <c>AttributePolicy.NextNormal</c>）。</summary>
    private static int Expected(int father, int mother, double meanScale)
    {
        var mean = ((father + mother) / 2d) * meanScale;
        var sigma = (Math.Abs(father - mother) / 2d) + ChildbirthPolicy.InheritanceSigmaBase;

        var u1 = 1d - FirstRoll;
        var z = Math.Sqrt(-2d * Math.Log(u1)) * Math.Cos(2d * Math.PI * SecondRoll);
        var value = (int)Math.Round(mean + (sigma * z), MidpointRounding.AwayFromZero);

        return Math.Clamp(value, AttributeLimits.Min, AttributeLimits.Max);
    }

    /// <summary>可计数的固定序列随机源（与 <c>FixedRandomService</c> 同款 + 消费计数）。</summary>
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

        /// <inheritdoc />
        public double NextDouble()
        {
            DoublesRequested++;
            return _values.Count > 0 ? _values.Dequeue() : DefaultFallback;
        }

        /// <inheritdoc />
        public int Next(int minInclusive, int maxExclusive) => minInclusive;

        /// <inheritdoc />
        public void NextBytes(Span<byte> destination) => destination.Clear();
    }
}
