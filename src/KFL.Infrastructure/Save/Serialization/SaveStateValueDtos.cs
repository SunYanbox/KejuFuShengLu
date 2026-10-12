using System.Text.Json.Serialization;
using KFL.Core.Enums;
using MessagePack;

namespace KFL.Infrastructure.Save.Serialization;

/// <summary>
/// 家族经济聚合（004-save-system；契约十 §4.3，data-model §3.3）。
/// </summary>
/// <remarks>
/// <b>不含任何派生量</b>：总收入/总支出/总资产皆可由本 DTO 重算，MUST NOT 入档（不变量 7，数据模型 §8）。
/// </remarks>
[MessagePackObject]
public sealed class FamilyEconomyDto
{
    /// <summary>当前生活水准。</summary>
    [Key(0)]
    [JsonPropertyName("livingStandard")]
    [JsonRequired]
    public LivingStandard LivingStandard { get; set; }

    /// <summary>待生效生活水准；<c>null</c> = 无待生效项。</summary>
    [Key(1)]
    [JsonPropertyName("pendingLivingStandard")]
    [JsonRequired]
    public LivingStandard? PendingLivingStandard { get; set; }

    /// <summary>米价指数（<c>decimal</c>，恒 <c>&gt; 0</c>）。</summary>
    [Key(2)]
    [JsonPropertyName("grainPriceIndex")]
    [JsonRequired]
    public decimal GrainPriceIndex { get; set; }

    /// <summary>资金池与贷款。</summary>
    [Key(3)]
    [JsonPropertyName("treasury")]
    [JsonRequired]
    public TreasuryDto Treasury { get; set; } = new();

    /// <summary>田宅铺持有量。</summary>
    [Key(4)]
    [JsonPropertyName("holdings")]
    [JsonRequired]
    public HoldingsDto Holdings { get; set; } = new();

    /// <summary>全量流水账，按<b>追加顺序</b>。</summary>
    [Key(5)]
    [JsonPropertyName("ledger")]
    [JsonRequired]
    public List<LedgerEntryDto> Ledger { get; set; } = [];

    /// <summary>饥馑状态。</summary>
    [Key(6)]
    [JsonPropertyName("famine")]
    [JsonRequired]
    public FamineDto Famine { get; set; } = new();
}

/// <summary>
/// 资金池、储蓄利率与贷款（004-save-system；契约十 §4.4，data-model §3.4）。
/// </summary>
/// <remarks>
/// <b>不含 <c>TreasuryPool</c></b>：它是现金+储蓄+商本的派生值，落盘即制造第二个真源（不变量 7）。
/// </remarks>
[MessagePackObject]
public sealed class TreasuryDto
{
    /// <summary>现金（文，<c>&gt;= 0</c>）。</summary>
    [Key(0)]
    [JsonPropertyName("cash")]
    [JsonRequired]
    public decimal Cash { get; set; }

    /// <summary>储蓄（文，<c>&gt;= 0</c>）。</summary>
    [Key(1)]
    [JsonPropertyName("savings")]
    [JsonRequired]
    public decimal Savings { get; set; }

    /// <summary>商本（文，<c>&gt;= 0</c>）。</summary>
    [Key(2)]
    [JsonPropertyName("merchantCapital")]
    [JsonRequired]
    public decimal MerchantCapital { get; set; }

    /// <summary>贷款。</summary>
    [Key(3)]
    [JsonPropertyName("loan")]
    [JsonRequired]
    public LoanDto Loan { get; set; } = new();

    /// <summary>当年储蓄利率；<c>null</c> = 尚未定过。</summary>
    [Key(4)]
    [JsonPropertyName("savingsRate")]
    [JsonRequired]
    public decimal? SavingsRate { get; set; }

    /// <summary>储蓄利率所属年份；<c>null</c> = 尚未定过。</summary>
    [Key(5)]
    [JsonPropertyName("savingsRateYear")]
    [JsonRequired]
    public int? SavingsRateYear { get; set; }
}

