using KFL.Core.Config;
using KFL.Core.Enums;
using KFL.Infrastructure.Abstractions;

namespace KFL.Rules.Config;

/// <summary>
/// 属性分布参数与取样（规格书 §4.1、§4.2；FR-005、FR-006；data-model §3.2）——两节的**唯一**声明处。
/// </summary>
/// <remarks>
/// <para>
/// **正态取样自实现（Box–Muller）**：每次取样**恰好消耗 2 次 <see cref="IRandomService.NextDouble"/>、
/// **不做**静态缓存（research R-08）——缓存会让「随机消费次序」依赖调用历史，
/// 从而破坏契约六 §3 的确定性断言。MUST NOT 使用 Bogus 的分布方法。
/// </para>
/// <para>
/// **取整与 clamp**：取样 → 四舍五入取整 → clamp 到
/// <see cref="AttributeLimits.Min"/>~<see cref="AttributeLimits.Max"/>（0~100）。
/// **天命寿数是唯一例外**：只 clamp **下界**（0）、**不设上限**（FR-006）。
/// </para>
/// <para>
/// **明确不含**：0~100 值域本身（属 <c>KFL.Core/Config/AttributeLimits</c>，实体自不变量）、
/// 新生儿遗传公式（§4.2，逻辑轨 ⑦）。
/// </para>
/// </remarks>
public static class AttributePolicy
{
    /// <summary>天赋正态均值（四项独立取样，§4.2「开局成员 / 无父母参照者」）。</summary>
    public const decimal TalentMean = 60m;

    /// <summary>天赋正态标准差。</summary>
    public const decimal TalentSigma = 20m;

    /// <summary>学业正态均值（无父母参照者）。</summary>
    public const decimal StudyMean = 30m;

    /// <summary>学业正态标准差。</summary>
    public const decimal StudySigma = 15m;

    /// <summary>体质正态均值（无父母参照者）。</summary>
    public const decimal HealthMean = 85m;

    /// <summary>体质正态标准差。</summary>
    public const decimal HealthSigma = 10m;

    /// <summary>天命寿数正态均值：男（§4.1）。</summary>
    public const decimal LifespanMeanMale = 60.7m;

    /// <summary>天命寿数正态标准差：男。</summary>
    public const decimal LifespanSigmaMale = 8m;

    /// <summary>天命寿数正态均值：女（§4.1）。</summary>
    public const decimal LifespanMeanFemale = 62.3m;

    /// <summary>天命寿数正态标准差：女。</summary>
    public const decimal LifespanSigmaFemale = 8m;

    /// <summary>
    /// 标准正态取样（Box–Muller），**恰好消耗 2 次** <see cref="IRandomService.NextDouble"/>。
    /// </summary>
    /// <param name="mean">均值。</param>
    /// <param name="sigma">标准差（<c>&gt;= 0</c>）。</param>
    /// <param name="random">随机来源。</param>
    /// <returns>服从 <c>N(mean, sigma²)</c> 的实数（未取整、未 clamp）。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sigma"/> 为负。</exception>
    public static double NextNormal(double mean, double sigma, IRandomService random)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (sigma < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(sigma), sigma, "标准差 MUST >= 0（规格书 §4.2）。");
        }

        // 1 − u 把 [0,1) 映射到 (0,1]，使 ln 恒有定义（u = 0 时 z = 0，不产生 ±∞）。
        var u1 = 1d - random.NextDouble();
        var u2 = random.NextDouble();

        var z = Math.Sqrt(-2d * Math.Log(u1)) * Math.Cos(2d * Math.PI * u2);

        return mean + (sigma * z);
    }

    /// <summary>天赋 = <c>N(60, 20)</c> → 四舍五入 → clamp 0~100。</summary>
    /// <param name="random">随机来源。</param>
    /// <returns>天赋取值。</returns>
    public static int NextTalent(IRandomService random) =>
        ClampAttribute(Round(NextNormal((double)TalentMean, (double)TalentSigma, random)));

    /// <summary>学业 = <c>N(30, 15)</c> → 四舍五入 → clamp 0~100。</summary>
    /// <param name="random">随机来源。</param>
    /// <returns>学业取值。</returns>
    public static int NextStudy(IRandomService random) =>
        ClampAttribute(Round(NextNormal((double)StudyMean, (double)StudySigma, random)));

    /// <summary>体质 = <c>N(85, 10)</c> → 四舍五入 → clamp 0~100。</summary>
    /// <param name="random">随机来源。</param>
    /// <returns>体质取值。</returns>
    public static int NextHealth(IRandomService random) =>
        ClampAttribute(Round(NextNormal((double)HealthMean, (double)HealthSigma, random)));

    /// <summary>
    /// 天命寿数 = <c>N(60.7, 8)</c>（男）/ <c>N(62.3, 8)</c>（女）→ 四舍五入 →
    /// **只 clamp 下界 0、不设上限**（FR-006）。
    /// </summary>
    /// <param name="gender">性别。</param>
    /// <param name="random">随机来源。</param>
    /// <returns>天命寿数（年）。</returns>
    public static int NextLifespan(Gender gender, IRandomService random)
    {
        var mean = gender == Gender.Male ? LifespanMeanMale : LifespanMeanFemale;
        var sigma = gender == Gender.Male ? LifespanSigmaMale : LifespanSigmaFemale;

        return Math.Max(AttributeLimits.Min, Round(NextNormal((double)mean, (double)sigma, random)));
    }

    /// <summary>四舍五入取整（远离零方向，即通常所说的「四舍五入」）。</summary>
    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private static int ClampAttribute(int value) => Math.Clamp(value, AttributeLimits.Min, AttributeLimits.Max);
}
