using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Exam;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// T035 / SC-007：考试日历（<c>KFL.Rules.Exam.ExamCalendar</c>）的科年判定、考试月映射与
/// 「严格晚于起点」的下一次考试月。
/// </summary>
/// <remarks>
/// 三个入口是规格书 §6 与契约十二 §1 的**纯派生**：无字段、无状态、不读 <c>GameState</c>。
/// 科年 <c>N</c> 的三场考试固定为 <c>(N, 8)</c> 解试、<c>(N+1, 3)</c> 省试、<c>(N+1, 4)</c> 殿试。
/// </remarks>
public class ExamCalendarTests
{
    /// <summary>科年判定：正年份中模三余一者为真（契约十二 §1 条款 1）。</summary>
    /// <param name="year">候选年份。</param>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(100)]
    public void 科年判定落在模三余一的正年份(int year) => Assert.True(ExamCalendar.IsKeYear(year));

    /// <summary>非科年与纪年起点之前一律为假（契约十二 §1 条款 1）。</summary>
    /// <param name="year">候选年份。</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    public void 科年判定拒绝非科年与纪年起点之前的年份(int year) => Assert.False(ExamCalendar.IsKeYear(year));

    /// <summary>考试月映射：只有解试、省试、殿试三个具名档期（契约十二 §1 条款 2、3）。</summary>
    [Fact]
    public void 考试月只有解试省试殿试三个档期()
    {
        Assert.Equal(ExamLevel.JieShi, ExamCalendar.LevelOf(new GameDate(1, 8)));
        Assert.Equal(ExamLevel.ShengShi, ExamCalendar.LevelOf(new GameDate(2, 3)));
        Assert.Equal(ExamLevel.DianShi, ExamCalendar.LevelOf(new GameDate(2, 4)));
        Assert.Equal(ExamLevel.JieShi, ExamCalendar.LevelOf(new GameDate(4, 8)));
        Assert.Equal(ExamLevel.ShengShi, ExamCalendar.LevelOf(new GameDate(5, 3)));
        Assert.Equal(ExamLevel.DianShi, ExamCalendar.LevelOf(new GameDate(5, 4)));
    }

    /// <summary>一个科年的三连按解试、省试、殿试的时间顺序排列（规格书 §6）。</summary>
    [Fact]
    public void 科年三连的解试省试殿试依次排列()
    {
        Assert.Equal(ExamLevel.JieShi, ExamCalendar.LevelOf(new GameDate(7, 8)));
        Assert.Equal(ExamLevel.ShengShi, ExamCalendar.LevelOf(new GameDate(8, 3)));
        Assert.Equal(ExamLevel.DianShi, ExamCalendar.LevelOf(new GameDate(8, 4)));
    }

    /// <summary>全窗口扫描：1~6 年共 72 个月里，非空恰好落在六个考试月（契约十二 §1 条款 2、3）。</summary>
    [Fact]
    public void 全窗口扫描非空当且仅当三档考试月()
    {
        var nonNull = new List<GameDate>();

        for (var year = 1; year <= 6; year++)
        {
            for (var month = 1; month <= 12; month++)
            {
                var date = new GameDate(year, month);

                if (ExamCalendar.LevelOf(date) is not null)
                {
                    nonNull.Add(date);
                }
            }
        }

        Assert.Equal(
            new[]
            {
                new GameDate(1, 8),
                new GameDate(2, 3),
                new GameDate(2, 4),
                new GameDate(4, 8),
                new GameDate(5, 3),
                new GameDate(5, 4),
            },
            nonNull);
    }

    /// <summary>边界走廊：(1,8) 之后经 (2,5~12)、(3,1~12)、(4,1~7) 一路为空，直至下一个科年 8 月。</summary>
    [Fact]
    public void 相邻两次解试之间的走廊月份一律没有考试()
    {
        for (var month = 9; month <= 12; month++)
        {
            Assert.Null(ExamCalendar.LevelOf(new GameDate(1, month)));
        }

        foreach (var month in new[] { 1, 2 })
        {
            Assert.Null(ExamCalendar.LevelOf(new GameDate(2, month)));
        }

        for (var month = 5; month <= 12; month++)
        {
            Assert.Null(ExamCalendar.LevelOf(new GameDate(2, month)));
        }

        for (var month = 1; month <= 12; month++)
        {
            Assert.Null(ExamCalendar.LevelOf(new GameDate(3, month)));
        }

        for (var month = 1; month <= 7; month++)
        {
            Assert.Null(ExamCalendar.LevelOf(new GameDate(4, month)));
        }

        Assert.Equal(ExamLevel.JieShi, ExamCalendar.LevelOf(new GameDate(4, 8)));
    }

