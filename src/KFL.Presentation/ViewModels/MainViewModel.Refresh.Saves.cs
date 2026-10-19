using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Achievements;
using KFL.Rules.Config;
using KFL.Rules.Economy;
using KFL.Presentation.Mvvm;

namespace KFL.Presentation.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>选中存档的概览：加载该档读取状态（不写入、不推进），外加文件最后写入时间。</summary>
    private void RefreshSaveOverview()
    {
        if (SelectedSave is not { } chosen)
        {
            SaveOverviewText = "（选中左侧存档后显示概览）";
            return;
        }

        SaveOverviewText = GameSession.DescribeSave(chosen.Value);
    }

    private void RefreshStatistics()
    {
        if (_session is null)
        {
            StatisticsText = string.Empty;
            StatsInsights = string.Empty;
            return;
        }

        var state = _session.State;
        var family = state.Family;
        var date = state.CurrentDate;
        var start = new GameDate(SaveCadencePolicy.EpochYear, SaveCadencePolicy.EpochMonth);
        var jinShi = family.Members.Count(person => person.CurrentDegree == DegreeLevel.JinShi);
        var juRen = family.Members.Count(person => person.CurrentDegree == DegreeLevel.JuRen);
        var officials = family.RegisteredMembers.Count(person => person.Rank is not null);
        var lines = new List<string>
        {
            $"存续 {date.Year - SaveCadencePolicy.EpochYear} 年 {date.Month - SaveCadencePolicy.EpochMonth + 1} 月（第 {SaveCadencePolicy.CompletedTurnsAt(date)} 回合）",
            $"在册 {family.RegisteredMembers.Count} 人 ｜ 归档 {family.ArchivedMembers.Count} 人 ｜ 进士 {jinShi} 人 ｜ 举人 {juRen} 人 ｜ 现任官员 {officials} 人",
            $"总资产 {Display.Money(TotalAssetsCalculator.Of(state.Economy))} ｜ 族库 {Display.Money(state.Economy.TreasuryPool)} ｜ 米价 {GrainPricePolicy.MarketPrice(state.Economy.GrainPriceIndex.Value):0.##} 文/斗",
            "── 累计分类收支（开局至今）──",
        };

        foreach (var (category, total) in state.Economy.Ledger.TotalsByCategory(start, date))
        {
            lines.Add($"　{Display.Category(category)}：{Display.Money(total)}");
        }

        lines.Add("── 人口 ──");
        foreach (var person in family.Members)
        {
            lines.Add($"　{person.Name}　{person.AgeAt(date)} 岁　{Display.Degree(person.CurrentDegree)}　{(person.Rank is { } rank ? rank.ToString() : "—")}　{Display.StatusOf(person, date)}");
        }

        StatisticsText = string.Join(Environment.NewLine, lines);
        StatsInsights = StatsCharts.BuildInsights(state);

        Charts.Clear();
        foreach (var frame in StatsCharts.Build(state, _moneyChartCumulative, _populationChartCumulative))
        {
            Charts.Add(frame);
        }
    }

    private void RefreshSaves()
    {
        Saves.Clear();
        foreach (var slot in GameSession.ListSaves())
        {
            var format = slot.Format == SaveFormat.Json ? "JSON" : "二进制";
            Saves.Add(new Choice<SaveSlot>(slot, $"{slot.Name} ｜ {format}"));
        }

        // 存档页一进来就能看到概览：默认选中最近写过的那个档。
        // 玩家反馈 9 / 12：档被删掉或换过之后，**失效的选择必须复位**，否则概览停在已不存在的档上。
        if (_selectedSave is null || !Saves.Any(option => option.Value.Equals(_selectedSave.Value)))
        {
            SelectedSave = Saves.Count > 0 ? Saves[0] : null;
        }

        MenuSaves.Clear();
        foreach (var slot in GameSession.ListSaves())
        {
            var format = slot.Format == SaveFormat.Json ? "JSON" : "二进制";
            MenuSaves.Add(new Choice<SaveSlot>(slot, $"{slot.Name} ｜ {format}"));
        }

        if (_selectedMenuSave is null || !MenuSaves.Any(option => option.Value.Equals(_selectedMenuSave.Value)))
        {
            SelectedMenuSave = MenuSaves.FirstOrDefault();
        }
    }

    /// <summary>列出 <c>saves/backups</c> 下的压缩备份（需求 15）。</summary>
    private void RefreshBackups()
    {
        Backups.Clear();
        foreach (var slot in GameSession.ListBackups())
        {
            var format = slot.Format == SaveFormat.Json ? "JSON" : "二进制";
            Backups.Add(new Choice<SaveSlot>(slot, $"{slot.Name} ｜ {format} ｜ 压缩备份"));
        }

        if (_selectedBackup is null || !Backups.Any(option => option.Value.Equals(_selectedBackup.Value)))
        {
            SelectedBackup = Backups.FirstOrDefault();
        }
    }

    /// <summary>主菜单里选中存档的概览（需求 1）。</summary>
    private void RefreshMenuSaveOverview()
    {
        MenuSaveOverviewText = SelectedMenuSave is { } chosen
            ? GameSession.DescribeSave(chosen.Value)
            : "（选中存档后显示概览）";
    }

    private void RefreshAchievements()
    {
        Achievements.Clear();
        if (_session is null)
        {
            return;
        }

        var state = _session.State;
        _session.ObserveUnlockTimes();
        foreach (var definition in AchievementCatalog.All.OrderBy(d => d.Code, StringComparer.Ordinal))
        {
            var marks = state.Achievements.Completed.Where(m => m.Achievement == definition.Id).ToList();
            var completed = AchievementQuery.IsCompleted(state, definition.Id);
            var note = completed
                ? string.Join('、', marks.Select(m => $"{Display.Difficulty(m.Difficulty)} {m.CompletedAt.Year}/{m.CompletedAt.Month}"
                    + (m.CompletedAt.Year == 1 && m.CompletedAt.Month == 1 ? "（开局即达成）" : string.Empty)))
                : string.Empty;
            var local = completed
                ? string.Join("、", marks.Select(m => _session.UnlockTimes.LocalText(m)))
                : string.Empty;
            Achievements.Add(new AchievementRow(
                definition.Code,
                definition.Name,
                Display.Series(definition.Series),
                completed,
                note,
                AchievementDescriptions.Describe(definition.Id),
                local,
                hidden: AchievementCatalog.IsHidden(definition)));
        }

        var total = AchievementCatalog.All.Count;
        var lines = new List<string> { $"总计 {AchievementQuery.CompletedCount(state)} / {total}" };
        foreach (var series in Enum.GetValues<AchievementSeries>())
        {
            var progress = AchievementQuery.SeriesProgress(state, series);
            lines.Add($"{Display.Series(series)} {progress.Completed}/{progress.Total}");
        }

        AchievementSummary = string.Join("　", lines);
    }
}
