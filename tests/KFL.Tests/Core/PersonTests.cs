using System.Reflection;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Core;

/// <summary>
/// T024：US1 AS1 与 FR-004 / FR-007 / FR-008 / SC-005——成员档案的字段可读可写、
/// 无写入通道的成员全集，以及功名变迁历史。
/// </summary>
public class PersonTests
{
    /// <summary>data-model §2.1 不变量 2 的**完整清单**（13 个），不是抽样。</summary>
    public static TheoryData<string> MembersWithoutWriteChannel => new()
    {
        "Id",
        "Gender",
        "BirthDate",
        "Talents",
        "Lifespan",
        "Generation",
        "FatherId",
        "MotherId",
        "SpouseId",
        "FormerSpouseIds",
        "DegreeHistory",
        "CurrentDegree",
        "CurrentPlacement",
    };

    /// <summary><see cref="Person"/> 自持的可写属性，只有这十个。</summary>
    public static TheoryData<string> WritableMembers => new()
    {
        "Name",
        "Study",
        "Health",
        "Rank",
        "Merit",
        "Status",
        "Timers",
        "Occupation",
        "MonthsInOffice",
        "EntryTrack",
    };

    [Fact]
    public void 档案字段均可读取()
    {
        var fixture = FamilyFixtures.MultiGeneration();
        var person = fixture.Family.TryGet(fixture.FounderId);

        Assert.NotNull(person);
        Assert.NotEqual(default, person.Id);
        Assert.Equal("陈祖", person.Name);
        Assert.Equal(Gender.Male, person.Gender);
        Assert.Equal(0, person.Generation);
        Assert.Equal(new GameDate(1, 1), person.BirthDate);
        Assert.Equal(50, person.Talents.Agriculture);
        Assert.Equal(50, person.Talents.Commerce);
        Assert.Equal(50, person.Talents.Officialdom);
        Assert.Equal(50, person.Talents.Craft);
        Assert.Equal(70, person.Lifespan);
        Assert.Equal(DegreeLevel.BaiShen, person.CurrentDegree);
        Assert.Null(person.Rank);
        Assert.Equal(0, person.Merit);
        Assert.Equal(StatusFlag.None, person.Status);
        Assert.Equal(Occupation.None, person.Occupation);
        Assert.NotNull(person.SpouseId);
    }

    [Fact]
    public void 天赋学业体质均落在零到一百()
    {
        var fixture = FamilyFixtures.MultiGeneration();
        var person = fixture.Family.TryGet(fixture.FatherId);

        Assert.NotNull(person);
        Assert.InRange(person.Talents.Agriculture, 0, 100);
        Assert.InRange(person.Talents.Commerce, 0, 100);
        Assert.InRange(person.Talents.Officialdom, 0, 100);
        Assert.InRange(person.Talents.Craft, 0, 100);
        Assert.InRange(person.Study, 0, 100);
        Assert.InRange(person.Health, 0, 100);

        person.Study = 100;
        person.Health = 0;
        Assert.Equal(100, person.Study);
        Assert.Equal(0, person.Health);

        Assert.Throws<ArgumentOutOfRangeException>(() => person.Study = 101);
        Assert.Throws<ArgumentOutOfRangeException>(() => person.Study = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => person.Health = 101);
        Assert.Throws<ArgumentOutOfRangeException>(() => person.Health = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => person.Merit = -1);
    }

    [Fact]
    public void 可写属性确实可写()
    {
        var person = FamilyFixtures.NewPerson(500, "测试", Gender.Male, 1, 1, generation: 0);

        person.Name = "改名";
        person.Study = 37;
        person.Health = 64;
        person.Rank = new OfficialRank(7);
        person.Merit = 12;
        person.Status = StatusFlag.Retired;
        person.Timers = default;
        person.Occupation = Occupation.Farming;
        person.MonthsInOffice = 3;

        Assert.Equal("改名", person.Name);
        Assert.Equal(37, person.Study);
        Assert.Equal(64, person.Health);
        Assert.Equal(7, person.Rank.GetValueOrDefault().Level);
        Assert.Equal(12, person.Merit);
        Assert.Equal(StatusFlag.Retired, person.Status);
        Assert.Equal(Occupation.Farming, person.Occupation);
        Assert.Equal(3, person.MonthsInOffice);
    }

    [Fact]
    public void 入仕途径与待阙位成对且清位前必须先清途径()
    {
        var person = FamilyFixtures.NewPerson(503, "待阙者", Gender.Male, 1, 1, generation: 0);

        // 默认无途径。
        Assert.Null(person.EntryTrack);

        // 位为假时禁止落途径（与计时字段同款交叉校验）。
        Assert.Throws<ArgumentException>(() => person.EntryTrack = AppointmentTrack.FirstClass);

        person.Status |= StatusFlag.AwaitingPost;
        person.Timers = new StatusTimers(null, null, null, 6);
        person.EntryTrack = AppointmentTrack.FirstClass;

        Assert.Equal(AppointmentTrack.FirstClass, person.EntryTrack.GetValueOrDefault());

        // 清位前 MUST 先把途径与计时都置空（次序：计时 → 途径 → 位）。
        Assert.Throws<ArgumentException>(() => person.Status &= ~StatusFlag.AwaitingPost);

        person.EntryTrack = null;

        Assert.Throws<ArgumentException>(() => person.Status &= ~StatusFlag.AwaitingPost);

        person.Timers = default;
        person.Status &= ~StatusFlag.AwaitingPost;

        Assert.Null(person.EntryTrack);
        Assert.False(person.Status.HasFlag(StatusFlag.AwaitingPost));
    }

