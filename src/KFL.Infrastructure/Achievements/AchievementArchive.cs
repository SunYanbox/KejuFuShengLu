using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Infrastructure.Achievements;

/// <summary>
/// 全局成就档案的**内存形状**（008-achievements-extinction；契约二十三「条款 8」）。
/// </summary>
/// <remarks>
/// <para>
/// 这是 <c>achievements.json</c> 的领域投影：<see cref="Version"/> 是**文件自身**的版本，
/// 与存档本体的 <c>SaveFileFormat.Version</c> **不联动**（契约二十三「条款 10」）。
/// </para>
/// <para>
/// <see cref="Diagnostics"/> 是**观察窗**而非状态：它只报告本次读取是否发生过隔离，
/// MUST NOT 参与合并判定（合并只认 <see cref="Achievements"/> / <see cref="Origins"/>）。
/// </para>
/// </remarks>
public sealed record AchievementArchive
{
    /// <summary>归档文件自身的版本；首版为 <c>1</c>。</summary>
    public required int Version { get; init; }

    /// <summary>成就达成记录（全量，含所有存档贡献的那些）。</summary>
    /// <remarks>次序即文件次序；读取方 MUST NOT 排序（契约二十三「条款 14」）。</remarks>
    public required IReadOnlyList<ArchiveAchievementRecord> Achievements { get; init; }

    /// <summary>出身达成段（仅「绝嗣 / 百年世家」两类，契约二十三「条款 38」）。</summary>
    public required IReadOnlyList<OriginRecord> Origins { get; init; }

    /// <summary>本次读取的诊断（是否隔离、隔离到哪、为什么）。</summary>
    public required AchievementArchiveDiagnostics Diagnostics { get; init; }

    /// <summary>空档案（文件不存在或损坏后的兜底；无诊断）。</summary>
    /// <remarks>
    /// 「文件缺失不是损坏」（契约二十三「条款 17」）——缺失路径返回的就是这个值。
    /// </remarks>
    public static AchievementArchive Empty { get; } = new()
    {
        Version = AchievementArchiveJson.Version,
        Achievements = [],
        Origins = [],
        Diagnostics = AchievementArchiveDiagnostics.None,
    };
}

/// <summary>
/// 一条成就达成记录（契约二十三「条款 8」）。
/// </summary>
/// <param name="Achievement">成就。</param>
/// <param name="Difficulty">达成时的难度。</param>
/// <param name="CompletedAt">**最早**达成年月（FR-018）。</param>
/// <param name="SourceSaveId">最早那条记录的来源存档标识（FR-025）。</param>
public readonly record struct ArchiveAchievementRecord(
    AchievementId Achievement,
    Difficulty Difficulty,
    GameDate CompletedAt,
    Guid SourceSaveId);

/// <summary>
/// 一条出身达成记录（契约二十三「条款 8」；仅绝嗣 / 百年世家）。
/// </summary>
/// <param name="Origin">达成时的出身（由调用方提供，实现 MUST NOT 自行猜，契约二十三「条款 13」）。</param>
/// <param name="Milestone">达成的里程碑。</param>
/// <param name="GameDate">达成年月。</param>
/// <param name="SourceSaveId">最早那条记录的来源存档标识（「条款 40」）。</param>
public readonly record struct OriginRecord(
    Origin Origin,
    OriginMilestone Milestone,
    GameDate GameDate,
    Guid SourceSaveId);

/// <summary>
/// 读取诊断：本次是否发生过损坏隔离（契约二十三「条款 8」）。
/// </summary>
/// <param name="WasQuarantined">是否发生过隔离。</param>
/// <param name="QuarantinePath">隔离后的文件路径；未隔离为 <c>null</c>。</param>
/// <param name="Reason">隔离原因；无诊断为 <c>null</c>。</param>
public readonly record struct AchievementArchiveDiagnostics(
    bool WasQuarantined,
    string? QuarantinePath,
    string? Reason)
{
    /// <summary>无诊断（正常路径）。</summary>
    public static AchievementArchiveDiagnostics None { get; } = new(false, null, null);

    /// <summary>一次损坏隔离。</summary>
    /// <param name="quarantinePath">隔离后的路径。</param>
    /// <param name="reason">原因。</param>
    /// <returns>诊断。</returns>
    public static AchievementArchiveDiagnostics Quarantined(string quarantinePath, string reason) =>
        new(true, quarantinePath, reason);
}