/// <summary>
/// 贷款（004-save-system；契约十 §4.4，data-model §3.4）。欠息<b>不加</b>入本金。
/// </summary>
[MessagePackObject]
public sealed class LoanDto
{
    /// <summary>本金（文，<c>&gt;= 0</c>）。</summary>
    [Key(0)]
    [JsonPropertyName("principal")]
    [JsonRequired]
    public decimal Principal { get; set; }

    /// <summary>欠息（文，<c>&gt;= 0</c>）。</summary>
    [Key(1)]
    [JsonPropertyName("accruedInterest")]
    [JsonRequired]
    public decimal AccruedInterest { get; set; }

    /// <summary>距上次计息的月数（<c>&gt;= 0</c>）。</summary>
    [Key(2)]
    [JsonPropertyName("monthsSinceInterest")]
    [JsonRequired]
    public int MonthsSinceInterest { get; set; }
}

/// <summary>
/// 田宅铺持有量（004-save-system；契约十 §4.5，data-model §3.5）。四项皆 <c>&gt;= 0</c>。
/// </summary>
[MessagePackObject]
public sealed class HoldingsDto
{
    /// <summary>田（亩）。</summary>
    [Key(0)]
    [JsonPropertyName("farmlandMu")]
    [JsonRequired]
    public int FarmlandMu { get; set; }

    /// <summary>乡宅（座）。</summary>
    [Key(1)]
    [JsonPropertyName("ruralHouses")]
    [JsonRequired]
    public int RuralHouses { get; set; }

    /// <summary>城宅（座）。</summary>
    [Key(2)]
    [JsonPropertyName("urbanHouses")]
    [JsonRequired]
    public int UrbanHouses { get; set; }

    /// <summary>铺面（间）。</summary>
    [Key(3)]
    [JsonPropertyName("shops")]
    [JsonRequired]
    public int Shops { get; set; }
}

/// <summary>
/// 一条流水（004-save-system；契约十 §4.5，data-model §3.5）。
/// </summary>
/// <remarks>
/// <see cref="AmountWen"/> 是<b>变动量</b>而非余额；资金类类别的金额 MUST NOT 为 0（校验第 ⑨ 条）。
/// </remarks>
[MessagePackObject]
public sealed class LedgerEntryDto
{
    /// <summary>年份。</summary>
    [Key(0)]
    [JsonPropertyName("year")]
    [JsonRequired]
    public int Year { get; set; }

    /// <summary>月份（1~12）。</summary>
    [Key(1)]
    [JsonPropertyName("month")]
    [JsonRequired]
    public int Month { get; set; }

    /// <summary>归属成员；<c>null</c> = 家族级条目。</summary>
    [Key(2)]
    [JsonPropertyName("personId")]
    [JsonRequired]
    public Guid? PersonId { get; set; }

    /// <summary>账目类别。</summary>
    [Key(3)]
    [JsonPropertyName("category")]
    [JsonRequired]
    public LedgerCategory Category { get; set; }

    /// <summary>变动金额（文）。</summary>
    [Key(4)]
    [JsonPropertyName("amountWen")]
    [JsonRequired]
    public decimal AmountWen { get; set; }
}

/// <summary>
/// 饥馑状态（004-save-system；契约十 §4.5，data-model §3.6）。
/// </summary>
/// <remarks>「阶段为零 ⇔ 计时为零」；非零阶段的计时恒 <c>&gt;= 1</c>（校验第 ⑦ 条）。</remarks>
[MessagePackObject]
public sealed class FamineDto
{
    /// <summary>饥馑阶段。</summary>
    [Key(0)]
    [JsonPropertyName("stage")]
    [JsonRequired]
    public FamineStage Stage { get; set; }

    /// <summary>本阶段已持续月数。</summary>
    [Key(1)]
    [JsonPropertyName("elapsedMonths")]
    [JsonRequired]
    public int ElapsedMonths { get; set; }
}

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
