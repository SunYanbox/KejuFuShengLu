namespace KFL.Presentation;

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
