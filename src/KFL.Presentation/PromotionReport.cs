using KFL.Core.Entities;
using KFL.Rules.Career;
using KFL.Rules.Config;

namespace KFL.Presentation;

/// <summary>
/// 「升官概率」的分类明细文本（2026-10-19 第九轮玩家反馈 6：需要地方查看成员每月升官职的概率与计算详情）。
/// </summary>
/// <remarks>
/// 与 <c>GameSession.DescribeAdmissionBreakdown</c> 同款风格：先给结论概率，再逐项列出各输入，
/// 最后一行给等价的算式。数值全部取自 <see cref="PromotionForecast"/> 与
/// <see cref="OfficialCareerPolicy"/>，不在文本里硬编码常量。
/// </remarks>
public static class PromotionReport
{
    /// <summary>生成升官概率明细（每行一条，供成员详情逐行显示）。</summary>
    /// <param name="forecast">预测结果（来自 <see cref="PromotionForecast.Of"/>）。</param>
    /// <param name="person">成员档案（用于功名文字与致仕判定）。</param>
    /// <returns>逐行明细文本。</returns>
    public static IReadOnlyList<string> Lines(PromotionForecast forecast, Person person)
    {
        ArgumentNullException.ThrowIfNull(forecast);
        ArgumentNullException.ThrowIfNull(person);

        var capNote = forecast.CapLevel == SalaryTable.HighestLevel
            ? "无官阶限制，最高可至第 1 级"
            : $"功名「{Display.Degree(person.CurrentDegree)}」限至第 {forecast.CapLevel} 级";

        return
        [
            $"升官概率（考课周期 {OfficialCareerPolicy.AppraisalPeriodMonths} 月）：{Percent(forecast.Chance)}",
            $"　当前官阶：第 {forecast.Rank.Level} 级　政绩：{forecast.Merit}　在职：{forecast.MonthsInOffice} / {OfficialCareerPolicy.AppraisalPeriodMonths} 月"
                + $"（距下次考课 {forecast.MonthsUntilAppraisal} 月）",
            $"　职称上限：{capNote}"
                + $"　晋级去向：{(forecast.NextLevel is { } next ? $"第 {next.Level} 级" : "已到顶（不掷骰，不消耗随机）")}",
            $"　概率算式：{Number(OfficialCareerPolicy.PromotionBaseChance)} + 政绩 {forecast.Merit} × {Number(OfficialCareerPolicy.PromotionChancePerMerit)}"
                + $" = {Percent(OfficialCareerPolicy.PromotionBaseChance + (forecast.Merit * OfficialCareerPolicy.PromotionChancePerMerit))}"
                + $"（封顶 {Percent(OfficialCareerPolicy.PromotionChanceCap)}）",
            $"　本月判定：{Verdict(forecast, person)}",
        ];
    }

    /// <summary>本月「是否可能晋升」的一句话结论。</summary>
    private static string Verdict(PromotionForecast forecast, Person person)
    {
        if (person.Status.HasFlag(KFL.Core.Enums.StatusFlag.Retired))
        {
            return "已致仕，只领半俸，不再参与考课";
        }

        if (forecast.ServingSentence)
        {
            return "服刑中：仕途整体暂停（待阙计时、在职计时与政绩都不推进）";
        }

        if (forecast.PromotionBanned)
        {
            return "禁升中：跳过到期判定并重置在职计时（本月不掷骰）";
        }

        if (forecast.AtLevelCap)
        {
            return "已到职称上限：不掷骰，也不消耗随机";
        }

        return forecast.MonthsUntilAppraisal > 0
            ? $"未到考课周期：还需 {forecast.MonthsUntilAppraisal} 个月"
            : $"可参与本月考课：掷骰 {Number(forecast.Chance)} 以下即升一级";
    }

    private static string Percent(decimal value)
        => (value * 100m).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%";

    private static string Number(decimal value)
        => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
