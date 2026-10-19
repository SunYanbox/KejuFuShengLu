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

    /// <summary>画刷键，取值只能是 <c>ChartBrush1</c> ~ <c>ChartBrush7</c>。</summary>
    public string BrushKey { get; } = brushKey;

    /// <summary>逐月像素点（与 <see cref="StatsCharts.Build"/> 的月度桶一一对应）。</summary>
    public PointCollection Points { get; } = points;
}

/// <summary>
/// 一个悬停命中条：覆盖某个月份的整列（玩家反馈 5：鼠标放在图上要能看见当月数值）。
/// </summary>
/// <param name="left">该列左边缘的画布 x 像素。</param>
/// <param name="width">该列宽度（像素）。</param>
/// <param name="text">悬停提示文字（多行：月份 + 各系列数值）。</param>
public sealed class ChartHover(double left, double width, string text)
{
    /// <summary>命中条左边缘（画布坐标）。</summary>
    public double Left { get; } = left;

    /// <summary>命中条宽度（像素）。</summary>
    public double Width { get; } = width;

    /// <summary>悬停提示文字。</summary>
    public string Text { get; } = text;
}

/// <summary>一张可直接绑定的折线图：标题、单位、文字结论、画布尺寸、系列、悬停条与双轴刻度。</summary>
public sealed class ChartFrame(
    string title,
    string unit,
    string summary,
    double width,
    double height,
    IReadOnlyList<ChartSeries> series,
    IReadOnlyList<ChartTick> xTicks,
    IReadOnlyList<ChartTick> yTicks,
    IReadOnlyList<ChartHover> hovers)
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

    /// <summary>逐月悬停命中条（画在同一画布上，填充透明可命中）。</summary>
    public IReadOnlyList<ChartHover> Hovers { get; } = hovers;

    /// <summary>X 轴刻度文字在画布上的顶边（供视图定位，避免视图硬编码高度）。</summary>
    public double XTickTop => Height - 20;
}

/// <summary>
/// 统计页的月度折线图数据：把家族流水账（<see cref="Ledger"/>）与功名变迁史按月聚合，
/// 生成「收支」「分类」「人口」三组图，供 WPF 视图直接绑定绘制。
/// </summary>
/// <remarks>
/// 2026-10-19 玩家反馈批次：①图例改中文；②每图带逐月悬停条；③收支/分类与人口各自
/// 支持「当前值 / 累计值」切换；④新增人口图（出生 / 死亡 / 购买 / 入狱 / 举人 / 贡士 / 进士）。
/// </remarks>
public static class StatsCharts
{
    private const string UnitName = "贯";
    private const string CountUnit = "人";
    private const string Brush1 = "ChartBrush1";
    private const string Brush2 = "ChartBrush2";
    private const string Brush3 = "ChartBrush3";
    private const string Brush4 = "ChartBrush4";
    private const string Brush5 = "ChartBrush5";
    private const string Brush6 = "ChartBrush6";
    private const string Brush7 = "ChartBrush7";

    private static readonly string[] BrushKeys =
        [Brush1, Brush2, Brush3, Brush4, Brush5, Brush6, Brush7];

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

    /// <summary>三张图的基准画布宽度（玩家反馈：纵向排列且左右对齐，避免相邻纵轴跨度极大）。</summary>
    private const int FrameWidth = 880;

    /// <summary>人口图的系列顺序（与 <see cref="PopulationSeries"/> 一一对应）。</summary>
    private static readonly (string Name, string BrushKey, LedgerCategory? Category)[] PopulationSeries =
    [
        ("出生", Brush3, LedgerCategory.ChildBorn),
        ("死亡", Brush1, LedgerCategory.MemberDeceased),
        ("购买", Brush2, LedgerCategory.PersonBought),
        ("入狱", Brush6, LedgerCategory.MemberImprisoned),
        ("举人", Brush4, null),
        ("贡士", Brush5, null),
        ("进士", Brush7, null),
    ];

