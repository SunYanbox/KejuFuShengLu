using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Bribery;
using KFL.Rules.Career;
using KFL.Rules.Config;

namespace KFL.Rules.Exam;

/// <summary>
/// 科举季结算步（规格书 §6、§7.1、§7.2；契约十三）——唯一入口
/// <see cref="Run(GameState, IRandomService)"/>。
/// </summary>
/// <remarks>
/// <para>
/// 本步是 <c>MonthlySettlementEngine.Settle</c> 的**第 ⑦ 步**，位于「⑥ 贷款」之后、
/// 「⑧ 推进时间（<see cref="GameState.AdvanceMonth"/>）」之前，因此判定用的是**考试当月**的
/// 日期与当月值（学业、仕天赋、难度、现金）。本步 MUST NOT 调用
/// <see cref="GameState.AdvanceMonth"/>、MUST NOT 修改 <see cref="GameState.CurrentDate"/>
/// （契约十三 §1.1）。
/// </para>
/// <para>
/// **步内固定次序**（契约十三 §1.2，同一段内一律按 <see cref="PersonId"/> 升序）：
/// ① 自动报名（仅当 <see cref="Family.AutoExamApplication"/> 为真；仅解试 / 省试月）
/// → 判定前资格复核（§2）→ ② 路费扣除与弃考（仅省试月）
/// → ③ 免解行使（仅解试月）→ ④ 录取 / 进甲判定 → ⑤ 及第入仕（仅殿试月）
/// → ⑥ 特奏名询问（仅省试月）。
/// </para>
/// <para>
/// **候选集**：解试 / 省试月 = 持有「本月级别」报名标记者（含 ① 刚自动报名的）；
/// 殿试月 = **无需报名**，由省试中第者（新晋贡士）与既有贡士自动参加（契约十二 §4 条款 3），
/// 故取「本月合格且 <see cref="ExamRegistration.TargetLevelOf"/> 为殿试」者。
/// </para>
/// <para>
/// **可变范围严格限定为**：<see cref="Person"/>{<see cref="Person.ApplyForExam"/> /
/// <see cref="Person.ClearExamApplication"/> / <see cref="Person.RecordExamFailure"/> /
/// <see cref="Person.AppendDegree"/>}、<c>Family.HasShiStatus</c>、
/// <see cref="FamilyEconomy.Apply"/>（仅 <see cref="LedgerCategory.ExamTravelCost"/>）
/// 与 <see cref="AppointmentEntry"/> 入口。MUST NOT 写 <see cref="Person.Rank"/> /
/// <see cref="Person.MonthsInOffice"/> / 储蓄 / 贷款 / 商本池（FR-020）。
/// </para>
/// <para>
/// **随机**全部经注入的 <see cref="IRandomService"/>；MUST NOT 使用 <c>System.Random</c> 或任何时钟。
/// 次序见契约十三 §1.3：报名 0 次；被跳过者（资格不符 / 现金不足 / 免解）0 次；
/// ④ 每人 1 次 <c>NextDouble()</c>（录取或进甲），进甲者**紧接** 1 次 <c>Next(1, 4)</c>；
/// ⑤ 每人 1 次 <c>Next(6, 25)</c>（经 <see cref="AppointmentEntry"/>）。
/// </para>
/// </remarks>
public static class ExamSeasonRunner
{
    /// <summary>执行科举季结算步并返回增量快照。</summary>
    /// <param name="state">游戏状态（就地修改）。</param>
    /// <param name="random">随机来源。</param>
    /// <returns>本月科举季的增量快照；非考试月为 <see cref="ExamSeasonSnapshot.None"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 或 <paramref name="random"/> 为 <c>null</c>。</exception>
    /// <remarks>
    /// 非考试月：①②③④⑤⑥ 全部跳过，随机消费 **0** 次，状态变更 **0** 处。
    /// 无论中第 / 落第 / 免解 / 弃考 / 资格不符，本月结算结束后
    /// <see cref="Person.AppliedExam"/> 必为 <c>null</c>。
    /// </remarks>
    public static ExamSeasonSnapshot Run(GameState state, IRandomService random)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(random);

        var month = state.CurrentDate;
        var level = ExamCalendar.LevelOf(month);

        if (level is null)
        {
            return ExamSeasonSnapshot.None(month);
        }

        var examLevel = level.Value;
        var autoApplications = new List<ExamApplicationLine>();
        var absences = new List<ExamAbsenceLine>();
        var exemptions = new List<PersonId>();
        var outcomes = new List<ExamOutcomeLine>();
        var appointments = new List<ExamAppointmentLine>();
        var inquiries = new List<PersonId>();

