using System.Reflection;
using KFL.Infrastructure.Save.Serialization;
using MessagePack;
using Xunit;

namespace KFL.Tests.Infrastructure;

public sealed partial class SaveFormatContractTests
{
    // ─────────────────── T053/T055 的钉死面：walker 的成员数常量 ───────────────────

    /// <summary>
    /// 契约十 §4 + §5 条款 4：<c>MessagePackSaveFormat</c> walker 的成员数常量 MUST 与各 DTO 的
    /// <c>[Key]</c> 数量逐一相等，头记录恒为 3 个成员。
    /// </summary>
    /// <remarks>
    /// walker 是**手写**的位置式遍历：给某个 DTO 增删一个 <c>[Key]</c> 而忘了改常量时，
    /// 构建与 T055 都照旧全绿，walker 却会静默走错位置（审核 2.10）。本用例把这张对应表钉死，
    /// 使它不再只靠注释声称。
    /// </remarks>
    [Fact]
    public void 二进制walker的成员数常量与DTO键数逐一相等()
    {
        var constants = typeof(MessagePackSaveFormat)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(int))
            .ToDictionary(
                field => field.Name,
                field => (int)field.GetRawConstantValue()!,
                StringComparer.Ordinal);

        (string Constant, Type Dto)[] mapping =
        [
            ("SaveStateMemberCount", typeof(SaveStateDto)),
            ("FamilyMemberCount", typeof(FamilyDto)),
            ("PersonMemberCount", typeof(PersonDto)),
            ("TalentMemberCount", typeof(TalentDto)),
            ("StatusTimersMemberCount", typeof(StatusTimersDto)),
            ("DegreeRecordMemberCount", typeof(DegreeRecordDto)),
            ("FamilyEconomyMemberCount", typeof(FamilyEconomyDto)),
            ("TreasuryMemberCount", typeof(TreasuryDto)),
            ("LoanMemberCount", typeof(LoanDto)),
            ("HoldingsMemberCount", typeof(HoldingsDto)),
            ("LedgerEntryMemberCount", typeof(LedgerEntryDto)),
            ("FamineMemberCount", typeof(FamineDto)),
            ("PunishmentJournalMemberCount", typeof(PunishmentJournalDto)),
            ("BribeFactMemberCount", typeof(BribeFactDto)),
            ("SentenceFactMemberCount", typeof(SentenceFactDto)),
            ("FineFactMemberCount", typeof(FineFactDto)),
            ("RelativeBanFactMemberCount", typeof(RelativeBanFactDto)),
            ("CollectivePunishmentFactMemberCount", typeof(CollectivePunishmentFactDto)),
            ("ReleaseFactMemberCount", typeof(ReleaseFactDto)),
            ("FamilyLifecycleMemberCount", typeof(FamilyLifecycleDto)),
            ("PendingBirthMemberCount", typeof(PendingBirthDto)),
            ("CoupleKeyMemberCount", typeof(CoupleKeyDto)),
            ("MatchmakeApplicationMemberCount", typeof(MatchmakeApplicationDto)),
            ("MarriageProposalMemberCount", typeof(MarriageProposalDto)),
            ("AchievementStateMemberCount", typeof(AchievementStateDto)),
            ("AchievementMarkMemberCount", typeof(AchievementMarkDto)),
            ("PendingUnlockMemberCount", typeof(PendingUnlockDto)),
            ("EndingStateMemberCount", typeof(EndingStateDto)),
            ("ChronicleMemberCount", typeof(ChronicleDto)),
        ];

        foreach (var (constant, dto) in mapping)
        {
            Assert.True(constants.ContainsKey(constant), $"MessagePackSaveFormat 缺成员数常量 {constant}。");

            var keyCount = dto.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Count(property => property.GetCustomAttribute<KeyAttribute>() is not null);

            Assert.Equal(keyCount, constants[constant]);
        }

