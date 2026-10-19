using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;
using KFL.Rules.FamilyLifecycle;
using KFL.Rules.Settlement;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// 玩家反馈 1（2026-10-19）：族内通婚（**只允许同代**、不考虑遗传病）与离婚。
/// </summary>
/// <remarks>
/// 规则数值一律经 <c>KFL.Rules/Config</c> 与规则层公开面取得（SC-008）；
/// 本文件的期望值不另存规则数值副本。
/// </remarks>
public class EndogamyDivorceTests
{
    private static readonly Guid StateId = Guid.Parse("b1f4c7a2-3d58-4e19-8c60-7a2b9d4e5f31");

    [Fact]
    public void 同代异性成员可在族内成婚且不产生任何费用()
    {
        var husband = RulesHarness.Member(1, Gender.Male, 24);
        var wife = RulesHarness.Member(2, Gender.Female, 20);
        var state = State(RulesHarness.FamilyOf(husband, wife), out var economy);

        var outcome = LifecycleEntry.TryMarryWithinFamily(state, husband, wife);

        Assert.True(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.None, outcome.RejectReason);
        Assert.Equal(Money.Zero, outcome.Amount);
        Assert.Null(outcome.Entry);
        Assert.Equal(wife.Id, husband.SpouseId);
        Assert.Equal(husband.Id, wife.SpouseId);
        Assert.Empty(economy.Ledger.Entries);
    }

    [Fact]
    public void 同代同性成员被拒()
    {
        var first = RulesHarness.Member(3, Gender.Male, 24);
        var second = RulesHarness.Member(4, Gender.Male, 22);
        var state = State(RulesHarness.FamilyOf(first, second), out _);

        var outcome = LifecycleEntry.TryMarryWithinFamily(state, first, second);

        Assert.False(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.NotMarriageable, outcome.RejectReason);
        Assert.Null(first.SpouseId);
    }

    [Fact]
    public void 跨代成员被拒且给出同代原因()
    {
        var father = RulesHarness.Member(5, Gender.Male, 44);
        var mother = RulesHarness.Member(6, Gender.Female, 40);
        var family = RulesHarness.FamilyOf(father, mother);
        family.Marry(father.Id, mother.Id);

        var daughter = WithParents(RulesHarness.Member(7, Gender.Female, 19), father.Id, mother.Id, generation: 1);
        family.AddChild(daughter);

        var state = State(family, out _);

        var outcome = LifecycleEntry.TryMarryWithinFamily(state, father, daughter);

        Assert.False(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.NotSameGeneration, outcome.RejectReason);
    }

    [Fact]
    public void 已有配偶者不可再成婚()
    {
        var husband = RulesHarness.Member(8, Gender.Male, 30);
        var wife = RulesHarness.Member(9, Gender.Female, 28);
        var other = RulesHarness.Member(10, Gender.Female, 26);
        var family = RulesHarness.FamilyOf(husband, wife, other);
        family.Marry(husband.Id, wife.Id);

        var state = State(family, out _);

        var outcome = LifecycleEntry.TryMarryWithinFamily(state, husband, other);

        Assert.False(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.NotMarriageable, outcome.RejectReason);
        Assert.Equal(wife.Id, husband.SpouseId);
    }

    [Fact]
    public void 离异清空双方配偶并记入前任名单()
    {
        var husband = RulesHarness.Member(11, Gender.Male, 30);
        var wife = RulesHarness.Member(12, Gender.Female, 28);
        var family = RulesHarness.FamilyOf(husband, wife);
        family.Marry(husband.Id, wife.Id);
        var state = State(family, out _);

        var outcome = LifecycleEntry.Divorce(state, husband);

        Assert.True(outcome.Accepted);
        Assert.Null(husband.SpouseId);
        Assert.Null(wife.SpouseId);
        Assert.Contains(wife.Id, husband.FormerSpouseIds);
        Assert.Contains(husband.Id, wife.FormerSpouseIds);
        Assert.False(LifecycleQueries.CanDivorce(husband));
    }

    [Fact]
    public void 无配偶者离异被拒()
    {
        var single = RulesHarness.Member(13, Gender.Male, 30);
        var state = State(RulesHarness.FamilyOf(single), out _);

        var outcome = LifecycleEntry.Divorce(state, single);

        Assert.False(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.NotSpouse, outcome.RejectReason);
    }

    [Fact]
    public void 候选名单只含同代在册未婚成年异性()
    {
        var husband = RulesHarness.Member(14, Gender.Male, 30);
        var wife = RulesHarness.Member(15, Gender.Female, 28);
        var brother = RulesHarness.Member(16, Gender.Male, 26);
        var sister = RulesHarness.Member(17, Gender.Female, 22);
        var family = RulesHarness.FamilyOf(husband, wife, brother, sister);
        family.Marry(husband.Id, wife.Id);

        var candidates = LifecycleQueries.EndogamyCandidates(family, sister, RulesHarness.Date);

        Assert.Contains(candidates, person => person.Id.Equals(brother.Id));
        Assert.DoesNotContain(candidates, person => person.Id.Equals(husband.Id));
        Assert.DoesNotContain(candidates, person => person.Id.Equals(wife.Id));
        Assert.DoesNotContain(candidates, person => person.Id.Equals(sister.Id));
    }

    private static GameState State(Family family, out FamilyEconomy economy)
    {
        economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(RulesHarness.Guan(20)),
            new Holdings());

        return new GameState(StateId, RulesHarness.Date, Difficulty.Normal, Origin.Farmer, family, economy);
    }

    /// <summary>把无父母夹具「改造」成某个家庭的子女（用于跨代用例）。</summary>
    /// <param name="template">原成员（取其 Id 与姓名）。</param>
    /// <param name="fatherId">父。</param>
    /// <param name="motherId">母。</param>
    /// <param name="generation">辈分。</param>
    /// <returns>带父母引用的新成员。</returns>
    private static Person WithParents(Person template, PersonId fatherId, PersonId motherId, int generation)
    {
        ArgumentNullException.ThrowIfNull(template);

        return new Person(
            template.Id,
            template.Name,
            template.Gender,
            template.BirthDate,
            template.Talents,
            template.Lifespan,
            generation,
            fatherId,
            motherId);
    }
}
