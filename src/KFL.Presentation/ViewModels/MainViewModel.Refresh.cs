using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;
using KFL.Rules.Economy;
using KFL.Rules.Exam;
using KFL.Rules.Settlement;
using KFL.Presentation.Mvvm;

namespace KFL.Presentation.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    // ───────────────────────── 刷新 ─────────────────────────

    private void RefreshLog()
    {
        LogLines.Clear();
        if (_session is null)
        {
            return;
        }

        foreach (var line in MergeLogLines(_session.Log))
        {
            LogLines.Add(line);
        }
    }

    /// <summary>
    /// 纪事合并（玩家反馈 15）：把**正文完全相同**的行并成一条，保留首次出现的时间戳并追加
    /// 「× N」，例如 <c>[10年11月] （10 贯 转入储蓄）× 35</c>。只出现一次的行原样输出。
    /// </summary>
    /// <param name="lines">原始纪事（已带 <c>[年 月]</c> 前缀）。</param>
    /// <returns>合并后的显示序列（保持首次出现的位置）。</returns>
    private static List<string> MergeLogLines(IReadOnlyList<string> lines)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var first = new Dictionary<string, (int Index, string Prefix, string Body)>(StringComparer.Ordinal);

        for (var index = 0; index < lines.Count; index++)
        {
            var (prefix, body) = SplitLogLine(lines[index]);
            counts.TryGetValue(body, out var seen);
            counts[body] = seen + 1;
            first.TryAdd(body, (index, prefix, body));
        }

        // 按原始顺序输出；每条只在其首次出现的位置出现一次。
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>(lines.Count);
        foreach (var line in lines)
        {
            var (_, body) = SplitLogLine(line);
            if (!emitted.Add(body))
            {
                continue;
            }

            var (_, prefix, _) = first[body];
            result.Add(counts[body] > 1
                ? $"{prefix} {body} × {counts[body]}"
                : $"{prefix} {body}");
        }

        return result;
    }

    /// <summary>把 <c>[年 月] 正文</c> 拆成前缀（含方括号）与正文；没有前缀时前缀为空串。</summary>
    private static (string Prefix, string Body) SplitLogLine(string line)
    {
        if (!line.StartsWith('['))
        {
            return (string.Empty, line);
        }

        var end = line.IndexOf(']');
        return end < 0
            ? (string.Empty, line)
            : (line[..(end + 1)], line[(end + 1)..].TrimStart());
    }

    public void RefreshAll()
    {
        if (_session is null)
        {
            return;
        }

        var state = _session.State;
        var date = state.CurrentDate;
        var family = state.Family;

        var previousSelectedId = Selected?.Person.Id;
        RefreshMembers();
        if (previousSelectedId is { } id)
        {
            Selected = Members.Concat(Archived).FirstOrDefault(row => row.Person.Id == id);
        }

        Selected ??= Members.FirstOrDefault();

        var economy = state.Economy;
        var treasury = economy.Treasury;
        StatusLine = $"{date.Year} 年 {date.Month} 月 ｜ {family.Name}家 ｜ 出身 {Display.Origin(state.Origin)} ｜ 难度 {Display.Difficulty(state.Difficulty)}"
            + (state.PendingDifficulty is { } pending ? $"（次月转 {Display.Difficulty(pending)}）" : string.Empty)
            + $" ｜ 米价 {GrainPricePolicy.MarketPrice(economy.GrainPriceIndex.Value):0.##} 文/斗（系数 {economy.GrainPriceIndex.Value:0.###}）"
            + $" ｜ 生活费 {Display.LivingStandard(economy.LivingStandard)}"
            + (economy.PendingLivingStandard is { } pendingStandard ? $"→{Display.LivingStandard(pendingStandard)}" : string.Empty)
            + $" ｜ 持续报考 {(family.AutoExamApplication ? "开" : "关")}"
            + (family.HasShiStatus ? " ｜ 『仕』族" : string.Empty)
            + (state.Ending.IsEnded ? " ｜ ※绝嗣" : string.Empty);

        EconomyLine = $"现银 {Display.Money(treasury.Cash)} ｜ 储蓄 {Display.Money(treasury.Savings)}（年息 {(treasury.SavingsRate is { } savingsRate ? savingsRate.ToString("P1", System.Globalization.CultureInfo.InvariantCulture) : "未定")}）"
            + $" ｜ 商本 {Display.Money(treasury.MerchantCapital)}"
            + $" ｜ 贷款 {(treasury.Loan.IsSettled ? "无" : Display.Money(treasury.Loan.Total) + $"（本 {Display.Money(treasury.Loan.Principal)} / 息 {Display.Money(treasury.Loan.AccruedInterest)}）")}"
            + $" ｜ 田 {economy.Holdings.FarmlandMu} 亩 · 农舍 {economy.Holdings.RuralHouses} · 城市宅 {economy.Holdings.UrbanHouses} · 铺 {economy.Holdings.Shops}"
            + $" ｜ 总资产 {Display.Money(TotalAssetsCalculator.Of(economy))}"
            + (economy.Famine.Stage == FamineStage.None ? string.Empty : $" ｜ ⚠饥荒({economy.Famine.Stage})");

        var candidates = ExamRegistration.EligibleUnapplied(family, date).ToList();
        ExamCandidates.Clear();
        foreach (var person in candidates)
        {
            ExamCandidates.Add(Members.FirstOrDefault(row => row.Person.Id == person.Id) ?? new MemberRow(person, family, date));
        }

        var level = ExamCalendar.LevelOf(date);

        // 报名窗口是「考试当月及前一月」（需求 4 / 9）：先算窗口级别、可报名人数与已报名名单。
        var registerLevel = ExamRegistration.RegisterLevelOf(date);
        var registrable = CountedMembers.Registered(family).Count(person => ExamRegistration.CanRegister(person, date));
        var appliedList = CountedMembers.Registered(family)
            .Where(person => person.AppliedExam is { } appliedLevel)
            .OrderBy(person => person.Id.Value)
            .Select(person => $"{person.Name}（{Display.ExamLevel(person.AppliedExam!.Value)}）")
            .ToList();
        var appliedText = appliedList.Count > 0 ? $" ｜ 已报名：{string.Join('、', appliedList)}" : " ｜ 已报名：无";

        // 录取率提示（需求 5：随行贿次数实时更新）。
        var rates = candidates
            .Select(person => (Person: person, Rate: _session.AdmissionRateOf(person)))
            .Where(pair => pair.Rate is not null)
            .ToList();
        var rateText = rates.Count > 0
            ? $" ｜ 预估录取率：{string.Join('、', rates.Select(pair => $"{pair.Person.Name} {(pair.Rate!.Value * 100m).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}%"))}"
            : string.Empty;

        if (registerLevel is { } windowLevel)
        {
            var sameMonth = level == windowLevel;
            ExamInfo = $"{(sameMonth ? "本月" : "下月")}为 {Display.ExamLevel(windowLevel)} 月。"
                + (windowLevel == ExamLevel.DianShi
                    ? "殿试无需报名。"
                    : $"可报名 {registrable} 人（路费 {Display.Money(Money.FromGuan(ExamPolicy.ExamTravelCostGuan(state.Difficulty)))}）。")
                + (candidates.Count > 0 ? $" 待报名：{string.Join('、', candidates.Select(c => c.Name))}" : string.Empty)
                + appliedText
                + rateText;
        }
        else
        {
            var upcoming = ExamCalendar.NextExamMonth(date);
            ExamInfo = $"本月非考期。下一场考试：{upcoming.Year} 年 {upcoming.Month} 月。"
                + appliedText
                + rateText;
        }

        // 每月收支（需求 7）：实时按当前设置推算；明细走顶部「本月收支明细」按钮。
        RefreshMonthlyBalance();

        if (IsBreakdownVisible)
        {
            MonthlyBreakdownText = MonthlyBreakdown.BuildPopupText(state);
        }

        RefreshAchievements();
        RefreshDetail();
        RefreshSaves();
        RefreshBackups();
        RefreshStatistics();
        IsEnded = state.Ending.IsEnded;
        ChronicleText = BuildChronicle();
    }

    /// <summary>
    /// 刷新顶部「本月收支（推算）」一行（需求 7 / 14）：金额部分随设置实时变，
    /// 末尾的「自动治疗」状态取代了原先那条点不动的「点击查看明细」。
    /// </summary>
    private void RefreshMonthlyBalance()
    {
        if (_session is null)
        {
            MonthlyBalanceText = string.Empty;
            return;
        }

        var state = _session.State;
        var projectedIncome = MonthlyBreakdown.ProjectedIncome(state);
        var projectedLiving = MonthlyBreakdown.ProjectedLivingCost(state);
        var (tutorTotal, tutorCount) = MonthlyBreakdown.ProjectedTutorFee(state);
        var incomeTotal = projectedIncome.Total;
        var expenseTotal = Money.FromWen(projectedLiving.Sum(line => line.Amount.Wen) + tutorTotal.Wen);
        var netTotal = incomeTotal - expenseTotal;

        MonthlyBalanceText = $"本月收支（推算）｜ 收入 {MonthlyBreakdown.WithSign(incomeTotal)}"
            + $" ｜ 支出 {MonthlyBreakdown.WithSign(Money.FromWen(-expenseTotal.Wen))}"
            + $" ｜ 结余 {MonthlyBreakdown.WithSign(netTotal)}"
            + (tutorCount > 0 ? $"（含名师束脩 {tutorCount} 人）" : string.Empty)
            + $" ｜ 自动治疗：{(AutoTreat ? "开" : "关")}";
    }

    private string BuildChronicle()
    {
        if (_session is null || _session.State.Ending.Chronicle is not { } chronicle)
        {
            return "家族尚在存续。";
        }

        var state = _session.State;
        return $"""
            家族编年史（{state.Family.Name}家）
            存续年数：{chronicle.SurvivedYears} 年
            传承代数：{chronicle.Generations} 代
            峰值资产：{Display.Money(chronicle.PeakTotalAssets)}
            最高功名：{Display.Degree(chronicle.HighestDegree)}
            进士总数：{chronicle.TotalJinShi} 人
            平均寿命：{chronicle.AverageLifespan:0.###} 岁
            族谱人数：{chronicle.Genealogy.Count} 人
            """;
    }
}
