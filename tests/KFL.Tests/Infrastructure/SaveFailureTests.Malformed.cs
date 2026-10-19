using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save;
using Xunit;

namespace KFL.Tests.Infrastructure;

public sealed partial class SaveFailureTests
{
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
}
