using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Services;
using KFL.Rules.Start;
using Xunit;

namespace KFL.Tests.Rules;

public partial class DeterminismTests
{
    [Fact]
    public void 相同出身姓氏难度与种子两次开局逐字段完全相同()
    {
        // SC-008 的开局半边：按契约六 §3 的消费次序（①姓氏 →②家主 →③配偶 →④孩子 i →⑤存档标识），
        // 同入参 + 同种子 MUST 得到逐字段相同的存档。
        foreach (var origin in new[] { Origin.Farmer, Origin.Artisan, Origin.Merchant, Origin.Scholar })
        {
            AssertSameOpening(Opened(origin), Opened(origin));
        }
    }

    [Fact]
    public void 不同种子开局至少一处不同()
    {
        var first = Opened(Origin.Scholar);
        var second = Opened(Origin.Scholar, Seed + 1);

        var firstFacts = Describe(first);
        var secondFacts = Describe(second);

        Assert.NotEqual(firstFacts, secondFacts);
    }

    /// <summary>按契约六 §3 的次序跑一次开局（固定姓名来源，随机只来自固定种子）。</summary>
    private static NewGameSetupResult Opened(Origin origin, int seed = Seed)
    {
        var request = new NewGameRequest(origin, Difficulty.Normal, "测", new GameDate(1, 1));

        return NewGameSetup.Create(request, new SeededRandomService(seed), new FixedNameGenerator("测"));
    }

    /// <summary>把一份开局结果的**全部可断言字段**摊平成一个可比较的字符串。</summary>
    private static string Describe(NewGameSetupResult setup)
    {
        var lines = new List<string>
        {
            setup.State.Id.ToString(),
            setup.State.CurrentDate.ToString(),
            setup.State.Difficulty.ToString(),
            setup.State.Origin.ToString(),
            setup.State.Family.Name,
            setup.State.Family.HasShiStatus.ToString(),
            setup.HeadId?.ToString() ?? "null",
            setup.State.Economy.LivingStandard.ToString(),
            setup.State.Economy.GrainPriceIndex.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            setup.State.Economy.Treasury.Cash.Wen.ToString(System.Globalization.CultureInfo.InvariantCulture),
            setup.State.Economy.Treasury.Savings.Wen.ToString(System.Globalization.CultureInfo.InvariantCulture),
            setup.State.Economy.Treasury.MerchantCapital.Wen.ToString(System.Globalization.CultureInfo.InvariantCulture),
            setup.State.Economy.Holdings.FarmlandMu.ToString(System.Globalization.CultureInfo.InvariantCulture),
            setup.State.Economy.Holdings.RuralHouses.ToString(System.Globalization.CultureInfo.InvariantCulture),
            setup.State.Economy.Holdings.UrbanHouses.ToString(System.Globalization.CultureInfo.InvariantCulture),
            setup.State.Economy.Holdings.Shops.ToString(System.Globalization.CultureInfo.InvariantCulture),
            setup.State.Economy.Ledger.Entries.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        foreach (var person in setup.Members)
        {
            lines.Add(string.Join(
                "|",
                person.Id,
                person.Name,
                person.Gender,
                person.BirthDate,
                person.Generation,
                person.FatherId?.ToString() ?? "null",
                person.MotherId?.ToString() ?? "null",
                person.SpouseId?.ToString() ?? "null",
                person.Talents.Agriculture,
                person.Talents.Commerce,
                person.Talents.Officialdom,
                person.Talents.Craft,
                person.Study,
                person.Health,
                person.Lifespan,
                person.Rank?.Level ?? 0,
                person.Merit,
                person.MonthsInOffice,
                person.Status,
                person.Timers.SentenceRemainingMonths ?? -1,
                person.Timers.ExamBanRemainingMonths ?? -1,
                person.Timers.PromotionBanRemainingMonths ?? -1,
                person.Timers.AwaitingPostRemainingMonths ?? -1,
                person.DegreeHistory.Count,
                person.DegreeHistory.Count == 0 ? "none" : person.DegreeHistory[^1].ToString()));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void AssertSameOpening(NewGameSetupResult first, NewGameSetupResult second)
    {
        Assert.Equal(Describe(first), Describe(second));

        // 除摊平比较外，再逐条断言 SC-008 点名的字段，避免 Describe 漏项时静默假绿。
        Assert.Equal(first.Members.Count, second.Members.Count);

        var firstMembers = first.Members.ToList();
        var secondMembers = second.Members.ToList();

        for (var index = 0; index < firstMembers.Count; index++)
        {
            Assert.Equal(firstMembers[index].Id, secondMembers[index].Id);
            Assert.Equal(firstMembers[index].Name, secondMembers[index].Name);
            Assert.Equal(firstMembers[index].Gender, secondMembers[index].Gender);
            Assert.Equal(firstMembers[index].BirthDate, secondMembers[index].BirthDate);
            Assert.Equal(firstMembers[index].Generation, secondMembers[index].Generation);
            Assert.Equal(firstMembers[index].FatherId, secondMembers[index].FatherId);
            Assert.Equal(firstMembers[index].MotherId, secondMembers[index].MotherId);
            Assert.Equal(firstMembers[index].SpouseId, secondMembers[index].SpouseId);
            Assert.Equal(firstMembers[index].Talents, secondMembers[index].Talents);
            Assert.Equal(firstMembers[index].Study, secondMembers[index].Study);
            Assert.Equal(firstMembers[index].Health, secondMembers[index].Health);
            Assert.Equal(firstMembers[index].Lifespan, secondMembers[index].Lifespan);
            Assert.Equal(firstMembers[index].DegreeHistory, secondMembers[index].DegreeHistory);
        }

        Assert.Equal(first.HeadId, second.HeadId);
        Assert.Equal(first.StartDate, second.StartDate);
        Assert.Equal(first.State.Id, second.State.Id);
    }
}
