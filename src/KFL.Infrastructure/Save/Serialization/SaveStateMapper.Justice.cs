// SaveStateMapper 的司法部分：惩罚日志及其六类事实行映射。

using KFL.Core.Entities;

namespace KFL.Infrastructure.Save.Serialization;

public static partial class SaveStateMapper
{
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
}
