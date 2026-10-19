// SaveStateMapper 的人物部分：Person ↔ PersonDto。

using KFL.Core.Entities;
using KFL.Core.ValueObjects;

namespace KFL.Infrastructure.Save.Serialization;

public static partial class SaveStateMapper
{
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
            IllnessSeverityBasisPoints = person.Timers.IllnessSeverityBasisPoints,
            TreatmentRemainingMonths = person.Timers.TreatmentRemainingMonths,
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
        IsTutoredByMaster = person.IsTutoredByMaster,
    };
}
