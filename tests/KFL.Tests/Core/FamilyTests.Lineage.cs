using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Core;

public partial class FamilyTests
{
    [Fact]
    public void 血亲辈分等于父母辈分加一且无写入通道()
    {
        var fixture = FamilyFixtures.MultiGeneration();
        var family = fixture.Family;

        Assert.Equal(1, family.TryGet(fixture.FatherId)!.Generation);
        Assert.Equal(2, family.TryGet(fixture.SonId)!.Generation);
        Assert.Equal(3, family.TryGet(fixture.GrandsonId)!.Generation);

        var generationProperty = typeof(Person).GetProperty(nameof(Person.Generation));
        Assert.NotNull(generationProperty);
        Assert.True(generationProperty.SetMethod is null || !generationProperty.SetMethod.IsPublic);

        // 血亲的辈分经 Family 的入口同样被拒（终身不可变更）。
        Assert.Throws<InvalidOperationException>(() => family.SetOutsiderGeneration(fixture.FatherId, 5));

        // 辈分写错的血亲根本加不进家族。
        var wrongGeneration = FamilyFixtures.NewPerson(
            400, "错辈", Gender.Male, 40, 1, generation: 9, fatherId: fixture.FatherId);
        Assert.Throws<ArgumentException>(() => family.AddChild(wrongGeneration));
    }

    [Fact]
    public void 娶入配偶的辈分等于其配偶辈分()
    {
        var fixture = FamilyFixtures.MarriedIn();
        var family = fixture.Family;
        var host = family.TryGet(fixture.SpouseHostId);
        var marriedIn = family.TryGet(fixture.MarriedInId);

        Assert.NotNull(host);
        Assert.NotNull(marriedIn);
        Assert.True(marriedIn.IsOutsider);
        Assert.Equal(host.Generation, marriedIn.Generation);
        Assert.Equal(1, marriedIn.Generation);
    }

    [Fact]
    public void 买来的旁系辈分为家主辈分加一且终身未婚仍可定位()
    {
        var fixture = FamilyFixtures.BoughtCollateral();
        var family = fixture.Family;
        var head = family.TryGet(fixture.HeadId);
        var collateral = family.TryGet(fixture.CollateralId);

        Assert.NotNull(head);
        Assert.NotNull(collateral);
        Assert.True(collateral.IsOutsider);
        Assert.Null(collateral.FatherId);
        Assert.Null(collateral.MotherId);
        Assert.Null(collateral.SpouseId);
        Assert.Equal(head.Generation + 1, collateral.Generation);

        // 开局家主治下取 1（§4.4「辈分自创始者为 0」）。
        Assert.Equal(1, collateral.Generation);
        Assert.Contains(collateral, family.Members);
        Assert.Empty(family.ChildrenOf(collateral.Id));
    }

    [Fact]
    public void 外来者已有子女后再改辈分被拒()
    {
        var fixture = FamilyFixtures.BoughtCollateral();
        var family = fixture.Family;

        // 尚无子女时可改。
        family.SetOutsiderGeneration(fixture.CollateralId, 2);
        Assert.Equal(2, family.TryGet(fixture.CollateralId)!.Generation);

        var child = family.AddChild(FamilyFixtures.NewPerson(
            410, "旁系之子", Gender.Male, 50, 1, generation: 3, fatherId: fixture.CollateralId));
        Assert.Equal(fixture.CollateralId, child.FatherId);

        Assert.Throws<InvalidOperationException>(() => family.SetOutsiderGeneration(fixture.CollateralId, 1));
        Assert.Equal(2, family.TryGet(fixture.CollateralId)!.Generation);
    }

    [Fact]
    public void 外来者辈分在其尚无子女时落定()
    {
        var fixture = FamilyFixtures.BoughtCollateral();
        var family = fixture.Family;

        family.SetOutsiderGeneration(fixture.CollateralId, 3);

        var wife = family.AddOutsider(FamilyFixtures.NewPerson(420, "买来者之妻", Gender.Female, 50, 2, generation: 3));
        family.Marry(fixture.CollateralId, wife.Id);

        Assert.Equal(3, family.TryGet(fixture.CollateralId)!.Generation);
        Assert.Equal(3, wife.Generation);
    }

