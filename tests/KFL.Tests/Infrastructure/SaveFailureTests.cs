using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using KFL.Core.Entities;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save;
using KFL.Infrastructure.Save.Serialization;
using MessagePack;
using Xunit;

namespace KFL.Tests.Infrastructure;

/// <summary>
/// 读档失败的分类验收测试（004-save-system；契约十 §6 / SC-007 / tasks.md T044、T049、T059）。
/// </summary>
/// <remarks>
/// <para>
/// 四类输入对应契约十 §6 的八个判定步：不存在 ⇒ <c>Missing</c>；头之后字节损坏 ⇒ <c>Malformed</c>；
/// 剩余字节不足头声明 ⇒ <c>Truncated</c>；版本不是 5 ⇒ <c>UnsupportedVersion</c>；
/// 主体可解析但成员缺失或违反一致性 ⇒ <c>InvalidStructure</c>。
/// </para>
/// <para>
/// 失败分类 MUST NOT 依赖异常文本（契约十 §6 条款 3），故这里一律断言
/// <see cref="SaveFileException.Kind"/>。
/// </para>
/// <para>
/// <b>契约十 §6 的③④⑤次序是两种格式共同的</b>：③ 是无条件的长度判定、④ 版本闸门、
/// ⑤ 才是主体解析。故四条损坏类用例一律**参数化到两种格式**（T049/T059），
/// 且每条都断言「失败后原文件字节与调用方已持有的状态差异 = 0」（§6 条款 4、SC-007）。
/// </para>
/// <para>
/// <b>非法枚举与未登记位的落点由输入形态决定</b>（T056/T057；契约十 §3 条款 1、§6、data-model §3.9）：
/// JSON 写的是**名字**，不认得的名字根本读不出来 ⇒ 第 ⑤ 步 ⇒ <c>Malformed</c>；
/// 二进制写的是**整数**，越界整数与未登记位都读得出来，但在第 ⑦ 步复原时被拒绝 ⇒ <c>InvalidStructure</c>。
/// </para>
/// </remarks>
public sealed partial class SaveFailureTests
{
    /// <summary>把主体字节拼成一个完整的二进制档（头是 fixarray(3) + 版本 + 主体长度 + 档标识）。</summary>
    /// <param name="saveId">主体标识（须与头一致，否则会先撞上标识一致性判定）。</param>
    /// <param name="payload">主体字节。</param>
    /// <returns>完整文件字节。</returns>
    private static byte[] ComposeBinaryFile(Guid saveId, byte[] payload)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(3);
        writer.Write(SaveFileFormat.Version);
        writer.Write(payload.Length);
        writer.Write(saveId.ToString("D", CultureInfo.InvariantCulture));
        writer.Flush();
        return [.. buffer.WrittenSpan, .. payload];
    }

    /// <summary>把薄状态存进档，并返回（调用方持有的状态, 独立参照状态）——两者逐字段等价。</summary>
    /// <param name="service">存档服务。</param>
    /// <param name="slot">档槽位。</param>
    /// <returns>已持有的状态与独立参照。</returns>
    private static (GameState Held, GameState Reference) SaveAndHold(SaveService service, SaveSlot slot)
    {
        var held = SaveFixtures.PlainState();
        var reference = SaveFixtures.PlainState();
        service.Save(held, slot, overwrite: false);
        return (held, reference);
    }

    /// <summary>SC-007 / 契约十 §6 条款 4：失败路径 MUST NOT 改原文件，也 MUST NOT 污染已持有的状态。</summary>
    /// <param name="path">档路径。</param>
    /// <param name="originalBytes">失败调用之前的文件字节。</param>
    /// <param name="held">调用方已持有的状态。</param>
    /// <param name="reference">与之逐字段等价的独立参照。</param>
    private static void AssertFailurePreserves(string path, byte[] originalBytes, GameState held, GameState reference)
    {
        Assert.Equal(originalBytes, SaveTestSandbox.ReadBytes(path));
        SaveStateComparer.AssertEquivalent(reference, held);
    }

    /// <summary>
    /// 截掉头之后的**一半主体**（剩余 = 声明长度的一半 + 头尾零头 &lt; 声明长度），头本身完好。
    /// </summary>
    /// <param name="path">档路径。</param>
    /// <param name="format">格式。</param>
    private static void TruncateHalfOfPayload(string path, SaveFormat format)
    {
        var bytes = SaveTestSandbox.ReadBytes(path);
        var declared = FormatOf(format).ReadHeader(bytes).PayloadLength;
        Assert.True(declared > 8, $"测试前提失败：主体太小（{declared} 字节），截一半保不住判定余量。");

        // 二进制：文件长度 - 声明长度 = 头长度（精确）；JSON：同一式子的结果还含「: 与收尾」的零头。
        SaveTestSandbox.TruncateTo(path, bytes.Length - declared + (declared / 2));
    }

    /// <summary>把头声明的 PayloadLength 抬到远大于实际字节数（正文一个字节都不动）。</summary>
    /// <param name="path">档路径。</param>
    private static void InflatePayloadLength(string path)
    {
        var text = ReadText(path);
        var marker = $"\"{PayloadLengthName}\": ";
        var start = text.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        Assert.True(start >= marker.Length, "测试前提失败：头里没有 PayloadLength。");

        var end = start;
        while (end < text.Length && char.IsAsciiDigit(text[end]))
        {
            end++;
        }

        var declared = int.Parse(text[start..end], CultureInfo.InvariantCulture);
        var inflated = (declared * 2) + 1000;
        var replaced = string.Concat(text[..start], inflated.ToString(CultureInfo.InvariantCulture), text[end..]);
        SaveTestSandbox.WriteBytes(path, Encoding.UTF8.GetBytes(replaced));
    }

    /// <summary>把头记录的版本改成 <paramref name="version"/>；两种格式的改法不同（二进制是定长整数、JSON 是可读数字文本）。</summary>
    /// <param name="version">目标版本号（用于造「旧版」与「更新版」两种拒绝面）。</param>
    /// <param name="path">档路径。</param>
    /// <param name="format">格式。</param>
    private static void SetVersionTo(int version, string path, SaveFormat format)
    {
        if (format == SaveFormat.Binary)
        {
            var bytes = SaveTestSandbox.ReadBytes(path);
            bytes[1] = (byte)version; // 头是 fixarray(3) + 版本整数，版本恰在偏移 1。
            SaveTestSandbox.WriteBytes(path, bytes);
            return;
        }

        var text = ReadText(path);
        var replaced = text.Replace(
            $"\"{FormatVersionName}\": {SaveFileFormat.Version.ToString(CultureInfo.InvariantCulture)}",
            $"\"{FormatVersionName}\": {version.ToString(CultureInfo.InvariantCulture)}",
            StringComparison.Ordinal);
        Assert.NotEqual(text, replaced);
        SaveTestSandbox.WriteBytes(path, Encoding.UTF8.GetBytes(replaced));
    }

    /// <summary>
    /// 改写 JSON 主体（头三个成员保持原值）并**重算 PayloadLength**。
    /// </summary>
    /// <remarks>
    /// 契约十 §6 第 ③ 步是无条件的长度判定：手改主体却不更新声明长度，读档会先落成
    /// <c>Truncated</c>，本类里「成员缺失」「违反不变式」的用例就验不到自己要验的东西。
    /// </remarks>
    /// <param name="path">档路径。</param>
    /// <param name="mutate">对主体对象的就地修改。</param>
    private static void RewritePayload(string path, Action<JsonObject> mutate)
    {
        var file = JsonNode.Parse(SaveTestSandbox.ReadBytes(path))!.AsObject();
        var state = file[StateName]!.AsObject();
        mutate(state);

        var stateJson = state.ToJsonString(PayloadJsonOptions);
        var payloadLength = Encoding.UTF8.GetByteCount(stateJson);
        var version = file[FormatVersionName]!.GetValue<int>();
        var saveId = file[SaveIdName]!.GetValue<string>();

        var text = string.Concat(
            "{\n  \"", FormatVersionName, "\": ", version.ToString(CultureInfo.InvariantCulture),
            ",\n  \"", PayloadLengthName, "\": ", payloadLength.ToString(CultureInfo.InvariantCulture),
            ",\n  \"", SaveIdName, "\": \"", saveId,
            "\",\n  \"", StateName, "\": ", stateJson, "\n}");

        SaveTestSandbox.WriteBytes(path, Encoding.UTF8.GetBytes(text));
    }

    /// <summary>用 0xC1（MessagePack 明令「永不使用」的字节；JSON 里则是非法 UTF-8）把尾部写坏。</summary>
    /// <param name="bytes">文件字节（就地修改）。</param>
    private static void CorruptTail(byte[] bytes)
    {
        for (var index = Math.Max(0, bytes.Length - 8); index < bytes.Length; index++)
        {
            bytes[index] = 0xC1;
        }
    }

    /// <summary>按格式取读取面（头长度判定与版本号都从它读）。</summary>
    /// <param name="format">格式。</param>
    /// <returns>格式读取面。</returns>
    private static ISaveFormat FormatOf(SaveFormat format) =>
        format == SaveFormat.Binary ? new MessagePackSaveFormat() : new JsonSaveFormat();

    private static string ReadText(string path) => Encoding.UTF8.GetString(SaveTestSandbox.ReadBytes(path));

    private const string FormatVersionName = "FormatVersion";

    private const string PayloadLengthName = "PayloadLength";

    private const string SaveIdName = "SaveId";

    private const string StateName = "State";

    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
