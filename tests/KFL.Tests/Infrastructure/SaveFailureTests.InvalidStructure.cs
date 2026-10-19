using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save;
using KFL.Infrastructure.Save.Serialization;
using MessagePack;
using Xunit;

namespace KFL.Tests.Infrastructure;

public sealed partial class SaveFailureTests
{
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
}
