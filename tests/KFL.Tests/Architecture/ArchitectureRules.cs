using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace KFL.Tests.Architecture;

/// <summary>一条守卫违规。编号即契约一的断言编号（G-01~G-08）。</summary>
/// <param name="RuleId">违规编号，如 <c>G-05</c>。</param>
/// <param name="Subject">违规主体：工程名、依赖边或「文件:行」。</param>
/// <param name="Message">可读的违规说明。</param>
public sealed record ArchitectureViolation(string RuleId, string Subject, string Message)
{
    /// <inheritdoc />
    public override string ToString() => $"[{RuleId}] {Subject}: {Message}";
}

/// <summary>
/// 守卫判定逻辑的**全部输入**（契约一 §1：判定与 IO 分离，纯函数可注入合成输入）。
/// </summary>
public sealed record ArchitectureInput
{
    /// <summary>仓库内全部解决方案文件（含 <c>.slnx</c> 与 <c>.sln</c>）的仓库根相对路径。</summary>
    public required IReadOnlyList<string> SolutionFilePaths { get; init; }

    /// <summary>唯一 <c>.slnx</c> 的文本内容。</summary>
    public required string SolutionFileText { get; init; }

    /// <summary>各 <c>.csproj</c> 的文本，键为仓库根相对路径。</summary>
    public required IReadOnlyDictionary<string, string> ProjectFiles { get; init; }

    /// <summary>源码文本，键为仓库根相对路径。</summary>
    public required IReadOnlyDictionary<string, string> SourceFiles { get; init; }

    /// <summary>仓库根 <c>global.json</c> 的文本；不存在为 <c>null</c>。</summary>
    public required string? GlobalJsonText { get; init; }
}

/// <summary>
/// 架构守卫的**纯函数**判定逻辑（契约一 G-01~G-08）。
/// </summary>
/// <remarks>
/// **MUST NOT 触碰文件系统**——判定与 IO 分离是 SC-003「合成违规输入必被捕获」可自证的前提。
/// 文件读取由 <see cref="RepositoryLocator"/> 承担。
/// </remarks>
public static class ArchitectureRules
{
    /// <summary>唯一允许的解决方案文件名（G-01、G-02）。</summary>
    public const string SolutionFileName = "KejuFuShengLu.slnx";

    /// <summary>G-02 的六个约定工程路径：反斜杠、仓库根相对、含 <c>.csproj</c>。</summary>
    public static readonly IReadOnlyList<string> ExpectedProjectPaths =
    [
        @"src\KFL.Core\KFL.Core.csproj",
        @"src\KFL.Infrastructure\KFL.Infrastructure.csproj",
        @"src\KFL.Rules\KFL.Rules.csproj",
        @"src\KFL.Presentation\KFL.Presentation.csproj",
        @"src\KFL.App\KFL.App.csproj",
        @"tests\KFL.Tests\KFL.Tests.csproj",
    ];

    /// <summary>UI 层工程（G-03 期望 <c>net10.0-windows</c>）。</summary>
    public static readonly IReadOnlyList<string> UiProjects = ["KFL.Presentation", "KFL.App"];

    /// <summary>非 UI 工程（G-03 期望 <c>net10.0</c>；G-04 禁止 WPF/WinForms 真值）。</summary>
    public static readonly IReadOnlyList<string> NonUiProjects =
        ["KFL.Core", "KFL.Infrastructure", "KFL.Rules", "KFL.Tests"];

    /// <summary>
    /// G-07 的禁用 token 清单——**逐字取自契约一 §2.1（唯一真源）**，本数组是其可执行形式。
    /// 共 14 个；MUST NOT 增删改，其他文档一律不复制副本。
    /// </summary>
    public static readonly IReadOnlyList<string> ForbiddenEnvironmentTokens =
    [
        "DateTime.Now",
        "DateTime.UtcNow",
        "DateTime.Today",
        "DateTimeOffset.Now",
        "Environment.TickCount",
        "new Random(",
        "Random.Shared",
        "Guid.NewGuid()",
        "File.",
        "Directory.",
        "Path.",
        "HttpClient",
        "Socket",
        "Dns.",
    ];

    /// <summary>G-06 的界面平台命名空间 token。</summary>
    public static readonly IReadOnlyList<string> PlatformNamespaceTokens =
    [
        "System.Windows",
        "System.Drawing",
        "PresentationCore",
        "PresentationFramework",
        "WindowsBase",
    ];

