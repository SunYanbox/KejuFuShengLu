// 家族类存档 DTO：夫妇键、待分娩、婚姻提议、托媒申请与生命周期簿记。

using System.Text.Json.Serialization;
using KFL.Core.Enums;
using MessagePack;

namespace KFL.Infrastructure.Save.Serialization;

/// <summary>
/// 一对夫妇（007-family-lifecycle；契约十九 §7.1、契约十 §4.2 键表、data-model §7）。**有序**：夫在前。
/// </summary>
[MessagePackObject]
public sealed class CoupleKeyDto
{
    /// <summary>夫的成员标识。</summary>
    [Key(0)]
    [JsonPropertyName("husbandId")]
    [JsonRequired]
    public Guid HusbandId { get; set; }

    /// <summary>妻的成员标识。</summary>
    [Key(1)]
    [JsonPropertyName("wifeId")]
    [JsonRequired]
    public Guid WifeId { get; set; }
}

/// <summary>
/// 一条待分娩记录（007-family-lifecycle；契约十九 §7.1、契约十 §4.2 键表、data-model §7）。
/// </summary>
/// <remarks>
/// 键自 0 连续，与 <c>PendingBirth</c> 的成员<b>逐一对应</b>（<c>Couple</c> 在 DTO 里展开为夫/妻两键，
/// 年月拆成两个整数）；<c>AppliedAt</c> 是<b>申请月</b>，分娩在其后的 ⑧-b 消费。
/// </remarks>
[MessagePackObject]
public sealed class PendingBirthDto
{
    /// <summary>夫的成员标识。</summary>
    [Key(0)]
    [JsonPropertyName("husbandId")]
    [JsonRequired]
    public Guid HusbandId { get; set; }

    /// <summary>妻的成员标识。</summary>
    [Key(1)]
    [JsonPropertyName("wifeId")]
    [JsonRequired]
    public Guid WifeId { get; set; }

    /// <summary>申请年份。</summary>
    [Key(2)]
    [JsonPropertyName("appliedYear")]
    [JsonRequired]
    public int AppliedYear { get; set; }

    /// <summary>申请月份（1~12）。</summary>
    [Key(3)]
    [JsonPropertyName("appliedMonth")]
    [JsonRequired]
    public int AppliedMonth { get; set; }
}

/// <summary>
/// 一条待应答的婚配提议（007-family-lifecycle；契约十九 §4、契约十 §4.2 键表、data-model §7）。
/// </summary>
/// <remarks>
/// 键自 0 连续、与 <c>MarriageProposal</c> 的成员<b>逐一对应</b>（年月拆两个键、
/// <c>ProposedAt</c> ⇒ <c>proposedYear</c>/<c>proposedMonth</c>），故共 19 键。
/// </remarks>
[MessagePackObject]
public sealed class MarriageProposalDto
{
    /// <summary>提议来源（托媒 / 榜下捉婿）。</summary>
    [Key(0)]
    [JsonPropertyName("kind")]
    [JsonRequired]
    public MarriageProposalKind Kind { get; set; }

    /// <summary>我方成员标识（提议的受益者）。</summary>
    [Key(1)]
    [JsonPropertyName("ourMemberId")]
    [JsonRequired]
    public Guid OurMemberId { get; set; }

    /// <summary>对方姓名（冠家族姓后的全名）。</summary>
    [Key(2)]
    [JsonPropertyName("candidateName")]
    [JsonRequired]
    public string CandidateName { get; set; } = string.Empty;

    /// <summary>对方性别。</summary>
    [Key(3)]
    [JsonPropertyName("candidateGender")]
    [JsonRequired]
    public Gender CandidateGender { get; set; }

    /// <summary>对方年龄（岁）。</summary>
    [Key(4)]
    [JsonPropertyName("candidateAge")]
    [JsonRequired]
    public int CandidateAge { get; set; }

    /// <summary>对方天赋 · 农。</summary>
    [Key(5)]
    [JsonPropertyName("talentAgriculture")]
    [JsonRequired]
    public int TalentAgriculture { get; set; }

    /// <summary>对方天赋 · 商。</summary>
    [Key(6)]
    [JsonPropertyName("talentCommerce")]
    [JsonRequired]
    public int TalentCommerce { get; set; }

    /// <summary>对方天赋 · 仕。</summary>
    [Key(7)]
    [JsonPropertyName("talentScholar")]
    [JsonRequired]
    public int TalentScholar { get; set; }

    /// <summary>对方天赋 · 工。</summary>
    [Key(8)]
    [JsonPropertyName("talentCraft")]
    [JsonRequired]
    public int TalentCraft { get; set; }

    /// <summary>对方才学（0~100）。</summary>
    [Key(9)]
    [JsonPropertyName("candidateStudy")]
    [JsonRequired]
    public int CandidateStudy { get; set; }

    /// <summary>对方体质（0~100）。</summary>
    [Key(10)]
    [JsonPropertyName("candidateHealth")]
    [JsonRequired]
    public int CandidateHealth { get; set; }

    /// <summary>对方天命寿数（<c>&gt;= 0</c>）。</summary>
    [Key(11)]
    [JsonPropertyName("candidateLifespan")]
    [JsonRequired]
    public int CandidateLifespan { get; set; }

    /// <summary>对方功名。</summary>
    [Key(12)]
    [JsonPropertyName("candidateDegree")]
    [JsonRequired]
    public DegreeLevel CandidateDegree { get; set; }

