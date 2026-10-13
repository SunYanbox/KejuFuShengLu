using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using KFL.Core.Enums;
using KFL.Infrastructure.Abstractions;
using MessagePack;

namespace KFL.Infrastructure.Save;

/// <summary>
/// 存档文件形态的**单点常量与编解码选项**（004-save-system；契约十 §2/§3/§7，R-05）。
/// </summary>
/// <remarks>
/// <para>
/// 两个格式的序列化选项**只在这里声明一次**，MUST NOT 散落在各实现里——否则
/// 「两个格式信息集等价」（契约十 §5）就没有可核对的单一依据。
/// </para>
/// <para>
/// 本类型**零文件系统访问**：它只描述字节形态，不接触路径。
/// </para>
/// </remarks>
public static class SaveFileFormat
{
    /// <summary>本版本的文件格式版本号。</summary>
    /// <remarks>
    /// 读取时 <c>FormatVersion != 本值</c> 一律拒绝（<see cref="SaveFileErrorKind.UnsupportedVersion"/>）；
    /// 本版本**不提供**任何迁移/兼容路径（<c>&lt; 2</c> 与 <c>&gt; 2</c> 同样拒绝，FR-015）。
    /// 版本 2 相对版本 1 新增科举进度四个成员（家族自动报名 + 两个失败数 + 报名级别，契约十四 §10）。
    /// </remarks>
    public const int Version = 2;

    /// <summary>二进制格式的扩展名。</summary>
    public const string BinaryExtension = ".dat";

    /// <summary>JSON 格式的扩展名。</summary>
    public const string JsonExtension = ".json";

    /// <summary>未配置存档根时使用的目录名（<c>AppContext.BaseDirectory</c> 旁边）。</summary>
    public const string DefaultDirectoryName = "saves";

    /// <summary>备份目录名（位于存档根之下）。</summary>
    public const string BackupsDirectoryName = "backups";

    /// <summary>本版本已知的两个扩展名。</summary>
    public static IReadOnlyList<string> KnownExtensions { get; } = [BinaryExtension, JsonExtension];

    /// <summary>二进制的序列化选项（契约十 §3）。</summary>
    /// <remarks>
    /// <para>
    /// <c>UntrustedData</c>：存档是**外来输入**（玩家可能手工改过、也可能是别人给的档），
    /// MUST 按不可信数据读取。压缩恒为 <c>None</c>——契约十 §1 定死「不分块、不压缩」。
    /// </para>
    /// <para>
    /// 解析器用 <c>Standard</c>：它只认带 <c>[MessagePackObject]</c>/<c>[Key]</c> 的类型，
    /// 于是「每个落盘字段都必须被显式声明」成为结构性事实，而不是靠自觉。
    /// </para>
    /// </remarks>
    public static MessagePackSerializerOptions BinaryOptions { get; } = MessagePackSerializerOptions.Standard
        .WithSecurity(MessagePackSecurity.UntrustedData)
        .WithCompression(MessagePackCompression.None);

    /// <summary>JSON 的序列化选项（契约十 §3）。</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><c>WriteIndented</c>：便于人工核对与差异比对。</description></item>
    /// <item><description>
    /// <c>UnsafeRelaxedJsonEscaping</c>：中文与「」**按原样**落盘。默认编码器会把非 ASCII
    /// 转义成 <c>\uXXXX</c>，那样「档名/人名读得出」就没了，也与二进制格式的可读性差距拉大。
    /// </description></item>
    /// <item><description>
    /// <c>JsonStringEnumConverter</c>：枚举写**名字**（可读、可人工核），二进制写**原值**
    /// （紧凑）；两者承载的是同一个信息集。
    /// </description></item>
    /// <item><description>
    /// <see cref="StatusFlagJsonConverter"/>：<c>StatusFlag</c> 是 <c>[Flags]</c> 位掩码，
    /// 默认枚举转换器会写成 <c>"AwaitingPost, Retired"</c> 这样的**一个字符串**；
    /// 契约十 §4 要求它写成**名字数组**（<c>["AwaitingPost","Retired"]</c>），
    /// 且 <c>None</c> 写空数组 <c>[]</c> 而不是 <c>["None"]</c>。
    /// </description></item>
    /// </list>
    /// </remarks>
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        // 次序是**承重**的：JsonStringEnumConverter 是工厂，对任何枚举都 CanConvert ⇒ true，
        // 排在前面会把 StatusFlag 也一并接走（写成一个 "A, B" 字符串）。专用转换器必须在前。
        Converters = { new StatusFlagJsonConverter(), new JsonStringEnumConverter() },
    };

    /// <summary>取该格式的扩展名。</summary>
    /// <param name="format">格式。</param>
    /// <returns><c>.dat</c> 或 <c>.json</c>。</returns>
    public static string ExtensionOf(SaveFormat format) =>
        format == SaveFormat.Json ? JsonExtension : BinaryExtension;

    /// <summary>按扩展名判定格式。</summary>
    /// <param name="extension">含点的扩展名（如 <c>.dat</c>）。</param>
    /// <param name="format">判定结果；未知扩展名时为 <see cref="SaveFormat.Binary"/>。</param>
    /// <returns>是已知扩展名 ⇒ <c>true</c>。</returns>
    public static bool TryFormatOfExtension(string extension, out SaveFormat format)
    {
        if (string.Equals(extension, JsonExtension, StringComparison.OrdinalIgnoreCase))
        {
            format = SaveFormat.Json;
            return true;
        }

        if (string.Equals(extension, BinaryExtension, StringComparison.OrdinalIgnoreCase))
        {
            format = SaveFormat.Binary;
            return true;
        }

        format = SaveFormat.Binary;
        return false;
    }
}

/// <summary>
/// <see cref="StatusFlag"/> 的 JSON 表示：**名字数组**，<c>None</c> 为 <c>[]</c>（契约十 §4；data-model §3.9）。
/// </summary>
/// <remarks>
/// 写成位掩码整数（<c>二进制的表示</c>）会让人工核对变成位运算；写成逗号拼接的单串
/// 又和「数组」的契约不符。本转换器两个方向都按数组，且**只认已定义成员名**：
/// <c>"None"</c> 与组合名（如 <c>"AwaitingPost, Retired"</c>）都不是合法元素。
/// </remarks>
internal sealed class StatusFlagJsonConverter : JsonConverter<StatusFlag>
{
    /// <summary>已定义的单个状态位，按位值升序。</summary>
    private static readonly StatusFlag[] Flags =
    [
        .. Enum.GetValues<StatusFlag>().Where(flag => flag != StatusFlag.None).OrderBy(flag => (int)flag),
    ];

    /// <summary>名字 → 位值；只含已定义成员，比较为序号比较。</summary>
    private static readonly Dictionary<string, StatusFlag> FlagsByName =
        Flags.ToDictionary(flag => flag.ToString(), StringComparer.Ordinal);

    /// <inheritdoc />
    public override StatusFlag Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("状态标记 MUST 是名字数组（StatusFlag.None 写成空数组 []）。");
        }

        var result = StatusFlag.None;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return result;
            }

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("状态标记数组的元素 MUST 是字符串。");
            }

            var name = reader.GetString();
            if (name is null || !FlagsByName.TryGetValue(name, out var flag))
            {
                throw new JsonException($"未知的状态标记：{name}。");
            }

            result |= flag;
        }

        throw new JsonException("状态标记数组未闭合。");
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer, StatusFlag value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartArray();
        foreach (var flag in Flags)
        {
            if (value.HasFlag(flag))
            {
                writer.WriteStringValue(flag.ToString());
            }
        }

        writer.WriteEndArray();
    }
}
