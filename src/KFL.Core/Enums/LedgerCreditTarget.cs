namespace KFL.Core.Enums;

/// <summary>
/// 资金类条目在**正额**入账时的目标池（规格书 §5.4：储蓄利息并入**储蓄本金**）。
/// </summary>
/// <remarks>
/// 负额不入本枚举表达的目标池：恒为负的类别没有入账目标（见 <c>LedgerCategoryMetadata.CreditTargetOf</c>
/// 的 <c>null</c>），扣付口径固定为「现金 → 储蓄」（契约四 §3）。
/// </remarks>
public enum LedgerCreditTarget
{
    /// <summary>现金。</summary>
    Cash,

    /// <summary>储蓄。</summary>
    Savings,
}
