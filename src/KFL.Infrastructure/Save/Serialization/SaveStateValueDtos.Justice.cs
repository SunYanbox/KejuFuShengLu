// 司法类存档 DTO：行贿、判决、罚金、亲属禁令、连坐、释放与惩罚日志。

using System.Text.Json.Serialization;
using KFL.Core.Enums;
using MessagePack;

namespace KFL.Infrastructure.Save.Serialization;

/// <summary>
/// 一次行贿的事实（006-bribery-punishment；契约十五 §5、data-model §2.2）。
/// </summary>
[MessagePackObject]
public sealed class BribeFactDto
{
    /// <summary>发生年月。</summary>
    [Key(0)]
    [JsonPropertyName("date")]
    [JsonRequired]
    public GameDateDto Date { get; set; } = new();

    /// <summary>行贿的考生。</summary>
    [Key(1)]
    [JsonPropertyName("examinee")]
    [JsonRequired]
    public Guid Examinee { get; set; }

    /// <summary>行贿的考试级别。</summary>
    [Key(2)]
    [JsonPropertyName("level")]
    [JsonRequired]
    public ExamLevel Level { get; set; }

    /// <summary>该级别第几次（自 1 起）。</summary>
    [Key(3)]
    [JsonPropertyName("nth")]
    [JsonRequired]
    public int Nth { get; set; }

    /// <summary>应付金额（文）。</summary>
    [Key(4)]
    [JsonPropertyName("amountWen")]
    [JsonRequired]
    public decimal AmountWen { get; set; }

    /// <summary>本次是否被抓。</summary>
    [Key(5)]
    [JsonPropertyName("caught")]
    [JsonRequired]
    public bool Caught { get; set; }

    /// <summary>本次是否因触顶而被拒。</summary>
    [Key(6)]
    [JsonPropertyName("capReached")]
    [JsonRequired]
    public bool CapReached { get; set; }
}

/// <summary>
/// 一次服刑判决的事实（006-bribery-punishment；契约十六 §5、data-model §2.2）。
/// </summary>
[MessagePackObject]
public sealed class SentenceFactDto
{
    /// <summary>发生年月。</summary>
    [Key(0)]
    [JsonPropertyName("date")]
    [JsonRequired]
    public GameDateDto Date { get; set; } = new();

    /// <summary>被判刑的当事人。</summary>
    [Key(1)]
    [JsonPropertyName("subject")]
    [JsonRequired]
    public Guid Subject { get; set; }

    /// <summary>服刑月数。</summary>
    [Key(2)]
    [JsonPropertyName("months")]
    [JsonRequired]
    public int Months { get; set; }
}

/// <summary>
/// 一次罚金的事实（006-bribery-punishment；契约十六 §4、data-model §2.2）。
/// </summary>
/// <remarks>
/// <see cref="AmountWen"/> 是罚金总额；<see cref="PaidFromTreasuryWen"/> 是池内实际付出额，
/// <see cref="ToLoanWen"/> 是转为贷款本金的缺口 —— 后两者之和恰等于罚金总额（契约十六 §4.2）。
/// </remarks>
[MessagePackObject]
public sealed class FineFactDto
{
    /// <summary>发生年月。</summary>
    [Key(0)]
    [JsonPropertyName("date")]
    [JsonRequired]
    public GameDateDto Date { get; set; } = new();

    /// <summary>被罚的当事人。</summary>
    [Key(1)]
    [JsonPropertyName("subject")]
    [JsonRequired]
    public Guid Subject { get; set; }

    /// <summary>罚金总额（文）。</summary>
    [Key(2)]
    [JsonPropertyName("amountWen")]
    [JsonRequired]
    public decimal AmountWen { get; set; }

    /// <summary>池内实际付出额（现金 + 储蓄；文）。</summary>
    [Key(3)]
    [JsonPropertyName("paidFromTreasuryWen")]
    [JsonRequired]
    public decimal PaidFromTreasuryWen { get; set; }

    /// <summary>转为贷款本金的缺口（文）。</summary>
    [Key(4)]
    [JsonPropertyName("toLoanWen")]
    [JsonRequired]
    public decimal ToLoanWen { get; set; }
}

/// <summary>
/// 一次亲属禁令的事实（006-bribery-punishment；契约十六 §3、data-model §2.2）。
/// </summary>
[MessagePackObject]
public sealed class RelativeBanFactDto
{
    /// <summary>发生年月。</summary>
    [Key(0)]
    [JsonPropertyName("date")]
    [JsonRequired]
    public GameDateDto Date { get; set; } = new();

    /// <summary>被连带的当事人（行贿者）。</summary>
    [Key(1)]
    [JsonPropertyName("subject")]
    [JsonRequired]
    public Guid Subject { get; set; }

