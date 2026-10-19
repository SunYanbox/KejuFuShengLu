using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Achievements;
using KFL.Infrastructure.Save;
using KFL.Infrastructure.Services;
using KFL.Rules.Achievements;
using KFL.Rules.Bribery;
using KFL.Rules.Config;
using KFL.Rules.Economy;
using KFL.Rules.Exam;
using KFL.Rules.FamilyLifecycle;
using KFL.Rules.Punishment;
using KFL.Rules.Save;
using KFL.Rules.Settlement;
using KFL.Rules.Start;
using System.IO;

namespace KFL.Presentation;

/// <summary>
/// 一局游戏的会话门面：持有 <see cref="GameState"/>、<see cref="TurnRunner"/> 与全部服务，
/// 把规则层入口包装成「动作 + 日志」的形态供界面直接调用。
/// </summary>
public sealed class GameSession
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

    public static IReadOnlyList<SaveSlot> ListSaves()
    {
        var saves = new SaveService();
        var found = new List<SaveSlot>();
        foreach (var path in SaveCatalog.ListFiles(saves.RootDirectory))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            // 存档根目录里同时躺着成就档（achievements.json）、成就时刻表与本机脏数据；
            // 只有「不以 achievement 开头」的 .dat/.json 才是可读存档。
            if (name.StartsWith("achievement", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var extension = Path.GetExtension(path);
            if (!SaveFileFormat.TryFormatOfExtension(extension, out var format))
            {
                continue;
            }

            try
            {
                found.Add(new SaveSlot(name, format));
            }
            catch (ArgumentException)
            {
                // 档名含非法字符（非本程序写入的文件）⇒ 跳过而不是让整页崩掉。
            }
        }

        return found
            .OrderByDescending(slot => File.Exists(saves.FilePathOf(slot)) ? File.GetLastWriteTimeUtc(saves.FilePathOf(slot)) : DateTime.MinValue)
            .ToList();
    }

    /// <summary>读档预览：加载该档后抽取可比较的概览，供读档对话框筛选（不写入、不推进）。</summary>
    public static string DescribeSave(SaveSlot slot)
    {
        SaveService saves;
        GameState state;
        try
        {
            saves = new SaveService();
            state = saves.Load(slot);
        }
        catch (SaveFileException ex)
        {
            return $"（无法读取：{ex.Message}）";
        }
        catch (IOException ex)
        {
            return $"（无法读取：{ex.Message}）";
        }

        var family = state.Family;
        var economy = state.Economy;
        var registered = family.RegisteredMembers.Count;
        var archived = family.ArchivedMembers.Count;
        var degrees = family.Members
            .GroupBy(p => p.CurrentDegree)
            .OrderByDescending(g => g.Key)
            .Select(g => $"{Display.Degree(g.Key)} {g.Count()}");
        var highest = family.Members.Count == 0 ? DegreeLevel.BaiShen : family.Members.Max(p => p.CurrentDegree);
        var path = saves.FilePathOf(slot);
        var written = File.Exists(path)
            ? File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)
            : "（文件不存在）";

        return string.Join(Environment.NewLine,
            $"{family.Name}家 ｜ 出身 {Display.Origin(state.Origin)} ｜ 难度 {Display.Difficulty(state.Difficulty)}",
            $"进度：{state.CurrentDate} ｜ 在册 {registered} 人 ｜ 归档 {archived} 人 ｜ 最高功名 {Display.Degree(highest)}",
            $"功名分布：{(degrees.Any() ? string.Join('、', degrees) : "（无）")}",
            $"总资产 {Display.Money(TotalAssetsCalculator.Of(economy))} ｜ 现银 {Display.Money(economy.Treasury.Cash)} ｜ 储蓄 {Display.Money(economy.Treasury.Savings)} ｜ 商本 {Display.Money(economy.Treasury.MerchantCapital)}",
            $"负债 {Display.Money(economy.Treasury.Loan.Total)} ｜ 田 {economy.Holdings.FarmlandMu} 亩 ｜ 农舍 {economy.Holdings.RuralHouses} ｜ 城宅 {economy.Holdings.UrbanHouses} ｜ 商铺 {economy.Holdings.Shops}",
            $"成就 {AchievementQuery.CompletedCount(state)}/68 ｜ 米价 {economy.GrainPriceIndex.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}",
            $"存档文件：{path}",
            $"最后写入（本机时间）：{written}");
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

    /// <summary>把一条回合结算翻译成人类可读的日志行。</summary>
    private void DescribeTurn(TurnOutcome outcome, HashSet<AchievementId> achievementsBefore)
    {
        var settlement = outcome.Settlement;
        var parts = new List<string>();

        if (settlement.LivingCostPaid.Wen > 0m)
        {
            parts.Add($"月用 {Display.Money(settlement.LivingCostPaid)}");
        }

        if (settlement.Incomes.Count > 0)
        {
            parts.Add($"进项 {Display.Money(settlement.Incomes.Sum(static line => line.Amount.Wen) is var total ? Money.FromWen(total) : Money.Zero)}");
        }

        parts.Add($"结余 {Display.Money(settlement.NetProfit)}");

        if (settlement.GrainPriceIndexAfter != settlement.GrainPriceIndexBefore)
        {
            parts.Add($"米价系数 {settlement.GrainPriceIndexAfter:0.###}");
        }

        Note(string.Join('，', parts) + "。");

        if (settlement.FamineBefore.Stage != settlement.FamineAfter.Stage)
        {
            Note(settlement.FamineAfter.Stage == FamineStage.None
                ? "☀ 饥荒缓解，恢复平常。"
                : $"⚠ 饥荒进入「{FamineStageText(settlement.FamineAfter.Stage)}」阶段（余 {FamineTimelineText(settlement.FamineAfter)}）。");
        }

        var life = settlement.Lifecycle;
        foreach (var birth in life.Births)
        {
            var baby = State.Family.TryGet(birth.PersonId);
            Note($"👶 {baby?.Name ?? "婴儿"} 降生（{Display.Gender(birth.Gender)}）。");
        }

        foreach (var marriage in life.Marriages)
        {
            var person = State.Family.TryGet(marriage.PersonId);
            var spouse = State.Family.TryGet(marriage.SpouseId);
            Note($"❤ {person?.Name ?? "族人"} 与 {spouse?.Name ?? "新人"} 成婚。");
        }

        foreach (var outMarriage in life.MarriedOut)
        {
            var person = State.Family.TryGet(outMarriage.PersonId);
            Note($"「{person?.Name ?? "族女"}」外嫁，移入外嫁档。");
        }

        foreach (var crossing in life.Crossings)
        {
            var person = State.Family.TryGet(crossing.PersonId);
            Note($"· {person?.Name} 进入 {BracketText(crossing.To)}。");
        }

        foreach (var illness in life.Illnesses)
        {
            var person = State.Family.TryGet(illness.PersonId);
            if (illness.Contracted)
            {
                Note($"✚ {person?.Name} 染疾。");
            }
            else if (illness.Recovered)
            {
                Note($"✓ {person?.Name} 康复。");
            }
        }

        foreach (var death in life.Deaths)
        {
            var person = State.Family.TryGet(death.PersonId);
            Note($"✝ {person?.Name ?? "族人"} 亡故（{DeathText(death.Cause)}）。");
        }

        foreach (var purchase in life.Purchases)
        {
            var person = State.Family.TryGet(purchase.PersonId);
            Note($"⚑ 买入人口 {person?.Name}（第 {purchase.Ordinal} 名，{Display.Money(purchase.Amount)}）。");
        }

        if (life.PendingProposal is { } proposal)
        {
            var our = State.Family.TryGet(proposal.OurMemberId);
            Note($"✉ 有人向「{our?.Name}」提亲：{proposal.CandidateName}（{Display.Gender(proposal.CandidateGender)}，{proposal.CandidateAge} 岁，{Display.Degree(proposal.CandidateDegree)}），聘礼 {Display.Money(proposal.BetrothalGift)}，嫁妆 {Display.Money(proposal.Dowry)} —— 待应答。");
        }

        if (life.HeadBecameVacant)
        {
            Note("※ 家主之位空虚，请指定新家主。");
        }
        else if (life.NewHeadId is { } newHead)
        {
            Note($"※ 新家主：{State.Family.TryGet(newHead)?.Name}。");
        }

        var exam = settlement.ExamSeason;
        if (exam.Level is { } level)
        {
            Note($"📜 {Display.ExamLevel(level)}月：报考 {exam.AutoApplications.Count + exam.Outcomes.Count} 人。");
        }

        foreach (var outcomeLine in exam.Outcomes)
        {
            var person = State.Family.TryGet(outcomeLine.PersonId);
            var name = person?.Name ?? "考生";
            if (outcomeLine.Passed)
            {
                var extra = outcomeLine.Level == ExamLevel.DianShi
                    ? $"（{Display.Placement(outcomeLine.Placement)}）"
                    : string.Empty;
                Note($"🎉 {name} {Display.ExamLevel(outcomeLine.Level)}及第，功名 {Display.Degree(outcomeLine.DegreeAfter)}{extra}！");
            }
            else
            {
                Note($"✗ {name} {Display.ExamLevel(outcomeLine.Level)}落第（累计 {outcomeLine.FailureCountAfter} 次）。");
            }
        }

        foreach (var exemption in exam.Exemptions)
        {
            Note($"※ {State.Family.TryGet(exemption)?.Name} 获免解（解试累计三败）。");
        }

        foreach (var inquiry in exam.SpecialTributeInquiries)
        {
            Note($"✉ {State.Family.TryGet(inquiry)?.Name} 符合特奏名：是否接受授官（L18）？");
        }

        foreach (var appointment in exam.Appointments)
        {
            Note($"■ {State.Family.TryGet(appointment.PersonId)?.Name} 入仕：{TrackText(appointment.Track)}，待阙 {appointment.AwaitingPostMonths} 月。");
        }

        if (exam.HasShiStatusBecomesTrue)
        {
            Note("★ 家族晋为「仕」族！婚嫁按仕族、举族沾光。");
        }

        var career = settlement.Career;
        foreach (var appointment in career.Appointments)
        {
            Note($"■ {State.Family.TryGet(appointment.PersonId)?.Name} 授官 {appointment.Rank}（{TrackText(appointment.Track)}）。");
        }

        foreach (var promotion in career.Promotions)
        {
            Note($"▲ {State.Family.TryGet(promotion.PersonId)?.Name} 考课升迁：{promotion.From} → {promotion.To}。");
        }

        foreach (var retirement in career.Retirements)
        {
            Note($"○ {State.Family.TryGet(retirement)?.Name} 七十致仕，领半俸。");
        }

        var punishment = settlement.Punishment;
        foreach (var bribe in punishment.Bribes)
        {
            var person = State.Family.TryGet(bribe.Examinee);
            Note(bribe.Caught
                ? $"🚨 {person?.Name} 行贿 {Display.ExamLevel(bribe.Level)} 被查！"
                : $"（{person?.Name} 行贿 {Display.ExamLevel(bribe.Level)} 未被察觉，第 {bribe.Nth} 次）");
        }

        foreach (var sentence in punishment.Sentences)
        {
            Note($"⛓ {State.Family.TryGet(sentence.Subject)?.Name} 被判刑 {sentence.Months} 月。");
        }

        foreach (var fine in punishment.Fines)
        {
            Note($"💰 罚金 {Display.Money(fine.Amount)}（家产抵扣 {Display.Money(fine.PaidFromTreasury)}，转贷 {Display.Money(fine.ToLoan)}）。");
        }

        if (punishment.FamilyBanDeclared)
        {
            Note("⛔ 全族禁考禁升 48 月！");
        }

        foreach (var collective in punishment.CollectivePunishments)
        {
            var relative = State.Family.TryGet(collective.Relative);
            Note($"连坐：{relative?.Name ?? "亲属"} —— {CollectiveText(collective.Outcome)}。");
        }

        foreach (var release in punishment.Releases)
        {
            Note($"🔓 {State.Family.TryGet(release.Subject)?.Name} 获释。");
        }

        foreach (var mark in State.Achievements.Completed)
        {
            if (achievementsBefore.Add(mark.Achievement))
            {
                var definition = AchievementCatalog.Of(mark.Achievement);
                Note($"🏅 成就达成：{definition.Code} {definition.Name}（{Display.Difficulty(mark.Difficulty)}）");
            }
        }

        if (State.Ending.IsEnded)
        {
            Note("═══ 家族绝嗣，游戏结束 ═══");
        }
    }

    private static string FamineStageText(FamineStage stage) => stage switch
    {
        FamineStage.Famine => "饥荒",
        FamineStage.Relief => "赈济",
        FamineStage.Severe => "大饥",
        _ => "平常",
    };

    private static string FamineTimelineText(FamineState state)
        => $"{FamineTimelineRemaining(state)} 月";

    private static int FamineTimelineRemaining(FamineState state)
        => KFL.Rules.Config.FamineTimeline.RemainingMonths(state);

    private static string BracketText(AgeBracket bracket) => bracket switch
    {
        AgeBracket.Child => "幼年",
        AgeBracket.Youth => "少年",
        AgeBracket.Adult => "成年",
        AgeBracket.Elder => "老年",
        _ => bracket.ToString(),
    };

    private static string DeathText(DeathCause cause) => cause switch
    {
        DeathCause.OldAge => "寿终",
        DeathCause.Illness => "病故",
        DeathCause.Famine => "饥荒",
        DeathCause.Infancy => "夭折",
        DeathCause.Childbirth => "产厄",
        _ => cause.ToString(),
    };

    private static string TrackText(KFL.Core.Enums.AppointmentTrack track) => track switch
    {
        KFL.Core.Enums.AppointmentTrack.FirstClass => "一甲",
        KFL.Core.Enums.AppointmentTrack.SecondClass => "二甲",
        KFL.Core.Enums.AppointmentTrack.ThirdClass => "三甲",
        KFL.Core.Enums.AppointmentTrack.SpecialTribute => "特奏名",
        _ => track.ToString(),
    };

    private static string CollectiveText(CollectiveOutcome outcome) => outcome switch
    {
        CollectiveOutcome.Executed => "处死",
        CollectiveOutcome.DegreeDemoted => "降一级功名",
        CollectiveOutcome.FamilyWideBan => "受全族禁令",
        _ => outcome.ToString(),
    };

    // ───────────────────────── 科举与行贿 ─────────────────────────

    public string RegisterExam(Person person)
    {
        var level = ExamRegistration.TargetLevelOf(person);
        if (level is null)
        {
            return $"{person.Name} 暂无可报考科目。";
        }

        if (!ExamRegistration.CanRegister(person, State.CurrentDate))
        {
            return $"{person.Name} 本月不可报名 {Display.ExamLevel(level.Value)}（非报名窗口或资格不符）。";
        }

        ExamRegistration.Register(person, State.CurrentDate);
        var cost = Money.FromGuan(ExamPolicy.ExamTravelCostGuan(State.Difficulty));
        Note($"✎ {person.Name} 报名 {Display.ExamLevel(level.Value)}（路费约 {Display.Money(cost)}）。");
        Changed?.Invoke();
        return string.Empty;
    }

    public string AcceptSpecialTribute(Person person)
    {
        if (!ExamRegistration.SpecialTributeEligible(person, State.CurrentDate))
        {
            return $"{person.Name} 不符合特奏名资格。";
        }

        ExamRegistration.AcceptSpecialTribute(person, State.CurrentDate, _random, State);
        Note($"✎ {person.Name} 接受特奏名，授 L18 待阙。");
        Changed?.Invoke();
        return string.Empty;
    }

    public string Bribe(Person person, ExamLevel level)
    {
        if (!BribeRules.CanBribe(State, person, level, out var feasibility))
        {
            return $"行贿 {Display.ExamLevel(level)} 不可行：{Display.BribeReason(feasibility.RejectReason)}。";
        }

        var outcome = BribeEntry.Bribe(State, person, level, _random);
        if (!outcome.Accepted)
        {
            return $"{person.Name} 行贿被拒：{Display.BribeReason(outcome.RejectReason)}。";
        }

        Note($"💸 {person.Name} 行贿 {Display.ExamLevel(level)} 第 {outcome.Nth} 次，{Display.Money(outcome.Amount)}（现银 {Display.Money(outcome.FromCash)} + 储蓄 {Display.Money(outcome.FromSavings)}），被抓概率 {(double)feasibility.Probability:P1}。");
        Changed?.Invoke();
        return string.Empty;
    }

    public string ResetBribe(Person person)
    {
        BribeEntry.ResetBribeCounts(person, null);
        Note($"（调试）已清零 {person.Name} 的行贿次数与乘数。");
        Changed?.Invoke();
        return string.Empty;
    }

    // ───────────────────────── 家计 ─────────────────────────

    public string BuyAsset(AssetKind kind, int count)
    {
        if (State.Economy.Holdings is null)
        {
            return "无产业账本。";
        }

        try
        {
            var paid = AssetMarket.Buy(State, kind, count);
            Note($"🏠 置办 {Display.Asset(kind)} ×{count}，付出 {Display.Money(paid)}。");
            Changed?.Invoke();
            return string.Empty;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    public string SellAsset(AssetKind kind, int count)
    {
        try
        {
            var gained = AssetMarket.Sell(State, kind, count);
            Note($"💱 变卖 {Display.Asset(kind)} ×{count}，得银 {Display.Money(gained)}。");
            Changed?.Invoke();
            return string.Empty;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    public string MoveMoney(bool toSavings, Money amount)
    {
        var treasury = State.Economy.Treasury;
        try
        {
            if (toSavings)
            {
                treasury.TransferToSavings(amount);
            }
            else
            {
                treasury.TransferToCash(amount);
            }

            Note($"（{Display.Money(amount)} 转入{(toSavings ? "储蓄" : "现银")}）");
            Changed?.Invoke();
            return string.Empty;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    public string MerchantCapital(bool inject, Money amount)
    {
        var treasury = State.Economy.Treasury;
        try
        {
            if (inject)
            {
                treasury.InjectMerchantCapital(amount);
            }
            else
            {
                treasury.WithdrawMerchantCapital(amount);
            }

            Note($"（商本{(inject ? "投入" : "抽回")} {Display.Money(amount)}）");
            Changed?.Invoke();
            return string.Empty;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    public string RepayLoan(Money amount)
    {
        try
        {
            var repayment = State.Economy.RepayLoan(amount, State.CurrentDate);
            Note($"（还贷：本金 {Display.Money(repayment.PrincipalPart)}，利息 {Display.Money(repayment.InterestPart)}）");
            Changed?.Invoke();
            return string.Empty;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    public string SetLivingStandard(LivingStandard standard)
    {
        State.Economy.PendingLivingStandard = standard;
        Note($"（生活费档位切换为「{Display.LivingStandard(standard)}」，次月生效）");
        Changed?.Invoke();
        return string.Empty;
    }

    public string SetDifficulty(Difficulty difficulty)
    {
        State.PendingDifficulty = difficulty;
        Note($"（难度切换为「{Display.Difficulty(difficulty)}」，次月生效）");
        Changed?.Invoke();
        return string.Empty;
    }

    public string SetAutoExamApplication(bool enabled)
    {
        State.Family.AutoExamApplication = enabled;
        Note($"（持续报考：{(enabled ? "开" : "关")}）");
        Changed?.Invoke();
        return string.Empty;
    }

    // ───────────────────────── 婚育 ─────────────────────────

    public string Matchmake(Person member, bool buyDegree)
    {
        var direction = LifecycleQueries.DirectionOf(member.Gender);
        var result = LifecycleEntry.TryMatchmake(State, member, direction, _random, _names, buyDegree);
        if (!result.Accepted)
        {
            return $"托媒失败：{Display.LifecycleReason(result.RejectReason)}。";
        }

        Note($"✉ 为 {member.Name} 托媒，花费 {Display.Money(result.Amount)}，静候佳音。");
        Changed?.Invoke();
        return string.Empty;
    }

    public string RespondToProposal(bool accept, bool buyDegree)
    {
        var result = LifecycleEntry.RespondToProposal(State, accept, _random, _names, buyDegree);
        if (result.RejectReason != LifecycleRejectReason.None)
        {
            return $"应答失败：{Display.LifecycleReason(result.RejectReason)}。";
        }

        Note(accept
            ? $"❤ 应允提亲，聘礼/嫁妆支出 {Display.Money(result.Amount)}。"
            : "✗ 婉拒提亲。");
        Changed?.Invoke();
        return string.Empty;
    }

    /// <summary>
    /// 族内通婚（玩家反馈 1）：把两名同代在册未婚成员结为夫妻，零费用。
    /// </summary>
    /// <param name="first">一方。</param>
    /// <param name="second">另一方（须同代异性）。</param>
    /// <returns>成功 ⇒ 空串；否则为给玩家看的原因。</returns>
    public string MarryWithinFamily(Person first, Person second)
    {
        var result = LifecycleEntry.TryMarryWithinFamily(State, first, second);
        if (!result.Accepted)
        {
            return $"族内通婚失败：{Display.LifecycleReason(result.RejectReason)}。";
        }

        Note($"❤ 族内通婚：{first.Name} 与 {second.Name} 结为夫妻（无聘礼 / 嫁妆）。");
        Changed?.Invoke();
        return string.Empty;
    }

    /// <summary>
    /// 离异（玩家反馈 1）：终止选中成员的婚姻，对方记入前任配偶，零费用。
    /// </summary>
    /// <param name="member">有配偶的一方。</param>
    /// <returns>成功 ⇒ 空串；否则为原因。</returns>
    public string Divorce(Person member)
    {
        var spouse = member.SpouseId is { } spouseId ? State.Family.TryGet(spouseId) : null;
        var result = LifecycleEntry.Divorce(State, member);
        if (!result.Accepted)
        {
            return $"离异失败：{Display.LifecycleReason(result.RejectReason)}。";
        }

        Note($"✂ {member.Name} 与 {(spouse is null ? "配偶" : spouse.Name)} 离异（子女的父母引用不变）。");
        Changed?.Invoke();
        return string.Empty;
    }

    public string ApplyChildbirth(CoupleKey couple)    {
        var result = LifecycleEntry.ApplyChildbirth(State, couple);
        if (!result.Accepted)
        {
            return $"生育申请失败：{Display.LifecycleReason(result.RejectReason)}。";
        }

        Note($"👶 申请生育，花费 {Display.Money(result.Amount)}，次月分娩。");
        Changed?.Invoke();
        return string.Empty;
    }

    public string BuyPerson()
    {
        var result = LifecycleEntry.BuyPerson(State, _random, _names);
        if (!result.Accepted)
        {
            return $"买入人口失败：{Display.LifecycleReason(result.RejectReason)}。";
        }

        Note($"⚑ 买入人口，花费 {Display.Money(result.Amount)}（现金 {Display.Money(result.FromCash)} + 储蓄 {Display.Money(result.FromSavings)}）。");
        Changed?.Invoke();
        return string.Empty;
    }

    public string StartTreatment(Person patient)
    {
        var result = LifecycleEntry.StartTreatment(State, patient);
        if (!result.Accepted)
        {
            return $"就医失败：{Display.LifecycleReason(result.RejectReason)}。";
        }

        Note($"✚ {patient.Name} 延医问药，花费 {Display.Money(result.Amount)}。");
        Changed?.Invoke();
        return string.Empty;
    }

    public string SetTutor(Person person, bool enabled)
    {
        if (!enabled)
        {
            person.IsTutoredByMaster = false;
            Note($"（{person.Name} 辞去名师）");
            Changed?.Invoke();
            return string.Empty;
        }

        var monthly = Money.FromGuan(GrowthTutorFeeGuan());
        person.IsTutoredByMaster = true;
        Note($"✎ {person.Name} 延请名师（约 {Display.Money(monthly)}/月，由月度结算扣缴）。");
        Changed?.Invoke();
        return string.Empty;
    }

    private static decimal GrowthTutorFeeGuan()
        => KFL.Rules.Config.GrowthPolicy.TutorFeeGuanPerMonth;

    public string SetHead(Person person)
    {
        State.Family.SetHead(person.Id);
        Note($"（家主改为 {person.Name}）");
        Changed?.Invoke();
        return string.Empty;
    }

    public string Release(Person person)
    {
        PunishmentCommands.ReleasePrisoner(person);
        Note($"（释放 {person.Name}）");
        Changed?.Invoke();
        return string.Empty;
    }

    public string Pardon(Person person)
    {
        PunishmentCommands.PardonDeathRow(person);
        Note($"（{person.Name} 免死）");
        Changed?.Invoke();
        return string.Empty;
    }

    public string ClearBans(Person person)
    {
        PunishmentCommands.ClearBans(person);
        Note($"（解除 {person.Name} 的禁考/禁升）");
        Changed?.Invoke();
        return string.Empty;
    }

    public string ClearFamilyBans()
    {
        PunishmentCommands.ClearFamilyBans(State.Family);
        Note("（解除全族禁令）");
        Changed?.Invoke();
        return string.Empty;
    }

    // ───────────────────────── 调试 ─────────────────────────

    public string DebugWake(int count)
    {
        var total = AchievementCounters.ReportsDebugWake(State, count);
        Note($"（调试）唤醒 ×{count}，累计 {total}。");
        Changed?.Invoke();
        return string.Empty;
    }

    public string DebugMoney(Money amount)
    {
        State.Economy.Apply(LedgerCategory.AssetSale, null, amount, State.CurrentDate);
        Note($"（调试）现银 {(amount.Wen >= 0 ? "+" : "")}{Display.Money(amount)}。");
        Changed?.Invoke();
        return string.Empty;
    }

    public string DebugHeal(Person person)
    {
        person.Status &= ~StatusFlag.Ill;
        Note($"（调试）治愈 {person.Name}。");
        Changed?.Invoke();
        return string.Empty;
    }

    public string DebugKill(Person person)
    {
        person.Status |= StatusFlag.Deceased;
        Note($"（调试）{person.Name} 病故。");
        Changed?.Invoke();
        return string.Empty;
    }

    /// <summary>调试：直接改写选中成员的某一项天赋（需求 17）。</summary>
    /// <param name="person">成员。</param>
    /// <param name="key">天赋名（农 / 商 / 仕 / 工）。</param>
    /// <param name="value">新值（0~100）。</param>
    /// <returns>结果文本。</returns>
    public string DebugSetTalent(Person person, string key, int value)
    {
        ArgumentNullException.ThrowIfNull(person);

        var talents = person.Talents;
        var clamped = Math.Clamp(value, 0, 100);
        var updated = key switch
        {
            "农" or "agriculture" => (TalentSet?)new TalentSet(clamped, talents.Commerce, talents.Officialdom, talents.Craft),
            "商" or "commerce" => new TalentSet(talents.Agriculture, clamped, talents.Officialdom, talents.Craft),
            "仕" or "officialdom" => new TalentSet(talents.Agriculture, talents.Commerce, clamped, talents.Craft),
            "工" or "craft" => new TalentSet(talents.Agriculture, talents.Commerce, talents.Officialdom, clamped),
            _ => null,
        };

        if (updated is not { } next)
        {
            return "用法：talent <农|商|仕|工> <0~100>";
        }

        person.DebugSetTalents(next);
        Note($"（调试）{person.Name} 天赋 {key} = {clamped}。");
        Changed?.Invoke();
        return $"{person.Name} 天赋 {key} = {clamped}。";
    }

    /// <summary>调试：直接改写选中成员的属性 / 学业 / 体质 / 天命寿数（需求 17）。</summary>
    /// <param name="person">成员。</param>
    /// <param name="key">属性名（study / health / lifespan / merit）。</param>
    /// <param name="value">新值。</param>
    /// <returns>结果文本。</returns>
    public string DebugSetAttribute(Person person, string key, int value)
    {
        ArgumentNullException.ThrowIfNull(person);

        switch (key)
        {
            case "study" or "学业":
                person.Study = Math.Clamp(value, 0, 100);
                break;
            case "health" or "体质":
                person.Health = Math.Clamp(value, 1, 100);
                break;
            case "lifespan" or "寿数" or "天命":
                person.DebugSetLifespan(Math.Max(1, value));
                break;
            case "merit" or "政绩":
                person.Merit = Math.Clamp(value, 0, 100);
                break;
            default:
                return "用法：attr <study|health|lifespan|merit> <数值>";
        }

        Note($"（调试）{person.Name} {key} = {value}。");
        Changed?.Invoke();
        return $"{person.Name} {key} = {value}。";
    }

    /// <summary>调试：为选中成员叠加「本次考试录取率」百分点加成（需求 17）。</summary>
    /// <param name="person">成员。</param>
    /// <param name="points">百分点（1 = +1%），可负。</param>
    /// <returns>结果文本。</returns>
    public string DebugAddExamRateBonus(Person person, decimal points)
    {
        ArgumentNullException.ThrowIfNull(person);

        DebugOverrides.AddExamRateBonus(person.Id, points / 100m);
        Note($"（调试）{person.Name} 录取率加成 +{points}%（当前 {DebugOverrides.ExamRateBonusOf(person.Id) * 100m:0.##}%）。");
        Changed?.Invoke();
        return $"{person.Name} 录取率加成 +{points} 个百分点（累计 {DebugOverrides.ExamRateBonusOf(person.Id) * 100m:0.##}%）。";
    }

    /// <summary>调试：为选中成员叠加「疾病自愈率」百分点加成（需求 17）。</summary>
    /// <param name="person">成员。</param>
    /// <param name="points">百分点（1 = +1%），可负。</param>
    /// <returns>结果文本。</returns>
    public string DebugAddHealRateBonus(Person person, decimal points)
    {
        ArgumentNullException.ThrowIfNull(person);

        DebugOverrides.AddHealRateBonus(person.Id, points / 100m);
        Note($"（调试）{person.Name} 自愈率加成 +{points}%（当前 {DebugOverrides.HealRateBonusOf(person.Id) * 100m:0.##}%）。");
        Changed?.Invoke();
        return $"{person.Name} 自愈率加成 +{points} 个百分点（累计 {DebugOverrides.HealRateBonusOf(person.Id) * 100m:0.##}%）。";
    }

    /// <summary>调试：清空全部在内存里的调试加成（需求 17）。</summary>
    /// <returns>结果文本。</returns>
    public string DebugClearOverrides()
    {
        DebugOverrides.Clear();
        Changed?.Invoke();
        return "已清空全部调试加成（录取率 / 自愈率）。";
    }

    /// <summary>调试：强制解锁成就（需求 12：调试秘辛系列）。</summary>
    /// <param name="code">成就编号（<c>ACH-064</c> / <c>64</c> / <c>all</c>）。</param>
    /// <returns>结果文本。</returns>
    public string DebugUnlockAchievement(string code)
    {
        var text = code.Trim().ToUpperInvariant();
        var targets = new List<AchievementDefinition>();

        if (text is "ALL" or "*")
        {
            targets.AddRange(AchievementCatalog.All);
        }
        else
        {
            var digits = text.StartsWith("ACH-", StringComparison.Ordinal) ? text[4..] : text;
            if (!int.TryParse(digits, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var number))
            {
                return "用法：ach-unlock <ACH-064|64|all>";
            }

            var found = AchievementCatalog.All.FirstOrDefault(definition => definition.Code == $"ACH-{number:D3}");
            if (found is null)
            {
                return $"未找到成就 ACH-{number:D3}。";
            }

            targets.Add(found);
        }

        var unlocked = 0;
        foreach (var definition in targets)
        {
            if (AchievementQuery.IsCompleted(State, definition.Id))
            {
                continue;
            }

            var mark = new AchievementMark(definition.Id, State.Difficulty, State.CurrentDate);
            if (State.Achievements.MarkCompletedAndEnqueue(mark, State.Id))
            {
                unlocked++;
            }
        }

        Note($"（调试）强制解锁成就 {unlocked} 条。");
        Changed?.Invoke();
        return unlocked == 0 ? "没有可解锁的成就（都已达成）。" : $"已强制解锁 {unlocked} 条成就。";
    }

    // ───────────────────────── 概率与自愈率明细（需求 5 / 18） ─────────────────────────

    /// <summary>某成员**当前应考级别**的录取率（已报名则取报名级别，否则取派生级别）。</summary>
    /// <param name="person">成员。</param>
    /// <returns>录取率；进士或无级别时为 <c>null</c>。</returns>
    public decimal? AdmissionRateOf(Person person) => AdmissionBreakdownOf(person)?.Rate;

    /// <summary>某成员当前级别录取率的逐项拆解。</summary>
    /// <param name="person">成员。</param>
    /// <returns>拆解；进士或无级别时为 <c>null</c>。</returns>
    public ExamRateBreakdown? AdmissionBreakdownOf(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        var level = person.AppliedExam ?? ExamRegistration.TargetLevelOf(person);

        if (level is not (ExamLevel.JieShi or ExamLevel.ShengShi))
        {
            return null;
        }

        return ExamAdmission.Breakdown(
            level.Value, person, State.Family.HasShiStatus, BribeRules.MultiplierOf(level.Value, person));
    }

    /// <summary>把录取率拆解排成给玩家看的多行文本（需求 18）。</summary>
    /// <param name="breakdown">拆解。</param>
    /// <returns>多行说明。</returns>
    public static string DescribeAdmissionBreakdown(ExamRateBreakdown breakdown)
    {
        var lines = new List<string>
        {
            $"{ExamLevelText(breakdown.Level)}录取率：{Percent(breakdown.Rate)}" +
                (breakdown.RawRate > ExamPolicy.DiceRateCap ? $"（封顶前 {Percent(breakdown.RawRate)}）" : string.Empty),
            $"  基础率：{Percent(breakdown.BaseRate)}",
            $"  学业系数：×{Number(breakdown.StudyFactor)}（学业 {breakdown.Study}）",
            $"  仕天赋：×{Number(breakdown.OfficialdomFactor)}（仕 {breakdown.Officialdom}，{Percent(breakdown.OfficialdomFactor - 1m)}）",
            $"  行贿乘数：×{Number(breakdown.BribeMultiplier)}（本级累计行贿 {breakdown.BribeCount} 次）",
            $"  仕族加成：×{Number(breakdown.ShiBonus)}",
        };

        if (breakdown.DebugBonus != 0m)
        {
            lines.Add($"  调试加成：+{Percent(breakdown.DebugBonus)}（百分点，直接加在乘积上）");
        }

        lines.Add(
            $"  = {Percent(breakdown.BaseRate)} × {Number(breakdown.StudyFactor)} × {Number(breakdown.OfficialdomFactor)}" +
            $" × {Number(breakdown.BribeMultiplier)} × {Number(breakdown.ShiBonus)}" +
            (breakdown.DebugBonus != 0m ? $" + {Percent(breakdown.DebugBonus)}" : string.Empty) +
            $" = {Percent(breakdown.RawRate + breakdown.DebugBonus)}" +
            (breakdown.RawRate + breakdown.DebugBonus > ExamPolicy.DiceRateCap
                ? $" → 封顶 {Percent(breakdown.Rate)}"
                : string.Empty));

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>某成员当前的自愈率拆解（需求 18）。</summary>
    /// <param name="person">成员。</param>
    /// <param name="treated">是否正在治疗。</param>
    /// <returns>拆解。</returns>
    public HealRateBreakdown RecoveryBreakdownOf(Person person, bool treated) =>
        IllnessQueries.RecoveryBreakdown(person, State.CurrentDate, treated);

    /// <summary>把自愈率拆解排成给玩家看的多行文本（需求 18）。</summary>
    /// <param name="breakdown">拆解。</param>
    /// <returns>多行说明。</returns>
    public static string DescribeRecoveryBreakdown(HealRateBreakdown breakdown)
    {
        var lines = new List<string>
        {
            $"自愈率：{Percent(breakdown.Rate)}（体质 {breakdown.Health}，{(breakdown.Treated ? "治疗中" : "未治疗")}，主导天赋 {breakdown.TalentName}）",
            $"  体质项：{(breakdown.Treated
                ? $"({breakdown.Health} × {Number(IllnessPolicy.TreatedRecoveryCoefficient)}% + {Number(IllnessPolicy.TreatedRecoveryOffset)})"
                : $"{breakdown.Health} × {Number(IllnessPolicy.BaseRecoveryCoefficient)}%")}" +
                $" = {Percent(breakdown.BasePart)}",
            $"  天赋乘数：×{Number(breakdown.TalentMultiplier)}（{breakdown.TalentName}为最高项）",
            $"  年龄增益：+{Percent(breakdown.AgeBonus)}（3~12 岁 +60%、12~24 岁 +30%）",
        };

        if (breakdown.DebugBonus != 0m)
        {
            lines.Add($"  调试加成：+{Percent(breakdown.DebugBonus)}");
        }

        lines.Add(
            $"  = ({Percent(breakdown.BasePart)} × {Number(breakdown.TalentMultiplier)})" +
            $" + {Percent(breakdown.AgeBonus)}" +
            (breakdown.DebugBonus != 0m ? $" + {Percent(breakdown.DebugBonus)}" : string.Empty) +
            $" = {Percent(breakdown.Rate)}");

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>自动治疗（需求 10）：把所有患病且未在治疗的成员一次性置入治疗。</summary>
    /// <returns>本次开始治疗的人数。</returns>
    public int AutoTreatAll()
    {
        var count = 0;

        foreach (var person in CountedMembers.Registered(State.Family).OrderBy(member => member.Id.Value).ToList())
        {
            if ((person.Status & StatusFlag.Ill) != StatusFlag.Ill
                || person.Timers.TreatmentRemainingMonths is not null)
            {
                continue;
            }

            var outcome = LifecycleEntry.StartTreatment(State, person);

            if (outcome.Accepted)
            {
                count++;
                Note($"✚ 自动治疗：{person.Name}（{IllnessPolicy.TreatmentMonths} 个月疗程）。");
            }
        }

        if (count > 0)
        {
            Changed?.Invoke();
        }

        return count;
    }

    /// <summary>百分比文本（<c>0.25 ⇒ 25%</c>）。</summary>
    private static string Percent(decimal value) =>
        (value * 100m).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%";

    /// <summary>系数文本（保留三位以内小数）。</summary>
    private static string Number(decimal value) =>
        value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    // ───────────────────────── 存档 ─────────────────────────

    public string Save()
    {
        try
        {
            _saves.Save(State, Slot, overwrite: true);
            Note($"◆ 已存档「{Slot.Name}」。");
            return string.Empty;
        }
        catch (SaveFileException ex)
        {
            return $"存档失败：{ex.Message}";
        }
    }

    public string SaveAs(string name, SaveFormat format)
    {
        try
        {
            var slot = new SaveSlot(name, format);

            // 另存为 = 以**新存档标识**复制一份（同一标识并存于两个档名是契约九 §5 禁止的状态，
            // 旧的「直接 Save」正是「存档标识已存在于另一个档名下」崩溃的来源）。
            State = _saves.SaveAsFork(State, slot);
            Slot = slot;
            _runner = new TurnRunner(_random, _saves, Slot, _names, _achievements);
            Note($"◆ 已另存为「{name}」（新存档标识）。");
            Changed?.Invoke();
            return string.Empty;
        }
        catch (SaveFileException ex)
        {
            return $"另存失败：{ex.Message}";
        }
    }

    /// <summary>仅改档名，存档标识不变（契约九 §5：改名 MUST NOT 变更 SaveId）。</summary>
    /// <param name="newName">新档名。</param>
    /// <returns>失败原因；成功为空串。</returns>
    public string Rename(string newName)
    {
        try
        {
            Slot = _saves.Rename(Slot, newName);
            _runner = new TurnRunner(_random, _saves, Slot, _names, _achievements);
            Note($"◆ 已改名为「{Slot.Name}」。");
            Changed?.Invoke();
            return string.Empty;
        }
        catch (SaveFileException ex)
        {
            return $"改名失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 删除一个存档主文件（2026-10-19 需求 9）。删除**当前档**时同时结束本会话。
    /// </summary>
    /// <param name="slot">档名 + 格式。</param>
    /// <returns>成功且确实删了 ⇒ 空串；否则为给玩家看的原因。</returns>
    public static string DeleteSave(SaveSlot slot)
    {
        try
        {
            var saves = new SaveService();
            return saves.Delete(slot) ? string.Empty : $"存档不存在：{slot.Name}";
        }
        catch (SaveFileException ex)
        {
            return $"删除失败：{ex.Message}";
        }
    }

    /// <summary>删除一个压缩备份（2026-10-19 需求 9）。</summary>
    /// <param name="slot">备份槽位。</param>
    /// <returns>成功且确实删了 ⇒ 空串；否则为原因。</returns>
    public static string DeleteBackup(SaveSlot slot)
    {
        try
        {
            var saves = new SaveService();
            return saves.DeleteBackup(slot) ? string.Empty : $"备份不存在：{slot.Name}";
        }
        catch (SaveFileException ex)
        {
            return $"删除备份失败：{ex.Message}";
        }
    }

    /// <summary>列出 <c>saves/backups</c> 下的全部压缩备份（2026-10-19 需求 15）。</summary>
    /// <returns>备份槽位（按档名升序）。</returns>
    public static IReadOnlyList<SaveSlot> ListBackups()
    {
        var saves = new SaveService();

        try
        {
            return saves.ListBackups();
        }
        catch (SaveFileException)
        {
            return [];
        }
    }

    /// <summary>从备份读档（2026-10-19 需求 15）：读回后即以此为当前档继续。</summary>
    /// <param name="slot">备份槽位。</param>
    /// <param name="seed">随机种子。</param>
    /// <returns>会话。</returns>
    public static GameSession LoadBackup(SaveSlot slot, int seed)
    {
        var random = new SeededRandomService(seed);
        var names = new SongStyleNameGenerator(random);
        var saves = new SaveService();
        var achievements = new AchievementStore(saves);
        var state = saves.LoadBackup(slot);
        var session = new GameSession(state, slot, saves, random, names, achievements);
        session.Note($"☆ 从备份读档 {slot.Name}：{state.Family.Name}家，{state.CurrentDate}。");
        return session;
    }

    public string Backup()
    {
        try
        {
            _saves.Backup(Slot);
            Note("◆ 已备份。");
            return string.Empty;
        }
        catch (SaveFileException ex)
        {
            return $"备份失败：{ex.Message}";
        }
    }

    public void SaveOnExit()
    {
        if (State.Ending.IsEnded)
        {
            return;
        }

        try
        {
            _runner.SaveOnExit(State);
        }
        catch (SaveFileException)
        {
            // 退出时静默失败：不阻断关闭流程。
        }
    }

    public AchievementArchive ReadArchive() => _achievements.Read();

    /// <summary>导出族流水账（规格书 §12.3「可导出 CSV」）。</summary>
    public string ExportLedgerCsv()
    {
        try
        {
            Directory.CreateDirectory(_saves.RootDirectory);
            var path = Path.Combine(_saves.RootDirectory, $"{Slot.Name}-流水账.csv");
            var lines = new List<string> { "年,月,类别,成员编号,金额（文）" };
            foreach (var entry in State.Economy.Ledger.Entries)
            {
                lines.Add(
                    $"{entry.Date.Year},{entry.Date.Month},{entry.Category},{entry.PersonId?.Value.ToString("D", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty},{entry.Amount.Wen.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }

            File.WriteAllLines(path, lines, System.Text.Encoding.UTF8);
            Note($"◆ 流水账已导出：{path}");
            return string.Empty;
        }
        catch (IOException ex)
        {
            return $"导出失败：{ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            return $"导出失败：{ex.Message}";
        }
    }
}
