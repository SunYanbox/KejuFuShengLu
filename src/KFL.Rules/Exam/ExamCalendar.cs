using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Rules.Exam;

/// <summary>
/// 考试日历的纯派生（规格书 §6；契约十二 §1）。
/// </summary>
/// <remarks>
/// <para>
/// 静态纯函数类：**无字段、无状态**。三个入口 MUST NOT 读 <c>GameState</c>、MUST NOT 改任何状态、
/// MUST NOT 消耗随机、MUST NOT 依赖系统时钟或全局状态（G-07、FR-001、FR-018）。
/// </para>
/// <para>
/// 科年 <c>N</c> 的三场考试：<c>(N, 8)</c> 解试、<c>(N+1, 3)</c> 省试、<c>(N+1, 4)</c> 殿试；
/// 其余月份无考试（<c>null</c>）。科年与考试月是**派生值，MUST NOT 持久化**（契约十二 §1 条款 6）。
/// </para>
/// </remarks>
public static class ExamCalendar
{
    /// <summary>科年判定：<c>N &gt;= 1</c> 且 <c>N % 3 == 1</c>（契约十二 §1 条款 1）。</summary>
    /// <param name="year">架空纪年。</param>
    /// <returns>该年为科年时为 <c>true</c>。</returns>
    public static bool IsKeYear(int year) => year >= 1 && (year % 3) == 1;

    /// <summary>某年月的考试级别（契约十二 §1 条款 2、3）。</summary>
    /// <param name="date">游戏年月。</param>
    /// <returns>该月的考试级别；无考试为 <c>null</c>。</returns>
    public static ExamLevel? LevelOf(GameDate date)
    {
        if (date.Year < 1)
        {
            return null;
        }

        return (date.Year % 3, date.Month) switch
        {
            (1, 8) => ExamLevel.JieShi,
            (2, 3) => ExamLevel.ShengShi,
            (2, 4) => ExamLevel.DianShi,
            _ => null,
        };
    }

    /// <summary>严格晚于 <paramref name="date"/> 的第一个考试月，含跨年与跨科年边界（契约十二 §1 条款 4）。</summary>
    /// <param name="date">起点年月。</param>
    /// <returns>下一个考试月。</returns>
    /// <remarks>
    /// 三条候选按时间升序排布：<c>N%3==1</c> 时当年 8 月、次年 3 月、次年 4 月；
    /// <c>N%3==2</c> 时当年 3 月、当年 4 月、再两年后的 8 月；<c>N%3==0</c> 时次年 8 月、
    /// 再一年后的 3 月与 4 月。取首个严格晚于 <paramref name="date"/> 者。
    /// </remarks>
    public static GameDate NextExamMonth(GameDate date)
    {
        var year = date.Year;

        if (year < 1)
        {
            return new GameDate(1, 8);
        }

        GameDate[] candidates = (year % 3) switch
        {
            1 =>
            [
                new GameDate(year, 8),
                new GameDate(year + 1, 3),
                new GameDate(year + 1, 4),
            ],
            2 =>
            [
                new GameDate(year, 3),
                new GameDate(year, 4),
                new GameDate(year + 2, 8),
            ],
            _ =>
            [
                new GameDate(year + 1, 8),
                new GameDate(year + 2, 3),
                new GameDate(year + 2, 4),
            ],
        };

        foreach (var candidate in candidates)
        {
            if (candidate > date)
            {
                return candidate;
            }
        }

        // 三条候选覆盖了起点之后最近的一次「解试→省试→殿试」三连，
        // 最后一支的最晚候选必晚于 date，故此处不可达。
        throw new InvalidOperationException(
            "考试日历推导失败：候选集未覆盖（契约十二 §1 条款 4）——这是实现缺陷，不是输入问题。");
    }
}
