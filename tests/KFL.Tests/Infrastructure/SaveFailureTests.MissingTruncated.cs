using System.Text.Json.Nodes;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save;
using Xunit;

namespace KFL.Tests.Infrastructure;

public sealed partial class SaveFailureTests
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
}
