using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KFL.Presentation.ViewModels;

namespace KFL.App;

/// <summary>
/// 主窗口：开局菜单 / 家族卡片墙 / 动作面板 / 纪事 / 调试控制台浮窗（规格书 §12、§13）。
/// 调试控制台的激活序列 S-U-N-T-I-O-N 与反引号开关在 <see cref="OnPreviewKeyDown"/> 里处理。
/// </summary>
public partial class MainWindow : Window
{
    private const string ActivationSequence = "suntion";

    private static readonly TimeSpan ActivationWindow = TimeSpan.FromSeconds(2);

    private readonly MainViewModel _viewModel = new();

    private string _typed = string.Empty;
    private DateTime _typedAt = DateTime.MinValue;
    private bool _consoleArmed;
    private bool _logScrollPending;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        // 纪事自动滚到最新（玩家反馈：申请生育等即时反馈「显示不太及时」——新行其实已入列，
        // 只是停在列表下沿之外，需手动滚动才看得到）。合并多次增量，避免逐行滚动。
        _viewModel.LogLines.CollectionChanged += (_, _) => ScrollLogToEnd();

        Closing += (_, _) => _viewModel.SaveOnExit();
        Loaded += OnLoaded;
    }

    /// <summary>把纪事列表滚到最后一行（多次增行合并为一次布局后的滚动）。</summary>
    private void ScrollLogToEnd()
    {
        if (_logScrollPending)
        {
            return;
        }

        _logScrollPending = true;
        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            new Action(() =>
            {
                _logScrollPending = false;
                if (LogList.Items.Count > 0)
                {
                    LogList.ScrollIntoView(LogList.Items[^1]);
                }
            }));
    }

    /// <summary>
    /// 开发自检钩子（与 <c>KFL_SMOKE</c> 同源，正常启动无副作用）：置环境变量
    /// <c>KFL_AUTOSTART</c> 为分号分隔的指令串——<c>play:月数[:出身]</c> 同步推进、
    /// <c>console</c> 直接打开调试控制台浮窗、纯月数则按定时器快进。
    /// 供截图/回归脚本在无人工点击的情况下进入游戏内状态。
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Environment.GetEnvironmentVariable("KFL_AUTOSTART") is not { Length: > 0 } mode)
        {
            return;
        }

        var tokens = mode.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!tokens.Any(token => token.StartsWith("play:", StringComparison.OrdinalIgnoreCase)))
        {
            _viewModel.SelectedOrigin = _viewModel.Origins[0];
            _viewModel.SelectedDifficulty = _viewModel.Difficulties[1];
            _viewModel.StartGameCommand.Execute(null);
        }

        foreach (var token in tokens)
        {
            if (token.StartsWith("play:", StringComparison.OrdinalIgnoreCase))
            {
                _viewModel.AutoPlay(token[5..]);
            }
            else if (token.StartsWith("cmd:", StringComparison.OrdinalIgnoreCase))
            {
                // 截图/回归脚本用：直接执行一个界面命令（如 cmd:menu 回主菜单验证存档列表）。
                _viewModel.DoCommand.Execute(token[4..]);
            }
            else if (token.Equals("console", StringComparison.OrdinalIgnoreCase))
            {
                _viewModel.ActivateConsole();
                if (!_viewModel.ConsoleVisible)
                {
                    _viewModel.ToggleConsole();
                }
            }
            else if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var months) && months > 0)
            {
                _viewModel.DoCommand.Execute($"ff:{months}");
            }
        }
    }

    /// <summary>删除存档前的二次确认（玩家反馈 9）。</summary>
    private void OnDeleteSaveClick(object sender, RoutedEventArgs e)
    {
        var name = _viewModel.SelectedSave?.Text ?? "（未选中）";
        if (MessageBox.Show(
                $"确定删除存档「{name}」吗？\n\n删除后无法恢复（备份存档不受影响）。",
                "删除存档",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning,
                MessageBoxResult.Cancel) != MessageBoxResult.OK)
        {
            return;
        }

        _viewModel.DoCommand.Execute("delete");
    }

    /// <summary>删除备份前的二次确认（玩家反馈 9）。</summary>
    private void OnDeleteBackupClick(object sender, RoutedEventArgs e)
    {
        var name = _viewModel.SelectedBackup?.Text ?? "（未选中）";
        if (MessageBox.Show(
                $"确定删除备份「{name}」吗？\n\n删除后无法恢复（主存档不受影响）。",
                "删除备份",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning,
                MessageBoxResult.Cancel) != MessageBoxResult.OK)
        {
            return;
        }

        _viewModel.DoCommand.Execute("deletebackup");
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

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Oem3 or Key.OemTilde)
        {
            if (_consoleArmed && !IsTextInputFocused())
            {
                _viewModel.ToggleConsole();
                e.Handled = true;
            }

            return;
        }

        var pressed = KeyToLetter(e.Key);
        if (pressed is null)
        {
            return;
        }

        if (IsTextInputFocused() && !_consoleArmed)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (now - _typedAt > ActivationWindow)
        {
            _typed = string.Empty;
            _consoleArmed = false;
        }

        _typedAt = now;
        _typed += pressed;

        if (ActivationSequence.StartsWith(_typed, StringComparison.Ordinal))
        {
            if (_typed.Length == ActivationSequence.Length)
            {
                _typed = string.Empty;
                _consoleArmed = true;
                _viewModel.ActivateConsole();
            }

            return;
        }

        _typed = pressed == "s" ? "s" : string.Empty;
    }

    private static bool IsTextInputFocused()
        => Keyboard.FocusedElement switch
        {
            TextBox or PasswordBox or RichTextBox => true,
            ComboBox { IsEditable: true } => true,
            _ => false,
        };

    private static string? KeyToLetter(Key key)
        => key is >= Key.A and <= Key.Z
            ? ((char)('a' + ((int)key - (int)Key.A))).ToString()
            : null;
}
