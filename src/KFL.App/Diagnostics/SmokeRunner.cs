using System.Globalization;
using System.IO;
using System.Linq;
using KFL.Presentation.ViewModels;

namespace KFL.App.Diagnostics;

/// <summary>
/// 无界面冒烟脚本（<c>KFL_SMOKE=&lt;输出文件&gt;</c>）：开局 → 推进数月 → 筛选/存档/买田/切格式/导出，
/// 逐项把界面模型的关键文本写入文件，供受限宿主下自检。
/// </summary>
internal static class SmokeRunner
{
    public static void Run(string path)
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
            log.Add($"统计概览 {vm.OverviewCards.Count} 卡：{string.Join(" ｜ ", vm.OverviewCards.Select(card => $"{card.Title} {card.Value} {card.Unit}（{card.YearOverYear}；{card.MonthOverMonth}）"))}");
            var hovers = vm.Charts.SelectMany(frame => frame.Hovers).ToList();
            if (vm.OverviewCards.Count != 6)
            {
                throw new InvalidOperationException($"统计概览应为 6 卡，实际 {vm.OverviewCards.Count}");
            }

            if (hovers.Count == 0
                || hovers.Any(hover =>
                    !hover.Text.Contains("同比", StringComparison.Ordinal)
                    || !hover.Text.Contains("环比", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("统计图悬停未逐条生成同比 / 环比文案");
            }

            var hoverSample = hovers.LastOrDefault(hover => hover.Text.Contains("月收入", StringComparison.Ordinal))
                ?? hovers[^1];
            log.Add("悬停样例：" + hoverSample.Text.Replace(Environment.NewLine, " ｜ "));
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