    /// <summary>G-05 的层序允许边集，逐字取自契约一 §3。</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> AllowedReferences =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["KFL.Core"] = [],
            ["KFL.Infrastructure"] = ["KFL.Core"],
            ["KFL.Rules"] = ["KFL.Core", "KFL.Infrastructure"],
            ["KFL.Presentation"] = ["KFL.Core", "KFL.Infrastructure", "KFL.Rules"],
            ["KFL.App"] = ["KFL.Core", "KFL.Infrastructure", "KFL.Rules", "KFL.Presentation"],
            ["KFL.Tests"] = ["KFL.Core", "KFL.Infrastructure", "KFL.Rules", "KFL.Presentation", "KFL.App"],
        };

    /// <summary>G-08 允许的 <c>rollForward</c> 取值。</summary>
    public static readonly IReadOnlyList<string> AllowedRollForward =
        ["latestFeature", "latestMinor", "latestMajor"];

    /// <summary>非 UI 产品工程源码根（G-06 扫描范围，也是 G-07 的一部分）。</summary>
    private static readonly string[] ProductSourceRoots =
        [@"src\KFL.Core\", @"src\KFL.Infrastructure\", @"src\KFL.Rules\"];

    /// <summary>
    /// G-07 扫描范围额外包含的测试目录（R-13 的收敛在此物化）。
    /// <c>Rules\</c> 由 002 research R-14 追加——规则层测试同样 MUST 无环境依赖。
    /// </summary>
    private static readonly string[] ScannedTestRoots =
    [
        @"tests\KFL.Tests\Core\",
        @"tests\KFL.Tests\Infrastructure\",
        @"tests\KFL.Tests\Fixtures\",
        @"tests\KFL.Tests\Rules\",
    ];

    /// <summary>G-07 显式豁免的目录：其职责就是读仓库文件与源码。</summary>
    private const string ExemptTestDirectory = @"tests\KFL.Tests\Architecture\";

    /// <summary>行级豁免注释；命中该注释的行不参与 G-07 扫描。</summary>
    public const string ExemptionComment = "// arch-guard:allow";

    private static readonly string[] IgnoredPathSegments = [".git", "bin", "obj"];

    /// <summary>取 csproj / props 中指定属性元素的取值；**注释不计入**（XML 注释不是元素）。</summary>
    /// <param name="projectText">csproj 或 props 的文本。</param>
    /// <param name="localName">属性元素名，如 <c>TargetFramework</c>。</param>
    /// <returns>各出现处的取值。</returns>
    public static IReadOnlyList<string> ValuesOf(string projectText, string localName) =>
        XDocument.Parse(projectText).Descendants()
            .Where(e => e.Name.LocalName == localName)
            .Select(e => e.Value.Trim())
            .ToList();

    /// <summary>csproj / props 中是否存在指定属性元素；**注释不计入**。</summary>
    /// <param name="projectText">csproj 或 props 的文本。</param>
    /// <param name="localName">属性元素名。</param>
    /// <returns>存在为 <c>true</c>。</returns>
    public static bool ContainsElement(string projectText, string localName) =>
        XDocument.Parse(projectText).Descendants().Any(e => e.Name.LocalName == localName);

    /// <summary>取 csproj 中 <c>ProjectReference</c> 指向的工程名（由 <c>Include</c> 文件名派生）。</summary>
    /// <param name="projectText">csproj 文本。</param>
    /// <returns>被引用工程名，按声明顺序。</returns>
    public static IReadOnlyList<string> ProjectReferenceNames(string projectText) =>
        XDocument.Parse(projectText).Descendants()
            .Where(e => e.Name.LocalName == "ProjectReference")
            .Select(e => Path.GetFileNameWithoutExtension(
                (e.Attribute("Include")?.Value ?? string.Empty).Replace('/', '\\')))
            .Where(name => name.Length > 0)
            .ToList();

    /// <summary>对全部输入求违规集合；零违规即通过。</summary>    /// <param name="input">仓库的结构化快照。</param>
    /// <returns>违规列表，按编号排序。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> 为 <c>null</c>。</exception>
    public static IReadOnlyList<ArchitectureViolation> Evaluate(ArchitectureInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var violations = new List<ArchitectureViolation>();
        var solutionPaths = input.SolutionFilePaths
            .Select(Normalize)
            .Where(p => p.Length > 0 && IsRelevantPath(p))
            .ToList();

        CheckSolutionFileSet(solutionPaths, violations);
        var declared = CheckSolutionProjectList(input.SolutionFileText, violations);
        var references = CheckProjects(input, violations);
        CheckDependencyDirection(references, violations);
        CheckPlatformNamespaceLeakage(input.SourceFiles, violations);
        CheckForbiddenEnvironmentTokens(input.SourceFiles, violations);
        CheckGlobalJson(input.GlobalJsonText, violations);

        _ = declared;
        return violations;
    }

    // ------------------------------------------------------------------ G-01

    private static void CheckSolutionFileSet(List<string> solutionPaths, List<ArchitectureViolation> violations)
    {
        var slnx = solutionPaths.Where(p => p.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)).ToList();
        var sln = solutionPaths.Where(p => p.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)).ToList();

        if (slnx.Count != 1 || !string.Equals(slnx[0], SolutionFileName, StringComparison.OrdinalIgnoreCase))
        {
            violations.Add(new ArchitectureViolation(
                "G-01",
                "仓库",
                $"仓库中 MUST 恰有一个 {SolutionFileName}，实际发现：{(slnx.Count == 0 ? "无" : string.Join("、", slnx))}。"));
        }

        if (sln.Count > 0)
        {
            violations.Add(new ArchitectureViolation(
                "G-01",
                "仓库",
                $".sln 与 .slnx MUST NOT 共存，实际发现：{string.Join("、", sln)}。"));
        }
    }

    // ------------------------------------------------------------------ G-02

    private static List<string> CheckSolutionProjectList(string solutionText, List<ArchitectureViolation> violations)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(solutionText);
        }
        catch (XmlException exception)
        {
            violations.Add(new ArchitectureViolation("G-02", SolutionFileName, $"无法解析：{exception.Message}"));
            return [];
        }

        // MUST 基于可解析的工程集合，MUST NOT 按文本中 Path= 出现次数计数。
        var declared = document.Descendants()
            .Where(e => e.Name.LocalName == "Project")
            .Select(e => Normalize(e.Attribute("Path")?.Value ?? string.Empty))
            .Where(p => p.Length > 0)
            .ToList();

        if (declared.Count != ExpectedProjectPaths.Count)
        {
            violations.Add(new ArchitectureViolation(
                "G-02",
                SolutionFileName,
                $"{SolutionFileName} MUST 恰好声明 {ExpectedProjectPaths.Count} 个 <Project>，实际 {declared.Count} 个。"));
        }

        foreach (var missing in ExpectedProjectPaths.Except(declared, StringComparer.OrdinalIgnoreCase))
        {
            violations.Add(new ArchitectureViolation(
                "G-02", SolutionFileName, $"缺少工程声明：{missing}。"));
        }

        foreach (var unexpected in declared.Except(ExpectedProjectPaths, StringComparer.OrdinalIgnoreCase))
        {
            violations.Add(new ArchitectureViolation(
                "G-02", SolutionFileName, $"多出未约定的工程声明：{unexpected}。"));
        }

        return declared;
    }

    // ------------------------------------------------------------------ G-03 / G-04 / G-05（边集收集）

    private static Dictionary<string, List<string>> CheckProjects(
        ArchitectureInput input, List<ArchitectureViolation> violations)
    {
        var projectFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in input.ProjectFiles)
        {
            projectFiles[Normalize(pair.Key)] = pair.Value;
        }

        var references = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in ExpectedProjectPaths)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            references[name] = [];

            if (!projectFiles.TryGetValue(path, out var text))
            {
                violations.Add(new ArchitectureViolation("G-03", name, $"找不到工程文件 {path}。"));
                continue;
            }

            XDocument document;
            try
            {
                document = XDocument.Parse(text);
            }
            catch (XmlException exception)
            {
                violations.Add(new ArchitectureViolation("G-03", name, $"无法解析 {path}：{exception.Message}"));
                continue;
            }

            CheckTargetFramework(name, document, violations);
            CheckNoPlatformOptIn(name, document, violations);
            CollectReferences(name, document, references);
        }

        return references;
    }

    private static void CheckTargetFramework(
        string projectName, XDocument document, List<ArchitectureViolation> violations)
    {
        var expected = UiProjects.Contains(projectName, StringComparer.OrdinalIgnoreCase)
            ? "net10.0-windows"
            : "net10.0";

        var declared = document.Descendants()
            .Where(e => e.Name.LocalName == "TargetFramework")
            .Select(e => e.Value.Trim())
            .ToList();

        if (declared.Count == 0)
        {
            violations.Add(new ArchitectureViolation(
                "G-03", projectName, $"未显式声明 TargetFramework（期望 {expected}）。"));
        }
        else if (declared.Any(v => !string.Equals(v, expected, StringComparison.OrdinalIgnoreCase)))
        {
            violations.Add(new ArchitectureViolation(
                "G-03", projectName, $"TargetFramework 期望 {expected}，实际 {string.Join("、", declared)}。"));
        }

        if (document.Descendants().Any(e => e.Name.LocalName == "TargetFrameworks"))
        {
            violations.Add(new ArchitectureViolation(
                "G-03", projectName, "MUST NOT 出现 TargetFrameworks（禁止多目标）。"));
        }
    }

    private static void CheckNoPlatformOptIn(
        string projectName, XDocument document, List<ArchitectureViolation> violations)
    {
        if (!NonUiProjects.Contains(projectName, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var elementName in new[] { "UseWPF", "UseWindowsForms" })
        {
            var optIn = document.Descendants()
                .Where(e => e.Name.LocalName == elementName)
                .Any(e => bool.TryParse(e.Value.Trim(), out var value) && value);

            if (optIn)
            {
                violations.Add(new ArchitectureViolation(
                    "G-04", projectName, $"非 UI 工程 MUST NOT 启用 {elementName}。"));
            }
        }
    }

    private static void CollectReferences(
        string projectName, XDocument document, Dictionary<string, List<string>> references)
    {
        foreach (var element in document.Descendants().Where(e => e.Name.LocalName == "ProjectReference"))
        {
            var include = Normalize(element.Attribute("Include")?.Value ?? string.Empty);
            if (include.Length == 0)
            {
                continue;
            }

            references[projectName].Add(Path.GetFileNameWithoutExtension(include));
        }
    }

    // ------------------------------------------------------------------ G-05

    private static void CheckDependencyDirection(
        Dictionary<string, List<string>> references, List<ArchitectureViolation> violations)
    {
        foreach (var (from, targets) in references)
        {
            var allowed = AllowedReferences.TryGetValue(from, out var list) ? list : [];

            foreach (var to in targets)
            {
                if (!AllowedReferences.ContainsKey(to))
                {
                    violations.Add(new ArchitectureViolation(
                        "G-05", $"{from} -> {to}", $"ProjectReference 指向未约定的工程 {to}。"));
                    continue;
                }

                if (!allowed.Contains(to, StringComparer.OrdinalIgnoreCase))
                {
                    violations.Add(new ArchitectureViolation(
                        "G-05",
                        $"{from} -> {to}",
                        $"依赖边不在允许边集内：{from} MUST NOT 引用 {to}（契约一 §3）。"));
                }
            }
        }

        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in references.Keys)
        {
            if (DetectCycle(node, references, visiting, visited, out var cycle))
            {
                violations.Add(new ArchitectureViolation(
                    "G-05", "依赖图", $"存在环：{cycle}。"));
                return;
            }
        }
    }

    private static bool DetectCycle(
        string node,
        Dictionary<string, List<string>> references,
        HashSet<string> visiting,
        HashSet<string> visited,
        out string cycle)
    {
        cycle = string.Empty;

        if (visited.Contains(node))
        {
            return false;
        }

        if (!visiting.Add(node))
        {
            cycle = string.Join(" -> ", visiting);
            return true;
        }

        if (references.TryGetValue(node, out var targets))
        {
            foreach (var target in targets)
            {
                if (DetectCycle(target, references, visiting, visited, out cycle))
                {
                    return true;
                }
            }
        }

        visiting.Remove(node);
        visited.Add(node);
        return false;
    }

    // ------------------------------------------------------------------ G-06

    private static void CheckPlatformNamespaceLeakage(
        IReadOnlyDictionary<string, string> sourceFiles, List<ArchitectureViolation> violations)
    {
        foreach (var (path, text) in EnumerateSources(sourceFiles, ProductSourceRoots))
        {
            foreach (var (line, number) in Lines(text))
            {
                var token = PlatformNamespaceTokens.FirstOrDefault(
                    t => line.Contains(t, StringComparison.Ordinal));

                if (token is not null)
                {
                    violations.Add(new ArchitectureViolation(
                        "G-06", $"{path}:{number}", $"非 UI 层源码 MUST NOT 出现界面平台命名空间 {token}。"));
                }
            }
        }
    }

    // ------------------------------------------------------------------ G-07

    private static void CheckForbiddenEnvironmentTokens(
        IReadOnlyDictionary<string, string> sourceFiles, List<ArchitectureViolation> violations)
    {
        var roots = ProductSourceRoots.Concat(ScannedTestRoots).ToArray();

        foreach (var (path, text) in EnumerateSources(sourceFiles, roots))
        {
            if (path.Contains(ExemptTestDirectory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var (line, number) in Lines(text))
            {
                if (line.Contains(ExemptionComment, StringComparison.Ordinal))
                {
                    continue;
                }

                var token = ForbiddenEnvironmentTokens.FirstOrDefault(
                    t => line.Contains(t, StringComparison.Ordinal));

                if (token is not null)
                {
                    violations.Add(new ArchitectureViolation(
                        "G-07",
                        $"{path}:{number}",
                        $"禁用 token「{token}」：非 UI 层与领域/接缝/夹具测试 MUST NOT 直接访问系统时钟、全局随机、文件系统或网络。"));
                }
            }
        }
    }

    // ------------------------------------------------------------------ G-08

    private static void CheckGlobalJson(string? globalJsonText, List<ArchitectureViolation> violations)
    {
        if (string.IsNullOrWhiteSpace(globalJsonText))
        {
            violations.Add(new ArchitectureViolation(
                "G-08", "global.json", "仓库根 MUST 存在 global.json 以固定 .NET 10 基线。"));
            return;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(globalJsonText);
        }
        catch (JsonException exception)
        {
            violations.Add(new ArchitectureViolation("G-08", "global.json", $"无法解析：{exception.Message}"));
            return;
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("sdk", out var sdk))
            {
                violations.Add(new ArchitectureViolation("G-08", "global.json", "缺少 sdk 节。"));
                return;
            }

            var version = sdk.TryGetProperty("version", out var versionElement)
                ? versionElement.GetString()
                : null;

            if (version is null || !VersionBand(version).Equals("10.0", StringComparison.Ordinal))
            {
                violations.Add(new ArchitectureViolation(
                    "G-08",
                    "global.json",
                    $"sdk.version 的主次版本 MUST 为 10.0，实际为 {version ?? "(缺失)"}。"));
            }

            var rollForward = sdk.TryGetProperty("rollForward", out var rollForwardElement)
                ? rollForwardElement.GetString()
                : null;

            if (rollForward is null || !AllowedRollForward.Contains(rollForward, StringComparer.Ordinal))
            {
                violations.Add(new ArchitectureViolation(
                    "G-08",
                    "global.json",
                    $"rollForward MUST 为 {string.Join("/", AllowedRollForward)} 之一（禁止 disable），实际为 {rollForward ?? "(缺失)"}。"));
            }
        }
    }

    // ------------------------------------------------------------------ 工具

    /// <summary>把「10.0.100」这样的版本串归约为「10.0」。</summary>
    private static string VersionBand(string version)
    {
        var parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : version;
    }

    private static string Normalize(string path) =>
        path.Replace('/', '\\').Trim().TrimStart('\\').TrimStart('.').TrimStart('\\');

    private static bool IsRelevantPath(string path) =>
        !path.Split('\\').Any(segment => IgnoredPathSegments.Contains(segment, StringComparer.OrdinalIgnoreCase));

    private static IEnumerable<(string Path, string Text)> EnumerateSources(
        IReadOnlyDictionary<string, string> sourceFiles, IEnumerable<string> roots)
    {
        var normalizedRoots = roots.Select(Normalize).ToArray();

        foreach (var pair in sourceFiles)
        {
            var path = Normalize(pair.Key);
            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || !IsRelevantPath(path))
            {
                continue;
            }

            if (normalizedRoots.Any(root => path.StartsWith(root, StringComparison.OrdinalIgnoreCase)))
            {
                yield return (path, pair.Value);
            }
        }
    }

    private static IEnumerable<(string Line, int Number)> Lines(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            yield return (lines[index], index + 1);
        }
    }
}
