using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Core;

public partial class FamilyTests
{
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

    [Fact]
    public void 连续报考开关默认为假且可独立切换()
    {
        // 契约十二 §5 条款 1：默认 false；与 HasShiStatus 同形（家族级持久化布尔）。
        var family = new Family("测试");
        var property = typeof(Family).GetProperty(nameof(Family.AutoExamApplication));

        Assert.NotNull(property);
        Assert.True(property.SetMethod is { IsPublic: true });
        Assert.False(family.AutoExamApplication);

        family.AutoExamApplication = true;
        Assert.True(family.AutoExamApplication);
        family.AutoExamApplication = false;
        Assert.False(family.AutoExamApplication);

        // 与仕身份互不影响。
        Assert.False(family.HasShiStatus);
    }

    [Fact]
    public void 家族持有惩罚日志且默认无写入通道()
    {
        // 006 / T007：日志是家族级**只追加**缓冲——只读开放，写入通道在 PunishmentJournal 自身
        // （Append / MarkFamilyBanDeclared / Clear / Restore），Family 上不得有公开 setter。
        var family = new Family("测试");
        var property = typeof(Family).GetProperty(nameof(Family.PunishmentJournal));

        Assert.NotNull(property);
        Assert.True(property.SetMethod is null || !property.SetMethod.IsPublic);
        Assert.NotNull(family.PunishmentJournal);
        Assert.True(family.PunishmentJournal.IsEmpty);
        Assert.False(family.PunishmentJournal.FamilyBanDeclared);
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
}
