using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KFL.App.Views;

/// <summary>
/// 家族页：家谱 / 成员详情 / 动作面板。
/// </summary>
/// <remarks>
/// 家谱视图的**缩放与平移**在此实现（玩家反馈 9）：
/// ①滚轮以指针为锚点缩放；②按住左键拖动平移；③「重置视图」恢复 100% 与左上角；
/// ④平移由 <see cref="ScrollViewer"/> 自身钳制，因此**不会离开已显示卡片的范围**。
/// 缩放用 <see cref="System.Windows.Media.ScaleTransform"/> 作为 **LayoutTransform**，
/// 使滚动条的 Extent 随缩放同步变化（否则滚动条范围会与实际内容脱节）。
/// </remarks>
public partial class FamilyTabView : UserControl
{
    /// <summary>缩放下界（再小就看不清卡片文字）。</summary>
    private const double MinZoom = 0.4d;

    /// <summary>缩放上界。</summary>
    private const double MaxZoom = 2.5d;

    /// <summary>每次缩放的比例步长。</summary>
    private const double ZoomStep = 1.15d;

    private bool _dragging;
    private Point _dragOrigin;
    private double _dragOffsetX;
    private double _dragOffsetY;

    public FamilyTabView()
    {
        InitializeComponent();
    }

    private double Zoom => TreeScale.ScaleX;

    private void OnZoomIn(object sender, RoutedEventArgs e) => ApplyZoom(Zoom * ZoomStep, anchor: null);

    private void OnZoomOut(object sender, RoutedEventArgs e) => ApplyZoom(Zoom / ZoomStep, anchor: null);

    /// <summary>恢复到默认缩放（100%）与左上角位置。</summary>
    private void OnResetTreeView(object sender, RoutedEventArgs e)
    {
        TreeScale.ScaleX = 1d;
        TreeScale.ScaleY = 1d;
        ZoomText.Text = "100%";
        TreeScroll.UpdateLayout();
        TreeScroll.ScrollToHorizontalOffset(0d);
        TreeScroll.ScrollToVerticalOffset(0d);
    }

    private void OnTreeWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        ApplyZoom(
            e.Delta > 0 ? Zoom * ZoomStep : Zoom / ZoomStep,
            e.GetPosition(TreeScroll));
    }

    /// <summary>
    /// 以 <paramref name="anchor"/>（视口坐标）为锚点缩放：锚点下的内容点在缩放前后保持不动。
    /// 锚点为空时取视口中心（供「＋ / －」按钮使用）。
    /// </summary>
    private void ApplyZoom(double target, Point? anchor)
    {
        var scale = Math.Clamp(target, MinZoom, MaxZoom);
        var previous = Zoom;
        if (Math.Abs(scale - previous) < 0.0001d)
        {
            return;
        }

        var focus = anchor ?? new Point(TreeScroll.ViewportWidth / 2d, TreeScroll.ViewportHeight / 2d);
        var contentX = (TreeScroll.HorizontalOffset + focus.X) / previous;
        var contentY = (TreeScroll.VerticalOffset + focus.Y) / previous;

        TreeScale.ScaleX = scale;
        TreeScale.ScaleY = scale;
        ZoomText.Text = $"{scale * 100d:0}%";

        // 先让布局按新比例跑一遍，再按「内容点不动」的等式回填偏移量。
        TreeScroll.UpdateLayout();
        TreeScroll.ScrollToHorizontalOffset((contentX * scale) - focus.X);
        TreeScroll.ScrollToVerticalOffset((contentY * scale) - focus.Y);
    }

    private void OnTreeDragStart(object sender, MouseButtonEventArgs e)
    {
        // 卡片里的「选中」按钮、滚动条与下拉框要照常操作，故只在非交互控件上起拖。
        if (e.OriginalSource is DependencyObject source
            && (FindAncestor<Button>(source) is not null
                || FindAncestor<System.Windows.Controls.Primitives.ScrollBar>(source) is not null
                || FindAncestor<ComboBox>(source) is not null))
        {
            return;
        }

        _dragging = true;
        _dragOrigin = e.GetPosition(TreeScroll);
        _dragOffsetX = TreeScroll.HorizontalOffset;
        _dragOffsetY = TreeScroll.VerticalOffset;
        _ = TreeScroll.CaptureMouse();
        Cursor = Cursors.SizeAll;
    }

    private void OnTreeDragMove(object sender, MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var current = e.GetPosition(TreeScroll);
        TreeScroll.ScrollToHorizontalOffset(_dragOffsetX - (current.X - _dragOrigin.X));
        TreeScroll.ScrollToVerticalOffset(_dragOffsetY - (current.Y - _dragOrigin.Y));
    }

    private void OnTreeDragEnd(object sender, RoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        TreeScroll.ReleaseMouseCapture();
        Cursor = Cursors.Arrow;
    }

    private static T? FindAncestor<T>(DependencyObject source)
        where T : DependencyObject
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }
}
