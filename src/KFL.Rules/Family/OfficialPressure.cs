using KFL.Core.Entities;
using KFL.Rules.Config;
using KFL.Rules.Settlement;

namespace KFL.Rules.FamilyLifecycle;

/// <summary>
/// 仕途压力 <c>P</c>（契约十九 §4.2 / FR-019）：榜下捉婿的概率与金额都以它为自变量。
/// </summary>
/// <remarks>
/// <para>
/// <c>P = Σ w(L)</c>，<c>L</c> 取**判定开始时在册且 <see cref="Person.Rank"/> 非空**的成员
/// （已亡、已外嫁、无官者一律不计；MUST NOT 另立「是否服刑」判据——服刑者官阶仍在）。
/// </para>
/// <para>
/// <c>P</c> **不落字段、不跨月缓存**（data-model §3.5）：每次判定现算，故它与 ⑧-a 的其他事实一样
/// 只描述当月。
/// </para>
/// <para>
/// 本类型全纯：不写状态、不消费随机。
/// </para>
/// </remarks>
public static class OfficialPressure
{
    /// <summary>在册官员的权重和（L1 ~ L18）。</summary>
    /// <param name="family">被观察的家族。</param>
    /// <returns><c>Σ w(L)</c>；无官员时恰为 0。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="family"/> 为 <c>null</c>。</exception>
    public static decimal Compute(Family family)
    {
        ArgumentNullException.ThrowIfNull(family);

        var pressure = decimal.Zero;
        foreach (var member in CountedMembers.Registered(family))
        {
            if (member.Rank is { } rank)
            {
                pressure += WeightOf(rank.Level);
            }
        }

        return pressure;
    }

    /// <summary>单个官阶的权重 <c>w(L) = 0.02 + 1.48 × (18 − L) / 17</c>。</summary>
    /// <param name="level">官阶 1~18。</param>
    /// <returns>该官阶的权重；L1 恰为 1.50、L18 恰为 0.02。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> 不在 1~18。</exception>
    public static decimal WeightOf(int level)
    {
        if (level < MarriagePolicy.PressureHighestLevel || level > MarriagePolicy.PressureLowestLevel)
        {
            throw new ArgumentOutOfRangeException(
                nameof(level),
                level,
                "官阶为 L1~L18；无官职请用 null 表达（规格书 §8）。");
        }

        return MarriagePolicy.PressureWeightBottom
            + ((MarriagePolicy.PressureWeightTop - MarriagePolicy.PressureWeightBottom)
                * (MarriagePolicy.PressureLowestLevel - level)
                / (MarriagePolicy.PressureLowestLevel - MarriagePolicy.PressureHighestLevel));
    }
}
