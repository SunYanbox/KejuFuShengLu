using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save;
using Xunit;

namespace KFL.Tests.Infrastructure;

/// <summary>
/// 原子写入的验收测试（004-save-system；契约九 §3 条款 6 / tasks.md T037）。
/// </summary>
/// <remarks>
/// 写入中途失败的注入点是 <see cref="AtomicFileWriter.OnChunkWritten"/>——
/// 契约九 §3 条款 6 明确要求它存在，正是为了「不杀进程也能验出半成品」。
/// </remarks>
public sealed class AtomicWriteTests
{
    private static readonly byte[] OldPayload = [.. Enumerable.Repeat((byte)0x5A, 8)];

    /// <summary>FR-013：写入中途失败 ⇒ 目标档仍是**旧的完整内容**，且不留临时文件。</summary>
    [Fact]
    public void 写入中途失败时目标档保持旧的完整内容()
    {
        using var sandbox = new SaveTestSandbox();
        SaveCatalog.EnsureDirectory(sandbox.Root);
        var target = sandbox.PathOf("甲档", SaveFormat.Binary);
        SaveTestSandbox.WriteBytes(target, OldPayload);

        var writer = new AtomicFileWriter();
        var chunks = 0;
        writer.OnChunkWritten = _ =>
        {
            if (++chunks == 2)
            {
                throw new IOException("注入：写到一半失败");
            }
        };

        Assert.Throws<IOException>(
            () => writer.Write(target, new byte[(AtomicFileWriter.ChunkSize * 3) + 1]));

        Assert.Equal(OldPayload, SaveTestSandbox.ReadBytes(target));
        Assert.Single(sandbox.RootFiles());
    }

    /// <summary>FR-013：首次写入中途失败 ⇒ 既没有半成品，也没有残留临时文件。</summary>
    [Fact]
    public void 首次写入中途失败时不留残档与临时文件()
    {
        using var sandbox = new SaveTestSandbox();
        SaveCatalog.EnsureDirectory(sandbox.Root);

        var writer = new AtomicFileWriter();
        writer.OnChunkWritten = _ => throw new IOException("注入：第一个分片就失败");
        var target = sandbox.PathOf("乙档", SaveFormat.Binary);

        Assert.Throws<IOException>(() => writer.Write(target, new byte[16]));

        Assert.Empty(sandbox.RootFiles());
    }

    /// <summary>契约九 §7：备份失败 MUST NOT 影响已写好的当前档，也 MUST NOT 被吞掉。</summary>
    [Fact]
    public void 备份失败不影响当前档且不留临时文件()
    {
        using var sandbox = new SaveTestSandbox();
        var writer = new AtomicFileWriter();
        var service = sandbox.CreateService(writer);
        var slot = new SaveSlot("甲档");

        service.Save(SaveFixtures.PlainState(), slot, overwrite: false);
        var before = SaveTestSandbox.ReadBytes(service.FilePathOf(slot));

        writer.OnChunkWritten = _ => throw new IOException("注入：备份写到一半失败");

        Assert.Throws<IOException>(() => service.Backup(slot));

        Assert.Equal(before, SaveTestSandbox.ReadBytes(service.FilePathOf(slot)));
        Assert.Empty(sandbox.BackupFiles());
        Assert.NotNull(service.Load(slot));
    }

    /// <summary>成功写入后只有目标档一个文件：临时文件绝不留到提交点之后。</summary>
    [Fact]
    public void 成功写入后不留临时文件()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("甲档");

        service.Save(SaveFixtures.PlainState(), slot, overwrite: false);

        Assert.Single(sandbox.RootFiles());
        Assert.DoesNotContain(
            sandbox.RootFiles(),
            path => path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
    }
}