    /// <summary>
    /// 按 Ledger 与功名变迁史逐月聚合。
    /// </summary>
    /// <param name="state">当前存档状态（只读，不会被修改）。</param>
    /// <param name="cumulativeMoney">收支与分类两组图取累计口径（玩家反馈 11 的切换按钮）。</param>
    /// <param name="cumulativePopulation">人口图取累计口径（玩家反馈 10 的切换按钮）。</param>
    /// <returns>固定顺序的三张折线图；无流水时是单张说明图。</returns>
    public static IReadOnlyList<ChartFrame> Build(
        GameState state, bool cumulativeMoney = false, bool cumulativePopulation = false)
    {
        ArgumentNullException.ThrowIfNull(state);

        var buckets = Buckets(state);

        if (buckets.Count == 0)
        {
            return
            [
                new ChartFrame(
                    "月度收支（暂无流水）",
                    UnitName,
                    "族中尚无流水账；先推进几个游戏月份，折线图会随 Ledger 自动生成。",
                    FrameWidth,
                    ChartHeight,
                    [],
                    [],
                    [],
                    []),
            ];
        }

        return
        [
            MoneyFrame(buckets, cumulativeMoney),
            CategoryFrame(buckets, cumulativeMoney),
            PopulationFrame(state, buckets, cumulativePopulation),
        ];
    }

