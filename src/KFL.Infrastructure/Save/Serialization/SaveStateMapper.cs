using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Infrastructure.Save.Serialization;

/// <summary>
/// 领域状态 ↔ 存档 DTO 的**逐字段显式映射**（004-save-system；data-model §3/§3.11）。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么不用反射/序列化框架直接映射领域类型</b>：那会让 <c>KFL.Core</c> 反过来依赖序列化
/// 框架，并要求领域类型带特性（FR-020、data-model 不变量 2）。逐字段写虽然长，但「哪些字段落盘」
/// 从此可以在一个文件里数出来。
/// </para>
/// <para>
/// <b>复原次序是本类型的核心约束</b>：<c>KFL.Core</c> 的各 setter 之间有真实的交叉校验
/// （状态位 ⇄ 计时 ⇄ 入仕途径、储蓄利率与其年份、父母引用、功名非降序、配偶双向），
/// 次序错就会被自家不变量拒绝。次序见 data-model §3.11，MUST NOT 调整。
/// </para>
/// <para>
/// <b>不做一致性裁决</b>：本类型只负责还原；「这份状态是否自洽」由第 ⑩ 步的
/// <see cref="SaveStateValidator"/> 回答。
/// </para>
/// </remarks>
public static partial class SaveStateMapper
{
    /// <summary>本版本已定义的九位状态标记（更高位属未定义，入档即结构错误）。</summary>
    private const StatusFlag KnownStatusFlags =
        StatusFlag.Ill | StatusFlag.Famine | StatusFlag.ServingSentence | StatusFlag.AwaitingPost |
        StatusFlag.ExamBanned | StatusFlag.PromotionBanned | StatusFlag.MarriedOut | StatusFlag.Retired |
        StatusFlag.Deceased;

    /// <summary>把领域状态拍成 DTO。</summary>
    /// <param name="state">领域状态。</param>
    /// <returns>瞬态 DTO。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 <c>null</c>。</exception>
    public static SaveStateDto ToDto(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var family = state.Family;
        var bookkeeping = family.CaptureOutsiderBookkeeping();

        return new SaveStateDto
        {
            Id = state.Id,
            CurrentYear = state.CurrentDate.Year,
            CurrentMonth = state.CurrentDate.Month,
            Difficulty = state.Difficulty,
            PendingDifficulty = state.PendingDifficulty,
            Origin = state.Origin,
            Family = new FamilyDto
            {
                Name = family.Name,
                HasShiStatus = family.HasShiStatus,
                HeadId = family.HeadId?.Value,
                Members = [.. family.Members.Select(ToDto)],
                SettledOutsiders = [.. bookkeeping.Settled.Select(id => id.Value)],
                MarriageAlignedOutsiders = [.. bookkeeping.MarriageAligned.Select(id => id.Value)],
                AutoExamApplication = family.AutoExamApplication,
                PunishmentJournal = ToDto(family.PunishmentJournal),
                Lifecycle = ToDto(family.Lifecycle),
            },
            Economy = ToDto(state.Economy),
            Achievements = ToDto(state.Achievements),
            Ending = ToDto(state.Ending),
        };
    }

    /// <summary>把 DTO 复原成领域状态。</summary>
    /// <param name="dto">已通过格式层读出的 DTO。</param>
    /// <returns>完整领域状态（尚未做第 ⑩ 步的一致性校验）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dto"/> 为 <c>null</c>。</exception>
    /// <exception cref="SaveFileException">
    /// 任一字段无法还原（违反 <c>KFL.Core</c> 自身不变量），分类恒为
    /// <see cref="SaveFileErrorKind.InvalidStructure"/>（契约十 §6 第 ⑦ 步）。
    /// </exception>
    public static GameState ToDomain(SaveStateDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        try
        {
            return Build(dto);
        }
        catch (SaveFileException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or NullReferenceException)
        {
            // NullReferenceException 也收在这里：契约十 §5 条款 3 禁止把缺失成员静默默认化，
            // 万一某条残留路径把 null 带进来，它 MUST 以 InvalidStructure 浮出，MUST NOT 让
            // 调用方看到一个未分类的空引用崩溃。
            throw new SaveFileException(
                SaveFileErrorKind.InvalidStructure,
                $"字段无法还原：{ex.Message}（契约十 §6 第 ⑦ 步）。",
                ex);
        }
    }

