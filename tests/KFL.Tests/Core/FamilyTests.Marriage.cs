using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Core;

public partial class FamilyTests
{
    // ---------------------------------------------------------------- 婚姻专项（FR-011、§9.1）

    [Fact]
    public void 一夫一妻已有配偶者再被指定配偶时被拒()
    {
        var fixture = FamilyFixtures.Couple();
        var family = fixture.Family;
        var intruder = family.AddOutsider(FamilyFixtures.NewPerson(110, "第三者", Gender.Female, 5, 5, generation: 0));

        Assert.Throws<InvalidOperationException>(() => family.Marry(fixture.HusbandId, intruder.Id));
        Assert.Throws<InvalidOperationException>(() => family.Marry(intruder.Id, fixture.WifeId));

        Assert.Equal(fixture.WifeId, family.TryGet(fixture.HusbandId)!.SpouseId);
        Assert.Null(intruder.SpouseId);
    }

    [Fact]
    public void 辈分无法对齐导致成婚被拒时不留下半提交的婚姻关系()
    {
        var family = new Family("测试");

        var founder = family.AddFoundingMember(FamilyFixtures.NewPerson(470, "鼻祖", Gender.Male, 1, 1, generation: 0));
        var firstSon = family.AddChild(FamilyFixtures.NewPerson(
            471, "长子", Gender.Male, 20, 1, generation: 1, fatherId: founder.Id));
        var secondSon = family.AddChild(FamilyFixtures.NewPerson(
            472, "次子", Gender.Male, 22, 1, generation: 1, fatherId: founder.Id));
        var outsider = family.AddOutsider(FamilyFixtures.NewPerson(473, "外来女", Gender.Female, 18, 2, generation: 5));

        // 外来者先有子女：其辈分此后 MUST NOT 再变，成婚时的对齐必然失败。
        family.AddChild(FamilyFixtures.NewPerson(
            474, "其子", Gender.Male, 40, 1, generation: 2, fatherId: firstSon.Id, motherId: outsider.Id));

        Assert.Throws<InvalidOperationException>(() => family.Marry(outsider.Id, secondSon.Id));

        // 「校验失败即拒绝，不产生非法档案」（data-model §5）：被拒的成婚 MUST NOT 留下任何已生效状态。
        Assert.Null(family.TryGet(outsider.Id)!.SpouseId);
        Assert.Null(family.TryGet(secondSon.Id)!.SpouseId);
        Assert.Equal(5, family.TryGet(outsider.Id)!.Generation);
    }

    [Fact]
    public void 丧偶再婚时前任进既往配偶且子女父母引用不变()
    {
        var fixture = FamilyFixtures.ParentChild();
        var family = fixture.Family;
        var childrenBefore = family.ChildrenOf(fixture.FatherId).Select(c => (c.Id, c.FatherId, c.MotherId)).ToList();

        family.EndMarriage(fixture.FatherId);

        var father = family.TryGet(fixture.FatherId);
        var mother = family.TryGet(fixture.MotherId);
        Assert.NotNull(father);
        Assert.NotNull(mother);
        Assert.Null(father.SpouseId);
        Assert.Null(mother.SpouseId);
        Assert.Equal([fixture.MotherId], father.FormerSpouseIds);
        Assert.Equal([fixture.FatherId], mother.FormerSpouseIds);

        // 再婚。
        var newWife = family.AddOutsider(FamilyFixtures.NewPerson(210, "继室", Gender.Female, 25, 5, generation: 0));
        family.Marry(fixture.FatherId, newWife.Id);

        Assert.Equal(newWife.Id, father.SpouseId);
        Assert.Equal(fixture.FatherId, newWife.SpouseId);
        Assert.DoesNotContain(father.FormerSpouseIds, id => id == father.SpouseId);
        Assert.Equal(father.FormerSpouseIds.Distinct().Count(), father.FormerSpouseIds.Count);

        // 子女的父母引用 MUST NOT 断裂。
        var childrenAfter = family.ChildrenOf(fixture.FatherId).Select(c => (c.Id, c.FatherId, c.MotherId)).ToList();
        Assert.Equal(childrenBefore, childrenAfter);
    }

    [Fact]
    public void 没有配偶时终止婚姻被拒()
    {
        var fixture = FamilyFixtures.BoughtCollateral();

        Assert.Throws<InvalidOperationException>(() => fixture.Family.EndMarriage(fixture.CollateralId));
    }

    [Fact]
    public void 成员不能与自己成婚()
    {
        var fixture = FamilyFixtures.BoughtCollateral();

        Assert.Throws<ArgumentException>(() => fixture.Family.Marry(fixture.HeadId, fixture.HeadId));
    }

    [Fact]
    public void 婚姻终止丢弃该夫妇的生育簿记()
    {
        var family = new Family("测试");
        var husband = family.AddFoundingMember(
            FamilyFixtures.NewPerson(1, "甲", Gender.Male, 1, 1, generation: 0));
        var wife = family.AddOutsider(
            FamilyFixtures.NewPerson(2, "乙", Gender.Female, 2, 3, generation: 0));
        family.Marry(husband.Id, wife.Id);

        var couple = new CoupleKey(husband.Id, wife.Id);
        var month = new GameDate(10, 3);
        family.Lifecycle.RecordBirthApplication(couple, month.Year);
        family.Lifecycle.ScheduleBirth(new PendingBirth(couple, month));

        family.EndMarriage(husband.Id);

        Assert.Empty(family.Lifecycle.AppliedBirthCouples);
        Assert.Empty(family.Lifecycle.PendingBirths);
    }

    [Fact]
    public void 再婚丢弃该成员从前的生育簿记且不影响他人()
    {
        var family = new Family("测试");
        var first = family.AddFoundingMember(
            FamilyFixtures.NewPerson(1, "甲", Gender.Male, 1, 1, generation: 0));
        var second = family.AddFoundingMember(
            FamilyFixtures.NewPerson(2, "丙", Gender.Male, 1, 1, generation: 0));
        var wifeOfFirst = family.AddOutsider(
            FamilyFixtures.NewPerson(3, "乙", Gender.Female, 2, 3, generation: 0));
        var wifeOfSecond = family.AddOutsider(
            FamilyFixtures.NewPerson(4, "丁", Gender.Female, 2, 3, generation: 0));

        family.Marry(first.Id, wifeOfFirst.Id);
        family.Marry(second.Id, wifeOfSecond.Id);

        var stale = new CoupleKey(first.Id, wifeOfFirst.Id);
        var alive = new CoupleKey(second.Id, wifeOfSecond.Id);
        var month = new GameDate(10, 3);

        family.Lifecycle.RecordBirthApplication(stale, month.Year);
        family.Lifecycle.RecordBirthApplication(alive, month.Year);

        family.EndMarriage(first.Id);
        family.Marry(first.Id, wifeOfFirst.Id);

        // 甲的旧簿记在终止时就已丢弃；乙（第二对）的簿记 MUST 完好无损。
        Assert.Equal([alive], family.Lifecycle.AppliedBirthCouples);
    }
}