    [Fact]
    public void 在职月数默认零且赋负值被拒()
    {
        var person = FamilyFixtures.NewPerson(502, "官员", Gender.Male, 1, 1, generation: 0);

        Assert.Equal(0, person.MonthsInOffice);
        Assert.Throws<ArgumentOutOfRangeException>(() => person.MonthsInOffice = -1);

        person.MonthsInOffice = 0;
        Assert.Equal(0, person.MonthsInOffice);
    }

    [Theory]
    [MemberData(nameof(MembersWithoutWriteChannel))]
    public void 无写入通道的成员没有公开setter(string propertyName)
    {
        var property = typeof(Person).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(property);
        Assert.True(
            property.SetMethod is null || !property.SetMethod.IsPublic,
            $"{propertyName} MUST NOT 有公开 setter（data-model §2.1 不变量 2）。");
    }

    [Theory]
    [MemberData(nameof(WritableMembers))]
    public void 自持可写属性有公开setter(string propertyName)
    {
        var property = typeof(Person).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(property);
        Assert.True(
            property.SetMethod is { IsPublic: true },
            $"{propertyName} 是 Person 自持的可写属性，应有公开 setter。");
    }

    [Fact]
    public void 年龄由出生年月派生且没有裸年龄字段()
    {
        var person = FamilyFixtures.NewPerson(501, "测试", Gender.Female, birthYear: 10, birthMonth: 5, generation: 0);

        Assert.Equal(0, person.AgeAt(new GameDate(10, 5)));
        Assert.Equal(0, person.AgeAt(new GameDate(11, 4)));
        Assert.Equal(1, person.AgeAt(new GameDate(11, 5)));
        Assert.Equal(2, person.AgeAt(new GameDate(12, 5)));
        Assert.Equal(new GameDate(10, 5), person.BirthDate);

        Assert.Null(typeof(Person).GetProperty("Age", BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void 父母或配偶指向自身时被拒()
    {
        var self = FamilyFixtures.Id(600);

        Assert.Throws<ArgumentException>(() => new Person(
            self, "自指", Gender.Male, new GameDate(1, 1), FamilyFixtures.Talents(50), 60, 0, fatherId: self));
        Assert.Throws<ArgumentException>(() => new Person(
            self, "自指", Gender.Male, new GameDate(1, 1), FamilyFixtures.Talents(50), 60, 0, motherId: self));

        // SpouseId 无公开写入通道，唯一入口是 Family；自婚在聚合入口被拒。
        var family = FamilyFixtures.Couple().Family;
        Assert.Throws<ArgumentException>(() => family.Marry(family.HeadId!.Value, family.HeadId!.Value));
    }

    [Fact]
    public void 天命寿数只约束下界没有上界()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FamilyFixtures.NewPerson(
            601, "测试", Gender.Male, 1, 1, generation: 0, lifespan: -1));

        // 「无上界」是有意留白（阶段⑧的天命寿数分布），001 MUST NOT 断言上限。
        var longLived = FamilyFixtures.NewPerson(602, "寿星", Gender.Male, 1, 1, generation: 0, lifespan: 9999);
        Assert.Equal(9999, longLived.Lifespan);
    }

    [Fact]
    public void 空历史的当前功名为白身()
    {
        var person = FamilyFixtures.NewPerson(700, "初生", Gender.Male, 1, 1, generation: 0);

        Assert.Empty(person.DegreeHistory);
        Assert.Equal(DegreeLevel.BaiShen, person.CurrentDegree);
        Assert.Null(person.CurrentPlacement);
    }

    [Fact]
    public void 当前功名严格由末条派生且降级随之下降()
    {
        var person = FamilyFixtures.NewPerson(701, "考生", Gender.Male, 1, 1, generation: 0);

        person.AppendDegree(new DegreeRecord(
            DegreeLevel.JuRen, null, new GameDate(10, 1), DegreeChangeCause.ExamPass));
        Assert.Equal(DegreeLevel.JuRen, person.CurrentDegree);
        Assert.Null(person.CurrentPlacement);

        person.AppendDegree(new DegreeRecord(
            DegreeLevel.GongShi, null, new GameDate(13, 1), DegreeChangeCause.ExamPass));
        Assert.Equal(DegreeLevel.GongShi, person.CurrentDegree);

        person.AppendDegree(new DegreeRecord(
            DegreeLevel.JinShi,
            ImperialPlacement.ZhuangYuan,
            new GameDate(16, 1),
            DegreeChangeCause.ExamPass,
            ImperialClass.FirstClass));
        Assert.Equal(DegreeLevel.JinShi, person.CurrentDegree);
        Assert.Equal(ImperialPlacement.ZhuangYuan, person.CurrentPlacement.GetValueOrDefault());

        // §7.4 连坐：进士 → 贡士，历史追加一条降级记录（research R-14）。
        person.AppendDegree(new DegreeRecord(
            DegreeLevel.GongShi, null, new GameDate(17, 1), DegreeChangeCause.PunishmentDemotion));

        Assert.Equal(DegreeLevel.GongShi, person.CurrentDegree);
        Assert.Null(person.CurrentPlacement);
        Assert.Equal(4, person.DegreeHistory.Count);
        Assert.Equal(DegreeChangeCause.PunishmentDemotion, person.DegreeHistory[^1].Cause);
    }

    [Fact]
    public void 历史非降序追加且既有记录不可改写()
    {
        var person = FamilyFixtures.NewPerson(702, "考生", Gender.Male, 1, 1, generation: 0);
        var first = new DegreeRecord(DegreeLevel.JuRen, null, new GameDate(10, 1), DegreeChangeCause.ExamPass);

        person.AppendDegree(first);
        person.AppendDegree(new DegreeRecord(
            DegreeLevel.GongShi, null, new GameDate(10, 1), DegreeChangeCause.ExamPass));

        Assert.Equal(first, person.DegreeHistory[0]);

        // 早于末条 → 拒绝（非降序）。
        Assert.Throws<ArgumentException>(() => person.AppendDegree(new DegreeRecord(
            DegreeLevel.BaiShen, null, new GameDate(1, 1), DegreeChangeCause.Initial)));

        // 对外暴露的是只读视图，不能借 IList<T> 改写历史。
        var asList = Assert.IsAssignableFrom<IList<DegreeRecord>>(person.DegreeHistory);
        Assert.Throws<NotSupportedException>(() => asList.Add(first));
        Assert.Throws<NotSupportedException>(() => asList.Clear());
        Assert.Equal(2, person.DegreeHistory.Count);
    }

    [Fact]
    public void 空历史与追加白身记录是两种不同状态()
    {
        var blank = FamilyFixtures.NewPerson(703, "初生", Gender.Male, 1, 1, generation: 0);

        var demoted = FamilyFixtures.NewPerson(704, "被降级者", Gender.Male, 1, 1, generation: 0);
        demoted.AppendDegree(new DegreeRecord(
            DegreeLevel.JuRen, null, new GameDate(10, 1), DegreeChangeCause.ExamPass));
        demoted.AppendDegree(new DegreeRecord(
            DegreeLevel.BaiShen, null, new GameDate(11, 1), DegreeChangeCause.PunishmentDemotion));

        // 两者的 CurrentDegree 相同（都是白身），但历史不同——must 可区分。
        Assert.Equal(DegreeLevel.BaiShen, blank.CurrentDegree);
        Assert.Equal(DegreeLevel.BaiShen, demoted.CurrentDegree);
        Assert.Empty(blank.DegreeHistory);
        Assert.Equal(2, demoted.DegreeHistory.Count);
        Assert.NotEqual(blank.DegreeHistory.Count, demoted.DegreeHistory.Count);
        Assert.Equal(DegreeLevel.JuRen, demoted.DegreeHistory[0].Level);
    }

    [Fact]
    public void 不存在独立的当前功名字段()
    {
        var properties = typeof(Person).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var fields = typeof(Person).GetFields(BindingFlags.Public | BindingFlags.Instance);

        Assert.Equal(
            ["CurrentDegree"],
            properties.Where(p => p.PropertyType == typeof(DegreeLevel)).Select(p => p.Name).ToArray());
        Assert.Equal(
            ["CurrentPlacement"],
            properties.Where(p => p.PropertyType == typeof(ImperialPlacement?)).Select(p => p.Name).ToArray());
        Assert.DoesNotContain(
            fields, f => f.FieldType == typeof(DegreeLevel) || f.FieldType == typeof(ImperialPlacement?));

        // 历史本身也只有一处。
        Assert.Equal(
            ["DegreeHistory"],
            properties.Where(p => p.PropertyType == typeof(IReadOnlyList<DegreeRecord>)).Select(p => p.Name).ToArray());
    }

    [Fact]
    public void 不含越界字段()
    {
        var properties = typeof(Person).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        // 分别由 Family 派生、家族级、存档级承载（data-model §2.1「明确不含」）。
        Assert.DoesNotContain("ChildIds", properties);
        Assert.DoesNotContain("IsShiIdentity", properties);
        Assert.DoesNotContain("Origin", properties);

        // 003 新增的三态与甲第都是**派生量**，MUST NOT 落成 Person 的字段/属性。
        Assert.DoesNotContain("SalaryMode", properties);
        Assert.DoesNotContain("ImperialClass", properties);
        Assert.DoesNotContain("AwaitingPostRemainingMonths", properties);
    }
}