        // 头记录 = 版本 + 主体长度 + 标识（契约十 §2）。
        Assert.Equal(3, constants["HeaderMemberCount"]);
    }

    /// <summary>契约十 §4 的二进制键表：<c>[Key(0..N-1)]</c> 依次对应的 JSON 成员名。</summary>
    private static readonly (Type Type, string[] NamesByKey)[] BinaryKeyTables =
    [
        (typeof(SaveStateDto),
            ["id", "currentYear", "currentMonth", "difficulty", "pendingDifficulty", "origin", "family", "economy",
                "achievements", "ending"]),
        (typeof(FamilyDto),
            ["name", "hasShiStatus", "headId", "members", "settledOutsiders", "marriageAlignedOutsiders",
                "autoExamApplication", "punishmentJournal", "lifecycle"]),
        (typeof(PersonDto),
            [
                "id", "name", "gender", "birthYear", "birthMonth", "generation", "talents", "study",
                "health", "lifespan", "rankLevel", "merit", "status", "timers", "occupation",
                "monthsInOffice", "entryTrack", "fatherId", "motherId", "spouseId", "formerSpouseIds",
                "degreeHistory", "jieShiFailureCount", "shengShiFailureCount", "appliedExam",
                "jieShiBribeCount", "shengShiBribeCount", "dianShiBribeCount", "isTutoredByMaster",
            ]),
        (typeof(TalentDto), ["agriculture", "commerce", "officialdom", "craft"]),
        (typeof(StatusTimersDto),
            ["sentenceRemainingMonths", "examBanRemainingMonths", "promotionBanRemainingMonths",
                "awaitingPostRemainingMonths", "illnessSeverityBasisPoints", "treatmentRemainingMonths"]),
        (typeof(DegreeRecordDto),
            ["level", "placement", "changedYear", "changedMonth", "cause", "imperialClass"]),
        (typeof(FamilyEconomyDto),
            ["livingStandard", "pendingLivingStandard", "grainPriceIndex", "treasury", "holdings", "ledger", "famine"]),
        (typeof(TreasuryDto),
            ["cash", "savings", "merchantCapital", "loan", "savingsRate", "savingsRateYear"]),
        (typeof(LoanDto), ["principal", "accruedInterest", "monthsSinceInterest"]),
        (typeof(HoldingsDto), ["farmlandMu", "ruralHouses", "urbanHouses", "shops"]),
        (typeof(LedgerEntryDto), ["year", "month", "personId", "category", "amountWen"]),
        (typeof(FamineDto), ["stage", "elapsedMonths"]),

        // 006-bribery-punishment：惩罚日志与六类事实行（键均自 0 连续，与 Core 记录成员逐一对应）。
        (typeof(PunishmentJournalDto),
            ["familyBanDeclared", "bribes", "sentences", "fines", "relativeBans", "collectivePunishments",
                "releases"]),
        (typeof(GameDateDto), ["year", "month"]),
        (typeof(BribeFactDto),
            ["date", "examinee", "level", "nth", "amountWen", "caught", "capReached"]),
        (typeof(SentenceFactDto), ["date", "subject", "months"]),
        (typeof(FineFactDto), ["date", "subject", "amountWen", "paidFromTreasuryWen", "toLoanWen"]),
        (typeof(RelativeBanFactDto),
            ["date", "subject", "relative", "examBanMonths", "promotionBanMonths"]),
        (typeof(CollectivePunishmentFactDto),
            ["date", "subject", "outcome", "relative", "degreeBefore", "degreeAfter"]),
        (typeof(ReleaseFactDto), ["date", "subject", "newRankLevel"]),

        // 007-family-lifecycle：家族生命周期簿记、一条待应答提议与一条当月托媒名额（键均自 0 连续）。
        (typeof(FamilyLifecycleDto),
            ["purchaseCount", "purchaseYear", "pendingBirths", "appliedBirthCouples", "birthApplicationYear",
                "proposal", "matchmakeApplications"]),
        (typeof(PendingBirthDto), ["husbandId", "wifeId", "appliedYear", "appliedMonth"]),
        (typeof(CoupleKeyDto), ["husbandId", "wifeId"]),
        (typeof(MatchmakeApplicationDto), ["memberId", "appliedYear", "appliedMonth"]),
        (typeof(MarriageProposalDto),
            [
                "kind", "ourMemberId", "candidateName", "candidateGender", "candidateAge",
                "talentAgriculture", "talentCommerce", "talentScholar", "talentCraft", "candidateStudy",
                "candidateHealth", "candidateLifespan", "candidateDegree", "class", "betrothalGift",
                "dowry", "proposedYear", "proposedMonth", "candidatePlacement",
            ]),

        // 008-achievements-extinction：档内成就累计量（8）与终局状态（3）+ 编年史七项。
        (typeof(AchievementStateDto),
            ["successfulBribeCount", "birthCount", "lifespans", "peakTotalAssets", "everHadLoan",
                "debugWakeCount", "completed", "pendingUnlocks"]),
        (typeof(AchievementMarkDto), ["achievement", "difficulty", "completedYear", "completedMonth"]),
        (typeof(PendingUnlockDto),
            ["achievement", "difficulty", "unlockedYear", "unlockedMonth", "sourceSaveId"]),
        (typeof(EndingStateDto), ["endedYear", "endedMonth", "chronicle"]),
        (typeof(ChronicleDto),
            ["survivedYears", "generations", "peakTotalAssets", "highestDegree", "totalJinShi",
                "averageLifespan", "genealogy"]),
    ];
}
