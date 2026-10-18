using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Achievements;
using KFL.Rules.Bribery;
using KFL.Rules.Config;
using KFL.Rules.Economy;
using KFL.Rules.Exam;
using KFL.Rules.FamilyLifecycle;
using KFL.Rules.Punishment;
using KFL.Rules.Settlement;
using KFL.Presentation.Mvvm;

namespace KFL.Presentation.ViewModels;

/// <summary>主界面视图模型：持有 <see cref="GameSession"/>，把所有交互派发到规则层。</summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly DispatcherTimer _fastForwardTimer;

    private GameSession? _session;
    private MemberRow? _selected;
    private string _detailText = "（未选择成员）";
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

        SelectedOrigin = Origins[0];
        SelectedDifficulty = Difficulties[1];
        SelectedLivingStandard = LivingStandards[1];
        SelectedAssetKind = AssetKinds[0];
        _newSeed = Environment.TickCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
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

    public ObservableCollection<Choice<SaveSlot>> Saves { get; } = [];

    public IReadOnlyList<Choice<Origin>> Origins { get; }

    public IReadOnlyList<Choice<Difficulty>> Difficulties { get; }

    public IReadOnlyList<Choice<LivingStandard>> LivingStandards { get; }

    public IReadOnlyList<Choice<AssetKind>> AssetKinds { get; }

    public Choice<Origin> SelectedOrigin { get; set; }

    public Choice<Difficulty> SelectedDifficulty { get; set; }

    public Choice<LivingStandard> SelectedLivingStandard { get; set; }

    public Choice<AssetKind> SelectedAssetKind { get; set; }

    public Choice<SaveSlot>? SelectedSave
    {
        get => _selectedSave;
        set => Set(ref _selectedSave, value);
    }

    public bool IsLoaded => _session is not null;

    public bool IsMenuVisible => !IsLoaded;

    public bool IsEnded
    {
        get => _isEnded;
        private set => Set(ref _isEnded, value);
    }

    public bool IsFastForwarding
    {
        get => _isFastForwarding;
        private set
        {
            if (Set(ref _isFastForwarding, value))
            {
                Raise(nameof(AdvanceLabel));
                // 规格书 §3.3：快进期间行贿入口置灰（其余面板仍可用）。
                Raise(nameof(CanBribe));
            }
        }
    }

    /// <summary>快进期间行贿入口是否可用（规格书 §3.3）。</summary>
    public bool CanBribe => !IsFastForwarding;

    public string AdvanceLabel => IsFastForwarding ? $"快进中…余 {_fastForwardMonthsLeft} 月" : "下月";

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

    public bool ConsoleVisible
    {
        get => _consoleVisible;
        set => Set(ref _consoleVisible, value);
    }

    public string ConsoleInput
    {
        get => _consoleInput;
        set => Set(ref _consoleInput, value);
    }

    public string ConsoleOutput
    {
        get => _consoleOutput;
        private set => Set(ref _consoleOutput, value);
    }

    public MemberRow? Selected
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value))
            {
                RefreshDetail();
            }
        }
    }

    public string DetailText
    {
        get => _detailText;
        private set => Set(ref _detailText, value);
    }

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
        Raise(nameof(IsLoaded));
        Raise(nameof(IsMenuVisible));
    }

    public void SaveOnExit() => _session?.SaveOnExit();

    // ───────────────────────── 刷新 ─────────────────────────

    private void RefreshLog()
    {
        LogLines.Clear();
        if (_session is null)
        {
            return;
        }

        foreach (var line in _session.Log)
        {
            LogLines.Add(line);
        }
    }

    public void RefreshAll()
    {
        if (_session is null)
        {
            return;
        }

        var state = _session.State;
        var date = state.CurrentDate;
        var family = state.Family;

        var previousSelectedId = Selected?.Person.Id;
        Members.Clear();
        foreach (var person in family.RegisteredMembers.OrderByDescending(p => family.HeadId == p.Id).ThenBy(p => p.Id.Value))
        {
            Members.Add(new MemberRow(person, family, date));
        }

        Archived.Clear();
        foreach (var person in family.ArchivedMembers.OrderByDescending(p => p.Id.Value))
        {
            Archived.Add(new MemberRow(person, family, date, archived: true));
        }

        if (previousSelectedId is { } id)
        {
            Selected = Members.Concat(Archived).FirstOrDefault(row => row.Person.Id == id);
        }

        Selected ??= Members.FirstOrDefault();

        var economy = state.Economy;
        var treasury = economy.Treasury;
        StatusLine = $"{date.Year} 年 {date.Month} 月 ｜ {family.Name}家 ｜ 出身 {Display.Origin(state.Origin)} ｜ 难度 {Display.Difficulty(state.Difficulty)}"
            + (state.PendingDifficulty is { } pending ? $"（次月转 {Display.Difficulty(pending)}）" : string.Empty)
            + $" ｜ 米价 {GrainPricePolicy.MarketPrice(economy.GrainPriceIndex.Value):0.##} 文/斗（系数 {economy.GrainPriceIndex.Value:0.###}）"
            + $" ｜ 生活费 {Display.LivingStandard(economy.LivingStandard)}"
            + (economy.PendingLivingStandard is { } pendingStandard ? $"→{Display.LivingStandard(pendingStandard)}" : string.Empty)
            + $" ｜ 持续报考 {(family.AutoExamApplication ? "开" : "关")}"
            + (family.HasShiStatus ? " ｜ 『仕』族" : string.Empty)
            + (state.Ending.IsEnded ? " ｜ ※绝嗣" : string.Empty);

        EconomyLine = $"现银 {Display.Money(treasury.Cash)} ｜ 储蓄 {Display.Money(treasury.Savings)}（年息 {(treasury.SavingsRate is { } savingsRate ? savingsRate.ToString("P1", System.Globalization.CultureInfo.InvariantCulture) : "未定")}）"
            + $" ｜ 商本 {Display.Money(treasury.MerchantCapital)}"
            + $" ｜ 贷款 {(treasury.Loan.IsSettled ? "无" : Display.Money(treasury.Loan.Total) + $"（本 {Display.Money(treasury.Loan.Principal)} / 息 {Display.Money(treasury.Loan.AccruedInterest)}）")}"
            + $" ｜ 田 {economy.Holdings.FarmlandMu} 亩 · 农舍 {economy.Holdings.RuralHouses} · 城市宅 {economy.Holdings.UrbanHouses} · 铺 {economy.Holdings.Shops}"
            + $" ｜ 总资产 {Display.Money(TotalAssetsCalculator.Of(economy))}"
            + (economy.Famine.Stage == FamineStage.None ? string.Empty : $" ｜ ⚠饥荒({economy.Famine.Stage})");

        var candidates = ExamRegistration.EligibleUnapplied(family, date).ToList();
        ExamCandidates.Clear();
        foreach (var person in candidates)
        {
            ExamCandidates.Add(Members.FirstOrDefault(row => row.Person.Id == person.Id) ?? new MemberRow(person, family, date));
        }

        var level = ExamCalendar.LevelOf(date);
        var registrable = CountedMembers.Registered(family).Count(person => ExamRegistration.CanRegister(person, date));
        ExamInfo = level is { } examLevel
            ? $"本月为 {Display.ExamLevel(examLevel)} 月。"
                + (examLevel == ExamLevel.DianShi ? "殿试无需报名。" : $"可报名 {registrable} 人（路费 {Display.Money(Money.FromGuan(ExamPolicy.ExamTravelCostGuan(state.Difficulty)))}）。")
                + (candidates.Count > 0 ? $" 待报名：{string.Join('、', candidates.Select(c => c.Name))}" : string.Empty)
            : $"本月非考期。下一场考试：{ExamCalendar.NextExamMonth(date).Year} 年 {ExamCalendar.NextExamMonth(date).Month} 月。";

        RefreshAchievements();
        RefreshDetail();
        RefreshSaves();
        RefreshStatistics();
        IsEnded = state.Ending.IsEnded;
        ChronicleText = BuildChronicle();
    }

    private void RefreshStatistics()
    {
        if (_session is null)
        {
            StatisticsText = string.Empty;
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
            lines.Add($"　{category}：{Display.Money(total)}");
        }

        lines.Add("── 人口 ──");
        foreach (var person in family.Members)
        {
            lines.Add($"　{person.Name}　{person.AgeAt(date)} 岁　{Display.Degree(person.CurrentDegree)}　{(person.Rank is { } rank ? rank.ToString() : "—")}　{Display.StatusOf(person, date)}");
        }

        StatisticsText = string.Join(Environment.NewLine, lines);
    }

    private void RefreshSaves()
    {
        Saves.Clear();
        foreach (var slot in GameSession.ListSaves())
        {
            Saves.Add(new Choice<SaveSlot>(slot, $"{slot.Name}（{slot.Format}）"));
        }
    }

    private void RefreshAchievements()
    {
        Achievements.Clear();
        if (_session is null)
        {
            return;
        }

        var state = _session.State;
        foreach (var definition in AchievementCatalog.All.OrderBy(d => d.Code, StringComparer.Ordinal))
        {
            var completed = AchievementQuery.IsCompleted(state, definition.Id);
            var note = completed
                ? string.Join('、', state.Achievements.Completed.Where(m => m.Achievement == definition.Id).Select(m => $"{Display.Difficulty(m.Difficulty)} {m.CompletedAt.Year}/{m.CompletedAt.Month}"))
                : string.Empty;
            Achievements.Add(new AchievementRow(definition.Code, definition.Name, Display.Series(definition.Series), completed, note));
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

    private string BuildChronicle()
    {
        if (_session is null || _session.State.Ending.Chronicle is not { } chronicle)
        {
            return "家族尚在存续。";
        }

        var state = _session.State;
        return $"""
            家族编年史（{state.Family.Name}家）
            存续年数：{chronicle.SurvivedYears} 年
            传承代数：{chronicle.Generations} 代
            峰值资产：{Display.Money(chronicle.PeakTotalAssets)}
            最高功名：{Display.Degree(chronicle.HighestDegree)}
            进士总数：{chronicle.TotalJinShi} 人
            平均寿命：{chronicle.AverageLifespan:0.###} 岁
            族谱人数：{chronicle.Genealogy.Count} 人
            """;
    }

    private void RefreshDetail()
    {
        if (_session is null || Selected is null)
        {
            DetailText = "（未选择成员）";
            return;
        }

        var state = _session.State;
        var family = state.Family;
        var date = state.CurrentDate;
        var person = Selected.Person;
        var lines = new List<string>
        {
            $"姓名：{person.Name}　{Display.Gender(person.Gender)}　{person.AgeAt(date)} 岁　第 {person.Generation} 代"
                + (family.HeadId == person.Id ? "　【家主】" : string.Empty)
                + (Selected.IsArchived ? "　（归档）" : string.Empty),
            $"功名：{Display.Degree(person.CurrentDegree)}"
                + (person.CurrentPlacement is { } placement ? $"（{Display.Placement(placement)}）" : string.Empty)
                + $"　官职：{(person.Rank is { } rank ? rank.ToString() : "无")}"
                + $"　政绩：{person.Merit}"
                + (person.EntryTrack is { } track ? $"　入仕：{track}" : string.Empty),
            $"学业：{person.Study}　体质：{person.Health}　天命寿数：{person.Lifespan}"
                + $"　天赋：农 {person.Talents.Agriculture} / 商 {person.Talents.Commerce} / 仕 {person.Talents.Officialdom} / 工 {person.Talents.Craft}",
            $"状态：{Display.StatusOf(person, date)}"
                + $"　配偶：{(person.SpouseId is { } spouseId ? family.TryGet(spouseId)?.Name ?? "—" : "—")}"
                + (person.IsTutoredByMaster ? "　名师：是" : string.Empty),
            $"报考：{ExamRegistration.TargetLevelOf(person)?.ToString() ?? "无可报考"}"
                + $"　本月可报名：{(ExamRegistration.CanRegister(person, date) ? "是" : "否")}"
                + $"　特奏名：{(ExamRegistration.SpecialTributeEligible(person, date) ? "符合" : "否")}"
                + $"　持续报考：{(family.AutoExamApplication ? "开" : "关")}",
            $"解试累计失败 {person.JieShiFailureCount} / 省试累计失败 {person.ShengShiFailureCount}",
        };

        foreach (var level in new[] { ExamLevel.JieShi, ExamLevel.ShengShi, ExamLevel.DianShi })
        {
            var feasibility = PunishmentQueries.Of(state, person, level);
            var nth = BribeRules.NthOf(person, level);
            lines.Add($"行贿 {Display.ExamLevel(level)}：第 {nth} 次，需 {Display.Money(feasibility.Amount)}，被抓 {(double)feasibility.Probability:P1}，乘数 {BribeRules.MultiplierOf(level, person):0.###}"
                + $"　[{(PunishmentQueries.IsWithinBribeWindow(date, level) ? "窗口内" : "非窗口")}｜{(feasibility.IsFeasible ? "可行" : Display.BribeReason(feasibility.RejectReason))}"
                + $"{(PunishmentQueries.IsLevelCapReached(state, person, level) ? "｜已达上限" : string.Empty)}]");
        }

        var illness = LifecycleQueries.IllnessOf(person);
        if (illness.Ill)
        {
            lines.Add($"疾病：程度 {illness.SeverityBasisPoints} 磅值　疗程剩余 {(illness.TreatmentRemainingMonths is { } remaining ? remaining.ToString(System.Globalization.CultureInfo.InvariantCulture) : "未就医")} 月");
        }

        if (LifecycleQueries.IsMarriageable(family, person, LifecycleQueries.DirectionOf(person.Gender), date))
        {
            var feasibility = LifecycleQueries.MatchmakeFeasibility(state, person, LifecycleQueries.DirectionOf(person.Gender));
            lines.Add($"婚姻：可托媒（{Display.LifecycleReason(feasibility.RejectReason)}）　托媒费 {Display.Money(feasibility.MatchmakerFee)}"
                + $"　买功名婚姻：{(feasibility.DegreeMarriageAvailable ? $"可行，{Display.Money(feasibility.DegreeMarriageFee)}" : "不可行")}");
        }
        else
        {
            lines.Add($"婚姻：暂不可托媒（{Display.LifecycleReason(LifecycleQueries.MatchmakeFeasibility(state, person, LifecycleQueries.DirectionOf(person.Gender)).RejectReason)}）");
        }

        lines.Add("功名变迁史：");
        if (person.DegreeHistory.Count == 0)
        {
            lines.Add("　（无）");
        }
        else
        {
            foreach (var record in person.DegreeHistory)
            {
                lines.Add($"　{record.ChangedAt.Year} 年 {record.ChangedAt.Month} 月　{Display.Degree(record.Level)}"
                    + (record.Placement is { } recordPlacement ? $"（{Display.Placement(recordPlacement)}）" : string.Empty)
                    + $"　[{record.Cause}]");
            }
        }

        DetailText = string.Join(Environment.NewLine, lines);
    }

    // ───────────────────────── 动作派发 ─────────────────────────

    private void Execute(string? token)
    {
        if (_session is null || string.IsNullOrEmpty(token))
        {
            return;
        }

        var parts = token.Split(':');
        var head = parts[0];
        var error = string.Empty;

        switch (head)
        {
            case "advance":
                if (_session.CanAdvance)
                {
                    _session.AdvanceMonth();
                }

                break;
            case "ff":
                FastForward(parts.Length > 1 ? parts[1] : "12");
                return;
            case "stop":
                StopFastForward();
                break;
            case "exam":
                error = WithSelected(session => session.RegisterExam(Selected!.Person));
                break;
            case "special":
                error = WithSelected(session => session.AcceptSpecialTribute(Selected!.Person));
                break;
            case "bribe":
                error = WithSelected(session => session.Bribe(Selected!.Person, ParseExamLevel(parts[1])));
                break;
            case "assetbuy":
                error = _session.BuyAsset(SelectedAssetKind.Value, parts.Length > 1 ? int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 1);
                break;
            case "assetsell":
                error = _session.SellAsset(SelectedAssetKind.Value, parts.Length > 1 ? int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 1);
                break;
            case "money":
                error = _session.MoveMoney(parts[1] == "savings", Money.FromGuan(decimal.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture)));
                break;
            case "merchant":
                error = _session.MerchantCapital(parts[1] == "in", Money.FromGuan(decimal.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture)));
                break;
            case "repay":
                error = _session.RepayLoan(Money.FromGuan(decimal.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)));
                break;
            case "living":
                error = _session.SetLivingStandard(Enum.Parse<LivingStandard>(parts[1]));
                break;
            case "difficulty":
                error = _session.SetDifficulty(Enum.Parse<Difficulty>(parts[1]));
                break;
            case "autoexam":
                error = _session.SetAutoExamApplication(!_session.State.Family.AutoExamApplication);
                break;
            case "matchmake":
                error = WithSelected(session => session.Matchmake(Selected!.Person, parts.Length > 1 && parts[1] == "degree"));
                break;
            case "proposal":
                error = _session.RespondToProposal(parts[1] != "reject", parts.Length > 2 && parts[2] == "degree");
                break;
            case "childbirth":
                error = WithCouple();
                break;
            case "buyperson":
                error = _session.BuyPerson();
                break;
            case "treat":
                error = WithSelected(session => session.StartTreatment(Selected!.Person));
                break;
            case "tutor":
                error = WithSelected(session => session.SetTutor(Selected!.Person, !Selected!.Person.IsTutoredByMaster));
                break;
            case "head":
                error = WithSelected(session => session.SetHead(Selected!.Person));
                break;
            case "release":
                error = WithSelected(session => session.Release(Selected!.Person));
                break;
            case "pardon":
                error = WithSelected(session => session.Pardon(Selected!.Person));
                break;
            case "clearbans":
                error = WithSelected(session => session.ClearBans(Selected!.Person));
                break;
            case "clearfamilybans":
                error = _session.ClearFamilyBans();
                break;
            case "save":
                error = _session.Save();
                break;
            case "saveas":
                error = _session.SaveAs(SaveName, SaveFormat.Binary);
                break;
            case "export":
                error = _session.ExportLedgerCsv();
                break;
            case "backup":
                error = _session.Backup();
                break;
            case "load":
                LoadGame(parts.Length > 1 && parts[1] == "selected" && SelectedSave is { } chosen
                    ? chosen.Value
                    : new SaveSlot(SaveName, SaveFormat.Binary));
                return;
            case "menu":
                Detach();
                return;
            case "console":
                ConsoleVisible = !ConsoleVisible;
                return;
            case "quit":
                ConsoleVisible = false;
                return;
            default:
                error = $"未知命令：{token}";
                break;
        }

        if (error.Length > 0)
        {
            _session.Note($"✗ {error}");
        }

        RefreshLog();
        RefreshAll();
    }

    private string WithSelected(Func<GameSession, string> action)
        => Selected is null ? "请先选择一名成员。" : action(_session!);

    private string WithCouple()
    {
        if (Selected is null || _session is null)
        {
            return "请先选择一名成员。";
        }

        var family = _session.State.Family;
        var person = Selected.Person;
        if (person.SpouseId is not { } spouseId || family.TryGet(spouseId) is not { } spouse)
        {
            return "所选成员无配偶，无法申请生育。";
        }

        var couple = person.Gender == Gender.Male
            ? new CoupleKey(person.Id, spouse.Id)
            : new CoupleKey(spouse.Id, person.Id);

        var feasibility = LifecycleQueries.ChildbirthFeasibility(_session.State, couple, _session.State.CurrentDate);
        if (!feasibility.Available)
        {
            return $"生育不可行：{Display.LifecycleReason(feasibility.RejectReason)}。";
        }

        return _session.ApplyChildbirth(couple);
    }

    private static ExamLevel ParseExamLevel(string text) => text switch
    {
        "JieShi" => ExamLevel.JieShi,
        "ShengShi" => ExamLevel.ShengShi,
        "DianShi" => ExamLevel.DianShi,
        _ => ExamLevel.JieShi,
    };

    // ───────────────────────── 快进 ─────────────────────────

    private void FastForward(string monthsText)
    {
        if (_session is null || !_session.CanAdvance)
        {
            return;
        }

        _fastForwardMonthsLeft = int.Parse(monthsText, System.Globalization.CultureInfo.InvariantCulture);
        IsFastForwarding = true;
        _fastForwardTimer.Start();
    }

    private void FastForwardTick()
    {
        if (_session is null || _fastForwardMonthsLeft <= 0)
        {
            StopFastForward();
            return;
        }

        var pause = _session.AdvanceMonth();
        _fastForwardMonthsLeft--;
        Raise(nameof(AdvanceLabel));

        if (pause)
        {
            _session.Note(_session.State.Ending.IsEnded ? "快进停止：家族绝嗣。" : "快进停止：出现待决策事件。");
            StopFastForward();
        }

        RefreshLog();
        RefreshAll();
    }

    private void StopFastForward()
    {
        _fastForwardTimer.Stop();
        _fastForwardMonthsLeft = 0;
        IsFastForwarding = false;
        RefreshLog();
        RefreshAll();
    }

    // ───────────────────────── 调试控制台 ─────────────────────────

    private void RunConsoleCommand()
    {
        var command = ConsoleInput.Trim();
        ConsoleInput = string.Empty;
        if (command.Length == 0)
        {
            return;
        }

        if (command is "close" or "exit")
        {
            ConsoleVisible = false;
            return;
        }

        var output = ExecuteConsole(command);
        ConsoleOutput = $"{ConsoleOutput}\n> {command}\n{output}";
        RefreshLog();
        RefreshAll();
    }

    private string ExecuteConsole(string command)
    {
        if (_session is null)
        {
            return "尚未开局。";
        }

        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        switch (parts[0])
        {
            case "help":
                return "wake N｜money G｜heal｜kill｜tutor｜bribe-reset｜clearbans｜clearfamilybans｜difficulty easy|normal|hard|hell｜difficulty-list｜ach｜date｜close";
            case "wake":
                return parts.Length > 1 && int.TryParse(parts[1], out var count)
                    ? _session.DebugWake(count)
                    : _session.DebugWake(1);
            case "money":
                return parts.Length > 1 && decimal.TryParse(parts[1], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var guan)
                    ? _session.DebugMoney(Money.FromGuan(guan))
                    : "用法：money 100";
            case "heal":
                return WithSelected(session => session.DebugHeal(Selected!.Person));
            case "kill":
                return _session.State.Family.HeadId is { } headId ? _session.DebugKill(_session.State.Family.TryGet(headId)!) : "无家主。";
            case "bribe-reset":
                return WithSelected(session => session.ResetBribe(Selected!.Person));
            case "clearbans":
                return WithSelected(session => session.ClearBans(Selected!.Person));
            case "clearfamilybans":
                return _session.ClearFamilyBans();
            case "tutor":
                return WithSelected(session => session.SetTutor(Selected!.Person, !Selected!.Person.IsTutoredByMaster));
            case "difficulty":
                return parts.Length > 1 && Enum.TryParse<Difficulty>(parts[1], ignoreCase: true, out var difficulty)
                    ? _session.SetDifficulty(difficulty)
                    : "用法：difficulty hard";
            case "difficulty-list":
                return $"当前 {Display.Difficulty(_session.State.Difficulty)}（收益 {DifficultyRates.RevenueFactor(_session.State.Difficulty):0.##} / 支出 {DifficultyRates.ExpenseFactor(_session.State.Difficulty):0.##} / 风险 {DifficultyRates.BriberyRiskFactor(_session.State.Difficulty):0.##} / 负面 {DifficultyRates.NegativeEventFactor(_session.State.Difficulty):0.##}）";
            case "ach":
                return AchievementSummary;
            case "date":
                return $"{_session.State.CurrentDate}，回合数 {SaveCadencePolicy.CompletedTurnsAt(_session.State.CurrentDate)}";
            default:
                return $"未知命令 {parts[0]}，输入 help。";
        }
    }

    /// <summary>控制台浮窗快捷键（反引号 / 波浪键）开关。</summary>
    public void ToggleConsole() => ConsoleVisible = !ConsoleVisible;

    public void ActivateConsole()
    {
        ConsoleVisible = true;
        ConsoleOutput = $"{ConsoleOutput}\nConsole Activated";
    }
}