    /// <summary>被施加禁令的亲属。</summary>
    [Key(2)]
    [JsonPropertyName("relative")]
    [JsonRequired]
    public Guid Relative { get; set; }

    /// <summary>禁考月数。</summary>
    [Key(3)]
    [JsonPropertyName("examBanMonths")]
    [JsonRequired]
    public int ExamBanMonths { get; set; }

    /// <summary>禁升月数；<c>null</c> = 本次不建禁升。</summary>
    [Key(4)]
    [JsonPropertyName("promotionBanMonths")]
    [JsonRequired]
    public int? PromotionBanMonths { get; set; }
}

/// <summary>
/// 一次连坐后果的事实（006-bribery-punishment；契约十六 §6、data-model §2.2）。
/// </summary>
[MessagePackObject]
public sealed class CollectivePunishmentFactDto
{
    /// <summary>发生年月。</summary>
    [Key(0)]
    [JsonPropertyName("date")]
    [JsonRequired]
    public GameDateDto Date { get; set; } = new();

    /// <summary>被连带的当事人（行贿者）。</summary>
    [Key(1)]
    [JsonPropertyName("subject")]
    [JsonRequired]
    public Guid Subject { get; set; }

    /// <summary>连坐分支。</summary>
    [Key(2)]
    [JsonPropertyName("outcome")]
    [JsonRequired]
    public CollectiveOutcome Outcome { get; set; }

    /// <summary>被波及的亲属。</summary>
    [Key(3)]
    [JsonPropertyName("relative")]
    [JsonRequired]
    public Guid Relative { get; set; }

    /// <summary>降级前的功名；<c>null</c> = 本分支不涉功名。</summary>
    [Key(4)]
    [JsonPropertyName("degreeBefore")]
    [JsonRequired]
    public DegreeLevel? DegreeBefore { get; set; }

    /// <summary>降级后的功名；<c>null</c> = 本分支不涉功名。</summary>
    [Key(5)]
    [JsonPropertyName("degreeAfter")]
    [JsonRequired]
    public DegreeLevel? DegreeAfter { get; set; }
}

/// <summary>
/// 一次释放的事实（006-bribery-punishment；契约十六 §7、data-model §2.2）。
/// </summary>
[MessagePackObject]
public sealed class ReleaseFactDto
{
    /// <summary>发生年月。</summary>
    [Key(0)]
    [JsonPropertyName("date")]
    [JsonRequired]
    public GameDateDto Date { get; set; } = new();

    /// <summary>被释放的当事人。</summary>
    [Key(1)]
    [JsonPropertyName("subject")]
    [JsonRequired]
    public Guid Subject { get; set; }

    /// <summary>释放后的官阶；<c>null</c> = 释放时无官职。</summary>
    [Key(2)]
    [JsonPropertyName("newRankLevel")]
    [JsonRequired]
    public int? NewRankLevel { get; set; }
}

/// <summary>
/// 惩罚日志（006-bribery-punishment；契约十六 §5、data-model §7.1）。
/// </summary>
/// <remarks>
/// 键自 0 连续：0 家族禁令标志、1~6 六类事实行集合。集合顺序即 <c>KFL.Core</c> 的
/// <c>PunishmentJournal</c> 的规范顺序（按 <c>PersonId</c> 升序，并列按级别 / 亲属）。
/// </remarks>
[MessagePackObject]
public sealed class PunishmentJournalDto
{
    /// <summary>本月是否声明过全族禁考禁升。</summary>
    [Key(0)]
    [JsonPropertyName("familyBanDeclared")]
    [JsonRequired]
    public bool FamilyBanDeclared { get; set; }

    /// <summary>行贿事实。</summary>
    [Key(1)]
    [JsonPropertyName("bribes")]
    [JsonRequired]
    public BribeFactDto[] Bribes { get; set; } = [];

    /// <summary>服刑事实。</summary>
    [Key(2)]
    [JsonPropertyName("sentences")]
    [JsonRequired]
    public SentenceFactDto[] Sentences { get; set; } = [];

    /// <summary>罚金事实。</summary>
    [Key(3)]
    [JsonPropertyName("fines")]
    [JsonRequired]
    public FineFactDto[] Fines { get; set; } = [];

    /// <summary>亲属禁令事实。</summary>
    [Key(4)]
    [JsonPropertyName("relativeBans")]
    [JsonRequired]
    public RelativeBanFactDto[] RelativeBans { get; set; } = [];

    /// <summary>连坐后果事实。</summary>
    [Key(5)]
    [JsonPropertyName("collectivePunishments")]
    [JsonRequired]
    public CollectivePunishmentFactDto[] CollectivePunishments { get; set; } = [];

    /// <summary>释放事实。</summary>
    [Key(6)]
    [JsonPropertyName("releases")]
    [JsonRequired]
    public ReleaseFactDto[] Releases { get; set; } = [];
}
