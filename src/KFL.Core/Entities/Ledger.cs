using System.Collections.ObjectModel;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Core.Entities;

/// <summary>
/// 家族级**追加式**流水账（规格书 §12.3；data-model §3.4；contracts/ledger.md）。
/// </summary>
/// <remarks>
/// <para>
/// **不变量**：MUST NOT 存储任何月度汇总值——聚合每次从条目算（FR-021、SC-008）。
/// 「按角色」与「家族级」两个维度是**同一批条目的两个查询方向**（001 R-16、spec US5 AS4）。
/// </para>
/// <para>
/// **存储与追加入口**：条目按追加顺序（年月非降序，由「一次结算只处理当月」的调用次序保证）。
/// **四个聚合查询**（US5，data-model §3.4）：<see cref="TreasuryDeltaIn"/>（SC-005 的等式左侧）、
/// <see cref="TotalsByCategory"/>（收益来源 / 支出明细）、<see cref="TotalsByPerson"/> 与
/// <see cref="MonthlyByPerson"/>（角色维度的累计与逐月）。
/// </para>
/// <para>
/// **实现方式如实记录**：全部为**线性扫描**——本阶段不建索引、不分块（R-16、契约四 §6）。
/// 「只按角色加起来」与「只按家族加起来」是同一批条目的两个查询方向，不是两份汇总存储。
/// </para>
/// </remarks>
public sealed class Ledger
{
    private readonly List<LedgerEntry> _entries = [];
    private readonly ReadOnlyCollection<LedgerEntry> _entriesView;

    /// <summary>构造一册空账。</summary>
    public Ledger() => _entriesView = _entries.AsReadOnly();

    /// <summary>
    /// 全部条目，只读视图，按追加顺序（年月非降序）。**无公开写入通道**：
    /// 追加的唯一入口是 <see cref="Append"/>，既有条目 MUST NOT 被改写或删除（FR-021）。
    /// </summary>
    public IReadOnlyList<LedgerEntry> Entries => _entriesView;

    /// <summary>
    /// **唯一**的追加入口（contracts/ledger.md §3：仅供 <c>FamilyEconomy</c> 的四个资金流动方法使用）。
    /// </summary>
    /// <remarks>
    /// 单独调用本方法会造成「条目与资金池不一致」——资金流动 MUST 经聚合（§3 条款 2）。
    /// </remarks>
    /// <param name="entry">一条条目（其自身的资金类非 0 不变量已在构造时校验）。</param>
    public void Append(LedgerEntry entry) => _entries.Add(entry);

    /// <summary>取**闭区间** <paramref name="from"/> ~ <paramref name="to"/> 内的条目（按追加顺序）。</summary>
    /// <param name="from">区间起点（含）。</param>
    /// <param name="to">区间终点（含）。</param>
    /// <returns>区间内的条目；区间为空时返回空列表。</returns>
    public IReadOnlyList<LedgerEntry> EntriesIn(GameDate from, GameDate to)
    {
        var result = new List<LedgerEntry>();

        foreach (var entry in _entries)
        {
            if (InRange(entry, from, to))
            {
                result.Add(entry);
            }
        }

        return result;
    }

    /// <summary>
    /// 闭区间内的**资金类**条目之和（SC-005 的等式左侧；事件类条目不参与）。
    /// </summary>
    /// <remarks>
    /// 「资金池期末 − 期初 = 本方法」就是 FR-021 的可断言形式：不存在「只动资金池而不落条目」的路径。
    /// </remarks>
    /// <param name="from">区间起点（含）。</param>
    /// <param name="to">区间终点（含）。</param>
    /// <returns>资金类条目之和。</returns>
    public Money TreasuryDeltaIn(GameDate from, GameDate to)
    {
        var delta = Money.Zero;

        foreach (var entry in _entries)
        {
            if (InRange(entry, from, to) && IsTreasury(entry))
            {
                delta += entry.Amount;
            }
        }

        return delta;
    }

    /// <summary>
    /// 闭区间内的**逐类别**合计（收益来源明细 / 支出明细；事件类条目不参与）。
    /// </summary>
    /// <param name="from">区间起点（含）。</param>
    /// <param name="to">区间终点（含）。</param>
    /// <returns>按**首次出现顺序**给出的类别合计；区间内无该类条目时不出现。</returns>
    public IReadOnlyList<(LedgerCategory Category, Money Total)> TotalsByCategory(GameDate from, GameDate to)
    {
        var totals = new List<(LedgerCategory Category, Money Total)>();
        var positions = new Dictionary<LedgerCategory, int>();

        foreach (var entry in _entries)
        {
            if (!InRange(entry, from, to) || !IsTreasury(entry))
            {
                continue;
            }

            if (positions.TryGetValue(entry.Category, out var position))
            {
                totals[position] = (entry.Category, totals[position].Total + entry.Amount);
            }
            else
            {
                positions.Add(entry.Category, totals.Count);
                totals.Add((entry.Category, entry.Amount));
            }
        }

        return totals;
    }

    /// <summary>
    /// 单个成员在闭区间内的累计收支（**含已归档成员**——条目不随成员归档而消失）。
    /// </summary>
    /// <remarks>
    /// 只累加 <see cref="LedgerEntry.PersonId"/> **恰为该成员**的资金类条目：家族级条目
    /// （<c>PersonId == null</c>，如铺面租、储蓄利息、工 bonus、田租）MUST NOT 被算进任何成员头上
    /// （US5 AS2；E-18 注：经商例外，它归属被采用的那名成员）。
    /// </remarks>
    /// <param name="personId">成员标识。</param>
    /// <param name="from">区间起点（含）。</param>
    /// <param name="to">区间终点（含）。</param>
    /// <returns>该成员的累计收支。</returns>
    public Money TotalsByPerson(PersonId personId, GameDate from, GameDate to)
    {
        var total = Money.Zero;

        foreach (var entry in _entries)
        {
            if (InRange(entry, from, to) && IsTreasury(entry) && entry.PersonId == personId)
            {
                total += entry.Amount;
            }
        }

        return total;
    }

    /// <summary>
    /// 单个成员在闭区间内的**逐月**收支（按年月升序；无条目的月份不出现）。
    /// </summary>
    /// <param name="personId">成员标识。</param>
    /// <param name="from">区间起点（含）。</param>
    /// <param name="to">区间终点（含）。</param>
    /// <returns>逐月合计。</returns>
    public IReadOnlyList<(GameDate Month, Money Total)> MonthlyByPerson(
        PersonId personId, GameDate from, GameDate to)
    {
        var totals = new List<(GameDate Month, Money Total)>();
        var positions = new Dictionary<GameDate, int>();

        foreach (var entry in _entries)
        {
            if (!InRange(entry, from, to) || !IsTreasury(entry) || entry.PersonId != personId)
            {
                continue;
            }

            if (positions.TryGetValue(entry.Date, out var position))
            {
                totals[position] = (entry.Date, totals[position].Total + entry.Amount);
            }
            else
            {
                positions.Add(entry.Date, totals.Count);
                totals.Add((entry.Date, entry.Amount));
            }
        }

        return totals;
    }

    private static bool InRange(LedgerEntry entry, GameDate from, GameDate to) =>
        entry.Date >= from && entry.Date <= to;

    private static bool IsTreasury(LedgerEntry entry) =>
        LedgerCategoryMetadata.KindOf(entry.Category) == LedgerEntryKind.Treasury;
}
