using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;
using KFL.Rules.Economy;
using KFL.Rules.FamilyLifecycle;
using KFL.Rules.Punishment;
using KFL.Rules.Settlement;

namespace KFL.Rules.Achievements;

/// <summary>
/// 68 条成就的纯谓词（契约二十二 §2 判据表；data-model §3.3~§3.5）。
/// </summary>
/// <remarks>
/// <para>
/// **唯一分派点**：<see cref="IsSatisfied"/> 是判据的唯一入口，每条成就分派到**一个**纯谓词
/// （契约二十二 「条款 3」）。谓词 MUST NOT 写状态、MUST NOT 消费随机、MUST NOT 访问
/// 时钟 / 文件 / 网络 —— 它们只读 <see cref="AchievementContext"/>。
/// </para>
/// <para>
/// **阈值口径**：判据里的每个阈值都取自 <see cref="AchievementPolicy"/>，本文件与
/// <c>AchievementCatalog</c> 里 MUST NOT 出现裸阈值字面量（「条款 4」）。仅结构性下标
/// （代际 0/1/2）与集合存在性判断在本地直接书写。
/// </para>
/// <para>
/// **成员口径**：除逐字写「在册」者（<see cref="AchievementId.ThreeGenerations"/> /
/// <see cref="AchievementId.FiveGenerations"/> / <see cref="AchievementId.OfficeHolders"/> /
/// <see cref="AchievementId.LargeHousehold"/>）外，一律遍历 <c>Family.Members</c> **全量**
/// 成员（含已亡与外嫁归档者，「条款 5」）。
/// </para>
/// <para>
/// **两条由判绝嗣步骤补判**：<see cref="AchievementId.FamilyExtinct"/> 与
/// <see cref="AchievementId.HellEnding"/> 的成立条件含「绝嗣触发」这一**状态迁移**而非状态本身，
/// 故此处恒 <c>false</c>，由 <c>ExtinctionJudge.TryTrigger</c> 补判（契约二十四 §4）。
/// <see cref="AchievementId.TradeLoss"/> 为「待接线」条目（规格书 §14.2），判据恒 <c>false</c>：
/// 经商亏损事件尚未接线（data-model §9）。
/// </para>
/// <para>
/// **存续年数**：<c>SurvivedYears = 当前年 − 1</c>（规格书 §14.1 起算口径；data-model §3.6）。
/// </para>
/// </remarks>
public static class AchievementPredicates
{
    /// <summary>判定单条成就是否成立。</summary>
    /// <param name="id">成就标识。</param>
    /// <param name="ctx">判定上下文（状态面 + 当月报告 + 全局档案）。</param>
    /// <returns>成立为 <c>true</c>；<paramref name="id"/> 未登记时抛 <see cref="ArgumentOutOfRangeException"/>。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> 不是已登记的成就标识。</exception>
    public static bool IsSatisfied(AchievementId id, AchievementContext ctx) => id switch
    {
        // 系列一：初露锋芒（ACH-001~012）
        AchievementId.FoundationLaid => true,
        AchievementId.FirstJuRen => HasDegreeRecord(
            ctx, record => record.Cause == DegreeChangeCause.ExamPass && record.Level >= DegreeLevel.JuRen),
        AchievementId.FirstJinShi => HasDegreeRecord(
            ctx, record => record.Cause == DegreeChangeCause.ExamPass && record.Level == DegreeLevel.JinShi),
        AchievementId.GoldenList => HasDegreeRecord(ctx, record => IsTopPlacement(record.Placement)),
        AchievementId.ThreeGenerations => ThreeGenerationsAlive(ctx),
        AchievementId.FiveGenerations => AnyRegistered(
            ctx, person => person.Generation >= AchievementPolicy.FiveGenerationsMinGeneration),
        AchievementId.TenThousandGuan => AtLeastGuan(Assets(ctx.State), AchievementPolicy.RichAssetsGuan),
        AchievementId.WealthOfTheRegion => AtLeastGuan(Assets(ctx.State), AchievementPolicy.WealthyAssetsGuan),
        AchievementId.Longevity => AnyMember(ctx, person => person.AgeAt(ctx.Now) >= AchievementPolicy.LongevityAge),
        AchievementId.CleanRecord => AnyMember(ctx, PunishmentQueries.WasBribeFreeAtImperialPass),
        AchievementId.PrisonRelease => InMonth(ctx, settlement => settlement.Punishment.Releases.Count > 0),
        AchievementId.NarrowEscape => ctx.State.Difficulty == Difficulty.Hell
            && SurvivedYears(ctx.Now) >= AchievementPolicy.NarrowEscapeSurvivedYears,

        // 系列二：科名累进（ACH-013~020）
        AchievementId.JuRenThree => CountWithExamPass(ctx, DegreeLevel.JuRen)
            >= AchievementPolicy.JuRenThreeThreshold,
        AchievementId.JuRenTen => CountWithExamPass(ctx, DegreeLevel.JuRen)
            >= AchievementPolicy.JuRenTenThreshold,
        AchievementId.JuRenHundred => CountWithExamPass(ctx, DegreeLevel.JuRen)
            >= AchievementPolicy.JuRenHundredThreshold,
        AchievementId.JuRenThousand => CountWithExamPass(ctx, DegreeLevel.JuRen)
            >= AchievementPolicy.JuRenThousandThreshold,
        AchievementId.JinShiThree => CountWithExamPass(ctx, DegreeLevel.JinShi)
            >= AchievementPolicy.JinShiThreeThreshold,
        AchievementId.JinShiTen => CountWithExamPass(ctx, DegreeLevel.JinShi)
            >= AchievementPolicy.JinShiTenThreshold,
        AchievementId.JinShiHundred => CountWithExamPass(ctx, DegreeLevel.JinShi)
            >= AchievementPolicy.JinShiHundredThreshold,
        AchievementId.JinShiThousand => CountWithExamPass(ctx, DegreeLevel.JinShi)
            >= AchievementPolicy.JinShiThousandThreshold,

        // 系列三：宦海沉浮（ACH-021~031）
        AchievementId.FirstAppointment => InMonth(ctx, settlement => settlement.Career.Appointments.Count > 0),
        AchievementId.Prefect => InMonth(
            ctx,
            settlement => settlement.Career.Promotions.Any(
                promotion => promotion.To.Level >= AchievementPolicy.PrefectRankMin
                    && promotion.To.Level <= AchievementPolicy.PrefectRankMax)),
        AchievementId.Chancellor => AnyMember(
            ctx, person => person.Rank is { } rank && rank.Level == SalaryTable.HighestLevel),
        AchievementId.Retirement => InMonth(ctx, settlement => settlement.Career.Retirements.Count > 0),
        AchievementId.OfficeHolders => CountRegistered(ctx, person => person.Rank is not null)
            >= AchievementPolicy.OfficeHoldersCount,
        AchievementId.Merit => AnyMember(ctx, person => person.Merit >= AchievementPolicy.MeritThreshold),
        AchievementId.JieShiFailuresThree => AnyMember(
            ctx, person => person.JieShiFailureCount >= ExamPolicy.JieShiExemptionFailureThreshold),
        AchievementId.VenerableScholar => AnyMember(
            ctx,
            person => person.AgeAt(ctx.Now) >= AchievementPolicy.VenerableStudyAge
                && person.ShengShiFailureCount >= AchievementPolicy.VenerableShengShiFailures
                && person.EntryTrack == AppointmentTrack.SpecialTribute),
        AchievementId.ImperialSweep => HasAllTopPlacements(ctx),
        AchievementId.ExamFailuresTwenty => TotalExamFailures(ctx) >= AchievementPolicy.ExamFailuresPileUp,
        AchievementId.FallenAtShengShi => FallenAtShengShi(ctx),

        // 系列四：宗族血脉（ACH-032~048）
        AchievementId.LargeHousehold => ctx.State.Family.RegisteredMembers.Count
            >= AchievementPolicy.LargeHouseholdMembers,
        AchievementId.FiftyBirths => ctx.State.Achievements.BirthCount >= AchievementPolicy.ClanBirthCount,
        AchievementId.ImperialGroomPick => SawImperialProposal(ctx),
        // ACH-035 秦晋之好：判据表写「当月 MarriageFact.IsDegreeMarriage」，但 007 的 ⑧-a 只写
        // `Lifecycle.Proposal`、**从不把婚配写进快照的 Marriages**（PopulationAdvance 只填
        // PendingProposal/Births/Illnesses/Crossings/Deaths）⇒ 当月面恒空。按 FR-008「同时消费
        // 状态面与当月报告面」补**状态面兜底**：家族台账里出现过「买功名婚姻」的支出或收入
        // （娶入 = DegreeMarriageFeePaid；嫁出 = DegreeMarriageDowryReceived），与规格书 §14.1
        // 「完成一次买功名婚姻（娶入或嫁出）」的历史口径一致（勘误 E-24）。
        AchievementId.DegreeMarriage => InMonth(
                ctx, settlement => settlement.Lifecycle.Marriages.Any(marriage => marriage.IsDegreeMarriage))
            || HasDegreeMarriageRecord(ctx.State),
        AchievementId.DeathInChildbirth => InMonth(
            ctx,
            settlement => settlement.Lifecycle.Deaths.Any(
                death => death.Cause == DeathCause.Childbirth && IsFemale(ctx.State, death.PersonId))),
        AchievementId.InfantDeath => AnyInfantDeath(ctx),
        AchievementId.FarmlandHundred => ctx.State.Economy.Holdings.FarmlandMu
            >= AchievementPolicy.FarmlandHundredMu,
        AchievementId.FarmlandFiveHundred => ctx.State.Economy.Holdings.FarmlandMu
            >= AchievementPolicy.FarmlandFiveHundredMu,
        AchievementId.FirstShop => ctx.State.Economy.Holdings.Shops > 0,
        AchievementId.TradeLoss => false,
        AchievementId.DebtBurden => ctx.State.Economy.Treasury.Loan.Principal
            > Money.FromGuan(AchievementPolicy.DebtBurdenGuan),
        AchievementId.DebtFree => ctx.State.Achievements.EverHadLoan
            && ctx.State.Economy.Treasury.Loan.IsSettled,
        AchievementId.FamilyExtinct => false,
        AchievementId.CenturyHouse => !ExtinctionJudge.IsExtinct(ctx.State)
            && SurvivedYears(ctx.Now) > AchievementPolicy.CenturyFamilySurvivedYears,
        AchievementId.MillenniumHouse => SurvivedYears(ctx.Now)
            > AchievementPolicy.MillenniumFamilySurvivedYears,
        AchievementId.AllFourOrigins => HasAllFourOrigins(ctx),
        AchievementId.HellEnding => false,

        // 系列五：覆巢之下（ACH-049~059）
        AchievementId.FirstSuccessfulBribe => InMonth(
            ctx, settlement => settlement.Punishment.Bribes.Any(bribe => !bribe.Caught)),
        AchievementId.BribeThree => ctx.State.Achievements.SuccessfulBribeCount
            >= AchievementPolicy.BribeThreeThreshold,
        AchievementId.BribeTen => ctx.State.Achievements.SuccessfulBribeCount
            >= AchievementPolicy.BribeTenThreshold,
        AchievementId.BribeHundred => ctx.State.Achievements.SuccessfulBribeCount
            >= AchievementPolicy.BribeHundredThreshold,
        AchievementId.BribeThousand => ctx.State.Achievements.SuccessfulBribeCount
            >= AchievementPolicy.BribeThousandThreshold,
        AchievementId.BribeCapReached => InMonth(
            ctx, settlement => settlement.Punishment.Bribes.Any(bribe => bribe.CapReached)),
        AchievementId.ExecutedForBribe => ExecutedForBribe(ctx),
        AchievementId.CollectivePunishment => InMonth(
            ctx,
            settlement => settlement.Punishment.CollectivePunishments.Any(
                fact => fact.Outcome == CollectiveOutcome.Executed
                    || fact.Outcome == CollectiveOutcome.DegreeDemoted)),
        AchievementId.MarriedOutPunished => MarriedOutPunished(ctx),
        AchievementId.FamilyWideBan => InMonth(ctx, settlement => settlement.Punishment.FamilyBanDeclared),
        AchievementId.ThreePrisoners => InMonth(
            ctx, settlement => settlement.Punishment.PrisonerCount >= AchievementPolicy.PrisonerCount),

        // 系列六：饥馑求生（ACH-060~063）
        AchievementId.FirstRelief => InMonth(
            ctx,
            settlement => settlement.FamineAfter.Stage == FamineStage.Relief
                && settlement.FamineBefore.Stage != FamineStage.Relief),
        AchievementId.ReliefRecoveredPaidInFull => InMonth(
            ctx,
            settlement => settlement.FamineBefore.Stage == FamineStage.Relief
                && settlement.FamineAfter.Stage != FamineStage.Relief
                && settlement.LivingCostPaid == settlement.LivingCostPayable),
        AchievementId.ReliefRecoveredShortfall => InMonth(
            ctx,
            settlement => settlement.FamineBefore.Stage == FamineStage.Relief
                && settlement.FamineAfter.Stage != FamineStage.Relief
                && settlement.LivingCostPaid < settlement.LivingCostPayable),
        AchievementId.FamineDeath => InMonth(
            ctx, settlement => settlement.Lifecycle.Deaths.Any(death => death.Cause == DeathCause.Famine)),

        // 系列七：调试秘辛（ACH-064~068）
        AchievementId.DebugWakeOne => ctx.State.Achievements.DebugWakeCount
            >= AchievementPolicy.DebugWakeOne,
        AchievementId.DebugWakeTen => ctx.State.Achievements.DebugWakeCount
            >= AchievementPolicy.DebugWakeTen,
        AchievementId.DebugWakeHundred => ctx.State.Achievements.DebugWakeCount
            >= AchievementPolicy.DebugWakeHundred,
        AchievementId.DebugWakeThousand => ctx.State.Achievements.DebugWakeCount
            >= AchievementPolicy.DebugWakeThousand,
        AchievementId.DebugWakeTenThousand => ctx.State.Achievements.DebugWakeCount
            >= AchievementPolicy.DebugWakeTenThousand,

        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "未登记的成就标识。"),
    };

    private static bool InMonth(AchievementContext ctx, Func<SettlementResult, bool> predicate) =>
        ctx.Settlement is { } settlement && predicate(settlement);

    private static Money Assets(GameState state) => TotalAssetsCalculator.Of(state.Economy);

    private static int SurvivedYears(GameDate now) => now.Year - 1;

    private static bool AtLeastGuan(Money value, int guan) => !(value < Money.FromGuan(guan));

    private static bool AnyMember(AchievementContext ctx, Func<Person, bool> predicate)
    {
        foreach (var person in ctx.State.Family.Members)
        {
            if (predicate(person))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AnyRegistered(AchievementContext ctx, Func<Person, bool> predicate)
    {
        foreach (var person in ctx.State.Family.RegisteredMembers)
        {
            if (predicate(person))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountMembers(AchievementContext ctx, Func<Person, bool> predicate)
    {
        var count = 0;
        foreach (var person in ctx.State.Family.Members)
        {
            if (predicate(person))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountRegistered(AchievementContext ctx, Func<Person, bool> predicate)
    {
        var count = 0;
        foreach (var person in ctx.State.Family.RegisteredMembers)
        {
            if (predicate(person))
            {
                count++;
            }
        }

        return count;
    }

    private static bool HasDegreeRecord(AchievementContext ctx, Func<DegreeRecord, bool> predicate)
    {
        foreach (var person in ctx.State.Family.Members)
        {
            foreach (var record in person.DegreeHistory)
            {
                if (predicate(record))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int CountWithExamPass(AchievementContext ctx, DegreeLevel level) =>
        CountMembers(ctx, person => HasExamPass(person, level));

    private static bool HasExamPass(Person person, DegreeLevel level)
    {
        foreach (var record in person.DegreeHistory)
        {
            if (record.Cause == DegreeChangeCause.ExamPass && record.Level >= level)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTopPlacement(ImperialPlacement? placement) =>
        placement is ImperialPlacement.ZhuangYuan
            or ImperialPlacement.BangYan
            or ImperialPlacement.TanHua;

    private static bool ThreeGenerationsAlive(AchievementContext ctx)
    {
        var founder = false;
        var child = false;
        var grandchild = false;
        foreach (var person in ctx.State.Family.RegisteredMembers)
        {
            switch (person.Generation)
            {
                case 0:
                    founder = true;
                    break;
                case 1:
                    child = true;
                    break;
                case 2:
                    grandchild = true;
                    break;
                default:
                    break;
            }
        }

        return founder && child && grandchild;
    }

    private static bool HasAllTopPlacements(AchievementContext ctx)
    {
        var zhuangYuan = false;
        var bangYan = false;
        var tanHua = false;
        foreach (var person in ctx.State.Family.Members)
        {
            foreach (var record in person.DegreeHistory)
            {
                switch (record.Placement)
                {
                    case ImperialPlacement.ZhuangYuan:
                        zhuangYuan = true;
                        break;
                    case ImperialPlacement.BangYan:
                        bangYan = true;
                        break;
                    case ImperialPlacement.TanHua:
                        tanHua = true;
                        break;
                    default:
                        break;
                }
            }
        }

        return zhuangYuan && bangYan && tanHua;
    }

    private static int TotalExamFailures(AchievementContext ctx)
    {
        var total = 0;
        foreach (var person in ctx.State.Family.Members)
        {
            total += person.JieShiFailureCount + person.ShengShiFailureCount;
        }

        return total;
    }

    private static bool FallenAtShengShi(AchievementContext ctx)
    {
        if (ctx.Settlement is not { } settlement)
        {
            return false;
        }

        foreach (var outcome in settlement.ExamSeason.Outcomes)
        {
            if (outcome.Level != ExamLevel.ShengShi || outcome.Passed)
            {
                continue;
            }

            if (FindMember(ctx.State, outcome.PersonId) is { } person
                && person.Study >= AchievementPolicy.GeniusStudy)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// ACH-035 的**状态面兜底**：家族台账里出现过「买功名婚姻」的款项（娶入
    /// <see cref="LedgerCategory.DegreeMarriageFeePaid"/> / 嫁出
    /// <see cref="LedgerCategory.DegreeMarriageDowryReceived"/>）。
    /// 台账是 005 起就随档持久化的既有数据，**不是**新派生量（FR-003 只禁派生谓词落盘）。
    /// </summary>
    /// <param name="state">游戏状态。</param>
    /// <returns>历史上完成过买功名婚姻时为 <c>true</c>。</returns>
    private static bool HasDegreeMarriageRecord(GameState state)
    {
        foreach (var entry in state.Economy.Ledger.Entries)
        {
            if (entry.Category is LedgerCategory.DegreeMarriageFeePaid
                or LedgerCategory.DegreeMarriageDowryReceived)
            {
                return true;
            }
        }

        return false;
    }

    private static bool SawImperialProposal(AchievementContext ctx)
    {
        if (ctx.Settlement is { } settlement
            && settlement.Lifecycle.PendingProposal?.Kind == MarriageProposalKind.ImperialProposal)
        {
            return true;
        }

        return LifecycleQueries.PendingProposal(ctx.State.Family, ctx.Now)?.Kind
            == MarriageProposalKind.ImperialProposal;
    }

    private static bool AnyInfantDeath(AchievementContext ctx)
    {
        if (ctx.Settlement is not { } settlement)
        {
            return false;
        }

        foreach (var death in settlement.Lifecycle.Deaths)
        {
            if (FindMember(ctx.State, death.PersonId) is { } person
                && person.AgeAt(settlement.Month) < AchievementPolicy.InfantDeathMaxAge)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFemale(GameState state, PersonId id) =>
        FindMember(state, id)?.Gender == Gender.Female;

    private static bool ExecutedForBribe(AchievementContext ctx)
    {
        if (ctx.Settlement is not { } settlement)
        {
            return false;
        }

        var cell = PunishmentPolicy.CellOf(ctx.State.Difficulty, ExamLevel.DianShi);
        if (cell.Execution != ExecutionKind.ImmediateSentence)
        {
            return false;
        }

        return settlement.Punishment.Bribes.Any(
            bribe => bribe.Caught && bribe.Level == ExamLevel.DianShi);
    }

    private static bool MarriedOutPunished(AchievementContext ctx)
    {
        if (ctx.Settlement is not { } settlement)
        {
            return false;
        }

        foreach (var ban in settlement.Punishment.RelativeBans)
        {
            if (IsMarriedOut(ctx.State, ban.Relative))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMarriedOut(GameState state, PersonId id) =>
        FindMember(state, id) is { } person && person.Status.HasFlag(StatusFlag.MarriedOut);

    private static bool HasAllFourOrigins(AchievementContext ctx)
    {
        var origins = new HashSet<Origin>();
        foreach (var record in ctx.ArchiveOrigins)
        {
            origins.Add(record.Origin);
        }

        return origins.Contains(Origin.Farmer)
            && origins.Contains(Origin.Artisan)
            && origins.Contains(Origin.Merchant)
            && origins.Contains(Origin.Scholar);
    }

    private static Person? FindMember(GameState state, PersonId id)
    {
        if (state.Family.TryGet(id) is { } registered)
        {
            return registered;
        }

        foreach (var person in state.Family.ArchivedMembers)
        {
            if (person.Id == id)
            {
                return person;
            }
        }

        return null;
    }
}
