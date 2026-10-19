using System.Windows.Media;

namespace KFL.Presentation;

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
