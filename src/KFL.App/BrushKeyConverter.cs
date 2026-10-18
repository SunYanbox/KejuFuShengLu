using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace KFL.App;

/// <summary>把资源键名（例如 <c>ChartBrush1</c>）解析成画笔，供图表折线着色。</summary>
public sealed class BrushKeyConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string key && Application.Current is { } app && app.TryFindResource(key) is Brush brush)
        {
            return brush;
        }

        return Brushes.Gray;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
