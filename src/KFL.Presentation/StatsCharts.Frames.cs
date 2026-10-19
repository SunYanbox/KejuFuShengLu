using System.Windows.Media;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Presentation;

public static partial class StatsCharts
{
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
}
