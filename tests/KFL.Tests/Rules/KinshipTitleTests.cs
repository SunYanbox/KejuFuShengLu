using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Career;
using KFL.Rules.Config;
using KFL.Rules.FamilyLifecycle;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// 家谱长幼称谓与「家主一脉嫡系男性」（2026-10-19 第九轮玩家反馈 2 / 4）。
/// </summary>
/// <remarks>
/// 口径：序号一律按**混合性别、含本人在内**的出生全序排定（本人占位保留），
/// 故「二哥」的兄弟姐妹是「大哥、三姐、四妹、五弟」而不是「大哥、二姐…」；
/// 嫡长子之外的子女一律「序号 + 儿子/女儿」，序号不断档。
/// </remarks>
public class KinshipTitleTests
{
    private static readonly string[] ExpectedSiblingTitles = ["大哥 陈大", "二姐 陈二", "四妹 陈四", "五弟 陈五"];
    private static readonly string[] ExpectedChildTitles = ["嫡长子", "二女儿", "三儿子", "四女儿", "五儿子"];

    [Fact]
    public void 兄弟姐妹按含本人的出生全序取序号且剔除本人()
    {
        var family = SiblingFamily();
        var third = family.Members.Single(member => member.Name == "陈三");

        var titles = GenealogyQueries.SiblingTitles(family, third)
            .Select(entry => $"{entry.Title} {entry.Person.Name}")
            .ToArray();

        // 全序（含本人）：陈大 1 / 陈二 2 / 陈三 3（本人）/ 陈四 4 / 陈五 5。
        Assert.Equal(ExpectedSiblingTitles, titles);
    }

    [Fact]
    public void 子女除嫡长子外按序号加儿子或女儿且序号不断档()
    {
        var family = SiblingFamily();
        var father = family.Members.Single(member => member.Name == "陈父");

        var titles = GenealogyQueries.ChildTitles(family, father)
            .Select(entry => entry.Title)
            .ToArray();

        Assert.Equal(ExpectedChildTitles, titles);
    }

    [Fact]
    public void 序号前缀覆盖大二十十一二十一()
    {
        Assert.Equal("大", GenealogyQueries.OrdinalOf(1));
        Assert.Equal("二", GenealogyQueries.OrdinalOf(2));
        Assert.Equal("九", GenealogyQueries.OrdinalOf(9));
        Assert.Equal("十", GenealogyQueries.OrdinalOf(10));
        Assert.Equal("十一", GenealogyQueries.OrdinalOf(11));
        Assert.Equal("二十一", GenealogyQueries.OrdinalOf(21));
        Assert.Throws<ArgumentOutOfRangeException>(() => GenealogyQueries.OrdinalOf(0));
    }

    [Fact]
    public void 家主一脉嫡系男性含嫡出子链上的男性不含女儿与庶出()
    {
        var family = SiblingFamily();
        var father = family.Members.Single(member => member.Name == "陈父");

        // 庶出：无生母参照的儿子，不算嫡系。
        var illegitimate = FamilyFixtures.NewPerson(
            99, "陈庶", Gender.Male, RulesHarness.Date.Year - 19, 1, generation: 1, fatherId: father.Id);
        family.AddChild(illegitimate);

        var ids = GenealogyQueries.HeadDirectLineMaleIds(family);

        Assert.Contains(father.Id, ids);
        Assert.Contains(family.Members.Single(member => member.Name == "陈大").Id, ids);
        Assert.Contains(family.Members.Single(member => member.Name == "陈三").Id, ids);
        Assert.Contains(family.Members.Single(member => member.Name == "陈五").Id, ids);
        Assert.DoesNotContain(family.Members.Single(member => member.Name == "陈二").Id, ids);
        Assert.DoesNotContain(family.Members.Single(member => member.Name == "陈四").Id, ids);
        Assert.DoesNotContain(illegitimate.Id, ids);
    }

    /// <summary>父（家主、男）＋ 妻 ＋ 五个按出生先后排列的子女：大男、二女、三男、四女、五男。</summary>
    private static Family SiblingFamily()
    {
        var father = FamilyFixtures.NewPerson(1, "陈父", Gender.Male, RulesHarness.Date.Year - 50, 1, generation: 0);
        var family = RulesHarness.FamilyOf(father);
        var mother = FamilyFixtures.NewPerson(2, "王母", Gender.Female, RulesHarness.Date.Year - 48, 1, generation: 0);
        family.AddOutsider(mother);
        family.Marry(father.Id, mother.Id);

        (int Index, string Name, Gender Gender, int BirthOffset)[] children =
        [
            (10, "陈大", Gender.Male, 30),
            (11, "陈二", Gender.Female, 28),
            (12, "陈三", Gender.Male, 25),
            (13, "陈四", Gender.Female, 22),
            (14, "陈五", Gender.Male, 20),
        ];

        foreach (var child in children)
        {
            var person = FamilyFixtures.NewPerson(
                child.Index,
                child.Name,
                child.Gender,
                RulesHarness.Date.Year - child.BirthOffset,
                1,
                generation: 1,
                fatherId: father.Id,
                motherId: mother.Id);
            family.AddChild(person);
        }

        family.SetHead(father.Id);
        return family;
    }
}

/// <summary>
/// 升官概率预测（2026-10-19 第九轮玩家反馈 6）：复刻考课判定的输入，但不掷骰、不写状态。
/// </summary>
public class PromotionForecastTests
{
    [Fact]
    public void 无官职时返回空()
    {
        var person = RulesHarness.Member(1, Gender.Male, 40);

        Assert.Null(PromotionForecast.Of(person));
    }

