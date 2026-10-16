using KFL.Core.Entities;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;

namespace KFL.Rules.Economy;

/// <summary>
/// **总资产**的单一访问器（规格书 §7.5；契约十六 §4.1；FR-011）。
/// </summary>
/// <remarks>
/// <para>
/// 口径：<c>现金 + 储蓄 + AssetPriceTable.MarketValue(田宅铺) − 贷款本金 − 欠息</c>，
/// **不含商本池**（§5.2 的 2026-10-06 裁决）。基数为负或 <c>0</c> ⇒ 一律按 <c>0</c> 处理。
/// </para>
/// <para>
/// 该口径的**已有消费者**是 12 月工出身 bonus 的「总资产为正」判定
/// （原先在 <c>MonthlySettlementEngine</c> 里就地算——那是第二真源，已改经本类）。
/// 本特性新增的消费者是比例罚金。
/// </para>
/// </remarks>
public static class TotalAssetsCalculator
{
    /// <summary>取一个家族经济体的总资产。</summary>
    /// <param name="economy">家族经济体。</param>
    /// <returns>总资产；为负或 <c>0</c> 时返回 <see cref="Money.Zero"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="economy"/> 为 <c>null</c>。</exception>
    public static Money Of(FamilyEconomy economy)
    {
        ArgumentNullException.ThrowIfNull(economy);

        return Of(economy.Treasury, economy.Holdings);
    }

    /// <summary>取「国库 + 资产」两段的总资产。</summary>
    /// <param name="treasury">国库（现金 / 储蓄 / 贷款）。</param>
    /// <param name="holdings">田宅铺。</param>
    /// <returns>总资产；为负或 <c>0</c> 时返回 <see cref="Money.Zero"/>。</returns>
    /// <exception cref="ArgumentNullException">任一参数为 <c>null</c>。</exception>
    public static Money Of(Treasury treasury, Holdings holdings)
    {
        ArgumentNullException.ThrowIfNull(treasury);
        ArgumentNullException.ThrowIfNull(holdings);

        Money total = treasury.Cash
            + treasury.Savings
            + AssetPriceTable.MarketValue(holdings)
            - treasury.Loan.Principal
            - treasury.Loan.AccruedInterest;

        return total.IsPositive ? total : Money.Zero;
    }
}
