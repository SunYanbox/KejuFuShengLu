using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Rules.Economy;

/// <summary>
/// 一次「支付原语」的三段拆分（FR-017；data-model §4.3 的 <c>PaymentResult</c>）。
/// </summary>
/// <remarks>
/// **不变量**：三段皆 <c>&gt;= 0</c>，且 <see cref="FromCash"/> + <see cref="FromSavings"/>
/// + <see cref="ToLoan"/> 恰等于传入的支付额。本类型只是返回值，不承载任何规则数值。
/// </remarks>
/// <param name="FromCash">从**现金**付出的部分。</param>
/// <param name="FromSavings">从**储蓄**付出的部分（现金不足时才动用）。</param>
/// <param name="ToLoan">现金与储蓄都不足时的缺口——**转为贷款本金**（不扣除、不落资金条目）。</param>
public readonly record struct PaymentResult(Money FromCash, Money FromSavings, Money ToLoan)
{
    /// <summary>零支付：三段全为 0。</summary>
    public static PaymentResult None => new(Money.Zero, Money.Zero, Money.Zero);

    /// <summary>本次从**资金池**（现金 + 储蓄）实际付出的部分。</summary>
    public Money FromTreasury => FromCash + FromSavings;

    /// <summary>本次支付的总金额（= 池内付出 + 转贷款）。</summary>
    public Money Total => FromCash + FromSavings + ToLoan;
}

/// <summary>
/// 支付原语（FR-017；data-model §4.3）：**现金 → 储蓄 → 余额转贷款**三步。
/// </summary>
/// <remarks>
/// <para>
/// **唯一用途是「付不起的支出」**：规格书 §5.4 规定「不存在主动借贷」，贷款只能由付不起的支出
/// 产生，而这类支出（罚金）属阶段⑥。故本阶段本类**没有产品调用方**，只有单测直接验证三步语义
/// （`LoanTests`）；它同时是 <c>Loan.Principal</c> 在本阶段**唯一**的产生路径（data-model §3.2 的注）。
/// </para>
/// <para>
/// **资产买卖与生活费 MUST NOT 走这条路径**：前者按 `AssetMarket` 买卖，后者按 E-06 做部分支付
/// 且缺口**不入账、不转贷款**（契约三 §5）。两者都不允许凭空产生负债。
/// </para>
/// <para>
/// **与 `FamilyEconomy.Apply` 的关系**：三个资金池对 `KFL.Core` 之外不可写，「只动资金池而不落条目」
/// 在类型层面不可表达（T017 不变量 2）。因此池内付出**必须**经
/// <see cref="FamilyEconomy.Apply"/> 落一条资金类条目，类别由调用方给出——它就是「这笔钱付给了谁」
/// 的账本口径，阶段⑥ 将传入罚金类别（本阶段的单测用既有的支出类类别作替身）。
/// 缺口转贷款不改资金池，故**不落**资金类条目（与 <c>LoanInterestAccrued</c> 同属负债变动）。
/// </para>
/// </remarks>
public static class PaymentPrimitive
{
    /// <summary>
    /// 按「现金 → 储蓄 → 余额转贷款」付出 <paramref name="amount"/>。
    /// </summary>
    /// <remarks>
    /// 金额为 0 时**无任何副作用**、也不落条目（资金类条目的金额 MUST 非 0）。
    /// </remarks>
    /// <param name="economy">家族经济聚合（池内付出经 <see cref="FamilyEconomy.Apply"/> 写入并留痕）。</param>
    /// <param name="category">本次支付的账本类别（阶段⑥ 传入罚金类别）。</param>
    /// <param name="date">归属年月。</param>
    /// <param name="amount">支付额，MUST <c>&gt;= 0</c>。</param>
    /// <returns>三段的拆分结果。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="economy"/> 为 <c>null</c>。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> 为负。</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="category"/> 恒为负的支出类之外收到正额入账目标时（由 <see cref="FamilyEconomy.Apply"/> 抛出）。
    /// </exception>
    public static PaymentResult Pay(
        FamilyEconomy economy, LedgerCategory category, GameDate date, Money amount)
    {
        ArgumentNullException.ThrowIfNull(economy);

        if (amount.IsNegative)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount), amount, "支付额 MUST >= 0（FR-017；退款不是支付原语的语义）。");
        }

        if (amount == Money.Zero)
        {
            return PaymentResult.None;
        }

        // 第一步：现金；第二步：储蓄；第三步：仍不足的缺口转贷款本金。
        var fromCash = amount < economy.Treasury.Cash ? amount : economy.Treasury.Cash;
        var rest = amount - fromCash;
        var fromSavings = rest < economy.Treasury.Savings ? rest : economy.Treasury.Savings;
        var toLoan = rest - fromSavings;

        var fromTreasury = fromCash + fromSavings;

        if (fromTreasury.IsPositive)
        {
            // 池内付出必落条目（FR-021）；Apply 内部即按「现金 → 储蓄」扣付。
            economy.Apply(category, null, -fromTreasury, date);
        }

        if (toLoan.IsPositive)
        {
            // 缺口转为负债：principal 是唯一写入点，且由 Loan 自身校验 >= 0。
            economy.Treasury.Loan.Principal += toLoan;
        }

        return new PaymentResult(fromCash, fromSavings, toLoan);
    }
}
