using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;

namespace KFL.Rules.Economy;

/// <summary>
/// 资产买卖的规则入口（FR-027；规格书 §5.3；research R-15）：田、农村宅、城市宅、铺面的购入与售出。
/// </summary>
/// <remarks>
/// <para>
/// **购售同价**：单价一律取自 <see cref="AssetPriceTable.UnitPrice"/>（唯一出处，SC-008），
/// 买入 = −数量 × 单价、售出 = +数量 × 单价，两条**资金类**条目
/// （<see cref="LedgerCategory.AssetPurchase"/> / <see cref="LedgerCategory.AssetSale"/>）。
/// </para>
/// <para>
/// **资金不足即拒绝，MUST NOT 转贷款**（§9.5/§5.4「不存在主动借贷」；R-15 的备选方案已被否决）。
/// 此处「资金不足」按 R-05 的可付额口径读作「现金 + 储蓄不够」——买入的池内付出经
/// <see cref="FamilyEconomy.Apply"/> 按「现金 → 储蓄」扣付，故拒绝线就是它的能力边界，
/// 不额外多一条无解释的限制；缺口**不入账、不转贷款**。
/// </para>
/// <para>
/// **界面入口属阶段③**：本阶段只交付规则入口，使资产池可变化并进入结算（FR-027）。
/// 资产买卖**MUST NOT** 影响本月净利润（净利润 = 收入 − 生活费，契约三 §7），故它不参与结算的六步。
/// </para>
/// </remarks>
public static class AssetMarket
{
    /// <summary>
    /// 购入 <paramref name="count"/> 个指定资产：扣付资金池并增持，落一条 <see cref="LedgerCategory.AssetPurchase"/> 条目。
    /// </summary>
    /// <param name="state">存档级状态（归属年月取它的当前年月，资产与资金都挂在它的聚合下）。</param>
    /// <param name="kind">资产种类。</param>
    /// <param name="count">购入数量，MUST <c>&gt;= 1</c>。</param>
    /// <returns>本次支付的总额（= 数量 × 单价）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 <c>null</c>。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> 小于 1，或 <paramref name="kind"/> 未登记。</exception>
    /// <exception cref="InvalidOperationException">现金 + 储蓄不足以支付（MUST NOT 转贷款）。</exception>
    public static Money Buy(GameState state, AssetKind kind, int count)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count), count, "购入数量 MUST >= 1（金额为 0 的买卖不是一次资金流动）。");
        }

        var total = AssetPriceTable.UnitPrice(kind) * count;
        var payable = state.Economy.Treasury.Cash + state.Economy.Treasury.Savings;

        if (total > payable)
        {
            throw new InvalidOperationException(
                $"现金 + 储蓄不足以支付 {total.Guan} 贯的资产购入（仅有 {payable.Guan} 贯）；"
                + "资产买卖 MUST NOT 转贷款（§9.5/§5.4 的同一口径）。");
        }

        // 先动钱（落条目 + 扣付），再改持有量：两者的校验都已在上方完成，故不存在半提交状态。
        state.Economy.Apply(LedgerCategory.AssetPurchase, null, -total, state.CurrentDate);
        state.Economy.Holdings.Add(kind, count);

        return total;
    }

    /// <summary>
    /// 售出 <paramref name="count"/> 个指定资产：减持并按**同一单价**进账，落一条 <see cref="LedgerCategory.AssetSale"/> 条目。
    /// </summary>
    /// <param name="state">存档级状态（归属年月取它的当前年月）。</param>
    /// <param name="kind">资产种类。</param>
    /// <param name="count">售出数量，MUST <c>&gt;= 1</c> 且不超过当前持有量。</param>
    /// <returns>本次进账的总额（= 数量 × 单价，与买入同价）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 <c>null</c>。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> 小于 1，或 <paramref name="kind"/> 未登记。</exception>
    /// <exception cref="InvalidOperationException"><paramref name="count"/> 超过当前持有量（数量 MUST <c>&gt;= 0</c>）。</exception>
    public static Money Sell(GameState state, AssetKind kind, int count)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count), count, "售出数量 MUST >= 1（金额为 0 的买卖不是一次资金流动）。");
        }

        var total = AssetPriceTable.UnitPrice(kind) * count;

        // 先校验并减持，再进账：持有量不足时 MUST NOT 有任何资金流入。
        state.Economy.Holdings.Remove(kind, count);
        state.Economy.Apply(LedgerCategory.AssetSale, null, total, state.CurrentDate);

        return total;
    }
}
