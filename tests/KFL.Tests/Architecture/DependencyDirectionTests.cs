using Xunit;

namespace KFL.Tests.Architecture;

/// <summary>T031：读六个 csproj 的 <c>ProjectReference</c>，断言 G-05 零违规。</summary>
/// <remarks>
/// 断言一律走 **XML 元素查询**：工程文件里的中文注释会提到属性名，按文本子串断言会把注释当成配置。
/// </remarks>
public class DependencyDirectionTests
{
    [Fact]
    public void 依赖边集在允许集内且无环()
    {
        Assert.Empty(RepositoryLocator.EvaluateRepository("G-05"));
    }

    [Fact]
    public void 核心层没有任何出边()
    {
        Assert.Empty(ArchitectureRules.ProjectReferenceNames(ProjectText("KFL.Core")));
    }

    [Fact]
    public void 依赖方向严格单向()
    {
        Assert.Contains(@"..\KFL.Core\KFL.Core.csproj", ProjectText("KFL.Infrastructure"), StringComparison.Ordinal);
        Assert.Contains(@"..\KFL.Core\KFL.Core.csproj", ProjectText("KFL.Rules"), StringComparison.Ordinal);
        Assert.Contains(@"..\KFL.Infrastructure\KFL.Infrastructure.csproj", ProjectText("KFL.Rules"), StringComparison.Ordinal);
        Assert.Contains(@"..\KFL.Rules\KFL.Rules.csproj", ProjectText("KFL.App"), StringComparison.Ordinal);
        Assert.Contains(@"..\KFL.Presentation\KFL.Presentation.csproj", ProjectText("KFL.App"), StringComparison.Ordinal);

        // 下层 MUST NOT 反向依赖上层：出边集合逐条比对，不看注释。
        Assert.Empty(ReferencesOf("KFL.Core"));
        Assert.Equal(["KFL.Core"], ReferencesOf("KFL.Infrastructure"));
        Assert.Equal(["KFL.Core", "KFL.Infrastructure"], ReferencesOf("KFL.Rules"));
        Assert.Equal(
            ["KFL.Core", "KFL.Infrastructure", "KFL.Rules", "KFL.Presentation"],
            ReferencesOf("KFL.App"));
    }

    [Fact]
    public void 测试工程豁免层序()
    {
        var text = ProjectText("KFL.Tests");

        Assert.Contains(@"..\..\src\KFL.Core\KFL.Core.csproj", text, StringComparison.Ordinal);
        Assert.Contains(@"..\..\src\KFL.Rules\KFL.Rules.csproj", text, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ReferencesOf(string projectName) =>
        ArchitectureRules.ProjectReferenceNames(ProjectText(projectName));

    private static string ProjectText(string projectName) =>
        RepositoryLocator.Load().ProjectFiles
            .Single(p => p.Key.EndsWith($"{projectName}.csproj", StringComparison.OrdinalIgnoreCase))
            .Value;
}
