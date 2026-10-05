using System.Reflection;
using Xunit;

namespace KFL.Tests.Architecture;

/// <summary>
/// T032：平台泄漏的**两层证据**。
/// </summary>
/// <remarks>
/// ① **G-06b（程序集级）**：<see cref="Assembly.GetReferencedAssemblies"/> 抓真实类型引用，
/// 含源码里看不见的传递引用——比文本扫描更硬，且**不经 <see cref="ArchitectureRules"/>**
/// （纯函数的输入里没有程序集元数据，见契约一 §4 末注）。
/// ② **G-06（源码级）**：把真实仓库三工程的源码文本喂给 <see cref="ArchitectureRules"/>。
/// 两个编号是同一违规的两种证据层级，**MUST NOT 混用**。
/// </remarks>
public class PlatformLeakageTests
{
    private static readonly string[] CoreProductAssemblies = ["KFL.Core", "KFL.Infrastructure", "KFL.Rules"];

    private static readonly string[] ForbiddenPlatformAssemblies =
    [
        "PresentationCore",
        "PresentationFramework",
        "WindowsBase",
        "System.Drawing",
        "System.Windows.Forms",
    ];

    [Fact]
    public void G06b核心三工程不引用界面平台程序集()
    {
        foreach (var assemblyName in CoreProductAssemblies)
        {
            var assembly = Assembly.Load(new AssemblyName(assemblyName));
            var referenced = assembly.GetReferencedAssemblies()
                .Select(a => a.Name ?? string.Empty)
                .ToList();

            foreach (var forbidden in ForbiddenPlatformAssemblies)
            {
                Assert.DoesNotContain(
                    referenced,
                    name => name.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public void G06源码级三工程不出现界面平台命名空间()
    {
        Assert.Empty(RepositoryLocator.EvaluateRepository("G-06"));
    }

    [Fact]
    public void 锚点程序集确实含核心类型()
    {
        Assert.NotNull(typeof(KFL.Core.ValueObjects.GameDate).Assembly);
        Assert.NotNull(typeof(KFL.Rules.Config.GameConfig).Assembly);
        Assert.NotNull(typeof(KFL.Tests.Architecture.ArchitectureRules).Assembly);

        // KFL.Infrastructure 按名称加载即可——它在本阶段可能尚无公开类型。
        Assert.Equal("KFL.Infrastructure", Assembly.Load(new AssemblyName("KFL.Infrastructure")).GetName().Name);
    }
}
