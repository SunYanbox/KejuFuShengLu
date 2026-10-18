namespace KFL.Core.Enums;

/// <summary>
/// 全局文件「出身达成」段的达成类别（FR-020；data-model §2.1）。
/// </summary>
/// <remarks>
/// <para>
/// **不是**成就标识：它记录「某个出身已达成过哪一类里程碑」，与 <see cref="AchievementId"/>
/// 是**两个**维度。`CenturyHouse` 之外的第二类——与 <see cref="AchievementId.FamilyExtinct"/>
/// （ACH-044）/ <see cref="AchievementId.CenturyHouse"/>（ACH-045）**同一次触发**，
/// 分别记一条本枚举。
/// </para>
/// <para>
/// **MUST NOT** 为「千秋万代」（ACH-046）加第三项：规格书 §14.1 的「四民皆历」只登记
/// 「绝嗣」与「百年世家」两类（契约二十三 §9「条款 38」的排除项）。
/// </para>
/// </remarks>
public enum OriginMilestone
{
    /// <summary>绝嗣（与 ACH-044 同一次触发）。</summary>
    Extinction = 0,

    /// <summary>百年世家（与 ACH-045 同一次触发）。</summary>
    CenturyFamily = 1,
}
