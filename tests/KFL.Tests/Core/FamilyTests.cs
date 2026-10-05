using System.Reflection;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Core;

/// <summary>
/// T026：US1 AS2 / AS4 与 FR-011 / SC-006，以及 spec Edge Cases 中属本阶段的三条
/// （婚姻一方死亡后存活方再婚、开局士出身不计仕身份、买来的旁系终身未婚仍可在家族树中定位）。
/// </summary>
public class FamilyTests
{
    // ---------------------------------------------------------------- 亲属引用一致性

    [Fact]
    public void 配偶引用双向一致()
    {
        var fixture = FamilyFixtures.Couple();
        var family = fixture.Family;
        var husband = family.TryGet(fixture.HusbandId);
        var wife = family.TryGet(fixture.WifeId);

        Assert.NotNull(husband);
        Assert.NotNull(wife);
        Assert.Equal(fixture.WifeId, husband.SpouseId);
        Assert.Equal(fixture.HusbandId, wife.SpouseId);
        Assert.Equal(fixture.WifeId, family.SpouseOf(fixture.HusbandId)?.Id);
        Assert.Equal(fixture.HusbandId, family.SpouseOf(fixture.WifeId)?.Id);
    }

    [Fact]
    public void 子女的父母引用指向正确()
    {
        var fixture = FamilyFixtures.ParentChild();
        var family = fixture.Family;
        var son = family.TryGet(fixture.ElderSonId);
        var daughter = family.TryGet(fixture.YoungerDaughterId);

        Assert.NotNull(son);
        Assert.NotNull(daughter);
        Assert.Equal(fixture.FatherId, son.FatherId);
        Assert.Equal(fixture.MotherId, son.MotherId);
        Assert.Equal(fixture.FatherId, daughter.FatherId);
        Assert.Equal(fixture.MotherId, daughter.MotherId);

        Assert.Equal([fixture.ElderSonId, fixture.YoungerDaughterId], family.ChildrenOf(fixture.FatherId).Select(c => c.Id));
        Assert.Equal([fixture.ElderSonId, fixture.YoungerDaughterId], family.ChildrenOf(fixture.MotherId).Select(c => c.Id));
    }

    [Fact]
    public void 多代同堂一次查询即可定位父母配偶与子女()
    {
        var fixture = FamilyFixtures.MultiGeneration();
        var family = fixture.Family;
        var father = family.TryGet(fixture.FatherId);

        Assert.NotNull(father);

        // 向上：父 → 祖辈。
        Assert.Equal(fixture.FounderId, father.FatherId);
        Assert.Equal(fixture.FoundressId, father.MotherId);
        Assert.Equal(fixture.MotherId, family.SpouseOf(fixture.FatherId)?.Id);

        // 向下：父 → 子辈 → 孙辈（一次查询 ChildrenOf，再各查一次）。
        var children = family.ChildrenOf(fixture.FatherId);
        Assert.Equal([fixture.SonId, fixture.DaughterId], children.Select(c => c.Id));
        Assert.Equal(
            [fixture.GrandsonId, fixture.GranddaughterId],
            family.ChildrenOf(fixture.SonId).Select(c => c.Id));

        // 从最末代向上遍历父系主轴必然终止，并抵达第一代。
        var current = family.TryGet(fixture.GrandsonId);
        var generationsUp = 0;
        while (current?.FatherId is { } up)
        {
            current = family.TryGet(up);
            generationsUp++;
            Assert.True(generationsUp < 10, "父系主轴遍历未终止，存在环。");
        }

        Assert.Equal(fixture.FounderId, current?.Id);
        Assert.Equal(3, generationsUp);
    }

    [Fact]
    public void 父母引用无环且无自环()
    {
        var fixture = FamilyFixtures.MultiGeneration();
        var family = fixture.Family;

        foreach (var member in family.Members)
        {
            Assert.NotEqual(member.Id, member.FatherId);
            Assert.NotEqual(member.Id, member.MotherId);
            Assert.NotEqual(member.Id, member.SpouseId);
        }

        Assert.Equal(
            [0, 0, 1, 1, 2, 2, 2, 3, 3],
            family.Members.Select(m => m.Generation).ToArray());
    }

    [Fact]
    public void 外嫁与已亡成员的档案仍可读且只在归档集()
    {
        var fixture = FamilyFixtures.MultiGeneration();
        var family = fixture.Family;
        var daughter = family.TryGet(fixture.DaughterId);
        var grandson = family.TryGet(fixture.GrandsonId);

        Assert.NotNull(daughter);
        Assert.NotNull(grandson);

        daughter.Status |= StatusFlag.MarriedOut;
        grandson.Status |= StatusFlag.Deceased;

        Assert.Contains(daughter, family.Members);
        Assert.Contains(grandson, family.Members);
        Assert.Equal("陈女", daughter.Name);
        Assert.Equal("陈孙", grandson.Name);
        Assert.DoesNotContain(daughter, family.RegisteredMembers);
        Assert.DoesNotContain(grandson, family.RegisteredMembers);
        Assert.Contains(daughter, family.ArchivedMembers);
        Assert.Contains(grandson, family.ArchivedMembers);
    }

    [Fact]
    public void 在册与归档的并集等于全部成员()
    {
        var fixture = FamilyFixtures.MultiGeneration();
        var family = fixture.Family;

        family.TryGet(fixture.DaughterId)!.Status |= StatusFlag.MarriedOut;
        family.TryGet(fixture.FounderId)!.Status |= StatusFlag.Deceased;

        var union = family.RegisteredMembers.Concat(family.ArchivedMembers).Select(m => m.Id).ToHashSet();

        Assert.Equal(family.Members.Count, union.Count);
        Assert.Equal(family.Members.Select(m => m.Id).ToHashSet(), union);
        Assert.Empty(family.RegisteredMembers.Intersect(family.ArchivedMembers));
    }

