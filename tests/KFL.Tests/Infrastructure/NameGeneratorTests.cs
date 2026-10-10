using KFL.Core.Enums;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Services;
using Xunit;

namespace KFL.Tests.Infrastructure;

/// <summary>
/// quickstart S6 / FR-009、FR-011、SC-008：姓名来源的两个实现都**确定性**且**不触碰全局随机源**。
/// </summary>
/// <remarks>
/// <para>
/// 本文件在架构守卫 G-07 与 SC-008 的扫描范围内（契约一 §2.1、契约八 §4），故：
/// 不使用任何环境访问；期望值一律经被测类型与 <c>KFL.Rules/Config</c> 的成员取得，
/// **不出现姓名库以外的规则数值**。
/// </para>
/// <para>
/// 「规则层不引用 Bogus 类型」一条由架构守卫（G-07 的扫描 + 依赖方向 G-05）覆盖，本文件不重复断言。
/// </para>
/// </remarks>
public class NameGeneratorTests
{
    /// <summary>固定种子：断言的是「同种子 → 同序列」，取值本身不重要。</summary>
    private const int Seed = 20261008;

    /// <summary>两个实现（内置宋风字库 / Bogus zh_CN 适配器）。</summary>
    public static TheoryData<string> Kinds => new() { Song, Bogus };

    private const string Song = "song";
    private const string Bogus = "bogus";

    [Theory]
    [MemberData(nameof(Kinds))]
    public void 两个实现都产出非空汉字姓名(string kind)
    {
        var names = Create(kind, Seed);

        AssertChinese(names.NextSurname());
        AssertChinese(names.NextGivenName(Gender.Male));
        AssertChinese(names.NextGivenName(Gender.Female));
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void 同种子两次构造得到逐项相同的序列(string kind)
    {
        var first = Create(kind, Seed);
        var second = Create(kind, Seed);

        for (var index = 0; index < 20; index++)
        {
            Assert.Equal(first.NextSurname(), second.NextSurname());
            Assert.Equal(first.NextGivenName(Gender.Male), second.NextGivenName(Gender.Male));
            Assert.Equal(first.NextGivenName(Gender.Female), second.NextGivenName(Gender.Female));
        }
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void 两个实例交替取值互不干扰(string kind)
    {
        // 期望序列：一个独立实例取 10 个名。
        var solo = Create(kind, Seed);
        var expected = new List<string>();

        for (var index = 0; index < 10; index++)
        {
            expected.Add(solo.NextGivenName(Gender.Male));
        }

        // 两名同种子实例交替取值：若实现（如 Bogus 适配器）改写了全局种子，
        // 第二个实例的构造与取值都会污染第一个实例的序列，这里的期望就会红。
        var first = Create(kind, Seed);
        var second = Create(kind, Seed);
        var firstTaken = new List<string>();
        var secondTaken = new List<string>();

        for (var index = 0; index < 10; index++)
        {
            firstTaken.Add(first.NextGivenName(Gender.Male));
            secondTaken.Add(second.NextGivenName(Gender.Male));
        }

        Assert.Equal(expected, firstTaken);
        Assert.Equal(expected, secondTaken);

        // 侧证「确实按种子分流」：不同种子给出不同序列。
        var other = Create(kind, Seed + 1);
        var otherTaken = new List<string>();

        for (var index = 0; index < 10; index++)
        {
            otherTaken.Add(other.NextGivenName(Gender.Male));
        }

        Assert.NotEqual(expected, otherTaken);
    }

    [Fact]
    public void 内置宋风字库的姓氏落在白名单内()
    {
        var names = new SongStyleNameGenerator(new SeededRandomService(Seed));

        for (var index = 0; index < 50; index++)
        {
            Assert.Contains(names.NextSurname(), SongStyleNameGenerator.Surnames, StringComparer.Ordinal);
        }

        Assert.Contains(
            names.NextGivenName(Gender.Male), SongStyleNameGenerator.MaleGivenNames, StringComparer.Ordinal);
        Assert.Contains(
            names.NextGivenName(Gender.Female), SongStyleNameGenerator.FemaleGivenNames, StringComparer.Ordinal);
    }

    private static INameGenerator Create(string kind, int seed)
    {
        IRandomService random = new SeededRandomService(seed);

        return kind == Song
            ? new SongStyleNameGenerator(random)
            : new BogusNameGenerator(random);
    }

    /// <summary>非空且每个字符都是汉字（CJK 统一表意文字基本区）。</summary>
    private static void AssertChinese(string value)
    {
        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.All(value, ch => Assert.InRange(ch, '\u4e00', '\u9fff'));
    }
}
