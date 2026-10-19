using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Infrastructure.Save.Serialization;

/// <summary>
/// 存档级**一致性校验**二十二条（004-save-system；research R-04、data-model §3.11 第 ⑩ 步；
/// 006-bribery-punishment 追加第 11、12 条；007-family-lifecycle 追加第 13~15 条；
/// 008-achievements-extinction 追加第 16~22 条）。
/// </summary>
/// <remarks>
/// <para>
/// <b>唯一真源是 <c>KFL.Core</c> 自身的不变量</b>：本类型不复制字段级规则，只在复原完成后
/// 回答「这份状态是否自洽」。任一条不成立 ⇒
/// <see cref="SaveFileException"/>（<see cref="SaveFileErrorKind.InvalidStructure"/>）。
/// </para>
/// <para>
/// 第 1~7、9~22 条是**跨对象**的（单看一个对象的 setter 看不出「配偶是否互相指向」），
/// 故必须在整体复原之后才有意义；第 8 条是**同一份文件内**的自一致性检查
/// （头是主体标识的 O(1) 探针副本），由 <see cref="ValidateIdentity"/> 承担。
/// </para>
/// <para>
/// 第 16~22 条**明确不校验**：<c>PeakTotalAssets</c> / <c>HighestDegree</c> / <c>TotalJinShi</c> /
/// <c>AverageLifespan</c> 与任何重算派生值的一致性——它们是**不可重算的历史量**
/// （契约二十三「条款 33」）。
/// </para>
/// </remarks>
public static partial class SaveStateValidator
{
    /// <summary>校验复原后的状态（第 1~7、9~22 条）。</summary>
    /// <param name="state">已完整复原的状态。</param>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 <c>null</c>。</exception>
    /// <exception cref="SaveFileException">任一不成立（分类恒为 <see cref="SaveFileErrorKind.InvalidStructure"/>）。</exception>
    public static void Validate(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var members = state.Family.Members;

        EnsureUniqueMemberIds(members);
        EnsureSpouseConsistency(members);
        EnsureFormerSpouses(members);
        EnsureHeadIsRegistered(state.Family);
        EnsureTimersBackedByStatus(members);
        EnsureEntryTrackBackedByStatus(members);
        EnsureFamineConsistency(state.Economy);
        EnsureLedgerAmountsNonZero(state.Economy);
        EnsureExamProgressInRange(members);
        EnsureBribeCountsNonNegative(members);
        EnsureJournalEntriesReferenceMembers(state.Family);
        EnsureLifecycleProposalReferencesMember(state.Family);
        EnsurePendingBirthsApplied(state.Family);
        EnsureLifecycleCountersLegal(state.Family, state.Economy, state.CurrentDate);
        EnsureAchievementKeysUnique(state.Achievements);
        EnsurePendingUnlocksSubsetOfCompleted(state.Achievements);
        EnsureLifespansMatchDeceased(state.Achievements, members);
        EnsureAchievementCountersNonNegative(state.Achievements);
        EnsureEndingTripleConsistent(state.Ending);
        EnsureEndedImpliesNoRegisteredMembers(state);
        EnsureChronicleMatchesFamily(state);
    }

    /// <summary>校验第 8 条：头记录的 <c>SaveId</c> MUST 等于主体的 <c>GameState.Id</c>。</summary>
    /// <param name="header">已读出的头记录。</param>
    /// <param name="state">已完整复原的状态。</param>
    /// <exception cref="SaveFileException">两者不等（分类恒为 <see cref="SaveFileErrorKind.InvalidStructure"/>）。</exception>
    public static void ValidateIdentity(SaveFileHeader header, GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (header.SaveId != state.Id)
        {
            throw Invalid(
                $"头记录的 SaveId {header.SaveId} 与主体的 GameState.Id {state.Id} 不一致（契约十 §2 不变量）。");
        }
    }

