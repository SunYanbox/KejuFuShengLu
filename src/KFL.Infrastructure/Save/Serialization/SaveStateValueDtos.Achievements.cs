// 成就类存档 DTO：成就累计量、完成记录、待落盘解锁、终局与编年史。

using System.Text.Json.Serialization;
using KFL.Core.Enums;
using MessagePack;

namespace KFL.Infrastructure.Save.Serialization;

/// <summary>
/// 档内成就累计量与解锁记录（008-achievements-extinction；契约二十三 §6「条款 25」、data-model §7.1）。
/// </summary>
/// <remarks>
/// 键自 0 连续、与 <c>AchievementState</c> 的**持久面逐一对应**（**恰 8** 项，SC-014）：
/// 0 累计成功行贿次数、1 累计出生人数、2 完整寿命序列、3 历史总资产峰值、4 是否曾有贷款、
/// 5 调试唤醒累计次数、6 已完成集合、7 待落盘队列。
/// <b>不含</b>任何可重算的派生量（已解锁数、七系进度皆由 6 派生），
/// 也不含加速集（<c>_completedKeys</c> 由 6 重建）。
/// </remarks>
[MessagePackObject]
public sealed class AchievementStateDto
{
    /// <summary>① 家族累计成功行贿次数（`>= 0`）。</summary>
    [Key(0)]
    [JsonPropertyName("successfulBribeCount")]
    [JsonRequired]
    public int SuccessfulBribeCount { get; set; }

    /// <summary>② 家族累计出生人数（含夭折；`>= 0`）。</summary>
    [Key(1)]
    [JsonPropertyName("birthCount")]
    [JsonRequired]
    public int BirthCount { get; set; }

    /// <summary>③ 已故成员的完整寿命序列，**按死亡次序**（三均值的原始输入，MUST NOT 只存合计）。</summary>
    [Key(2)]
    [JsonPropertyName("lifespans")]
    [JsonRequired]
    public List<int> Lifespans { get; set; } = [];

    /// <summary>④ 家族历史总资产峰值（文，`>= 0`）。</summary>
    [Key(3)]
    [JsonPropertyName("peakTotalAssets")]
    [JsonRequired]
    public decimal PeakTotalAssets { get; set; }

    /// <summary>⑤ 是否曾经产生过贷款（清偿后仍为 `true`）。</summary>
    [Key(4)]
    [JsonPropertyName("everHadLoan")]
    [JsonRequired]
    public bool EverHadLoan { get; set; }

    /// <summary>⑥ 调试唤醒累计次数（`>= 0`）。</summary>
    [Key(5)]
    [JsonPropertyName("debugWakeCount")]
    [JsonRequired]
    public int DebugWakeCount { get; set; }

    /// <summary>⑦ 本档已完成集合，**按解锁次序**；键 `(achievement, difficulty)` 内唯一。</summary>
    [Key(6)]
    [JsonPropertyName("completed")]
    [JsonRequired]
    public List<AchievementMarkDto> Completed { get; set; } = [];

    /// <summary>⑧ 待落盘解锁队列，**按入队次序**；每个键 MUST 也在 <see cref="Completed"/> 中。</summary>
    [Key(7)]
    [JsonPropertyName("pendingUnlocks")]
    [JsonRequired]
    public List<PendingUnlockDto> PendingUnlocks { get; set; } = [];
}

/// <summary>
/// 一条档内完成记录（008-achievements-extinction；契约二十三 §6「条款 25」）。
/// </summary>
/// <remarks>
/// 键自 0 连续：0 成就编号、1 难度编号、2 完成年、3 完成月。
/// 两个编号一律写**底层整数**（004 既有约定；<c>JsonStringEnumConverter</c> 会把枚举写成名字，
/// 故本 DTO 的成员类型是 <see cref="int"/>），年月用两个 <see cref="int"/> 键（「条款 28」）。
/// </remarks>
[MessagePackObject]
public sealed class AchievementMarkDto
{
    /// <summary>`AchievementId` 的底层整数（1~68）。</summary>
    [Key(0)]
    [JsonPropertyName("achievement")]
    [JsonRequired]
    public int Achievement { get; set; }

    /// <summary>`Difficulty` 的底层整数。</summary>
    [Key(1)]
    [JsonPropertyName("difficulty")]
    [JsonRequired]
    public int Difficulty { get; set; }

    /// <summary>完成年份。</summary>
    [Key(2)]
    [JsonPropertyName("completedYear")]
    [JsonRequired]
    public int CompletedYear { get; set; }

    /// <summary>完成月份（1~12）。</summary>
    [Key(3)]
    [JsonPropertyName("completedMonth")]
    [JsonRequired]
    public int CompletedMonth { get; set; }
}

