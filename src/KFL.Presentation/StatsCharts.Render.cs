using System.Globalization;
using System.Windows;
using System.Windows.Media;
using KFL.Core.ValueObjects;

namespace KFL.Presentation;

public static partial class StatsCharts
{
    private static ChartFrame Frame(
        string title,
        string unit,
        string summary,
        List<Bucket> buckets,
        IReadOnlyList<LineSpec> lines)
    {
        var months = buckets.Select(bucket => bucket.Month).ToList();
        var width = (double)Math.Max(FrameWidth, months.Count * MonthPixelStep);
        var minimum = 0d;
        var maximum = 0d;
        var magnitudes = new List<double>();

        foreach (var line in lines)
        {
            foreach (var value in line.Values)
            {
                minimum = Math.Min(minimum, value);
                maximum = Math.Max(maximum, value);
                magnitudes.Add(Math.Abs(value));
            }
        }

        // 纵轴量程按 95% 分位裁剪：账面上偶发的一次性巨款（赏赐、嫁妆、罚金）会把其余月份压成一条直线，
        // 读图反而看不出常态波动，因此极端值贴边显示，并在图注里说明。
        var clampedPeaks = false;
        if (magnitudes.Count > 0)
        {
            magnitudes.Sort();
            var rank = (int)Math.Floor(magnitudes.Count * 0.95);
            var percentile = magnitudes[Math.Min(magnitudes.Count - 1, rank)] * 1.05;
            if (percentile > 0)
            {
                var lower = Math.Max(minimum, -percentile);
                var upper = Math.Min(maximum, percentile);
                clampedPeaks = lower > minimum || upper < maximum;
                minimum = lower;
                maximum = upper;
            }
        }

        if (clampedPeaks)
        {
            summary += "（纵轴按 95% 分位取量程，超出的峰值贴边显示）";
        }

        var series = new List<ChartSeries>(lines.Count);
        foreach (var line in lines)
        {
            var points = new PointCollection(line.Values.Length);
            for (var i = 0; i < line.Values.Length; i++)
            {
                points.Add(new Point(
                    XAt(i, months.Count, width),
                    YAt(Math.Clamp(line.Values[i], minimum, maximum), minimum, maximum)));
            }

            series.Add(new ChartSeries(line.Name, line.BrushKey, points));
        }

        var xTicks = new List<ChartTick>();
        var step = months.Count <= MonthlyTickCap ? 1 : YearlyTickStep;
        for (var i = 0; i < months.Count; i += step)
        {
            xTicks.Add(new ChartTick(
                XAt(i, months.Count, width),
                FormattableString.Invariant($"{months[i].Year}年{months[i].Month}月")));
        }

        var yTicks = new List<ChartTick>(YTickCount);
        for (var i = 0; i < YTickCount; i++)
        {
            var value = minimum + ((maximum - minimum) * i / (YTickCount - 1));
            yTicks.Add(new ChartTick(
                YAt(value, minimum, maximum),
                value.ToString("0.##", CultureInfo.InvariantCulture)));
        }

        return new ChartFrame(
            title, unit, summary, width, ChartHeight, series, xTicks, yTicks,
            Hovers(months, width, lines, unit));
    }

    /// <summary>逐月命中条：提示文字列出该月每个系列的值（玩家反馈 5）。</summary>
    private static List<ChartHover> Hovers(
        List<GameDate> months, double width, IReadOnlyList<LineSpec> lines, string unit)
    {
        var hovers = new List<ChartHover>(months.Count);
        var column = months.Count <= 1
            ? width - PlotLeft - PlotRight
            : Math.Max(12d, (width - PlotLeft - PlotRight) / Math.Max(1, months.Count - 1));

        for (var i = 0; i < months.Count; i++)
        {
            var center = XAt(i, months.Count, width);
            var left = Math.Max(0d, center - (column / 2));
            var text = new List<string>
            {
                FormattableString.Invariant($"{months[i].Year} 年 {months[i].Month} 月"),
            };

            foreach (var line in lines)
            {
                var current = line.Values[i];
                var yearOverYear = Rate(
                    current, BaselineAt(months, line.Values, MonthOffset(months[i], -12), line.CarryForward));
                var monthOverMonth = Rate(
                    current, BaselineAt(months, line.Values, MonthOffset(months[i], -1), line.CarryForward));
                text.Add(FormattableString.Invariant(
                    $"{line.Name} {current:0.##} {unit}（同比 {FormatRate(yearOverYear)}；环比 {FormatRate(monthOverMonth)}）"));
            }

            hovers.Add(new ChartHover(left, column, string.Join(Environment.NewLine, text)));
        }

        return hovers;
    }

    private static double XAt(int index, int count, double width)
    {
        if (count <= 1)
        {
            return (PlotLeft + (width - PlotRight)) / 2;
        }

        return PlotLeft + (index * (width - PlotLeft - PlotRight) / (count - 1));
    }

    private static double YAt(double value, double minimum, double maximum)
    {
        var span = maximum - minimum;
        if (span <= 0)
        {
            return ChartHeight - PlotBottom;
        }

        var ratio = (value - minimum) / span;
        return (ChartHeight - PlotBottom) - (ratio * (ChartHeight - PlotBottom - PlotTop));
    }

    private static double[] ToGuan(decimal[] values)
    {
        var result = new double[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            result[i] = (double)(values[i] / WenPerGuan);
        }

        return result;
    }

    private static double[] Difference(double[] left, double[] right)
    {
        var result = new double[left.Length];
        for (var i = 0; i < left.Length; i++)
        {
            result[i] = left[i] - right[i];
        }

        return result;
    }

    private static double[] Cumulative(double[] values)
    {
        var result = new double[values.Length];
        var running = 0d;
        for (var i = 0; i < values.Length; i++)
        {
            running += values[i];
            result[i] = running;
        }

        return result;
    }

    /// <summary>
    /// 一张图的输入线：名称、画刷键与逐月值。
    /// <paramref name="CarryForward"/> 为真表示累计 / 存量系列，缺失月份沿用最近一个桶的值。
    /// </summary>
    private readonly record struct LineSpec(
        string Name, string BrushKey, double[] Values, bool CarryForward = false);
}
