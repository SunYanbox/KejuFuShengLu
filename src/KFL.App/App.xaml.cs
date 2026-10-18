using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using KFL.Presentation.ViewModels;

namespace KFL.App;

/// <summary>
/// 应用入口。置环境变量 <c>KFL_SMOKE=&lt;输出文件&gt;</c> 可跑一次无界面冒烟（开局 → 推进 24 月 →
/// 存档/买田/导出），用于受限宿主下的自检；正常启动无副作用。
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
            RunSmoke(smokePath);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            ReportCrash(args.Exception);
            // 单次渲染/交互失败不应该带走整局游戏：日志落盘后让 dispatcher 继续跑。
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => ReportCrash(args.ExceptionObject as Exception);

        base.OnStartup(e);
    }

    private static void ReportCrash(Exception? exception)
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

    private static void RunSmoke(string path)
    {
        var log = new List<string>();
        try
        {
            var vm = new MainViewModel();
            vm.SelectedOrigin = vm.Origins[0];
            vm.SelectedDifficulty = vm.Difficulties[1];

            // 可选：按 spec 里出身的中文名选开局（默认农户），便于长跑覆盖科举/授官链路。
            if (Environment.GetEnvironmentVariable("KFL_SMOKE_ORIGIN") is { Length: > 0 } originName)
            {
                var picked = vm.Origins.FirstOrDefault(option => option.Text.StartsWith(originName, StringComparison.Ordinal));
                if (picked is not null)
                {
                    vm.SelectedOrigin = picked;
                }
            }

            vm.StartGameCommand.Execute(null);

            // 可选：开启「持续报考」，让考试月自动报名（覆盖解试/省试/殿试与授官链路）。
            if (Environment.GetEnvironmentVariable("KFL_SMOKE_AUTOEXAM") is { Length: > 0 })
            {
                vm.DoCommand.Execute("autoexam");
            }

            log.Add($"开局：{vm.StatusLine}");

            var months = int.TryParse(
                Environment.GetEnvironmentVariable("KFL_SMOKE_MONTHS"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed) && parsed > 0
                ? parsed
                : 24;

            for (var i = 0; i < months; i++)
            {
                vm.DoCommand.Execute("advance");
            }

            log.Add($"推进 {months} 月后：{vm.StatusLine}");
            log.Add($"成员卡 {vm.Members.Count} 张，归档 {vm.Archived.Count} 张，详情 {vm.DetailText.Length} 字");
            log.Add($"纪事 {vm.LogLines.Count} 行；末行：{(vm.LogLines.Count > 0 ? vm.LogLines[^1] : "-")}");
            log.Add($"考试条：{vm.ExamInfo}");
            log.Add($"成就条：{vm.AchievementSummary}");
            log.Add($"统计 {vm.StatisticsText.Length} 字；图表 {vm.Charts.Count} 张");
            log.Add($"成员筛选条：{vm.MemberCountText}");
            foreach (var filter in vm.MemberFilters)
            {
                vm.SelectedMemberFilter = filter;
                log.Add($"  筛选「{filter.Text}」→ 列表 {vm.Members.Count} 行");
            }

            vm.SelectedMemberFilter = vm.MemberFilters[0];
            log.Add($"本机存档槽 {vm.Saves.Count} 个");
            if (vm.Saves.Count > 0)
            {
                vm.SelectedSave = vm.Saves[0];
                log.Add("存档概览：" + vm.SaveOverviewText);
            }

            log.Add($"成就样例：{vm.Achievements[0].Code} {vm.Achievements[0].Name} ｜条件 {vm.Achievements[0].Description} ｜达成 {vm.Achievements[0].Detail}");
            var completed = vm.Achievements.FirstOrDefault(row => row.Completed);
            log.Add($"首个已达成：{completed?.Code} {completed?.Name} ｜{completed?.Detail}");

            vm.DoCommand.Execute("assetbuy:10");
            log.Add($"买田 10 亩后末行：{LastLog(vm)}");
            vm.DoCommand.Execute("money:savings:10");
            log.Add($"存 10 贯后末行：{LastLog(vm)}");
            vm.DoCommand.Execute("living:Comfortable");
            vm.DoCommand.Execute("advance");
            log.Add($"档位切换后末行：{LastLog(vm)}");
            vm.DoCommand.Execute("export");
            log.Add($"导出后末行：{LastLog(vm)}");
            vm.DoCommand.Execute("save");
            log.Add($"存档后末行：{LastLog(vm)}");
            log.Add($"存档槽 {vm.Saves.Count} 个；当前名 {vm.SaveName}");
            vm.SelectedSaveFormat = vm.SaveFormats[1];
            log.Add($"切 JSON 后末行：{LastLog(vm)}");
            vm.DoCommand.Execute("advance");
            log.Add($"JSON 档自动存档后末行：{LastLog(vm)}；存档槽 {vm.Saves.Count} 个");
            vm.DoCommand.Execute("console");
            log.Add($"控制台可见={vm.ConsoleVisible}，输出 {vm.ConsoleOutput.Length} 字");
            vm.ConsoleInput = "difficulty-list";
            vm.ConsoleRunCommand.Execute(null);
            log.Add($"控制台输出末段：{vm.ConsoleOutput[^Math.Min(120, vm.ConsoleOutput.Length)..]}");
            vm.DoCommand.Execute("menu");
            log.Add($"回主菜单后 IsMenuVisible={vm.IsMenuVisible}");
            log.Add("SMOKE OK");
        }
        catch (Exception ex)
        {
            log.Add("SMOKE FAILED: " + ex);
        }

        File.WriteAllLines(path, log);
    }

    private static string LastLog(MainViewModel vm) => vm.LogLines.Count > 0 ? vm.LogLines[^1] : "-";
}
