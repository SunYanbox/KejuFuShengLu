using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;

namespace KFL.Rules.Settlement;

/// <summary>
/// 贷款结算的**纯函数**（规格书 §5.4；research R-08；契约三 §7）——计息节点判定、利率 roll 与划扣额。
/// </summary>
/// <remarks>
/// <para>
/// **不碰任何状态**：本类只读 <see cref="Loan"/> 与 <see cref="Money"/>/<see cref="Origin"/> 输入，
/// 写出脏状态（计息入欠息、划扣、计时推进）全部由编排 <c>MonthlySettlementEngine</c> 经
/// <c>FamilyEconomy</c> 的写入通道执行（FR-021）。
/// </para>
/// <para>
/// **随机消费**：只有 <see cref="RollRate"/> 会消费 1 次 <see cref="IRandomService.NextDouble"/>，
/// 且调用点由**计息节点是否命中**决定；节点是否命中只由 <see cref="Loan.MonthsSinceInterest"/>
/// 决定，MUST NOT 依赖任何随机结果或当月净利润（契约三 §4 条款 2、E-07）。
/// </para>
/// </remarks>
public static class LoanSettlement
{
    /// <summary>
    /// 本月是否命中计息节点：<see cref="Loan.MonthsSinceInterest"/> 达
    /// <see cref="InterestPolicy.InterestPeriodMonths"/>（**只看计数**，E-07）。
    /// </summary>
    /// <param name="loan">贷款。</param>
    /// <returns>命中为 <c>true</c>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="loan"/> 为 <c>null</c>。</exception>
    public static bool IsInterestDue(Loan loan)
    {
        ArgumentNullException.ThrowIfNull(loan);

        return loan.MonthsSinceInterest >= InterestPolicy.InterestPeriodMonths;
    }

    /// <summary>roll 一次贷款利率（区间 <c>0.5%~2.4%</c>，消费 1 次 <see cref="IRandomService.NextDouble"/>）。</summary>
    /// <param name="randomService">随机来源接缝。</param>
    /// <returns>本次计息用的利率。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="randomService"/> 为 <c>null</c>。</exception>
    public static decimal RollRate(IRandomService randomService)
    {
        ArgumentNullException.ThrowIfNull(randomService);

        return InterestPolicy.RateFor(randomService.NextDouble());
    }

    /// <summary>
    /// 本月划扣额 = <c>min(净利润 × 比例, 本金 + 欠息)</c>（§5.4；R-08 第 4 条）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// **必须封顶**：不封顶会在债务只剩一小截时划走全部应划额并产生规格书没有的「退款」资金流。
    /// </para>
    /// <para>
    /// 净利润 ≤ 0 → 返回 <see cref="Money.Zero"/>：**不划扣、不产生罚则、不重置也不跳过计息计时**
    /// （E-07；计时推进在编排里，与本方法无关）。
    /// </para>
    /// </remarks>
    /// <param name="netProfit">本月净利润（收入合计 − 生活费实付）。</param>
    /// <param name="hasShiStatus">家族是否具「仕」身份。</param>
    /// <param name="origin">家族出身。</param>
    /// <param name="loan">贷款（只读，用于取债务总额封顶）。</param>
    /// <returns>本月划扣额（可能为 0）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="loan"/> 为 <c>null</c>。</exception>
    public static Money ComputeRepayment(Money netProfit, bool hasShiStatus, Origin origin, Loan loan)
    {
        ArgumentNullException.ThrowIfNull(loan);

        if (!netProfit.IsPositive)
        {
            return Money.Zero;
        }

        var total = loan.Total;

        if (!total.IsPositive)
        {
            // 已结清：MUST NOT 对已结清贷款继续划扣（契约三 §7）。
            return Money.Zero;
        }

        var wanted = netProfit * LoanPolicy.RepaymentRatio(hasShiStatus, origin);

        return wanted < total ? wanted : total;
    }
}
