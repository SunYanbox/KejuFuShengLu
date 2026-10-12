using System.Buffers;
using System.Globalization;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using KFL.Infrastructure.Abstractions;

namespace KFL.Infrastructure.Save.Serialization;

/// <summary>
/// JSON（<c>.json</c>）格式：手写头 + System.Text.Json 主体（004-save-system；契约十 §2/§3）。
/// </summary>
/// <remarks>
/// <para>
/// 文件形状 <c>{"FormatVersion":1,"PayloadLength":N,"SaveId":"…","State":&lt;主体&gt;}</c>：
/// 三个头成员**先于** <c>State</c> 出现且次序固定（契约十 §2）。头用
/// <see cref="Utf8JsonWriter.WriteRawValue(ReadOnlySpan{byte}, bool)"/> 拼装，主体保持
/// 它自己的逐行缩进。
/// </para>
/// <para>
/// <b>只读头时不解析主体</b>：用 <see cref="Utf8JsonReader"/> 顺序前进到 <c>"State"</c>
/// 这个属性名就停住，因此 <c>FindBySaveId</c> 的探针不会因为主体损坏而失败，
/// 也满足契约九 §5「MUST NOT 读取主体」。
/// </para>
/// <para>
/// <b>截断与损坏的区分</b>：JSON 的 <c>PayloadLength</c> 只是「写的时候主体有多少字节」，
/// 契约十 §2 明说它 MUST NOT 被当作校验和——手工改短仍然合法。所以这里的判据是：
/// 整体解析失败**且**剩余字节小于头声明 ⇒ 截断；解析失败而长度不缺 ⇒ 字节损坏（契约十 §6 第 ③⑤ 步）。
/// </para>
/// </remarks>
public sealed class JsonSaveFormat : ISaveFormat
{
    /// <summary>头记录的三个成员名，次序即写入次序（契约十 §2）。</summary>
    private const string FormatVersionName = "FormatVersion";

    /// <summary>见 <see cref="FormatVersionName"/>。</summary>
    private const string PayloadLengthName = "PayloadLength";

    /// <summary>见 <see cref="FormatVersionName"/>。</summary>
    private const string SaveIdName = "SaveId";

    /// <summary>承载主体的成员名。</summary>
    private const string StateName = "State";

    /// <summary>
    /// <c>SaveStateDto</c> 上所有 <c>[JsonPropertyName]</c>，即本版本的键表（契约十 §4.1）。
    /// </summary>
    /// <remarks>
    /// 从 DTO 自身读出，而不是另抄一份常量清单——抄一份就多一个「两处不一致」的可能，
    /// 而契约十 §5 恰恰要求键表可被断言。
    /// </remarks>
    private static readonly string[] StateMemberNames = [.. typeof(SaveStateDto)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
        .OfType<string>()];

    /// <inheritdoc />
    public SaveFormat Format => SaveFormat.Json;

