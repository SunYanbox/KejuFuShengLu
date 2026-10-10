using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Rules.Career;

/// <summary>一次授官的记录（成员 → 入仕途径 → 新官阶）。</summary>
/// <param name="PersonId">被授官的成员。</param>
/// <param name="Track">入仕途径（入口记录在 <c>Person.EntryTrack</c> 上的值；特奏名无进士记录）。</param>
/// <param name="Rank">新官阶。</param>
public sealed record CareerAppointment(PersonId PersonId, AppointmentTrack Track, OfficialRank Rank);

/// <summary>一次晋升的记录（旧级 → 新级；新级数值更小 = 更高品）。</summary>
/// <param name="PersonId">被晋升的成员。</param>
/// <param name="From">晋升前的官阶。</param>
/// <param name="To">晋升后的官阶。</param>
public sealed record CareerPromotion(PersonId PersonId, OfficialRank From, OfficialRank To);

/// <summary>
/// 一次官吏推进的**增量快照**（data-model §3.9；契约七 §9）。
/// </summary>
/// <remarks>
/// <para>
/// 它是**增量**、不是第二真源：MUST NOT 存「当前官阶表」之类的副本，成员的真状态只在
/// <c>Person</c> 上（R-15）。全部集合按 <see cref="PersonId"/> 升序，便于逐条断言。
/// </para>
/// <para>
/// <b>两种「不判定」MUST NOT 合并</b>：<see cref="AppraisalSkipped"/> = 因**禁升**跳过到期判定、
/// 计时**已重置**为 0；<see cref="AppraisalPaused"/> = 因**服刑**暂停、计时**未重置**
/// （刑满后从暂停处继续）。二者是不同语义（契约七 §4 条款 7）。
/// </para>
/// </remarks>
public sealed class CareerAdvanceResult
{
    /// <summary>本次授官；无授官时为空列表。</summary>
    public required IReadOnlyList<CareerAppointment> Appointments { get; init; }

    /// <summary>本次政绩增量（仅登记**实际增长**的成员；封顶时不留条目）。</summary>
    public required IReadOnlyDictionary<PersonId, int> MeritGains { get; init; }

    /// <summary>本次晋升；无晋升时为空列表。</summary>
    public required IReadOnlyList<CareerPromotion> Promotions { get; init; }

    /// <summary>本次致仕的成员；无致仕时为空列表。</summary>
    public required IReadOnlyList<PersonId> Retirements { get; init; }

    /// <summary>因**禁升**跳过到期判定者（计时已重置为 0）。</summary>
    public required IReadOnlyList<PersonId> AppraisalSkipped { get; init; }
    /// <summary>因**服刑**暂停者（计时未重置）。</summary>
    public required IReadOnlyList<PersonId> AppraisalPaused { get; init; }

    /// <summary>推进**结束后**每名被处理成员的三态（含无官职者）。</summary>
    public required IReadOnlyDictionary<PersonId, SalaryMode> SalaryModes { get; init; }
}
