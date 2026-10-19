using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save.Serialization;
using MessagePack;
using Xunit;

namespace KFL.Tests.Infrastructure;

/// <summary>
/// 存档**字节形态**的契约测试（004-save-system；契约十 §2~§5 / tasks.md T030）。
/// </summary>
/// <remarks>
/// 这里断言的是「盘上长什么样」，不是「读回来对不对」——后者由 <c>SaveServiceTests</c> 负责。
/// 两者缺一不可：只比往返会让「两个格式都少存同一个字段」蒙混过关（契约十 §5 条款 3）。
/// </remarks>
public sealed partial class SaveFormatContractTests
{
    /// <summary>契约十 §4：JSON 键表逐条等于契约表（键名 MUST NOT 随 C# 改名而变）。</summary>
    [Fact]
    public void JSON键表与契约键表逐条一致()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("甲档", SaveFormat.Json);

        service.Save(SaveFixtures.RichState(), slot, overwrite: false);

        using var document = JsonDocument.Parse(SaveTestSandbox.ReadBytes(service.FilePathOf(slot)));
        var paths = new SortedSet<string>(StringComparer.Ordinal);
        CollectPaths(document.RootElement.GetProperty("State"), string.Empty, paths);

        Assert.Equal(ContractKeyTable, paths);
    }

    // ───────────────────────────── T055：[Key(n)] 的键序号 ─────────────────────────────

    /// <summary>
    /// T055 / 契约十 §4 + §5 条款 4：每个 DTO 的 <c>[Key(n)]</c> MUST 恰为 <c>0..N-1</c>，
    /// 且第 <c>n</c> 个键 MUST 落在契约键表第 <c>n</c> 个成员上。
    /// </summary>
    /// <remarks>
    /// 位置式数组的「位置」就是 <c>[Key]</c> 序号：只断言集合是 <c>0..N-1</c> 还不够——
    /// 交换两个 <c>[Key]</c> 后集合照旧完整，但盘上的字节与契约错位。故这里按序号排好序后
    /// 与契约 §4 的成员次序逐位比对。
    /// </remarks>
    [Fact]
    public void 二进制键序号自零连续且与契约键表逐位一致()
    {
        foreach (var (type, namesByKey) in BinaryKeyTables)
        {
            var members = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => (
                    Key: property.GetCustomAttribute<KeyAttribute>(),
                    Name: property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name))
                .ToList();

            // 成员数与契约键表一致，且每个成员都带 [Key] / [JsonPropertyName]。
            Assert.Equal(namesByKey.Length, members.Count);
            Assert.All(members, member =>
            {
                Assert.NotNull(member.Key);
                Assert.NotNull(member.Name);
            });

            var ordered = members.OrderBy(member => member.Key!.IntKey!.Value).ToList();

            // 序号 MUST 恰为 0..N-1：不重、不漏、不从 1 起。
            Assert.Equal(
                Enumerable.Range(0, namesByKey.Length),
                ordered.Select(member => member.Key!.IntKey!.Value));

            // 第 n 个键 MUST 是契约键表的第 n 个成员。
            Assert.Equal(namesByKey, ordered.Select(member => member.Name));
        }
    }

    /// <summary>递归收集「成员路径」（数组元素用 <c>[]</c> 标记，同名元素只留一份）。</summary>
    /// <param name="element">当前元素。</param>
    /// <param name="path">到达当前元素的路径。</param>
    /// <param name="paths">收集结果。</param>
    private static void CollectPaths(JsonElement element, string path, ISet<string> paths)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var child = path.Length == 0 ? property.Name : $"{path}.{property.Name}";
                paths.Add(child);
                CollectPaths(property.Value, child, paths);
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectPaths(item, $"{path}[]", paths);
            }
        }
    }

    /// <summary>契约十 §4 的键表（逐条抄自契约，按序数排序）。</summary>
    private static readonly string[] ContractKeyTable =
    [
        // 008：`achievements` 恒非空（`SaveStateDto.Achievements` 有初值），其三个序列在夹具里为空，
        //      故只出现数组名本身；`ending` 恒写 `null`（无 `DefaultIgnoreCondition`）——
        //      键在、子键不在，正是「缺键」与「显式空值」必须被读侧区分的地方（契约二十三 条款 17）。
        "achievements",
        "achievements.birthCount",
        "achievements.completed",
        "achievements.debugWakeCount",
        "achievements.everHadLoan",
        "achievements.lifespans",
        "achievements.peakTotalAssets",
        "achievements.pendingUnlocks",
        "achievements.successfulBribeCount",
        "currentMonth",
        "currentYear",
        "difficulty",
        "economy",
        "economy.famine",
        "economy.famine.elapsedMonths",
        "economy.famine.stage",
        "economy.grainPriceIndex",
        "economy.holdings",
        "economy.holdings.farmlandMu",
        "economy.holdings.ruralHouses",
        "economy.holdings.shops",
        "economy.holdings.urbanHouses",
        "economy.ledger",
        "economy.ledger[].amountWen",
        "economy.ledger[].category",
        "economy.ledger[].month",
        "economy.ledger[].personId",
        "economy.ledger[].year",
        "economy.livingStandard",
        "economy.pendingLivingStandard",
        "economy.treasury",
        "economy.treasury.cash",
        "economy.treasury.loan",
        "economy.treasury.loan.accruedInterest",
        "economy.treasury.loan.monthsSinceInterest",
        "economy.treasury.loan.principal",
        "economy.treasury.merchantCapital",
        "economy.treasury.savings",
        "economy.treasury.savingsRate",
        "economy.treasury.savingsRateYear",
        "ending",
        "family",
        "family.autoExamApplication",
        "family.hasShiStatus",
        "family.headId",
        // 007：夹具的厚状态把五组生命周期状态与当月托媒名额都填成非空，故集合元素与嵌套提议的
        //      每个键都会实际出现在 JSON 里；四个新 DTO 的内部键位另由上面的 BinaryKeyTables 与
        //      walker 常量表钉死。
        "family.lifecycle",
        "family.lifecycle.appliedBirthCouples",
        "family.lifecycle.appliedBirthCouples[].husbandId",
        "family.lifecycle.appliedBirthCouples[].wifeId",
        "family.lifecycle.birthApplicationYear",
        "family.lifecycle.matchmakeApplications",
        "family.lifecycle.matchmakeApplications[].appliedMonth",
        "family.lifecycle.matchmakeApplications[].appliedYear",
        "family.lifecycle.matchmakeApplications[].memberId",
        "family.lifecycle.pendingBirths",
        "family.lifecycle.pendingBirths[].appliedMonth",
        "family.lifecycle.pendingBirths[].appliedYear",
        "family.lifecycle.pendingBirths[].husbandId",
        "family.lifecycle.pendingBirths[].wifeId",
        "family.lifecycle.proposal",
        "family.lifecycle.proposal.betrothalGift",
        "family.lifecycle.proposal.candidateAge",
        "family.lifecycle.proposal.candidateDegree",
        "family.lifecycle.proposal.candidateGender",
        "family.lifecycle.proposal.candidateHealth",
        "family.lifecycle.proposal.candidateLifespan",
        "family.lifecycle.proposal.candidateName",
        "family.lifecycle.proposal.candidatePlacement",
        "family.lifecycle.proposal.candidateStudy",
        "family.lifecycle.proposal.class",
        "family.lifecycle.proposal.dowry",
        "family.lifecycle.proposal.kind",
        "family.lifecycle.proposal.ourMemberId",
        "family.lifecycle.proposal.proposedMonth",
        "family.lifecycle.proposal.proposedYear",
        "family.lifecycle.proposal.talentAgriculture",
        "family.lifecycle.proposal.talentCommerce",
        "family.lifecycle.proposal.talentCraft",
        "family.lifecycle.proposal.talentScholar",
        "family.lifecycle.purchaseCount",
        "family.lifecycle.purchaseYear",
        "family.marriageAlignedOutsiders",
        "family.members",
        "family.members[].appliedExam",
        "family.members[].birthMonth",
        "family.members[].birthYear",
        "family.members[].degreeHistory",
        "family.members[].degreeHistory[].cause",
        "family.members[].degreeHistory[].changedMonth",
        "family.members[].degreeHistory[].changedYear",
        "family.members[].degreeHistory[].imperialClass",
        "family.members[].degreeHistory[].level",
        "family.members[].degreeHistory[].placement",
        "family.members[].dianShiBribeCount",
        "family.members[].entryTrack",
        "family.members[].fatherId",
        "family.members[].formerSpouseIds",
        "family.members[].gender",
        "family.members[].generation",
        "family.members[].health",
        "family.members[].id",
        "family.members[].isTutoredByMaster",
        "family.members[].jieShiBribeCount",
        "family.members[].jieShiFailureCount",
        "family.members[].lifespan",
        "family.members[].merit",
        "family.members[].monthsInOffice",
        "family.members[].motherId",
        "family.members[].name",
        "family.members[].occupation",
        "family.members[].rankLevel",
        "family.members[].shengShiBribeCount",
        "family.members[].shengShiFailureCount",
        "family.members[].spouseId",
        "family.members[].status",
        "family.members[].study",
        "family.members[].talents",
        "family.members[].talents.agriculture",
        "family.members[].talents.commerce",
        "family.members[].talents.craft",
        "family.members[].talents.officialdom",
        "family.members[].timers",
        "family.members[].timers.awaitingPostRemainingMonths",
        "family.members[].timers.examBanRemainingMonths",
        "family.members[].timers.illnessSeverityBasisPoints",
        "family.members[].timers.promotionBanRemainingMonths",
        "family.members[].timers.sentenceRemainingMonths",
        "family.members[].timers.treatmentRemainingMonths",
        "family.name",
        "family.punishmentJournal",
        "family.punishmentJournal.bribes",
        "family.punishmentJournal.bribes[].amountWen",
        "family.punishmentJournal.bribes[].capReached",
        "family.punishmentJournal.bribes[].caught",
        "family.punishmentJournal.bribes[].date",
        "family.punishmentJournal.bribes[].date.month",
        "family.punishmentJournal.bribes[].date.year",
        "family.punishmentJournal.bribes[].examinee",
        "family.punishmentJournal.bribes[].level",
        "family.punishmentJournal.bribes[].nth",
        "family.punishmentJournal.collectivePunishments",
        "family.punishmentJournal.collectivePunishments[].date",
        "family.punishmentJournal.collectivePunishments[].date.month",
        "family.punishmentJournal.collectivePunishments[].date.year",
        "family.punishmentJournal.collectivePunishments[].degreeAfter",
        "family.punishmentJournal.collectivePunishments[].degreeBefore",
        "family.punishmentJournal.collectivePunishments[].outcome",
        "family.punishmentJournal.collectivePunishments[].relative",
        "family.punishmentJournal.collectivePunishments[].subject",
        "family.punishmentJournal.familyBanDeclared",
        "family.punishmentJournal.fines",
        "family.punishmentJournal.fines[].amountWen",
        "family.punishmentJournal.fines[].date",
        "family.punishmentJournal.fines[].date.month",
        "family.punishmentJournal.fines[].date.year",
        "family.punishmentJournal.fines[].paidFromTreasuryWen",
        "family.punishmentJournal.fines[].subject",
        "family.punishmentJournal.fines[].toLoanWen",
        "family.punishmentJournal.relativeBans",
        "family.punishmentJournal.relativeBans[].date",
        "family.punishmentJournal.relativeBans[].date.month",
        "family.punishmentJournal.relativeBans[].date.year",
        "family.punishmentJournal.relativeBans[].examBanMonths",
        "family.punishmentJournal.relativeBans[].promotionBanMonths",
        "family.punishmentJournal.relativeBans[].relative",
        "family.punishmentJournal.relativeBans[].subject",
        "family.punishmentJournal.releases",
        "family.punishmentJournal.releases[].date",
        "family.punishmentJournal.releases[].date.month",
        "family.punishmentJournal.releases[].date.year",
        "family.punishmentJournal.releases[].newRankLevel",
        "family.punishmentJournal.releases[].subject",
        "family.punishmentJournal.sentences",
        "family.punishmentJournal.sentences[].date",
        "family.punishmentJournal.sentences[].date.month",
        "family.punishmentJournal.sentences[].date.year",
        "family.punishmentJournal.sentences[].months",
        "family.punishmentJournal.sentences[].subject",
        "family.settledOutsiders",
        "id",
        "origin",
        "pendingDifficulty",
    ];
}
