using System.Reflection;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Career;
using KFL.Rules.Config;
using KFL.Rules.Exam;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// T028 / T036：可报级别、七项资格、报名窗口与报名幂等（FR-002~FR-005；契约十二 §3~§4），
/// 以及 SC-007 后半的「合格未报名者名单」纯函数面。
/// </summary>
/// <remarks>
/// <para>
/// 期望值一律经 <c>KFL.Rules/Config</c> 取得（SC-008）：年龄线用
/// <see cref="ExamPolicy.MinimumAge"/>、待阙区间用
/// <see cref="OfficialCareerPolicy.AwaitingPostMinMonths"/>。
/// </para>
/// <para>
/// 「随机消费 0 次、账本 0 条」由**签名**保证，而不是运行期的巧合：整个报名面
/// （<see cref="ExamRegistration.CanRegister"/>、<see cref="ExamRegistration.Register"/>、
/// <see cref="ExamRegistration.EligibleUnapplied"/>）只收「成员 / 家族 + 年月」，
/// 既无 <c>IRandomService</c>，也无 <c>GameState</c> / <c>FamilyEconomy</c>。
/// </para>
/// </remarks>
public class ExamRegistrationTests
{
    /// <summary>参照年月（<c>(80, 1)</c>）：非考试月，专用于资格判定。</summary>
    private static readonly GameDate Date = RulesHarness.Date;

    /// <summary>解试月：科年 79（<c>79 % 3 == 1</c>）的 8 月。</summary>
    private static readonly GameDate JieShiMonth = new(79, 8);

    /// <summary>省试月：科年的次年 3 月。</summary>
    private static readonly GameDate ShengShiMonth = new(80, 3);

    /// <summary>殿试月：科年的次年 4 月。</summary>
    private static readonly GameDate DianShiMonth = new(80, 4);

    /// <summary>解试月**之前**（科年 7 月）——报名窗口的前半段（需求 4/9：当月及前一月）。</summary>
    private static readonly GameDate BeforeJieShi = new(79, 7);

    /// <summary>解试月前**两**月（科年 6 月）——窗口之外。</summary>
    private static readonly GameDate TwoMonthsBeforeJieShi = new(79, 6);

    /// <summary>解试月**之后**（科年 9 月）——同样是窗口外。</summary>
    private static readonly GameDate AfterJieShi = new(79, 9);

    // ------------------------------------------------------------------ 可报级别

    [Fact]
    public void 目标级别按当前功名四级映射且进士无级别()
    {
        Assert.Equal(ExamLevel.JieShi, ExamRegistration.TargetLevelOf(BaiShen(1)));
        Assert.Equal(ExamLevel.ShengShi, ExamRegistration.TargetLevelOf(JuRen(2)));
        Assert.Equal(ExamLevel.DianShi, ExamRegistration.TargetLevelOf(GongShi(3)));
        Assert.Null(ExamRegistration.TargetLevelOf(JinShi(4)));
    }

    // ------------------------------------------------------------------ 七项资格

    [Fact]
    public void 七项资格全真时合格()
    {
        Assert.True(ExamRegistration.IsEligible(BaiShen(1, age: ExamPolicy.MinimumAge), Date));
        Assert.True(ExamRegistration.IsEligible(JuRen(2), Date));
    }

    [Fact]
    public void 非在册者不合格()
    {
        // 在册 = 未亡 ∧ 未外嫁；两项各判一次，且复用 CountedMembers.IsRegistered 这一唯一出处。
        var deceased = BaiShen(1);
        deceased.Status |= StatusFlag.Deceased;

        var marriedOut = RulesHarness.Member(2, Gender.Female, 20);
        marriedOut.Status |= StatusFlag.MarriedOut;

        Assert.False(ExamRegistration.IsEligible(deceased, Date));
        Assert.False(ExamRegistration.IsEligible(marriedOut, Date));
    }

    [Fact]
    public void 服刑者不合格()
    {
        var person = BaiShen(1);
        RulesHarness.ServingSentence(person, 12);

        Assert.False(ExamRegistration.IsEligible(person, Date));
    }

