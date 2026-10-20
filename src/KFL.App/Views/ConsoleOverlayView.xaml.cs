using KFL.Presentation.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace KFL.App.Views;

/// <summary>调试控制台浮窗（反引号 ` 开关）。</summary>
public partial class ConsoleOverlayView : UserControl
{
    public ConsoleOverlayView()
    {
        InitializeComponent();
    }

    private void OnCommandInputPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Up:
                viewModel.ConsoleHistoryUp();
                break;
            case Key.Down:
                viewModel.ConsoleHistoryDown();
                break;
            default:
                return;
        }

        e.Handled = true;
        // 绑定目标在属性变更后才刷新，故把光标移到行尾放到输入队列之后。
        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() =>
            {
                CommandInput.CaretIndex = CommandInput.Text.Length;
                CommandInput.ScrollToEnd();
            }));
    }
}
