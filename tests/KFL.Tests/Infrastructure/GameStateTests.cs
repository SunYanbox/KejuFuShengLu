using System.Reflection;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Infrastructure;

/// <summary>T044：<see cref="GameState"/> 的字段与阶段边界（spec Out of Scope）。</summary>
public class GameStateTests
{
    private static readonly Guid AnyId = Guid.Parse("5f1d0a3e-6f2b-4c8d-9e10-112233445566");

    [Fact]
    public void 拒绝空标识()
    {
        Assert.Throws<ArgumentException>(() => new GameState(
            Guid.Empty, new GameDate(1, 1), Difficulty.Normal, Origin.Farmer, FamilyFixtures.Couple().Family));
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
            AnyId, new GameDate(1, 1), Difficulty.Normal, Origin.Farmer, null!));
    }

    [Fact]
    public void 不存在资产现金储蓄贷款与统计容器字段()
    {
        // 阶段边界守卫：这些一律属阶段②及以后（spec Out of Scope）。
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
    }

    [Fact]
    public void 字段全集恰为五个()
    {
        var expected = new[] { "Id", "CurrentDate", "Difficulty", "Origin", "Family" };

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
        new(AnyId, new GameDate(1, 1), Difficulty.Normal, origin, family ?? FamilyFixtures.Couple().Family);
}
