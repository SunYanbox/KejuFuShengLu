// SaveStateMapper 的家族部分：生命周期、婚姻提议与年月映射。

using KFL.Core.Entities;
using KFL.Core.ValueObjects;

namespace KFL.Infrastructure.Save.Serialization;

public static partial class SaveStateMapper
{
    /// <summary>把家族生命周期簿记映射成瞬态 DTO（007-family-lifecycle；契约二十 §5、data-model §3.1）。</summary>
    /// <param name="lifecycle">生命周期簿记。</param>
    /// <returns>瞬态 DTO。</returns>
    private static FamilyLifecycleDto ToDto(FamilyLifecycleState lifecycle) => new()
    {
        PurchaseCount = lifecycle.PurchaseCount,
        PurchaseYear = lifecycle.PurchaseYear,
        PendingBirths = [.. lifecycle.PendingBirths.Select(birth => new PendingBirthDto
        {
            HusbandId = birth.Couple.HusbandId.Value,
            WifeId = birth.Couple.WifeId.Value,
            AppliedYear = birth.AppliedAt.Year,
            AppliedMonth = birth.AppliedAt.Month,
        })],
        AppliedBirthCouples = [.. lifecycle.AppliedBirthCouples.Select(couple => new CoupleKeyDto
        {
            HusbandId = couple.HusbandId.Value,
            WifeId = couple.WifeId.Value,
        })],
        BirthApplicationYear = lifecycle.BirthApplicationYear,
        Proposal = lifecycle.Proposal is { } proposal ? ToDto(proposal) : null,
        MatchmakeApplications = [.. lifecycle.MatchmakeApplications.Select(application => new MatchmakeApplicationDto
        {
            MemberId = application.MemberId.Value,
            AppliedYear = application.AppliedAt.Year,
            AppliedMonth = application.AppliedAt.Month,
        })],
    };

    /// <summary>把待应答提议映射成瞬态 DTO（007-family-lifecycle；契约二十 §5、data-model §2.2）。</summary>
    /// <param name="proposal">提议。</param>
    /// <returns>瞬态 DTO。</returns>
    private static MarriageProposalDto ToDto(MarriageProposal proposal) => new()
    {
        Kind = proposal.Kind,
        OurMemberId = proposal.OurMemberId.Value,
        CandidateName = proposal.CandidateName,
        CandidateGender = proposal.CandidateGender,
        CandidateAge = proposal.CandidateAge,
        TalentAgriculture = proposal.TalentAgriculture,
        TalentCommerce = proposal.TalentCommerce,
        TalentScholar = proposal.TalentScholar,
        TalentCraft = proposal.TalentCraft,
        CandidateStudy = proposal.CandidateStudy,
        CandidateHealth = proposal.CandidateHealth,
        CandidateLifespan = proposal.CandidateLifespan,
        CandidateDegree = proposal.CandidateDegree,
        Class = proposal.Class,
        BetrothalGift = proposal.BetrothalGift.Wen,
        Dowry = proposal.Dowry.Wen,
        ProposedYear = proposal.ProposedAt.Year,
        ProposedMonth = proposal.ProposedAt.Month,
        CandidatePlacement = proposal.CandidatePlacement,
    };

    /// <summary>把瞬态 DTO 复原成待应答提议（007-family-lifecycle；契约二十 §5、data-model §2.2）。</summary>
    /// <param name="dto">提议 DTO。</param>
    /// <returns>领域记录。</returns>
    private static MarriageProposal ToDomain(MarriageProposalDto dto) => new()
    {
        Kind = dto.Kind,
        OurMemberId = new PersonId(dto.OurMemberId),
        CandidateName = dto.CandidateName,
        CandidateGender = dto.CandidateGender,
        CandidateAge = dto.CandidateAge,
        TalentAgriculture = dto.TalentAgriculture,
        TalentCommerce = dto.TalentCommerce,
        TalentScholar = dto.TalentScholar,
        TalentCraft = dto.TalentCraft,
        CandidateStudy = dto.CandidateStudy,
        CandidateHealth = dto.CandidateHealth,
        CandidateLifespan = dto.CandidateLifespan,
        CandidateDegree = dto.CandidateDegree,
        Class = dto.Class,
        BetrothalGift = Money.FromWen(dto.BetrothalGift),
        Dowry = Money.FromWen(dto.Dowry),
        ProposedAt = new GameDate(dto.ProposedYear, dto.ProposedMonth),
        CandidatePlacement = dto.CandidatePlacement,
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
}
