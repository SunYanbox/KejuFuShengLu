using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Achievements;
using KFL.Infrastructure.Save;
using KFL.Infrastructure.Services;
using KFL.Rules.Config;
using KFL.Rules.Exam;
using KFL.Rules.FamilyLifecycle;
using KFL.Rules.Save;
using KFL.Rules.Settlement;
using KFL.Rules.Start;
using System.IO;

namespace KFL.Presentation;

/// <summary>
/// 一局游戏的会话门面：持有 <see cref="GameState"/>、<see cref="TurnRunner"/> 与全部服务，
/// 把规则层入口包装成「动作 + 日志」的形态供界面直接调用。
/// </summary>
public sealed partial class GameSession
{
    private const int MaxLogLines = 600;

    private readonly ISaveService _saves;
    private readonly IAchievementStore _achievements;
    private readonly IRandomService _random;
    private readonly INameGenerator _names;
    private TurnRunner _runner;
    private readonly List<string> _log = [];

    private GameSession(
        GameState state,
        SaveSlot slot,
        ISaveService saves,
        IRandomService random,
        INameGenerator names,
        IAchievementStore achievements)
    {
        State = state;
        Slot = slot;
        _saves = saves;
        _random = random;
        _names = names;
        _achievements = achievements;
        _runner = new TurnRunner(random, saves, slot, names, achievements);
        UnlockTimes = AchievementTimeLog.For(slot, saves.RootDirectory);
    }

    public GameState State { get; private set; }

    public SaveSlot Slot { get; private set; }

    public IReadOnlyList<string> Log => _log;

    public IRandomService Random => _random;

    public INameGenerator Names => _names;

    public string SaveDirectory => _saves.RootDirectory;

    /// <summary>成就本机解锁时刻记录（内部 UTC，展示转本地时区）。</summary>
    public AchievementTimeLog UnlockTimes { get; }

    /// <summary>把当前已达成的成就登记进本机时刻表（重复调用无副作用）。</summary>
    public void ObserveUnlockTimes() => UnlockTimes.Observe(State.Achievements);

    /// <summary>
    /// 切换本档的存档格式（二进制 .dat / JSON .json）：立即改写扩展名并重建推进器，
    /// 从下一次自动存档开始写入新格式。旧格式文件保留在原处。
    /// </summary>
    /// <param name="format">目标格式。</param>
    /// <returns>成功返回空串，失败返回给玩家看的中文原因。</returns>
    public string SetSaveFormat(SaveFormat format)
    {
        if (Slot.Format == format)
        {
            return string.Empty;
        }

        Slot = Slot with { Format = format };
        _runner = new TurnRunner(_random, _saves, Slot, _names, _achievements);
        Note($"存档格式切换为{FormatName(format)}（{_saves.FilePathOf(Slot)}），下次自动存档起生效。");
        Changed?.Invoke();
        return string.Empty;
    }

    private static string FormatName(SaveFormat format) =>
        format == SaveFormat.Json ? " JSON（.json，可读）" : " 二进制（.dat，默认）";

    public event Action? Changed;

    public static GameSession CreateNew(Origin origin, Difficulty difficulty, string? surname, int seed)
    {
        var random = new SeededRandomService(seed);
        var names = new SongStyleNameGenerator(random);
        var saves = new SaveService();
        var achievements = new AchievementStore(saves);
        var result = NewGameSetup.Create(
            new NewGameRequest(origin, difficulty, surname, new GameDate(SaveCadencePolicy.EpochYear, SaveCadencePolicy.EpochMonth)),
            random,
            names);
        var slot = new SaveSlot(result.State.Family.Name + "家", SaveFormat.Binary);
        var session = new GameSession(result.State, slot, saves, random, names, achievements);
        session.Note($"★ {result.State.Family.Name}家开基：出身 {Display.Origin(origin)}，难度 {Display.Difficulty(difficulty)}，起始 {result.State.CurrentDate}。");
        return session;
    }

