using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Achievements;
using KFL.Rules.Career;
using KFL.Rules.Config;
using KFL.Rules.Settlement;

namespace KFL.Rules.Exam;

/// <summary>
/// 报考资格、报考级别与报名入口（规格书 §6；契约十二 §3~§6）。
/// </summary>
/// <remarks>
/// <para>
/// 本类是报考判定的**单点**：界面 MUST NOT 自行复制资格判定或日历推导（FR-018、契约十二 §7）。
/// </para>
/// <para>
/// **MUST NOT**：消耗随机、读写账本、追加功名记录、递减 / 新建 <c>StatusFlag.ExamBanned</c>
/// 或其计时（后者属逻辑轨 ⑥，本特性只消费）。
/// </para>
/// </remarks>
public static class ExamRegistration
{
    /// <summary>按当前功名派生应报考的级别（契约十二 §3.1）。</summary>
    /// <param name="person">成员。</param>
    /// <returns>应报考级别；进士无更高级别，返回 <c>null</c>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 为 <c>null</c>。</exception>
    /// <exception cref="ArgumentOutOfRangeException">当前功名不在四级之内。</exception>
    /// <remarks>
    /// 「当前功名」= 功名变迁史**末条**记录（<see cref="Person.CurrentDegree"/>）；
    /// 史为空 ⇒ 白身（FR-010）。
    /// </remarks>
    public static ExamLevel? TargetLevelOf(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        return person.CurrentDegree switch
        {
            DegreeLevel.BaiShen => ExamLevel.JieShi,
            DegreeLevel.JuRen => ExamLevel.ShengShi,
            DegreeLevel.GongShi => ExamLevel.DianShi,
            DegreeLevel.JinShi => null,
            _ => throw new ArgumentOutOfRangeException(
                nameof(person), person.CurrentDegree, "未登记的功名等级（规格书 §6 功名链共四级）。"),
        };
    }

    /// <summary>七项报考资格的合取（契约十二 §3.2）。</summary>
    /// <param name="person">成员。</param>
    /// <param name="date">判定时点（资格判定用**考试当月**的年月）。</param>
    /// <returns>全为真时为 <c>true</c>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 为 <c>null</c>。</exception>
    /// <remarks>
    /// 条件 ② **复用 <see cref="CountedMembers.IsRegistered"/>**（在册 = 未亡 ∧ 未外嫁），
    /// MUST NOT 另写一份判据（FR-002）。
    /// 「已入仕者退出科举」的旧口径（官阶非空即退出）已于 2026-10-19 裁决废止：
    /// **举人即授官**，故持有官阶者仍可继续应省试 / 殿试；退出的唯一结构性条件是
    /// 条件 ⑥（待阙位为真，即已及第入仕等待授职）与功名已到进士。
    /// 性别不构成限制、年龄无上限。
    /// </remarks>
    public static bool IsEligible(Person person, GameDate date)
    {
        ArgumentNullException.ThrowIfNull(person);

        return person.AgeAt(date) >= ExamPolicy.MinimumAge
            && CountedMembers.IsRegistered(person)
            && !person.Status.HasFlag(StatusFlag.ServingSentence)
            && !person.Status.HasFlag(StatusFlag.ExamBanned)
            && !person.Status.HasFlag(StatusFlag.AwaitingPost)
            && TargetLevelOf(person) is not null;
    }

    /// <summary>
    /// 该时点**正在开放报名**的级别（「考试当月及前一月」，2026-10-19 裁决）。
    /// </summary>
    /// <param name="date">判定时点。</param>
    /// <returns>本月是解试 / 省试月则返回本月级别；否则下月是解试 / 省试月则返回下月级别；皆非返回 <c>null</c>。</returns>
    /// <remarks>
    /// 结算在「进入考试月的那一次推进」里就把考试判完了，所以玩家真正能影响的窗口是**考试前一月**；
    /// 界面据此在考前一个月即可报名（修复「考试前触发」「进入考试月不自动报名」两个反馈）。
    /// </remarks>
    public static ExamLevel? RegisterLevelOf(GameDate date)
    {
        var current = ExamCalendar.LevelOf(date);

        if (current is ExamLevel.JieShi or ExamLevel.ShengShi)
        {
            return current;
        }

        var next = ExamCalendar.LevelOf(date.NextMonth());

        return next is ExamLevel.JieShi or ExamLevel.ShengShi ? next : null;
    }

    /// <summary>该月该人能否报名（契约十二 §3.3）。</summary>
    /// <param name="person">成员。</param>
    /// <param name="date">判定时点。</param>
    /// <returns>报名窗口开在该人应考级别 ∧ 合格时为 <c>true</c>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 为 <c>null</c>。</exception>
    /// <remarks>殿试恒为 <c>false</c>：殿试不需报名（契约十二 §4 条款 3）。</remarks>
    public static bool CanRegister(Person person, GameDate date)
    {
        ArgumentNullException.ThrowIfNull(person);

        var level = RegisterLevelOf(date);

        return level is ExamLevel.JieShi or ExamLevel.ShengShi
            && IsEligible(person, date)
            && TargetLevelOf(person) == level;
    }

