using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;
using KFL.Presentation.Mvvm;

namespace KFL.Presentation.ViewModels;

/// <summary>主界面视图模型：持有 <see cref="GameSession"/>，把所有交互派发到规则层。</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly DispatcherTimer _fastForwardTimer;

    private GameSession? _session;
    private MemberRow? _selected;
    private string _detailText = "（未选择成员）";
    private string _detailHeadline = "（未选择成员）";
    private string _detailBrushKey = "InkBrush";
    private string _statusLine = "尚未开局";
    private string _economyLine = string.Empty;
    private string _examInfo = string.Empty;
    private string _achievementSummary = string.Empty;
    private string _chronicleText = string.Empty;
    private string _statisticsText = string.Empty;
    private string _saveName = string.Empty;
    private Choice<SaveSlot>? _selectedSave;
    private string _consoleInput = string.Empty;
    private string _consoleOutput = "科举浮生录 · 调试控制台\n输入 help 查看命令。";
    private bool _consoleVisible;
    private bool _isFastForwarding;
    private int _fastForwardMonthsLeft;
    private string _newSurname = string.Empty;
    private string _newSeed = string.Empty;
    private bool _isEnded;
    private Choice<string> _selectedMemberFilter;
    private Choice<SaveFormat> _selectedSaveFormat;
    private string _saveOverviewText = "（选中左侧存档后显示概览）";
    private string _memberCountText = string.Empty;
    private string _monthlyBalanceText = string.Empty;
    private bool _isBreakdownVisible;
    private string _monthlyBreakdownText = "（点击上方「本月收支」查看按人、按来源的明细）";
    private bool _autoTreat;
    private Choice<SaveSlot>? _selectedBackup;
    private Choice<SaveSlot>? _selectedMenuSave;
    private string _menuSaveOverviewText = "（选中存档后显示概览）";
    private double _consoleHeight = 440;
    private bool _moneyChartCumulative;
    private bool _populationChartCumulative;
    private string _statsInsights = string.Empty;
    private string _overviewCaption = string.Empty;
    private string _moneyAmountText = "10";
    private readonly HashSet<Guid> _childbirthPending = [];
    private bool _showParentLinks = true;
    private bool _showChildLinks = true;
    private bool _showSiblingLinks = true;
    private bool _showPurchaseMarks = true;
    private bool _showHeadBranchOnly;
    private double _treeWidth = FamilyTree.ColumnStride;
    private double _treeHeight = FamilyTree.RowStride;
    private Choice<PersonId>? _selectedEndogamy;

    public MainViewModel()
    {
        DoCommand = new ActionCommand(Execute);
        StartGameCommand = new ActionCommand(_ => StartNewGame());
        ConsoleRunCommand = new ActionCommand(_ => RunConsoleCommand());
        ConsoleQuickCommand = new ActionCommand(parameter =>
        {
            ConsoleInput = parameter as string ?? string.Empty;
            RunConsoleCommand();
        });

        _fastForwardTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
        _fastForwardTimer.Tick += (_, _) => FastForwardTick();

        Origins =
        [
            new Choice<Origin>(Origin.Farmer, "农户（田 40 亩，省钱）"),
            new Choice<Origin>(Origin.Artisan, "匠户（农村宅，岁末分红）"),
            new Choice<Origin>(Origin.Merchant, "商户（现金 500 贯 + 商本 300 贯）"),
            new Choice<Origin>(Origin.Scholar, "士人（家主举人，读书更快）"),
        ];
        Difficulties =
        [
            new Choice<Difficulty>(Difficulty.Easy, "简单（收益 1.4 / 支出 0.6 / 风险 0.2）"),
            new Choice<Difficulty>(Difficulty.Normal, "普通（基准）"),
            new Choice<Difficulty>(Difficulty.Hard, "困难（收益 0.9 / 支出 1.1 / 风险 1.5）"),
            new Choice<Difficulty>(Difficulty.Hell, "地狱（收益 0.8 / 支出 1.3 / 风险 3.0）"),
        ];
        LivingStandards =
        [
            new Choice<LivingStandard>(LivingStandard.Frugal, "拮据"),
            new Choice<LivingStandard>(LivingStandard.Normal, "普通"),
            new Choice<LivingStandard>(LivingStandard.Comfortable, "体面"),
        ];
        AssetKinds =
        [
            new Choice<AssetKind>(AssetKind.Farmland, "田亩（1 贯/亩）"),
            new Choice<AssetKind>(AssetKind.RuralHouse, "农舍（10 贯）"),
            new Choice<AssetKind>(AssetKind.UrbanHouse, "城市宅（100 贯）"),
            new Choice<AssetKind>(AssetKind.Shop, "商铺（300 贯，月租 20%）"),
        ];
        MemberFilters =
        [
            new Choice<string>("registered", "在册成员"),
            new Choice<string>("all", "全部（在册 + 归档）"),
            new Choice<string>("archived", "已归档（亡故 / 外嫁）"),
            new Choice<string>("deceased", "已死亡"),
            new Choice<string>("marriedout", "已外嫁"),
            new Choice<string>("serving", "服刑中"),
            new Choice<string>("awaiting", "待阙中"),
            new Choice<string>("banned", "禁考 / 禁升"),
        ];
        SaveFormats =
        [
            new Choice<SaveFormat>(SaveFormat.Binary, "二进制 .dat（默认）"),
            new Choice<SaveFormat>(SaveFormat.Json, "JSON .json（可读）"),
        ];

        SelectedOrigin = Origins[0];
        SelectedDifficulty = Difficulties[1];
        SelectedLivingStandard = LivingStandards[1];
        SelectedAssetKind = AssetKinds[0];
        _selectedMemberFilter = MemberFilters[0];
        _selectedSaveFormat = SaveFormats[0];
        _newSeed = Environment.TickCount.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // 开局菜单（需求 1）：未开局时也要能列出本机已有存档并读取。
        RefreshSaves();
    }

    public ICommand DoCommand { get; }

    public ICommand StartGameCommand { get; }

    public ICommand ConsoleRunCommand { get; }

    public ICommand ConsoleQuickCommand { get; }

    public GameSession? Session => _session;

    public ObservableCollection<MemberRow> Members { get; } = [];

    public ObservableCollection<MemberRow> Archived { get; } = [];

    public ObservableCollection<MemberRow> ExamCandidates { get; } = [];

    public ObservableCollection<AchievementRow> Achievements { get; } = [];

    public ObservableCollection<string> LogLines { get; } = [];

    // ───────────────────────── 开局 ─────────────────────────

    public void StartNewGame()
    {
        var seed = int.TryParse(NewSeed, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : Environment.TickCount;
        var surname = string.IsNullOrWhiteSpace(NewSurname) ? null : NewSurname.Trim();
        _session = GameSession.CreateNew(SelectedOrigin.Value, SelectedDifficulty.Value, surname, seed);
        SaveName = _session.Slot.Name;
        Attach();
    }

    /// <summary>
    /// 抓图/自检用：按 <c>月数[:出身中文前缀]</c> 规格自动开局并推进若干月。
    /// 由环境变量 <c>KFL_UI_PLAY</c> 触发，正常启动不走这里。
    /// </summary>
    public void AutoPlay(string spec)
    {
        var parts = spec.Split(':', 2);
        var months = int.TryParse(parts[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
        if (parts.Length > 1)
        {
            var origin = Origins.FirstOrDefault(option => option.Text.StartsWith(parts[1], StringComparison.Ordinal));
            if (origin is not null)
            {
                SelectedOrigin = origin;
            }
        }

        StartNewGame();
        DoCommand.Execute("autoexam");
        for (var i = 0; i < months; i++)
        {
            DoCommand.Execute("advance");
        }
    }

    public void LoadGame(SaveSlot slot)
    {
        try
        {
            _session = GameSession.LoadSaved(slot, Environment.TickCount);
            SaveName = slot.Name;
            Attach();
        }
        catch (Exception ex) when (ex is KFL.Infrastructure.Save.SaveFileException or IOException or UnauthorizedAccessException)
        {
            ConsoleOutput = $"读档失败：{ex.Message}";
            ConsoleVisible = true;
        }
    }

    /// <summary>从 <c>saves/backups/*.zip</c> 读档（需求 15）。</summary>
    public void LoadBackup(SaveSlot slot)
    {
        try
        {
            _session = GameSession.LoadBackup(slot, Environment.TickCount);
            SaveName = _session.Slot.Name;
            Attach();
        }
        catch (Exception ex) when (ex is KFL.Infrastructure.Save.SaveFileException or IOException or UnauthorizedAccessException)
        {
            ConsoleOutput = $"读备份失败：{ex.Message}";
            ConsoleVisible = true;
        }
    }

    private void Attach()
    {
        RefreshLog();
        RefreshAll();
        Raise(nameof(IsLoaded));
        Raise(nameof(IsMenuVisible));
    }

    public void Detach()
    {
        _session?.SaveOnExit();
        _session = null;
        Members.Clear();
        Archived.Clear();
        ExamCandidates.Clear();
        Achievements.Clear();
        LogLines.Clear();
        StatusLine = "尚未开局";
        EconomyLine = string.Empty;
        DetailText = "（未选择成员）";
        SetDetailHeadline("（未选择成员）", "InkBrush");
        Raise(nameof(IsLoaded));
        Raise(nameof(IsMenuVisible));
    }

    public void SaveOnExit() => _session?.SaveOnExit();
}