    /// <summary>
    /// 统计页的「算好的数据」（玩家反馈 16）：把局势判断压成几行文字，供玩家一眼决策。
    /// </summary>
    /// <param name="state">当前存档状态（只读）。</param>
    /// <returns>多行中文结论；无流水时给出起步提示。</returns>
    public static string BuildInsights(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var buckets = Buckets(state);
        var lines = new List<string>();

        if (buckets.Count == 0)
        {
            return "尚无流水：推进几个月后，这里会给出月均收支、结构占比与人口/功名概览。";
        }

        var income = ToGuan(buckets.Select(bucket => bucket.IncomeWen).ToArray());
        var expense = ToGuan(buckets.Select(bucket => bucket.ExpenseWen).ToArray());
        var net = Difference(income, expense);
        var count = buckets.Count;
        var averageNet = net.Sum() / count;

        lines.Add(FormattableString.Invariant(
            $"月均：收入 {income.Sum() / count:0.##} 贯 ｜ 支出 {expense.Sum() / count:0.##} 贯 ｜ 结余 {averageNet:0.##} 贯 ｜ 覆盖 {count} 个月"));

        var lastNet = net[^1];
        var trend = net.Length >= 6
            ? net.TakeLast(6).Sum() / 6
            : averageNet;
        var trendNote = trend < 0 ? "（为负：正在吃本金或借债）" : "（为正：仍在积累）";
        lines.Add(FormattableString.Invariant(
            $"最近一月结余 {lastNet:0.##} 贯，近 6 月均值 {trend:0.##} 贯") + trendNote);

        // 收支结构：把全期各类别按大小取前几名，直接回答「钱从哪来、花在哪」。
        var byCategory = buckets
            .SelectMany(bucket => bucket.CategoryWen)
            .GroupBy(pair => pair.Key)
            .Select(group => (Category: group.Key, Total: group.Sum(pair => pair.Value)))
            .Where(item => item.Total != 0m)
            .OrderByDescending(item => Math.Abs(item.Total))
            .Take(5)
            .ToList();

        if (byCategory.Count > 0)
        {
            var parts = byCategory.Select(item => FormattableString.Invariant(
                $"{Display.Category(item.Category)} {item.Total / WenPerGuan:+0.##;-0.##;0}"));
            lines.Add("主要科目（全期合计）：" + string.Join(" ｜ ", parts));
        }

        var cash = state.Economy.Treasury.Cash.Guan;
        var savings = state.Economy.Treasury.Savings.Guan;
        var loan = state.Economy.Treasury.Loan.Principal.Guan;
        var burn = expense.Length > 0 ? expense.Average() : 0d;

        var support = burn > 0d
            ? FormattableString.Invariant($"；按当前支出速度，现银可支撑 {(double)cash / burn:0.#} 个月")
            : "；本月无支出";
        lines.Add(FormattableString.Invariant(
            $"现银 {cash:0.##} 贯 ｜ 储蓄 {savings:0.##} 贯 ｜ 欠本金 {loan:0.##} 贯") + support);

        var registered = state.Family.RegisteredMembers.Count;
        var degrees = PopulationCounts(state).Sum(pair => pair.Value);
        lines.Add(FormattableString.Invariant(
            $"在册 {registered} 人 ｜ 全期功名事件 {degrees} 次 ｜ 米价系数 {state.Economy.GrainPriceIndex.Value:0.###}"));

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>逐月聚合桶：月份、收入、支出与各类别净额（聚合期就地累加）。</summary>
    private sealed class Bucket(GameDate month)
    {
        /// <summary>该桶对应的月份。</summary>
        public GameDate Month { get; } = month;

        /// <summary>当月流入（文）。</summary>
        public decimal IncomeWen { get; set; }

        /// <summary>当月流出（文，取绝对值）。</summary>
        public decimal ExpenseWen { get; set; }

        /// <summary>当月各类别净额（文，正为流入）。</summary>
        public Dictionary<LedgerCategory, decimal> CategoryWen { get; } = [];
    }

    /// <summary>把一个状态摊成「有数据的月份」有序桶（流水 + 功名变迁的月份并集）。</summary>
    private static List<Bucket> Buckets(GameState state)
    {
        var entries = state.Economy.Ledger.Entries;
        var degreeMonths = state.Family.Members
            .SelectMany(person => person.DegreeHistory)
            .Select(record => record.ChangedAt);

        var months = entries.Select(entry => entry.Date)
            .Concat(degreeMonths)
            .Distinct()
            .OrderBy(date => date)
            .ToList();

        var buckets = months.Select(month => new Bucket(month)).ToList();
        var indexOf = new Dictionary<GameDate, int>(months.Count);
        for (var i = 0; i < months.Count; i++)
        {
            indexOf.Add(months[i], i);
        }

        foreach (var entry in entries)
        {
            var bucket = buckets[indexOf[entry.Date]];
            var amount = entry.Amount.Wen;

            if (amount > 0m)
            {
                bucket.IncomeWen += amount;
            }
            else
            {
                bucket.ExpenseWen -= amount;
            }

            bucket.CategoryWen.TryGetValue(entry.Category, out var running);
            bucket.CategoryWen[entry.Category] = running + amount;
        }

        return buckets;
    }

    /// <summary>第一张图：月（或累计）收入 / 支出 / 结余。</summary>
    private static ChartFrame MoneyFrame(List<Bucket> buckets, bool cumulative)
    {
        var income = ToGuan(buckets.Select(bucket => bucket.IncomeWen).ToArray());
        var expense = ToGuan(buckets.Select(bucket => bucket.ExpenseWen).ToArray());
        var net = Difference(income, expense);

        if (cumulative)
        {
            income = Cumulative(income);
            expense = Cumulative(expense);
            net = Cumulative(net);
        }

        var count = buckets.Count;
        var summary = cumulative
            ? FormattableString.Invariant(
                $"累计口径：累计收入 {income[^1]:0.##} 贯，累计支出 {expense[^1]:0.##} 贯，累计结余 {net[^1]:0.##} 贯。")
            : FormattableString.Invariant(
                $"当前口径：共 {count} 个月，月均收入 {income.Sum() / count:0.##} 贯，月均支出 {expense.Sum() / count:0.##} 贯，月均结余 {net.Sum() / count:0.##} 贯。");

        var prefix = cumulative ? "累计" : "月";

        return Frame(
            cumulative ? "累计收入 / 累计支出 / 累计结余" : "月收入 / 月支出 / 月结余",
            UnitName,
            summary,
            buckets,
            [
                new LineSpec($"{prefix}收入", Brush1, income),
                new LineSpec($"{prefix}支出", Brush2, expense),
                new LineSpec($"{prefix}结余", Brush3, net),
            ]);
    }

    /// <summary>第二张图：分类月度净额（或分类累计净额），取全期金额绝对值前 6 类。</summary>
    private static ChartFrame CategoryFrame(List<Bucket> buckets, bool cumulative)
    {
        var magnitudes = new Dictionary<LedgerCategory, decimal>();
        foreach (var bucket in buckets)
        {
            foreach (var pair in bucket.CategoryWen)
            {
                magnitudes.TryGetValue(pair.Key, out var running);
                magnitudes[pair.Key] = running + Math.Abs(pair.Value);
            }
        }

        var topCategories = magnitudes
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key)
            .Take(MaxCategorySeries)
            .Select(pair => pair.Key)
            .ToList();

        var series = new List<LineSpec>(topCategories.Count);
        for (var i = 0; i < topCategories.Count; i++)
        {
            var category = topCategories[i];
            var monthly = ToGuan(buckets
                .Select(bucket => bucket.CategoryWen.TryGetValue(category, out var value) ? value : 0m)
                .ToArray());

            series.Add(new LineSpec(
                Display.Category(category), BrushKeys[i], cumulative ? Cumulative(monthly) : monthly));
        }

        var scope = cumulative ? "各线是该类别净额的逐月累计。" : "各线是该类别当月净额。";
        var summary = FormattableString.Invariant(
            $"按全期金额绝对值取前 {topCategories.Count} 类（全期共 {magnitudes.Count} 类）；") + scope + "收入为正、支出为负。";

        return Frame(
            cumulative ? "分类累计（前 6 类）" : "分类月度（前 6 类）",
            UnitName,
            summary,
            buckets,
            series);
    }

