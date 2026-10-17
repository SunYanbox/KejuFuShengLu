namespace KFL.Core.ValueObjects;

/// <summary>
/// 托媒申请记录：某成员在某年月已提出过一次托媒（007 的 FR-013「同一成员同一月内至多一次」）。
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="PendingBirth"/> 同款：只承载「谁、何时」，不含任何成功率或结果。
/// 记录在**扣费成功后**追加——资金不足 / 无更低功名的拒绝**不**消耗当月名额。
/// </para>
/// <para>
/// 该记录是**持久字段**（FR-047 第 ⑥ 组）：托媒可以跨月反复尝试，若只存在内存里，
/// 玩家存档读档后即可在同一个月内二次托媒。
/// </para>
/// </remarks>
/// <param name="MemberId">提出托媒的我方成员。</param>
/// <param name="AppliedAt">提出托媒的年月。</param>
public readonly record struct MatchmakeApplication(PersonId MemberId, GameDate AppliedAt);
