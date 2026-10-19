using System.Reflection;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Core;

/// <summary>
/// T026：US1 AS2 / AS4 与 FR-011 / SC-006，以及 spec Edge Cases 中属本阶段的三条
/// （婚姻一方死亡后存活方再婚、开局士出身不计仕身份、买来的旁系终身未婚仍可在家族树中定位）。
/// </summary>
public partial class FamilyTests
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
}