    /// <summary>第三张图：人口事件的当月发生数（或累计数）。</summary>
    private static ChartFrame PopulationFrame(GameState state, List<Bucket> buckets, bool cumulative)
    {
        var counts = PopulationCounts(state);
        var caseCounts = LedgerEventCounts(state);

        var series = new List<LineSpec>(PopulationSeries.Length);
        for (var i = 0; i < PopulationSeries.Length; i++)
        {
            var (name, brushKey, category) = PopulationSeries[i];
            var monthly = new double[buckets.Count];

            for (var month = 0; month < buckets.Count; month++)
            {
                var key = buckets[month].Month;
                var value = category is { } ledgerCategory
                    ? (caseCounts.TryGetValue(ledgerCategory, out var events) && events.TryGetValue(key, out var hits)
                        ? hits
                        : 0)
                    : (counts.TryGetValue((name, key), out var degrees) ? degrees : 0);

                monthly[month] = value;
            }

            series.Add(new LineSpec(name, brushKey, cumulative ? Cumulative(monthly) : monthly));
        }

        var total = series.Sum(line => line.Values.Length > 0 ? line.Values[^1] : 0d);
        var summary = cumulative
            ? FormattableString.Invariant($"累计口径：七类事件逐月累计，末月合计 {total:0} 人/次。")
            : "当前口径：每类事件的当月发生数（出生 / 死亡 / 购买 / 入狱为账本事件，举人 / 贡士 / 进士取自功名变迁史）。";

        return Frame(
            cumulative ? "人口与功名（累计）" : "人口与功名（当月）",
            CountUnit,
            summary,
            buckets,
            series);
    }

    /// <summary>功名事件：键是（系列名，月份），值是当月发生数。</summary>
    private static Dictionary<(string Name, GameDate Month), double> PopulationCounts(GameState state)
    {
        var result = new Dictionary<(string, GameDate), double>();

        foreach (var person in state.Family.Members)
        {
            foreach (var record in person.DegreeHistory)
            {
                var name = record.Level switch
                {
                    DegreeLevel.JuRen => "举人",
                    DegreeLevel.GongShi => "贡士",
                    DegreeLevel.JinShi => "进士",
                    _ => null,
                };

                if (name is null)
                {
                    continue;
                }

                var key = (name, record.ChangedAt);
                result.TryGetValue(key, out var running);
                result[key] = running + 1d;
            }
        }

        return result;
    }

    /// <summary>账本事件类别的逐月发生数。</summary>
    private static Dictionary<LedgerCategory, Dictionary<GameDate, double>> LedgerEventCounts(GameState state)
    {
        var result = new Dictionary<LedgerCategory, Dictionary<GameDate, double>>();

        foreach (var entry in state.Economy.Ledger.Entries)
        {
            if (LedgerCategoryMetadata.KindOf(entry.Category) != LedgerEntryKind.Event)
            {
                continue;
            }

            if (!result.TryGetValue(entry.Category, out var byMonth))
            {
                byMonth = [];
                result.Add(entry.Category, byMonth);
            }

            byMonth.TryGetValue(entry.Date, out var running);
            byMonth[entry.Date] = running + 1d;
        }

        return result;
    }

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
                text.Add(FormattableString.Invariant($"{line.Name} {line.Values[i]:0.##} {unit}"));
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

    /// <summary>一张图的输入线：名称、画刷键与逐月值。</summary>
    private readonly record struct LineSpec(string Name, string BrushKey, double[] Values);
}