    /// <summary>起点 (1,1) 的精确行走序列（契约十二 §1 条款 4）。</summary>
    [Fact]
    public void 下一个考试月自一年一月起给出精确序列()
    {
        var expected = new[]
        {
            (1, 8),
            (2, 3),
            (2, 4),
            (4, 8),
            (5, 3),
            (5, 4),
            (7, 8),
        };

        var current = new GameDate(1, 1);

        foreach (var (year, month) in expected)
        {
            current = ExamCalendar.NextExamMonth(current);

            Assert.Equal(new GameDate(year, month), current);
        }
    }

    /// <summary>严格晚于起点：对 1~9 年的每一个月都成立（契约十二 §1 条款 4）。</summary>
    [Fact]
    public void 下一个考试月严格晚于任意起点()
    {
        for (var year = 1; year <= 9; year++)
        {
            for (var month = 1; month <= 12; month++)
            {
                var date = new GameDate(year, month);

                Assert.True(
                    ExamCalendar.NextExamMonth(date) > date,
                    $"{date} 的下一个考试月 MUST 严格晚于起点本身（契约十二 §1 条款 4）。");
            }
        }
    }

    /// <summary>解试当月不是它自己的下一个考试月，而是次年三月（严格晚于）。</summary>
    [Fact]
    public void 解试当月的下一个考试月是次年三月()
    {
        Assert.Equal(new GameDate(2, 3), ExamCalendar.NextExamMonth(new GameDate(1, 8)));
    }

    /// <summary>殿试当月之后是下一科年的解试（跨科年边界）。</summary>
    [Fact]
    public void 殿试当月的下一个考试月是下一科年八月()
    {
        Assert.Equal(new GameDate(4, 8), ExamCalendar.NextExamMonth(new GameDate(2, 4)));
    }

    /// <summary>纯派生：同输入两次调用结果相等，且静态类无字段、无实例方法（G-07、FR-001、FR-018）。</summary>
    [Fact]
    public void 三个入口是纯派生且类内无字段无实例方法()
    {
        var levelFirst = ExamCalendar.LevelOf(new GameDate(1, 8));
        var levelSecond = ExamCalendar.LevelOf(new GameDate(1, 8));
        var noExamFirst = ExamCalendar.LevelOf(new GameDate(2, 5));
        var noExamSecond = ExamCalendar.LevelOf(new GameDate(2, 5));
        var nextFirst = ExamCalendar.NextExamMonth(new GameDate(3, 7));
        var nextSecond = ExamCalendar.NextExamMonth(new GameDate(3, 7));

        Assert.Equal(levelFirst, levelSecond);
        Assert.Equal(noExamFirst, noExamSecond);
        Assert.Equal(nextFirst, nextSecond);

        var flags = System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.Instance;

        Assert.Empty(typeof(ExamCalendar).GetFields(flags));

        var methods = typeof(ExamCalendar).GetMethods(flags | System.Reflection.BindingFlags.DeclaredOnly);

        Assert.NotEmpty(methods);

        foreach (var method in methods)
        {
            Assert.True(method.IsStatic, $"{method.Name} MUST 是静态方法（契约十二 §1：静态纯函数类）。");
        }
    }

    /// <summary>前史纪年（年 &lt; 1）：没有考试，且下一个考试月是元年八月（契约十二 §1 条款 2、3、4）。</summary>
    [Fact]
    public void 前史纪年没有考试且下一个考试月是元年八月()
    {
        Assert.Null(ExamCalendar.LevelOf(new GameDate(0, 1)));
        Assert.Null(ExamCalendar.LevelOf(new GameDate(0, 8)));
        Assert.Null(ExamCalendar.LevelOf(new GameDate(-3, 3)));

        Assert.Equal(new GameDate(1, 8), ExamCalendar.NextExamMonth(new GameDate(0, 1)));
        Assert.Equal(new GameDate(1, 8), ExamCalendar.NextExamMonth(new GameDate(-7, 12)));
    }
}