    private static GameState Build(SaveStateDto dto)
    {
        // ⓪ 枚举必须都是本版本定义的成员（第 ⑦ 步的一部分，进入任何 Core setter 之前）。
        EnsureEnumsDefined(dto);

        var familyDto = Required(dto.Family, nameof(dto.Family));
        var family = new Family(familyDto.Name)
        {
            HasShiStatus = familyDto.HasShiStatus,
            AutoExamApplication = familyDto.AutoExamApplication,
        };
        var memberDtos = Required(familyDto.Members, "family.members");
        var persons = new List<Person>(memberDtos.Count);

        // ① 家族骨架：按存盘次序注册全部成员（含归档者）；父母必先于子女，
        //    因此血亲可以走 AddChild 并接受「辈分 = 父母辈分 + 1」的校验。
        //    配偶此刻一律留空——成员尚未全部注册，Register 的双向校验无从满足。
        foreach (var memberDto in memberDtos)
        {
            var person = CreatePerson(memberDto);
            if (person.IsOutsider)
            {
                // 开局成员、娶入配偶、收养的旁系成员皆无父母参照；辈分已由构造函数携带，
                // 不调用 SetOutsiderGeneration（那会把「落定」簿记写脏，而簿记在第 ⑦ 步按盘上事实复原）。
                family.AddOutsider(person);
            }
            else
            {
                family.AddChild(person);
            }

            persons.Add(person);
        }

        // ② 构造期之外的标量（这些 setter 之间没有次序依赖）。
        for (var i = 0; i < persons.Count; i++)
        {
            var person = persons[i];
            var memberDto = memberDtos[i];

            person.Study = memberDto.Study;
            person.Health = memberDto.Health;
            person.Rank = memberDto.RankLevel is { } level ? new OfficialRank(level) : null;
            person.Merit = memberDto.Merit;
            person.Occupation = memberDto.Occupation;
            person.MonthsInOffice = memberDto.MonthsInOffice;

            // 科举进度（005）：唯一的写入通道是 internal 的 RestoreExamProgress，
            // 它同时校验两个计数 >= 0 且报名级别 ∈ {null, JieShi, ShengShi}。
            person.RestoreExamProgress(
                memberDto.JieShiFailureCount,
                memberDto.ShengShiFailureCount,
                memberDto.AppliedExam);

            // 行贿计数（006）：唯一写入通道是 internal 的 RestoreBribeCounts，它校验三者 >= 0。
            person.RestoreBribeCounts(
                memberDto.JieShiBribeCount,
                memberDto.ShengShiBribeCount,
                memberDto.DianShiBribeCount);

            // 延名师标志（007）：唯一写入通道是 internal 的 RestoreTutoring。
            person.RestoreTutoring(memberDto.IsTutoredByMaster);
        }

        // ③ 状态位 MUST 先于计时与入仕途径：Person.Status 的 setter 会拒绝「清掉某位而其计时仍在」，
        //    此刻计时尚为默认（全空），故任何状态位都写得进去。
        for (var i = 0; i < persons.Count; i++)
        {
            var status = memberDtos[i].Status;
            if ((status & ~KnownStatusFlags) != StatusFlag.None)
            {
                throw Invalid(
                    $"成员 {memberDtos[i].Id} 的状态标记含未定义位（{status}），本版本只定义 {KnownStatusFlags}。");
            }

            persons[i].Status = status;
        }

        // ④ 计时与入仕途径：它们的 setter 要求对应状态位已为真。
        for (var i = 0; i < persons.Count; i++)
        {
            var person = persons[i];
            var timersDto = Required(memberDtos[i].Timers, "timers");

            person.Timers = new StatusTimers(
                timersDto.SentenceRemainingMonths,
                timersDto.ExamBanRemainingMonths,
                timersDto.PromotionBanRemainingMonths,
                timersDto.AwaitingPostRemainingMonths,
                timersDto.IllnessSeverityBasisPoints,
                timersDto.TreatmentRemainingMonths);
            person.EntryTrack = memberDtos[i].EntryTrack;
        }

        // ⑤ 功名履历：逐条追加以复用「非降序」校验（顺序与盘上一致）。
        for (var i = 0; i < persons.Count; i++)
        {
            foreach (var record in Required(memberDtos[i].DegreeHistory, "degreeHistory"))
            {
                persons[i].AppendDegree(new DegreeRecord(
                    record.Level,
                    record.Placement,
                    new GameDate(record.ChangedYear, record.ChangedMonth),
                    record.Cause,
                    record.ImperialClass));
            }
        }

        // ⑥ 配偶：此刻成员已全部注册，双向赋值才有意义（一致性由第 ⑩ 步裁决）。
        for (var i = 0; i < persons.Count; i++)
        {
            if (memberDtos[i].SpouseId is { } spouseId)
            {
                persons[i].SetSpouse(new PersonId(spouseId));
            }
        }

        for (var i = 0; i < persons.Count; i++)
        {
            foreach (var formerSpouseId in Required(memberDtos[i].FormerSpouseIds, "formerSpouseIds"))
            {
                persons[i].AddFormerSpouse(new PersonId(formerSpouseId));
            }
        }

        // ⑦ 家主与两处外来者簿记：簿记要求全部成员已注册。
        family.SetHead(familyDto.HeadId is { } headId ? new PersonId(headId) : null);
        family.RestoreOutsiderBookkeeping(new OutsiderBookkeeping(
            [.. Required(familyDto.SettledOutsiders, "settledOutsiders").Select(id => new PersonId(id))],
            [.. Required(familyDto.MarriageAlignedOutsiders, "marriageAlignedOutsiders").Select(id => new PersonId(id))]));

        // ⑦′ 惩罚日志（006-bribery-punishment）：只追加的当月事实缓冲，独立一步复原，
        //     与 Rank / Status / Timers / 功名履历的次序不耦合（契约十六 §5、data-model §7.1）。
        var journalDto = Required(familyDto.PunishmentJournal, "punishmentJournal");
        family.PunishmentJournal.Restore(
            journalDto.FamilyBanDeclared,
            [.. Required(journalDto.Bribes, "bribes").Select(fact => new BribeFact(
                ToDate(fact.Date),
                new PersonId(fact.Examinee),
                fact.Level,
                fact.Nth,
                Money.FromWen(fact.AmountWen),
                fact.Caught,
                fact.CapReached))],
            [.. Required(journalDto.Sentences, "sentences").Select(fact => new SentenceFact(
                ToDate(fact.Date),
                new PersonId(fact.Subject),
                fact.Months))],
            [.. Required(journalDto.Fines, "fines").Select(fact => new FineFact(
                ToDate(fact.Date),
                new PersonId(fact.Subject),
                Money.FromWen(fact.AmountWen),
                Money.FromWen(fact.PaidFromTreasuryWen),
                Money.FromWen(fact.ToLoanWen)))],
            [.. Required(journalDto.RelativeBans, "relativeBans").Select(fact => new RelativeBanFact(
                ToDate(fact.Date),
                new PersonId(fact.Subject),
                new PersonId(fact.Relative),
                fact.ExamBanMonths,
                fact.PromotionBanMonths))],
            [.. Required(journalDto.CollectivePunishments, "collectivePunishments").Select(fact => new CollectivePunishmentFact(
                ToDate(fact.Date),
                new PersonId(fact.Subject),
                fact.Outcome,
                new PersonId(fact.Relative),
                fact.DegreeBefore,
                fact.DegreeAfter))],
            [.. Required(journalDto.Releases, "releases").Select(fact => new ReleaseFact(
                ToDate(fact.Date),
                new PersonId(fact.Subject),
                fact.NewRankLevel))]);

        // ⑦″ 家族生命周期簿记（007-family-lifecycle）：收养计数、待分娩记录、已申请生育的夫妇、
        //     待应答提议与当月托媒名额。只搬运盘上事实，引用完整性由第 ⑩ 步的一致性校验裁决。
        var lifecycleDto = Required(familyDto.Lifecycle, "lifecycle");
        family.Lifecycle.RestoreBookkeeping(
            lifecycleDto.PurchaseCount,
            lifecycleDto.PurchaseYear,
            [.. Required(lifecycleDto.PendingBirths, "pendingBirths").Select(birth => new PendingBirth(
                new CoupleKey(new PersonId(birth.HusbandId), new PersonId(birth.WifeId)),
                new GameDate(birth.AppliedYear, birth.AppliedMonth)))],
            [.. Required(lifecycleDto.AppliedBirthCouples, "appliedBirthCouples").Select(couple => new CoupleKey(
                new PersonId(couple.HusbandId),
                new PersonId(couple.WifeId)))],
            lifecycleDto.BirthApplicationYear,
            lifecycleDto.Proposal is { } proposal ? ToDomain(proposal) : null,
            [.. Required(lifecycleDto.MatchmakeApplications, "matchmakeApplications").Select(application =>
                new MatchmakeApplication(
                    new PersonId(application.MemberId),
                    new GameDate(application.AppliedYear, application.AppliedMonth)))]);

        // ⑧⑨ 经济聚合与存档级字段。
        var economyDto = Required(dto.Economy, nameof(dto.Economy));
        var economy = BuildEconomy(economyDto);
        var state = new GameState(
            dto.Id,
            new GameDate(dto.CurrentYear, dto.CurrentMonth),
            dto.Difficulty,
            dto.Origin,
            family,
            economy)
        {
            PendingDifficulty = dto.PendingDifficulty,
        };

        // ⑨′ 成就累计量与终局（008-achievements-extinction；契约二十三 §6「条款 25」、契约二十四 §1）。
        //     次序 MUST NOT 反转：先复原 Achievements（其 ⑦ 是**档内**已完成集合、⑧ 是待落盘队列，
        //     两者都是盘上事实），再由 Ending 复原编年史快照。累计量 MUST NOT 反向读编年史——
        //     快照是终局当刻的**冻结值**，与当前累计量在语义上并不相等（SC-011）。
        var achievementsDto = Required(dto.Achievements, nameof(dto.Achievements));
        state.Achievements.Restore(
            achievementsDto.SuccessfulBribeCount,
            achievementsDto.BirthCount,
            [.. Required(achievementsDto.Lifespans, "lifespans")],
            Money.FromWen(achievementsDto.PeakTotalAssets),
            achievementsDto.EverHadLoan,
            achievementsDto.DebugWakeCount,
            [.. Required(achievementsDto.Completed, "completed").Select(mark => new AchievementMark(
                (AchievementId)mark.Achievement,
                (Difficulty)mark.Difficulty,
                new GameDate(mark.CompletedYear, mark.CompletedMonth)))],
            [.. Required(achievementsDto.PendingUnlocks, "pendingUnlocks").Select(unlock => new PendingUnlock(
                (AchievementId)unlock.Achievement,
                (Difficulty)unlock.Difficulty,
                new GameDate(unlock.UnlockedYear, unlock.UnlockedMonth),
                unlock.SourceSaveId))]);

        if (dto.Ending is { } endingDto)
        {
            var chronicleDto = Required(endingDto.Chronicle, "ending.chronicle");
            state.Ending.Restore(
                true,
                new GameDate(endingDto.EndedYear, endingDto.EndedMonth),
                new ChronicleSnapshot
                {
                    SurvivedYears = chronicleDto.SurvivedYears,
                    Generations = chronicleDto.Generations,
                    PeakTotalAssets = Money.FromWen(chronicleDto.PeakTotalAssets),
                    HighestDegree = (DegreeLevel)chronicleDto.HighestDegree,
                    TotalJinShi = chronicleDto.TotalJinShi,
                    AverageLifespan = chronicleDto.AverageLifespan,
                    Genealogy = [.. Required(chronicleDto.Genealogy, "genealogy").Select(id => new PersonId(id))],
                });
        }
        else
        {
            // 未结束：DTO 的 null 就是「家族仍存续」，写成显式的 (false, null, null)。
            state.Ending.Restore(false, null, null);
        }

        return state;
    }

    private static T Required<T>(T? value, string name)
        where T : class =>
        value ?? throw Invalid($"文件缺本版本必需成员 {name}（或其值为 null）（契约十 §6 第 ⑥ 步）。");

    private static SaveFileException Invalid(string message) =>
        new(SaveFileErrorKind.InvalidStructure, message);
}
