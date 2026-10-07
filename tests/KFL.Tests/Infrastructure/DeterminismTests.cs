using System.Reflection;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Services;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Infrastructure;

/// <summary>
/// T043：US3 AS1（**类名必须含 <c>Determinism</c>**，quickstart §4 用
/// <c>--filter "FullyQualifiedName~Determinism"</c> 定位）。
/// </summary>
/// <remarks>
/// AS2（非 UI 层无系统时钟 / 全局随机 / 文件系统 / 网络）的验证物是 US2 的 T033（G-07），
/// **不在本测试内**——这是本阶段唯一的跨故事依赖，已在 tasks.md 登记。
/// </remarks>
public class DeterminismTests
{
    private static readonly GameDate StartDate = new(1, 1);

    [Fact]
    public void 同种子两次创建的存档级状态完全一致()
    {
        var first = CreateWithSeed(20261005);
        var second = CreateWithSeed(20261005);

        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(Guid.Empty, second.Id);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.CurrentDate, second.CurrentDate);
        Assert.Equal(StartDate, first.CurrentDate);
        Assert.Equal(1, first.CurrentDate.Year);
        Assert.Equal(1, first.CurrentDate.Month);
    }

    [Fact]
    public void 不同种子得到不同标识()
    {
        Assert.NotEqual(CreateWithSeed(1).Id, CreateWithSeed(2).Id);
    }

    [Fact]
    public void 游戏时间来源返回存档的当前年月()
    {
        var state = CreateWithSeed(20261005);
        var clock = new GameStateClock(state);

        Assert.Equal(state.CurrentDate, clock.Current);
        Assert.Equal(StartDate, clock.Current);

        var other = CreateWithSeed(20261005);
        Assert.Equal(new GameStateClock(other).Current, clock.Current);
    }

    [Fact]
    public void 标识没有公开写入通道()
    {
        // FR-012 的「标识 MUST NOT 随存档改名而变化」在 001 内**无可验证对象**
        // （档名与落盘属阶段④），本阶段只能验证「标识不由外部改写」这一形状。
        // MUST NOT 用「同一实例复用即不变」充当该子句的证据——那是同义反复，不可反驳。
        var property = typeof(GameState).GetProperty(nameof(GameState.Id), BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(property);
        Assert.True(
            property.SetMethod is null || !property.SetMethod.IsPublic,
            "GameState.Id MUST NOT 有公开 setter（FR-012）。");
    }

    [Fact]
    public void 生成路径不依赖全局随机源()
    {
        // GameStateFactory 是 001 内唯一的产品生成路径；同种子同标识即证明其来源可注入。
        var factory = new GameStateFactory(new SeededRandomService(42));
        var first = factory.Create(StartDate, Difficulty.Normal, Origin.Scholar, new Family("测试"), NewEconomy());
        var second = factory.Create(StartDate, Difficulty.Hard, Origin.Farmer, new Family("另一族"), NewEconomy());

        var replay = new GameStateFactory(new SeededRandomService(42));
        var firstAgain = replay.Create(
            StartDate, Difficulty.Normal, Origin.Scholar, new Family("测试"), NewEconomy());
        var secondAgain = replay.Create(
            StartDate, Difficulty.Hard, Origin.Farmer, new Family("另一族"), NewEconomy());

        Assert.Equal(first.Id, firstAgain.Id);
        Assert.Equal(second.Id, secondAgain.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void 接缝实现位于基础设施层且不经静态全局()
    {
        Assert.Equal("KFL.Infrastructure", typeof(SeededRandomService).Assembly.GetName().Name);
        Assert.Equal("KFL.Infrastructure", typeof(GameStateClock).Assembly.GetName().Name);
        Assert.Equal("KFL.Infrastructure", typeof(GameStateFactory).Assembly.GetName().Name);
        Assert.Equal("KFL.Core", typeof(GameState).Assembly.GetName().Name);
    }

    private static GameState CreateWithSeed(int seed)
    {
        var family = FamilyFixtures.Couple().Family;
        return new GameStateFactory(new SeededRandomService(seed))
            .Create(StartDate, Difficulty.Normal, Origin.Farmer, family, NewEconomy());
    }

    /// <summary>
    /// 一个合法的经济聚合初值（6 参构造的最后一个实参，R-13）。米价系数与生活费档位的**产品**
    /// 初值单点在 <c>KFL.Rules/Config</c>（<c>GrainPricePolicy</c> / <c>LivingCostTable.InitialStandard</c>），
    /// 本测试只要求一个合法取值——确定性断言针对的是标识生成，与这两个初值无关。
    /// </summary>
    private static FamilyEconomy NewEconomy() => new(LivingStandard.Normal, new GrainPriceIndex(1m));
}
