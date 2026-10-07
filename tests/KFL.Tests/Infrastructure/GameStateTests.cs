using System.Reflection;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Infrastructure;

/// <summary>T044：<see cref="GameState"/> 的字段与阶段边界（spec Out of Scope，T021 修订）。</summary>
public class GameStateTests
{
    private static readonly Guid AnyId = Guid.Parse("5f1d0a3e-6f2b-4c8d-9e10-112233445566");

    [Fact]
    public void 拒绝空标识()
    {
        Assert.Throws<ArgumentException>(() => new GameState(
            Guid.Empty,
            new GameDate(1, 1),
            Difficulty.Normal,
            Origin.Farmer,
            FamilyFixtures.Couple().Family,
            NewEconomy()));
    }

    [Fact]
    public void 起始年月为一一年一月()
    {
        var state = NewState();

        Assert.Equal(1, state.CurrentDate.Year);
        Assert.Equal(1, state.CurrentDate.Month);
        Assert.Equal(new GameDate(1, 1), state.CurrentDate);
    }

    [Fact]
    public void 难度四档可读可写()
    {
        var state = NewState();

        foreach (var difficulty in Enum.GetValues<Difficulty>())
        {
            state.Difficulty = difficulty;

            Assert.Equal(difficulty, state.Difficulty);
        }
    }

    [Fact]
    public void 出身四档可读()
    {
        foreach (var origin in Enum.GetValues<Origin>())
        {
            Assert.Equal(origin, NewState(origin).Origin);
        }
    }

    [Fact]
    public void 家族可读且不可为空()
    {
        var family = FamilyFixtures.Couple().Family;

        Assert.Same(family, NewState(family: family).Family);
        Assert.Throws<ArgumentNullException>(() => new GameState(
            AnyId, new GameDate(1, 1), Difficulty.Normal, Origin.Farmer, null!, NewEconomy()));
        Assert.Throws<ArgumentNullException>(() => new GameState(
            AnyId, new GameDate(1, 1), Difficulty.Normal, Origin.Farmer, family, null!));
    }

    [Fact]
    public void 自身不直接暴露资金与统计容器且经济状态只在聚合之下()
    {
        // 阶段① 的边界守卫，T021 修订：原文是「GameState 完全不存在资产/现金/储蓄/贷款/统计容器字段」。
        // 阶段② 起经济状态有了正当归属（FamilyEconomy 聚合，data-model §3.6），该断言**有依据地放宽**：
        // 放的只是「不在 GameState 自身暴露」，守的仍是阶段① 的要害——资金与统计容器不得绕过聚合，
        // 因为 FR-021 / SC-005 要求资金流动只有「落条目 + 动池」这一条通道。
        string[] forbiddenFragments =
            ["Asset", "Cash", "Saving", "Loan", "Pool", "Ledger", "Statistics", "Money", "Capital"];

        var members = typeof(GameState)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Concat(typeof(GameState)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name))
            .ToList();

        foreach (var fragment in forbiddenFragments)
        {
            Assert.DoesNotContain(members, name => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }

        // 这些容器必须仍可达，只是统一挂在 Economy 之下（且 Economy 只读引用、不可为 null）。
        var economyProperty = typeof(GameState)
            .GetProperty(nameof(GameState.Economy), BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(economyProperty);
        Assert.True(economyProperty.CanRead, "GameState.Economy MUST 可读。");
        Assert.Null(economyProperty.SetMethod);

        var economy = NewState().Economy;

        Assert.NotNull(economy);
        Assert.NotNull(economy.Treasury);
        Assert.NotNull(economy.Ledger);
    }

    [Fact]
    public void 公开属性全集恰为七个()
    {
        // T021 修订：阶段① 的「恰为五个」随 Economy / PendingDifficulty 的落地扩充为七个；
        // 断言仍在守住「新增字段必须经显式裁决」——多一个公开属性就会红。
        var expected = new[]
        {
            "Id", "CurrentDate", "Difficulty", "Origin", "Family", "Economy", "PendingDifficulty",
        };

        Assert.Equal(
            expected,
            typeof(GameState)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray()
                .OrderBy(n => Array.IndexOf(expected, n))
                .ToArray());
    }

    private static GameState NewState(Origin origin = Origin.Farmer, Family? family = null) =>
        new(
            AnyId,
            new GameDate(1, 1),
            Difficulty.Normal,
            origin,
            family ?? FamilyFixtures.Couple().Family,
            NewEconomy());

    /// <summary>
    /// 一个合法的经济聚合初值。米价系数的**产品**初值单点在
    /// <c>KFL.Rules/Config/GrainPricePolicy</c>（阶段② 的配置批次），本测试只需要一个合法正值；
    /// 生活费档位取 <see cref="LivingStandard.Normal"/>，仅作夹具取值，产品初值同样单点在配置层。
    /// </summary>
    private static FamilyEconomy NewEconomy() => new(LivingStandard.Normal, new GrainPriceIndex(1m));
}
