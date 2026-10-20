using System.Windows.Media;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Economy;
using KFL.Rules.Exam;

namespace KFL.Presentation;

/// <summary>
/// 统计页的月度折线图数据：把家族流水账（<see cref="Ledger"/>）与功名变迁史按月聚合，
/// 生成「收支」「分类」「人口」三组图，供 WPF 视图直接绑定绘制。
/// </summary>
/// <remarks>
/// 2026-10-19 玩家反馈批次：①图例改中文；②每图带逐月悬停条；③收支/分类与人口各自
/// 支持「当前值 / 累计值」切换；④新增人口图（出生 / 死亡 / 收养 / 入狱 / 举人 / 贡士 / 进士）。
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
    private const string Brush8 = "ChartBrush8";

    private static readonly string[] BrushKeys =
        [Brush1, Brush2, Brush3, Brush4, Brush5, Brush6, Brush7, Brush8];

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
    /// <remarks>
    /// 七条「发生数」系列 + 末尾单独计算的「新增在世 / 在世人口」（见 <c>PopulationFrame</c>）。
    /// 出生严格取 <see cref="LedgerCategory.ChildBorn"/> 事件数：一次真实分娩一条，收养另记
    /// <see cref="LedgerCategory.PersonBought"/>，二者不混算。
    /// </remarks>
    private static readonly (string Name, string BrushKey, LedgerCategory? Category)[] PopulationSeries =
    [
        ("出生", Brush3, LedgerCategory.ChildBorn),
        ("死亡", Brush1, LedgerCategory.MemberDeceased),
        ("收养", Brush2, LedgerCategory.PersonBought),
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
    /// <returns>多行中文结论；开局尚无流水时仍给出人口 / 功名 / 科举等实时结构。</returns>
    public static string BuildInsights(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var buckets = Buckets(state);
        var lines = new List<string>();

        var cash = state.Economy.Treasury.Cash.Guan;
        var savings = state.Economy.Treasury.Savings.Guan;
        var loan = state.Economy.Treasury.Loan.Principal.Guan;
        var burn = 0d;

        if (buckets.Count == 0)
        {
            // 开局尚无流水：结构 / 科举 / 风险不依赖账本，仍要能看见，避免整页只剩一句提示。
            lines.Add("尚无流水：收支项会随推进出现；以下人口 / 功名 / 科举取自当前存档。");
        }
        else
        {
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

            burn = expense.Length > 0 ? expense.Average() : 0d;

            var support = burn > 0d
                ? FormattableString.Invariant($"；按当前支出速度，现银可支撑 {(double)cash / burn:0.#} 个月")
                : "；本月无支出";
            lines.Add(FormattableString.Invariant(
                $"现银 {cash:0.##} 贯 ｜ 储蓄 {savings:0.##} 贯 ｜ 欠本金 {loan:0.##} 贯") + support);

            var degrees = PopulationCounts(state).Sum(pair => pair.Value);
            lines.Add(FormattableString.Invariant(
                $"全期功名事件 {degrees} 次 ｜ 米价系数 {state.Economy.GrainPriceIndex.Value:0.###}"));
        }

        lines.Add(FormattableString.Invariant($"在册 {state.Family.RegisteredMembers.Count} 人"));
        AppendStructureInsights(state, lines, cash, savings, loan, burn);
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// 局势速览的「结构 / 科举 / 风险」段（需求 4：把需要玩家自己算的判断压成现成数字）。
    /// </summary>
    /// <remarks>
    /// 只做只读派生：人口结构取在册成员年龄与当前功名，人口增减取近 6 个自然月的账本事件，
    /// 科举窗口复用 <see cref="ExamRegistration"/>（不复制资格判定），风险提示按流动性、
    /// 负债率与人口趋势给阈值化结论。
    /// </remarks>
    private static void AppendStructureInsights(
        GameState state,
        List<string> lines,
        decimal cash,
        decimal savings,
        decimal loan,
        double burn)
    {
        var date = state.CurrentDate;
        var family = state.Family;
        var registered = family.RegisteredMembers;

        var children = registered.Count(person => person.AgeAt(date) < 14);
        var adults = registered.Count(person => person.AgeAt(date) is >= 14 and < 50);
        var elders = registered.Count(person => person.AgeAt(date) >= 50);
        lines.Add(FormattableString.Invariant(
            $"人口结构：幼年（<14）{children} 人 ｜ 青壮（14–49）{adults} 人 ｜ 老年（≥50）{elders} 人"));

        var events = LedgerEventCounts(state);
        var born = Recent(events, LedgerCategory.ChildBorn);
        var died = Recent(events, LedgerCategory.MemberDeceased);
        var adopted = Recent(events, LedgerCategory.PersonBought);
        var marriedOut = Recent(events, LedgerCategory.MemberMarriedOut);
        var populationNet = born + adopted - died - marriedOut;
        lines.Add(FormattableString.Invariant(
            $"近 6 月人口：出生 {born:0} ｜ 死亡 {died:0} ｜ 收养 {adopted:0} ｜ 外嫁 {marriedOut:0} ｜ 净增 {populationNet:+0;-0;0}"));

        int Degree(DegreeLevel level) => registered.Count(person => person.CurrentDegree == level);
        var officials = registered.Count(person => person.Rank is not null);
        var awaiting = registered.Count(person => person.Status.HasFlag(StatusFlag.AwaitingPost));
        var baiShen = Degree(DegreeLevel.BaiShen);
        var juRen = Degree(DegreeLevel.JuRen);
        var gongShi = Degree(DegreeLevel.GongShi);
        var jinShi = Degree(DegreeLevel.JinShi);
        lines.Add(FormattableString.Invariant(
            $"功名结构：白身 {baiShen} ｜ 举人 {juRen} ｜ 贡士 {gongShi} ｜ 进士 {jinShi} ｜ 现任官 {officials} ｜ 待阙 {awaiting}"));

        var monthsToWindow = MonthsToRegistrable(family, date);
        if (monthsToWindow == 0)
        {
            var count = registered.Count(person =>
                ExamRegistration.CanRegister(person, date) || ExamRegistration.SpecialTributeEligible(person, date));
            lines.Add(FormattableString.Invariant($"科举：本月是报名窗口，{count} 人可报名（点「快进到科举」会停在此类月份）"));
        }
        else if (monthsToWindow > 0)
        {
            var window = date;
            for (var i = 0; i < monthsToWindow; i++)
            {
                window = window.NextMonth();
            }

            lines.Add(FormattableString.Invariant(
                $"科举：最早可报名窗口 {window.Year} 年 {window.Month} 月（{monthsToWindow} 个月后）"));
        }
        else
        {
            lines.Add("科举：未来 20 年没有可报名窗口（族人年龄 / 身份不满足报考条件）");
        }

        var gross = cash + savings + loan;
        var debtRatio = gross > 0m ? loan / gross : 0m;
        var warnings = new List<string>();
        if (burn > 0d && cash / (decimal)burn < 6m)
        {
            warnings.Add(FormattableString.Invariant($"现银仅够 {cash / (decimal)burn:0.#} 个月"));
        }

        if (debtRatio > 0.5m)
        {
            warnings.Add(FormattableString.Invariant($"负债率 {debtRatio * 100m:0}%"));
        }

        if (populationNet < 0)
        {
            warnings.Add("近 6 月人口净减");
        }

        if (monthsToWindow > 6)
        {
            warnings.Add(FormattableString.Invariant($"距可报名窗口 {monthsToWindow} 个月"));
        }

        lines.Add("风险提示：" + (warnings.Count > 0 ? string.Join(" ｜ ", warnings) : "未发现明显风险"));

        double Recent(Dictionary<LedgerCategory, Dictionary<GameDate, double>> counts, LedgerCategory category) =>
            counts.TryGetValue(category, out var byMonth)
                ? byMonth.Where(pair => date.ElapsedMonths(pair.Key) is >= 0 and <= 5).Sum(pair => pair.Value)
                : 0d;
    }

    /// <summary>从 <paramref name="from"/> 起，最早在几个月后出现「家里有人可报名」的窗口；20 年内没有返回 -1。</summary>
    private static int MonthsToRegistrable(Family family, GameDate from)
    {
        var probe = from;
        for (var months = 0; months <= 240; months++)
        {
            if (ExamRegistration.HasRegistrable(family, probe))
            {
                return months;
            }

            probe = probe.NextMonth();
        }

        return -1;
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