/// <summary>
/// 一条待落盘的解锁记录（008-achievements-extinction；契约二十三 §6「条款 25」）。
/// </summary>
/// <remarks>
/// 键自 0 连续：0 成就编号、1 难度编号、2 解锁年、3 解锁月、4 来源存档 UUID。
/// 与 <see cref="AchievementMarkDto"/> 的唯一差别是末键与未来落盘用的来源标识。
/// </remarks>
[MessagePackObject]
public sealed class PendingUnlockDto
{
    /// <summary>`AchievementId` 的底层整数（1~68）。</summary>
    [Key(0)]
    [JsonPropertyName("achievement")]
    [JsonRequired]
    public int Achievement { get; set; }

    /// <summary>`Difficulty` 的底层整数。</summary>
    [Key(1)]
    [JsonPropertyName("difficulty")]
    [JsonRequired]
    public int Difficulty { get; set; }

    /// <summary>解锁年份。</summary>
    [Key(2)]
    [JsonPropertyName("unlockedYear")]
    [JsonRequired]
    public int UnlockedYear { get; set; }

    /// <summary>解锁月份（1~12）。</summary>
    [Key(3)]
    [JsonPropertyName("unlockedMonth")]
    [JsonRequired]
    public int UnlockedMonth { get; set; }

    /// <summary>来源存档 UUID（写入全局记录用，FR-025）。</summary>
    [Key(4)]
    [JsonPropertyName("sourceSaveId")]
    [JsonRequired]
    public Guid SourceSaveId { get; set; }
}

/// <summary>
/// 终局状态（008-achievements-extinction；契约二十三 §6「条款 25」、data-model §7.1）。
/// </summary>
/// <remarks>
/// 键自 0 连续：0 结束年、1 结束月、2 编年史快照。
/// 本 DTO **只在已结束时存在**——「未结束」由 <c>SaveStateDto[9]</c> 写 <c>null</c> 表达，
/// MUST NOT 改成「`bool` + 两个可空字段」的三键表示（契约二十三「条款 26」）。
/// </remarks>
[MessagePackObject]
public sealed class EndingStateDto
{
    /// <summary>终局触发年份。</summary>
    [Key(0)]
    [JsonPropertyName("endedYear")]
    [JsonRequired]
    public int EndedYear { get; set; }

    /// <summary>终局触发月份（1~12）。</summary>
    [Key(1)]
    [JsonPropertyName("endedMonth")]
    [JsonRequired]
    public int EndedMonth { get; set; }

    /// <summary>触发当刻冻结的编年史快照。</summary>
    [Key(2)]
    [JsonPropertyName("chronicle")]
    [JsonRequired]
    public ChronicleDto Chronicle { get; set; } = new();
}

/// <summary>
/// 绝嗣结局的编年史快照（008-achievements-extinction；契约二十三 §6「条款 25」、契约二十四 §1）。
/// </summary>
/// <remarks>
/// 键自 0 连续、与 <c>ChronicleSnapshot</c> 的七项**同序**：
/// 0 存续年数、1 世代数、2 历史资产峰值、3 最高功名、4 进士人数、5 平均寿命、6 宗谱。
/// 前六项是**冻结的历史**；⑦ 宗谱是**次序敏感**的 UUID 序列（MUST NOT 用集合语义）。
/// </remarks>
[MessagePackObject]
public sealed class ChronicleDto
{
    /// <summary>① 存续年数 = 结束年 − 1。</summary>
    [Key(0)]
    [JsonPropertyName("survivedYears")]
    [JsonRequired]
    public int SurvivedYears { get; set; }

    /// <summary>② 世代数 = 全量成员最大辈分 + 1。</summary>
    [Key(1)]
    [JsonPropertyName("generations")]
    [JsonRequired]
    public int Generations { get; set; }

    /// <summary>③ 历史总资产峰值（文，`>= 0`）。</summary>
    [Key(2)]
    [JsonPropertyName("peakTotalAssets")]
    [JsonRequired]
    public decimal PeakTotalAssets { get; set; }

    /// <summary>④ 最高功名（`DegreeLevel` 的底层整数）。</summary>
    [Key(3)]
    [JsonPropertyName("highestDegree")]
    [JsonRequired]
    public int HighestDegree { get; set; }

    /// <summary>⑤ 进士人数（按人去重）。</summary>
    [Key(4)]
    [JsonPropertyName("totalJinShi")]
    [JsonRequired]
    public int TotalJinShi { get; set; }

    /// <summary>⑥ 平均寿命（三均值，3 位小数）。</summary>
    [Key(5)]
    [JsonPropertyName("averageLifespan")]
    [JsonRequired]
    public decimal AverageLifespan { get; set; }

    /// <summary>⑦ 宗谱 = 全量成员 `PersonId` 的**次序敏感** UUID 序列（含归档）。</summary>
    [Key(6)]
    [JsonPropertyName("genealogy")]
    [JsonRequired]
    public List<Guid> Genealogy { get; set; } = [];
}
