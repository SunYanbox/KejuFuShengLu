using KFL.Core.ValueObjects;

namespace KFL.Rules.Config;

/// <summary>
/// 调试覆盖（调试控制台专用，2026-10-19 需求 17）：按成员追加的录取率 / 自疗率百分点加成。
/// </summary>
/// <remarks>
/// <para>
/// **不落存档**：它是进程内的调试开关，不进 <c>GameState</c>、不进 DTO、不进任何持久化通道，
/// 因此不改变存档格式版本（<c>SaveFileFormat.Version</c> 保持 5）。
/// </para>
/// <para>
/// **默认全空**：没有登记过即为 0，正常对局与全部既有测试的行为逐字节不变（确定性不受影响）。
/// </para>
/// </remarks>
public static class DebugOverrides
{
    private static readonly Dictionary<Guid, decimal> ExamRateBonuses = [];
    private static readonly Dictionary<Guid, decimal> HealRateBonuses = [];

    /// <summary>为某成员追加「本次考试录取率」加成（百分点，<c>0.20m</c> = +20 个百分点）。</summary>
    /// <param name="personId">成员标识。</param>
    /// <param name="percentagePoints">加成百分点（可为负）。</param>
    public static void AddExamRateBonus(PersonId personId, decimal percentagePoints) =>
        ExamRateBonuses[personId.Value] = ExamRateBonusOf(personId) + percentagePoints;

    /// <summary>取某成员的录取率加成（百分点）。</summary>
    /// <param name="personId">成员标识。</param>
    /// <returns>加成百分点；未登记为 0。</returns>
    public static decimal ExamRateBonusOf(PersonId personId) =>
        ExamRateBonuses.TryGetValue(personId.Value, out var bonus) ? bonus : 0m;

    /// <summary>为某成员追加「疾病自疗率」加成（百分点）。</summary>
    /// <param name="personId">成员标识。</param>
    /// <param name="percentagePoints">加成百分点（可为负）。</param>
    public static void AddHealRateBonus(PersonId personId, decimal percentagePoints) =>
        HealRateBonuses[personId.Value] = HealRateBonusOf(personId) + percentagePoints;

    /// <summary>取某成员的自疗率加成（百分点）。</summary>
    /// <param name="personId">成员标识。</param>
    /// <returns>加成百分点；未登记为 0。</returns>
    public static decimal HealRateBonusOf(PersonId personId) =>
        HealRateBonuses.TryGetValue(personId.Value, out var bonus) ? bonus : 0m;

    /// <summary>清空全部调试覆盖（换档 / 新开局时调用）。</summary>
    public static void Clear()
    {
        ExamRateBonuses.Clear();
        HealRateBonuses.Clear();
    }
}
