using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KFL.Presentation.ViewModels;

namespace KFL.App;

/// <summary>
/// 主窗口：状态栏 / 四页签 / 纪事 / 各遮罩层（规格书 §12、§13）。
/// 界面本身拆在 <c>Views/</c> 与 <c>Controls/</c> 下，这里只保留窗口级行为：
/// 调试控制台的激活序列 S-U-N-T-I-O-N 与反引号开关（<see cref="OnPreviewKeyDown"/>）、
/// 退出存档，以及自检钩子的挂接（见 <c>MainWindow.Autostart.cs</c>）。
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

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key,
        };

        if (key is Key.Oem3 or Key.OemTilde)
        {
            // 已激活后反引号是全局开关：焦点在输入框（含控制台自身）里也必须生效。
            if (_consoleArmed && !e.IsRepeat)
            {
                _viewModel.ToggleConsole();
                e.Handled = true;
            }

            return;
        }

        var pressed = KeyToLetter(key);
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
