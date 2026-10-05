using Xunit;

namespace KFL.Tests.Architecture;

/// <summary>T029：把真实仓库的 <c>.slnx</c> 喂给守卫，断言 G-01 / G-02 / G-08 零违规。</summary>
public class SolutionShapeTests
{
    [Fact]
    public void 仓库恰有一个slnx且无sln共存()
    {
        Assert.Empty(RepositoryLocator.EvaluateRepository("G-01"));
    }

    [Fact]
    public void 解决方案恰好登记六个约定工程()
    {
        // 等价于 `dotnet sln KejuFuShengLu.slnx list` 的断言：测试工程尤其不能缺席
        // ——<TestProject> 会被 .slnx 解析器静默忽略（research R-02 的真实陷阱）。
        // G-02 的路径集合断言已包含 `tests\KFL.Tests\KFL.Tests.csproj`，故此处不必再自行解析。
        Assert.Empty(RepositoryLocator.EvaluateRepository("G-02"));
    }

    [Fact]
    public void globalJson只锁十点零版本带()
    {
        Assert.Empty(RepositoryLocator.EvaluateRepository("G-08"));
    }

    [Fact]
    public void 全局json内容符合契约()
    {
        var text = RepositoryLocator.Load().GlobalJsonText;

        Assert.NotNull(text);
        Assert.Contains("10.0.100", text, StringComparison.Ordinal);
        Assert.Contains("latestFeature", text, StringComparison.Ordinal);
        Assert.DoesNotContain("disable", text, StringComparison.OrdinalIgnoreCase);
    }
}
