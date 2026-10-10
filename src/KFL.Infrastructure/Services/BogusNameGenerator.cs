using Bogus;
using Bogus.DataSets;
using KFL.Core.Enums;
using KFL.Infrastructure.Abstractions;

namespace KFL.Infrastructure.Services;

/// <summary>
/// Bogus <c>zh_CN</c> 姓名适配器（规格书 §2 的「姓名生成（Bogus zh_CN）」；FR-009）。
/// </summary>
/// <remarks>
/// <para>
/// <b>局部种子、无全局状态</b>：构造时**以注入随机取一个整数种子**，再
/// <c>new Randomizer(seed)</c> 得到**局部**随机器，只在该局部实例上取名。
/// 本类 MUST NOT 赋值 Bogus 的全局静态 <c>Randomizer.Seed</c>——那会让「同种子 → 同结果」
/// 变成隐式全局状态的可达路径（章程原则 IV；R-09）。
/// </para>
/// <para>
/// <b>不使用 Bogus 的分布方法</b>：正态取样自实现在 <c>KFL.Rules/Config/AttributePolicy</c>
/// （Box–Muller），Bogus 只负责姓名。本类也 MUST NOT 出现任何游戏数值。
/// </para>
/// <para>
/// 「两个实例交替取值互不干扰」由 T013a 的断言侧证：局部随机器不再是共享全局状态。
/// </para>
/// </remarks>
public sealed class BogusNameGenerator : INameGenerator
{
    /// <summary>中文（简体）区域；姓名库随该区域取用。</summary>
    private const string ChineseLocale = "zh_CN";

    private readonly Name _name;

    /// <summary>构造：从注入的随机来源取整数种子，建立**局部**随机器。</summary>
    /// <param name="random">随机来源（种子的唯一来源）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="random"/> 为 <c>null</c>。</exception>
    public BogusNameGenerator(IRandomService random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var seed = random.Next(int.MinValue, int.MaxValue);

        // 局部 Randomizer：它「完全忽略全局静态 Randomizer.Seed」（Bogus 的构造注释原文）。
        var randomizer = new Randomizer(seed);

        _name = new Name(ChineseLocale) { Random = randomizer };
    }

    /// <inheritdoc />
    public string NextSurname() => _name.LastName();

    /// <inheritdoc />
    public string NextGivenName(Gender gender) => _name.FirstName(
        gender == Gender.Male ? Name.Gender.Male : Name.Gender.Female);
}
