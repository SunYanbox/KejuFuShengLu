using System.Text.Json.Serialization;
using MessagePack;

namespace KFL.Infrastructure.Save.Serialization;

/// <summary>
/// 年月（006-bribery-punishment；契约十六 §5、data-model §7.1）。
/// </summary>
/// <remarks>
/// 惩罚日志的**事实行**把年月收进单键，使每个事实行 DTO 的键数与对应 <c>KFL.Core</c>
/// 记录成员**逐一对应**（data-model §7.1）。
/// </remarks>
[MessagePackObject]
public sealed class GameDateDto
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
}
