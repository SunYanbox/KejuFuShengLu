using System.Text.Json;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save;
using KFL.Infrastructure.Save.Serialization;
using MessagePack;
using Xunit;

namespace KFL.Tests.Infrastructure;

public sealed partial class SaveFormatContractTests
{
    /// <summary>契约十 §2：二进制头 = <c>0x93</c>（fixarray(3)）+ 版本 + 主体长度 + 标识。</summary>
    [Fact]
    public void 二进制头是三元素数组且成员次序固定()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("甲档", SaveFormat.Binary);
        var state = SaveFixtures.PlainState();

        service.Save(state, slot, overwrite: false);
        var bytes = SaveTestSandbox.ReadBytes(service.FilePathOf(slot));

        Assert.Equal(0x93, bytes[0]);

        var header = new MessagePackSaveFormat().ReadHeader(bytes);
        Assert.Equal(SaveFileFormat.Version, header.FormatVersion);
        Assert.Equal(state.Id, header.SaveId);
        Assert.Equal(bytes.Length - HeaderLength(bytes), header.PayloadLength);
    }

    /// <summary>契约十 §2：JSON 头上恰好三个成员，<c>State</c> 紧随其后。</summary>
    [Fact]
    public void JSON头成员只有三个且次序固定()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("甲档", SaveFormat.Json);

        service.Save(SaveFixtures.PlainState(), slot, overwrite: false);

        using var document = JsonDocument.Parse(SaveTestSandbox.ReadBytes(service.FilePathOf(slot)));
        var names = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();

        Assert.Equal(["FormatVersion", "PayloadLength", "SaveId", "State"], names);
        Assert.Equal(SaveFileFormat.Version, document.RootElement.GetProperty("FormatVersion").GetInt32());
    }

    /// <summary>契约十 §3 / data-model §3.9：<c>StatusFlag.None</c> 写成空数组，而非 <c>["None"]</c>。</summary>
    [Fact]
    public void 无状态位在JSON里写成空数组()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("甲档", SaveFormat.Json);

        service.Save(SaveFixtures.PlainState(), slot, overwrite: false);

        using var document = JsonDocument.Parse(SaveTestSandbox.ReadBytes(service.FilePathOf(slot)));
        var status = document.RootElement
            .GetProperty("State")
            .GetProperty("family")
            .GetProperty("members")[0]
            .GetProperty("status");

        Assert.Equal(JsonValueKind.Array, status.ValueKind);
        Assert.Equal(0, status.GetArrayLength());
    }

    /// <summary>契约十 §5：两种格式承载同一信息集（同一状态经两格式读回后逐字段相等）。</summary>
    /// <remarks>
    /// 两侧各用一个**独立根**：同一个根下「同一 <c>SaveId</c> 并存于两个文件」会被
    /// <c>SaveIdentityConflictException</c> 拒绝（契约九 §5 的唯一性不变式），
    /// 那不是本用例要验证的东西。
    /// </remarks>
    [Fact]
    public void 两种格式承载同一信息集()
    {
        var state = SaveFixtures.RichState();

        using var binarySandbox = new SaveTestSandbox();
        var binarySlot = new SaveSlot("甲档", SaveFormat.Binary);
        binarySandbox.CreateService().Save(state, binarySlot, overwrite: false);
        var fromBinary = binarySandbox.CreateService().Load(binarySlot);

        using var jsonSandbox = new SaveTestSandbox();
        var jsonSlot = new SaveSlot("甲档", SaveFormat.Json);
        jsonSandbox.CreateService().Save(state, jsonSlot, overwrite: false);
        var fromJson = jsonSandbox.CreateService().Load(jsonSlot);

        SaveStateComparer.AssertEquivalent(fromBinary, fromJson);
    }

    /// <summary>数出二进制头有多少字节（0x93 + 版本 + 长度 + 标识）。</summary>
    /// <param name="bytes">文件字节。</param>
    /// <returns>头长度。</returns>
    private static int HeaderLength(byte[] bytes)
    {
        var reader = new MessagePackReader(bytes);
        Assert.Equal(3, reader.ReadArrayHeader());
        reader.ReadInt32();
        reader.ReadInt32();
        reader.ReadString();

        return checked((int)reader.Consumed);
    }
}