    /// <summary>报名（写 <see cref="Person.AppliedExam"/>；契约十二 §4）。</summary>
    /// <param name="person">成员。</param>
    /// <param name="date">报名时点（= 考试当月）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 为 <c>null</c>。</exception>
    /// <exception cref="InvalidOperationException">
    /// 窗口外（非考试月，或考试月但不是本人级别 / 不合格）。
    /// </exception>
    /// <remarks>
    /// **幂等**：同一人同一级别重复报名 MUST 无副作用（不抛、不改字段）。
    /// 报名 MUST NOT 消耗随机、MUST NOT 读写账本、MUST NOT 追加功名记录。
    /// </remarks>
    public static void Register(Person person, GameDate date)
    {
        if (!CanRegister(person, date))
        {
            throw new InvalidOperationException(
                $"报名窗口外：{date} 不是该成员的报名月（契约十二 §4 条款 1、2）。");
        }

        var level = TargetLevelOf(person)!.Value;

        if (person.AppliedExam == level)
        {
            return;
        }

        person.ApplyForExam(level);
    }

    /// <summary>该考试月**合格且未报名**者名单（契约十二 §5；FR-018③）。</summary>
    /// <param name="family">家族。</param>
    /// <param name="date">考试当月。</param>
    /// <returns>按 <see cref="PersonId"/> 升序的只读名单；非解试 / 省试月或殿试月为空表。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="family"/> 为 <c>null</c>。</exception>
    /// <remarks>纯查询：调用前后家族、成员、账本与随机状态逐字段相同。</remarks>
    public static IReadOnlyList<Person> EligibleUnapplied(Family family, GameDate date)
    {
        ArgumentNullException.ThrowIfNull(family);

        var level = RegisterLevelOf(date);

        if (level is not (ExamLevel.JieShi or ExamLevel.ShengShi))
        {
            return [];
        }

        return family.Members
            .Where(person => person.AppliedExam is null
                && IsEligible(person, date)
                && TargetLevelOf(person) == level)
            .OrderBy(person => person.Id.Value)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>特奏名资格（契约十二 §6.2 条款 1）。</summary>
    /// <param name="person">成员。</param>
    /// <param name="date">判定时点。</param>
    /// <returns>满 50 岁 ∧ 省试累计失败数 <c>&gt;= 6</c> ∧ 无官阶 ∧ 非待阙时为 <c>true</c>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 为 <c>null</c>。</exception>
    public static bool SpecialTributeEligible(Person person, GameDate date)
    {
        ArgumentNullException.ThrowIfNull(person);

        return person.AgeAt(date) >= ExamPolicy.SpecialTributeMinimumAge
            && person.ShengShiFailureCount >= ExamPolicy.SpecialTributeFailureThreshold
            && person.Rank is null
            && !person.Status.HasFlag(StatusFlag.AwaitingPost);
    }

    /// <summary>接受特奏名：置入待阙（契约十二 §6.2 条款 4、5）。</summary>
    /// <param name="person">成员。</param>
    /// <param name="date">接受时点。</param>
    /// <param name="random">随机来源（待阙时长的唯一来源，经 003 的通用入口）。</param>
    /// <param name="state">
    /// 当前对局（008 起可选）。传入时在成功应答后即时判定 <c>ExamInquiryAnswered</c>
    /// （契约二十二 §3 第 11 行）；为 <c>null</c> 时行为与 005 原签名逐字段相同。
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 或 <paramref name="random"/> 为 <c>null</c>。</exception>
    /// <exception cref="InvalidOperationException">不满足资格。</exception>
    /// <remarks>
    /// **功名变迁史不新增任何记录**（MUST NOT 伪造进士记录）；接受后退出科举由
    /// <see cref="IsEligible"/> 的条件 ⑤、⑥ **结构性**保证。
    /// <para>
    /// 008（T055 / E-20）：成功应答即上报，**不影响**本节既有资格判定与随机次序——即时判定
    /// MUST NOT 落全局文件、MUST NOT 写档、MUST NOT 抛（契约二十二 §3 条款 10）。
    /// </para>
    /// </remarks>
    public static void AcceptSpecialTribute(
        Person person,
        GameDate date,
        IRandomService random,
        GameState? state = null)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (!SpecialTributeEligible(person, date))
        {
            throw new InvalidOperationException(
                "不满足特奏名资格：满 50 岁 ∧ 省试累计失败数 >= 6 ∧ 无官阶 ∧ 非待阙（契约十二 §6.2）。");
        }

        AppointmentEntry.Begin(person, AppointmentTrack.SpecialTribute, date, random);

        // 008 接线（T055）：成功应答后即时判定（不落文件、不写档、不抛）。
        if (state is not null)
        {
            AchievementAdjudicator.Report(state, AchievementTrigger.ExamInquiryAnswered);
        }
    }
}
