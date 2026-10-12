namespace KFL.Tests.Architecture;

/// <summary>
/// 文件读取外壳：定位仓库根，读 <c>.slnx</c> / <c>.csproj</c> / <c>*.cs</c>，
/// 交给 <see cref="ArchitectureRules"/> 判定（契约一 §1）。
/// </summary>
/// <remarks>
/// 本文件与整个 <c>Architecture/</c> 目录在 G-07 的扫描范围之外——它的职责就是读仓库文件。
/// </remarks>
public static class RepositoryLocator
{
    private static readonly string[] IgnoredPathSegments = [".git", "bin", "obj"];

    /// <summary>
    /// 定位仓库根：从 <see cref="AppContext.BaseDirectory"/> 向上逐级查找 <c>*.slnx</c>，
    /// **找不到即失败**（不静默返回 <c>null</c>）。
    /// </summary>
    /// <returns>仓库根的绝对路径。</returns>
    /// <exception cref="InvalidOperationException">向上找不到 <c>*.slnx</c>。</exception>
    public static string LocateRepositoryRoot()
    {
        var found = WalkUp(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException(
                $"未能定位仓库根：从 {AppContext.BaseDirectory} 向上找不到 *.slnx。");

        return found;
    }

    /// <summary>读取仓库快照，供 <see cref="ArchitectureRules.Evaluate"/> 判定。</summary>
    /// <returns>仓库的结构化快照。</returns>
    public static ArchitectureInput Load()
    {
        var root = LocateRepositoryRoot();

        var solutionFilePaths = EnumerateFiles(root, "*.slnx")
            .Concat(EnumerateFiles(root, "*.sln"))
            .Select(f => Relative(root, f))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        var solutionPath = solutionFilePaths
            .FirstOrDefault(p => p.Equals(ArchitectureRules.SolutionFileName, StringComparison.OrdinalIgnoreCase));

        var projectFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in EnumerateFiles(root, "*.csproj"))
        {
            projectFiles[Relative(root, file)] = File.ReadAllText(file);
        }

        var sourceFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in EnumerateFiles(root, "*.cs"))
        {
            sourceFiles[Relative(root, file)] = File.ReadAllText(file);
        }

        var globalJsonPath = Path.Combine(root, "global.json");

        return new ArchitectureInput
        {
            SolutionFilePaths = solutionFilePaths,
            SolutionFileText = solutionPath is null
                ? string.Empty
                : File.ReadAllText(Path.Combine(root, solutionPath)),
            ProjectFiles = projectFiles,
            SourceFiles = sourceFiles,
            GlobalJsonText = File.Exists(globalJsonPath) ? File.ReadAllText(globalJsonPath) : null,
        };
    }

    /// <summary>
    /// 读真实仓库并求违规集合；给定 <paramref name="ruleIds"/> 时只保留这些编号的结果。
    /// 这是守卫测试的统一入口（IO 外壳 + 纯函数判定）。
    /// </summary>
    /// <param name="ruleIds">关注的断言编号；为空表示全部。</param>
    /// <returns>违规列表。</returns>
    public static IReadOnlyList<ArchitectureViolation> EvaluateRepository(params string[] ruleIds)
    {
        var violations = ArchitectureRules.Evaluate(Load());
        return ruleIds.Length == 0
            ? violations
            : violations.Where(v => ruleIds.Contains(v.RuleId, StringComparer.Ordinal)).ToList();
    }

    private static string? WalkUp(string start)
    {
        var directory = new DirectoryInfo(start);

        while (directory is not null)
        {
            if (directory.EnumerateFiles("*.slnx").Any())
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>
    /// 递归枚举匹配文件，**在进入目录之前**就剪掉 <c>.git</c>/<c>bin</c>/<c>obj</c>。
    /// </summary>
    /// <remarks>
    /// MUST NOT 先枚举后过滤：<c>Directory.EnumerateFiles(…, AllDirectories)</c> 会先递归进
    /// <c>tests/KFL.Tests/bin/…/save-tests/&lt;guid&gt;</c> 之类的目录，而测试并行跑时那类目录可能
    /// 正在被创建或删除，于是扫描抛 <c>UnauthorizedAccessException</c>——门禁就会出现「与代码无关的
    /// 非确定性失败」。
    /// </remarks>
    /// <param name="root">仓库根。</param>
    /// <param name="pattern">文件模式。</param>
    /// <returns>匹配的文件路径。</returns>
    private static IEnumerable<string> EnumerateFiles(string root, string pattern)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();

            foreach (var subdirectory in Directory.EnumerateDirectories(directory))
            {
                if (!IsIgnored(Path.GetFileName(subdirectory)))
                {
                    pending.Push(subdirectory);
                }
            }

            foreach (var file in Directory.EnumerateFiles(directory, pattern))
            {
                yield return file;
            }
        }
    }

    /// <summary>路径段是否在忽略表内（大小写不敏感，与各平台一致）。</summary>
    /// <param name="segment">单个路径段（目录名或文件名）。</param>
    /// <returns>被忽略即真。</returns>
    private static bool IsIgnored(string segment) =>
        IgnoredPathSegments.Contains(segment, StringComparer.OrdinalIgnoreCase);

    private static string Relative(string root, string file) => Path.GetRelativePath(root, file);
}
