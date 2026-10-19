using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save;
using Xunit;

namespace KFL.Tests.Infrastructure;

public sealed partial class SaveFailureTests
{
    /// <summary>FR-015：头记录版本不为本版本（旧版 2、旧版 3 与更新版 +1 同样拒）⇒ <c>UnsupportedVersion</c>。</summary>
    /// <remarks>
    /// 版本 3 是 007 之前的格式（`FamilyLifecycleDto` 只有 6 键、无 `matchmakeApplications`）；
    /// 它在本版本下是**残缺**的，必须在版本闸门处拒绝，MUST NOT 交给缺键校验兜底（契约十 §6 条款 1、SC-010）。
    /// </remarks>
    /// <param name="format">格式。</param>
    /// <param name="version">写进头里的非本版本版本号。</param>
    [Theory]
    [InlineData(SaveFormat.Binary, 2)]
    [InlineData(SaveFormat.Json, 2)]
    [InlineData(SaveFormat.Binary, 3)]
    [InlineData(SaveFormat.Json, 3)]
    [InlineData(SaveFormat.Binary, SaveFileFormat.Version + 1)]
    [InlineData(SaveFormat.Json, SaveFileFormat.Version + 1)]
    public void 头版本不为本版本时报UnsupportedVersion(SaveFormat format, int version)
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
    [InlineData(SaveFormat.Binary, 2)]
    [InlineData(SaveFormat.Json, 2)]
    [InlineData(SaveFormat.Binary, 3)]
    [InlineData(SaveFormat.Json, 3)]
    [InlineData(SaveFormat.Binary, SaveFileFormat.Version + 1)]
    [InlineData(SaveFormat.Json, SaveFileFormat.Version + 1)]
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
}
