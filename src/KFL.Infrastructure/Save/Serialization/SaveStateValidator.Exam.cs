// SaveStateValidator 第 10 条：科举进度合法。

using KFL.Core.Entities;
using KFL.Core.Enums;

namespace KFL.Infrastructure.Save.Serialization;

public static partial class SaveStateValidator
{
    /// <summary>第 10 条：科举进度合法（005-exam-cycle；契约十四 §10）。</summary>
    /// <remarks>
    /// 两个失败数 MUST <c>&gt;= 0</c>；报名级别非空时 MUST 是 <c>JieShi</c> 或 <c>ShengShi</c>
    /// ——殿试由省试及第自动进入，**不是**可报名级别。判据同时覆盖二进制侧的越界整数
    /// （<c>0</c> 与 <c>&gt; 3</c>），JSON 侧认不得的**名字**在第 ⑤ 步即以
    /// <see cref="SaveFileErrorKind.Malformed"/> 失败。
    /// </remarks>
    private static void EnsureExamProgressInRange(IReadOnlyCollection<Person> members)
    {
        foreach (var member in members)
        {
            if (member.JieShiFailureCount < 0 || member.ShengShiFailureCount < 0)
            {
                throw Invalid(
                    $"成员 {member.Id} 的科举失败数为负（解试 {member.JieShiFailureCount}、省试 {member.ShengShiFailureCount}，R-04 第 10 条）。");
            }

            if (member.AppliedExam is { } applied &&
                applied is not ExamLevel.JieShi and not ExamLevel.ShengShi)
            {
                throw Invalid(
                    $"成员 {member.Id} 的报名级别 {applied} 不是合法的报名态（只允许 JieShi 与 ShengShi，R-04 第 10 条）。");
            }
        }
    }
}
