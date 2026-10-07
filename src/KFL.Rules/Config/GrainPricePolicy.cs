namespace KFL.Rules.Config;

/// <summary>
/// 米价系数策略（规格书 §5.1；data-model §4.1；research R-10 的 §17 裁决 E-01）。
/// </summary>
/// <remarks>
/// <para>
/// **状态量就是米价系数**（初始 <see cref="Initial"/>、每月游走 <see cref="WalkAmplitude"/>
/// 对应 ±10%、clamp 到 <see cref="Min"/>~<see cref="Max"/>），**直接作生活费乘数**；
/// <c>米价</c> = `(系数 − <see cref="PriceOffset"/>) ÷ <see cref="PriceScale"/>` 只是**派生展示值**
/// （阶段③ 用），**不参与本阶段结算**。
/// </para>
/// <para>
/// 本类是这五个数值的唯一出处（SC-008）；<c>KFL.Core</c> 的 <c>GrainPriceIndex</c>
/// 只承载「系数是一个正数」，MUST NOT 另存 clamp 区间、游走幅度与初值。
/// </para>
/// </remarks>
public static class GrainPricePolicy
{
    /// <summary>初始米价系数（新建存档）。</summary>
    public const decimal Initial = 1.0m;

    /// <summary>clamp 下界。</summary>
    public const decimal Min = 0.7m;

    /// <summary>clamp 上界。</summary>
    public const decimal Max = 3.0m;

    /// <summary>月游走幅度：`±10%` 对应整幅 <c>0.2</c>（映射式的 `r × 0.2 − 0.1`）。</summary>
    public const decimal WalkAmplitude = 0.2m;

    /// <summary>米价派生式的偏移项（`米价 = (系数 − 0.4) ÷ 0.6`）。</summary>
    public const decimal PriceOffset = 0.4m;

    /// <summary>米价派生式的比例项（`米价 = (系数 − 0.4) ÷ 0.6`）。</summary>
    public const decimal PriceScale = 0.6m;

    /// <summary>把米价系数 clamp 到 <see cref="Min"/>~<see cref="Max"/>。</summary>
    /// <param name="value">待 clamp 的系数。</param>
    /// <returns>区间内的取值。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> 不大于 0。</exception>
    public static decimal Clamp(decimal value)
    {
        if (value <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value, "米价系数 MUST > 0（data-model §1.2）。");
        }

        return Math.Clamp(value, Min, Max);
    }

    /// <summary>
    /// 按月游走一次并 clamp：`系数 ← clamp(系数 × (1 + (r × 0.2 − 0.1)))`，`r ∈ [0,1)`。
    /// </summary>
    /// <param name="current">游走前的系数（MUST <c>&gt; 0</c>）。</param>
    /// <param name="r">注入的随机取值，`[0,1)`。</param>
    /// <returns>游走并 clamp 后的系数。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="current"/> 不大于 0。</exception>
    public static decimal Walk(decimal current, double r)
    {
        if (current <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(current), current, "米价系数 MUST > 0（data-model §1.2）。");
        }

        var factor = 1m + (((decimal)r * WalkAmplitude) - (WalkAmplitude / 2m));

        return Clamp(current * factor);
    }

    /// <summary>米价的派生展示值：`(系数 − 0.4) ÷ 0.6`（阶段③ 使用，不参与结算）。</summary>
    /// <param name="index">米价系数。</param>
    /// <returns>派生米价。</returns>
    public static decimal MarketPrice(decimal index) => (index - PriceOffset) / PriceScale;
}
