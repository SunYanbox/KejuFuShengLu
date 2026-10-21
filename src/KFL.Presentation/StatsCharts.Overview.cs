using System.Globalization;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Presentation;

public static partial class StatsCharts
{
    /// <summary>
    /// 统计页顶部概览：取**最新有流水 / 功名数据的月份**，给出收入、支出、结余、
    /// 在册人口、出生、死亡的当前值与同比 / 环比增长率。
    /// </summary>
    /// <remarks>
    /// 不取 <see cref="GameState.CurrentDate"/>：当前月可能尚未结算，把未结算的 0 当成当月值
    /// 会伪造出一个巨大的负增长。取最新桶与图表 / 悬停的最后一列保持同一口径。
    /// </remarks>
    public static StatsOverview BuildOverview(GameState state)
    {
        var buckets = Buckets(state);
        var caseCounts = LedgerEventCounts(state);

        if (buckets.Count == 0)
        {
            var registered = state.Family.RegisteredMembers.Count;
            return new StatsOverview(
                "尚无流水：推进月份后收支项会出现；同比 / 环比在基期可用后显示，基期为 0 时显示「—」。",
                [
                    Unavailable("月收入", UnitName),
                    Unavailable("月支出", UnitName),
                    Unavailable("月结余", UnitName),
                    new StatsOverviewCard(
                        "在册人口",
                        registered.ToString(CultureInfo.InvariantCulture),
                        CountUnit,
                        "同比 —",
                        "环比 —"),
                    Unavailable("本月出生", CountUnit),
                    Unavailable("本月死亡", CountUnit),
                ]);
        }

        var months = buckets.Select(bucket => bucket.Month).ToList();
        var income = ToGuan(buckets.Select(bucket => bucket.IncomeWen).ToArray());
        var expense = ToGuan(buckets.Select(bucket => bucket.ExpenseWen).ToArray());
        var net = Difference(income, expense);
        var births = EventCounts(buckets, caseCounts, LedgerCategory.ChildBorn);
        var deaths = EventCounts(buckets, caseCounts, LedgerCategory.MemberDeceased);
        var (alive, _) = LivingPopulation(state, buckets, caseCounts);

        var index = buckets.Count - 1;
        var month = months[index];
        var caption = FormattableString.Invariant(
            $"截至 {month.Year} 年 {month.Month} 月 · 当月口径 ｜ 同比 = 与去年同月相比；环比 = 与上月相比；基期为 0 或历史不足时显示「—」。");

        StatsOverviewCard Card(
            string title, string value, string unit, double[] values, bool carryForward)
        {
            var current = values[index];
            var yearOverYear = BaselineAt(months, values, MonthOffset(month, -12), carryForward);
            var monthOverMonth = BaselineAt(months, values, MonthOffset(month, -1), carryForward);
            return new StatsOverviewCard(
                title,
                value,
                unit,
                $"同比 {FormatRate(Rate(current, yearOverYear))}",
                $"环比 {FormatRate(Rate(current, monthOverMonth))}");
        }

        return new StatsOverview(
            caption,
            [
                Card("月收入", Amount(income[index]), UnitName, income, carryForward: false),
                Card("月支出", Amount(expense[index]), UnitName, expense, carryForward: false),
                Card("月结余", Signed(net[index]), UnitName, net, carryForward: false),
                Card("在册人口", Count(alive[index]), CountUnit, alive, carryForward: true),
                Card("本月出生", Count(births[index]), CountUnit, births, carryForward: false),
                Card("本月死亡", Count(deaths[index]), CountUnit, deaths, carryForward: false),
            ]);
    }

    /// <summary>某事件类别逐桶的发生数（缺失月份为 0）。</summary>
    private static double[] EventCounts(
        List<Bucket> buckets,
        Dictionary<LedgerCategory, Dictionary<GameDate, double>> caseCounts,
        LedgerCategory category)
    {
        var result = new double[buckets.Count];
        if (!caseCounts.TryGetValue(category, out var byMonth))
        {
            return result;
        }

        for (var i = 0; i < buckets.Count; i++)
        {
            if (byMonth.TryGetValue(buckets[i].Month, out var value))
            {
                result[i] = value;
            }
        }

        return result;
    }

    private static StatsOverviewCard Unavailable(string title, string unit) =>
        new(title, "—", unit, "同比 —", "环比 —");

    private static string Amount(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Signed(double value) => value.ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture);

    private static string Count(double value) => value.ToString("0", CultureInfo.InvariantCulture);
}
