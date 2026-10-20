using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Achievements;
using KFL.Rules.Config;
using KFL.Rules.FamilyLifecycle;

namespace KFL.Presentation;

public sealed partial class GameSession
{
    // ───────────────────────── 调试 ─────────────────────────

    public string DebugWake(int count)
    {
        var total = AchievementCounters.ReportsDebugWake(State, count);
        Note($"（调试）唤醒 ×{count}，累计 {total}。");
        Changed?.Invoke();
        return string.Empty;
    }

    /// <summary>
    /// 记录一次「打开调试控制台」的唤醒，不写编年史。
    /// </summary>
    /// <remarks>
    /// S-U-N-T-I-O-N 激活与反引号打开各调用一次（玩家需求：取代 <c>wake</c> 命令）。
    /// </remarks>
    public void DebugRecordWake() => AchievementCounters.ReportsDebugWake(State, 1);

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
        if ((person.Status & StatusFlag.Deceased) != StatusFlag.None)
        {
            return $"{person.Name} 已经死亡。";
        }

        var deaths = new DeathRegister();
        deaths.Add(person, DeathCause.Illness);
        DeathSettlement.Run(State, deaths, State.CurrentDate);
        Note($"（调试）{person.Name} 病故。");
        Changed?.Invoke();
        return $"{person.Name} 已死亡。";
    }

    /// <summary>调试：复活选中成员（仅清除死亡标记，不恢复婚姻 / 官职）。</summary>
    /// <param name="person">成员。</param>
    /// <returns>结果文本。</returns>
    public string DebugRevive(Person person)
    {
        if ((person.Status & StatusFlag.Deceased) == StatusFlag.None)
        {
            return $"{person.Name} 尚未死亡。";
        }

        person.Status &= ~StatusFlag.Deceased;
        Note($"（调试）{person.Name} 复活。");
        Changed?.Invoke();
        return $"{person.Name} 已复活。";
    }

    /// <summary>调试：令选中成员与其配偶立刻产下一名子女（不掷难产死亡）。</summary>
    /// <param name="person">父或母。</param>
    /// <returns>结果文本。</returns>
    public string DebugBirthNow(Person person)
    {
        if (!TryCoupleOf(person, out var couple, out var error))
        {
            return error;
        }

        var fact = Childbirth.DeliverNow(State, couple, _random, _names);
        if (fact is null)
        {
            return "生育失败：夫妇任一方已不在族中。";
        }

        State.Achievements.RecordBirths(1);
        var child = State.Family.TryGet(fact.PersonId);
        Note($"（调试）{person.Name} 与配偶立刻得{(fact.Gender == Gender.Male ? "子" : "女")} {child?.Name ?? "（新生儿）"}。");
        Changed?.Invoke();
        return $"已出生：{child?.Name ?? "（新生儿）"}（{(fact.Gender == Gender.Male ? "男" : "女")}）。";
    }

    /// <summary>调试：令选中成员与其配偶立刻产下一名指定性别与属性的子女（不掷难产死亡）。</summary>
    /// <param name="person">父或母。</param>
    /// <param name="gender">指定性别。</param>
    /// <param name="talents">指定四项天赋；<c>null</c> = 随父母遗传。</param>
    /// <param name="study">指定学业；<c>null</c> = 随父母遗传。</param>
    /// <param name="health">指定体质；<c>null</c> = 随父母遗传。</param>
    /// <param name="lifespan">指定寿数；<c>null</c> = 按性别掷骰。</param>
    /// <returns>结果文本。</returns>
    public string DebugBirthCustom(
        Person person,
        Gender gender,
        TalentSet? talents,
        int? study,
        int? health,
        int? lifespan)
    {
        if (!TryCoupleOf(person, out var couple, out var error))
        {
            return error;
        }

        var fact = Childbirth.DeliverNow(
            State,
            couple,
            _random,
            _names,
            gender,
            talents,
            study is null ? null : Math.Clamp(study.Value, 0, 100),
            health is null ? null : Math.Clamp(health.Value, 1, 100),
            lifespan is null ? null : Math.Max(1, lifespan.Value));
        if (fact is null)
        {
            return "生育失败：夫妇任一方已不在族中。";
        }

        State.Achievements.RecordBirths(1);
        var child = State.Family.TryGet(fact.PersonId);
        Note($"（调试）{person.Name} 与配偶立刻得{(fact.Gender == Gender.Male ? "子" : "女")} {child?.Name ?? "（新生儿）"}（指定属性）。");
        Changed?.Invoke();
        return $"已出生：{child?.Name ?? "（新生儿）"}（{(fact.Gender == Gender.Male ? "男" : "女")}）。";
    }

    /// <summary>调试：把选中成员的官阶直接设为指定级数；<c>null</c> = 罢为无官职。</summary>
    /// <param name="person">成员。</param>
    /// <param name="level">官阶级数 <c>1~18</c>（数字越小官越高）；<c>null</c> = 无官职。</param>
    /// <returns>结果文本。</returns>
    public string DebugSetRank(Person person, int? level)
    {
        if (level is not null and (< 1 or > 18))
        {
            return "用法：rank <1~18|none>";
        }

        if (level is null)
        {
            person.Rank = null;
            Note($"（调试）{person.Name} 罢为无官职。");
            Changed?.Invoke();
            return $"{person.Name} 已无官职。";
        }

        // 待阙状态与既有官阶互斥：先清计时、再清途径、最后清状态位（顺序不可颠倒）。
        if (person.Timers.AwaitingPostRemainingMonths is not null)
        {
            var timers = person.Timers;
            person.Timers = new StatusTimers(
                timers.SentenceRemainingMonths,
                timers.ExamBanRemainingMonths,
                timers.PromotionBanRemainingMonths,
                awaitingPostRemainingMonths: null,
                timers.IllnessSeverityBasisPoints,
                timers.TreatmentRemainingMonths);
        }

        person.EntryTrack = null;
        if ((person.Status & StatusFlag.AwaitingPost) != StatusFlag.None)
        {
            person.Status &= ~StatusFlag.AwaitingPost;
        }

        person.Rank = new OfficialRank(level.Value);
        person.MonthsInOffice = 0;
        Note($"（调试）{person.Name} 官阶设为 {level.Value} 级。");
        Changed?.Invoke();
        return $"{person.Name} 官阶 = {level.Value} 级。";
    }

    private bool TryCoupleOf(Person person, out CoupleKey couple, out string error)
    {
        couple = default;

        if ((person.Status & StatusFlag.Deceased) != StatusFlag.None)
        {
            error = $"{person.Name} 已死亡，无法生育。";
            return false;
        }

        if (person.SpouseId is not { } spouseId || State.Family.TryGet(spouseId) is not { } spouse)
        {
            error = $"{person.Name} 没有配偶。";
            return false;
        }

        if ((spouse.Status & StatusFlag.Deceased) != StatusFlag.None)
        {
            error = $"{spouse.Name} 已死亡，无法生育。";
            return false;
        }

        if (person.Gender == spouse.Gender)
        {
            error = "配偶性别相同，无法生育。";
            return false;
        }

        couple = person.Gender == Gender.Male
            ? new CoupleKey(person.Id, spouse.Id)
            : new CoupleKey(spouse.Id, person.Id);
        error = string.Empty;
        return true;
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
}
