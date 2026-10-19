namespace KFL.Presentation;

/// <summary>一条刻度：<see cref="Position"/> 是画布像素坐标（X 轴为 x 像素、Y 轴为 y 像素）。</summary>
public sealed class ChartTick(double position, string text)
{
    /// <summary>刻度在画布上的像素位置。</summary>
    public double Position { get; } = position;

    /// <summary>刻度文字。</summary>
    public string Text { get; } = text;
}