    [Fact]
    public void 指向非本家族成员的引用被拒()
    {
        var fixture = FamilyFixtures.ParentChild();
        var family = fixture.Family;
        var stranger = FamilyFixtures.Id(9999);

        var orphan = FamilyFixtures.NewPerson(
            900, "外来户", Gender.Male, 30, 1, generation: 1, fatherId: stranger);

        Assert.Throws<ArgumentException>(() => family.AddChild(orphan));

        var spouseOfStranger = FamilyFixtures.NewPerson(901, "冒充", Gender.Female, 30, 1, generation: 1);
        family.AddOutsider(spouseOfStranger);
        Assert.Throws<ArgumentException>(() => family.Marry(fixture.FatherId, stranger));
    }

    [Fact]
    public void 成员标识必须唯一()
    {
        var fixture = FamilyFixtures.Couple();
        var family = fixture.Family;
        var duplicate = FamilyFixtures.NewPerson(101, "重名", Gender.Male, 5, 5, generation: 0);

        Assert.Throws<ArgumentException>(() => family.AddFoundingMember(duplicate));
    }

    [Fact]
    public void Family是配偶变更的唯一入口()
    {
        var publicMethods = typeof(Person)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .ToList();

        Assert.DoesNotContain("SetSpouse", publicMethods);
        Assert.DoesNotContain("AddFormerSpouse", publicMethods);
        Assert.DoesNotContain("SetGeneration", publicMethods);

        var spouseProperty = typeof(Person).GetProperty(nameof(Person.SpouseId));
        Assert.NotNull(spouseProperty);
        Assert.True(spouseProperty.SetMethod is null || !spouseProperty.SetMethod.IsPublic);
    }

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

    // ---------------------------------------------------------------- 出身 × 仕身份（§10.2）

    [Fact]
    public void 四出身与仕身份八种组合均可构造且互不约束()
    {
        var origins = new[] { Origin.Farmer, Origin.Artisan, Origin.Merchant, Origin.Scholar };
        var combinations = 0;

        foreach (var origin in origins)
        {
            foreach (var hasShiStatus in new[] { false, true })
            {
                var family = new Family("测试") { HasShiStatus = hasShiStatus };
                var member = family.AddFoundingMember(
                    FamilyFixtures.NewPerson(300, "开局者", Gender.Male, 1, 1, generation: 0));

                // 士出身 + 举人，但仕身份为 false：开局士出身不计仕身份（§10.2、spec Edge Case）。
                if (origin == Origin.Scholar)
                {
                    member.AppendDegree(new DegreeRecord(
                        DegreeLevel.JuRen, null, new GameDate(1, 1), DegreeChangeCause.Initial));
                }

                Assert.Equal(hasShiStatus, family.HasShiStatus);
                Assert.Equal(origin, origin);
                Assert.Equal(Origin.Scholar == origin ? DegreeLevel.JuRen : DegreeLevel.BaiShen, member.CurrentDegree);
                combinations++;
            }
        }

        Assert.Equal(8, combinations);
    }

    [Fact]
    public void 仕身份可独立切换且不影响功名()
    {
        var family = new Family("测试");
        var member = family.AddFoundingMember(
            FamilyFixtures.NewPerson(301, "开局者", Gender.Male, 1, 1, generation: 0));

        Assert.False(family.HasShiStatus);
        family.HasShiStatus = true;
        Assert.True(family.HasShiStatus);
        family.HasShiStatus = false;
        Assert.False(family.HasShiStatus);
        Assert.Equal(DegreeLevel.BaiShen, member.CurrentDegree);
    }

    // ---------------------------------------------------------------- 辈分与家主专项（research R-15）

    [Fact]
    public void 开局成员辈分为零()
    {
        var fixture = FamilyFixtures.MultiGeneration();
        var family = fixture.Family;

        Assert.Equal(0, family.TryGet(fixture.FounderId)!.Generation);
        Assert.Equal(0, family.TryGet(fixture.FoundressId)!.Generation);
    }

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
    public void 家主可为空且指向非本家族成员时被拒()
    {
        var family = new Family("空族");

        Assert.Null(family.HeadId);
        family.SetHead(null);
        Assert.Null(family.HeadId);

        Assert.Throws<InvalidOperationException>(() => family.SetHead(FamilyFixtures.Id(8888)));

        // 无在册男性成员时（该情形不构成绝嗣）买来者的辈分无从推导。
        Assert.Throws<InvalidOperationException>(() => _ = family.BoughtCollateralGeneration);
    }

    [Fact]
    public void 家主不得指向已归档成员()
    {
        var fixture = FamilyFixtures.MultiGeneration();
        var family = fixture.Family;

        family.TryGet(fixture.DaughterId)!.Status |= StatusFlag.MarriedOut;
        family.TryGet(fixture.SonId)!.Status |= StatusFlag.Deceased;

        Assert.Throws<InvalidOperationException>(() => family.SetHead(fixture.DaughterId));
        Assert.Throws<InvalidOperationException>(() => family.SetHead(fixture.SonId));
        Assert.Equal(fixture.FounderId, family.HeadId);
    }

    [Fact]
    public void 家主可指向在册成员()
    {
        var fixture = FamilyFixtures.MultiGeneration();
        var family = fixture.Family;

        family.SetHead(fixture.SonId);

        Assert.Equal(fixture.SonId, family.HeadId);
        Assert.Contains(family.TryGet(fixture.SonId)!, family.RegisteredMembers);
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
