using System.Reflection;
using KFL.Core.ValueObjects;
using Xunit;

namespace KFL.Tests.Core;

/// <summary>
/// T024：<see cref="Money"/> 的边界与口径（data-model §1.1；research R-02）。
/// </summary>
/// <remarks>
/// 只钉**结构性与精度**事实：贯 / 文的换算、派生值、四则与比较、无隐式数值转换、无内部舍入。
/// 任何平衡数值都不在本文件出现。
/// </remarks>
public class MoneyTests
{
    [Fact]
    public void FromGuan按一贯千文换算()
    {
        Assert.Equal(1000m, Money.FromGuan(1m).Wen);
        Assert.Equal(500m, Money.FromGuan(0.5m).Wen);
        Assert.Equal(0m, Money.FromGuan(0m).Wen);
        Assert.Equal(Money.FromWen(2500m), Money.FromGuan(2.5m));
    }

    [Fact]
    public void FromWen与Guan派生互为逆运算()
    {
        Assert.Equal(1m, Money.FromWen(1000m).Guan);
        Assert.Equal(0.001m, Money.FromWen(1m).Guan);
        Assert.Equal(-0.5m, Money.FromWen(-500m).Guan);
        Assert.Equal(Money.FromWen(1000m), Money.FromGuan(Money.FromWen(1000m).Guan));
        Assert.Equal(Money.FromWen(2500m), Money.FromGuan(Money.FromWen(2500m).Guan));
    }

    [Fact]
    public void Zero为零文且非正非负()
    {
        Assert.Equal(0m, Money.Zero.Wen);
        Assert.Equal(0m, Money.Zero.Guan);
        Assert.False(Money.Zero.IsPositive);
        Assert.False(Money.Zero.IsNegative);
        Assert.Equal(Money.Zero, Money.FromWen(0m));
        Assert.Equal(Money.Zero, Money.FromGuan(0m));
    }

    [Fact]
    public void 正负判定按文数()
    {
        Assert.True(Money.FromWen(1m).IsPositive);
        Assert.False(Money.FromWen(1m).IsNegative);
        Assert.True(Money.FromWen(-1m).IsNegative);
        Assert.False(Money.FromWen(-1m).IsPositive);
        Assert.True(Money.FromGuan(0.5m).IsPositive);
    }

    [Fact]
    public void 四则运算保持文口径()
    {
        var left = Money.FromWen(300m);
        var right = Money.FromWen(120m);

        Assert.Equal(Money.FromWen(420m), left + right);
        Assert.Equal(Money.FromWen(180m), left - right);
        Assert.Equal(Money.FromWen(120m), right - left + Money.FromWen(300m));
        Assert.Equal(Money.FromWen(-300m), -left);
        Assert.Equal(Money.FromWen(300m), -(-left));
        Assert.Equal(Money.FromGuan(0.75m), left * 2.5m);
        Assert.Equal(Money.FromWen(300m), left * 1m);
    }

    [Fact]
    public void 比较与相等等价于按文数比较()
    {
        var small = Money.FromWen(1m);
        var big = Money.FromWen(2m);
        var equalToSmall = Money.FromWen(1m);

        Assert.True(small < big);
        Assert.True(big > small);
        Assert.False(small > big);
        Assert.False(big < small);
        Assert.False(small < equalToSmall);
        Assert.False(equalToSmall > small);
        Assert.True(small == Money.FromWen(1m));
        Assert.True(small != big);
        Assert.False(small != Money.FromWen(1m));
        Assert.Equal(Money.FromWen(1000m), Money.FromGuan(1m));
        Assert.Equal(Money.FromWen(1000m).GetHashCode(), Money.FromGuan(1m).GetHashCode());
    }

    [Fact]
    public void 有限decimal两端被接受且乘法溢出被拒()
    {
        // decimal 在 C# 与 .NET 里**没有** NaN / Infinity 取值（既无 decimal.NaN 也无
        // decimal.PositiveInfinity），故「非有限 decimal」在类型层面即不可表达：FromWen 的实参
        // 本身就是有限值。可断言的是：有限域两端被接受，且 FromGuan 的 ×1000 越界 MUST 抛出
        // 而不是静默截断或返回非有限值（Money.FromWen 的有限性守卫即按此定义）。
        Assert.Equal(decimal.MaxValue, Money.FromWen(decimal.MaxValue).Wen);
        Assert.Equal(decimal.MinValue, Money.FromWen(decimal.MinValue).Wen);

        Assert.Throws<OverflowException>(() => Money.FromGuan(decimal.MaxValue));
        Assert.Throws<OverflowException>(() => Money.FromGuan(decimal.MinValue));
    }

    [Fact]
    public void 不存在隐式或显式数值转换()
    {
        // 编译期口径守卫的可执行形式：「贯」「文」与裸 double / int / decimal MUST NOT 互转。
        var conversions = typeof(Money)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name is "op_Implicit" or "op_Explicit")
            .ToList();

        Assert.Empty(conversions);

        // 具名入口是唯一构造通道：不存在带参数的公开构造函数，裸 new Money(500) 不可编译。
        var publicValueConstructors = typeof(Money)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(constructor => constructor.GetParameters().Length > 0)
            .ToList();

        Assert.Empty(publicValueConstructors);

        // 唯一的底层构造入口是 private Money(decimal)，对外不可见（data-model §1.1）。
        var hiddenConstructors = typeof(Money)
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(constructor =>
                constructor.GetParameters().Length == 1
                && constructor.GetParameters()[0].ParameterType == typeof(decimal))
            .ToList();

        Assert.Single(hiddenConstructors);
    }

    [Fact]
    public void 乘系数不内部舍入()
    {
        var third = 1m / 3m;

        var exact = Money.FromGuan(1m) * third;

        // 全精度：与直接用 decimal 计算逐位相同。
        Assert.Equal(1000m * third, exact.Wen);
        Assert.Equal(Money.FromWen(1000m * third), exact);

        // 「先取整再比较」会得到不同的结果——舍入属展示层（阶段③），MUST NOT 发生在 Money 内。
        Assert.NotEqual(decimal.Round(1000m * third), exact.Wen);
        Assert.NotEqual(decimal.Truncate(1000m * third), exact.Wen);
        Assert.NotEqual(Money.Zero, Money.FromWen(1m) * third);
        Assert.NotEqual(Money.FromWen(1m), Money.FromWen(1m) * third);
    }
}
