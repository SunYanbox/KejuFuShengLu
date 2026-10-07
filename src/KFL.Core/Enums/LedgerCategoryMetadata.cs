namespace KFL.Core.Enums;

/// <summary>
/// <see cref="LedgerCategory"/> 的**结构事实**：类别 → 条目种类、类别 → 正额入账目标
/// （data-model §2.1；research R-04）。
/// </summary>
/// <remarks>
/// <para>
/// 归 <c>KFL.Core</c> 的理由：「哪些类别动钱」是**结构事实**而不是平衡数值（R-04），
/// 且 <c>KFL.Core</c> 的聚合（<c>FamilyEconomy.Apply</c>）必须能在同一层校验并执行自己追加的条目；
/// 放进 <c>KFL.Rules/Config</c> 会让 Core 无法校验自己的追加。
/// </para>
/// <para>
/// **SC-005 的求和口径** = 对 <see cref="KindOf"/> 返回 <see cref="LedgerEntryKind.Treasury"/>
/// 的条目求和（contracts/ledger.md §4）。
/// </para>
/// </remarks>
public static class LedgerCategoryMetadata
{
    /// <summary>取类别的条目种类（资金类 / 事件类）。</summary>
    /// <param name="category">账本类别。</param>
    /// <returns>该类别所属的条目种类。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="category"/> 不在 19 个已登记取值内。</exception>
    public static LedgerEntryKind KindOf(LedgerCategory category) => category switch
    {
        // 事件类：不引起资金池变动（饥馑四类阶段迁移 + 贷款计息入欠息）。
        LedgerCategory.LoanInterestAccrued => LedgerEntryKind.Event,
        LedgerCategory.FamineEntered => LedgerEntryKind.Event,
        LedgerCategory.FamineReliefEntered => LedgerEntryKind.Event,
        LedgerCategory.FamineSevereEntered => LedgerEntryKind.Event,
        LedgerCategory.FamineResolved => LedgerEntryKind.Event,

        // 资金类：其余 14 个类别全部引起资金池变动。
        LedgerCategory.LivingCost => LedgerEntryKind.Treasury,
        LedgerCategory.SelfFarmingIncome => LedgerEntryKind.Treasury,
        LedgerCategory.LandRentIncome => LedgerEntryKind.Treasury,
        LedgerCategory.FarmingWageIncome => LedgerEntryKind.Treasury,
        LedgerCategory.CraftingIncome => LedgerEntryKind.Treasury,
        LedgerCategory.TradeIncome => LedgerEntryKind.Treasury,
        LedgerCategory.OfficialSalary => LedgerEntryKind.Treasury,
        LedgerCategory.ShopRentIncome => LedgerEntryKind.Treasury,
        LedgerCategory.ArtisanBonus => LedgerEntryKind.Treasury,
        LedgerCategory.SavingsInterest => LedgerEntryKind.Treasury,
        LedgerCategory.LoanPrincipalRepaid => LedgerEntryKind.Treasury,
        LedgerCategory.LoanInterestRepaid => LedgerEntryKind.Treasury,
        LedgerCategory.AssetPurchase => LedgerEntryKind.Treasury,
        LedgerCategory.AssetSale => LedgerEntryKind.Treasury,

        _ => throw new ArgumentOutOfRangeException(
            nameof(category), category, "未登记的账本类别（data-model §2.1 共 19 个取值）。"),
    };

    /// <summary>取类别在**正额**入账时的目标池。</summary>
    /// <param name="category">账本类别。</param>
    /// <returns>
    /// 目标池；<c>null</c> = 该类别没有正额入账目标（恒为负的支出类，或事件类）。
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="category"/> 不在 19 个已登记取值内。</exception>
    public static LedgerCreditTarget? CreditTargetOf(LedgerCategory category) => category switch
    {
        // 正额入现金（9 类）。
        LedgerCategory.SelfFarmingIncome => LedgerCreditTarget.Cash,
        LedgerCategory.LandRentIncome => LedgerCreditTarget.Cash,
        LedgerCategory.FarmingWageIncome => LedgerCreditTarget.Cash,
        LedgerCategory.CraftingIncome => LedgerCreditTarget.Cash,
        LedgerCategory.TradeIncome => LedgerCreditTarget.Cash,
        LedgerCategory.OfficialSalary => LedgerCreditTarget.Cash,
        LedgerCategory.ShopRentIncome => LedgerCreditTarget.Cash,
        LedgerCategory.ArtisanBonus => LedgerCreditTarget.Cash,
        LedgerCategory.AssetSale => LedgerCreditTarget.Cash,

        // 正额入储蓄（1 类，§5.4：储蓄利息并入储蓄本金）。
        LedgerCategory.SavingsInterest => LedgerCreditTarget.Savings,

        // 恒为负的支出类（4 类）：没有正额入账目标。
        LedgerCategory.LivingCost => null,
        LedgerCategory.LoanPrincipalRepaid => null,
        LedgerCategory.LoanInterestRepaid => null,
        LedgerCategory.AssetPurchase => null,

        // 事件类（5 类）：金额是该事件自身的语义，不指示任何入账目标。
        LedgerCategory.LoanInterestAccrued => null,
        LedgerCategory.FamineEntered => null,
        LedgerCategory.FamineReliefEntered => null,
        LedgerCategory.FamineSevereEntered => null,
        LedgerCategory.FamineResolved => null,

        _ => throw new ArgumentOutOfRangeException(
            nameof(category), category, "未登记的账本类别（data-model §2.1 共 19 个取值）。"),
    };
}
