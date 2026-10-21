using System.Globalization;
using KFL.Core.ValueObjects;

namespace KFL.Presentation;

public static partial class StatsCharts
{
    /// <summary>
    /// 统计用自然月偏移。图表的 x 轴仍是「有数据的月份」，但同比 / 环比必须按日历月找基期，
    /// 否则稀疏桶的「前一桶」可能是几个月之前。
    /// </summary>
    private static GameDate MonthOffset(GameDate date, int offset)
    {
        var total = (date.Year * 12) + (date.Month - 1) + offset;
        var year = total / 12;
        var month = total % 12;
        if (month < 0)
        {
            month += 12;
            year -= 1;
        }

        return new GameDate(year, month + 1);
    }

    /// <summary>
    /// 取 <paramref name="target"/> 月的基期值。流量类系列缺失月份按 0；累计 / 存量类系列
    /// 取「不晚于目标月的最近一个桶」的值（没有流水时累计 / 存量不变）。
    /// 目标月早于首个数据月时返回 <see langword="null"/>，表示历史不足。
    /// </summary>
    private static double? BaselineAt(
        List<GameDate> months, double[] values, GameDate target, bool carryForward)
    {
        if (months.Count == 0 || target < months[0])
        {
            return null;
        }

        var low = 0;
        var high = months.Count - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (months[middle] <= target)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return carryForward || months[low] == target ? values[low] : 0d;
    }

    /// <summary>
    /// 增长率（小数，不是百分数）。基期为 0 时不可计算；基期为负时用绝对值作分母，
    /// 保证「亏得少了」显示为正增长而不是符号反转。
    /// </summary>
    private static double? Rate(double current, double? baseline)
    {
        if (baseline is not { } value || value == 0d)
        {
            return null;
        }

        return (current - value) / Math.Abs(value);
    }

    /// <summary>增长率显示：正数带 +、负数带 −、不可计算显示「—」。</summary>
    private static string FormatRate(double? rate) =>
        rate is { } value
            ? value.ToString("+0.00%;-0.00%;0.00%", CultureInfo.InvariantCulture)
            : "—";
}
