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

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Closing += (_, _) => _viewModel.SaveOnExit();
        Loaded += OnLoaded;
    }

    /// <summary>
    /// 开发自检钩子（与 <c>KFL_SMOKE</c> 同源，正常启动无副作用）：置环境变量
    /// <c>KFL_AUTOSTART</c> 为月数则直接开局并快进该月数；为 <c>console</c> 则另开调试控制台浮窗。
    /// 供截图/回归脚本在无人工点击的情况下进入游戏内状态。
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Environment.GetEnvironmentVariable("KFL_AUTOSTART") is not { Length: > 0 } mode)
        {
            return;
        }

        _viewModel.SelectedOrigin = _viewModel.Origins[0];
        _viewModel.SelectedDifficulty = _viewModel.Difficulties[1];
        _viewModel.StartGameCommand.Execute(null);

        if (mode.Equals("console", StringComparison.OrdinalIgnoreCase))
        {
            _viewModel.ActivateConsole();
            _viewModel.ToggleConsole();
            return;
        }

        if (int.TryParse(mode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var months) && months > 0)
        {
            _viewModel.DoCommand.Execute($"ff:{months}");
        }
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
