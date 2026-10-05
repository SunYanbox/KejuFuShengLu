using Xunit;

namespace KFL.Tests.Architecture;

/// <summary>
/// T033：源码级证据，断言 G-07 零违规。
/// </summary>
/// <remarks>
/// 扫描范围 = 非 UI 产品工程（<c>KFL.Core</c>/<c>KFL.Infrastructure</c>/<c>KFL.Rules</c>）**加上**
/// <c>tests/KFL.Tests/Core</c>、<c>Infrastructure</c>、<c>Fixtures</c>；<c>tests/KFL.Tests/Architecture/</c>
/// **显式豁免**（其职责就是读仓库文件，R-13 的收敛在此物化）。
/// 本测试同时是 US3 AS2 的**唯一验证物**（跨故事依赖 US3 → T033）。
/// </remarks>
public class EnvironmentDependencyTests
{
    [Fact]
    public void 非UI层与领域测试无环境依赖()
    {
        Assert.Empty(RepositoryLocator.EvaluateRepository("G-07"));
    }

    [Fact]
    public void 禁用token清单逐字取自契约一第2点1节且共十四项()
    {
        // 契约一 §2.1 是清单的唯一真源；本断言防止清单被悄悄增删。
        Assert.Equal(14, ArchitectureRules.ForbiddenEnvironmentTokens.Count);
        Assert.Equal(
            [
                "DateTime.Now", "DateTime.UtcNow", "DateTime.Today", "DateTimeOffset.Now",
                "Environment.TickCount", "new Random(", "Random.Shared", "Guid.NewGuid()",
                "File.", "Directory.", "Path.", "HttpClient", "Socket", "Dns.",
            ],
            ArchitectureRules.ForbiddenEnvironmentTokens);
    }

    [Fact]
    public void 扫描范围排除架构目录()
    {
        var snapshot = RepositoryLocator.Load();

        // 架构目录的文件确实被读到（否则这条断言恒真），但不在 G-07 的判定范围内。
        Assert.Contains(
            snapshot.SourceFiles.Keys,
            path => path.Contains(@"tests\KFL.Tests\Architecture\", StringComparison.OrdinalIgnoreCase));

        Assert.Empty(RepositoryLocator.EvaluateRepository("G-07"));
    }
}
