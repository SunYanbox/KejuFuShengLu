using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save;

namespace KFL.Tests.Infrastructure;

/// <summary>
/// 存档测试的**唯一**临时目录与文件助手（004-save-system；契约九 §4.1，tasks.md T021）。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么必须只有这一个</b>：G-07 的扫描面覆盖 <c>tests\KFL.Tests\Infrastructure\</c>，
/// 每一个文件系统调用行都要带**同行**豁免理由。把「造临时目录、读字节、
/// 列文件、把备份复制回主档、清理」全收进一个类型，豁免行就是**可数尽**的（本文件共 12 行）——
/// 散在各测试里则会变成一笔没人能核对的账（SC-008）。
/// </para>
/// <para>
/// <b>每个用例一个根</b>：根名带一个随机后缀，故并行跑测试类也不互相踩。
/// </para>
/// </remarks>
public sealed class SaveTestSandbox : IDisposable
{
    private bool _disposed;

    /// <summary>建一个空的临时存档根（目录本身**不预建**：让被测代码自己决定何时建）。</summary>
    /// <remarks>
    /// 根落在**测试程序集目录**下（<c>bin/…</c>，已被 <c>.gitignore</c> 忽略）而不是**系统临时目录**：
    /// 受限宿主只允许子进程写工作区，系统临时目录里的建目录会被拒为
    /// <c>UnauthorizedAccessException</c>；程序集旁始终可写，且在两种环境下都成立。
    /// </remarks>
    public SaveTestSandbox()
    {
        ParentDirectory = SaveCatalog.Combine(AppContext.BaseDirectory, "save-tests");
        Root = SaveCatalog.Combine(ParentDirectory, Guid.NewGuid().ToString("N")); // arch-guard:allow 测试沙箱：临时根 + 随机后缀（唯一允许出现随机源的地方就是这里）
        Directory.CreateDirectory(ParentDirectory); // arch-guard:allow 测试沙箱：只预建父目录，根本身仍留给被测代码自己建
    }

    /// <summary>存档根。</summary>
    public string Root { get; }

    /// <summary>所有用例临时根共用的父目录。</summary>
    public string ParentDirectory { get; }

    /// <summary>在**根路径处**放一个文件（令建目录必然失败，用于验收「根建不出来 ⇒ SaveAccessException」）。</summary>
    public void BlockRootWithFile() =>
        File.WriteAllBytes(Root, [0x01]); // arch-guard:allow 测试沙箱：造「根路径被文件占住」的现场

    /// <summary>备份目录（<c>&lt;root&gt;/backups</c>）。</summary>
    public string BackupsDirectory =>
        SaveCatalog.Combine(Root, SaveFileFormat.BackupsDirectoryName);

    /// <summary>建一个把存档根指向本沙箱的服务。</summary>
    /// <returns>存档服务。</returns>
    public SaveService CreateService() => new(new SaveOptions { RootDirectory = Root });

    /// <summary>建一个把存档根指向本沙箱、并注入给定写入器的服务（用于写入中途失败的用例）。</summary>
    /// <param name="writer">原子写入器（可带测试钩子）。</param>
    /// <returns>存档服务。</returns>
    public SaveService CreateService(AtomicFileWriter writer) =>
        new(new SaveOptions { RootDirectory = Root }, writer);

    /// <summary>主档路径。</summary>
    /// <param name="name">存档名。</param>
    /// <param name="format">格式。</param>
    /// <returns>路径。</returns>
    public string PathOf(string name, SaveFormat format) =>
        SaveCatalog.Combine(Root, name + SaveFileFormat.ExtensionOf(format));

    /// <summary>备份路径。</summary>
    /// <param name="name">存档名。</param>
    /// <param name="format">格式。</param>
    /// <returns>路径。</returns>
    public string BackupPathOf(string name, SaveFormat format) =>
        SaveCatalog.Combine(BackupsDirectory, name + SaveFileFormat.ExtensionOf(format));

    /// <summary>列出存档根下的全部文件路径（目录不存在 ⇒ 空）。</summary>
    /// <returns>文件路径。</returns>
    public IReadOnlyList<string> RootFiles() => ListFiles(Root);

    /// <summary>列出备份目录下的全部文件路径（目录不存在 ⇒ 空）。</summary>
    /// <returns>文件路径。</returns>
    public IReadOnlyList<string> BackupFiles() => ListFiles(BackupsDirectory);

    /// <summary>读文件全部字节。</summary>
    /// <param name="path">路径。</param>
    /// <returns>字节。</returns>
    public static byte[] ReadBytes(string path) =>
        File.ReadAllBytes(path); // arch-guard:allow 测试沙箱：读字节供断言与「损坏注入」

    /// <summary>写文件全部字节（**损坏注入**用：截断、改版本号、投毒）。</summary>
    /// <param name="path">路径。</param>
    /// <param name="bytes">字节。</param>
    public static void WriteBytes(string path, byte[] bytes) =>
        File.WriteAllBytes(path, bytes); // arch-guard:allow 测试沙箱：写字节，用于构造截断/损坏/版本不符的档

    /// <summary>把文件截到前 <paramref name="byteCount"/> 个字节（构造截断档）。</summary>
    /// <param name="path">路径。</param>
    /// <param name="byteCount">保留的字节数。</param>
    public static void TruncateTo(string path, int byteCount)
    {
        var bytes = ReadBytes(path);
        WriteBytes(path, bytes[..byteCount]);
    }

    /// <summary>把备份**复制回**主档位置（FR-016 的回退路径）。</summary>
    /// <param name="name">存档名。</param>
    /// <param name="format">格式。</param>
    public void CopyBackupOverPrimary(string name, SaveFormat format)
    {
        var backup = BackupPathOf(name, format);
        var primary = PathOf(name, format);

        File.Copy(backup, primary, overwrite: true); // arch-guard:allow 测试沙箱：模拟「把备份复制回主文件」这一回退动作
    }

    /// <summary>删除沙箱根（递归）。</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (Directory.Exists(Root)) // arch-guard:allow 测试沙箱：清理前先看临时根在不在（存在即目录）
        {
            Directory.Delete(Root, recursive: true); // arch-guard:allow 测试沙箱：递归删除本用例的临时根
        }
        else if (File.Exists(Root)) // arch-guard:allow 测试沙箱：BlockRootWithFile 造出来的挡路文件
        {
            File.Delete(Root); // arch-guard:allow 测试沙箱：删掉挡路文件
        }
    }

    private static IReadOnlyList<string> ListFiles(string directory)
    {
        if (!Directory.Exists(directory)) // arch-guard:allow 测试沙箱：列目录（目录不存在即空，与 SaveCatalog 同款口径）
        {
            return [];
        }

        return [.. Directory.EnumerateFiles(directory)]; // arch-guard:allow 测试沙箱：枚举目录项
    }
}