    [Fact]
    public void 禁考者不合格且资格判定不清算该计时()
    {
        var person = BaiShen(1);
        person.Status |= StatusFlag.ExamBanned;

        var timersBefore = person.Timers;

        Assert.False(ExamRegistration.IsEligible(person, Date));

        // 只消费、不递减、不新建（契约十二 §3.2）。
        Assert.True(person.Status.HasFlag(StatusFlag.ExamBanned));
        Assert.Equal(timersBefore, person.Timers);
    }

    [Fact]
    public void 有官阶者仍合格且可应下一级考试()
    {
        // 2026-10-19 需求 8/21：举人即授官，仍可应省试；有官阶不再剥夺报考资格。
        var official = RulesHarness.Official(1, 3);
        Assert.NotNull(official.Rank);
        Assert.True(ExamRegistration.IsEligible(official, Date));

        // 举人（有官阶）确实以省试为目标级别，而不是被自动报考解试。
        var juRen = JuRen(2);
        Assert.True(AppointmentEntry.BeginForJuRen(juRen));
        Assert.NotNull(juRen.Rank);
        Assert.Equal(ExamLevel.ShengShi, ExamRegistration.TargetLevelOf(juRen));
        Assert.True(ExamRegistration.IsEligible(juRen, Date));
    }

    [Fact]
    public void 待阙者不合格()
    {
        var person = BaiShen(1);
        RulesHarness.Awaiting(person, OfficialCareerPolicy.AwaitingPostMinMonths, AppointmentTrack.SpecialTribute);

        Assert.False(ExamRegistration.IsEligible(person, Date));
    }

    [Fact]
    public void 未达报考年龄者不合格而恰好达标者合格()
    {
        Assert.False(ExamRegistration.IsEligible(BaiShen(1, age: ExamPolicy.MinimumAge - 1), Date));
        Assert.True(ExamRegistration.IsEligible(BaiShen(2, age: ExamPolicy.MinimumAge), Date));
    }

    [Fact]
    public void 无可报级别者不合格()
    {
        var jinShi = JinShi(1);

        Assert.Null(ExamRegistration.TargetLevelOf(jinShi));
        Assert.False(ExamRegistration.IsEligible(jinShi, Date));
    }

    [Fact]
    public void 性别不构成限制且年龄无上限()
    {
        var female = RulesHarness.Member(1, Gender.Female, ExamPolicy.MinimumAge);
        var elder = BaiShen(2, age: 90);

        Assert.True(ExamRegistration.IsEligible(female, Date));
        Assert.True(ExamRegistration.IsEligible(elder, Date));
    }

    // ------------------------------------------------------------------ 报名窗口

    [Fact]
    public void 只有科年八月与次年三月是报名窗口()
    {
        var baiShen = BaiShen(1);
        var juRen = JuRen(2);

        Assert.Equal(ExamLevel.JieShi, ExamCalendar.LevelOf(JieShiMonth));
        Assert.Equal(ExamLevel.ShengShi, ExamCalendar.LevelOf(ShengShiMonth));

        Assert.True(ExamRegistration.CanRegister(baiShen, JieShiMonth));
        Assert.True(ExamRegistration.CanRegister(juRen, ShengShiMonth));

        // 报名窗口 = 考试当月 **及前一月**（需求 4/9：玩家必须有机会在考试判定前报名）。
        Assert.True(ExamRegistration.CanRegister(baiShen, BeforeJieShi));
        Assert.Equal(ExamLevel.JieShi, ExamRegistration.RegisterLevelOf(BeforeJieShi));

        // 窗口之外：考前两月与考后。
        Assert.False(ExamRegistration.CanRegister(baiShen, TwoMonthsBeforeJieShi));
        Assert.False(ExamRegistration.CanRegister(baiShen, AfterJieShi));

        // 殿试月恒不接受报名（殿试由省试及第自动进入，FR-005）。
        Assert.False(ExamRegistration.CanRegister(juRen, DianShiMonth));
        Assert.False(ExamRegistration.CanRegister(GongShi(3), DianShiMonth));

        // 窗口在、但**级别不符**：白身在省试月、举人在解试月都不行。
        Assert.False(ExamRegistration.CanRegister(baiShen, ShengShiMonth));
        Assert.False(ExamRegistration.CanRegister(juRen, JieShiMonth));
    }

