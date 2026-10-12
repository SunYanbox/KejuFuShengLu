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
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            throw new SaveFileException(
                SaveFileErrorKind.InvalidStructure,
                $"字段无法还原：{ex.Message}（契约十 §6 第 ⑦ 步）。",
                ex);
        }
    }

    private static GameState Build(SaveStateDto dto)
    {
        var familyDto = Required(dto.Family, nameof(dto.Family));
        var family = new Family(familyDto.Name) { HasShiStatus = familyDto.HasShiStatus };
        var memberDtos = familyDto.Members ?? [];
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
            foreach (var record in memberDtos[i].DegreeHistory ?? [])
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
            foreach (var formerSpouseId in memberDtos[i].FormerSpouseIds ?? [])
            {
                persons[i].AddFormerSpouse(new PersonId(formerSpouseId));
            }
        }

        // ⑦ 家主与两处外来者簿记：簿记要求全部成员已注册。
        family.SetHead(familyDto.HeadId is { } headId ? new PersonId(headId) : null);
        family.RestoreOutsiderBookkeeping(new OutsiderBookkeeping(
            [.. (familyDto.SettledOutsiders ?? []).Select(id => new PersonId(id))],
            [.. (familyDto.MarriageAlignedOutsiders ?? []).Select(id => new PersonId(id))]));

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
        foreach (var entry in dto.Ledger ?? [])
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
    };

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
    private static T Required<T>(T? value, string name)
        where T : class =>
        value ?? throw Invalid($"文件缺本版本必需成员 {name}（或其值为 null）（契约十 §6 第 ⑥ 步）。");

    private static SaveFileException Invalid(string message) =>
        new(SaveFileErrorKind.InvalidStructure, message);
}
