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
    /// <para>
    /// <b>嵌套层同样要检</b>：MessagePack-CSharp 2.5.305 对「短于 <c>maxKey</c> 的位置式数组」
    /// **不抛异常**，而是把缺失成员静默留成默认值（实测：4 成员的 <c>TalentDto</c> 砍成
    /// <c>fixarray(3)</c> 后反序列化得到 <c>Craft = 0</c>，无任何异常）。这既是 FR-020 禁止的
    /// 「为序列化放宽不变量」，也让 §6 第 ⑥ 步形同虚设——故
    /// <see cref="EnsurePayloadShape"/> 逐层核对每个 DTO 的成员数（契约十 §5 条款 3）。
    /// </para>
    /// </remarks>
    private const int SaveStateMemberCount = 8;

    /// <summary>嵌套 DTO 的成员数（契约十 §4 键表；全部按 <c>[Key(0..n-1)]</c> 连续编号）。</summary>
    private const int FamilyMemberCount = 6;

    /// <inheritdoc cref="FamilyMemberCount"/>
    private const int PersonMemberCount = 22;

    /// <inheritdoc cref="FamilyMemberCount"/>
    private const int TalentMemberCount = 4;

    /// <inheritdoc cref="FamilyMemberCount"/>
    private const int StatusTimersMemberCount = 4;

    /// <inheritdoc cref="FamilyMemberCount"/>
    private const int DegreeRecordMemberCount = 6;

    /// <inheritdoc cref="FamilyMemberCount"/>
    private const int FamilyEconomyMemberCount = 7;

    /// <inheritdoc cref="FamilyMemberCount"/>
    private const int TreasuryMemberCount = 6;

    /// <inheritdoc cref="FamilyMemberCount"/>
    private const int LoanMemberCount = 3;

    /// <inheritdoc cref="FamilyMemberCount"/>
    private const int HoldingsMemberCount = 4;

    /// <inheritdoc cref="FamilyMemberCount"/>
    private const int LedgerEntryMemberCount = 5;

    /// <inheritdoc cref="FamilyMemberCount"/>
    private const int FamineMemberCount = 2;

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
        catch (Exception ex) when (ex is MessagePackSerializationException or EndOfStreamException)
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
        catch (Exception ex) when (ex is MessagePackSerializationException or EndOfStreamException)
        {
            // 空文件、或在**头记录内部**被截断：MessagePackReader 抛裸 EndOfStreamException
            // （"Attempted to read past the end of the stream"），也必须归入第 ② 步的 Malformed，
            // 否则 Load 会漏出未分类异常（契约十 §6 第 ② 步、§2/§6 条款 4）。
            throw Malformed($"头记录读不出（契约十 §6 第 ② 步）：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 逐层核对位置式数组的成员数（契约十 §4 键表 / §5 条款 3 / §6 第 ⑥ 步）。
    /// </summary>
    /// <remarks>
    /// 只核对「成员数」这一件事：值本身的合法性由第 ⑤ 步（反序列化）与第 ⑦ 步（复原/mapper）
    /// 负责。核对次序与 <c>[Key(n)]</c> 的编号一一对应，本类型的成员数常量与各 DTO 的键数
    /// 由 <c>SaveFormatContractTests</c> 的反射断言逐表钉死（T053/T055）；改 DTO 的 <c>[Key]</c>
    /// 而不改这里的常量会让那条断言失败，而不是让 walker 静默走错位置。
    /// </remarks>
    /// <param name="payloadBytes">主体字节。</param>
    private static void EnsurePayloadShape(ReadOnlySpan<byte> payloadBytes)
    {
        try
        {
            var reader = new MessagePackReader(payloadBytes.ToArray());

            EnsureArrayHeader(ref reader, SaveStateMemberCount, "SaveStateDto");
            Skip(ref reader, 6);                    // 0 Id / 1 年 / 2 月 / 3 难度 / 4 待定难度 / 5 出身
            EnsureFamilyShape(ref reader, "SaveStateDto.Family");       // 6
            EnsureEconomyShape(ref reader, "SaveStateDto.Economy");     // 7
        }
        catch (Exception ex) when (ex is MessagePackSerializationException or EndOfStreamException)
        {
            // 与「第 ⑤ 步」同款：读不出结构即字节损坏（结构性缺成员在上面走 InvalidStructure）。
            throw Malformed($"主体读不出（契约十 §6 第 ⑤/⑥ 步）：{ex.Message}", ex);
        }
    }

    private static void EnsureFamilyShape(ref MessagePackReader reader, string path)
    {
        EnsureArrayHeader(ref reader, FamilyMemberCount, path);
        Skip(ref reader, 3);                        // 0 名 / 1 士籍 / 2 家主

        var members = reader.ReadArrayHeader();     // 3 成员（含归档者）
        for (var index = 0; index < members; index++)
        {
            EnsurePersonShape(ref reader, $"{path}.Members[{index}]");
        }

        SkipGuidList(ref reader);                   // 4 已落籍旁系
        SkipGuidList(ref reader);                   // 5 外嫁对齐旁系
    }

    private static void EnsurePersonShape(ref MessagePackReader reader, string path)
    {
        EnsureArrayHeader(ref reader, PersonMemberCount, path);
        Skip(ref reader, 6);                        // 0..5 Id/名/性别/生年/生月/辈分

        EnsureArrayHeader(ref reader, TalentMemberCount, $"{path}.Talents");   // 6
        Skip(ref reader, TalentMemberCount);

        Skip(ref reader, 6);                        // 7..12 才学/健康/寿数/官阶/功名/状态位

        EnsureArrayHeader(ref reader, StatusTimersMemberCount, $"{path}.Timers");   // 13
        Skip(ref reader, StatusTimersMemberCount);

        Skip(ref reader, 3);                        // 14..16 职业/在职月数/入仕轨
        Skip(ref reader, 3);                        // 17..19 父/母/配偶

        SkipGuidList(ref reader);                   // 20 历任配偶

        var degrees = reader.ReadArrayHeader();     // 21 功名履历
        for (var index = 0; index < degrees; index++)
        {
            EnsureArrayHeader(ref reader, DegreeRecordMemberCount, $"{path}.DegreeHistory[{index}]");
            Skip(ref reader, DegreeRecordMemberCount);
        }
    }

    private static void EnsureEconomyShape(ref MessagePackReader reader, string path)
    {
        EnsureArrayHeader(ref reader, FamilyEconomyMemberCount, path);
        Skip(ref reader, 3);                        // 0 生活水准 / 1 待定水准 / 2 粮价指数

        EnsureArrayHeader(ref reader, TreasuryMemberCount, $"{path}.Treasury");    // 3
        Skip(ref reader, 3);                        // 0 现银 / 1 储蓄 / 2 商本

        EnsureArrayHeader(ref reader, LoanMemberCount, $"{path}.Treasury.Loan");   // 3
        Skip(ref reader, LoanMemberCount);

        Skip(ref reader, 2);                        // 4 储蓄利率 / 5 利率年份

        EnsureArrayHeader(ref reader, HoldingsMemberCount, $"{path}.Holdings");    // 4
        Skip(ref reader, HoldingsMemberCount);

        var entries = reader.ReadArrayHeader();     // 5 账本
        for (var index = 0; index < entries; index++)
        {
            EnsureArrayHeader(ref reader, LedgerEntryMemberCount, $"{path}.Ledger[{index}]");
            Skip(ref reader, LedgerEntryMemberCount);
        }

        EnsureArrayHeader(ref reader, FamineMemberCount, $"{path}.Famine");        // 6
        Skip(ref reader, FamineMemberCount);
    }

    private static void SkipGuidList(ref MessagePackReader reader)
    {
        var count = reader.ReadArrayHeader();
        Skip(ref reader, count);
    }

    private static void EnsureArrayHeader(ref MessagePackReader reader, int expected, string path)
    {
        var actual = reader.ReadArrayHeader();
        if (actual != expected)
        {
            throw InvalidStructure(
                $"主体缺本版本必需成员：{path} 应有 {expected} 个成员，实际 {actual} 个（契约十 §4、§6 第 ⑥ 步）。");
        }
    }

    private static void Skip(ref MessagePackReader reader, int count)
    {
        for (var index = 0; index < count; index++)
        {
            reader.Skip();
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
