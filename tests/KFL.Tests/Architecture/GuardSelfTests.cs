using Xunit;

namespace KFL.Tests.Architecture;

/// <summary>
/// T034：守卫自证，覆盖契约一 §4 全部六用例。
/// </summary>
/// <remarks>
/// <para>
/// **MUST NOT 通过真实改写仓库文件来验证**：本方案的允许边集是全序，任何反向边都闭合成环；
/// <c>using System.Windows.Media;</c> 在 <c>net10.0</c> 工程里也必然编译失败。二者都会让
/// 「守卫失败」与「编译失败」不可区分，手工路径恒不可用（见
/// <c>specs/001-core-skeleton/implementation-notes.md</c> 第 1 节的 T035 行）。
/// </para>
/// <para>
/// 合成输入以真实仓库快照为基线、**在内存中**注入违规——这是「合法输入」与「违规输入」
/// 共用同一基线的唯一方式，也保证用例⑥（真实仓库零违规）与其余用例互为对照。
/// </para>
/// </remarks>
public class GuardSelfTests
{
    private const string CoreProjectPath = @"src\KFL.Core\KFL.Core.csproj";
    private const string RulesProjectPath = @"src\KFL.Rules\KFL.Rules.csproj";
    private const string TestsProjectPath = @"tests\KFL.Tests\KFL.Tests.csproj";

    /// <summary>用例①：反向依赖。</summary>
    [Fact]
    public void 用例一反向依赖报G05且信息含两个工程名()
    {
        var input = WithProjectText(
            CoreProjectPath,
            """
              <ItemGroup>
                <ProjectReference Include="..\KFL.Rules\KFL.Rules.csproj" />
              </ItemGroup>
            """);

        var violations = ArchitectureRules.Evaluate(input);
        var g05 = violations.Where(v => v.RuleId == "G-05").ToList();

        Assert.NotEmpty(g05);
        var text = Describe(g05);
        Assert.Contains("KFL.Core", text, StringComparison.Ordinal);
        Assert.Contains("KFL.Rules", text, StringComparison.Ordinal);
    }

    /// <summary>用例②：工程级平台泄漏。</summary>
    [Fact]
    public void 用例二工程级平台泄漏报G04()
    {
        var input = WithProjectText(
            RulesProjectPath,
            """
              <PropertyGroup>
                <UseWPF>true</UseWPF>
              </PropertyGroup>
            """);

        Assert.Contains(
            ArchitectureRules.Evaluate(input),
            v => v.RuleId == "G-04" && v.Subject == "KFL.Rules");
    }

    /// <summary>用例③：源码级平台泄漏 → **G-06**（该 token 不在契约一 §2.1 的清单里，故不得报成 G-07）。</summary>
    [Fact]
    public void 用例三源码级平台泄漏报G06而非G07()
    {
        var input = WithSource(@"src\KFL.Rules\PlatformLeak.cs", "using System.Windows.Media;");

        var violations = ArchitectureRules.Evaluate(input);

        Assert.Contains(violations, v => v.RuleId == "G-06");
        Assert.DoesNotContain(violations, v => v.RuleId == "G-07");
    }

    /// <summary>用例④：环境依赖。</summary>
    [Fact]
    public void 用例四环境依赖报G07()
    {
        var input = WithSource(@"src\KFL.Rules\Clock.cs", "var now = DateTime.Now;");

        var violations = ArchitectureRules.Evaluate(input);

        Assert.Contains(violations, v => v.RuleId == "G-07");
        Assert.DoesNotContain(violations, v => v.RuleId == "G-06");
    }

    /// <summary>用例⑤：测试工程漏登记。</summary>
    [Fact]
    public void 用例五测试工程漏登记报G02并报出缺失路径()
    {
        var baseline = RepositoryLocator.Load();
        var fiveOnly = string.Join(
            "\n",
            baseline.SolutionFileText.Split('\n').Where(line => !line.Contains("KFL.Tests.csproj", StringComparison.Ordinal)));

        var violations = ArchitectureRules.Evaluate(baseline with { SolutionFileText = fiveOnly });
        var g02 = violations.Where(v => v.RuleId == "G-02").ToList();

        Assert.NotEmpty(g02);
        Assert.Contains(TestsProjectPath, Describe(g02), StringComparison.Ordinal);
    }