    public static GameSession LoadSaved(SaveSlot slot, int seed)
    {
        var random = new SeededRandomService(seed);
        var names = new SongStyleNameGenerator(random);
        var saves = new SaveService();
        var achievements = new AchievementStore(saves);
        var state = saves.Load(slot);
        var session = new GameSession(state, slot, saves, random, names, achievements);
        session.Note($"☆ 读档 {slot.Name}：{state.Family.Name}家，{state.CurrentDate}。");
        return session;
    }

    public void Note(string message)
    {
        _log.Add($"[{State.CurrentDate.Year}年{State.CurrentDate.Month}月] {message}");
        if (_log.Count > MaxLogLines)
        {
            _log.RemoveRange(0, _log.Count - MaxLogLines);
        }
    }

    public void ClearLog() => _log.Clear();

    // ───────────────────────── 时间推进 ─────────────────────────

    public bool CanAdvance => !State.Ending.IsEnded;

    /// <summary>推进一个月；返回是否仍需暂停（出现决策类事件）。</summary>
    public bool AdvanceMonth()
    {
        if (State.Ending.IsEnded)
        {
            Note("家族已绝嗣，本档不可继续推进。");
            return true;
        }

        var before = State.Achievements.Completed.Select(static mark => mark.Achievement).ToHashSet();
        var outcome = _runner.Advance(State);
        DescribeTurn(outcome, before);
        AutoRegisterForUpcomingExam();

        if (outcome.AchievementFlush is { IsSuccess: false } flush)
        {
            Note($"（成就落盘失败：{flush.FailureReason}）");
        }

        Changed?.Invoke();
        return State.Ending.IsEnded || HasPendingDecision();
    }

    /// <summary>
    /// 持续报考：进入「报名窗口」（考试当月及前一月）时**立刻**替全家报名
    /// （2026-10-19 需求 9：原先只在考试月结算内部自动报名，玩家看不到、也来不及行贿）。
    /// </summary>
    public void AutoRegisterForUpcomingExam()
    {
        if (!State.Family.AutoExamApplication)
        {
            return;
        }

        var date = State.CurrentDate;
        var level = ExamRegistration.RegisterLevelOf(date);

        if (level is null)
        {
            return;
        }

        var registered = new List<string>();

        foreach (var person in ExamRegistration.EligibleUnapplied(State.Family, date))
        {
            ExamRegistration.Register(person, date);
            registered.Add(person.Name);
        }

        if (registered.Count > 0)
        {
            Note($"✎ 持续报考：为 {registered.Count} 人报名{ExamLevelText(level.Value)}（{string.Join("、", registered)}）。");
        }
    }

    /// <summary>考试级别中文名。</summary>
    /// <param name="level">级别。</param>
    /// <returns>中文名。</returns>
    public static string ExamLevelText(ExamLevel level) => level switch
    {
        ExamLevel.JieShi => "解试",
        ExamLevel.ShengShi => "省试",
        ExamLevel.DianShi => "殿试",
        _ => level.ToString(),
    };

    /// <summary>是否存在需要玩家决策的事件（婚配提议 / 特奏名询问 / 科举报名窗口）。</summary>
    public bool HasPendingDecision()
    {
        if (State.Ending.IsEnded)
        {
            return true;
        }

        if (LifecycleQueries.PendingProposal(State.Family, State.CurrentDate) is not null)
        {
            return true;
        }

        // 报名窗口 = 考试当月及前一月（2026-10-19：结算在「进入考试月」那一步就判完考试，
        // 因此必须在**前一月**停下来，否则玩家永远没机会报名 / 行贿）。
        var date = State.CurrentDate;
        if (ExamRegistration.RegisterLevelOf(date) is not null
            && CountedMembers.Registered(State.Family).Any(person =>
                ExamRegistration.CanRegister(person, date) || ExamRegistration.SpecialTributeEligible(person, date)))
        {
            return true;
        }

        return false;
    }
}
