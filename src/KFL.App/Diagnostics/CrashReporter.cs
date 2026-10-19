using System.IO;
using System.Windows;

namespace KFL.App.Diagnostics;

/// <summary>
/// 未处理异常的兜底处置：落盘 <c>crash-yyyyMMdd-HHmmss.log</c>（exe 相对目录）并弹窗提示，
/// 避免「闪退无痕」——受限宿主里 WPF 的某些控件会因临时目录不可写而在布局期抛异常。
/// </summary>
internal static class CrashReporter
{
    public static void Report(Exception? exception)
    {
        var text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}{exception}";
        var path = Path.Combine(
            AppContext.BaseDirectory,
            $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");

        try
        {
            File.WriteAllText(path, text);
        }
        catch (IOException)
        {
            path = "(日志写入失败)";
        }
        catch (UnauthorizedAccessException)
        {
            path = "(日志写入被拒)";
        }

        MessageBox.Show(
            $"{exception?.Message}{Environment.NewLine}{Environment.NewLine}诊断日志：{path}",
            "科举浮生录 · 运行时错误",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
