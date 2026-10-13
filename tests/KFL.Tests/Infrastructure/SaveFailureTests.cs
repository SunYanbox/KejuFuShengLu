using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using KFL.Core.Entities;
using KFL.Core.Enums;
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
/// 剩余字节不足头声明 ⇒ <c>Truncated</c>；版本不是 2 ⇒ <c>UnsupportedVersion</c>；
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
public sealed class SaveFailureTests
{
    /// <summary>SC-007 类 ①：档不存在 ⇒ <c>Missing</c>。</summary>
    [Fact]
    public void 档不存在时报Missing()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();

        var exception = Assert.Throws<SaveFileException>(() => service.Load(new SaveSlot("没有这个档")));

        Assert.Equal(SaveFileErrorKind.Missing, exception.Kind);
    }

    /// <summary>SC-007 类 ②：档被截掉一半主体 ⇒ <c>Truncated</c>（头完好、剩余不足头声明）。</summary>
    /// <param name="format">格式。</param>
    [Theory]
    [InlineData(SaveFormat.Binary)]
    [InlineData(SaveFormat.Json)]
    public void 档被截断时报Truncated(SaveFormat format)
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("甲档", format);
        var (held, reference) = SaveAndHold(service, slot);

        var path = service.FilePathOf(slot);
        TruncateHalfOfPayload(path, format);
        var corrupted = SaveTestSandbox.ReadBytes(path);

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.Truncated, exception.Kind);
        AssertFailurePreserves(path, corrupted, held, reference);
    }

    /// <summary>
    /// 契约十 §6 第 ③ 步：JSON 正文**合法**，只是头声明的长度大于实际字节数 ⇒ <c>Truncated</c>。
    /// </summary>
    /// <remarks>
    /// 这条把「③ 是无条件的长度判定」钉死：截断判定 MUST NOT 挂在下游解析失败的异常过滤器上
    /// （否则「正文合法、长度声明虚高」会漏成成功读档，T049）。
    /// </remarks>
    [Fact]
    public void JSON头声明长度大于实际字节数时报Truncated()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("庚档", SaveFormat.Json);
        var (held, reference) = SaveAndHold(service, slot);

        var path = service.FilePathOf(slot);
        InflatePayloadLength(path);
        var corrupted = SaveTestSandbox.ReadBytes(path);

        // 正文仍是合法 JSON（只有头声明的长度虚高），故本条不可能落到 ⑤ 的 Malformed。
        var root = JsonNode.Parse(corrupted)!.AsObject();
        Assert.NotNull(root["State"]);

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.Truncated, exception.Kind);
        AssertFailurePreserves(path, corrupted, held, reference);
    }

    /// <summary>SC-007 类 ③：合法 JSON 少一个必填成员 ⇒ <c>InvalidStructure</c>。</summary>
    [Fact]
    public void JSON缺必填成员时报InvalidStructure()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("乙档", SaveFormat.Json);
        var (held, reference) = SaveAndHold(service, slot);

        var path = service.FilePathOf(slot);
        RewritePayload(path, state => Assert.True(state.Remove("currentMonth"), "测试前提失败：主体里没有 currentMonth。"));
        var corrupted = SaveTestSandbox.ReadBytes(path);

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.InvalidStructure, exception.Kind);
        AssertFailurePreserves(path, corrupted, held, reference);
    }

    /// <summary>SC-007 类 ③：主体可解析但字段违反 Core 不变式（月份 13）⇒ <c>InvalidStructure</c>。</summary>
    [Fact]
    public void JSON字段违反不变式时报InvalidStructure()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("丙档", SaveFormat.Json);
        var (held, reference) = SaveAndHold(service, slot);

        var path = service.FilePathOf(slot);
        RewritePayload(path, state => state["currentMonth"] = 13);
        var corrupted = SaveTestSandbox.ReadBytes(path);

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.InvalidStructure, exception.Kind);
        AssertFailurePreserves(path, corrupted, held, reference);
    }

    /// <summary>FR-015：头记录版本不为 2（旧版 1 与更新版 3 同样拒）⇒ <c>UnsupportedVersion</c>。</summary>
    /// <param name="format">格式。</param>
    /// <param name="version">写进头里的非本版本版本号。</param>
    [Theory]
    [InlineData(SaveFormat.Binary, 1)]
    [InlineData(SaveFormat.Json, 1)]
    [InlineData(SaveFormat.Binary, 3)]
    [InlineData(SaveFormat.Json, 3)]
    public void 头版本不为二时报UnsupportedVersion(SaveFormat format, int version)
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("丁档", format);
        var (held, reference) = SaveAndHold(service, slot);

        var path = service.FilePathOf(slot);
        SetVersionTo(version, path, format);
        var corrupted = SaveTestSandbox.ReadBytes(path);

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.UnsupportedVersion, exception.Kind);
        AssertFailurePreserves(path, corrupted, held, reference);
    }

    /// <summary>契约十 §6 条款 1：版本闸门 MUST 早于任何主体解析——主体同时损坏也先报版本。</summary>
    /// <param name="format">格式。</param>
    /// <param name="version">写进头里的非本版本版本号。</param>
    [Theory]
    [InlineData(SaveFormat.Binary, 1)]
    [InlineData(SaveFormat.Json, 1)]
    [InlineData(SaveFormat.Binary, 3)]
    [InlineData(SaveFormat.Json, 3)]
    public void 版本闸门早于主体解析(SaveFormat format, int version)
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("戊档", format);
        var (held, reference) = SaveAndHold(service, slot);

        var path = service.FilePathOf(slot);
        var bytes = SaveTestSandbox.ReadBytes(path);
        CorruptTail(bytes);
        SaveTestSandbox.WriteBytes(path, bytes);
        SetVersionTo(version, path, format);
        var corrupted = SaveTestSandbox.ReadBytes(path);

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.UnsupportedVersion, exception.Kind);
        AssertFailurePreserves(path, corrupted, held, reference);
    }

    /// <summary>
    /// 契约十四 §10 / 契约十 §6 条款 7：报名级别 MUST 为 <c>null</c> 或 ∈ { <c>JieShi</c>, <c>ShengShi</c> }；
    /// 殿试由省试及第**自动进入**，不是可报名级别 ⇒ 第 ⑩ 条 ⇒ <c>InvalidStructure</c>。
    /// </summary>
    /// <remarks>
    /// 这里刻意走 JSON：<c>"DianShi"</c> 是本版本**认得的枚举名**，故第 ⑤ 步读得出来、第 ⑦ 步
    /// 也放行，只有第 ⑩ 条的合法域判据能拦住它——这正是「枚举合法 ⇏ 语义合法」的分界。
    /// </remarks>
    [Fact]
    public void JSON报名级别为殿试时报InvalidStructure()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("壬档", SaveFormat.Json);
        var (held, reference) = SaveAndHold(service, slot);

        var path = service.FilePathOf(slot);
        RewritePayload(path, state => state["family"]!["members"]!.AsArray()[0]!["appliedExam"] = "DianShi");
        var corrupted = SaveTestSandbox.ReadBytes(path);

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.InvalidStructure, exception.Kind);
        AssertFailurePreserves(path, corrupted, held, reference);
    }

    /// <summary>SC-007 类 ④：头完好、主体字节损坏 ⇒ <c>Malformed</c>。</summary>
    /// <param name="format">格式。</param>
    [Theory]
    [InlineData(SaveFormat.Binary)]
    [InlineData(SaveFormat.Json)]
    public void 头之后字节损坏时报Malformed(SaveFormat format)
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("己档", format);
        var (held, reference) = SaveAndHold(service, slot);

        var path = service.FilePathOf(slot);
        var bytes = SaveTestSandbox.ReadBytes(path);
        CorruptTail(bytes);
        SaveTestSandbox.WriteBytes(path, bytes);
        var corrupted = SaveTestSandbox.ReadBytes(path);

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.Malformed, exception.Kind);
        AssertFailurePreserves(path, corrupted, held, reference);
    }

    // ─────────────────── T056 / T057：非法枚举与未登记状态位 ───────────────────

    /// <summary>
    /// T056 / 契约十 §3 条款 1：JSON 写的是枚举**名**，本版本不认得的名字读不出来 ⇒ 第 ⑤ 步 ⇒ <c>Malformed</c>。
    /// </summary>
    [Fact]
    public void JSON枚举名不在本版本时报Malformed()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("庚档", SaveFormat.Json);
        var (held, reference) = SaveAndHold(service, slot);

        var path = service.FilePathOf(slot);
        RewritePayload(path, state => state["difficulty"] = "无此难度");
        var corrupted = SaveTestSandbox.ReadBytes(path);

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.Malformed, exception.Kind);
        AssertFailurePreserves(path, corrupted, held, reference);
    }

    /// <summary>
    /// T057 / data-model §3.9：JSON 写的是状态标记**名**数组；不认得的名字是「读不出来」⇒ 第 ⑤ 步 ⇒ <c>Malformed</c>。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="二进制未登记状态位时报InvalidStructure"/> 是一对：同一个「未登记」概念，在两种线上形态下
    /// 落在**不同**的判定步上（名字 ⇒ ⑤ 解析失败；位 ⇒ ⑦ 复原被拒），两条用例把该口径钉死。
    /// </remarks>
    [Fact]
    public void JSON未登记状态标记名时报Malformed()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("癸档", SaveFormat.Json);
        var (held, reference) = SaveAndHold(service, slot);

        var path = service.FilePathOf(slot);
        RewritePayload(path, state => state["family"]!["members"]!.AsArray()[0]!["status"] = new JsonArray("无此标记"));
        var corrupted = SaveTestSandbox.ReadBytes(path);

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.Malformed, exception.Kind);
        AssertFailurePreserves(path, corrupted, held, reference);
    }

    /// <summary>
    /// T056 / data-model §3.10：二进制写的是枚举**原值整数**；越界值读得出来，复原时被拒 ⇒ 第 ⑦ 步 ⇒ <c>InvalidStructure</c>。
    /// </summary>
    [Fact]
    public void 二进制枚举整数越界时报InvalidStructure()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("辛档", SaveFormat.Binary);
        var (held, reference) = SaveAndHold(service, slot);

        var dto = SaveStateMapper.ToDto(held);
        dto.Difficulty = (Difficulty)999;

        var path = service.FilePathOf(slot);
        SaveTestSandbox.WriteBytes(
            path, ComposeBinaryFile(dto.Id, MessagePackSerializer.Serialize(dto, SaveFileFormat.BinaryOptions)));
        var corrupted = SaveTestSandbox.ReadBytes(path);

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.InvalidStructure, exception.Kind);
        AssertFailurePreserves(path, corrupted, held, reference);
    }

    /// <summary>
    /// T057 / data-model §3.9：二进制里的**未登记位**（<c>1 &lt;&lt; 9</c> 及以上）读得出来，
    /// 复原时被「本版本只定义九位」的检查拒绝 ⇒ 第 ⑦ 步 ⇒ <c>InvalidStructure</c>。
    /// </summary>
    [Fact]
    public void 二进制未登记状态位时报InvalidStructure()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("壬档", SaveFormat.Binary);
        var (held, reference) = SaveAndHold(service, slot);

        var dto = SaveStateMapper.ToDto(held);
        dto.Family.Members[0].Status = (StatusFlag)(1 << 9);

        var path = service.FilePathOf(slot);
        SaveTestSandbox.WriteBytes(
            path, ComposeBinaryFile(dto.Id, MessagePackSerializer.Serialize(dto, SaveFileFormat.BinaryOptions)));
        var corrupted = SaveTestSandbox.ReadBytes(path);

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.InvalidStructure, exception.Kind);
        AssertFailurePreserves(path, corrupted, held, reference);
    }

    /// <summary>
    /// 契约十 §6 第 ② 步：空文件或**头记录内部**被截断的二进制档 ⇒ <c>Malformed</c>。
    /// </summary>
    /// <remarks>
    /// 头读取越过流尾时 MessagePack 抛的是 <c>EndOfStreamException</c>（不是
    /// <c>MessagePackSerializationException</c>）；它 MUST 被归类成失败信号，而不是裸奔到调用方
    /// （审核 2.1）。既有用例只损坏**尾部**，这条把「头本身不完整」补上。
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void 头记录不完整的二进制档报Malformed(int remainingBytes)
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("子档", SaveFormat.Binary);
        var (held, reference) = SaveAndHold(service, slot);

        var path = service.FilePathOf(slot);
        SaveTestSandbox.TruncateTo(path, remainingBytes);
        var corrupted = SaveTestSandbox.ReadBytes(path);

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.Malformed, exception.Kind);
        AssertFailurePreserves(path, corrupted, held, reference);
    }

    /// <summary>
    /// 契约十 §5 条款 3（「缺失 MUST 被检出、MUST NOT 静默默认化」）：JSON 里集合成员写成
    /// <c>null</c> ⇒ <c>InvalidStructure</c>。
    /// </summary>
    /// <remarks>
    /// <c>[JsonRequired]</c> 只保证键**在**，挡不住 <c>null</c>；若放行，复原时会被当成空集合
    /// 静默载入（审核 2.3）。
    /// </remarks>
    [Theory]
    [InlineData("family.members")]
    [InlineData("family.settledOutsiders")]
    [InlineData("economy.ledger")]
    public void JSON集合成员为null时报InvalidStructure(string memberName)
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("丑档", SaveFormat.Json);
        var (held, reference) = SaveAndHold(service, slot);

        var path = service.FilePathOf(slot);
        var parts = memberName.Split('.');
        RewritePayload(path, state => state[parts[0]]![parts[1]] = null);
        var corrupted = SaveTestSandbox.ReadBytes(path);

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.InvalidStructure, exception.Kind);
        AssertFailurePreserves(path, corrupted, held, reference);
    }

    /// <summary>
    /// 契约十 §5 条款 3：JSON 集合里的 <c>null</c> **元素**（如 <c>"members":[null]</c>）
    /// MUST 以 <c>InvalidStructure</c> 浮出，MUST NOT 让调用方看到裸 <c>NullReferenceException</c>（审核 2.2）。
    /// </summary>
    [Fact]
    public void JSON集合含null元素时报InvalidStructure()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("寅档", SaveFormat.Json);
        var (held, reference) = SaveAndHold(service, slot);

        var path = service.FilePathOf(slot);
        RewritePayload(path, state => state["family"]!["members"]!.AsArray()[0] = null);
        var corrupted = SaveTestSandbox.ReadBytes(path);

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.InvalidStructure, exception.Kind);
        AssertFailurePreserves(path, corrupted, held, reference);
    }

    /// <summary>
    /// 契约十 §2：头成员**次序固定**（<c>FormatVersion</c> → <c>PayloadLength</c> → <c>SaveId</c>），
    /// 次序不符 ⇒ <c>Malformed</c>；二进制侧由 <c>fixarray(3)</c> 天然强制，JSON 侧必须显式判定。
    /// </summary>
    [Fact]
    public void JSON头成员次序不符时报Malformed()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("卯档", SaveFormat.Json);
        var (held, reference) = SaveAndHold(service, slot);

        var path = service.FilePathOf(slot);
        var file = JsonNode.Parse(ReadText(path))!.AsObject();
        var reordered = new JsonObject
        {
            ["PayloadLength"] = file["PayloadLength"]!.DeepClone(),
            ["FormatVersion"] = file["FormatVersion"]!.DeepClone(),
            ["SaveId"] = file["SaveId"]!.DeepClone(),
            ["State"] = file["State"]!.DeepClone(),
        };

        SaveTestSandbox.WriteBytes(path, Encoding.UTF8.GetBytes(reordered.ToJsonString(PayloadJsonOptions)));
        var corrupted = SaveTestSandbox.ReadBytes(path);

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.Malformed, exception.Kind);
        AssertFailurePreserves(path, corrupted, held, reference);
    }

    /// <summary>契约十 §2：同一个头成员重复出现 ⇒ <c>Malformed</c>（MUST NOT 让「后者胜」）。</summary>
    [Fact]
    public void JSON头成员重复时报Malformed()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("辰档", SaveFormat.Json);
        var (held, reference) = SaveAndHold(service, slot);

        var path = service.FilePathOf(slot);
        var file = JsonNode.Parse(ReadText(path))!.AsObject();
        var stateJson = file["State"]!.ToJsonString(PayloadJsonOptions);
        var version = SaveFileFormat.Version.ToString(CultureInfo.InvariantCulture);
        var duplicated = string.Concat(
            "{\n  \"FormatVersion\": ", version,
            ",\n  \"FormatVersion\": ", version,
            ",\n  \"PayloadLength\": ", Encoding.UTF8.GetByteCount(stateJson).ToString(CultureInfo.InvariantCulture),
            ",\n  \"SaveId\": \"", file["SaveId"]!.GetValue<string>(),
            "\",\n  \"State\": ", stateJson, "\n}");

        SaveTestSandbox.WriteBytes(path, Encoding.UTF8.GetBytes(duplicated));
        var corrupted = SaveTestSandbox.ReadBytes(path);

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.Malformed, exception.Kind);
        AssertFailurePreserves(path, corrupted, held, reference);
    }

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
