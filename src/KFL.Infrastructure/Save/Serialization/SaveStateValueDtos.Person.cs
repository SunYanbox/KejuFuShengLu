// 人物类存档 DTO：天赋、状态计时与功名履历。

using System.Text.Json.Serialization;
using KFL.Core.Enums;
using MessagePack;

namespace KFL.Infrastructure.Save.Serialization;

/// <summary>
/// 天赋四维（004-save-system；契约十 §4.2，data-model §3.7）。四项皆 0~100。
/// </summary>
[MessagePackObject]
public sealed class TalentDto
{
    /// <summary>农事。</summary>
    [Key(0)]
    [JsonPropertyName("agriculture")]
    [JsonRequired]
    public int Agriculture { get; set; }

    /// <summary>经商。</summary>
    [Key(1)]
    [JsonPropertyName("commerce")]
    [JsonRequired]
    public int Commerce { get; set; }

    /// <summary>仕途。</summary>
    [Key(2)]
    [JsonPropertyName("officialdom")]
    [JsonRequired]
    public int Officialdom { get; set; }

    /// <summary>技艺。</summary>
    [Key(3)]
    [JsonPropertyName("craft")]
    [JsonRequired]
    public int Craft { get; set; }
}

/// <summary>
/// 状态计时四路（004-save-system；契约十 §4.2，data-model §3.7）。
/// </summary>
/// <remarks>
/// 四路与 <c>StatusFlag</c> 的对应位必须同时为真（由 <c>StatusTimers</c> 构造与校验共同保证）。
/// </remarks>
[MessagePackObject]
public sealed class StatusTimersDto
{
    /// <summary>服刑剩余月数（<c>&gt;= 0</c>）。</summary>
    [Key(0)]
    [JsonPropertyName("sentenceRemainingMonths")]
    [JsonRequired]
    public int? SentenceRemainingMonths { get; set; }

    /// <summary>科考禁令剩余月数（<c>&gt;= 0</c>）。</summary>
    [Key(1)]
    [JsonPropertyName("examBanRemainingMonths")]
    [JsonRequired]
    public int? ExamBanRemainingMonths { get; set; }

    /// <summary>升迁禁令剩余月数（<c>&gt;= 0</c>）。</summary>
    [Key(2)]
    [JsonPropertyName("promotionBanRemainingMonths")]
    [JsonRequired]
    public int? PromotionBanRemainingMonths { get; set; }

    /// <summary>待铨剩余月数（<c>&gt;= 0</c>）。</summary>
    [Key(3)]
    [JsonPropertyName("awaitingPostRemainingMonths")]
    [JsonRequired]
    public int? AwaitingPostRemainingMonths { get; set; }

    /// <summary>疾病程度（万分比，0~10000；007-family-lifecycle）。非空 ⇔ <c>Ill</c> 位置真。</summary>
    [Key(4)]
    [JsonPropertyName("illnessSeverityBasisPoints")]
    [JsonRequired]
    public int? IllnessSeverityBasisPoints { get; set; }

    /// <summary>疗程剩余月数（0~3；007-family-lifecycle）。<c>null</c> = 未在疗程中。</summary>
    [Key(5)]
    [JsonPropertyName("treatmentRemainingMonths")]
    [JsonRequired]
    public int? TreatmentRemainingMonths { get; set; }
}

/// <summary>
/// 一条功名变更记录（004-save-system；契约十 §4.2，data-model §3.7）。
/// </summary>
/// <remarks>
/// 年月拆成两个整数（不落 <c>GameDate</c> 类型）；复原时重新组装并接受 <c>GameDate</c> 的自校验。
/// </remarks>
[MessagePackObject]
public sealed class DegreeRecordDto
{
    /// <summary>变更后的功名等级。</summary>
    [Key(0)]
    [JsonPropertyName("level")]
    [JsonRequired]
    public DegreeLevel Level { get; set; }

    /// <summary>甲第名次；<c>null</c> = 非进士。</summary>
    [Key(1)]
    [JsonPropertyName("placement")]
    [JsonRequired]
    public ImperialPlacement? Placement { get; set; }

    /// <summary>变更年份。</summary>
    [Key(2)]
    [JsonPropertyName("changedYear")]
    [JsonRequired]
    public int ChangedYear { get; set; }

    /// <summary>变更月份（1~12）。</summary>
    [Key(3)]
    [JsonPropertyName("changedMonth")]
    [JsonRequired]
    public int ChangedMonth { get; set; }

    /// <summary>变更原因。</summary>
    [Key(4)]
    [JsonPropertyName("cause")]
    [JsonRequired]
    public DegreeChangeCause Cause { get; set; }

    /// <summary>甲第；<c>null</c> = 未标注。</summary>
    [Key(5)]
    [JsonPropertyName("imperialClass")]
    [JsonRequired]
    public ImperialClass? ImperialClass { get; set; }
}
