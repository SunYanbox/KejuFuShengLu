using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;
using KFL.Rules.FamilyLifecycle;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// 家谱派生查询（2026-10-19 第五轮玩家反馈）：
/// 「买入成员」身份由账本 <see cref="LedgerCategory.PersonBought"/> 事件条目**派生**——
/// 买来者组内通婚所生的子女没有该条目，因此不会被误标；
/// 「家主支脉」= 家主本人 + 直系血亲祖先 + 全部直系血亲后代 + 这些人的当前配偶，不含旁系。
/// </summary>
public class GenealogyQueriesTests
{
    [Fact]
    public void 没有买人记录时购买集合为空()
    {
        var family = Household(out _);
        var state = State(family, RulesHarness.Guan(1000));

        Assert.Empty(GenealogyQueries.PurchasedPersonIds(state));
    }

    [Fact]
    public void 买入成员进入购买集合而买入成员所生子女不在其中()
    {
        var family = Household(out var head);
        var state = State(family, RulesHarness.Guan(1000));

        Assert.True(LifecycleEntry
            .BuyPerson(state, new FixedRandomService(), new FixedNameGenerator())
            .Accepted);

        var bought = Assert.Single(family.Members, member => member.Id != head.Id);
        var ids = GenealogyQueries.PurchasedPersonIds(state);
        Assert.Single(ids);
        Assert.Contains(bought.Id, ids);

        // 组内通婚所生的子女：有父母参照、无 PersonBought 条目 ⇒ 不算买入成员。
        var child = FamilyFixtures.NewPerson(
            90,
            "陈生",
            Gender.Male,
            RulesHarness.Date.Year - 1,
            RulesHarness.Date.Month,
            generation: bought.Generation + 1,
            fatherId: bought.Id);
        family.AddChild(child);

        var afterChildbirth = GenealogyQueries.PurchasedPersonIds(state);
        Assert.Single(afterChildbirth);
        Assert.Contains(bought.Id, afterChildbirth);
        Assert.DoesNotContain(child.Id, afterChildbirth);
    }

    [Fact]
    public void 多次买入逐条进入购买集合()
    {
        var family = Household(out var head);
        var state = State(family, RulesHarness.Guan(1000));

        Assert.True(LifecycleEntry.BuyPerson(state, new FixedRandomService(), new FixedNameGenerator()).Accepted);
        Assert.True(LifecycleEntry.BuyPerson(state, new FixedRandomService(), new FixedNameGenerator()).Accepted);

        var bought = family.Members.Where(member => member.Id != head.Id).Select(member => member.Id).ToHashSet();
        var ids = GenealogyQueries.PurchasedPersonIds(state);

        Assert.Equal(2, ids.Count);
        Assert.Equal(bought, ids.ToHashSet());
    }

    [Fact]
    public void 家主支脉含直系祖先后代与配偶但不含旁系()
    {
        var founder = FamilyFixtures.NewPerson(1, "陈祖", Gender.Male, RulesHarness.Date.Year - 70, 1, generation: 0);
        var family = RulesHarness.FamilyOf(founder);
        var head = FamilyFixtures.NewPerson(2, "陈家主", Gender.Male, RulesHarness.Date.Year - 45, 1, generation: 1, fatherId: founder.Id);
        family.AddChild(head);
        var uncle = FamilyFixtures.NewPerson(3, "陈叔", Gender.Male, RulesHarness.Date.Year - 42, 1, generation: 1, fatherId: founder.Id);
        family.AddChild(uncle);
        var son = FamilyFixtures.NewPerson(4, "陈子", Gender.Male, RulesHarness.Date.Year - 20, 1, generation: 2, fatherId: head.Id);
        family.AddChild(son);
        var cousin = FamilyFixtures.NewPerson(5, "陈堂", Gender.Male, RulesHarness.Date.Year - 18, 1, generation: 2, fatherId: uncle.Id);
        family.AddChild(cousin);
        var wife = FamilyFixtures.NewPerson(6, "王妻", Gender.Female, RulesHarness.Date.Year - 44, 1, generation: 1);
        family.AddOutsider(wife);
        family.Marry(head.Id, wife.Id);
        family.SetHead(head.Id);

        var branch = GenealogyQueries.HeadBranchPersonIds(family);

        Assert.Contains(head.Id, branch);
        Assert.Contains(founder.Id, branch);
        Assert.Contains(son.Id, branch);
        Assert.Contains(wife.Id, branch);
        Assert.DoesNotContain(uncle.Id, branch);
        Assert.DoesNotContain(cousin.Id, branch);
        Assert.Equal(4, branch.Count);
    }

    [Fact]
    public void 家主空缺时支脉为空集()
    {
        var head = RulesHarness.Member(1, Gender.Male, 40);
        var family = new Family("测");
        family.AddFoundingMember(head);

        Assert.Null(family.HeadId);
        Assert.Empty(GenealogyQueries.HeadBranchPersonIds(family));
    }

    private static Family Household(out Person head)
    {
        head = RulesHarness.Member(1, Gender.Male, 40);
        return RulesHarness.FamilyOf(head);
    }

    private static GameState State(Family family, Money cash) =>
        new(
            Guid.Parse("7d3f5b21-9c84-4e6a-b0d7-2f8e1a4c6b93"),
            RulesHarness.Date,
            Difficulty.Normal,
            Origin.Artisan,
            family,
            new FamilyEconomy(
                LivingStandard.Normal,
                new GrainPriceIndex(GrainPricePolicy.Initial),
                new Treasury(cash),
                new Holdings()));
}
