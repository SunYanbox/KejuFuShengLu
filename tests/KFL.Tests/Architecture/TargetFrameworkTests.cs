using Xunit;

namespace KFL.Tests.Architecture;

/// <summary>T030：读六个 csproj 文本，断言 G-03 / G-04 零违规。</summary>
/// <remarks>
/// 断言一律走 **XML 元素查询**（<see cref="ArchitectureRules.ValuesOf"/>）：工程文件里的
/// 中文注释会提到属性名，按文本子串断言会把注释当成配置。
/// </remarks>
public class TargetFrameworkTests
{
    [Fact]
    public void 目标框架按层划分且无多目标()
    {
        Assert.Empty(RepositoryLocator.EvaluateRepository("G-03"));
    }

    [Fact]
    public void 非UI工程未启用WPF或WinForms()
    {
        Assert.Empty(RepositoryLocator.EvaluateRepository("G-04"));
    }

    [Fact]
    public void 非UI三层确实声明net十点零()
    {
        foreach (var name in new[] { "KFL.Core", "KFL.Infrastructure", "KFL.Rules" })
        {
            var text = ProjectText(name);

            Assert.Equal(["net10.0"], ArchitectureRules.ValuesOf(text, "TargetFramework"));
            Assert.False(ArchitectureRules.ContainsElement(text, "TargetFrameworks"));
            Assert.False(ArchitectureRules.ContainsElement(text, "UseWPF"));
        }
    }

    [Fact]
    public void UI两层确实声明net十点零windows()
    {
        foreach (var name in new[] { "KFL.Presentation", "KFL.App" })
        {
            var text = ProjectText(name);

            Assert.Equal(["net10.0-windows"], ArchitectureRules.ValuesOf(text, "TargetFramework"));
            Assert.Equal(["true"], ArchitectureRules.ValuesOf(text, "UseWPF"));
        }
    }

    [Fact]
    public void 目标框架不得写进目录级属性表()
    {
        // 给了默认值会让写错的 TFM 被静默补上，G-03 即失效（research R-01）。
        var root = RepositoryLocator.LocateRepositoryRoot();
        var directoryBuildProps = File.ReadAllText(Path.Combine(root, "Directory.Build.props"));

        Assert.False(ArchitectureRules.ContainsElement(directoryBuildProps, "TargetFramework"));
        Assert.False(ArchitectureRules.ContainsElement(directoryBuildProps, "TargetFrameworks"));
    }

    private static string ProjectText(string projectName) =>
        RepositoryLocator.Load().ProjectFiles
            .Single(p => p.Key.EndsWith($"{projectName}.csproj", StringComparison.OrdinalIgnoreCase))
            .Value;
}
