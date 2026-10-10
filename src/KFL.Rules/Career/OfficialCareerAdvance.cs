using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;
using KFL.Rules.Settlement;

namespace KFL.Rules.Career;

/// <summary>
/// 仕途月度推进（规格书 §8.2；FR-015~FR-020；data-model §3.8；契约七 §2/§4/§6）。
/// </summary>
/// <remarks>
/// <para>
/// <b>成员集合</b>取 002 的**在册**口径（<see cref="CountedMembers.Registered"/>：未亡且未外嫁，
/// **含服刑与待阙**），一律按 <see cref="PersonId"/> **升序**处理——故**已亡与外嫁者 MUST NOT 被推进**
/// （其仕途随归档停止），服刑者仍在集合内、由 ③-a/③-b/③-d 的**显式暂停**处理。
/// </para>
/// <para>
/// <b>月内次序</b>（契约七 §2 条款 2~4，逐条可断言）：③-a 待阙递减并授官 → ③-b 政绩 +1 →
/// ③-c 致仕判定 → ③-d 在职计时 +1 与考课判定。故：当月授官者当月即计入在职月数并领全俸；
/// 满 70 岁当月仍有政绩 +1（③-b 先于 ③-c），但 MUST NOT 参与考课（③-c 先于 ③-d）。
/// </para>
/// <para>
/// <b>服刑全程暂停</b>（Q5 裁决）：其待阙计时、在职计时与政绩**不推进，也不重置**（刑满后从暂停处继续）；
/// 它与<b>禁升</b>的「跳过到期判定并重置计时」是**两种语义**，快照里分别记
/// <see cref="CareerAdvanceResult.AppraisalPaused"/> 与 <see cref="CareerAdvanceResult.AppraisalSkipped"/>。
/// </para>
/// <para>
/// <b>本特性只消费</b> <c>PromotionBanned</c> 与 <c>ServingSentence</c>：MUST NOT 递减、新建或清除
/// <c>PromotionBanRemainingMonths</c>（逻辑轨 ⑥）。
/// </para>
/// </remarks>
public static class OfficialCareerAdvance
{
    /// <summary>推进一个月（就地改写成员档案），并返回本次的增量快照。</summary>
    /// <param name="family">家族（推进对象的唯一来源）。</param>
    /// <param name="month">归属年月（年龄与致仕判定以它为准）。</param>
    /// <param name="random">随机来源（仅考课判定会消费，且每人至多 1 次 <c>NextDouble</c>）。</param>
    /// <returns>本次推进的增量快照。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="family"/> 或 <paramref name="random"/> 为 <c>null</c>。</exception>
    public static CareerAdvanceResult Run(Family family, GameDate month, IRandomService random)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(random);

        var appointments = new List<CareerAppointment>();
        var meritGains = new Dictionary<PersonId, int>();
        var promotions = new List<CareerPromotion>();
        var retirements = new List<PersonId>();
        var appraisalSkipped = new List<PersonId>();
        var appraisalPaused = new List<PersonId>();
        var salaryModes = new Dictionary<PersonId, SalaryMode>();

        foreach (var person in CountedMembers.Registered(family).OrderBy(p => p.Id.Value))
        {
            var mode = SalaryModePolicy.Of(person);
            var servingSentence = person.Status.HasFlag(StatusFlag.ServingSentence);

            // ③-a 待阙递减；递减到 0 的当月授官（服刑者暂停：不递减、不清位）。
            if (mode == SalaryMode.AwaitingPost && !servingSentence && TryAppoint(person, out var appointment))
            {
                appointments.Add(appointment);
                mode = SalaryModePolicy.Of(person);
            }

            // ③-b 政绩 +1（在任者；封顶 MeritMaximum；服刑者暂停）。
            if (mode == SalaryMode.Active && !servingSentence)
            {
                var gained = AddMerit(person);

                if (gained > 0)
                {
                    meritGains[person.Id] = gained;
                }
            }

            // ③-c 致仕（在任 ∧ 满 RetirementAge ⇒ 置 Retired，官阶保留；幂等）。
            if (mode == SalaryMode.Active && person.AgeAt(month) >= OfficialCareerPolicy.RetirementAge)
            {
                person.Status |= StatusFlag.Retired;
                retirements.Add(person.Id);
                mode = SalaryModePolicy.Of(person);
            }

            // ③-d 在职计时 +1 与考课判定（致仕者与满龄者不参与；服刑者暂停）。
            if (mode == SalaryMode.Active && person.AgeAt(month) < OfficialCareerPolicy.RetirementAge)
            {
                AdvanceOfficeTimer(person, random, promotions, appraisalSkipped, appraisalPaused);
            }

            salaryModes[person.Id] = SalaryModePolicy.Of(person);
        }

