using System.Windows;
using System.Windows.Controls;

namespace KFL.App.Controls;

/// <summary>
/// 统计页一张折线图卡片（DataContext 为 <c>ChartFrame</c>）。
/// </summary>
public partial class StatChartCard : UserControl
{
    public StatChartCard()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 统计页每张图的横向滚动条（玩家反馈 6）：内容宽度变化或首次加载后一律滚到最右，
    /// 直接看到最新月份；下一次重绘同样回到最右，不记忆上次位置。
    /// </summary>
    private void OnChartScrollLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ScrollViewer viewer)
        {
            return;
        }

        viewer.ScrollToRightEnd();
        viewer.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Loaded,
            new Action(viewer.ScrollToRightEnd));
    }
}
