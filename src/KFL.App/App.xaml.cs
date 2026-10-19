using System.Windows;
using KFL.App.Diagnostics;

namespace KFL.App;

/// <summary>
/// 应用入口。置环境变量 <c>KFL_SMOKE=&lt;输出文件&gt;</c> 可跑一次无界面冒烟（开局 → 推进 24 月 →
/// 存档/买田/导出），用于受限宿主下的自检；正常启动无副作用。冒烟脚本见
/// <see cref="SmokeRunner"/>，未处理异常的落盘与弹窗见 <see cref="CrashReporter"/>。
/// </summary>
/// <remarks>
/// 未处理异常一律落盘 <c>crash-*.log</c>（exe 相对目录）并弹窗提示，避免「闪退无痕」——
/// 受限宿主里 WPF 的某些控件会因临时目录不可写而在布局期抛异常。
/// </remarks>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (Environment.GetEnvironmentVariable("KFL_SMOKE") is { Length: > 0 } smokePath)
        {
            SmokeRunner.Run(smokePath);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            CrashReporter.Report(args.Exception);
            // 单次渲染/交互失败不应该带走整局游戏：日志落盘后让 dispatcher 继续跑。
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => CrashReporter.Report(args.ExceptionObject as Exception);

        base.OnStartup(e);
    }
}