        // ① 自动报名：名单按 PersonId 升序；0 随机、0 账本条目；MUST NOT 覆盖已有标记。
        if (state.Family.AutoExamApplication
            && examLevel is ExamLevel.JieShi or ExamLevel.ShengShi)
        {
            foreach (var person in ExamRegistration.EligibleUnapplied(state.Family, month))
            {
                ExamRegistration.Register(person, month);
                autoApplications.Add(new ExamApplicationLine(person.Id, examLevel));
            }
        }

        var candidates = CandidatesOf(state.Family, examLevel, month);

        // §2 判定前复核（在 ② / ④ 之前，故先扣费后复核的顺序不成立）。
        RemoveIneligible(candidates, examLevel, month, absences);

        // ② 路费扣除与弃考（仅省试月）。
        if (examLevel == ExamLevel.ShengShi)
        {
            ChargeTravelCost(state, candidates, examLevel, month, absences);
        }

        // ③ 免解行使（仅解试月；必须在 ④ 掷骰之前判定，否则会多消耗一次随机）。
        if (examLevel == ExamLevel.JieShi)
        {
            foreach (var person in candidates)
            {
                if (person.JieShiFailureCount >= ExamPolicy.JieShiExemptionFailureThreshold)
                {
                    exemptions.Add(person.Id);
                }
            }
        }

        var exempted = exemptions.ToHashSet();

        // ④ 录取 / 进甲判定：全部人员的判定 MUST 先完成，然后才进入 ⑤。
        foreach (var person in candidates)
        {
            outcomes.Add(Judge(person, examLevel, month, state.Family.HasShiStatus, random, exempted.Contains(person.Id)));
            person.ClearExamApplication();
        }

        // ⑤ 及第入仕（仅殿试月）。
        if (examLevel == ExamLevel.DianShi)
        {
            foreach (var person in candidates)
            {
                if (person.Rank is not null || person.Status.HasFlag(StatusFlag.AwaitingPost))
                {
                    continue;
                }

                AppointmentEntry.BeginForImperialGraduate(person, month, random);
                appointments.Add(new ExamAppointmentLine(
                    person.Id,
                    person.EntryTrack!.Value,
                    person.Timers.AwaitingPostRemainingMonths!.Value));
            }
        }

        // ⑥ 特奏名询问（仅省试月；对本月落第者，产出不改变任何状态）。
        if (examLevel == ExamLevel.ShengShi)
        {
            foreach (var outcome in outcomes)
            {
                if (outcome.Passed)
                {
                    continue;
                }

                var person = state.Family.TryGet(outcome.PersonId);

                if (person is not null && ExamRegistration.SpecialTributeEligible(person, month))
                {
                    inquiries.Add(outcome.PersonId);
                }
            }
        }

        // ④⑤ 之后按 FR-020 置仕族：家族首次产出进士 ⇒ 置真（幂等，仅首次在快照里记为真）。
        var hasShiStatusBecomesTrue = false;

        if (examLevel == ExamLevel.DianShi
            && outcomes.Any(outcome => outcome.Passed)
            && !state.Family.HasShiStatus)
        {
            state.Family.HasShiStatus = true;
            hasShiStatusBecomesTrue = true;
        }

        // 报名标记是**考试当月态**：本月结算结束后必为 null。
        ClearResidualApplications(state.Family);