    [Fact]
    public void 窗口外报名抛异常且逐字段不变()
    {
        var baiShen = BaiShen(1);
        var before = Describe(baiShen);

        Assert.Throws<InvalidOperationException>(() => ExamRegistration.Register(baiShen, TwoMonthsBeforeJieShi));
        Assert.Throws<InvalidOperationException>(() => ExamRegistration.Register(baiShen, AfterJieShi));
        Assert.Throws<InvalidOperationException>(() => ExamRegistration.Register(baiShen, DianShiMonth));

        var juRen = JuRen(2);
        var juRenBefore = Describe(juRen);
        Assert.Throws<InvalidOperationException>(() => ExamRegistration.Register(juRen, JieShiMonth));

        Assert.Equal(before, Describe(baiShen));
        Assert.Null(baiShen.AppliedExam);
        Assert.Equal(juRenBefore, Describe(juRen));
        Assert.Null(juRen.AppliedExam);
    }

    [Fact]
    public void 重复报名幂等且逐字段不变()
    {
        var person = BaiShen(1);

        ExamRegistration.Register(person, JieShiMonth);
        Assert.Equal(ExamLevel.JieShi, person.AppliedExam);

        var afterFirst = Describe(person);
        ExamRegistration.Register(person, JieShiMonth);

        Assert.Equal(afterFirst, Describe(person));
        Assert.Equal(ExamLevel.JieShi, person.AppliedExam);
    }

    [Fact]
    public void 报名殿试抛异常且不落标记()
    {
        var person = GongShi(1);
        var before = Describe(person);

        Assert.Throws<InvalidOperationException>(() => person.ApplyForExam(ExamLevel.DianShi));

        Assert.Equal(before, Describe(person));
        Assert.Null(person.AppliedExam);
    }

    [Fact]
    public void 报名与名单查询不接触随机与账本()
    {
        // SC-007 后半的签名保证：三个入口都只有「成员 / 家族 + 年月」两个入参。
        AssertSignature(nameof(ExamRegistration.CanRegister), typeof(Person), typeof(GameDate));
        AssertSignature(nameof(ExamRegistration.Register), typeof(Person), typeof(GameDate));
        AssertSignature(nameof(ExamRegistration.EligibleUnapplied), typeof(Family), typeof(GameDate));
    }

    // ------------------------------------------------------------------ 合格未报名者名单（T036）

    [Fact]
    public void 合格未报名名单只列本月级别且报名后移出()
    {
        var baiShen = BaiShen(1);
        var tooYoung = BaiShen(2, age: ExamPolicy.MinimumAge - 1);
        var juRen = JuRen(3);
        var official = RulesHarness.Official(4, 3);
        var family = RulesHarness.FamilyOf(baiShen, tooYoung, juRen, official);

        // 解试月：名单里是「既合格、级别又对口、且尚未报名」的白身——含**有官阶**的白身
        // （需求 8/21：有官阶不再剥夺报考资格）。
        Assert.Equal(
            new[] { baiShen.Id, official.Id },
            ExamRegistration.EligibleUnapplied(family, JieShiMonth).Select(person => person.Id));

        // 省试月：换成举人；白身（含有官阶者）级别不符。
        Assert.Equal(
            new[] { juRen.Id },
            ExamRegistration.EligibleUnapplied(family, ShengShiMonth).Select(person => person.Id));

        // 报名后本人移出。
        ExamRegistration.Register(baiShen, JieShiMonth);
        Assert.Equal(
            new[] { official.Id },
            ExamRegistration.EligibleUnapplied(family, JieShiMonth).Select(person => person.Id));
    }

    [Fact]
    public void 非报名月与级别不符时合格未报名名单为空()
    {
        var baiShen = BaiShen(1);
        var juRen = JuRen(2);
        var family = RulesHarness.FamilyOf(baiShen, juRen);

        // 窗口之外：考前两月与考后都没有任何级别在招。
        Assert.Empty(ExamRegistration.EligibleUnapplied(family, TwoMonthsBeforeJieShi));
        Assert.Empty(ExamRegistration.EligibleUnapplied(family, AfterJieShi));

        // 殿试月恒为空表（不存在「报名参加殿试」这一状态）。
        Assert.Empty(ExamRegistration.EligibleUnapplied(family, DianShiMonth));

        // 级别不符：解试月的名单里没有举人。
        Assert.Equal(
            new[] { baiShen.Id },
            ExamRegistration.EligibleUnapplied(family, JieShiMonth).Select(person => person.Id));
    }

