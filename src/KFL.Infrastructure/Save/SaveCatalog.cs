namespace KFL.Infrastructure.Save;

/// <summary>
/// 存档目录的**只读目录服务**：建目录、列文件、读字节（004-save-system；契约九 §4.1，R-18）。
/// </summary>
/// <remarks>
/// <para>
/// 它**不认识**存档格式，也不决定任何口径：档名口径在 <see cref="SaveName"/>，字节形态在
/// <see cref="SaveFileFormat"/>，冲突判定在 <c>SaveService</c>。这里只有「目录里有什么」
/// 和「这个文件有多少字节」。
/// </para>
/// <para>
/// 与 <see cref="AtomicFileWriter"/> 一样，是存档层仅有的两处文件系统访问点之一；
/// 文件系统调用行带**同行** arch-guard 豁免理由（SC-008）。
/// </para>
/// <para>
/// 目录不存在时**列目录返回空**（不抛）——「还没存过档」是正常状态，不是错误；
/// 只有**建**目录失败才以 <see cref="SaveAccessException"/> 浮出（契约九 §4 第 5 条）。
/// </para>
/// <para>
/// 无状态，故为**静态类**：它不携带任何按实例变化的东西（对比
/// <see cref="AtomicFileWriter"/> 携带测试钩子而是实例类型）。
/// </para>
/// </remarks>
public static class SaveCatalog
{
    /// <summary>确保目录存在（含多级父目录）；失败 ⇒ <see cref="SaveAccessException"/>。</summary>
    /// <param name="directory">目录路径。</param>
    /// <exception cref="ArgumentException"><paramref name="directory"/> 为空白。</exception>
    /// <exception cref="SaveAccessException">目录建不出来（无权限、路径非法、卷不存在等）。</exception>
    public static void EnsureDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        try
        {
            Directory.CreateDirectory(directory); // arch-guard:allow 契约九 §4.1：确保存档根/备份目录存在
        }
        catch (IOException ex)
        {
            throw new SaveAccessException($"存档目录不可用：{directory}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new SaveAccessException($"存档目录不可写：{directory}", ex);
        }
        catch (ArgumentException ex)
        {
            throw new SaveAccessException($"存档目录路径非法：{directory}", ex);
        }
        catch (NotSupportedException ex)
        {
            throw new SaveAccessException($"存档目录路径形态不受支持：{directory}", ex);
        }
    }

    /// <summary>列出目录下的文件路径；目录不存在 ⇒ 空序列。</summary>
    /// <param name="directory">目录路径。</param>
    /// <returns>文件路径序列（无序）。</returns>
    public static IReadOnlyList<string> ListFiles(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return [];
        }

        if (!Directory.Exists(directory)) // arch-guard:allow 契约九 §4.1：目录不存在时列目录返回空而非抛
        {
            return [];
        }

        return [.. Directory.EnumerateFiles(directory)]; // arch-guard:allow 契约九 §4.1：枚举目录项供同名/同标识判定
    }

    /// <summary>读文件全部字节。</summary>
    /// <param name="path">文件路径。</param>
    /// <returns>文件字节。</returns>
    public static byte[] ReadAllBytes(string path) =>
        File.ReadAllBytes(path); // arch-guard:allow 契约九 §4.1：读档时一次性读入，单文件全量

    /// <summary>文件是否存在（存在性与类型无关；不区分大小写由文件系统决定）。</summary>
    /// <param name="path">文件路径。</param>
    /// <returns>存在 ⇒ <c>true</c>。</returns>
    public static bool FileExists(string path) =>
        File.Exists(path); // arch-guard:allow 契约九 §4.1：Load 第 ① 步「文件存在吗」

    /// <summary>拼一个位于给定目录下的路径。</summary>
    /// <remarks>
    /// 这不是文件系统**访问**（无 I/O），只是把已校验过的档名与目录拼起来；放在本类型里是为了
    /// 让「路径怎么拼」也只出现一次。
    /// </remarks>
    /// <param name="directory">目录。</param>
    /// <param name="fileName">文件名。</param>
    /// <returns>拼接后的路径。</returns>
    public static string Combine(string directory, string fileName) =>
        Path.Combine(directory, fileName); // arch-guard:allow 契约九 §4.1：路径拼接单点（无 I/O）

    /// <summary>未配置存档根时的默认根：<c>AppContext.BaseDirectory</c> 旁的 <c>saves/</c>。</summary>
    /// <returns>默认存档根。</returns>
    public static string DefaultRootDirectory() =>
        Path.Combine(AppContext.BaseDirectory, SaveFileFormat.DefaultDirectoryName); // arch-guard:allow 契约九 §4 第 3 条：默认存档根 = 可执行文件旁的 saves/（无 I/O）

    /// <summary>给定存档根下的备份目录路径。</summary>
    /// <param name="rootDirectory">存档根。</param>
    /// <returns>备份目录路径。</returns>
    public static string BackupsDirectoryOf(string rootDirectory) =>
        Path.Combine(rootDirectory, SaveFileFormat.BackupsDirectoryName); // arch-guard:allow 契约九 §4 第 4 条：备份目录 = 存档根下的 backups/（无 I/O）

    /// <summary>取不含扩展名的文件名（档名口径的载体）。</summary>
    /// <param name="path">文件路径。</param>
    /// <returns>不含扩展名的文件名。</returns>
    public static string FileNameStemOf(string path) =>
        Path.GetFileNameWithoutExtension(path); // arch-guard:allow 契约九 §4.1：从目录项还原「档名」（无 I/O）

    /// <summary>取扩展名（含点号）。</summary>
    /// <param name="path">文件路径。</param>
    /// <returns>扩展名。</returns>
    public static string ExtensionOf(string path) =>
        Path.GetExtension(path); // arch-guard:allow 契约九 §4.1：按扩展名判定格式（无 I/O）
}
