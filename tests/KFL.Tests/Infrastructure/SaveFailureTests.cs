using System.Text;
using System.Text.Json.Nodes;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save;
using Xunit;

namespace KFL.Tests.Infrastructure;

/// <summary>
/// 读档失败的分类验收测试（004-save-system；契约十 §6 / SC-007 / tasks.md T044）。
/// </summary>
/// <remarks>
/// <para>
/// 四类输入对应契约十 §6 的八个判定步：不存在 ⇒ <c>Missing</c>；头之后字节损坏 ⇒ <c>Malformed</c>；
/// 剩余字节不足头声明 ⇒ <c>Truncated</c>；版本不是 1 ⇒ <c>UnsupportedVersion</c>；
/// 主体可解析但成员缺失或违反一致性 ⇒ <c>InvalidStructure</c>。
/// </para>
/// <para>
/// 失败分类 MUST NOT 依赖异常文本（契约十 §6 条款 3），故这里一律断言
/// <see cref="SaveFileException.Kind"/>。
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

    /// <summary>SC-007 类 ②：二进制档被截到 60% ⇒ <c>Truncated</c>（头完好、剩余不足）。</summary>
    [Fact]
    public void 二进制档被截断时报Truncated()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("甲档", SaveFormat.Binary);

        service.Save(SaveFixtures.PlainState(), slot, overwrite: false);
        var path = service.FilePathOf(slot);
        var original = SaveTestSandbox.ReadBytes(path);
        Assert.True(original.Length > 64, "夹具太薄，截到 60% 会连头都保不住。");

        SaveTestSandbox.TruncateTo(path, original.Length * 3 / 5);

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.Truncated, exception.Kind);
    }

    /// <summary>SC-007 类 ③：合法 JSON 少一个必填成员 ⇒ <c>InvalidStructure</c>。</summary>
    [Fact]
    public void JSON缺必填成员时报InvalidStructure()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("乙档", SaveFormat.Json);

        service.Save(SaveFixtures.PlainState(), slot, overwrite: false);
        RemovePayloadMember(service.FilePathOf(slot), "currentMonth");

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.InvalidStructure, exception.Kind);
    }

    /// <summary>SC-007 类 ③：主体可解析但字段违反 Core 不变式（月份 13）⇒ <c>InvalidStructure</c>。</summary>
    [Fact]
    public void JSON字段违反不变式时报InvalidStructure()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("丙档", SaveFormat.Json);

        service.Save(SaveFixtures.PlainState(), slot, overwrite: false);

        // 替换成同长度的非法值，**不改动头声明的长度**（PayloadLength 不是校验和）。
        var path = service.FilePathOf(slot);
        var text = Encoding.UTF8.GetString(SaveTestSandbox.ReadBytes(path));
        var replaced = text.Replace("\"currentMonth\": 1,", "\"currentMonth\": 13,", StringComparison.Ordinal);
        Assert.NotEqual(text, replaced);
        SaveTestSandbox.WriteBytes(path, Encoding.UTF8.GetBytes(replaced));

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.InvalidStructure, exception.Kind);
    }

    /// <summary>FR-015：头记录版本不为 1 ⇒ <c>UnsupportedVersion</c>。</summary>
    [Fact]
    public void 头版本不为一时报UnsupportedVersion()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("丁档", SaveFormat.Binary);

        service.Save(SaveFixtures.PlainState(), slot, overwrite: false);
        var path = service.FilePathOf(slot);
        var bytes = SaveTestSandbox.ReadBytes(path);
        bytes[1] = 0x02;
        SaveTestSandbox.WriteBytes(path, bytes);

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.UnsupportedVersion, exception.Kind);
    }

    /// <summary>契约十 §6 条款 1：版本闸门 MUST 早于任何主体解析——主体同时损坏也先报版本。</summary>
    [Fact]
    public void 版本闸门早于主体解析()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("戊档", SaveFormat.Binary);

        service.Save(SaveFixtures.PlainState(), slot, overwrite: false);
        var path = service.FilePathOf(slot);
        var bytes = SaveTestSandbox.ReadBytes(path);
        bytes[1] = 0x02;
        CorruptTail(bytes);
        SaveTestSandbox.WriteBytes(path, bytes);

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.UnsupportedVersion, exception.Kind);
    }

    /// <summary>SC-007 类 ④：头完好、主体字节损坏 ⇒ <c>Malformed</c>。</summary>
    [Fact]
    public void 头之后字节损坏时报Malformed()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("己档", SaveFormat.Binary);

        service.Save(SaveFixtures.PlainState(), slot, overwrite: false);
        var path = service.FilePathOf(slot);
        var bytes = SaveTestSandbox.ReadBytes(path);
        CorruptTail(bytes);
        SaveTestSandbox.WriteBytes(path, bytes);

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.Malformed, exception.Kind);
    }

    /// <summary>用 0xC1（MessagePack 明令「永不使用」的字节）把尾部写坏。</summary>
    /// <param name="bytes">文件字节（就地修改）。</param>
    private static void CorruptTail(byte[] bytes)
    {
        for (var index = Math.Max(0, bytes.Length - 8); index < bytes.Length; index++)
        {
            bytes[index] = 0xC1;
        }
    }

    /// <summary>把一个主体成员整个删掉，并**保证结果仍是合法 JSON**（只删成员，不碰头）。</summary>
    /// <param name="path">档路径。</param>
    /// <param name="memberName">成员名。</param>
    private static void RemovePayloadMember(string path, string memberName)
    {
        var node = JsonNode.Parse(SaveTestSandbox.ReadBytes(path))!.AsObject();
        var state = node["State"]!.AsObject();

        Assert.True(state.Remove(memberName), $"测试前提失败：主体里没有 {memberName}。");

        SaveTestSandbox.WriteBytes(path, Encoding.UTF8.GetBytes(node.ToJsonString()));
    }
}
