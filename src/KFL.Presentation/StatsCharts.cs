using System.Windows.Media;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Presentation;

/// <summary>
/// 统计页的月度折线图数据：把家族流水账（<see cref="Ledger"/>）与功名变迁史按月聚合，
/// 生成「收支」「分类」「人口」三组图，供 WPF 视图直接绑定绘制。
/// </summary>
/// <remarks>
/// 2026-10-19 玩家反馈批次：①图例改中文；②每图带逐月悬停条；③收支/分类与人口各自
/// 支持「当前值 / 累计值」切换；④新增人口图（出生 / 死亡 / 购买 / 入狱 / 举人 / 贡士 / 进士）。
/// </remarks>
public static partial class StatsCharts
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
}
