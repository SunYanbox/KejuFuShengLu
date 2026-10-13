namespace KFL.Core.Enums;

/// <summary>
/// 科举考试级别（规格书 §6；契约十二 §2）。
/// </summary>
/// <remarks>
/// <para>
/// 三级：解试 → 省试 → 殿试。**MUST NOT 设 <c>None = 0</c>**：「本月无考试」由
/// <see cref="ExamLevel"/> 的可空引用（<c>null</c>）表达，<c>0</c> 与 <c>&gt; 3</c> 都不是具名成员。
/// </para>
/// <para>
/// **科年与考试月是派生值，MUST NOT 持久化**：科年 <c>N</c> 的 <c>(N, 8)</c> 为解试、
/// <c>(N+1, 3)</c> 为省试、<c>(N+1, 4)</c> 为殿试（规格书 §6）。唯一出处是
/// <c>KFL.Rules/Exam/ExamCalendar</c>。
/// </para>
/// <para>
/// 报名标记只允许 <see cref="JieShi"/> 与 <see cref="ShengShi"/>——殿试由省试及第自动进入，
/// 不存在「报名参加殿试」这一状态（FR-005、FR-023）。
/// </para>
/// </remarks>
public enum ExamLevel
{
    /// <summary>解试：科年 <c>N</c> 的 8 月（规格书 §6）。</summary>
    JieShi = 1,

    /// <summary>省试：科年 <c>N</c> 的次年 3 月（规格书 §6）。</summary>
    ShengShi = 2,

    /// <summary>殿试：科年 <c>N</c> 的次年 4 月（规格书 §6）。</summary>
    DianShi = 3,
}
