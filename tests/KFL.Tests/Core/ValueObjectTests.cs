using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using Xunit;

namespace KFL.Tests.Core;

/// <summary>
/// T023：值类型与枚举边界。
/// </summary>
public class ValueObjectTests
{
    [Fact]
    public void GameDate接受起始年月与年末月()
    {
        var start = new GameDate(1, 1);

        Assert.Equal(1, start.Year);
        Assert.Equal(1, start.Month);
        Assert.Equal(12, new GameDate(1, 12).Month);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 6)]
    [InlineData(int.MinValue, 1)]
    public void GameDate拒绝非法年份(int year, int month) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameDate(year, month));

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 13)]
    [InlineData(1, -1)]
    [InlineData(1, int.MaxValue)]
    public void GameDate拒绝非法月份(int year, int month) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameDate(year, month));

    [Fact]
    public void GameDate比较与月差()
    {
        var earlier = new GameDate(1, 1);
        var later = new GameDate(2, 1);

        Assert.True(earlier < later);
        Assert.True(later > earlier);
        Assert.True(earlier <= new GameDate(1, 1));
        Assert.True(earlier >= new GameDate(1, 1));
        Assert.Equal(0, earlier.CompareTo(new GameDate(1, 1)));

        Assert.Equal(12, earlier.ElapsedMonths(later));
        Assert.Equal(-12, later.ElapsedMonths(earlier));
        Assert.Equal(13, new GameDate(1, 1).ElapsedMonths(new GameDate(2, 2)));
        Assert.Equal(1, new GameDate(1, 12).ElapsedMonths(new GameDate(2, 1)));
    }

    [Fact]
    public void GameDate生日当月即计入周岁()
    {
        var born = new GameDate(1, 1);

        // 生于 1 年 1 月者，在其 12 岁生日当月（13 年 1 月）已满 12 周岁。
        Assert.Equal(12, born.AgeInYearsAt(new GameDate(13, 1)));
        Assert.Equal(11, born.AgeInYearsAt(new GameDate(12, 12)));
        Assert.Equal(0, born.AgeInYearsAt(new GameDate(1, 1)));

        var bornInMay = new GameDate(10, 5);
        Assert.Equal(11, bornInMay.AgeInYearsAt(new GameDate(22, 4)));
        Assert.Equal(12, bornInMay.AgeInYearsAt(new GameDate(22, 5)));
    }

    [Fact]
    public void GameDate拒绝查询出生之前的年龄()
    {
        var born = new GameDate(5, 6);

        Assert.Throws<ArgumentOutOfRangeException>(() => born.AgeInYearsAt(new GameDate(5, 5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => born.AgeInYearsAt(new GameDate(4, 1)));
    }

    [Fact]
    public void PersonId拒绝空标识()
    {
        Assert.Throws<ArgumentException>(() => new PersonId(Guid.Empty));

        var id = new PersonId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), id.Value);
    }

    [Fact]
    public void TalentSet接受边界值()
    {
        var zero = new TalentSet(0, 0, 0, 0);
        Assert.Equal(0, zero.Agriculture);
        Assert.Equal(0, zero.Commerce);
        Assert.Equal(0, zero.Officialdom);
        Assert.Equal(0, zero.Craft);

        var full = new TalentSet(100, 100, 100, 100);
        Assert.Equal(100, full.Agriculture);
        Assert.Equal(100, full.Commerce);
        Assert.Equal(100, full.Officialdom);
        Assert.Equal(100, full.Craft);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(0, -1, 0, 0)]
    [InlineData(0, 0, -1, 0)]
    [InlineData(0, 0, 0, -1)]
    [InlineData(101, 0, 0, 0)]
    [InlineData(0, 101, 0, 0)]
    [InlineData(0, 0, 101, 0)]
    [InlineData(0, 0, 0, 101)]
    public void TalentSet拒绝越界(int agriculture, int commerce, int officialdom, int craft) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TalentSet(agriculture, commerce, officialdom, craft));

    private static readonly GameDate AnyDate = new(1, 1);

    [Theory]
    [InlineData(DegreeLevel.BaiShen)]
    [InlineData(DegreeLevel.JuRen)]
    [InlineData(DegreeLevel.GongShi)]
    public void DegreeRecord允许非进士名次为空(DegreeLevel level)
    {
        var record = new DegreeRecord(level, null, AnyDate, DegreeChangeCause.ExamPass);

        Assert.Equal(level, record.Level);
        Assert.Null(record.Placement);
    }

    [Fact]
    public void DegreeRecord允许进士带一甲名次()
    {
        var record = new DegreeRecord(
            DegreeLevel.JinShi, ImperialPlacement.ZhuangYuan, AnyDate, DegreeChangeCause.ExamPass);

        Assert.Equal(DegreeLevel.JinShi, record.Level);
        Assert.Equal(ImperialPlacement.ZhuangYuan, record.Placement.GetValueOrDefault());
    }

    [Theory]
    [InlineData(DegreeLevel.BaiShen)]
    [InlineData(DegreeLevel.JuRen)]
    [InlineData(DegreeLevel.GongShi)]
    public void DegreeRecord拒绝非进士带一甲名次(DegreeLevel level) =>
        Assert.Throws<ArgumentException>(
            () => new DegreeRecord(level, ImperialPlacement.TanHua, AnyDate, DegreeChangeCause.Initial));

    [Fact]
    public void DegreeRecord四个成员均为必需()
    {
        var constructor = Assert.Single(typeof(DegreeRecord).GetConstructors());
        var parameters = constructor.GetParameters();

        Assert.Equal(4, parameters.Length);
        Assert.Contains(parameters, p => p.Name == "level");
        Assert.Contains(parameters, p => p.Name == "placement");
        Assert.Contains(parameters, p => p.Name == "changedAt");
        Assert.Contains(parameters, p => p.Name == "cause");
        Assert.DoesNotContain(parameters, p => p.IsOptional);
    }

    [Fact]
    public void DegreeRecord年月与原因被如实保存()
    {
        var changedAt = new GameDate(18, 7);
        var record = new DegreeRecord(DegreeLevel.GongShi, null, changedAt, DegreeChangeCause.PunishmentDemotion);

        Assert.Equal(changedAt, record.ChangedAt);
        Assert.Equal(DegreeChangeCause.PunishmentDemotion, record.Cause);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(18)]
    public void OfficialRank接受两端(int level)
    {
        var rank = new OfficialRank(level);

        Assert.Equal(level, rank.Level);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(19)]
    [InlineData(-1)]
    public void OfficialRank拒绝越界(int level) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new OfficialRank(level));

    [Fact]
    public void StatusTimers拒绝负值()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StatusTimers(sentenceRemainingMonths: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StatusTimers(examBanRemainingMonths: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StatusTimers(promotionBanRemainingMonths: -1));
    }

    [Fact]
    public void StatusTimers接受零与空()
    {
        var timers = new StatusTimers(sentenceRemainingMonths: 0, examBanRemainingMonths: null);

        Assert.Equal(0, timers.SentenceRemainingMonths.GetValueOrDefault(-1));
        Assert.Null(timers.ExamBanRemainingMonths);
        Assert.Null(timers.PromotionBanRemainingMonths);
    }

    [Fact]
    public void Difficulty次序为简单到地狱()
    {
        Assert.True(Difficulty.Easy < Difficulty.Normal);
        Assert.True(Difficulty.Normal < Difficulty.Hard);
        Assert.True(Difficulty.Hard < Difficulty.Hell);
    }

    [Theory]
    [InlineData((int)Origin.Farmer)]
    [InlineData((int)Origin.Artisan)]
    [InlineData((int)Origin.Merchant)]
    [InlineData((int)Origin.Scholar)]
    public void Origin四出身齐备(int value) => Assert.True(Enum.IsDefined((Origin)value));

    [Theory]
    [InlineData((int)Occupation.None)]
    [InlineData((int)Occupation.Studying)]
    [InlineData((int)Occupation.Farming)]
    [InlineData((int)Occupation.Crafting)]
    [InlineData((int)Occupation.Trading)]
    public void Occupation五取值齐备(int value) => Assert.True(Enum.IsDefined((Occupation)value));

    [Fact]
    public void Gender两取值齐备()
    {
        Assert.True(Enum.IsDefined(Gender.Male));
        Assert.True(Enum.IsDefined(Gender.Female));
    }
}
