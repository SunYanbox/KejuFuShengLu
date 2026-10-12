using System.Buffers;
using System.Globalization;
using KFL.Infrastructure.Abstractions;
using MessagePack;

namespace KFL.Infrastructure.Save.Serialization;

/// <summary>
/// 二进制（<c>.dat</c>）格式：手写头 + MessagePack 主体（004-save-system；契约十 §2/§3）。
/// </summary>
/// <remarks>
/// <para>
/// 头是 <c>fixarray(3)</c>（起手字节 <c>0x93</c>）后跟 <c>FormatVersion</c>（int）、
/// <c>PayloadLength</c>（int）、<c>SaveId</c>（36 字符的 <c>D</c> 格式字符串）；随后<b>直接</b>
/// 拼接主体的 MessagePack 字节流。头不是 DTO，因为它必须能被独立读出（契约十 §2）。
/// </para>
/// <para>
/// 主体按 <c>[Key(0..n-1)]</c> 连续编号 ⇒ 落成<b>位置式数组</b>：成员缺失就是数组短了，
/// 于是「缺本版本必需成员」在类型层面可检（契约十 §4/§5）。
/// </para>
/// <para>
/// 读取一律按<b>不可信输入</b>（<see cref="SaveFileFormat.BinaryOptions"/> 已启用
/// <c>MessagePackSecurity.UntrustedData</c>）。
/// </para>
/// </remarks>
public sealed class MessagePackSaveFormat : ISaveFormat
{
    /// <summary>头记录恰含三个成员（契约十 §2）。</summary>
    private const int HeaderMemberCount = 3;

    /// <summary>
    /// <c>SaveStateDto</c> 的成员数（契约十 §4.1：键 <c>0..7</c> 连续）。
    /// </summary>
    /// <remarks>
    /// 位置式数组的「数组长度」就是「本版本必需成员是否齐备」的判据（契约十 §6 第 ⑥ 步）。
    /// </remarks>
    private const int SaveStateMemberCount = 8;

    /// <inheritdoc />
    public SaveFormat Format => SaveFormat.Binary;

    /// <inheritdoc />
    public byte[] Write(Guid saveId, SaveStateDto payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var payloadBytes = MessagePackSerializer.Serialize(payload, SaveFileFormat.BinaryOptions);

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(HeaderMemberCount);
        writer.Write(SaveFileFormat.Version);
        writer.Write(payloadBytes.Length);
        writer.Write(saveId.ToString("D", CultureInfo.InvariantCulture));
        writer.Flush();

        var file = new byte[buffer.WrittenCount + payloadBytes.Length];
        buffer.WrittenSpan.CopyTo(file);
        payloadBytes.CopyTo(file, buffer.WrittenCount);
        return file;
    }

    /// <inheritdoc />
    public SaveFileHeader ReadHeader(ReadOnlySpan<byte> fileBytes) => ReadHeaderCore(fileBytes).Header;

    /// <inheritdoc />
    public (SaveFileHeader Header, SaveStateDto Payload) Read(ReadOnlySpan<byte> fileBytes)
    {
        var (header, headerLength) = ReadHeaderCore(fileBytes);

        // 契约十 §6 第 ③ 步：剩余字节 < 头声明的 PayloadLength ⇒ 截断。
        // 二进制的声明长度是**权威**的（位置式数组没有「多余空白」这类合法的长度差）。
        var remaining = fileBytes.Length - headerLength;
        if (remaining < header.PayloadLength)
        {
            throw Truncated(
                $"文件被截断：头声明主体 {header.PayloadLength} 字节，实际仅剩 {remaining} 字节（契约十 §6 第 ③ 步）。");
        }

        // 契约十 §6 第 ④ 步：版本闸门 MUST 早于任何主体解析（FR-015）。
        EnsureVersion(header.FormatVersion);

        var payloadBytes = fileBytes[headerLength..].ToArray();

        // 契约十 §6 第 ⑥ 步：位置式数组的成员数不足 ⇒ 缺必需成员（结构性，而非字节损坏）。
        EnsurePayloadShape(payloadBytes);

        // 契约十 §6 第 ⑤ 步：主体解析失败 ⇒ 字节损坏。
        SaveStateDto payload;
        try
        {
            payload = MessagePackSerializer.Deserialize<SaveStateDto>(
                new ReadOnlyMemory<byte>(payloadBytes, 0, header.PayloadLength),
                SaveFileFormat.BinaryOptions);
        }
        catch (MessagePackSerializationException ex)
        {
            throw Malformed($"主体解析失败（契约十 §6 第 ⑤ 步）：{ex.Message}", ex);
        }

        if (payload is null)
        {
            throw Malformed("主体解析结果为 null（契约十 §6 第 ⑤ 步）。");
        }

        return (header, payload);
    }

    private static (SaveFileHeader Header, int HeaderLength) ReadHeaderCore(ReadOnlySpan<byte> fileBytes)
    {
        try
        {
            var reader = new MessagePackReader(fileBytes.ToArray());

            if (reader.ReadArrayHeader() != HeaderMemberCount)
            {
                throw Malformed($"头记录 MUST 恰含 {HeaderMemberCount} 个成员（契约十 §2）。");
            }

            var formatVersion = reader.ReadInt32();
            var payloadLength = reader.ReadInt32();
            var saveIdText = reader.ReadString();

            if (saveIdText is null || !Guid.TryParse(saveIdText, out var saveId))
            {
                throw Malformed("头记录的 SaveId 不是合法的 UUID 字符串（契约十 §2）。");
            }

            if (payloadLength < 0)
            {
                throw Malformed($"头记录的 PayloadLength 为负（{payloadLength}），文件已损坏（契约十 §2）。");
            }

            return (new SaveFileHeader(formatVersion, payloadLength, saveId), checked((int)reader.Consumed));
        }
        catch (MessagePackSerializationException ex)
        {
            throw Malformed($"头记录读不出（契约十 §6 第 ② 步）：{ex.Message}", ex);
        }
    }

    private static void EnsurePayloadShape(ReadOnlySpan<byte> payloadBytes)
    {
        try
        {
            var reader = new MessagePackReader(payloadBytes.ToArray());
            var memberCount = reader.ReadArrayHeader();
            if (memberCount != SaveStateMemberCount)
            {
                throw InvalidStructure(
                    $"主体缺本版本必需成员：应有 {SaveStateMemberCount} 个，实际 {memberCount} 个（契约十 §6 第 ⑥ 步）。");
            }
        }
        catch (MessagePackSerializationException ex)
        {
            throw Malformed($"主体的数组头读不出（契约十 §6 第 ⑤ 步）：{ex.Message}", ex);
        }
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

    private static SaveFileException Truncated(string message) =>
        new(SaveFileErrorKind.Truncated, message);

    private static SaveFileException Malformed(string message, Exception innerException) =>
        new(SaveFileErrorKind.Malformed, message, innerException);

    private static SaveFileException InvalidStructure(string message) =>
        new(SaveFileErrorKind.InvalidStructure, message);
}
