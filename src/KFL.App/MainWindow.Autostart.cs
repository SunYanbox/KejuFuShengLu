using System.Globalization;
using System.Windows;

namespace KFL.App;

/// <summary>
/// 主窗口的无人工介入自检钩子：在窗口加载后按环境变量 <c>KFL_AUTOSTART</c> 把界面推进到
/// 游戏内状态，供截图 / 回归脚本使用（正常启动无副作用）。
/// </summary>
public partial class MainWindow
{
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
}
