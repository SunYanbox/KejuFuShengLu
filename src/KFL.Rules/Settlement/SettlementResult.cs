using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Career;

namespace KFL.Rules.Settlement;

/// <summary>逐年龄档的生活费明细（US1 AS1；data-model §4.2 的 <c>LivingCosts</c>）。</summary>
/// <param name="AgeBracket">年龄档。</param>
/// <param name="DailyCost">该档每名计口成员的**日耗**（文/日）。</param>
/// <param name="MemberCount">该档计口成员人数。</param>
/// <param name="Subtotal">该档在当月**应付额**中的贡献（小计）。</param>
public sealed record LivingCostLine(
    AgeBracket AgeBracket,
    decimal DailyCost,
    int MemberCount,
    Money Subtotal);

/// <summary>一条收入分项（契约三 §6；data-model §4.2 的 <c>Incomes</c>）。</summary>
/// <param name="Category">收入来源对应的账本类别。</param>
/// <param name="PersonId">归属成员；<c>null</c> = **家族级**（铺面租、储蓄利息、工 bonus、田租）。</param>
/// <param name="Amount">金额。</param>
public sealed record IncomeLine(
    LedgerCategory Category,
    PersonId? PersonId,
    Money Amount);

/// <summary>
/// 一次结算的可断言快照（data-model §4.2）。**纯数据容器，零逻辑**。
/// </summary>
/// <remarks>
/// <para>
/// **不变量**（由产出方 <c>MonthlySettlementEngine.Settle</c> 保证）：
/// ① <see cref="Entries"/> MUST 与本次结算向 <c>Ledger</c> 追加的条目**逐条相同**；
/// ② <see cref="TreasuryPoolAfter"/> − <see cref="TreasuryPoolBefore"/> MUST 等于
/// <see cref="Entries"/> 中**资金类**条目（<c>LedgerCategoryMetadata.KindOf</c> ==
/// <see cref="LedgerEntryKind.Treasury"/>）之和——**这条不变量就是 SC-005**，在任何一次结算上都必须成立。
/// </para>
/// <para>
/// **MUST NOT** 被写回 <c>GameState</c>：资金池是唯一余额真源（FR-021、contracts/ledger.md §1）。
/// </para>
/// <para>
/// **明确不含**：任何计算逻辑、任何汇总缓存、任何对 <c>GameState</c> 的引用。
/// </para>
/// </remarks>
public sealed record SettlementResult
{
    /// <summary>
    /// 本次结算的月份（归属月）。结算末尾已推进时间，故 <c>GameState.CurrentDate</c> 此时已是**下月**。
    /// </summary>
    public required GameDate Month { get; init; }

    /// <summary>米价系数在结算第②步**游走前**的取值。</summary>
    public required decimal GrainPriceIndexBefore { get; init; }

    /// <summary>米价系数在结算第②步**游走后、clamp 后**的取值（当月生效）。</summary>
    public required decimal GrainPriceIndexAfter { get; init; }

    /// <summary>逐年龄档的生活费明细（日耗、人数与小计）。</summary>
    public required IReadOnlyList<LivingCostLine> LivingCosts { get; init; }

    /// <summary>当月**应付**生活费（含救济折扣，E-06）。</summary>
    public required Money LivingCostPayable { get; init; }

    /// <summary>当月**实际扣付**的生活费（资金池不足时为部分支付额，E-06）。</summary>
    public required Money LivingCostPaid { get; init; }

    /// <summary>各来源分项：账本类别 + 归属成员（可空）+ 金额。</summary>
    public required IReadOnlyList<IncomeLine> Incomes { get; init; }

    /// <summary>净利润 = 收入合计 − 生活费**实付**额（E-04）；不含划扣本身、不含资产买卖现金流。</summary>
    public required Money NetProfit { get; init; }

    /// <summary>本月计息额（借贷方欠息的增长；仅命中 12 月节点时非 0）。</summary>
    public required Money LoanInterestAccrued { get; init; }

    /// <summary>本月划扣的本金 / 欠息拆分（先本后息；未划扣时两段皆为 0）。</summary>
    public required LoanRepayment LoanRepayment { get; init; }

    /// <summary>储蓄利息（只可能出现在 12 月；正额入储蓄本金，§5.4）。</summary>
    public required Money SavingsInterest { get; init; }

    /// <summary>工出身 bonus（只可能出现在 12 月，且需工出身、总资产为正；§5.2）。</summary>
    public required Money ArtisanBonus { get; init; }

    /// <summary>结算第④步**之前**的饥馑状态快照。</summary>
    public required FamineState FamineBefore { get; init; }

    /// <summary>结算第④步**之后**的饥馑状态快照。</summary>
    public required FamineState FamineAfter { get; init; }

    /// <summary>本次结算产生的**全部**条目（含事件类），与向 <c>Ledger</c> 追加的条目逐条相同。</summary>
    public required IReadOnlyList<LedgerEntry> Entries { get; init; }

    /// <summary>本次结算**开始前**的资金池（现金 + 储蓄 + 商本，R-05）。</summary>
    public required Money TreasuryPoolBefore { get; init; }

    /// <summary>本次结算**结束后**的资金池，供 SC-005 直接断言。</summary>
    public required Money TreasuryPoolAfter { get; init; }

    /// <summary>
    /// 本次结算**第③步（官吏推进）**的增量快照（授官 / 政绩 / 晋升 / 致仕 / 禁升跳过 / 服刑暂停 / 三态）。
    /// 它是增量、不是第二真源（契约七 §9）。
    /// </summary>
    public required CareerAdvanceResult Career { get; init; }
}