    /// <summary>用例⑥：合法输入（真实仓库内容）→ 零违规。</summary>
    [Fact]
    public void 用例六真实仓库零违规()
    {
        var violations = ArchitectureRules.Evaluate(RepositoryLocator.Load());

        Assert.Empty(violations);
    }

    /// <summary>附加：行级豁免注释 <c>// arch-guard:allow</c> 生效（契约一 §2.1）。</summary>
    [Fact]
    public void 行级豁免注释可使该行不被G07命中()
    {
        var exempted = WithSource(
            @"src\KFL.Rules\Exempted.cs",
            $"var now = DateTime.Now; {ArchitectureRules.ExemptionComment} 守卫自身测试需要");

        Assert.DoesNotContain(ArchitectureRules.Evaluate(exempted), v => v.RuleId == "G-07");

        var notExempted = WithSource(@"src\KFL.Rules\NotExempted.cs", "var now = DateTime.Now;");

        Assert.Contains(ArchitectureRules.Evaluate(notExempted), v => v.RuleId == "G-07");
    }

    /// <summary>
    /// 附加（T052）：G-07 的**测试目录扫描范围非空洞**——同一注入落在被扫描的四个测试目录下
    /// 都必须报出。否则 R-13 把 <c>Core</c> / <c>Infrastructure</c> / <c>Fixtures</c> /
    /// <c>Rules</c> 纳入扫描这件事没有证据（其余合成输入只落在 <c>src\KFL.Rules\</c> 下）。
    /// </summary>
    /// <param name="path">注入位置，取自扫描范围内的四个测试目录。</param>
    [Theory]
    [InlineData(@"tests\KFL.Tests\Core\EnvironmentLeak.cs")]
    [InlineData(@"tests\KFL.Tests\Infrastructure\EnvironmentLeak.cs")]
    [InlineData(@"tests\KFL.Tests\Fixtures\EnvironmentLeak.cs")]
    [InlineData(@"tests\KFL.Tests\Rules\EnvironmentLeak.cs")]
    public void 测试目录内的禁用token报G07(string path)
    {
        var violations = ArchitectureRules.Evaluate(WithSource(path, "var now = DateTime.Now;"));

        Assert.Contains(
            violations,
            v => v.RuleId == "G-07" && v.Subject.StartsWith(path, StringComparison.Ordinal));
    }

    /// <summary>
    /// 附加（T052）：与上一条互为对照——<c>Architecture/</c> 是**显式豁免**而非漏扫，
    /// 同一注入落在此处 MUST NOT 报 G-07。两条一起才说明该目录边界是被判定的，而非恒不命中。
    /// </summary>
    [Fact]
    public void 架构目录内的同一注入不报G07()
    {
        var violations = ArchitectureRules.Evaluate(
            WithSource(@"tests\KFL.Tests\Architecture\EnvironmentLeak.cs", "var now = DateTime.Now;"));

        Assert.DoesNotContain(violations, v => v.RuleId == "G-07");
    }

    private static string Describe(IEnumerable<ArchitectureViolation> violations) =>
        string.Join("；", violations.Select(v => v.ToString()));

    private static ArchitectureInput WithProjectText(string projectPath, string injectedFragment)
    {
        var baseline = RepositoryLocator.Load();
        var projects = baseline.ProjectFiles.ToDictionary(
            pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        projects[projectPath] = projects[projectPath].Replace(
            "</Project>", $"{injectedFragment}\n</Project>", StringComparison.Ordinal);

        return baseline with { ProjectFiles = projects };
    }

    private static ArchitectureInput WithSource(string path, string text)
    {
        var baseline = RepositoryLocator.Load();
        var sources = baseline.SourceFiles.ToDictionary(
            pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        sources[path] = text;

        return baseline with { SourceFiles = sources };
    }
}
