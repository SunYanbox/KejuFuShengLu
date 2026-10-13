using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Rules.Exam;

/// <summary>
/// 弃考原因（契约十三 §2、§3.3）。
/// </summary>
public enum ExamAbsenceReason
{
    /// <summary>判定期前的资格复核不通过（契约十三 §2）。</summary>
    IneligibleBeforeJudgement,

    /// <summary>省试路费不足（契约十三 §3.3）。</summary>
    InsufficientTravelCost,
}

/// <summary>一条自动报名记录（契约十三 §1.2 ①）。</summary>
/// <param name="PersonId">被自动报名者。</param>
/// <param name="Level">报名级别。</param>
public sealed record ExamApplicationLine(PersonId PersonId, ExamLevel Level);

/// <summary>一条弃考记录（契约十三 §2、§3.3）。</summary>
/// <param name="PersonId">弃考者。</param>
/// <param name="Level">级别。</param>
/// <param name="Reason">弃考原因。</param>
public sealed record ExamAbsenceLine(PersonId PersonId, ExamLevel Level, ExamAbsenceReason Reason);

/// <summary>一条判定结果（契约十三 §4、§5、§6）。</summary>
/// <param name="PersonId">被判定者。</param>
/// <param name="Level">级别。</param>
/// <param name="Passed">是否中第（免解者亦为 <c>true</c>）。</param>
/// <param name="Exempted">是否走免解路径（0 随机）。</param>
/// <param name="ImperialClass">甲第；仅殿试且中第者非空。</param>
/// <param name="Placement">一甲名次；仅一甲中第者非空。</param>
/// <param name="FailureCountAfter">本次判定后的该级累计失败数。</param>
/// <param name="DegreeAfter">本次判定后的当前功名。</param>
public sealed record ExamOutcomeLine(
    PersonId PersonId,
    ExamLevel Level,
    bool Passed,
    bool Exempted,
    ImperialClass? ImperialClass,
    ImperialPlacement? Placement,
    int FailureCountAfter,
    DegreeLevel DegreeAfter);

/// <summary>一条及第入仕记录（契约十三 §7）。</summary>
/// <param name="PersonId">入仕者。</param>
/// <param name="Track">入仕途径。</param>
/// <param name="AwaitingPostMonths">待阙剩余月数。</param>
public sealed record ExamAppointmentLine(PersonId PersonId, AppointmentTrack Track, int AwaitingPostMonths);

/// <summary>
/// 科举季结算快照（契约十三 §9）——**增量、只读、不写回** <c>GameState</c>。
/// </summary>
/// <remarks>
/// <para>
/// 它不是第二真源：全部集合按 <see cref="PersonId"/> 升序，且**只记录本月发生的事**；
/// 路费**金额不在此**（已由 <see cref="LedgerCategory.ExamTravelCost"/> 条目留痕）。
/// </para>
/// <para>
/// 非考试月：<see cref="Level"/> 为 <c>null</c>、全部集合为空、
/// <see cref="HasShiStatusBecomesTrue"/> 为 <c>false</c>。
/// </para>
/// </remarks>
public sealed record ExamSeasonSnapshot
{
    /// <summary>本次结算所属年月（结算开始时的考试月）。</summary>
    public required GameDate Month { get; init; }

    /// <summary>本月考试级别；无考试为 <c>null</c>。</summary>
    public required ExamLevel? Level { get; init; }

    /// <summary>① 自动报名记录，按 <see cref="PersonId"/> 升序。</summary>
    public required IReadOnlyList<ExamApplicationLine> AutoApplications { get; init; }

    /// <summary>② 弃考记录，按 <see cref="PersonId"/> 升序。</summary>
    public required IReadOnlyList<ExamAbsenceLine> Absences { get; init; }

    /// <summary>③ 免解者，按 <see cref="PersonId"/> 升序。</summary>
    public required IReadOnlyList<PersonId> Exemptions { get; init; }

    /// <summary>④ 判定结果，按 <see cref="PersonId"/> 升序。</summary>
    public required IReadOnlyList<ExamOutcomeLine> Outcomes { get; init; }

    /// <summary>⑤ 及第入仕记录，按 <see cref="PersonId"/> 升序。</summary>
    public required IReadOnlyList<ExamAppointmentLine> Appointments { get; init; }

    /// <summary>⑥ 特奏名询问，按 <see cref="PersonId"/> 升序。</summary>
    public required IReadOnlyList<PersonId> SpecialTributeInquiries { get; init; }

    /// <summary>本月的「首次产出进士 ⇒ 家族立仕族」是否**由本次结算**首次置真。</summary>
    public required bool HasShiStatusBecomesTrue { get; init; }

    /// <summary>非考试月的空快照（契约十三 §9）。</summary>
    /// <param name="month">结算所属年月。</param>
    /// <returns>级别为 <c>null</c>、全部集合为空的快照。</returns>
    public static ExamSeasonSnapshot None(GameDate month) => new()
    {
        Month = month,
        Level = null,
        AutoApplications = [],
        Absences = [],
        Exemptions = [],
        Outcomes = [],
        Appointments = [],
        SpecialTributeInquiries = [],
        HasShiStatusBecomesTrue = false,
    };
}
