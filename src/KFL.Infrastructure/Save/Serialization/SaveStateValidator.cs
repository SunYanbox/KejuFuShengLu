using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Infrastructure.Save.Serialization;

/// <summary>
/// 存档级**一致性校验**十二条（004-save-system；research R-04、data-model §3.11 第 ⑩ 步；
/// 006-bribery-punishment 追加第 11、12 条）。
/// </summary>
/// <remarks>
/// <para>
/// <b>唯一真源是 <c>KFL.Core</c> 自身的不变量</b>：本类型不复制字段级规则，只在复原完成后
/// 回答「这份状态是否自洽」。任一条不成立 ⇒
/// <see cref="SaveFileException"/>（<see cref="SaveFileErrorKind.InvalidStructure"/>）。
/// </para>
/// <para>
/// 第 1~7、9~12 条是**跨对象**的（单看一个对象的 setter 看不出「配偶是否互相指向」），
/// 故必须在整体复原之后才有意义；第 8 条是**同一份文件内**的自一致性检查
/// （头是主体标识的 O(1) 探针副本），由 <see cref="ValidateIdentity"/> 承担。
/// </para>
/// </remarks>
public static class SaveStateValidator
{
    /// <summary>校验复原后的状态（第 1~7、9~12 条）。</summary>
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