    /// <summary>第 1 条：成员标识唯一。</summary>
    private static void EnsureUniqueMemberIds(IReadOnlyCollection<Person> members)
    {
        var seen = new HashSet<PersonId>();

        foreach (var member in members)
        {
            if (!seen.Add(member.Id))
            {
                throw Invalid($"成员标识 {member.Id} 重复（R-04 第 1 条）。");
            }
        }
    }

    /// <summary>第 2 条：配偶关系双向一致。</summary>
    private static void EnsureSpouseConsistency(IReadOnlyCollection<Person> members)
    {
        foreach (var member in members)
        {
            switch (member.SpouseId)
            {
                case null:
                    continue;

                case { } spouseId:
                    if (spouseId == member.Id)
                    {
                        throw Invalid($"成员 {member.Id} 的配偶指向自身（R-04 第 2 条）。");
                    }

                    var spouse = Find(members, spouseId)
                        ?? throw Invalid($"成员 {member.Id} 的配偶 {spouseId} 不是本家族成员（R-04 第 2 条）。");

                    if (spouse.SpouseId != member.Id)
                    {
                        throw Invalid(
                            $"配偶关系非双向：{member.Id} 指向 {spouseId}，而 {spouseId} 指向 {spouse.SpouseId?.ToString() ?? "null"}（R-04 第 2 条）。");
                    }

                    continue;
            }
        }
    }

    /// <summary>第 3 条：<c>FormerSpouseIds</c> 每项都指向本家族成员、不含自身、不含当前配偶、无重复。</summary>
    private static void EnsureFormerSpouses(IReadOnlyCollection<Person> members)
    {
        foreach (var member in members)
        {
            var seen = new HashSet<PersonId>();

            foreach (var formerSpouseId in member.FormerSpouseIds)
            {
                if (formerSpouseId == member.Id)
                {
                    throw Invalid($"成员 {member.Id} 的 FormerSpouseIds 含自身（R-04 第 3 条）。");
                }

                if (formerSpouseId == member.SpouseId)
                {
                    throw Invalid($"成员 {member.Id} 的 FormerSpouseIds 含当前配偶（R-04 第 3 条）。");
                }

                if (!seen.Add(formerSpouseId))
                {
                    throw Invalid($"成员 {member.Id} 的 FormerSpouseIds 有重复项 {formerSpouseId}（R-04 第 3 条）。");
                }

                if (Find(members, formerSpouseId) is null)
                {
                    throw Invalid(
                        $"成员 {member.Id} 的前任配偶 {formerSpouseId} 不是本家族成员（R-04 第 3 条）。");
                }
            }
        }
    }

    /// <summary>
    /// 第 4 条：<c>HeadId</c> 为 <c>null</c> 或指向在册成员；且「无在册男性 ⇒ <c>HeadId == null</c>」。
    /// </summary>
    /// <remarks>
    /// 后一半是 007 data-model §6 不变量 13（⑧-h 的职责）：家主只能空缺于家族再无在册男性之时，
    /// 有在册男性却空缺说明 ⑧-h 未执行。绝嗣本身**不**是本校验的事（不产生任何「绝嗣」状态）。
    /// </remarks>
    private static void EnsureHeadIsRegistered(Family family)
    {
        var hasRegisteredMale = family.RegisteredMembers.Any(member => member.Gender == Gender.Male);

        if (family.HeadId is not { } headId)
        {
            if (hasRegisteredMale)
            {
                throw Invalid($"有在册男性但家主为空（R-04 第 4 条、007 不变量 13）。");
            }

            return;
        }

        if (family.TryGet(headId) is null)
        {
            throw Invalid($"家主 {headId} 不是本家族成员（R-04 第 4 条）。");
        }

        if (!family.RegisteredMembers.Any(member => member.Id == headId))
        {
            throw Invalid($"家主 {headId} 不是在册成员（R-04 第 4 条）。");
        }
    }

