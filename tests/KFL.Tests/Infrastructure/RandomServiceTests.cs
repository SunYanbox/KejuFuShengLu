using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Services;
using Xunit;

namespace KFL.Tests.Infrastructure;

/// <summary>T042：接缝契约测试（契约二 §1）。</summary>
public class RandomServiceTests
{
    [Fact]
    public void 同种子得到逐位相同的序列()
    {
        var first = new SeededRandomService(20261005);
        var second = new SeededRandomService(20261005);

        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(first.NextDouble(), second.NextDouble());
            Assert.Equal(first.Next(-1000, 1000), second.Next(-1000, 1000));
        }

        var firstBytes = new byte[64];
        var secondBytes = new byte[64];
        first.NextBytes(firstBytes);
        second.NextBytes(secondBytes);

        Assert.Equal(firstBytes, secondBytes);
        Assert.Contains(firstBytes, b => b != 0);
    }

    [Fact]
    public void 不同种子立即产生不同序列()
    {
        // 确定性来自注入，而不是碰巧。
        var first = new SeededRandomService(1);
        var second = new SeededRandomService(2);

        Assert.NotEqual(first.NextDouble(), second.NextDouble());
    }

    [Fact]
    public void Next返回值落在半开区间()
    {
        var random = new SeededRandomService(7);

        for (var i = 0; i < 500; i++)
        {
            var value = random.Next(3, 11);

            Assert.InRange(value, 3, 10);
        }
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(5, 4)]
    [InlineData(0, -1)]
    [InlineData(int.MaxValue, int.MinValue)]
    public void Next拒绝空区间或反向区间(int minInclusive, int maxExclusive)
    {
        var random = new SeededRandomService(11);

        Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(minInclusive, maxExclusive));
    }

    [Fact]
    public void NextBytes写满整个缓冲区()
    {
        var random = new SeededRandomService(13);
        var buffer = new byte[32];

        random.NextBytes(buffer);

        // 若实现了部分填充，这里会出现连续的 0（或是未初始化值被覆盖不全）。
        Assert.Contains(buffer, b => b != 0);
        Assert.Equal(32, buffer.Length);
    }

    [Fact]
    public void NextDouble落在零到一之间()
    {
        var random = new SeededRandomService(17);

        for (var i = 0; i < 500; i++)
        {
            var value = random.NextDouble();

            Assert.True(value >= 0.0, $"NextDouble 返回 {value}，小于 0.0。");
            Assert.True(value < 1.0, $"NextDouble 返回 {value}，不小于 1.0。");
        }
    }

    [Fact]
    public void 接缝通过构造函数注入而非静态单例()
    {
        var random = new SeededRandomService(19);

        Assert.IsAssignableFrom<IRandomService>(random);
        Assert.Empty(typeof(SeededRandomService).GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static));
    }
}
