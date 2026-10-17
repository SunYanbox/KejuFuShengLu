using KFL.Core.Enums;

namespace KFL.Core.ValueObjects;

/// <summary>
/// 待应答的婚配提议**完整快照**（规格书 §9.1 随机提亲、§9.2 榜下捉婿；FR-044）。
/// </summary>
/// <remarks>
/// <para>
/// 提议**不是** <see cref="Entities.Person"/>，MUST NOT 进 <c>Family.Members</c>：
/// 候选者尚未入册，只有玩家应答（<c>LifecycleEntry.RespondToProposal</c>）后才落成成员。
/// 快照字段因此必须自带全部初始化结果——否则无法在应答时复现同一个候选者。
/// </para>
/// <para>
/// **时效**：提议**当月未应答即失效**。这里没有状态位，判据是
/// <c>ProposedAt</c> 的年月是否等于当月（<c>LifecycleQueries.PendingProposal</c> 表达）。
/// </para>
/// <para>
/// <see cref="CandidateLifespan"/> 只 clamp 下界，无上界（与 <c>Person.Lifespan</c> 同款）。
/// </para>
/// </remarks>
public sealed record MarriageProposal
{
    /// <summary>提议来源（⑧-a 的两段）。</summary>
    public required MarriageProposalKind Kind { get; init; }

    /// <summary>我方参与婚配的成员（提议的应答对象）。</summary>
    public required PersonId OurMemberId { get; init; }

    /// <summary>候选者姓名（已冠家族姓氏）。</summary>
    public required string CandidateName { get; init; }

    /// <summary>候选者性别。</summary>
    public required Gender CandidateGender { get; init; }

    /// <summary>候选者年龄（提出时的整岁）。</summary>
    public required int CandidateAge { get; init; }

    /// <summary>候选者农学天赋。</summary>
    public required int TalentAgriculture { get; init; }

    /// <summary>候选者商学天赋。</summary>
    public required int TalentCommerce { get; init; }

    /// <summary>候选者仕学天赋。</summary>
    public required int TalentScholar { get; init; }

    /// <summary>候选者工学天赋。</summary>
    public required int TalentCraft { get; init; }

    /// <summary>候选者学业。</summary>
    public required int CandidateStudy { get; init; }

    /// <summary>候选者体质。</summary>
    public required int CandidateHealth { get; init; }

    /// <summary>候选者天命寿数（只 clamp 下界）。</summary>
    public required int CandidateLifespan { get; init; }

    /// <summary>候选者功名（提亲自带白身；捉婿按官员压力分档）。</summary>
    public required DegreeLevel CandidateDegree { get; init; }

    /// <summary>对方阶层（决定聘礼 / 嫁妆档位）。</summary>
    public required MarriageClass Class { get; init; }

    /// <summary>聘礼金额（我方应付给对方；捉婿为 <c>0</c>）。</summary>
    public required Money BetrothalGift { get; init; }

    /// <summary>嫁妆金额（对方应付给我方）。</summary>
    public required Money Dowry { get; init; }

    /// <summary>提议提出时的年月（当月未应答即失效）。</summary>
    public required GameDate ProposedAt { get; init; }

    /// <summary>候选者的入仕途径；仅当自带功名时有值（捉婿档）。</summary>
    public ImperialPlacement? CandidatePlacement { get; init; }
}
