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
public static class SaveStateMapper
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
            },
            Economy = ToDto(state.Economy),
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
                // 开局成员、娶入配偶、买来的旁系皆无父母参照；辈分已由构造函数携带，
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
                timersDto.AwaitingPostRemainingMonths);
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

        return state;
    }

    private static Person CreatePerson(PersonDto dto)
    {
        var talentsDto = Required(dto.Talents, "talents");

        return new Person(
            new PersonId(dto.Id),
            dto.Name,
            dto.Gender,
            new GameDate(dto.BirthYear, dto.BirthMonth),
            new TalentSet(
                talentsDto.Agriculture,
                talentsDto.Commerce,
                talentsDto.Officialdom,
                talentsDto.Craft),
            dto.Lifespan,
            dto.Generation,
            dto.FatherId is { } fatherId ? new PersonId(fatherId) : null,
            dto.MotherId is { } motherId ? new PersonId(motherId) : null);
    }

    private static FamilyEconomy BuildEconomy(FamilyEconomyDto dto)
    {
        var treasuryDto = Required(dto.Treasury, "treasury");
        var loanDto = Required(treasuryDto.Loan, "loan");

        var loan = new Loan
        {
            Principal = Money.FromWen(loanDto.Principal),
            MonthsSinceInterest = loanDto.MonthsSinceInterest,
        };

        // 欠息只增不减：AccrueInterest 是唯一入口，故此处按「补记」复原。
        if (loanDto.AccruedInterest != 0m)
        {
            loan.AccrueInterest(Money.FromWen(loanDto.AccruedInterest));
        }

        // 储蓄利率与其年份互为条件：年份 MUST 先落，否则年份为 null 时利率写不进去。
        var treasury = new Treasury(
            Money.FromWen(treasuryDto.Cash),
            Money.FromWen(treasuryDto.Savings),
            Money.FromWen(treasuryDto.MerchantCapital))
        {
            Loan = loan,
            SavingsRateYear = treasuryDto.SavingsRateYear,
            SavingsRate = treasuryDto.SavingsRate,
        };

        var holdingsDto = Required(dto.Holdings, "holdings");
        var holdings = new Holdings
        {
            FarmlandMu = holdingsDto.FarmlandMu,
            RuralHouses = holdingsDto.RuralHouses,
            UrbanHouses = holdingsDto.UrbanHouses,
            Shops = holdingsDto.Shops,
        };

        var ledger = new Ledger();
        foreach (var entry in Required(dto.Ledger, "ledger"))
        {
            ledger.Append(new LedgerEntry(
                new GameDate(entry.Year, entry.Month),
                entry.PersonId is { } personId ? new PersonId(personId) : null,
                entry.Category,
                Money.FromWen(entry.AmountWen)));
        }

        var economy = new FamilyEconomy(
            dto.LivingStandard,
            new GrainPriceIndex(dto.GrainPriceIndex),
            treasury,
            holdings,
            ledger)
        {
            PendingLivingStandard = dto.PendingLivingStandard,
        };

        var famineDto = Required(dto.Famine, "famine");
        economy.RestoreFamine(famineDto.Stage, famineDto.ElapsedMonths);

        return economy;
    }

    private static PersonDto ToDto(Person person) => new()
    {
        Id = person.Id.Value,
        Name = person.Name,
        Gender = person.Gender,
        BirthYear = person.BirthDate.Year,
        BirthMonth = person.BirthDate.Month,
        Generation = person.Generation,
        Talents = new TalentDto
        {
            Agriculture = person.Talents.Agriculture,
            Commerce = person.Talents.Commerce,
            Officialdom = person.Talents.Officialdom,
            Craft = person.Talents.Craft,
        },
        Study = person.Study,
        Health = person.Health,
        Lifespan = person.Lifespan,
        RankLevel = person.Rank?.Level,
        Merit = person.Merit,
        Status = person.Status,
        Timers = new StatusTimersDto
        {
            SentenceRemainingMonths = person.Timers.SentenceRemainingMonths,
            ExamBanRemainingMonths = person.Timers.ExamBanRemainingMonths,
            PromotionBanRemainingMonths = person.Timers.PromotionBanRemainingMonths,
            AwaitingPostRemainingMonths = person.Timers.AwaitingPostRemainingMonths,
        },
        Occupation = person.Occupation,
        MonthsInOffice = person.MonthsInOffice,
        EntryTrack = person.EntryTrack,
        FatherId = person.FatherId?.Value,
        MotherId = person.MotherId?.Value,
        SpouseId = person.SpouseId?.Value,
        FormerSpouseIds = [.. person.FormerSpouseIds.Select(id => id.Value)],
        DegreeHistory = [.. person.DegreeHistory.Select(record => new DegreeRecordDto
        {
            Level = record.Level,
            Placement = record.Placement,
            ChangedYear = record.ChangedAt.Year,
            ChangedMonth = record.ChangedAt.Month,
            Cause = record.Cause,
            ImperialClass = record.Class,
        })],
        JieShiFailureCount = person.JieShiFailureCount,
        ShengShiFailureCount = person.ShengShiFailureCount,
        AppliedExam = person.AppliedExam,
        JieShiBribeCount = person.JieShiBribeCount,
        ShengShiBribeCount = person.ShengShiBribeCount,
        DianShiBribeCount = person.DianShiBribeCount,
    };

    /// <summary>把惩罚日志映射成瞬态 DTO（006-bribery-punishment；契约十六 §5、data-model §7.1）。</summary>
    /// <param name="journal">惩罚日志。</param>
    /// <returns>瞬态 DTO。</returns>
    private static PunishmentJournalDto ToDto(PunishmentJournal journal) => new()
    {
        FamilyBanDeclared = journal.FamilyBanDeclared,
        Bribes =
        [
            .. journal.Bribes.Select(fact => new BribeFactDto
            {
                Date = ToDto(fact.Date),
                Examinee = fact.Examinee.Value,
                Level = fact.Level,
                Nth = fact.Nth,
                AmountWen = fact.Amount.Wen,
                Caught = fact.Caught,
                CapReached = fact.CapReached,
            }),
        ],
        Sentences =
        [
            .. journal.Sentences.Select(fact => new SentenceFactDto
            {
                Date = ToDto(fact.Date),
                Subject = fact.Subject.Value,
                Months = fact.Months,
            }),
        ],
        Fines =
        [
            .. journal.Fines.Select(fact => new FineFactDto
            {
                Date = ToDto(fact.Date),
                Subject = fact.Subject.Value,
                AmountWen = fact.Amount.Wen,
                PaidFromTreasuryWen = fact.PaidFromTreasury.Wen,
                ToLoanWen = fact.ToLoan.Wen,
            }),
        ],
        RelativeBans =
        [
            .. journal.RelativeBans.Select(fact => new RelativeBanFactDto
            {
                Date = ToDto(fact.Date),
                Subject = fact.Subject.Value,
                Relative = fact.Relative.Value,
                ExamBanMonths = fact.ExamBanMonths,
                PromotionBanMonths = fact.PromotionBanMonths,
            }),
        ],
        CollectivePunishments =
        [
            .. journal.CollectivePunishments.Select(fact => new CollectivePunishmentFactDto
            {
                Date = ToDto(fact.Date),
                Subject = fact.Subject.Value,
                Outcome = fact.Outcome,
                Relative = fact.Relative.Value,
                DegreeBefore = fact.DegreeBefore,
                DegreeAfter = fact.DegreeAfter,
            }),
        ],
        Releases =
        [
            .. journal.Releases.Select(fact => new ReleaseFactDto
            {
                Date = ToDto(fact.Date),
                Subject = fact.Subject.Value,
                NewRankLevel = fact.NewRankLevel,
            }),
        ],
    };

    /// <summary>年月 ↔ 瞬态 DTO。</summary>
    /// <param name="date">年月。</param>
    /// <returns>瞬态 DTO。</returns>
    private static GameDateDto ToDto(GameDate date) => new()
    {
        Year = date.Year,
        Month = date.Month,
    };

    /// <summary>瞬态 DTO 的年月 ↔ 年月。</summary>
    /// <param name="dto">瞬态 DTO。</param>
    /// <returns>年月（接受 <see cref="GameDate"/> 的自校验）。</returns>
    private static GameDate ToDate(GameDateDto dto) => new(dto.Year, dto.Month);

    private static FamilyEconomyDto ToDto(FamilyEconomy economy) => new()
    {
        LivingStandard = economy.LivingStandard,
        PendingLivingStandard = economy.PendingLivingStandard,
        GrainPriceIndex = economy.GrainPriceIndex.Value,
        Treasury = new TreasuryDto
        {
            Cash = economy.Treasury.Cash.Wen,
            Savings = economy.Treasury.Savings.Wen,
            MerchantCapital = economy.Treasury.MerchantCapital.Wen,
            Loan = new LoanDto
            {
                Principal = economy.Treasury.Loan.Principal.Wen,
                AccruedInterest = economy.Treasury.Loan.AccruedInterest.Wen,
                MonthsSinceInterest = economy.Treasury.Loan.MonthsSinceInterest,
            },
            SavingsRate = economy.Treasury.SavingsRate,
            SavingsRateYear = economy.Treasury.SavingsRateYear,
        },
        Holdings = new HoldingsDto
        {
            FarmlandMu = economy.Holdings.FarmlandMu,
            RuralHouses = economy.Holdings.RuralHouses,
            UrbanHouses = economy.Holdings.UrbanHouses,
            Shops = economy.Holdings.Shops,
        },
        Ledger = [.. economy.Ledger.Entries.Select(entry => new LedgerEntryDto
        {
            Year = entry.Date.Year,
            Month = entry.Date.Month,
            PersonId = entry.PersonId?.Value,
            Category = entry.Category,
            AmountWen = entry.Amount.Wen,
        })],
        Famine = new FamineDto
        {
            Stage = economy.Famine.Stage,
            ElapsedMonths = economy.Famine.ElapsedMonths,
        },
    };

    /// <summary>取必需子对象；为 <c>null</c> 即结构错误（缺成员，或显式写了 <c>null</c>）。</summary>
    /// <summary>
    /// 契约十 §3 条款 1 / data-model §3.10：枚举 MUST 是本版本定义的具名成员，越界整数 ⇒ 结构不符（第 ⑦ 步）。
    /// </summary>
    /// <remarks>
    /// 二进制把枚举写成**原值整数**，越界值读得出来（JSON 侧不认得的**名字**在第 ⑤ 步就已失败，属
    /// <see cref="SaveFileErrorKind.Malformed"/>）——若不复核，<c>(Difficulty)999</c> 这类值会静默变成
    /// 领域里的越界枚举，等于为序列化放宽不变量（FR-020）。
    /// <c>StatusFlag</c> 是位掩码，另有自己的位集判据（见 <see cref="Build"/> 第 ③ 步）。
    /// </remarks>
    /// <param name="dto">已通过格式层读出的 DTO。</param>
    private static void EnsureEnumsDefined(SaveStateDto dto)
    {
        EnsureDefined(dto.Difficulty, "difficulty");
        EnsureDefined(dto.PendingDifficulty, "pendingDifficulty");
        EnsureDefined(dto.Origin, "origin");

        foreach (var member in Required(Required(dto.Family, nameof(dto.Family)).Members, "family.members"))
        {
            EnsureDefined(member.Gender, "gender");
            EnsureDefined(member.Occupation, "occupation");
            EnsureDefined(member.EntryTrack, "entryTrack");

            foreach (var degree in Required(member.DegreeHistory, "degreeHistory"))
            {
                EnsureDefined(degree.Level, "level");
                EnsureDefined(degree.Placement, "placement");
                EnsureDefined(degree.Cause, "cause");
                EnsureDefined(degree.ImperialClass, "imperialClass");
            }
        }

        var economy = Required(dto.Economy, nameof(dto.Economy));
        EnsureDefined(economy.LivingStandard, "livingStandard");
        EnsureDefined(economy.PendingLivingStandard, "pendingLivingStandard");

        foreach (var entry in Required(economy.Ledger, "ledger"))
        {
            EnsureDefined(entry.Category, "category");
        }

        EnsureDefined(Required(economy.Famine, "famine").Stage, "stage");
    }

    /// <summary>值 MUST 是具名枚举成员。</summary>
    /// <param name="value">待检值。</param>
    /// <param name="field">字段名（用线上键名，便于与盘上内容对照）。</param>
    /// <typeparam name="TEnum">枚举类型。</typeparam>
    private static void EnsureDefined<TEnum>(TEnum value, string field)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw Invalid($"字段 {field} 的值 {value} 不是本版本定义的枚举成员（契约十 §3 条款 1）。");
        }
    }

    /// <summary>可空枚举：非空时 MUST 是具名成员。</summary>
    /// <param name="value">待检值。</param>
    /// <param name="field">字段名。</param>
    /// <typeparam name="TEnum">枚举类型。</typeparam>
    private static void EnsureDefined<TEnum>(TEnum? value, string field)
        where TEnum : struct, Enum
    {
        if (value is { } present)
        {
            EnsureDefined(present, field);
        }
    }

    private static T Required<T>(T? value, string name)
        where T : class =>
        value ?? throw Invalid($"文件缺本版本必需成员 {name}（或其值为 null）（契约十 §6 第 ⑥ 步）。");

    private static SaveFileException Invalid(string message) =>
        new(SaveFileErrorKind.InvalidStructure, message);
}
