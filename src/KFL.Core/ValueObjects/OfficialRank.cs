namespace KFL.Core.ValueObjects;

/// <summary>
/// 官阶（规格书 §8.1 十八级）。
/// </summary>
/// <remarks>
/// **不表示「无官职」**——无官职由 <c>Person.Rank is null</c> 表达（FR-009）。
/// 年俸数值表（72~5100 贯）属阶段⑧，本类型不落任何俸禄常量。
/// </remarks>
public readonly record struct OfficialRank
{
    /// <summary>构造并校验。</summary>
    /// <param name="level">品级，1~18。</param>
    /// <exception cref="ArgumentOutOfRangeException">不在 1~18。</exception>
    public OfficialRank(int level)
    {
        if (level is < 1 or > 18)
        {
            throw new ArgumentOutOfRangeException(
                nameof(level), level, "官阶为 L1~L18；无官职请用 null 表达（规格书 §8）。");
        }

        Level = level;
    }

    /// <summary>品级。</summary>
    public int Level { get; }

    /// <inheritdoc />
    public override string ToString() => $"L{Level}";
}
