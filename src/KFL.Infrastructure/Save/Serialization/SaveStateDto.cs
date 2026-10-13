using System.Text.Json.Serialization;
using KFL.Core.Enums;
using MessagePack;

namespace KFL.Infrastructure.Save.Serialization;

/// <summary>
/// 状态主体：线上形状的**唯一真源**（004-save-system；契约十 §4.1，data-model §3.1）。
/// </summary>
/// <remarks>
/// <para>
/// <b>瞬态</b>：本类型只在一次 <c>Save</c>/<c>Load</c> 的调用栈上存在，
/// MUST NOT 被 <c>Core</c>/<c>Rules</c>/<c>SaveService</c> 长期持有（data-model 不变量 1；契约九 §1 条款 4）。
/// </para>
/// <para>
/// <b>键连续</b>：<c>[Key]</c> 从 0 连续编号 ⇒ 二进制为**位置式数组**，成员缺失即结构错误；
/// JSON 的 <c>[JsonPropertyName]</c> 与 <c>[JsonRequired]</c> 承担同一职责（契约十 §5 条款 3）。
/// 键名 MUST NOT 随 C# 属性改名而变（契约十 §3 条款 4）。
/// </para>
/// <para>
/// 类型的**语义**、来源与复原入口逐条见 data-model §3——本文件只固定键与类型。
/// </para>
/// </remarks>
[MessagePackObject]
public sealed class SaveStateDto
{
    /// <summary>存档标识（<c>GameState.Id</c>；MUST NOT 为 <c>Guid.Empty</c>）。</summary>
    [Key(0)]
    [JsonPropertyName("id")]
    [JsonRequired]
    public Guid Id { get; set; }

    /// <summary>当前纪年（<c>GameState.CurrentDate.Year</c>；含前史纪年）。</summary>
    [Key(1)]
    [JsonPropertyName("currentYear")]
    [JsonRequired]
    public int CurrentYear { get; set; }

    /// <summary>当前月份（1~12，由 <c>GameDate</c> 自校验）。</summary>
    [Key(2)]
    [JsonPropertyName("currentMonth")]
    [JsonRequired]
    public int CurrentMonth { get; set; }

    /// <summary>难度。</summary>
    [Key(3)]
    [JsonPropertyName("difficulty")]
    [JsonRequired]
    public Difficulty Difficulty { get; set; }

    /// <summary>待生效难度；<c>null</c> = 无。本版本恒为 <c>null</c>（入档字段，非派生）。</summary>
    [Key(4)]
    [JsonPropertyName("pendingDifficulty")]
    [JsonRequired]
    public Difficulty? PendingDifficulty { get; set; }

    /// <summary>出身。</summary>
    [Key(5)]
    [JsonPropertyName("origin")]
    [JsonRequired]
    public Origin Origin { get; set; }

    /// <summary>家族（含全部成员与两处外来者簿记）。</summary>
    [Key(6)]
    [JsonPropertyName("family")]
    [JsonRequired]
    public FamilyDto Family { get; set; } = new();

    /// <summary>家族经济（资金池、资产、全量账本、饥馑）。</summary>
    [Key(7)]
    [JsonPropertyName("economy")]
    [JsonRequired]
    public FamilyEconomyDto Economy { get; set; } = new();
}

/// <summary>
/// 家族（004-save-system；契约十 §4.2，data-model §3.2）。
/// </summary>
/// <remarks>
/// <see cref="Members"/> **涵盖在册与归档两类**（归档不是删除）：<c>Count == 在册数 + 归档数</c>，
/// 成员的 <see cref="PersonDto.Status"/> 里保留 <c>Deceased</c>/<c>MarriedOut</c> 位。
/// </remarks>
[MessagePackObject]
public sealed class FamilyDto
{
    /// <summary>家族姓氏/名号（**与存档名无关**，R-25）。</summary>
    [Key(0)]
    [JsonPropertyName("name")]
    [JsonRequired]
    public string Name { get; set; } = string.Empty;

    /// <summary>是否具仕身份。</summary>
    [Key(1)]
    [JsonPropertyName("hasShiStatus")]
    [JsonRequired]
    public bool HasShiStatus { get; set; }

    /// <summary>家主标识；<c>null</c> = 未立家主。</summary>
    [Key(2)]
    [JsonPropertyName("headId")]
    [JsonRequired]
    public Guid? HeadId { get; set; }

    /// <summary>全部成员（含归档），**按加入顺序**。</summary>
    [Key(3)]
    [JsonPropertyName("members")]
    [JsonRequired]
    public List<PersonDto> Members { get; set; } = [];

    /// <summary>辈分已由家族落定的外来者（私有簿记，R-03）。</summary>
    [Key(4)]
    [JsonPropertyName("settledOutsiders")]
    [JsonRequired]
    public List<Guid> SettledOutsiders { get; set; } = [];

    /// <summary>首次在家族内成婚而对齐过辈分的外来者（私有簿记，R-03）。</summary>
    [Key(5)]
    [JsonPropertyName("marriageAlignedOutsiders")]
    [JsonRequired]
    public List<Guid> MarriageAlignedOutsiders { get; set; } = [];

    /// <summary>是否自动为合格成员报名科举（005-exam-cycle；契约十四 §2 ①自动报名）。</summary>
    [Key(6)]
    [JsonPropertyName("autoExamApplication")]
    [JsonRequired]
    public bool AutoExamApplication { get; set; }
}

