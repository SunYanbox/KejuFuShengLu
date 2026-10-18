using System.Globalization;
using System.Windows;
using System.Windows.Media;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Presentation;

/// <summary>一条刻度：<see cref="Position"/> 是画布像素坐标（X 轴为 x 像素、Y 轴为 y 像素）。</summary>
public sealed class ChartTick(double position, string text)
{
    /// <summary>刻度在画布上的像素位置。</summary>
    public double Position { get; } = position;

    /// <summary>刻度文字。</summary>
    public string Text { get; } = text;
}

/// <summary>一条折线：名称、画刷键与逐月像素点。</summary>
public sealed class ChartSeries(string name, string brushKey, PointCollection points)
{
    /// <summary>折线名称。</summary>
    public string Name { get; } = name;

    /// <summary>画刷键，取值只能是 <c>ChartBrush1</c> ~ <c>ChartBrush6</c>。</summary>
    public string BrushKey { get; } = brushKey;

    /// <summary>逐月像素点（与 <see cref="StatsCharts.Build"/> 的月度桶一一对应）。</summary>
    public PointCollection Points { get; } = points;
}

/// <summary>一张可直接绑定的折线图：标题、单位、文字结论、画布尺寸、系列与双轴刻度。</summary>
public sealed class ChartFrame(
    string title,
    string unit,
    string summary,
    double width,
    double height,
    IReadOnlyList<ChartSeries> series,
    IReadOnlyList<ChartTick> xTicks,
    IReadOnlyList<ChartTick> yTicks)
{
    /// <summary>图表标题。</summary>
    public string Title { get; } = title;

    /// <summary>数值单位（例如「贯」）。</summary>
    public string Unit { get; } = unit;

    /// <summary>一到两句文字结论。</summary>
    public string Summary { get; } = summary;

    /// <summary>画布宽（横向滚动的依据）。</summary>
    public double Width { get; } = width;

    /// <summary>画布高。</summary>
    public double Height { get; } = height;

    /// <summary>折线系列。</summary>
    public IReadOnlyList<ChartSeries> Series { get; } = series;

    /// <summary>X 轴刻度。</summary>
    public IReadOnlyList<ChartTick> XTicks { get; } = xTicks;

    /// <summary>Y 轴刻度。</summary>
    public IReadOnlyList<ChartTick> YTicks { get; } = yTicks;

    /// <summary>X 轴刻度文字在画布上的顶边（供视图定位，避免视图硬编码高度）。</summary>
    public double XTickTop => Height - 20;
}

/// <summary>
/// 统计页的月度折线图数据：把家族流水账（<see cref="Ledger"/>）按月聚合成
/// 「月收支」「累计收支」「分类累计」三张图，供 WPF 视图直接绑定绘制。
/// </summary>
public static class StatsCharts
{
    private const string UnitName = "贯";
    private const string Brush1 = "ChartBrush1";
    private const string Brush2 = "ChartBrush2";
    private const string Brush3 = "ChartBrush3";
    private const string Brush4 = "ChartBrush4";
    private const string Brush5 = "ChartBrush5";
    private const string Brush6 = "ChartBrush6";

    private const double ChartHeight = 220;
    private const double PlotLeft = 64;
    private const double PlotTop = 16;
    private const double PlotRight = 24;
    private const double PlotBottom = 26;
    private const int YTickCount = 5;
    private const int MaxCategorySeries = 6;
    private const int WenPerGuan = 1000;
    private const int MonthlyTickCap = 24;
    private const int YearlyTickStep = 12;
    private const int MonthPixelStep = 44;

