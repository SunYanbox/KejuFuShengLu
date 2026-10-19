using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KFL.Presentation.ViewModels;

namespace KFL.App.Views;

/// <summary>
/// 纪事（家族流水）面板：新行入列后自动滚到最新（玩家反馈：申请生育等即时反馈「显示不太及时」——
/// 新行其实已入列，只是停在列表下沿之外，需手动滚动才看得到）。合并多次增量，避免逐行滚动。
/// </summary>
public partial class ChronicleLogView : UserControl
{
    private INotifyCollectionChanged? _observed;
    private bool _scrollPending;

    public ChronicleLogView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => Detach();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();
        if (e.NewValue is MainViewModel viewModel)
        {
            _observed = viewModel.LogLines;
            _observed.CollectionChanged += OnLogLinesChanged;
        }
    }

    private void Detach()
    {
        if (_observed is null)
        {
            return;
        }

        _observed.CollectionChanged -= OnLogLinesChanged;
        _observed = null;
    }

    private void OnLogLinesChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScrollToEnd();

    /// <summary>把纪事列表滚到最后一行（多次增行合并为一次布局后的滚动）。</summary>
    private void ScrollToEnd()
    {
        if (_scrollPending)
        {
            return;
        }

        _scrollPending = true;
        Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() =>
            {
                _scrollPending = false;
                if (LogList.Items.Count > 0)
                {
                    LogList.ScrollIntoView(LogList.Items[^1]);
                }
            }));
    }
}