        return new CareerAdvanceResult
        {
            Appointments = appointments,
            MeritGains = meritGains,
            Promotions = promotions,
            Retirements = retirements,
            AppraisalSkipped = appraisalSkipped,
            AppraisalPaused = appraisalPaused,
            SalaryModes = salaryModes,
        };
    }

    /// <summary>③-a：待阙计时递减 1；递减到 0 的当月授官。</summary>
    /// <param name="person">待阙中的成员。</param>
    /// <param name="appointment">授官记录；本月未授官为 <c>null</c>。</param>
    /// <returns>本月是否发生了授官。</returns>
    /// <remarks>
    /// 途径**只读** <see cref="Person.EntryTrack"/>（入口写入的入仕途径），MUST NOT 从功名记录重新派生
    /// ——见 <see cref="AppointmentEntry.TrackOf"/> 的注记。
    /// </remarks>
    private static bool TryAppoint(Person person, out CareerAppointment appointment)
    {
        appointment = null!;

        // 「位为真而计时或途径缺失」在类型层可表达，但本特性不建立该形态（入口必定成对写入）：
        // 无计时可递减、或无途径可授官，故不推进——不猜一个月数、也不猜一个途径，也不清位。
        if (person.EntryTrack is not { } track
            || person.Timers.AwaitingPostRemainingMonths is not { } remaining)
        {
            return false;
        }

        if (remaining > 1)
        {
            person.Timers = AppointmentEntry.ReplaceAwaitingPost(person.Timers, remaining - 1);
            return false;
        }

        // remaining <= 1：本月的递减归零（或已是 0 的残留）⇒ 当月授官。
        var rank = new OfficialRank(OfficialCareerPolicy.InitialRankOf(track));

        person.Rank = rank;
        person.MonthsInOffice = 0;

        // 先清计时与途径、后清位（Person 的交叉校验方向）。
        person.Timers = AppointmentEntry.ReplaceAwaitingPost(person.Timers, null);
        person.EntryTrack = null;
        person.Status &= ~StatusFlag.AwaitingPost;

        appointment = new CareerAppointment(person.Id, track, rank);
        return true;
    }

    /// <summary>③-b：政绩 +1，封顶 <see cref="OfficialCareerPolicy.MeritMaximum"/>；返回实际增量。</summary>
    private static int AddMerit(Person person)
    {
        var before = person.Merit;
        var after = Math.Min(before + OfficialCareerPolicy.MeritPerMonth, OfficialCareerPolicy.MeritMaximum);

        person.Merit = after;
        return after - before;
    }

    /// <summary>③-d：在职计时 +1 与考课判定；服刑者暂停（不推进、不重置）。</summary>
    private static void AdvanceOfficeTimer(
        Person person,
        IRandomService random,
        List<CareerPromotion> promotions,
        List<PersonId> appraisalSkipped,
        List<PersonId> appraisalPaused)
    {
        if (person.Status.HasFlag(StatusFlag.ServingSentence))
        {
            // 服刑 = 暂停：计时未重置，刑满后从暂停处继续（与「禁升跳过」不是同一语义）。
            if (person.MonthsInOffice >= OfficialCareerPolicy.AppraisalPeriodMonths)
            {
                appraisalPaused.Add(person.Id);
            }

            return;
        }

        person.MonthsInOffice += 1;

        if (person.MonthsInOffice < OfficialCareerPolicy.AppraisalPeriodMonths)
        {
            return;
        }

        if (person.Status.HasFlag(StatusFlag.PromotionBanned))
        {
            // 禁升 = 跳过：不掷骰（MUST NOT 消耗随机）、不升迁、不补判，但计时照常重置。
            appraisalSkipped.Add(person.Id);
        }
        else if (TryPromote(person, random, out var promotion))
        {
            promotions.Add(promotion);
        }

        person.MonthsInOffice = 0;
    }

    /// <summary>考课掷骰：恰好 1 次 <c>NextDouble</c>；成功且未到最高品时级数 −1。</summary>
    private static bool TryPromote(Person person, IRandomService random, out CareerPromotion promotion)
    {
        promotion = null!;

        var current = person.Rank!.Value;
        var roll = (decimal)random.NextDouble();

        if (roll >= OfficialCareerPolicy.PromotionChance(person.Merit))
        {
            return false;
        }

        // 成功但已在最高品：维持 L1，MUST NOT 越界（也不登记「晋升」）。
        if (current.Level <= SalaryTable.HighestLevel)
        {
            return false;
        }

        var next = new OfficialRank(current.Level - 1);
        person.Rank = next;
        promotion = new CareerPromotion(person.Id, current, next);
        return true;
    }
}