        return new ExamSeasonSnapshot
        {
            Month = month,
            Level = examLevel,
            AutoApplications = autoApplications.AsReadOnly(),
            Absences = absences.AsReadOnly(),
            Exemptions = exemptions.AsReadOnly(),
            Outcomes = outcomes.AsReadOnly(),
            Appointments = appointments.AsReadOnly(),
            SpecialTributeInquiries = inquiries.AsReadOnly(),
            HasShiStatusBecomesTrue = hasShiStatusBecomesTrue,
        };
    }

    /// <summary>
    /// 候选集（契约十三 §1.2 条款 3；契约十二 §4 条款 3）：一律按 <see cref="PersonId"/> 升序。
    /// </summary>
    private static List<Person> CandidatesOf(Family family, ExamLevel level, GameDate month)
    {
        if (level == ExamLevel.DianShi)
        {
            // 殿试不需报名：由省试中第者与既有贡士自动参加。
            return family.Members
                .Where(person => ExamRegistration.IsEligible(person, month)
                    && ExamRegistration.TargetLevelOf(person) == ExamLevel.DianShi)
                .OrderBy(person => person.Id.Value)
                .ToList();
        }

        return family.Members
            .Where(person => person.AppliedExam == level)
            .OrderBy(person => person.Id.Value)
            .ToList();
    }

    /// <summary>判定前复核（契约十三 §2）：不合格者跳过并清除标记，失败数不变。</summary>
    private static void RemoveIneligible(
        List<Person> candidates, ExamLevel level, GameDate month, List<ExamAbsenceLine> absences)
    {
        List<Person>? removed = null;

        foreach (var person in candidates)
        {
            if (ExamRegistration.IsEligible(person, month)
                && ExamRegistration.TargetLevelOf(person) == level)
            {
                continue;
            }

            person.ClearExamApplication();
            absences.Add(new ExamAbsenceLine(person.Id, level, ExamAbsenceReason.IneligibleBeforeJudgement));
            (removed ??= []).Add(person);
        }

        if (removed is null)
        {
            return;
        }

        foreach (var person in removed)
        {
            candidates.Remove(person);
        }
    }

    /// <summary>省试路费扣除与弃考（契约十三 §3）：门槛 = 现金，扣付必然只动现金。</summary>
    private static void ChargeTravelCost(
        GameState state,
        List<Person> candidates,
        ExamLevel level,
        GameDate month,
        List<ExamAbsenceLine> absences)
    {
        var cost = Money.FromGuan(ExamPolicy.ExamTravelCostGuan(state.Difficulty));
        List<Person>? absent = null;

        foreach (var person in candidates)
        {
            if (state.Economy.Treasury.Cash < cost)
            {
                person.ClearExamApplication();
                absences.Add(new ExamAbsenceLine(person.Id, level, ExamAbsenceReason.InsufficientTravelCost));
                (absent ??= []).Add(person);
                continue;
            }

            state.Economy.Apply(LedgerCategory.ExamTravelCost, person.Id, -cost, month);
        }

        if (absent is null)
        {
            return;
        }

        foreach (var person in absent)
        {
            candidates.Remove(person);
        }
    }

    /// <summary>逐人判定（契约十三 §4~§6）：恰好 1 次录取 / 进甲掷骰，进甲者紧接 1 次名次掷骰。</summary>
    private static ExamOutcomeLine Judge(
        Person person,
        ExamLevel level,
        GameDate month,
        bool hasShiStatus,
        IRandomService random,
        bool exempted)
    {
        switch (level)
        {
            // ③ 免解：0 随机，直接记为通过（不重复追加已有功名）。
            case ExamLevel.JieShi when exempted:
                AppendDegreeIfBelow(person, DegreeLevel.JuRen, null, null, month);
                return new ExamOutcomeLine(
                    person.Id, level, true, true, null, null, person.JieShiFailureCount, person.CurrentDegree);

            case ExamLevel.JieShi:
            case ExamLevel.ShengShi:
            {
                var rate = ExamAdmission.AdmissionRate(
                    level, person, hasShiStatus, BribeRules.MultiplierOf(level, person));
                var passed = (decimal)random.NextDouble() < rate;
                var target = level == ExamLevel.JieShi ? DegreeLevel.JuRen : DegreeLevel.GongShi;

                if (passed)
                {
                    person.AppendDegree(
                        new DegreeRecord(target, null, month, DegreeChangeCause.ExamPass, null));
                }
                else
                {
                    person.RecordExamFailure(level);
                }

                var failureCountAfter = level == ExamLevel.JieShi
                    ? person.JieShiFailureCount
                    : person.ShengShiFailureCount;

                return new ExamOutcomeLine(
                    person.Id, level, passed, false, null, null, failureCountAfter, person.CurrentDegree);
            }

            case ExamLevel.DianShi:
            {
                // 殿试录取不掷骰：进殿试者 100% 成为进士（Q1 裁决），只掷进甲一次。
                var promoted = (decimal)random.NextDouble() < ExamAdmission.ImperialPromotionRate(
                    person, BribeRules.MultiplierOf(ExamLevel.DianShi, person));
                var imperialClass = ExamAdmission.ImperialClassFor(person, promoted);
                ImperialPlacement? placement = promoted ? ExamAdmission.RollPlacement(random) : null;

                person.AppendDegree(
                    new DegreeRecord(DegreeLevel.JinShi, placement, month, DegreeChangeCause.ExamPass, imperialClass));

                // 殿试无失败概念，该级累计失败数恒 0（data-model §3.5）。
                return new ExamOutcomeLine(
                    person.Id, level, true, false, imperialClass, placement, 0, person.CurrentDegree);
            }

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(level), level, "未登记的考试级别（规格书 §6 共三级）。");
        }
    }

    /// <summary>仅当当前功名**低于**目标时追加（契约十三 §5 条款 4：不重复追加）。</summary>
    private static void AppendDegreeIfBelow(
        Person person,
        DegreeLevel level,
        ImperialClass? imperialClass,
        ImperialPlacement? placement,
        GameDate month)
    {
        if (person.CurrentDegree >= level)
        {
            return;
        }

        person.AppendDegree(new DegreeRecord(level, placement, month, DegreeChangeCause.ExamPass, imperialClass));
    }

    /// <summary>考试月结算收尾：报名标记是当月态，本月结束后必为 <c>null</c>。</summary>
    private static void ClearResidualApplications(Family family)
    {
        foreach (var person in family.Members)
        {
            if (person.AppliedExam is not null)
            {
                person.ClearExamApplication();
            }
        }
    }
}