    /// <summary>第 4 条：<c>HeadId</c> 为 <c>null</c> 或指向在册成员。</summary>
    private static void EnsureHeadIsRegistered(Family family)
    {
        if (family.HeadId is not { } headId)
        {
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

    /// <summary>第 5 条：每个非空计时都对应其状态位为真。</summary>
    private static void EnsureTimersBackedByStatus(IReadOnlyCollection<Person> members)
    {
        foreach (var member in members)
        {
            EnsureBacked(member, StatusFlag.ServingSentence, member.Timers.SentenceRemainingMonths, "服刑");
            EnsureBacked(member, StatusFlag.ExamBanned, member.Timers.ExamBanRemainingMonths, "禁考");
            EnsureBacked(member, StatusFlag.PromotionBanned, member.Timers.PromotionBanRemainingMonths, "禁升");
            EnsureBacked(member, StatusFlag.AwaitingPost, member.Timers.AwaitingPostRemainingMonths, "待阙");
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

    /// <summary>第 10 条：科举进度合法（005-exam-cycle；契约十四 §10）。</summary>
    /// <remarks>
    /// 两个失败数 MUST <c>&gt;= 0</c>；报名级别非空时 MUST 是 <c>JieShi</c> 或 <c>ShengShi</c>
    /// ——殿试由省试及第自动进入，**不是**可报名级别。判据同时覆盖二进制侧的越界整数
    /// （<c>0</c> 与 <c>&gt; 3</c>），JSON 侧认不得的**名字**在第 ⑤ 步即以
    /// <see cref="SaveFileErrorKind.Malformed"/> 失败。
    /// </remarks>
    private static void EnsureExamProgressInRange(IReadOnlyCollection<Person> members)
    {
        foreach (var member in members)
        {
            if (member.JieShiFailureCount < 0 || member.ShengShiFailureCount < 0)
            {
                throw Invalid(
                    $"成员 {member.Id} 的科举失败数为负（解试 {member.JieShiFailureCount}、省试 {member.ShengShiFailureCount}，R-04 第 10 条）。");
            }

            if (member.AppliedExam is { } applied &&
                applied is not ExamLevel.JieShi and not ExamLevel.ShengShi)
            {
                throw Invalid(
                    $"成员 {member.Id} 的报名级别 {applied} 不是合法的报名态（只允许 JieShi 与 ShengShi，R-04 第 10 条）。");
            }
        }
    }

    /// <summary>第 11 条：三个行贿计数 MUST <c>&gt;= 0</c>（006-bribery-punishment；data-model §6 I-1）。</summary>
    /// <remarks>
    /// 覆盖二进制侧的越界整数（JSON 侧为负数同样落在这里）；正数**无上限**——行贿次数
    /// 本身没有封顶，唯一的「封顶」是触顶拒绝（契约十五 §4.2）。
    /// </remarks>
    private static void EnsureBribeCountsNonNegative(IReadOnlyCollection<Person> members)
    {
        foreach (var member in members)
        {
            if (member.JieShiBribeCount < 0 || member.ShengShiBribeCount < 0 || member.DianShiBribeCount < 0)
            {
                throw Invalid(
                    $"成员 {member.Id} 的行贿计数为负（解试 {member.JieShiBribeCount}、省试 {member.ShengShiBribeCount}、殿试 {member.DianShiBribeCount}，R-04 第 11 条）。");
            }
        }
    }

    /// <summary>第 12 条：日志的每一行 <c>PersonId</c> MUST 存在于 <c>Family.Members</c>（含归档者）。</summary>
    /// <remarks>
    /// 覆盖八个位置（六类事实行）：行贿的考生、服刑/罚金/释放的当事人、亲属禁令与连坐的当事人及亲属
    /// （006-bribery-punishment；data-model §6 I-15、契约十六 §5）。
    /// </remarks>
    private static void EnsureJournalEntriesReferenceMembers(Family family)
    {
        var members = family.Members;
        var journal = family.PunishmentJournal;

        foreach (var fact in journal.Bribes)
        {
            EnsureMember(fact.Examinee, "行贿的考生");
        }

        foreach (var fact in journal.Sentences)
        {
            EnsureMember(fact.Subject, "服刑的当事人");
        }

        foreach (var fact in journal.Fines)
        {
            EnsureMember(fact.Subject, "罚金的当事人");
        }

        foreach (var fact in journal.RelativeBans)
        {
            EnsureMember(fact.Subject, "亲属禁令的当事人");
            EnsureMember(fact.Relative, "亲属禁令的亲属");
        }

        foreach (var fact in journal.CollectivePunishments)
        {
            EnsureMember(fact.Subject, "连坐的当事人");
            EnsureMember(fact.Relative, "连坐的亲属");
        }

        foreach (var fact in journal.Releases)
        {
            EnsureMember(fact.Subject, "释放的当事人");
        }

        void EnsureMember(PersonId id, string what)
        {
            if (Find(members, id) is null)
            {
                throw Invalid($"{what} {id} 不是本家族成员（R-04 第 12 条）。");
            }
        }
    }

    /// <summary>
    /// 第 13 条：待应答提议的 <c>OurMemberId</c> MUST 指向本家族的在册未婚成员（007-family-lifecycle）。
    /// </summary>
    /// <remarks>
    /// data-model §6 不变量 3：提议至多一条，且 <c>OurMemberId</c> MUST 指向在册成员
    /// （已亡与外嫁者 MUST NOT 承载提议）。
    /// </remarks>
    private static void EnsureLifecycleProposalReferencesMember(Family family)
    {
        if (family.Lifecycle.Proposal is not { } proposal)
        {
            return;
        }

        var member = Find(family.Members, proposal.OurMemberId)
            ?? throw Invalid($"提议的 OurMemberId {proposal.OurMemberId} 不是本家族成员（R-04 第 13 条）。");

        if ((member.Status & (StatusFlag.Deceased | StatusFlag.MarriedOut)) != StatusFlag.None)
        {
            throw Invalid(
                $"提议的 OurMemberId {proposal.OurMemberId} 指向已亡或外嫁的成员（R-04 第 13 条）。");
        }

        if (member.SpouseId is not null)
        {
            throw Invalid($"提议的 OurMemberId {proposal.OurMemberId} 已有配偶（R-04 第 13 条）。");
        }
    }

    /// <summary>
    /// 第 14 条：每条待分娩记录都 MUST 出自同年的已申请夫妇集合（007-family-lifecycle）。
    /// </summary>
    /// <remarks>
    /// data-model §6 不变量：<c>PendingBirths ⊆ AppliedBirthCouples</c>。簿记在跨年时会同时清空两侧，
    /// 故本条只在被清空到一半的损坏档上生效。
    /// </remarks>
    private static void EnsurePendingBirthsApplied(Family family)
    {
        var lifecycle = family.Lifecycle;

        foreach (var birth in lifecycle.PendingBirths)
        {
            if (!lifecycle.AppliedBirthCouples.Contains(birth.Couple))
            {
                throw Invalid(
                    $"待分娩记录的夫妇（夫 {birth.Couple.HusbandId}、妻 {birth.Couple.WifeId}）不在已申请本年生育的夫妇集合内（R-04 第 14 条）。");
            }

            EnsureMember(birth.Couple.HusbandId, "待分娩记录的夫");
            EnsureMember(birth.Couple.WifeId, "待分娩记录的妻");
        }

        foreach (var couple in lifecycle.AppliedBirthCouples)
        {
            EnsureMember(couple.HusbandId, "已申请生育的夫");
            EnsureMember(couple.WifeId, "已申请生育的妻");
        }

        void EnsureMember(PersonId id, string what)
        {
            if (Find(family.Members, id) is null)
            {
                throw Invalid($"{what} {id} 不是本家族成员（R-04 第 14 条）。");
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
