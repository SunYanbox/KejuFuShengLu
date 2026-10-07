namespace KFL.Rules.Config;

/// <summary>
/// 储蓄与贷款的利率表（规格书 §5.4）：区间 <b>0.5% ~ 2.4%</b>、计息周期 <b>12 月</b>。
/// </summary>
/// <remarks>
/// <para>
/// US2（贷款计息）与 US3（储蓄利率）**共用**这张表：两处的映射公式同为
/// <c>利率 = 区间下限 + r × (区间上限 − 区间下限)</c>（<c>r ∈ [0,1)</c>，即 <c>0.005 + r × 0.019</c>）。
/// 任一故事独占都会让另一故事出现跨故事结论依赖（tasks T022）。
/// </para>
/// <para>
/// **区间端点供断言**：<c>NextDouble()</c> 取 0 时得到区间下限，取接近 1 的极大值时逼近区间上限
/// （契约三 §4 的映射表）。
/// </para>
/// <para>
/// **明确不含**：储蓄利率的 roll 时点与「当年不变」语义（属结算编排，契约三 §7）、
/// 贷款划扣比例（属 <c>LoanPolicy</c>）、贷款计时的推进（属 <c>LoanSettlement</c> / 结算编排）。
/// </para>
/// </remarks>
public static class InterestPolicy
{
    /// <summary>利率区间下限（0.5%）。</summary>
    private const decimal MinRate = 0.005m;

    /// <summary>利率区间上限（2.4%）。</summary>
    private const decimal MaxRate = 0.024m;

    /// <summary>储蓄利率区间（下限含）。1 月 roll，当年不变（§5.4；R-11）。</summary>
    public static readonly (decimal Min, decimal Max) SavingsRate = (MinRate, MaxRate);

    /// <summary>贷款利率区间（下限含）。每满 <see cref="InterestPeriodMonths"/> 个月按**当时本金** roll 一次（§5.4）。</summary>
    public static readonly (decimal Min, decimal Max) LoanRate = (MinRate, MaxRate);

    /// <summary>贷款计息周期（自然月）：达此月数即计息并把计时归零（§5.4；E-07）。</summary>
    public const int InterestPeriodMonths = 12;
}
