using System.Collections.ObjectModel;
using KFL.Core.Enums;
using KFL.Infrastructure.Abstractions;
using KFL.Presentation.Mvvm;

namespace KFL.Presentation.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    public ObservableCollection<Choice<SaveSlot>> Saves { get; } = [];

    /// <summary>saves/backups 下的压缩备份（需求 15）。</summary>
    public ObservableCollection<Choice<SaveSlot>> Backups { get; } = [];

    /// <summary>主菜单里可直接读取的已有存档（需求 1）。</summary>
    public ObservableCollection<Choice<SaveSlot>> MenuSaves { get; } = [];

    public IReadOnlyList<Choice<Origin>> Origins { get; }

    public IReadOnlyList<Choice<Difficulty>> Difficulties { get; }

    public IReadOnlyList<Choice<LivingStandard>> LivingStandards { get; }

    public IReadOnlyList<Choice<AssetKind>> AssetKinds { get; }

    public IReadOnlyList<Choice<string>> MemberFilters { get; }

    public IReadOnlyList<Choice<SaveFormat>> SaveFormats { get; }

    public Choice<Origin> SelectedOrigin { get; set; }

    public Choice<Difficulty> SelectedDifficulty { get; set; }

    public Choice<LivingStandard> SelectedLivingStandard { get; set; }

    public Choice<AssetKind> SelectedAssetKind { get; set; }

    public Choice<SaveSlot>? SelectedSave
    {
        get => _selectedSave;
        set
        {
            if (Set(ref _selectedSave, value))
            {
                RefreshSaveOverview();
            }
        }
    }

    public bool IsLoaded => _session is not null;

    public bool IsMenuVisible => !IsLoaded;

    public bool IsEnded
    {
        get => _isEnded;
        private set => Set(ref _isEnded, value);
    }

    public string StatusLine
    {
        get => _statusLine;
        private set => Set(ref _statusLine, value);
    }

    public string EconomyLine
    {
        get => _economyLine;
        private set => Set(ref _economyLine, value);
    }

    public string ExamInfo
    {
        get => _examInfo;
        private set => Set(ref _examInfo, value);
    }

    public string AchievementSummary
    {
        get => _achievementSummary;
        private set => Set(ref _achievementSummary, value);
    }

    public string StatisticsText
    {
        get => _statisticsText;
        private set => Set(ref _statisticsText, value);
    }

    public string ChronicleText
    {
        get => _chronicleText;
        private set => Set(ref _chronicleText, value);
    }

    public string SaveName
    {
        get => _saveName;
        set => Set(ref _saveName, value);
    }

    public string NewSurname
    {
        get => _newSurname;
        set => Set(ref _newSurname, value);
    }

    public string NewSeed
    {
        get => _newSeed;
        set => Set(ref _newSeed, value);
    }

}
