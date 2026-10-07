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

    /// <summary>
    /// 把 <c>[0, 1]</c> 的随机取值映射为区间内的利率：<c>利率 = 下限 + r × (上限 − 下限)</c>
    /// （契约三 §4 的映射表）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// **US2 与 US3 共用本方法**：贷款计息（<c>LoanSettlement.RollRate</c>）与当年储蓄利率
    /// （<c>SavingsSettlement.RollRate</c>）的映射公式相同，双方 MUST NOT 各自复制一份
    /// （tasks T043/T047；SC-008 要求同一个数不出第二个出处）。
    /// </para>
    /// <para>
    /// **越界输入夹到端点**：接缝契约已保证 <c>NextDouble() ∈ [0.0, 1.0)</c>，此处不做抛异常式校验，
    /// 而是把区间外的取值夹住——这样「取 0 与极大值」的边界断言在 <c>double.MaxValue</c> 这类
    /// 极端输入上依然落在区间内（否则 <c>(decimal)</c> 换算会抛 <see cref="OverflowException"/>）。
    /// </para>
    /// </remarks>
    /// <param name="r">随机取值。</param>
    /// <returns>区间内的利率。</returns>
    public static decimal RateFor(double r)
    {
        var bounded = double.IsNaN(r) || r < 0d ? 0d : r > 1d ? 1d : r;

        return MinRate + ((decimal)bounded * (MaxRate - MinRate));
    }
}
