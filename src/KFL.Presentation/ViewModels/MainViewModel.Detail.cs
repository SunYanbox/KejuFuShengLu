using System.Collections.ObjectModel;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Bribery;
using KFL.Rules.Career;
using KFL.Rules.Config;
using KFL.Rules.Exam;
using KFL.Rules.FamilyLifecycle;
using KFL.Rules.Punishment;
using KFL.Presentation.Mvvm;

namespace KFL.Presentation.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private void RefreshDetail()
    {
        if (_session is null || Selected is null)
        {
            DetailText = "（未选择成员）";
            RelationLinks.Clear();
            RelationRows.Clear();
            return;
        }

        var state = _session.State;
        var family = state.Family;
        var date = state.CurrentDate;
        var person = Selected.Person;

        // 关系超链接（需求 7）：与下方的纯文本详情节保持一致，点一下即跳转到对方卡片。
        RelationLinks.Clear();
        foreach (var link in FamilyRelations.LinksOf(family, person))
        {
            RelationLinks.Add(link);
        }

        // 三行分组（第九轮：父母与配偶 / 兄弟姐妹 / 子女各占一行，称谓带长幼序号与状态配色）。
        RelationRows.Clear();
        foreach (var row in FamilyRelations.RowsOf(family, person))
        {
            RelationRows.Add(row);
        }

        var lines = new List<string>
        {
            $"姓名：{person.Name}　{Display.Gender(person.Gender)}　{person.AgeAt(date)} 岁　第 {person.Generation} 代"
                + (family.HeadId == person.Id ? "　【家主】" : string.Empty)
                + (Selected.IsArchived ? "　（归档）" : string.Empty),
            $"父母：父 {Selected.FatherText}　母 {Selected.MotherText}",
            $"子女：{FamilyRelations.ChildrenText(family, person)}",
            $"兄弟姐妹：{FamilyRelations.SiblingsText(family, person)}",
            $"功名：{Display.Degree(person.CurrentDegree)}"
                + (person.CurrentPlacement is { } placement ? $"（{Display.Placement(placement)}）" : string.Empty)
                + $"　官职：{(person.Rank is { } rank ? $"第 {rank.Level} 级" : "无")}"
                + (person.Rank is { } currentRank ? $"（月俸 {Display.Money(Money.FromGuan(SalaryTable.MonthlySalaryGuan(currentRank.Level)))}）" : string.Empty)
                + $"　政绩：{person.Merit}"
                + (person.EntryTrack is { } track ? $"　入仕：{track}" : string.Empty),
            $"学业：{person.Study}　体质：{person.Health}　天命寿数：{person.Lifespan}"
                + $"　天赋：农 {person.Talents.Agriculture} / 商 {person.Talents.Commerce} / 仕 {person.Talents.Officialdom} / 工 {person.Talents.Craft}",
            $"状态：{Display.StatusOf(person, date)}"
                + $"　配偶：{(person.SpouseId is { } spouseId ? family.TryGet(spouseId)?.Name ?? "—" : "—")}"
                + (person.IsTutoredByMaster ? "　名师：是" : string.Empty),
            $"标志：{BuildBadgeText(person)}",
            $"报考：{(ExamRegistration.TargetLevelOf(person) is { } targetLevel ? Display.ExamLevel(targetLevel) : "无可报考")}"
                + $"　本月可报名：{(ExamRegistration.CanRegister(person, date) ? "是" : "否")}"
                + (person.AppliedExam is { } appliedLevel ? $"　已报名：{Display.ExamLevel(appliedLevel)}" : "　已报名：无")
                + $"　特奏名：{(ExamRegistration.SpecialTributeEligible(person, date) ? "符合" : "否")}"
                + $"　持续报考：{(family.AutoExamApplication ? "开" : "关")}",
            $"解试累计失败 {person.JieShiFailureCount} / 省试累计失败 {person.ShengShiFailureCount}",
        };

        // 月收支与来源明细（需求 3 / 7）。
        lines.AddRange(MonthlyBreakdown.MemberLines(state, person));

        // 录取率明细（需求 5 / 18）：已报名取报名级别，否则取派生级别。
        if (_session.AdmissionBreakdownOf(person) is { } admission)
        {
            lines.Add(GameSession.DescribeAdmissionBreakdown(admission));
        }

        // 升官概率明细（第九轮需求 6）：与录取率同款——结论概率 + 各输入 + 等价算式。
        if (PromotionForecast.Of(person) is { } forecast)
        {
            lines.AddRange(PromotionReport.Lines(forecast, person));
        }
        else
        {
            lines.Add("升官概率：—（尚无官职；先由科举入仕，或受特奏名直接授官）");
        }

        // 自愈率明细（需求 18）：患病时才展示（未患病该值没有意义，只给一行占位）。
        var illness = LifecycleQueries.IllnessOf(person);
        var treated = illness.TreatmentRemainingMonths is not null;
        if (person.Status.HasFlag(StatusFlag.Ill) || treated)
        {
            lines.Add(GameSession.DescribeRecoveryBreakdown(_session.RecoveryBreakdownOf(person, treated)));
        }
        else
        {
            lines.Add("自愈率：—（未患病）");
        }

        foreach (var level in new[] { ExamLevel.JieShi, ExamLevel.ShengShi, ExamLevel.DianShi })
        {
            var feasibility = PunishmentQueries.Of(state, person, level);
            var nth = BribeRules.NthOf(person, level);
            lines.Add($"行贿 {Display.ExamLevel(level)}：第 {nth} 次，需 {Display.Money(feasibility.Amount)}，被抓 {(double)feasibility.Probability:P1}，乘数 {BribeRules.MultiplierOf(level, person):0.###}"
                + $"　[{(PunishmentQueries.IsWithinBribeWindow(date, level) ? "窗口内" : "非窗口")}｜{(feasibility.IsFeasible ? "可行" : Display.BribeReason(feasibility.RejectReason))}"
                + $"{(PunishmentQueries.IsLevelCapReached(state, person, level) ? "｜已达上限" : string.Empty)}]");
        }

        if (illness.Ill)
        {
            lines.Add($"疾病：程度 {illness.SeverityBasisPoints} 磅值　疗程剩余 {(illness.TreatmentRemainingMonths is { } remaining ? remaining.ToString(System.Globalization.CultureInfo.InvariantCulture) : "未就医")} 月");
        }

        if (LifecycleQueries.IsMarriageable(family, person, LifecycleQueries.DirectionOf(person.Gender), date))
        {
            var feasibility = LifecycleQueries.MatchmakeFeasibility(state, person, LifecycleQueries.DirectionOf(person.Gender));
            lines.Add($"婚姻：可托媒（{Display.LifecycleReason(feasibility.RejectReason)}）　托媒费 {Display.Money(feasibility.MatchmakerFee)}"
                + $"　买功名婚姻：{(feasibility.DegreeMarriageAvailable ? $"可行，{Display.Money(feasibility.DegreeMarriageFee)}" : "不可行")}");
        }
        else
        {
            lines.Add($"婚姻：暂不可托媒（{Display.LifecycleReason(LifecycleQueries.MatchmakeFeasibility(state, person, LifecycleQueries.DirectionOf(person.Gender)).RejectReason)}）");
        }

        lines.Add("功名变迁史：");
        if (person.DegreeHistory.Count == 0)
        {
            lines.Add("　（无）");
        }
        else
        {
            foreach (var record in person.DegreeHistory)
            {
                lines.Add($"　{record.ChangedAt.Year} 年 {record.ChangedAt.Month} 月　{Display.Degree(record.Level)}"
                    + (record.Placement is { } recordPlacement ? $"（{Display.Placement(recordPlacement)}）" : string.Empty)
                    + $"　[{record.Cause}]");
            }
        }

        DetailText = string.Join(Environment.NewLine, lines);
        RefreshEndogamyCandidates();
    }

    /// <summary>
    /// 族内通婚候选（玩家反馈 1）：选中成员的候选者随选择实时刷新，
    /// XAML 用一个 ComboBox 挑人、一个按钮成婚。
    /// </summary>
    public ObservableCollection<Choice<PersonId>> EndogamyCandidates { get; } = [];

    /// <summary>族内通婚对话框里选中的候选者。</summary>
    public Choice<PersonId>? SelectedEndogamy
    {
        get => _selectedEndogamy;
        set => Set(ref _selectedEndogamy, value);
    }

    /// <summary>重建族内通婚候选列表（同代、异性、在册未婚成年）。</summary>
    private void RefreshEndogamyCandidates()
    {
        EndogamyCandidates.Clear();

        if (_session is null || Selected is null)
        {
            SelectedEndogamy = null;
            return;
        }

        var family = _session.State.Family;
        var date = _session.State.CurrentDate;
        foreach (var person in LifecycleQueries.EndogamyCandidates(family, Selected.Person, date))
        {
            EndogamyCandidates.Add(new Choice<PersonId>(
                person.Id,
                $"{person.Name}（{Display.Gender(person.Gender)}{person.AgeAt(date)} 岁 ｜ {Display.Degree(person.CurrentDegree)}）"));
        }

        if (_selectedEndogamy is null
            || !EndogamyCandidates.Any(option => option.Value == _selectedEndogamy.Value))
        {
            SelectedEndogamy = EndogamyCandidates.FirstOrDefault();
        }
    }

    // ───────────────────── 标志与月结钩子 ─────────────────────

    /// <summary>成员详情与卡片共用的标志串（需求 3：患病 / 治疗 / 已报名 / 名师 / 求子 / 入狱 …）。</summary>
    private string BuildBadgeText(Person person)
    {
        var badges = new List<string>();
        if (person.Status.HasFlag(StatusFlag.Ill)) { badges.Add("患病"); }
        if (person.Timers.TreatmentRemainingMonths is not null) { badges.Add("治疗中"); }
        if (person.Status.HasFlag(StatusFlag.ServingSentence)) { badges.Add($"入狱（余 {person.Timers.SentenceRemainingMonths ?? 0} 月）"); }
        if (person.AppliedExam is { } applied) { badges.Add($"已报{Display.ExamLevel(applied)}"); }
        if (person.IsTutoredByMaster) { badges.Add("名师"); }
        if (_childbirthPending.Contains(person.Id.Value)) { badges.Add("求子"); }
        if (person.Status.HasFlag(StatusFlag.AwaitingPost)) { badges.Add($"待阙（余 {person.Timers.AwaitingPostRemainingMonths ?? 0} 月）"); }
        if (person.Status.HasFlag(StatusFlag.ExamBanned)) { badges.Add("禁考"); }
        if (person.Status.HasFlag(StatusFlag.PromotionBanned)) { badges.Add("禁升"); }
        // 方向取词（§12.2 / §17 第七轮 Q3）：本人出嫁 ⇒「外嫁」；嫁出婚姻随女入籍的外来夫婿 ⇒「女婿」。
        if (person.Status.HasFlag(StatusFlag.MarriedOut)) { badges.Add(person.Gender == Gender.Male ? "女婿" : "外嫁"); }
        if (person.Status.HasFlag(StatusFlag.Retired)) { badges.Add("致仕"); }
        if (person.Status.HasFlag(StatusFlag.Deceased)) { badges.Add("身故"); }
        return badges.Count > 0 ? string.Join(" ｜ ", badges) : "（无）";
    }
}
