using System.Collections.ObjectModel;
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
/// 本类型当前只交付**存储与追加入口**：条目按追加顺序（年月非降序，由「一次结算只处理当月」
/// 的调用次序保证）。四个聚合查询（<c>TreasuryDeltaIn</c> / <c>TotalsByCategory</c> /
/// <c>TotalsByPerson</c> / <c>MonthlyByPerson</c>）属 US5，本阶段 MUST NOT 预先写入。
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
            if (entry.Date >= from && entry.Date <= to)
            {
                result.Add(entry);
            }
        }

        return result;
    }
}
