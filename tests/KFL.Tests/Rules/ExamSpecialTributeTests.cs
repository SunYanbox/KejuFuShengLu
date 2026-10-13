using System.Reflection;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;
using KFL.Rules.Exam;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// T032 / SC-005：特奏名的资格判定（满 50 岁 ∧ 省试累计 6 败 ∧ 无官阶 ∧ 非待阙）、
/// 接受入仕与拒绝面（FR-012；契约十二 §6）。
/// </summary>
/// <remarks>
/// 期望值一律经 <c>KFL.Rules/Config</c> 的成员取得（SC-008）：年龄线用
/// <see cref="ExamPolicy.SpecialTributeMinimumAge"/>、失败数阈值用
/// <see cref="ExamPolicy.SpecialTributeFailureThreshold"/>、待阙区间用
/// <see cref="OfficialCareerPolicy.AwaitingPostMinMonths"/> /
/// <see cref="OfficialCareerPolicy.AwaitingPostMaxMonths"/>。
/// 随机来自 <see cref="FixedRandomService"/>：其 <c>Next</c> 恒返回下界，故待阙月数恰为 6。
/// </remarks>
public class ExamSpecialTributeTests
{
    private const BindingFlags PersonMemberFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly GameDate Date = RulesHarness.Date;

    [Fact]
    public void 特奏名年龄恰好五十岁合格而少一岁不合格()
    {
        var eligible = FailureLadenMember(1, ExamPolicy.SpecialTributeMinimumAge);
        var tooYoung = FailureLadenMember(2, ExamPolicy.SpecialTributeMinimumAge - 1);

        Assert.True(ExamRegistration.SpecialTributeEligible(eligible, Date));
        Assert.False(ExamRegistration.SpecialTributeEligible(tooYoung, Date));
    }

    [Fact]
    public void 特奏名省试失败数恰好阈值合格而少一败不合格()
    {
        var eligible = FailureLadenMember(3, ExamPolicy.SpecialTributeMinimumAge);
        var shortOfThreshold = RulesHarness.Member(4, Gender.Male, ExamPolicy.SpecialTributeMinimumAge);
        RecordShengShiFailures(shortOfThreshold, ExamPolicy.SpecialTributeFailureThreshold - 1);

        Assert.True(ExamRegistration.SpecialTributeEligible(eligible, Date));
        Assert.False(ExamRegistration.SpecialTributeEligible(shortOfThreshold, Date));
    }

    [Fact]
    public void 有官阶者年满五十且省试六败也不合格()
    {
        var official = RulesHarness.Official(
            5,
            OfficialCareerPolicy.InitialRankOf(AppointmentTrack.SpecialTribute),
            age: ExamPolicy.SpecialTributeMinimumAge);
        RecordShengShiFailures(official, ExamPolicy.SpecialTributeFailureThreshold);

        Assert.NotNull(official.Rank);
        Assert.False(ExamRegistration.SpecialTributeEligible(official, Date));
    }

    [Fact]
    public void 已在待阙者不合格()
    {
        var person = FailureLadenMember(6, ExamPolicy.SpecialTributeMinimumAge);
        RulesHarness.Awaiting(person, OfficialCareerPolicy.AwaitingPostMinMonths);

        Assert.True(person.Status.HasFlag(StatusFlag.AwaitingPost));
        Assert.False(ExamRegistration.SpecialTributeEligible(person, Date));
    }

    [Fact]
    public void 不合格者接受特奏名抛异常且逐字段不变()
    {
        var person = RulesHarness.Official(
            7,
            OfficialCareerPolicy.InitialRankOf(AppointmentTrack.SpecialTribute),
            age: ExamPolicy.SpecialTributeMinimumAge);
        RecordShengShiFailures(person, ExamPolicy.SpecialTributeFailureThreshold);

        var status = person.Status;
        var timers = person.Timers;
        var track = person.EntryTrack;
        var rank = person.Rank;
        var degrees = person.DegreeHistory.Count;

        Assert.Throws<InvalidOperationException>(
            () => ExamRegistration.AcceptSpecialTribute(person, Date, new FixedRandomService()));

        Assert.Equal(status, person.Status);
        Assert.Equal(timers, person.Timers);
        Assert.Equal(track, person.EntryTrack);
        Assert.Equal(rank, person.Rank);
        Assert.Equal(degrees, person.DegreeHistory.Count);
    }

    [Fact]
    public void 接受特奏名置入待阙且不追加功名记录()
    {
        var person = FailureLadenMember(8, ExamPolicy.SpecialTributeMinimumAge);
        var degreesBefore = person.DegreeHistory.Count;

        ExamRegistration.AcceptSpecialTribute(person, Date, new FixedRandomService());

        Assert.True(person.Status.HasFlag(StatusFlag.AwaitingPost));
        Assert.Equal(AppointmentTrack.SpecialTribute, person.EntryTrack);

        var remaining = person.Timers.AwaitingPostRemainingMonths.GetValueOrDefault(-1);
        Assert.True(remaining >= OfficialCareerPolicy.AwaitingPostMinMonths);
        Assert.True(remaining <= OfficialCareerPolicy.AwaitingPostMaxMonths);

        Assert.Equal(degreesBefore, person.DegreeHistory.Count);
        Assert.Equal(DegreeLevel.BaiShen, person.CurrentDegree);
    }

    [Fact]
    public void 结构上不存在拒绝字段故拒绝即不调用方法()
    {
        var forbidden = new[] { "Refuse", "Decline", "Tribute" };

        var declaredNames = typeof(Person)
            .GetProperties(PersonMemberFlags)
            .Select(property => property.Name)
            .Concat(typeof(Person).GetFields(PersonMemberFlags).Select(field => field.Name));

        var offenders = declaredNames
            .Where(name => forbidden.Any(keyword => name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void 接受特奏名不改动省试与解试失败计数()
    {
        var person = FailureLadenMember(9, ExamPolicy.SpecialTributeMinimumAge);
        person.RecordExamFailure(ExamLevel.JieShi);

        var jieShiBefore = person.JieShiFailureCount;
        var shengShiBefore = person.ShengShiFailureCount;

        ExamRegistration.AcceptSpecialTribute(person, Date, new FixedRandomService());

        Assert.Equal(jieShiBefore, person.JieShiFailureCount);
        Assert.Equal(shengShiBefore, person.ShengShiFailureCount);
    }

    [Fact]
    public void 接受特奏名后因待阙位而结构性退出科举()
    {
        var person = FailureLadenMember(10, ExamPolicy.SpecialTributeMinimumAge);
        var later = ExamCalendar.NextExamMonth(Date);

        Assert.Equal(ExamLevel.ShengShi, ExamCalendar.LevelOf(later));
        Assert.True(ExamRegistration.IsEligible(person, later));

        ExamRegistration.AcceptSpecialTribute(person, Date, new FixedRandomService());

        Assert.False(ExamRegistration.IsEligible(person, later));
    }

    /// <summary>构造一名年龄恰为 <paramref name="age"/>、省试累计失败数恰为阈值的白身成员。</summary>
    private static Person FailureLadenMember(int index, int age)
    {
        var person = RulesHarness.Member(index, Gender.Male, age);
        RecordShengShiFailures(person, ExamPolicy.SpecialTributeFailureThreshold);
        return person;
    }

    /// <summary>累计 <paramref name="count"/> 次省试落第（唯一写入通道是 <see cref="Person.RecordExamFailure"/>）。</summary>
    private static void RecordShengShiFailures(Person person, int count)
    {
        for (var step = 0; step < count; step++)
        {
            person.RecordExamFailure(ExamLevel.ShengShi);
        }
    }
}