    [Fact]
    public void 合格未报名名单按成员编号升序()
    {
        var third = BaiShen(3);
        var first = BaiShen(1);
        var second = BaiShen(2);
        var family = RulesHarness.FamilyOf(third, first, second);

        Assert.Equal(
            new[] { first.Id, second.Id, third.Id },
            ExamRegistration.EligibleUnapplied(family, JieShiMonth).Select(person => person.Id));
    }

    [Fact]
    public void 合格未报名名单是纯函数()
    {
        var first = BaiShen(1);
        var second = JuRen(2);
        var family = RulesHarness.FamilyOf(first, second);

        var before = $"{Describe(first)}||{Describe(second)}||{family.Members.Count}";

        var once = ExamRegistration.EligibleUnapplied(family, JieShiMonth);
        var twice = ExamRegistration.EligibleUnapplied(family, JieShiMonth);

        Assert.Equal(once.Select(person => person.Id), twice.Select(person => person.Id));
        Assert.Equal(before, $"{Describe(first)}||{Describe(second)}||{family.Members.Count}");
    }

    // ------------------------------------------------------------------ 构造件

    /// <summary>白身成员（功名史为空）。</summary>
    /// <param name="index">夹具标识下标。</param>
    /// <param name="age">参照年月下的周岁数。</param>
    private static Person BaiShen(int index, int age = 20) => RulesHarness.Member(index, Gender.Male, age);

    /// <summary>已到举人的成员。</summary>
    /// <param name="index">夹具标识下标。</param>
    /// <param name="age">参照年月下的周岁数。</param>
    private static Person JuRen(int index, int age = 20) =>
        WithDegree(BaiShen(index, age), DegreeLevel.JuRen);

    /// <summary>已到贡士的成员。</summary>
    /// <param name="index">夹具标识下标。</param>
    /// <param name="age">参照年月下的周岁数。</param>
    private static Person GongShi(int index, int age = 20) =>
        WithDegree(JuRen(index, age), DegreeLevel.GongShi);

    /// <summary>已到进士的成员。</summary>
    /// <param name="index">夹具标识下标。</param>
    /// <param name="age">参照年月下的周岁数。</param>
    private static Person JinShi(int index, int age = 20) =>
        WithDegree(GongShi(index, age), DegreeLevel.JinShi);

    /// <summary>追加一条中第功名记录（并保留既有记录）。</summary>
    /// <param name="person">成员。</param>
    /// <param name="level">追加到的功名。</param>
    private static Person WithDegree(Person person, DegreeLevel level)
    {
        person.AppendDegree(new DegreeRecord(level, null, Date, DegreeChangeCause.ExamPass, null));
        return person;
    }

    /// <summary>把「逐字段不变」可断言的成员状态摊平成一个字符串。</summary>
    /// <param name="person">成员。</param>
    private static string Describe(Person person) => string.Join(
        "|",
        person.Id,
        person.Status,
        person.AppliedExam?.ToString() ?? "none",
        person.JieShiFailureCount,
        person.ShengShiFailureCount,
        person.DegreeHistory.Count,
        person.CurrentDegree,
        person.CurrentPlacement?.ToString() ?? "none",
        person.Rank?.Level ?? 0,
        person.MonthsInOffice,
        person.EntryTrack?.ToString() ?? "none",
        person.Timers);

    /// <summary>断言某个入口的入参就是「契约冻结的两个」——多一个 <c>IRandomService</c> 都算破防。</summary>
    /// <param name="name">方法名。</param>
    /// <param name="first">第一参数类型。</param>
    /// <param name="second">第二参数类型。</param>
    private static void AssertSignature(string name, Type first, Type second)
    {
        var method = typeof(ExamRegistration).GetMethod(name);
        Assert.NotNull(method);

        var parameters = method!.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal(first, parameters[0].ParameterType);
        Assert.Equal(second, parameters[1].ParameterType);
    }
}