    /// <inheritdoc />
    public byte[] Write(Guid saveId, SaveStateDto payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, SaveFileFormat.JsonOptions);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(
            buffer,
            new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteNumber(FormatVersionName, SaveFileFormat.Version);
            writer.WriteNumber(PayloadLengthName, payloadBytes.Length);
            writer.WriteString(SaveIdName, saveId.ToString("D", CultureInfo.InvariantCulture));
            writer.WritePropertyName(StateName);
            writer.WriteRawValue(payloadBytes, skipInputValidation: true);
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <inheritdoc />
    public SaveFileHeader ReadHeader(ReadOnlySpan<byte> fileBytes) => ReadHeaderCore(fileBytes).Header;

    /// <inheritdoc />
    public (SaveFileHeader Header, SaveStateDto Payload) Read(ReadOnlySpan<byte> fileBytes)
    {
        var (header, payloadOffset) = ReadHeaderCore(fileBytes);

        // 契约十 §6 第 ④ 步：版本闸门 MUST 早于任何主体解析（FR-015）。三类版本异常都在此拦住。
        EnsureVersion(header.FormatVersion);

        if (payloadOffset < 0)
        {
            throw InvalidStructure("文件缺 State 成员（契约十 §6 第 ⑥ 步）。");
        }

        var remaining = fileBytes.Length - payloadOffset;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(fileBytes.ToArray());
        }
        catch (JsonException ex) when (remaining < header.PayloadLength)
        {
            throw new SaveFileException(
                SaveFileErrorKind.Truncated,
                $"文件被截断：头声明主体 {header.PayloadLength} 字节，实际仅剩 {remaining} 字节（契约十 §6 第 ③ 步）。",
                ex);
        }
        catch (JsonException ex)
        {
            throw new SaveFileException(
                SaveFileErrorKind.Malformed,
                $"文件不是合法的 JSON（契约十 §6 第 ⑤ 步）：{ex.Message}",
                ex);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw Malformed("文件的根不是 JSON 对象（契约十 §2）。");
            }

            // 契约十 §2：主体挂在 State 成员上；键表核对针对**主体**，不是外层头。
            if (!root.TryGetProperty(StateName, out var stateElement))
            {
                throw InvalidStructure($"文件缺 {StateName} 成员（契约十 §2）。");
            }

            // 契约十 §6 第 ⑥ 步：必需成员齐备（任意深度），缺一即结构错误。
            foreach (var name in StateMemberNames)
            {
                if (!stateElement.TryGetProperty(name, out _))
                {
                    throw InvalidStructure($"文件缺本版本必需成员 {name}（契约十 §6 第 ⑥ 步）。");
                }
            }

            EnsureRequiredMembersPresent(stateElement, typeof(SaveStateDto));

            SaveStateDto payload;
            try
            {
                payload = stateElement.Deserialize<SaveStateDto>(SaveFileFormat.JsonOptions)
                    ?? throw Malformed("主体解析结果为 null（契约十 §6 第 ⑤ 步）。");
            }
            catch (JsonException ex)
            {
                throw new SaveFileException(
                    SaveFileErrorKind.Malformed,
                    $"主体解析失败（契约十 §6 第 ⑤ 步）：{ex.Message}",
                    ex);
            }

            return (header, payload);
        }
    }

    /// <summary>
    /// 递归核对「本类型声明的必需成员都在」——判据只看键名是否存在，
    /// MUST NOT 依赖异常文本（契约十 §6 条款 3）。
    /// </summary>
    private static void EnsureRequiredMembersPresent(JsonElement element, Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (element.ValueKind == JsonValueKind.Array)
        {
            if (TryGetElementType(underlying, out var elementType))
            {
                foreach (var item in element.EnumerateArray())
                {
                    EnsureRequiredMembersPresent(item, elementType);
                }
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object || underlying.Namespace != typeof(SaveStateDto).Namespace)
        {
            return;
        }

        foreach (var property in underlying.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name is not { } name)
            {
                continue;
            }

            if (!element.TryGetProperty(name, out var child))
            {
                if (property.GetCustomAttribute<JsonRequiredAttribute>() is not null)
                {
                    throw InvalidStructure(
                        $"文件缺本版本必需成员 {underlying.Name}.{name}（契约十 §6 第 ⑥ 步）。");
                }

                continue;
            }

            EnsureRequiredMembersPresent(child, property.PropertyType);
        }
    }

    private static bool TryGetElementType(Type type, out Type elementType)
    {
        if (type.IsArray && type.GetElementType() is { } arrayElement)
        {
            elementType = arrayElement;
            return true;
        }

        if (type.IsGenericType && type.GetGenericArguments() is [{ } genericElement])
        {
            elementType = genericElement;
            return true;
        }

        elementType = type;
        return false;
    }

    private static (SaveFileHeader Header, int PayloadOffset) ReadHeaderCore(ReadOnlySpan<byte> fileBytes)
    {
        var reader = new Utf8JsonReader(fileBytes);

        int? formatVersion = null;
        int? payloadLength = null;
        Guid? saveId = null;
        var payloadOffset = -1;

        try
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                throw Malformed("文件的根不是 JSON 对象（契约十 §2）。");
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw Malformed("JSON 对象里出现了非属性名的记号（契约十 §2）。");
                }

                var name = reader.GetString();

                // 遇到主体就停：MUST NOT 解析主体（契约九 §5），也让截断判据先于主体解析生效。
                if (string.Equals(name, StateName, StringComparison.Ordinal))
                {
                    payloadOffset = checked((int)reader.BytesConsumed);
                    break;
                }

                switch (name)
                {
                    case FormatVersionName:
                        formatVersion = ReadInt32(ref reader, name);
                        break;
                    case PayloadLengthName:
                        payloadLength = ReadInt32(ref reader, name);
                        break;
                    case SaveIdName:
                        saveId = ReadSaveId(ref reader);
                        break;
                    default:
                        throw Malformed($"头记录出现未知成员 {name}（契约十 §2 只允许 FormatVersion/PayloadLength/SaveId/State）。");
                }
            }
        }
        catch (JsonException ex)
        {
            throw new SaveFileException(
                SaveFileErrorKind.Malformed,
                $"头记录读不出（契约十 §6 第 ② 步）：{ex.Message}",
                ex);
        }

        if (formatVersion is not { } version)
        {
            throw Malformed($"头记录缺 {FormatVersionName}（契约十 §2）。");
        }

        if (payloadLength is not { } length)
        {
            throw Malformed($"头记录缺 {PayloadLengthName}（契约十 §2）。");
        }

        if (length < 0)
        {
            throw Malformed($"头记录的 {PayloadLengthName} 为负（{length}），文件已损坏（契约十 §2）。");
        }

        if (saveId is not { } id)
        {
            throw Malformed($"头记录缺 {SaveIdName}（契约十 §2）。");
        }

        return (new SaveFileHeader(version, length, id), payloadOffset);
    }

    private static int ReadInt32(ref Utf8JsonReader reader, string name)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var value))
        {
            throw Malformed($"头记录的 {name} MUST 是 32 位整数（契约十 §2）。");
        }

        return value;
    }

    private static Guid ReadSaveId(ref Utf8JsonReader reader)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.String)
        {
            throw Malformed($"头记录的 {SaveIdName} MUST 是字符串（契约十 §2）。");
        }

        if (reader.GetString() is not { } text || !Guid.TryParse(text, out var saveId))
        {
            throw Malformed($"头记录的 {SaveIdName} 不是合法的 UUID 字符串（契约十 §2）。");
        }

        return saveId;
    }

    private static void EnsureVersion(int formatVersion)
    {
        if (formatVersion != SaveFileFormat.Version)
        {
            throw new SaveFileException(
                SaveFileErrorKind.UnsupportedVersion,
                $"文件格式版本为 {formatVersion}，本版本只认 {SaveFileFormat.Version}（FR-015：本版本不提供迁移路径）。");
        }
    }

    private static SaveFileException Malformed(string message) =>
        new(SaveFileErrorKind.Malformed, message);

    private static SaveFileException InvalidStructure(string message) =>
        new(SaveFileErrorKind.InvalidStructure, message);
}