    /// <summary>对方阶层。</summary>
    [Key(13)]
    [JsonPropertyName("class")]
    [JsonRequired]
    public MarriageClass Class { get; set; }

    /// <summary>聘礼（文；娶入 = 我方支出、嫁出 = 我方收入）。</summary>
    [Key(14)]
    [JsonPropertyName("betrothalGift")]
    [JsonRequired]
    public decimal BetrothalGift { get; set; }

    /// <summary>嫁妆（文；娶入 = 我方收入、嫁出 = 我方支出）。</summary>
    [Key(15)]
    [JsonPropertyName("dowry")]
    [JsonRequired]
    public decimal Dowry { get; set; }

    /// <summary>提议年份。</summary>
    [Key(16)]
    [JsonPropertyName("proposedYear")]
    [JsonRequired]
    public int ProposedYear { get; set; }

    /// <summary>提议月份（1~12）。提议只在其产出当月有效。</summary>
    [Key(17)]
    [JsonPropertyName("proposedMonth")]
    [JsonRequired]
    public int ProposedMonth { get; set; }

    /// <summary>对方甲第名次；<c>null</c> = 未标注（仅榜下捉婿可能非空）。</summary>
    [Key(18)]
    [JsonPropertyName("candidatePlacement")]
    [JsonRequired]
    public ImperialPlacement? CandidatePlacement { get; set; }
}

/// <summary>
/// 一条托媒申请记录（007-family-lifecycle；契约十九 §3、契约十 §4.2 键表、data-model §7）。
/// </summary>
/// <remarks>
/// 键自 0 连续，与 <c>MatchmakeApplication</c> 的成员<b>逐一对应</b>（年月拆成两个整数）。
/// 记录在**扣费成功后**追加，用于 FR-013 的「同一成员同一月至多一次」。
/// </remarks>
[MessagePackObject]
public sealed class MatchmakeApplicationDto
{
    /// <summary>提出托媒的我方成员标识。</summary>
    [Key(0)]
    [JsonPropertyName("memberId")]
    [JsonRequired]
    public Guid MemberId { get; set; }

    /// <summary>申请年份。</summary>
    [Key(1)]
    [JsonPropertyName("appliedYear")]
    [JsonRequired]
    public int AppliedYear { get; set; }

    /// <summary>申请月份（1~12）。</summary>
    [Key(2)]
    [JsonPropertyName("appliedMonth")]
    [JsonRequired]
    public int AppliedMonth { get; set; }
}

/// <summary>
/// 家族生命周期簿记（007-family-lifecycle；契约十 §4.2 键表、data-model §7）。
/// </summary>
/// <remarks>
/// 键自 0 连续、与 <c>FamilyLifecycleState</c> 的持久面<b>逐一对应</b>：
/// 0 本年购人数、1 购人年份、2 待分娩记录、3 已申请本年生育的夫妇、4 生育申请年份、
/// 5 待应答提议、6 本月已托媒的成员记录。
/// </remarks>
[MessagePackObject]
public sealed class FamilyLifecycleDto
{
    /// <summary>本年（<see cref="PurchaseYear"/>）已购人数。</summary>
    [Key(0)]
    [JsonPropertyName("purchaseCount")]
    [JsonRequired]
    public int PurchaseCount { get; set; }

    /// <summary>购人计数所属年份（<c>&gt;= 1</c>）。</summary>
    [Key(1)]
    [JsonPropertyName("purchaseYear")]
    [JsonRequired]
    public int PurchaseYear { get; set; }

    /// <summary>待分娩记录，**按申请顺序**。</summary>
    [Key(2)]
    [JsonPropertyName("pendingBirths")]
    [JsonRequired]
    public List<PendingBirthDto> PendingBirths { get; set; } = [];

    /// <summary>已申请本年生育的夫妇，**按夫妇字典序**。</summary>
    [Key(3)]
    [JsonPropertyName("appliedBirthCouples")]
    [JsonRequired]
    public List<CoupleKeyDto> AppliedBirthCouples { get; set; } = [];

    /// <summary>生育申请所属年份（<c>&gt;= 1</c>）。</summary>
    [Key(4)]
    [JsonPropertyName("birthApplicationYear")]
    [JsonRequired]
    public int BirthApplicationYear { get; set; }

    /// <summary>待应答提议；<c>null</c> = 无。</summary>
    /// <remarks>
    /// 本成员<b>有意不标 <c>[JsonRequired]</c></b>：<c>null</c> 是本版本允许的取值
    /// （「当月无待应答提议」），而 <c>[JsonRequired]</c> 既要求成员存在、也（在
    /// <c>JsonSaveFormat</c> 的判据下）禁止引用类型取 <c>null</c>。缺键与 <c>null</c>
    /// 的区别由契约十 §5 条款 3 的「缺失 MUST 被检出」覆盖：JSON 里必须写出
    /// <c>"proposal": null</c>，缺键仍会被拒。
    /// </remarks>
    [Key(5)]
    [JsonPropertyName("proposal")]
    public MarriageProposalDto? Proposal { get; set; }

    /// <summary>本月已提出托媒的成员记录，**按登记顺序**。</summary>
    [Key(6)]
    [JsonPropertyName("matchmakeApplications")]
    [JsonRequired]
    public List<MatchmakeApplicationDto> MatchmakeApplications { get; set; } = [];
}
