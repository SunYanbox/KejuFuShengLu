using KFL.Core.Enums;

namespace KFL.Rules.Achievements;

/// <summary>
/// 一次判定的结果（契约二十二 §2 条款 3；<c>data-model.md</c> §3.7）。
/// </summary>
/// <remarks>
/// <para>
/// <b>两个集合都是本次判定的增量</b>：<see cref="NewlyUnlocked"/> 是<b>本次新写入 ⑦⑧ 的</b>键，
/// <see cref="AlreadyCompleted"/> 是「判据成立、但 ⑦ 里已有同键」的那些——后者是<b>幂等的证据</b>，
/// 不是失败。二者都按 <see cref="AchievementId"/> 升序，便于逐字段对比（SC-003 的可 diff 断言）。
/// </para>
/// <para>
/// <b>绝嗣是另一条判分线</b>：<see cref="ExtinctionJustTriggered"/> 由
/// <see cref="ExtinctionJudge.TryTrigger"/> 在 68 条之后补判，且只在「本次真的把家族判定为绝嗣」
/// 时为真（已结束的状态面走冻结分支，恒为假）。
/// </para>
/// </remarks>
public sealed record AchievementOutcome
{
    /// <summary>本次新达成的成就（按 <see cref="AchievementId"/> 升序）。</summary>
    public required IReadOnlyList<AchievementId> NewlyUnlocked { get; init; }

    /// <summary>本次判据成立、但此前已达成过的成就（按 <see cref="AchievementId"/> 升序）。</summary>
    public required IReadOnlyList<AchievementId> AlreadyCompleted { get; init; }

    /// <summary>本次是否刚刚触发绝嗣结局。</summary>
    public bool ExtinctionJustTriggered { get; init; }

    /// <summary>空结果（冻结分支与「无新增」路径共用）。</summary>
    public static AchievementOutcome Empty { get; } = new()
    {
        NewlyUnlocked = [],
        AlreadyCompleted = [],
        ExtinctionJustTriggered = false,
    };
}
