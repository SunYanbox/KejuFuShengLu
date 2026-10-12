using System.Diagnostics.CodeAnalysis;

namespace KFL.Infrastructure.Save;

/// <summary>
/// 原子写：**同目录**临时文件 → 落盘刷盘 → 同卷改名替换（004-save-system；契约九 §3，FR-013）。
/// </summary>
/// <remarks>
/// <para>
/// 目标文件在**任何时刻**要么是完整的旧档、要么是完整的新档，绝不会是半个——进程被杀、
/// 磁盘写满、中途无权限都一样。这是靠三件事次第成立的：
/// </para>
/// <list type="number">
/// <item><description>临时文件建在**目标所在目录**（同卷，改名才是原子替换而非跨卷拷贝）；</description></item>
/// <item><description>写完先 <c>Flush(flushToDisk: true)</c> 再改名——否则掉电后可能得到一个「名字换好了、内容还在缓存里」的空壳；</description></item>
/// <item><description>改名用**带覆盖的同卷移动**一步替换。</description></item>
/// </list>
/// <para>
/// 失败路径**最佳努力**清掉临时文件，且 MUST NOT 触碰目标文件。
/// </para>
/// <para>
/// 本类型与 <see cref="SaveCatalog"/> 是存档层仅有的两处文件系统访问点（契约九 §4.1）；
/// 每一行落在这里的文件系统调用都带**同行** <c>// arch-guard:allow</c> 理由（SC-008）——
/// 注意是同行：注释写在上一行不算豁免，扫描是逐行的。
/// </para>
/// </remarks>
public sealed class AtomicFileWriter
{
    /// <summary>写入分块大小（字节）。</summary>
    /// <remarks>
    /// 存在的理由是**可测**：整块一次 <c>Write</c> 的话，「写到一半失败时目标文件仍完好」
    /// 这条不变量就只能靠杀进程来验；分块 + <see cref="OnChunkWritten"/> 让它变成普通单测。
    /// </remarks>
    public const int ChunkSize = 4096;

    /// <summary>临时文件名里保留的档名前缀长度（T063：让临时路径长度与档名长度脱钩）。</summary>
    private const int TempNamePrefixLength = 16;

    /// <summary>
    /// **测试专用**的写入钩子：每写完一块调用一次，参数是**已写入的累计字节数**。
    /// </summary>
    /// <remarks>
    /// 抛异常即模拟「写到一半失败」（契约九 §3 第 6 条）。生产代码 MUST NOT 赋值。
    /// 做成**实例**成员而非静态：xUnit 默认跨测试类并行，静态钩子会互相串扰。
    /// </remarks>
    public Action<int>? OnChunkWritten { get; set; }

    /// <summary>把字节原子写到目标路径。</summary>
    /// <param name="targetPath">目标文件的绝对或相对路径。</param>
    /// <param name="payload">要写入的完整字节。</param>
    /// <exception cref="ArgumentException"><paramref name="targetPath"/> 为空。</exception>
    public void Write(string targetPath, ReadOnlySpan<byte> payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        var tempPath = TempPathFor(targetPath);
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) // arch-guard:allow 契约九 §4.1：在目标同目录建临时文件（原子替换的第 1 步）
            {
                WriteChunks(stream, payload);

                stream.Flush(flushToDisk: true); // arch-guard:allow 契约九 §3 第 2 步：改名之前 MUST 落盘刷盘，否则掉电会留下空壳
            }

            File.Move(tempPath, targetPath, overwrite: true); // arch-guard:allow 契约九 §3 第 3 步：同卷原子改名替换，目标此刻要么旧档要么新档
        }
        catch (Exception)
        {
            TryDelete(tempPath);
            throw;
        }
    }

    /// <summary>把源文件原子复制到目标路径（备份用；覆盖式）。</summary>
    /// <param name="sourcePath">源文件路径。</param>
    /// <param name="targetPath">目标文件路径。</param>
    public void Copy(string sourcePath, string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var bytes = File.ReadAllBytes(sourcePath); // arch-guard:allow 契约九 §7：备份 = 把当前档完整拷一份过去；源文件由调用方保证存在
        Write(targetPath, bytes);
    }

    /// <summary>改名（**纯移动**，字节不变；改变文件位置的另一个入口）。</summary>
    /// <remarks>
    /// 契约九 §6：主档改名 MUST 是**纯移动**——只要字节不动，<c>SaveId</c> 与全部内容
    /// （含 <c>Family.Name</c>）就必然不变，这正是 FR-012 要的「改档名不变性」。
    /// </remarks>
    /// <param name="sourcePath">源文件路径。</param>
    /// <param name="targetPath">目标文件路径（不存在才移动得动）。</param>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "与 Write/Copy/Delete 同属契约九 §4.1 的那一个文件系统访问点，保持同一实例调用形态。")]
    public void Move(string sourcePath, string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        try
        {
            File.Move(sourcePath, targetPath); // arch-guard:allow 契约九 §6：改名 = 纯移动，不改一个字节（同名目标存在即失败，不覆盖）
        }
        catch (IOException ex)
        {
            throw new SaveAccessException($"存档改名失败：{sourcePath} → {targetPath}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new SaveAccessException($"存档改名被拒：{sourcePath} → {targetPath}", ex);
        }
    }

    /// <summary>删除文件（最佳努力；用于移除同名异构档与旧格的备份）。</summary>
    /// <param name="path">文件路径。</param>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "本类型就是契约九 §4.1 里的一个文件系统访问点；Delete 与 Write/Copy 属同一访问点，"
            + "保持同一实例调用形态（SaveService 只持有一个写入器字段）比静态/实例各半更好读。")]
    public void Delete(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        TryDelete(path);
    }

    private void WriteChunks(Stream stream, ReadOnlySpan<byte> payload)
    {
        var written = 0;
        while (written < payload.Length)
        {
            var count = Math.Min(ChunkSize, payload.Length - written);
            stream.Write(payload.Slice(written, count));
            written += count;
            OnChunkWritten?.Invoke(written);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path); // arch-guard:allow 契约九 §3 失败路径最佳努力清理临时文件；对不存在的路径是空操作
        }
        catch (IOException)
        {
            // 清理失败不改变任何已成立的事实：目标文件始终未被触碰。
        }
        catch (UnauthorizedAccessException)
        {
            // 同上：清理是尽力而为，失败不得掩盖真正的写入失败。
        }
    }

    private static string TempPathFor(string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath); // arch-guard:allow 契约九 §3 第 1 条：临时文件 MUST 与目标同目录/同卷
        var fileName = Path.GetFileName(targetPath); // arch-guard:allow 契约九 §4.1：取名需要目标文件名，便于失败现场辨认

        if (string.IsNullOrEmpty(directory))
        {
            directory = ".";
        }

        // T063：临时路径 MUST NOT 随档名一起变长——255 字符的档名再套一层全名会顶穿 MAX_PATH。
        // 故只保留档名前 TempNamePrefixLength 个字符（含扩展名，纯为辨认失败现场），
        // 临时路径的长度上界因此与档名长度无关。
        var prefix = fileName.Length > TempNamePrefixLength ? fileName[..TempNamePrefixLength] : fileName;

        // 随机后缀避免并发写同名临时文件；前缀的点号让它不撞已知档名。
        return Path.Combine(directory, $".{prefix}.{Guid.NewGuid():N}.tmp"); // arch-guard:allow 契约九 §3 第 1 条：临时文件路径（无 I/O）
    }
}
