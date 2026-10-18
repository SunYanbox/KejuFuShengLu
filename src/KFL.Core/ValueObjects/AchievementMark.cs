using KFL.Core.Enums;

namespace KFL.Core.ValueObjects;

/// <summary>
/// 档内「本档已完成集合」的元素，也是全局成就文件的合并键（FR-013 ⑦；data-model §2.2）。
/// </summary>
/// <remarks>
/// <para>
/// **键是 `(Achievement, Difficulty)`**：规格书 §14:538 要求「每条成就的各难度**独立**标记完成状态」，
/// 故 `<see cref="Difficulty"/>` 参与键，同一成就的不同难度是两条记录。
/// </para>
/// <para>
/// **最早日期语义（FR-018）不在本类型里**：合并（全局文件）取 `min(CompletedAt)`；
/// 本类型只承载「本档首次成立的年月」。
/// </para>
/// </remarks>
/// <param name="Achievement">成就标识。</param>
/// <param name="Difficulty">成立时的难度（参与键）。</param>
/// <param name="CompletedAt">本档内首次成立的年月。</param>
public readonly record struct AchievementMark(
    AchievementId Achievement,
    Difficulty Difficulty,
    GameDate CompletedAt);
