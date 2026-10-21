namespace KFL.Presentation;

/// <summary>统计页顶部概览区：最新有数据月份的指标卡与同比 / 环比说明。</summary>
public sealed class StatsOverview
{
    public StatsOverview(string caption, IReadOnlyList<StatsOverviewCard> cards)
    {
        Caption = caption;
        Cards = cards;
    }

    /// <summary>概览标题行（截至月份、同比 / 环比口径、无数据提示）。</summary>
    public string Caption { get; }

    /// <summary>概览指标卡，顺序固定为收入 / 支出 / 结余 / 在册人口 / 出生 / 死亡。</summary>
    public IReadOnlyList<StatsOverviewCard> Cards { get; }
}

/// <summary>一张概览指标卡：当前值、单位与同比 / 环比增长率文案。</summary>
public sealed class StatsOverviewCard
{
    public StatsOverviewCard(
        string title,
        string value,
        string unit,
        string yearOverYear,
        string monthOverMonth)
    {
        Title = title;
        Value = value;
        Unit = unit;
        YearOverYear = yearOverYear;
        MonthOverMonth = monthOverMonth;
    }

    /// <summary>指标名，如「月收入」。</summary>
    public string Title { get; }

    /// <summary>最新有数据月份的数值；不可用时为「—」。</summary>
    public string Value { get; }

    /// <summary>单位，如「贯」「人」。</summary>
    public string Unit { get; }

    /// <summary>同比文案，如「同比 +13.12%」；不可计算为「同比 —」。</summary>
    public string YearOverYear { get; }

    /// <summary>环比文案，如「环比 -4.73%」；不可计算为「环比 —」。</summary>
    public string MonthOverMonth { get; }
}
