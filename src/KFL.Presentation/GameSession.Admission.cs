using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Rules.Bribery;
using KFL.Rules.Config;
using KFL.Rules.Exam;
using KFL.Rules.FamilyLifecycle;
using KFL.Rules.Settlement;

namespace KFL.Presentation;

public sealed partial class GameSession
{
    // ───────────────────────── 概率与自愈率明细（需求 5 / 18） ─────────────────────────

    /// <summary>某成员**当前应考级别**的录取率（已报名则取报名级别，否则取派生级别）。</summary>
    /// <param name="person">成员。</param>
    /// <returns>录取率；进士或无级别时为 <c>null</c>。</returns>
    public decimal? AdmissionRateOf(Person person) => AdmissionBreakdownOf(person)?.Rate;

    /// <summary>某成员当前级别录取率的逐项拆解。</summary>
    /// <param name="person">成员。</param>
    /// <returns>拆解；进士或无级别时为 <c>null</c>。</returns>
    public ExamRateBreakdown? AdmissionBreakdownOf(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        var level = person.AppliedExam ?? ExamRegistration.TargetLevelOf(person);

        if (level is not (ExamLevel.JieShi or ExamLevel.ShengShi))
        {
            return null;
        }

        return ExamAdmission.Breakdown(
            level.Value, person, State.Family.HasShiStatus, BribeRules.MultiplierOf(level.Value, person));
    }

    /// <summary>把录取率拆解排成给玩家看的多行文本（需求 18）。</summary>
    /// <param name="breakdown">拆解。</param>
    /// <returns>多行说明。</returns>
    public static string DescribeAdmissionBreakdown(ExamRateBreakdown breakdown)
    {
        var lines = new List<string>
        {
            $"{ExamLevelText(breakdown.Level)}录取率：{Percent(breakdown.Rate)}" +
                (breakdown.RawRate > ExamPolicy.DiceRateCap ? $"（封顶前 {Percent(breakdown.RawRate)}）" : string.Empty),
            $"  基础率：{Percent(breakdown.BaseRate)}",
            $"  学业系数：×{Number(breakdown.StudyFactor)}（学业 {breakdown.Study}）",
            $"  仕天赋：×{Number(breakdown.OfficialdomFactor)}（仕 {breakdown.Officialdom}，{Percent(breakdown.OfficialdomFactor - 1m)}）",
            $"  行贿乘数：×{Number(breakdown.BribeMultiplier)}（本级累计行贿 {breakdown.BribeCount} 次）",
            $"  仕族加成：×{Number(breakdown.ShiBonus)}",
        };

        if (breakdown.DebugBonus != 0m)
        {
            lines.Add($"  调试加成：+{Percent(breakdown.DebugBonus)}（百分点，直接加在乘积上）");
        }

        lines.Add(
            $"  = {Percent(breakdown.BaseRate)} × {Number(breakdown.StudyFactor)} × {Number(breakdown.OfficialdomFactor)}" +
            $" × {Number(breakdown.BribeMultiplier)} × {Number(breakdown.ShiBonus)}" +
            (breakdown.DebugBonus != 0m ? $" + {Percent(breakdown.DebugBonus)}" : string.Empty) +
            $" = {Percent(breakdown.RawRate + breakdown.DebugBonus)}" +
            (breakdown.RawRate + breakdown.DebugBonus > ExamPolicy.DiceRateCap
                ? $" → 封顶 {Percent(breakdown.Rate)}"
                : string.Empty));

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>某成员当前的自愈率拆解（需求 18）。</summary>
    /// <param name="person">成员。</param>
    /// <param name="treated">是否正在治疗。</param>
    /// <returns>拆解。</returns>
    public HealRateBreakdown RecoveryBreakdownOf(Person person, bool treated) =>
        IllnessQueries.RecoveryBreakdown(person, State.CurrentDate, treated);

    /// <summary>把自愈率拆解排成给玩家看的多行文本（需求 18）。</summary>
    /// <param name="breakdown">拆解。</param>
    /// <returns>多行说明。</returns>
    public static string DescribeRecoveryBreakdown(HealRateBreakdown breakdown)
    {
        var lines = new List<string>
        {
            $"自愈率：{Percent(breakdown.Rate)}（体质 {breakdown.Health}，{(breakdown.Treated ? "治疗中" : "未治疗")}，主导天赋 {breakdown.TalentName}）",
            $"  体质项：{(breakdown.Treated
                ? $"({breakdown.Health} × {Number(IllnessPolicy.TreatedRecoveryCoefficient)}% + {Number(IllnessPolicy.TreatedRecoveryOffset)})"
                : $"{breakdown.Health} × {Number(IllnessPolicy.BaseRecoveryCoefficient)}%")}" +
                $" = {Percent(breakdown.BasePart)}",
            $"  天赋乘数：×{Number(breakdown.TalentMultiplier)}（{breakdown.TalentName}为最高项）",
            $"  年龄增益：+{Percent(breakdown.AgeBonus)}（3~12 岁 +60%、12~24 岁 +30%）",
        };

        if (breakdown.DebugBonus != 0m)
        {
            lines.Add($"  调试加成：+{Percent(breakdown.DebugBonus)}");
        }

        lines.Add(
            $"  = ({Percent(breakdown.BasePart)} × {Number(breakdown.TalentMultiplier)})" +
            $" + {Percent(breakdown.AgeBonus)}" +
            (breakdown.DebugBonus != 0m ? $" + {Percent(breakdown.DebugBonus)}" : string.Empty) +
            $" = {Percent(breakdown.Rate)}");

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>自动治疗（需求 10）：把所有患病且未在治疗的成员一次性置入治疗。</summary>
    /// <returns>本次开始治疗的人数。</returns>
    public int AutoTreatAll()
    {
        var count = 0;

        foreach (var person in CountedMembers.Registered(State.Family).OrderBy(member => member.Id.Value).ToList())
        {
            if ((person.Status & StatusFlag.Ill) != StatusFlag.Ill
                || person.Timers.TreatmentRemainingMonths is not null)
            {
                continue;
            }

            var outcome = LifecycleEntry.StartTreatment(State, person);

            if (outcome.Accepted)
            {
                count++;
                Note($"✚ 自动治疗：{person.Name}（{IllnessPolicy.TreatmentMonths} 个月疗程）。");
            }
        }

        if (count > 0)
        {
            Changed?.Invoke();
        }

        return count;
    }

    /// <summary>百分比文本（<c>0.25 ⇒ 25%</c>）。</summary>
    private static string Percent(decimal value) =>
        (value * 100m).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%";

    /// <summary>系数文本（保留三位以内小数）。</summary>
    private static string Number(decimal value) =>
        value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
