namespace KFL.Core.ValueObjects;

/// <summary>
/// 游戏年月（规格书 §3：从 1 年 1 月开始推演，1 回合 = 1 游戏月，1 年 = 12 月）。
/// </summary>
/// <remarks>
/// **明确不含**：12 / 14 等成年年龄，以及生日月份之外的任何规则数值——成年判定属阶段②
/// （research R-07）。本类型只做日期算术。
/// </remarks>
public readonly record struct GameDate : IComparable<GameDate>, IComparable
{
    /// <summary>构造并校验。</summary>
    /// <param name="year">架空纪年，从 1 起。</param>
    /// <param name="month">1~12，无闰月。</param>
    /// <exception cref="ArgumentOutOfRangeException">年 &lt; 1 或月不在 1~12。</exception>
    public GameDate(int year, int month)
    {
        if (year < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(year), year, "游戏纪年从 1 起（规格书 §3）。");
        }

        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), month, "月份必须在 1~12 之间（规格书 §3）。");
        }

        Year = year;
        Month = month;
    }

    /// <summary>架空纪年。</summary>
    public int Year { get; }

    /// <summary>月份，1~12。</summary>
    public int Month { get; }

    /// <summary>从本年月到 <paramref name="other"/> 的月差（可为负）。</summary>
    /// <param name="other">另一个年月。</param>
    /// <returns>月差；<paramref name="other"/> 在后为正。</returns>
    public int ElapsedMonths(GameDate other) => ((other.Year - Year) * 12) + (other.Month - Month);

    /// <summary>在本年月出生者于 <paramref name="at"/> 时已满几周岁（生日当月即计入，规格书 §4.3）。</summary>
    /// <param name="at">查询时点。</param>
    /// <returns>已满周岁数。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="at"/> 早于本年月（出生之前不存在年龄）。</exception>
    public int AgeInYearsAt(GameDate at)
    {
        if (at.CompareTo(this) < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(at), at, "不能查询出生之前的年龄（本年月即出生年月）。");
        }

        var years = at.Year - Year;
        if (at.Month < Month)
        {
            years--;
        }

        return years;
    }

    /// <inheritdoc />
    public int CompareTo(GameDate other) =>
        Year != other.Year ? Year.CompareTo(other.Year) : Month.CompareTo(other.Month);

    /// <inheritdoc />
    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        GameDate other => CompareTo(other),
        _ => throw new ArgumentException($"无法与 {obj.GetType()} 比较。", nameof(obj)),
    };

    /// <summary>较早者在前。</summary>
    public static bool operator <(GameDate left, GameDate right) => left.CompareTo(right) < 0;

    /// <summary>不晚于。</summary>
    public static bool operator <=(GameDate left, GameDate right) => left.CompareTo(right) <= 0;

    /// <summary>较晚者在前。</summary>
    public static bool operator >(GameDate left, GameDate right) => left.CompareTo(right) > 0;

    /// <summary>不早于。</summary>
    public static bool operator >=(GameDate left, GameDate right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => $"{Year} 年 {Month} 月";
}
