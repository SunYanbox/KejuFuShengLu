namespace KFL.Presentation;

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
