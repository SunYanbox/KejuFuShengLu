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
/// </remarks>
/// <param name="Settlement">本回合的结算结果。</param>
/// <param name="CompletedTurns">截至本回合结束时的已完成回合数。</param>
/// <param name="Saved">本回合是否写入当前档。</param>
/// <param name="BackedUp">本回合是否做了备份。</param>
public readonly record struct TurnOutcome(
    SettlementResult Settlement,
    int CompletedTurns,
    bool Saved,
    bool BackedUp);