    /// <summary>第 5 条：每个非空计时都对应其状态位为真（含 007 的患病位与疗程六路）。</summary>
    /// <remarks>
    /// 007 data-model §6 不变量 1/2：<c>Ill</c> 位与 <see cref="StatusTimers.IllnessSeverityBasisPoints"/>
    /// **同生同灭**（双向），且 <see cref="StatusTimers.TreatmentRemainingMonths"/> 只在 <c>Ill</c> 时非空。
    /// </remarks>
    private static void EnsureTimersBackedByStatus(IReadOnlyCollection<Person> members)
    {
        foreach (var member in members)
        {
            EnsureBacked(member, StatusFlag.ServingSentence, member.Timers.SentenceRemainingMonths, "服刑");
            EnsureBacked(member, StatusFlag.ExamBanned, member.Timers.ExamBanRemainingMonths, "禁考");
            EnsureBacked(member, StatusFlag.PromotionBanned, member.Timers.PromotionBanRemainingMonths, "禁升");
            EnsureBacked(member, StatusFlag.AwaitingPost, member.Timers.AwaitingPostRemainingMonths, "待阙");

            var ill = member.Status.HasFlag(StatusFlag.Ill);
            var severity = member.Timers.IllnessSeverityBasisPoints;

            if (ill != (severity is not null))
            {
                throw Invalid(
                    $"成员 {member.Id} 的「患病」位与疾病程度不同生同灭（Ill={ill}、s 非空={severity is not null}，R-04 第 5 条）。");
            }

            EnsureBacked(member, StatusFlag.Ill, member.Timers.TreatmentRemainingMonths, "疗程");
        }
    }

    /// <summary>第 6 条：<c>EntryTrack</c> 非空 ⇒ <c>AwaitingPost</c> 为真。</summary>
    private static void EnsureEntryTrackBackedByStatus(IReadOnlyCollection<Person> members)
    {
        foreach (var member in members)
        {
            if (member.EntryTrack is not null && !member.Status.HasFlag(StatusFlag.AwaitingPost))
            {
                throw Invalid($"成员 {member.Id} 的 EntryTrack 非空但「待阙」位为假（R-04 第 6 条）。");
            }
        }
    }

    /// <summary>第 7 条：饥馑「阶段为零 ⇔ 计时为零」，且阶段非零时计时 ≥ 1。</summary>
    private static void EnsureFamineConsistency(FamilyEconomy economy)
    {
        var stage = economy.Famine.Stage;
        var elapsedMonths = economy.Famine.ElapsedMonths;

        if (stage == FamineStage.None)
        {
            if (elapsedMonths != 0)
            {
                throw Invalid($"饥馑阶段为零但计时为 {elapsedMonths}（R-04 第 7 条）。");
            }

            return;
        }

        if (elapsedMonths < 1)
        {
            throw Invalid($"饥馑阶段为 {stage} 但计时为 {elapsedMonths}（R-04 第 7 条：非零阶段计时 MUST >= 1）。");
        }
    }

    /// <summary>第 9 条：账本每一条资金类条目金额非零。</summary>
    private static void EnsureLedgerAmountsNonZero(FamilyEconomy economy)
    {
        foreach (var entry in economy.Ledger.Entries)
        {
            if (LedgerCategoryMetadata.KindOf(entry.Category) != LedgerEntryKind.Treasury)
            {
                continue;
            }

            if (entry.Amount == Money.Zero)
            {
                throw Invalid($"账本第 {entry.Date.Year} 年 {entry.Date.Month} 月的资金类条目 {entry.Category} 金额为 0（R-04 第 9 条）。");
            }
        }
    }

    private static void EnsureBacked(Person member, StatusFlag flag, int? timer, string what)
    {
        if (timer is not null && !member.Status.HasFlag(flag))
        {
            throw Invalid($"成员 {member.Id} 的{what}计时非空但状态位为假（R-04 第 5 条）。");
        }
    }

    private static Person? Find(IReadOnlyCollection<Person> members, PersonId id)
    {
        foreach (var member in members)
        {
            if (member.Id == id)
            {
                return member;
            }
        }

        return null;
    }

    private static SaveFileException Invalid(string message) =>
        new(SaveFileErrorKind.InvalidStructure, message);
}