    [Fact]
    public void 外来者辈分至多落定一次()
    {
        var fixture = FamilyFixtures.BoughtCollateral();
        var family = fixture.Family;

        family.SetOutsiderGeneration(fixture.CollateralId, 2);

        // 尚未成婚、也没有子女，但「落定」只有一次（规格书 §4.4）。
        Assert.Throws<InvalidOperationException>(() => family.SetOutsiderGeneration(fixture.CollateralId, 3));
        Assert.Equal(2, family.TryGet(fixture.CollateralId)!.Generation);
    }

    [Fact]
    public void 外来者首次家族内成婚时辈分额外变动一次()
    {
        var family = new Family("测试");

        var founder = family.AddFoundingMember(FamilyFixtures.NewPerson(450, "鼻祖", Gender.Male, 1, 1, generation: 0));
        var son = family.AddChild(FamilyFixtures.NewPerson(
            451, "长子", Gender.Male, 20, 1, generation: 1, fatherId: founder.Id));
        var marriedIn = family.AddOutsider(FamilyFixtures.NewPerson(452, "娶入", Gender.Female, 18, 2, generation: 5));

        family.Marry(son.Id, marriedIn.Id);

        // 首次家族内成婚 = 落定之外的**额外**那一次变动，对齐到家族内配偶的辈分。
        Assert.Equal(1, marriedIn.Generation);

        // 这一次用过即终局：不能再由 SetOutsiderGeneration 指定。
        Assert.Throws<InvalidOperationException>(() => family.SetOutsiderGeneration(marriedIn.Id, 3));
        Assert.Equal(1, marriedIn.Generation);
    }

    [Fact]
    public void 外来者丧偶再婚不再变动辈分()
    {
        // 本用例只验辈分规则：外来者先嫁（血亲辈分 1）对齐到 1，丧偶后再嫁（血亲辈分 2）MUST NOT 再变。
        var family = new Family("测试");

        var founder = family.AddFoundingMember(FamilyFixtures.NewPerson(460, "鼻祖", Gender.Male, 1, 1, generation: 0));
        var elderSon = family.AddChild(FamilyFixtures.NewPerson(
            461, "长子", Gender.Male, 20, 1, generation: 1, fatherId: founder.Id));
        var youngerSon = family.AddChild(FamilyFixtures.NewPerson(
            462, "次子", Gender.Male, 22, 1, generation: 1, fatherId: founder.Id));
        var nephew = family.AddChild(FamilyFixtures.NewPerson(
            463, "侄", Gender.Male, 44, 1, generation: 2, fatherId: youngerSon.Id));
        var widow = family.AddOutsider(FamilyFixtures.NewPerson(464, "外来寡", Gender.Female, 18, 2, generation: 5));

        family.Marry(elderSon.Id, widow.Id);
        Assert.Equal(1, widow.Generation);

        family.EndMarriage(widow.Id);
        family.Marry(widow.Id, nephew.Id);

        Assert.Equal(1, widow.Generation);
        Assert.Equal(2, nephew.Generation);
    }

    [Fact]
    public void 开局成员必须无父母且辈分为零()
    {
        var family = new Family("测试");

        var withParents = FamilyFixtures.NewPerson(
            430, "冒充开局", Gender.Male, 1, 1, generation: 0, fatherId: FamilyFixtures.Id(431));
        Assert.Throws<ArgumentException>(() => family.AddFoundingMember(withParents));

        var wrongGeneration = FamilyFixtures.NewPerson(432, "错辈", Gender.Male, 1, 1, generation: 2);
        Assert.Throws<ArgumentException>(() => family.AddFoundingMember(wrongGeneration));

        var withParentsAsOutsider = FamilyFixtures.NewPerson(
            433, "有父母的外来者", Gender.Male, 1, 1, generation: 0,
            fatherId: FamilyFixtures.Id(1));
        Assert.Throws<ArgumentException>(() => family.AddOutsider(withParentsAsOutsider));
    }
}
