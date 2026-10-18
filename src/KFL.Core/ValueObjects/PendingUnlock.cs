using KFL.Core.Enums;

namespace KFL.Core.ValueObjects;

/// <summary>
/// 档内「待落盘解锁队列」的元素（FR-013 ⑧；FR-024 的重试依据；data-model §2.2）。
/// </summary>
/// <remarks>
/// <para>
/// 比 <see cref="AchievementMark"/> 多 <see cref="SourceSaveId"/>——FR-013 ⑧ 逐字要求
/// 全局记录「含解锁年月与**来源 UUID**」。
/// </para>
/// <para>
/// **不变量**：每个 <see cref="PendingUnlock"/> 的 `(Achievement, Difficulty)` MUST 也能在
/// <c>AchievementState.Completed</c> 里找到（契约二十三 §7 第 2 条）；类型层由
/// <c>AchievementState.MarkCompletedAndEnqueue</c> 是**唯一**同时写 ⑦ 与 ⑧ 的入口来保证。
/// </para>
/// </remarks>
/// <param name="Achievement">成就标识。</param>
/// <param name="Difficulty">解锁时的难度（参与键）。</param>
/// <param name="UnlockedAt">解锁年月。</param>
/// <param name="SourceSaveId">来源存档标识（写入全局记录用）。</param>
public readonly record struct PendingUnlock(
    AchievementId Achievement,
    Difficulty Difficulty,
    GameDate UnlockedAt,
    Guid SourceSaveId);