/// <summary>
/// 成员（004-save-system；契约十 §4.2，data-model §3.8）。
/// </summary>
/// <remarks>
/// <see cref="Generation"/>/<see cref="Lifespan"/>/<see cref="Talents"/>/<see cref="FatherId"/>/
/// <see cref="MotherId"/> 由构造函数携带（不可变），其余经公开/内部 setter 复原；
/// 赋值次序见 data-model §3.11，MUST NOT 调整。
/// </remarks>
[MessagePackObject]
public sealed class PersonDto
{
    /// <summary>成员标识（MUST NOT 为 <c>Guid.Empty</c>）。</summary>
    [Key(0)]
    [JsonPropertyName("id")]
    [JsonRequired]
    public Guid Id { get; set; }

    /// <summary>姓名（空白被拒）。</summary>
    [Key(1)]
    [JsonPropertyName("name")]
    [JsonRequired]
    public string Name { get; set; } = string.Empty;

    /// <summary>性别。</summary>
    [Key(2)]
    [JsonPropertyName("gender")]
    [JsonRequired]
    public Gender Gender { get; set; }

    /// <summary>出生年（含前史纪年，负值合法）。</summary>
    [Key(3)]
    [JsonPropertyName("birthYear")]
    [JsonRequired]
    public int BirthYear { get; set; }

    /// <summary>出生月（1~12）。</summary>
    [Key(4)]
    [JsonPropertyName("birthMonth")]
    [JsonRequired]
    public int BirthMonth { get; set; }

    /// <summary>辈分（<c>&gt;= 0</c>）。</summary>
    [Key(5)]
    [JsonPropertyName("generation")]
    [JsonRequired]
    public int Generation { get; set; }

    /// <summary>天赋四维。</summary>
    [Key(6)]
    [JsonPropertyName("talents")]
    [JsonRequired]
    public TalentDto Talents { get; set; } = new();

    /// <summary>才学（0~100）。</summary>
    [Key(7)]
    [JsonPropertyName("study")]
    [JsonRequired]
    public int Study { get; set; }

    /// <summary>健康（0~100）。</summary>
    [Key(8)]
    [JsonPropertyName("health")]
    [JsonRequired]
    public int Health { get; set; }

    /// <summary>寿数（<c>&gt;= 0</c>）。</summary>
    [Key(9)]
    [JsonPropertyName("lifespan")]
    [JsonRequired]
    public int Lifespan { get; set; }

    /// <summary>官品等级（1~18）；<c>null</c> = 无官职。</summary>
    [Key(10)]
    [JsonPropertyName("rankLevel")]
    [JsonRequired]
    public int? RankLevel { get; set; }

    /// <summary>功绩（<c>&gt;= 0</c>）。</summary>
    [Key(11)]
    [JsonPropertyName("merit")]
    [JsonRequired]
    public int Merit { get; set; }

    /// <summary>状态标记（九位可并存；二进制 = 位掩码整数，JSON = 名字数组）。</summary>
    [Key(12)]
    [JsonPropertyName("status")]
    [JsonRequired]
    public StatusFlag Status { get; set; }

    /// <summary>状态计时（四路）。</summary>
    [Key(13)]
    [JsonPropertyName("timers")]
    [JsonRequired]
    public StatusTimersDto Timers { get; set; } = new();

    /// <summary>职业。</summary>
    [Key(14)]
    [JsonPropertyName("occupation")]
    [JsonRequired]
    public Occupation Occupation { get; set; }

    /// <summary>在任月数（<c>&gt;= 0</c>）。</summary>
    [Key(15)]
    [JsonPropertyName("monthsInOffice")]
    [JsonRequired]
    public int MonthsInOffice { get; set; }

    /// <summary>入仕途径；非空 ⇒ <c>AwaitingPost</c> 位必为真。</summary>
    [Key(16)]
    [JsonPropertyName("entryTrack")]
    [JsonRequired]
    public AppointmentTrack? EntryTrack { get; set; }

    /// <summary>父标识（不可变）。</summary>
    [Key(17)]
    [JsonPropertyName("fatherId")]
    [JsonRequired]
    public Guid? FatherId { get; set; }

    /// <summary>母标识（不可变）。</summary>
    [Key(18)]
    [JsonPropertyName("motherId")]
    [JsonRequired]
    public Guid? MotherId { get; set; }

    /// <summary>现任配偶标识；<c>null</c> = 无。双向一致由校验保证。</summary>
    [Key(19)]
    [JsonPropertyName("spouseId")]
    [JsonRequired]
    public Guid? SpouseId { get; set; }

    /// <summary>历任配偶标识，**按追加顺序**。</summary>
    [Key(20)]
    [JsonPropertyName("formerSpouseIds")]
    [JsonRequired]
    public List<Guid> FormerSpouseIds { get; set; } = [];

    /// <summary>功名履历，**全量、按序**（非降序）。</summary>
    [Key(21)]
    [JsonPropertyName("degreeHistory")]
    [JsonRequired]
    public List<DegreeRecordDto> DegreeHistory { get; set; } = [];

    /// <summary>解试累计失败数（<c>&gt;= 0</c>；005-exam-cycle）。</summary>
    [Key(22)]
    [JsonPropertyName("jieShiFailureCount")]
    [JsonRequired]
    public int JieShiFailureCount { get; set; }

    /// <summary>省试累计失败数（<c>&gt;= 0</c>；005-exam-cycle）。</summary>
    [Key(23)]
    [JsonPropertyName("shengShiFailureCount")]
    [JsonRequired]
    public int ShengShiFailureCount { get; set; }

    /// <summary>
    /// 本月报名级别；<c>null</c> = 未报名（005-exam-cycle）。
    /// 殿试由省试及第自动进入，**不是**合法报名态 ⇒ 校验第 ⑩ 条拒绝。
    /// </summary>
    [Key(24)]
    [JsonPropertyName("appliedExam")]
    [JsonRequired]
    public ExamLevel? AppliedExam { get; set; }
}
