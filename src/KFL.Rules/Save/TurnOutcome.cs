using KFL.Infrastructure.Achievements;
using KFL.Rules.Settlement;

namespace KFL.Rules.Save;

/// <summary>
/// 一回合推进的结果（004-save-system；契约十一 §2）——结算结果与存档动作的**增量快照**。
/// </summary>
/// <remarks>
/// <para>
/// 它是返回值，不是状态：MUST NOT 写回 <c>GameState</c>，也不被任何一层持有
/// （data-model §3 的「不变量 1」同款口径——瞬时物件只活在一次调用栈上）。
/// </para>
/// <para>
/// <see cref="Saved"/> 与 <see cref="BackedUp"/> 让调用方（UI 轨 U1/U2、测试）能区分
/// 「结算了但没存」「存了但没备份」这两种中间状态；<c>Q6</c> 明确「本回合推进了却没落盘」
/// 是**允许**的中间态，故这里如实报告，而不是用异常或魔法值表达。
/// </para>
/// <para>
/// <b>③ 与 ⑤ 的刻意对比</b>（008-achievements-extinction；契约二十三「条款 22」）：
/// ③（<c>ISaveService.Save</c>，见 <see cref="Saved"/>）落的是**用户数据**，失败**上抛**且
/// 不回滚内存状态——调用方必须知道；⑤（<c>IAchievementStore.Flush</c>，见
/// <see cref="AchievementFlush"/>）落的是**元进度**，失败**不上抛**，待落盘队列留在档内等
/// 下一次回合重试（FR-024 明令 MUST NOT 阻断回合推进与存档）。两者语义相反是**有意的**，
/// 不是不一致。
/// </para>
/// </remarks>
/// <param name="Settlement">本回合的结算结果。</param>
/// <param name="CompletedTurns">截至本回合结束时的已完成回合数。</param>
/// <param name="Saved">本回合是否写入当前档。</param>
/// <param name="BackedUp">本回合是否做了备份。</param>
public readonly record struct TurnOutcome(
    SettlementResult Settlement,
    int CompletedTurns,
    bool Saved,
    bool BackedUp)
{
    /// <summary>
    /// ⑤ 全局成就落盘的结果；未配置成就存储（或本次队列为空）时为 <c>null</c>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 做成**可选 init 属性**（而非第 5 个位置参数）是刻意的：既有构造点分布在
    /// <c>TurnRunner</c> 与多处测试夹具里，位置参数一旦增加就全部要改；可选属性让
    /// 「未配置成就存储」这一既有语义天然映射为 <c>null</c>（契约二十三「条款 20」）。
    /// </para>
    /// <para>
    /// 失败**不是**异常：<see cref="AchievementFlushResult.IsSuccess"/> 为 <c>false</c> 时
    /// 本回合照样算推进完成（③ 与 ⑤ 互不牵连）。
    /// </para>
    /// </remarks>
    public AchievementFlushResult? AchievementFlush { get; init; }
}

