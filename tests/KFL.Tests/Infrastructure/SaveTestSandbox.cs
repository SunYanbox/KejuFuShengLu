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
    /// <para>
    /// 根的父目录按**优先次序**选：① 系统临时目录（<c>GetTempPath</c>）——FR-022 与
    /// SC-009 的正解（测试落在临时目录、MUST NOT 污染仓库工作区）；② 测试程序集旁（<c>bin/…</c>，
    /// 已被 <c>.gitignore</c> 忽略）——受限宿主只允许子进程写工作区，系统临时目录里的建目录会拿到
    /// <c>UnauthorizedAccessException</c>（`AGENTS.md`「坑」一节）。
    /// </para>
    /// <para>
    /// 回落位置同样不污染工作区、也不干扰架构守卫：守卫在**进入** <c>bin/obj</c> 之前就剪枝
    /// （<c>RepositoryLocator</c>），<see cref="Dispose"/> 再尽力回收空的父目录（T051）。
    /// </para>
    /// </remarks>
    public SaveTestSandbox()
    {
        ParentDirectory = EnsureParentDirectory();
        Root = SaveCatalog.Combine(ParentDirectory, Guid.NewGuid().ToString("N")); // arch-guard:allow 测试沙箱：临时根 + 随机后缀（唯一允许出现随机源的地方就是这里）
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

    /// <summary>删除沙箱根（递归），并尽力回收空的父目录。</summary>
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

        ReleaseParentDirectory();
    }

    /// <summary>父目录候选，按优先次序（见构造函数 remarks）。</summary>
    private static IEnumerable<string> ParentDirectoryCandidates
    {
        get
        {
            yield return SaveCatalog.Combine(Path.GetTempPath(), "kfl-save-tests"); // arch-guard:allow 测试沙箱：首选系统临时目录（FR-022 / SC-009）
            yield return SaveCatalog.Combine(AppContext.BaseDirectory, "save-tests");
        }
    }

    /// <summary>
    /// 选定并**预建**父目录（根本身仍留给被测代码自己建）；候选全部失败即抛。
    /// </summary>
    /// <returns>父目录绝对路径。</returns>
    /// <remarks>
    /// 每个候选**重试一次**：同一进程里并行跑的用例会在 <see cref="Dispose"/> 里回收空的父目录
    /// （<see cref="ReleaseParentDirectory"/>），「建目录」与「删空目录」撞在一起时第一次可能拿到
    /// <c>IOException</c>；重试即可建回（这曾表现为千分之一的偶发失败）。
    /// </remarks>
    private static string EnsureParentDirectory()
    {
        Exception? lastFailure = null;

        foreach (var candidate in ParentDirectoryCandidates)
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    Directory.CreateDirectory(candidate); // arch-guard:allow 测试沙箱：预建临时根的父目录（候选与次序见构造函数的 remarks）
                    return candidate;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    lastFailure = ex;
                }
            }
        }

        throw new InvalidOperationException(
            $"测试沙箱：系统临时目录与测试程序集旁都建不出临时根父目录（最后失败：{lastFailure?.Message}）。",
            lastFailure);
    }

    /// <summary>
    /// 尽力删掉**空**的父目录（否则测试跑完会在 <c>bin/</c> 下留一个空目录）。
    /// 并发的用例还留着根时它非空、删不掉，那就留着——下一次建根时会连同父目录一起重建。
    /// </summary>
    private void ReleaseParentDirectory()
    {
        try
        {
            Directory.Delete(ParentDirectory); // arch-guard:allow 测试沙箱：只删**空**父目录，非空/不存在都会抛 IOException
        }
        catch (IOException)
        {
            // 并发用例还留着根（非空）或目录已不在：留着即可。
        }
        catch (UnauthorizedAccessException)
        {
            // 父目录被别的进程占住：留着即可（空目录不是残留产物）。
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
