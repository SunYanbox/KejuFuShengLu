using System.Collections.Concurrent;
using Xunit;

namespace KFL.Tests.Infrastructure;

/// <summary>
/// 存档沙箱在**并行用例**下的自洽性（2026-10-21 CI 偶发失败的回归测试）。
/// </summary>
/// <remarks>
/// <para>
/// 现场：xunit 默认按测试类并行，而所有用例共用同一个父目录。旧版
/// <see cref="SaveTestSandbox.Dispose"/> 会「父目录空了就删掉」，于是 A 的清理与 B 的建根动作撞车，
/// B 拿到 <c>DirectoryNotFoundException</c>（CI 上表现为
/// <c>存档目录不可用：…\kfl-save-tests\&lt;guid&gt;</c>）。
/// </para>
/// <para>
/// 本用例只调沙箱自己的 API（不直接碰文件系统，故 G-07 无需豁免行）：只要共享父目录在用例之间
/// 被删来删去，收敛前的实现会在几百轮里命中一次。
/// </para>
/// </remarks>
public sealed class SaveTestSandboxConcurrencyTests
{
    /// <summary>并发路数（足够多路同时处于「刚建根」与「刚清理」之间）。</summary>
    private const int Workers = 8;

    /// <summary>每路轮数。</summary>
    private const int RoundsPerWorker = 64;

    /// <summary>多路同时建根与清理，任何一路都不应因共享父目录被拆而失败。</summary>
    [Fact]
    public void 并行建根与清理不互相拆台()
    {
        var failures = new ConcurrentBag<string>();

        Parallel.For(
            0,
            Workers,
            new ParallelOptions { MaxDegreeOfParallelism = Workers },
            worker =>
            {
                for (var round = 0; round < RoundsPerWorker; round++)
                {
                    try
                    {
                        using var sandbox = new SaveTestSandbox();
                        sandbox.EnsureRoot();
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"第 {worker} 路第 {round} 轮：{ex.GetType().Name}：{ex.Message}");
                    }
                }
            });

        Assert.True(failures.IsEmpty, string.Join(Environment.NewLine, failures));
    }
}
