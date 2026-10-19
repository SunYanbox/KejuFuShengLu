using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Achievements;
using KFL.Rules.Config;
using KFL.Rules.Exam;
using KFL.Rules.Punishment;

namespace KFL.Tests.Rules;

public partial class AchievementPredicateTests
{
    /// <summary>取「及第人数达到某档」的上下文。</summary>
    private static AchievementContext 科名累进(int 门槛, DegreeLevel 功名, bool 正侧) =>
        上下文(及第家族(正侧 ? 门槛 : 门槛 - 1, 功名));

    /// <summary>取「及第全程未行贿」的上下文。</summary>
    private static AchievementContext 清白传家(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        var 家族 = 家庭(成员);
        成员.AppendDegree(及第记录(DegreeLevel.JinShi));

        if (!正侧)
        {
            成员.RecordBribe(ExamLevel.JieShi);
        }

        return 上下文(家族);
    }

    /// <summary>取「当月有释放」的上下文。</summary>
    private static AchievementContext 铁窗生涯(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        var 释放 = 名单(正侧, new ReleaseFact(基准年月, 成员.Id, null));
        return 上下文(
            家庭(成员),
            当月报告: 报告(刑罚: PunishmentSnapshot.None(基准年月) with { Releases = 释放 }));
    }

    /// <summary>取「解试累计失败数」的上下文。</summary>
    private static AchievementContext 屡挫屡奋(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        var 家族 = 家庭(成员);
        var 次数 = 正侧
            ? ExamPolicy.JieShiExemptionFailureThreshold
            : ExamPolicy.JieShiExemptionFailureThreshold - 1;

        for (var i = 0; i < 次数; i++)
        {
            成员.RecordExamFailure(ExamLevel.JieShi);
        }

        return 上下文(家族);
    }

    /// <summary>取「皓首穷经」三要素齐备的上下文。</summary>
    private static AchievementContext 皓首穷经(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, AchievementPolicy.VenerableStudyAge);
        var 家族 = 家庭(成员);
        RulesHarness.Awaiting(成员, 1, AppointmentTrack.SpecialTribute);

        var 失败数 = 正侧
            ? AchievementPolicy.VenerableShengShiFailures
            : AchievementPolicy.VenerableShengShiFailures - 1;

        for (var i = 0; i < 失败数; i++)
        {
            成员.RecordExamFailure(ExamLevel.ShengShi);
        }

        return 上下文(家族);
    }

    /// <summary>取「鼎甲包揽」的上下文。</summary>
    private static AchievementContext 鼎甲包揽(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        var 家族 = 家庭(成员);
        成员.AppendDegree(一甲记录(ImperialPlacement.ZhuangYuan));
        成员.AppendDegree(一甲记录(ImperialPlacement.BangYan));

        if (正侧)
        {
            成员.AppendDegree(一甲记录(ImperialPlacement.TanHua));
        }

        return 上下文(家族);
    }

    /// <summary>取「科场遗珠」的上下文。</summary>
    private static AchievementContext 科场遗珠(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        var 家族 = 家庭(成员);
        var 次数 = 正侧
            ? AchievementPolicy.ExamFailuresPileUp
            : AchievementPolicy.ExamFailuresPileUp - 1;

        for (var i = 0; i < 次数; i++)
        {
            成员.RecordExamFailure(ExamLevel.ShengShi);
        }

        return 上下文(家族);
    }

    /// <summary>取「名落孙山」的上下文（省试落榜且学业达线）。</summary>
    private static AchievementContext 名落孙山(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        成员.Study = 正侧 ? AchievementPolicy.GeniusStudy : AchievementPolicy.GeniusStudy - 1;
        var 家族 = 家庭(成员);
        var 结果 = new ExamOutcomeLine(
            成员.Id,
            ExamLevel.ShengShi,
            Passed: false,
            Exempted: false,
            ImperialClass: null,
            Placement: null,
            FailureCountAfter: 1,
            DegreeAfter: DegreeLevel.JuRen);
        return 上下文(
            家族,
            当月报告: 报告(科举: ExamSeasonSnapshot.None(基准年月) with { Outcomes = new[] { 结果 } }));
    }

    /// <summary>建立「人人都有同一级别及第记录」的家族。</summary>
    private static Family 及第家族(int 人数, DegreeLevel 功名)
    {
        var 成员 = new Person[人数];

        for (var i = 0; i < 人数; i++)
        {
            成员[i] = 及第成员(i + 1, 功名);
        }

        return RulesHarness.FamilyOf(成员);
    }

    /// <summary>创建一名带及第记录的成员。</summary>
    private static Person 及第成员(int 序号, DegreeLevel 功名)
    {
        var 成员 = RulesHarness.Member(序号, Gender.Male, 30);
        成员.AppendDegree(及第记录(功名));
        return 成员;
    }

    /// <summary>创建一名一甲及第的成员。</summary>
    private static Person 一甲成员(int 序号, ImperialPlacement 名次)
    {
        var 成员 = RulesHarness.Member(序号, Gender.Male, 30);
        成员.AppendDegree(一甲记录(名次));
        return 成员;
    }

    /// <summary>一条「及第」功名变迁记录（无甲第、无名次）。</summary>
    private static DegreeRecord 及第记录(DegreeLevel 功名) =>
        new(功名, null, 及第年月, DegreeChangeCause.ExamPass);

    /// <summary>一条「进士一甲」功名变迁记录。</summary>
    private static DegreeRecord 一甲记录(ImperialPlacement 名次) =>
        new(DegreeLevel.JinShi, 名次, 及第年月, DegreeChangeCause.ExamPass, ImperialClass.FirstClass);
}
