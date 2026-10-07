using KFL.Core.Enums;

namespace KFL.Core.ValueObjects;

/// <summary>
/// 家族流水账的一条条目（规格书 §12.3；research R-04 的 E-08 二分类）。
/// </summary>
/// <remarks>
/// <para>
/// **不变量**：**资金类**条目（<see cref="LedgerCategoryMetadata.KindOf"/> 返回
/// <see cref="LedgerEntryKind.Treasury"/>）的 <see cref="Amount"/> MUST 非 0。
/// </para>
/// <para>
/// <see cref="Amount"/> 是**变动量**（正 = 流入、负 = 流出），MUST NOT 被解释成余额：
/// 余额的唯一真源是 <c>FamilyEconomy.Treasury</c>。
/// </para>
/// <para>
/// 条目**不存**「资金类 / 事件类」的第二个副本——种类由 <see cref="LedgerCategoryMetadata"/>
/// 唯一决定（data-model §1.3）。
/// </para>
/// </remarks>
public readonly record struct LedgerEntry
{
    /// <summary>构造并校验。</summary>
    /// <param name="date">归属年月。</param>
    /// <param name="personId">归属角色；<c>null</c> = **家族级**（铺面租、储蓄利息、工 bonus、田租）。</param>
    /// <param name="category">账本类别。</param>
    /// <param name="amount">金额：资金类 = 本次对资金池的变动额；事件类 = 该事件自身的金额语义。</param>
    /// <exception cref="ArgumentException"><paramref name="category"/> 为资金类而 <paramref name="amount"/> 为 0。</exception>
    public LedgerEntry(GameDate date, PersonId? personId, LedgerCategory category, Money amount)
    {
        if (LedgerCategoryMetadata.KindOf(category) == LedgerEntryKind.Treasury && amount == Money.Zero)
        {
            throw new ArgumentException(
                "资金类条目的 Amount MUST 非 0（contracts/ledger.md §4 不变量 4）。", nameof(amount));
        }

        Date = date;
        PersonId = personId;
        Category = category;
        Amount = amount;
    }

    /// <summary>归属年月。</summary>
    public GameDate Date { get; }

    /// <summary>
    /// 归属角色；<c>null</c> = **家族级**条目（铺面租、储蓄利息、工 bonus、田租——001 R-16 的三条理由：
    /// 这些收支无法归到某一个成员）。
    /// </summary>
    public PersonId? PersonId { get; }

    /// <summary>账本类别。</summary>
    public LedgerCategory Category { get; }

    /// <summary>金额（**变动量**，不是余额）。</summary>
    public Money Amount { get; }
}
