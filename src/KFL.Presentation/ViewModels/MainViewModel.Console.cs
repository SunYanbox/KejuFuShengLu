using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;
using KFL.Rules.Exam;
using KFL.Rules.Settlement;
using KFL.Presentation.Mvvm;

namespace KFL.Presentation.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>
    /// 录取率与自愈率明细正文（需求 18）——列出本窗口内每位候选人的级别与完整算式，
    /// 以及患病成员的自愈率来源。数值随行贿次数、学业、仕天赋实时变化。
    /// 玩家反馈 14 后它不再是独立窗口，而是控制台 <c>examrates</c> 的输出。
    /// </summary>
    private string BuildExamRatesText()
    {
        if (_session is null)
        {
            return "（尚未开始游戏）";
        }

        var state = _session.State;
        var date = state.CurrentDate;
        var lines = new List<string>();

        var level = ExamRegistration.RegisterLevelOf(date);
        lines.Add($"【{date.Year} 年 {date.Month} 月】"
            + (level is { } windowLevel
                ? $"报名窗口：{Display.ExamLevel(windowLevel)}（考试当月及前一月）"
                : "当前不在任何考试的报名窗口内。"));

        var targets = CountedMembers.Registered(state.Family)
            .Where(person => _session.AdmissionBreakdownOf(person) is not null)
            .OrderBy(person => person.Id.Value)
            .ToList();

        if (targets.Count == 0)
        {
            lines.Add("（当前没有可报考解试/省试的在册成员）");
        }

        foreach (var person in targets)
        {
            if (_session.AdmissionBreakdownOf(person) is not { } breakdown)
            {
                continue;
            }

            var applied = person.AppliedExam is { } mark ? $"已报名（{Display.ExamLevel(mark)}）" : "未报名";
            lines.Add(string.Empty);
            lines.Add($"◆ {person.Name}（{Display.ExamLevel(breakdown.Level)}，{applied}）");
            lines.Add(GameSession.DescribeAdmissionBreakdown(breakdown));
        }

        var treated = CountedMembers.Registered(state.Family)
            .Where(person => person.Status.HasFlag(StatusFlag.Ill))
            .OrderBy(person => person.Id.Value)
            .ToList();
        if (treated.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("── 自愈率（患病成员）──");
            foreach (var person in treated)
            {
                lines.Add($"◆ {person.Name}");
                lines.Add(GameSession.DescribeRecoveryBreakdown(
                    _session.RecoveryBreakdownOf(person, person.Timers.TreatmentRemainingMonths is not null)));
            }
        }

        lines.Add(string.Empty);
        lines.Add("提示：行贿乘数随该级别的行贿次数实时变化；调试加成来自控制台 examrate / healrate 命令。");
        return string.Join(Environment.NewLine, lines);
    }

    // ───────────────────────── 调试控制台 ─────────────────────────

    /// <summary>控制台高度（需求 17：可调，便于看清长输出）。</summary>
    public double ConsoleHeight
    {
        get => _consoleHeight;
        set => Set(ref _consoleHeight, Math.Clamp(value, 240, 900));
    }

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

    /// <summary>调试控制台命令表：用法与说明成对维护，渲染时对齐（玩家反馈：原先只列命令名）。</summary>
    private static readonly (string Usage, string Description)[] ConsoleCommands =
    [
        ("help", "显示本帮助"),
        ("wake [N]", "悟道 N 次（省略为 1）；累计可解锁「调试秘辛」系列成就"),
        ("money <贯>", "家族现银 +贯（可小数，负数即扣款）"),
        ("heal", "治愈选中成员（清患病与疗程标记）"),
        ("kill", "令现任家主病故（用于验证继承 / 绝嗣）"),
        ("release", "释放选中成员（清除服刑与刑期）"),
        ("pardon", "免死：清除选中成员的死刑判决"),
        ("clearbans", "解除选中成员的禁考 / 禁升禁令"),
        ("clearfamilybans", "解除全族禁考 / 禁升禁令（连坐用）"),
        ("bribe-reset", "清空选中成员解试 / 省试 / 殿试行贿次数与乘数"),
        ("talent <农|商|仕|工> <0~100>", "改写选中成员的四项天赋点"),
        ("attr <study|health|lifespan|merit> <值>", "改写选中成员的学业 / 体质 / 天命寿数 / 政绩"),
        ("examrate <百分点>", "为选中成员本次考试录取率 +N 个百分点（如 20 = +20%）"),
        ("healrate <百分点>", "为选中成员疾病自愈率 +N 个百分点"),
        ("clear-overrides", "清空选中成员的全部调试加成"),
        ("ach-unlock <ACH-064|64|all>", "强制解锁成就（含「调试秘辛」隐藏成就）"),
        ("console-big / console-small", "放大 / 缩小控制台浮窗"),
        ("tutor", "切换选中成员的「名师」开关（月费由结算扣）"),
        ("difficulty <档>", "easy | normal | hard | hell（次月生效）"),
        ("difficulty-list", "列出四档难度的收益 / 支出 / 风险 / 负面系数"),
        ("ach", "成就进度一览（七系列）"),
        ("date", "当前游戏年月与回合数"),
        ("close", "关闭控制台（等同反引号）"),
    ];

    private static string ConsoleHelpText()
    {
        var lines = new List<string>(ConsoleCommands.Length + 2)
        {
            "科举浮生录 · 调试控制台命令（参数 <必填> [可选]）",
        };
        lines.AddRange(ConsoleCommands.Select(command => $"  {command.Usage.PadRight(18)}{command.Description}"));
        lines.Add("提示：禁考 / 禁升 / 服刑 / 免死等调试按钮已从主界面移入本控制台。");
        return string.Join('\n', lines);
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
                return ConsoleHelpText();
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
            case "talent":
                return parts.Length > 2 && int.TryParse(parts[2], out var talentValue)
                    ? WithSelected(session => session.DebugSetTalent(Selected!.Person, parts[1], talentValue))
                    : "用法：talent 农 80";
            case "attr":
                return parts.Length > 2 && int.TryParse(parts[2], out var attributeValue)
                    ? WithSelected(session => session.DebugSetAttribute(Selected!.Person, parts[1], attributeValue))
                    : "用法：attr study 100";
            case "examrate":
                return parts.Length > 1 && decimal.TryParse(parts[1], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var examPoints)
                    ? WithSelected(session => session.DebugAddExamRateBonus(Selected!.Person, examPoints))
                    : "用法：examrate 20";
            case "healrate":
                return parts.Length > 1 && decimal.TryParse(parts[1], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var healPoints)
                    ? WithSelected(session => session.DebugAddHealRateBonus(Selected!.Person, healPoints))
                    : "用法：healrate 20";
            case "clear-overrides":
                return _session.DebugClearOverrides();
            case "ach-unlock":
                return parts.Length > 1
                    ? _session.DebugUnlockAchievement(parts[1])
                    : "用法：ach-unlock 64 | ACH-064 | all";
            case "console-big":
                ConsoleHeight = 760;
                return $"控制台高度 {ConsoleHeight:0}。";
            case "console-small":
                ConsoleHeight = 320;
                return $"控制台高度 {ConsoleHeight:0}。";
            case "release":
                return WithSelected(session => session.Release(Selected!.Person));
            case "pardon":
                return WithSelected(session => session.Pardon(Selected!.Person));
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
        // 玩家反馈：原先只列命令名、看不到参数说明，因此激活时直接把带参数的帮助贴出来。
        ConsoleOutput = $"{ConsoleOutput}\nConsole Activated\n\n{ConsoleHelpText()}";
    }
}