    /// <summary>按 Ledger 逐月聚合，返回 3 张图；Ledger 为空时返回 1 张带说明的空图（Series 为空列表）。</summary>
    /// <param name="state">当前存档状态（只读，不会被修改）。</param>
    /// <returns>固定顺序的三张折线图；无流水时是单张说明图。</returns>
    public static IReadOnlyList<ChartFrame> Build(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var entries = state.Economy.Ledger.Entries;
        var months = entries
            .Select(entry => entry.Date)
            .Distinct()
            .OrderBy(date => date)
            .ToList();

        if (months.Count == 0)
        {
            return
            [
                new ChartFrame(
                    "月度收支（暂无流水）",
                    UnitName,
                    "族中尚无流水账；先推进几个游戏月份，折线图会随 Ledger 自动生成。",
                    Math.Max(900, 0),
                    ChartHeight,
                    [],
                    [],
                    []),
            ];
        }

        var positions = new Dictionary<GameDate, int>(months.Count);
        for (var i = 0; i < months.Count; i++)
        {
            positions.Add(months[i], i);
        }

        var incomeWen = new decimal[months.Count];
        var expenseWen = new decimal[months.Count];
        var categoryMonthlyWen = new Dictionary<LedgerCategory, decimal[]>();
        var categoryMagnitude = new Dictionary<LedgerCategory, decimal>();

        foreach (var entry in entries)
        {
            var index = positions[entry.Date];
            var amount = entry.Amount.Wen;

            if (amount > 0m)
            {
                incomeWen[index] += amount;
            }
            else
            {
                expenseWen[index] -= amount;
            }

            if (!categoryMonthlyWen.TryGetValue(entry.Category, out var monthly))
            {
                monthly = new decimal[months.Count];
                categoryMonthlyWen.Add(entry.Category, monthly);
            }

            monthly[index] += amount;

            categoryMagnitude.TryGetValue(entry.Category, out var magnitude);
            categoryMagnitude[entry.Category] = magnitude + Math.Abs(amount);
        }

        var income = ToGuan(incomeWen);
        var expense = ToGuan(expenseWen);
        var net = Difference(income, expense);
        var cumulativeIncome = Cumulative(income);
        var cumulativeExpense = Cumulative(expense);
        var cumulativeNet = Cumulative(net);

        var count = months.Count;
        var monthlySummary = FormattableString.Invariant($"共 {count} 个月：月均收入 {income.Sum() / count:0.##} 贯，月均支出 {expense.Sum() / count:0.##} 贯，月均结余 {net.Sum() / count:0.##} 贯。");

        var monthlyFrame = Frame(
            "月收入 / 月支出 / 月结余",
            monthlySummary,
            months,
            [
                new LineSpec("收入", Brush1, income),
                new LineSpec("支出", Brush2, expense),
                new LineSpec("结余", Brush3, net),
            ]);

        var cumulativeSummary = FormattableString.Invariant($"累计收入 {cumulativeIncome[count - 1]:0.##} 贯，累计支出 {cumulativeExpense[count - 1]:0.##} 贯，累计结余 {cumulativeNet[count - 1]:0.##} 贯。");

        var cumulativeFrame = Frame(
            "累计收支与结余",
            cumulativeSummary,
            months,
            [
                new LineSpec("累计收入", Brush1, cumulativeIncome),
                new LineSpec("累计支出", Brush2, cumulativeExpense),
                new LineSpec("累计结余", Brush3, cumulativeNet),
            ]);

        var brushKeys = new[] { Brush1, Brush2, Brush3, Brush4, Brush5, Brush6 };
        var topCategories = categoryMagnitude
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key)
            .Take(MaxCategorySeries)
            .Select(pair => pair.Key)
            .ToList();

        var categoryLines = new List<LineSpec>(topCategories.Count);
        for (var i = 0; i < topCategories.Count; i++)
        {
            var category = topCategories[i];
            categoryLines.Add(new LineSpec(
                category.ToString(),
                brushKeys[i],
                Cumulative(ToGuan(categoryMonthlyWen[category]))));
        }

        var categorySummary = FormattableString.Invariant($"按全期金额绝对值取前 {topCategories.Count} 类（全期共 {categoryMagnitude.Count} 类）；各线是该类别净额的逐月累计，收入为正、支出为负。");

        var categoryFrame = Frame("分类累计（前 6 类）", categorySummary, months, categoryLines);

        return [monthlyFrame, cumulativeFrame, categoryFrame];
    }

    private static ChartFrame Frame(
        string title,
        string summary,
        List<GameDate> months,
        IReadOnlyList<LineSpec> lines)
    {
        var width = (double)Math.Max(900, months.Count * MonthPixelStep);
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

        return new ChartFrame(title, UnitName, summary, width, ChartHeight, series, xTicks, yTicks);
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

    /// <summary>一张图的输入线：名称、画刷键与以「贯」为单位的逐月值。</summary>
    private readonly record struct LineSpec(string Name, string BrushKey, double[] Values);
}
