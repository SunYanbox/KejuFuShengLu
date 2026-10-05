using System.Diagnostics.CodeAnalysis;

namespace KFL.Infrastructure.Abstractions;

/// <summary>
/// 随机来源接缝（契约二 §1）。签名**逐字**取自契约二。
/// </summary>
/// <remarks>
/// 此后任何需要随机的规则都从这里取，MUST NOT 直接读全局随机数（章程原则 IV）。
/// 001 只交付一个实现：<see cref="Services.SeededRandomService"/>。
/// </remarks>
public interface IRandomService
{
    /// <summary>取一个 <c>[0.0, 1.0)</c> 的浮点数。</summary>
    /// <returns>区间内的值。</returns>
    double NextDouble();

    /// <summary>取一个 <c>[minInclusive, maxExclusive)</c> 的整数。</summary>
    /// <param name="minInclusive">下界（含）。</param>
    /// <param name="maxExclusive">上界（不含）。</param>
    /// <returns>区间内的值。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxExclusive"/> &lt;= <paramref name="minInclusive"/>。</exception>
    [SuppressMessage(
        "Naming",
        "CA1716:Identifiers should not match keywords",
        Justification = "签名逐字取自契约二 §1（int Next(int minInclusive, int maxExclusive)），改名会偏离接口真源。")]
    int Next(int minInclusive, int maxExclusive);

    /// <summary>用随机字节**填满** <paramref name="destination"/>，不得部分填充。</summary>
    /// <param name="destination">目标缓冲区。</param>
    void NextBytes(Span<byte> destination);
}
