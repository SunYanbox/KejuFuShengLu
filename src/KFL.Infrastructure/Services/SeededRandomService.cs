using KFL.Infrastructure.Abstractions;

namespace KFL.Infrastructure.Services;

/// <summary>
/// 以固定种子播种的随机来源（契约二 §1 的唯一实现）。
/// </summary>
/// <remarks>
/// <para>
/// 同一种子构造两次，**逐位相同**的调用序列 MUST 得到逐位相同的结果——这是后续所有随机结算
/// 能被单测覆盖的前提（章程原则 IV）。
/// </para>
/// <para>
/// 已知取舍（不阻塞 001，留待阶段④存档落盘时复核）：<see cref="Random"/> 对给定种子的
/// 生成算法**不保证跨 .NET 版本稳定**。001 无落盘，故「同种子 → 同结果」在单次运行内即成立；
/// 若阶段④要求跨版本重放一段存档，需在此改用自实现的确定性算法。
/// </para>
/// </remarks>
public sealed class SeededRandomService : IRandomService
{
    private readonly Random _random;

    /// <summary>以给定种子构造。</summary>
    /// <param name="seed">随机种子；同种子得到同序列。</param>
    public SeededRandomService(int seed)
    {
        _random = new Random(seed); // arch-guard:allow 播种的局部实例，非全局随机源（Random.Shared 与无参 Random 均未使用），契约二 §1
    }

    /// <inheritdoc />
    public double NextDouble() => _random.NextDouble();

    /// <inheritdoc />
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxExclusive"/> &lt;= <paramref name="minInclusive"/>。注意
    /// <see cref="Random.Next(int, int)"/> 对相等区间**不抛异常**，故这里显式校验（契约二 §1）。
    /// </exception>
    public int Next(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive),
                maxExclusive,
                $"上界 MUST 大于下界：maxExclusive({maxExclusive}) <= minInclusive({minInclusive})（契约二 §1）。");
        }

        return _random.Next(minInclusive, maxExclusive);
    }

    /// <inheritdoc />
    public void NextBytes(Span<byte> destination) => _random.NextBytes(destination);
}