    [Fact]
    public void 白身官员的职称上限为最低品且到顶者不再掷骰()
    {
        var capped = PromotionForecast.Of(RulesHarness.Official(1, level: SalaryTable.LowestLevel));

        Assert.NotNull(capped);
        Assert.Equal(SalaryTable.LowestLevel, capped.CapLevel);
        Assert.True(capped.AtLevelCap);
        Assert.Null(capped.NextLevel);
        Assert.False(capped.CanPromoteThisMonth);

        // 白身的上限就是最低品：连 L17 也不可再升（对应 TryPromote 的「level - 1 < cap」）。
        Assert.True(PromotionForecast.Of(RulesHarness.Official(2, level: SalaryTable.LowestLevel - 1))!.AtLevelCap);
    }

    [Fact]
    public void 未到顶且满考课周期时可晋升且概率随政绩线性增长()
    {
        // 举人上限为第 16 级，故 L17 尚有晋升余地。
        var juRen = RulesHarness.Official(1, level: SalaryTable.LowestLevel - 1, monthsInOffice: OfficialCareerPolicy.AppraisalPeriodMonths);
        juRen.AppendDegree(new DegreeRecord(DegreeLevel.JuRen, null, RulesHarness.Date, DegreeChangeCause.Initial));

        var forecast = PromotionForecast.Of(juRen);

        Assert.NotNull(forecast);
        Assert.Equal(SalaryTable.LowestLevel - 1, forecast.Rank.Level);
        Assert.Equal(16, forecast.CapLevel);
        Assert.Equal(16, forecast.NextLevel!.Value.Level);
        Assert.False(forecast.AtLevelCap);
        Assert.True(forecast.CanPromoteThisMonth);
        Assert.Equal(0, forecast.MonthsUntilAppraisal);
        Assert.Equal(OfficialCareerPolicy.PromotionChance(0), forecast.Chance);

        // 概率 = 基础率 + 政绩 × 每点加成，且封顶。
        Assert.Equal(
            OfficialCareerPolicy.PromotionBaseChance,
            ChanceOf(2, merit: 0));
        Assert.Equal(
            OfficialCareerPolicy.PromotionBaseChance + (100 * OfficialCareerPolicy.PromotionChancePerMerit),
            ChanceOf(3, merit: 100));
        Assert.Equal(
            OfficialCareerPolicy.PromotionChanceCap,
            ChanceOf(4, merit: 200));
    }

    /// <summary>取「举人 + L17 + 满考课周期」官员的预测概率。</summary>
    private static decimal ChanceOf(int index, int merit)
    {
        var person = RulesHarness.Official(
            index,
            level: SalaryTable.LowestLevel - 1,
            monthsInOffice: OfficialCareerPolicy.AppraisalPeriodMonths,
            merit: merit);
        person.AppendDegree(new DegreeRecord(DegreeLevel.JuRen, null, RulesHarness.Date, DegreeChangeCause.Initial));
        return PromotionForecast.Of(person)!.Chance;
    }

    [Fact]
    public void 未满考课周期时给出剩余月数且本月不可晋升()
    {
        var person = RulesHarness.Official(1, level: SalaryTable.LowestLevel - 1, monthsInOffice: 10);
        person.AppendDegree(new DegreeRecord(DegreeLevel.JuRen, null, RulesHarness.Date, DegreeChangeCause.Initial));

        var forecast = PromotionForecast.Of(person);

        Assert.NotNull(forecast);
        Assert.Equal(OfficialCareerPolicy.AppraisalPeriodMonths - 10, forecast.MonthsUntilAppraisal);
        Assert.False(forecast.CanPromoteThisMonth);
    }

    [Fact]
    public void 禁升与服刑都使本月不可晋升()
    {
        var banned = RulesHarness.Official(1, level: SalaryTable.LowestLevel - 1, monthsInOffice: OfficialCareerPolicy.AppraisalPeriodMonths);
        banned.Status |= StatusFlag.PromotionBanned;

        var serving = RulesHarness.Official(2, level: SalaryTable.LowestLevel - 1, monthsInOffice: OfficialCareerPolicy.AppraisalPeriodMonths);
        serving.Status |= StatusFlag.ServingSentence;

        Assert.False(PromotionForecast.Of(banned)!.CanPromoteThisMonth);
        Assert.True(PromotionForecast.Of(banned)!.PromotionBanned);
        Assert.False(PromotionForecast.Of(serving)!.CanPromoteThisMonth);
        Assert.True(PromotionForecast.Of(serving)!.ServingSentence);
    }

    [Fact]
    public void 预测不修改任何成员状态()
    {
        var person = RulesHarness.Official(
            1, level: SalaryTable.LowestLevel - 1, monthsInOffice: OfficialCareerPolicy.AppraisalPeriodMonths, merit: 7);
        var beforeLevel = person.Rank!.Value.Level;
        var beforeMonths = person.MonthsInOffice;
        var beforeMerit = person.Merit;
        var beforeStatus = person.Status;

        _ = PromotionForecast.Of(person);

        Assert.Equal(beforeLevel, person.Rank!.Value.Level);
        Assert.Equal(beforeMonths, person.MonthsInOffice);
        Assert.Equal(beforeMerit, person.Merit);
        Assert.Equal(beforeStatus, person.Status);
    }
}
