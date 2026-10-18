using KFL.Rules.Achievements;
using KFL.Rules.Config;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// T058：<see cref="TukeyTrimean.Of"/> 的黄金用例——三均值口径与「奇数样本的中位数同时计入上下半」的
/// 归一化约定。
/// </summary>
/// <remarks>
/// 编年史 ⑥ 的平均寿命口径即此函数；任何改动都会改写历史存档的既有数值，因此逐个钉死。
/// </remarks>
public class TukeyTrimeanTests
{
    [Fact]
    public void 单样本三种均值相同()
    {
        Assert.Equal(50.000m, TukeyTrimean.Of([50]));
    }

    [Fact]
    public void 双样本上下半各取一端()
    {
        Assert.Equal(50.000m, TukeyTrimean.Of([40, 60]));
    }

    [Fact]
    public void 奇数样本的中位数同时计入上下半()
    {
        // 若把中位数排除在上下半之外（教科书写法之一），此例会得 (20+100+80)/4 = 50 之外的值。
        Assert.Equal(50.000m, TukeyTrimean.Of([10, 50, 90]));
    }

    [Fact]
    public void 四样本取两个半样本的中间()
    {
        Assert.Equal(47.500m, TukeyTrimean.Of([10, 40, 60, 70]));
    }

    [Fact]
    public void 五样本带离群值()
    {
        Assert.Equal(45.000m, TukeyTrimean.Of([10, 20, 50, 60, 70]));
    }

    [Fact]
    public void 离群值被三均值压制而不是被拉平()
    {
        // 区区 100 岁的离群值若按算术平均会得到 40.000（或按某些口径 36.250）；三均值口径为 30.000。
        Assert.Equal(30.000m, TukeyTrimean.Of([10, 20, 30, 40, 100]));
        Assert.NotEqual(36.250m, TukeyTrimean.Of([10, 20, 30, 40, 100]));
    }

    [Fact]
    public void 空序列为零而不是抛异常()
    {
        // 无死亡记录（新开局或全员在世）必须得到一个可入存档的数值，MUST NOT 抛、MUST NOT 为 null。
        Assert.Equal(0.000m, TukeyTrimean.Of([]));
    }

    [Fact]
    public void 九样本取四分之一与四分之三分位()
    {
        Assert.Equal(5.000m, TukeyTrimean.Of([1, 2, 3, 4, 5, 6, 7, 8, 9]));
    }

    [Fact]
    public void 入参不被就地排序()
    {
        int[] ages = [90, 10, 50];

        Assert.Equal(50.000m, TukeyTrimean.Of(ages));

        // 只读契约：实现内部复制后再排序，调用方的数组保持原序。
        Assert.Equal([90, 10, 50], ages);
    }

    [Fact]
    public void 结果保留三位小数刻度()
    {
        var bits = decimal.GetBits(TukeyTrimean.Of([10, 40, 60, 70]));

        // 刻度固定为 3（与 AchievementPolicy.AverageLifespanDecimals 一致），
        // 因为保存格式按位比对十进制值（004 FR-021）。
        Assert.Equal(AchievementPolicy.AverageLifespanDecimals, (bits[3] >> 16) & 0xFF);
    }

    [Fact]
    public void 负数年龄不被特殊处理()
    {
        // 断言的是「实现里没有为负数分支」：仅做排序与分位，不夹取。
        Assert.Equal(-5.000m, TukeyTrimean.Of([-10, -5, -5, 0]));
    }
}
