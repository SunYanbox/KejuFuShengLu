using KFL.Core.ValueObjects;

namespace KFL.Rules.Punishment;

/// <summary>
/// 某一次**惩罚推进**（结算第③步）产出的当月事实快照（契约十六 §9；data-model §4.2 第③步）。
/// </summary>
/// <remarks>
/// <para>
/// 快照是**只读的当月事实**：它不承载任何规则数值，也**不回写** <c>GameState</c>
/// （data-model 不变量 I-18）。构造完成之后各集合不再变化。
/// </para>
/// <para>
/// 六个集合**按 <c>PersonId</c> 升序**（并列时按级别 / 亲属 id），与
/// <c>PunishmentJournal</c> 的追加序一致；集合为空时用 <see cref="Array.Empty{T}"/>。
/// </para>
/// </remarks>
public sealed record PunishmentSnapshot
{
    /// <summary>快照所属年月（= 推进当刻的 <c>GameState.CurrentDate</c>）。</summary>
    public required GameDate Month { get; init; }

    /// <summary>当月推进读到的行贿事实（来自家族日志的投影）。</summary>
    public required IReadOnlyList<BribeFact> Bribes { get; init; }

    /// <summary>当月推进读到的服刑事实。</summary>
    public required IReadOnlyList<SentenceFact> Sentences { get; init; }

    /// <summary>当月推进读到的罚金事实。</summary>
    public required IReadOnlyList<FineFact> Fines { get; init; }

    /// <summary>当月推进读到的亲属禁考 / 禁升事实。</summary>
    public required IReadOnlyList<RelativeBanFact> RelativeBans { get; init; }

    /// <summary>当月推进读到的连坐事实。</summary>
    public required IReadOnlyList<CollectivePunishmentFact> CollectivePunishments { get; init; }

    /// <summary>当月推进读到的释放事实。</summary>
    public required IReadOnlyList<ReleaseFact> Releases { get; init; }

    /// <summary>本族当月是否被宣告全族禁考禁升（来自日志的 <c>FamilyBanDeclared</c>）。</summary>
    public required bool FamilyBanDeclared { get; init; }

    /// <summary>当月推进结束时仍在服刑的在册人数。</summary>
    public required int PrisonerCount { get; init; }

    /// <summary>取「当月无任何惩罚事实」的空快照。</summary>
    /// <param name="month">归属年月。</param>
    /// <returns>六个集合为空、<see cref="FamilyBanDeclared"/> 为 <c>false</c>、<see cref="PrisonerCount"/> 为 <c>0</c> 的快照。</returns>
    public static PunishmentSnapshot None(GameDate month) => new()
    {
        Month = month,
        Bribes = Array.Empty<BribeFact>(),
        Sentences = Array.Empty<SentenceFact>(),
        Fines = Array.Empty<FineFact>(),
        RelativeBans = Array.Empty<RelativeBanFact>(),
        CollectivePunishments = Array.Empty<CollectivePunishmentFact>(),
        Releases = Array.Empty<ReleaseFact>(),
        FamilyBanDeclared = false,
        PrisonerCount = 0,
    };
}
