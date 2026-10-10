using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Services;
using KFL.Rules.Career;
using KFL.Rules.Config;
using KFL.Rules.Settlement;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// quickstart S4、SC-005、SC-006：政绩 +1 与封顶、36 月考课（35 月不判）、概率公式、
/// 成功 −1 / 失败不变 / L1 维持、禁升跳过（不消耗随机）与同种子轨迹一致。
/// </summary>
/// <remarks>期望值一律经 <c>KFL.Rules/Config</c> 的成员取得（SC-009）。</remarks>
public class CareerAdvanceTests
{
    private static readonly GameDate Date = RulesHarness.Date;

    [Fact]
    public void 在任者政绩每月加一()
    {
        var person = RulesHarness.Official(1, level: 15, merit: 7);
        var family = RulesHarness.FamilyOf(person);

        var result = OfficialCareerAdvance.Run(family, Date, new CountingRandomService(0.0d));

        Assert.Equal(8, person.Merit);
        Assert.Equal(GameConfig.Career.MeritPerMonth, result.MeritGains[person.Id]);
    }

    [Fact]
    public void 政绩封顶于上限()
    {
        var person = RulesHarness.Official(1, level: 15, merit: GameConfig.Career.MeritMaximum);
        var family = RulesHarness.FamilyOf(person);

        var result = OfficialCareerAdvance.Run(family, Date, new CountingRandomService(0.0d));

        Assert.Equal(GameConfig.Career.MeritMaximum, person.Merit);
        Assert.DoesNotContain(person.Id, result.MeritGains.Keys);
    }

    [Fact]
    public void 非官员待阙者与已致仕者都不增长政绩()
    {
        var none = RulesHarness.Member(1, Gender.Male, 40);
        var awaiting = RulesHarness.Awaiting(RulesHarness.Member(2, Gender.Male, 40), remainingMonths: 12);
        var retired = RulesHarness.Official(3, level: 15, merit: 9);
        retired.Status |= StatusFlag.Retired;

        var family = RulesHarness.FamilyOf(none, awaiting, retired);

        OfficialCareerAdvance.Run(family, Date, new CountingRandomService(0.0d));

        Assert.Equal(0, none.Merit);
        Assert.Equal(0, awaiting.Merit);
        Assert.Equal(9, retired.Merit);
    }

    [Fact]
    public void 第三十五个在职月不判定而第三十六个在职月判定并重置计时()
    {
        var person = RulesHarness.Official(
            1, level: 15, monthsInOffice: GameConfig.Career.AppraisalPeriodMonths - 2, merit: 0);
        var family = RulesHarness.FamilyOf(person);

        // 第 35 个在职月：随机取值 0 本可成功，但 MUST NOT 判定（也不消耗随机）。
        var noAppraisal = new CountingRandomService(0.0d);
        var before = OfficialCareerAdvance.Run(family, Date, noAppraisal);

        Assert.Equal(GameConfig.Career.AppraisalPeriodMonths - 1, person.MonthsInOffice);
        Assert.Equal(15, person.Rank!.Value.Level);
        Assert.Empty(before.Promotions);
        Assert.Equal(0, noAppraisal.Doubles);

        // 第 36 个在职月：判定一次、成功 −1、计时重置为 0。
        var appraisal = new CountingRandomService(0.0d);
        var after = OfficialCareerAdvance.Run(family, Date, appraisal);

        Assert.Equal(14, person.Rank!.Value.Level);
        Assert.Equal(0, person.MonthsInOffice);
        Assert.Equal(1, appraisal.Doubles);
        Assert.Single(after.Promotions);
    }

    [Fact]
    public void 考课概率逐点等于配置公式且封顶分支可达()
    {
        foreach (var merit in new[] { 0, 1, 10, 100, 200 })
        {
            Assert.Equal(
                Math.Min(
                    GameConfig.Career.PromotionBaseChance + (merit * GameConfig.Career.PromotionChancePerMerit),
                    GameConfig.Career.PromotionChanceCap),
                GameConfig.Career.PromotionChance(merit));
        }

        // Merit = 100 ⇒ 25% + 100 × 0.3% = 55%（不封顶）。
        Assert.Equal(
            GameConfig.Career.PromotionBaseChance + (100 * GameConfig.Career.PromotionChancePerMerit),
            GameConfig.Career.PromotionChance(100));

        // 封顶分支：超出政绩区间的入参（200）MUST 返回封顶值（MUST NOT 以「不可达」为由省略）。
        Assert.Equal(GameConfig.Career.PromotionChanceCap, GameConfig.Career.PromotionChance(200));
    }

    [Fact]
    public void 判定成功减一级失败不变而最高品时成功仍维持()
    {
        // 概率截面：Merit = 100 ⇒ 55%。0.54 成功、0.56 失败。
        Assert.Equal(14, Appraise(level: 15, merit: 100, roll: 0.54d).Level);
        Assert.Equal(15, Appraise(level: 15, merit: 100, roll: 0.56d).Level);

        // 最高品（L1）：成功也维持 L1，MUST NOT 越界，也不登记晋升。
        var top = Appraise(level: SalaryTable.HighestLevel, merit: 100, roll: 0.0d);

        Assert.Equal(SalaryTable.HighestLevel, top.Level);
        Assert.Empty(top.Promotions);
    }

    [Fact]
    public void 本月政绩加一计入本月到期的考课概率()
    {
        // Merit = 54 时概率 41.2%，+1 后 41.5%：0.414 落在两者之间 ⇒ 只有把本月 +1 计入才会成功。
        var result = Appraise(level: 15, merit: 54, roll: 0.414d);

        Assert.Equal(55, result.Merit);
        Assert.Equal(14, result.Level);
    }

    [Fact]
    public void 禁升期到期跳过判定且不消耗随机也不补判()
    {
        var person = RulesHarness.PromotionBannedOfficial(
            1,
            remainingMonths: 12,
            level: 15,
            monthsInOffice: GameConfig.Career.AppraisalPeriodMonths - 1,
            merit: 0);

        var family = RulesHarness.FamilyOf(person);
        var random = new CountingRandomService(0.0d);
        var result = OfficialCareerAdvance.Run(family, Date, random);

        Assert.Equal(15, person.Rank!.Value.Level);
        Assert.Equal(0, person.MonthsInOffice);
        Assert.Equal(0, random.Doubles);
        Assert.Contains(person.Id, result.AppraisalSkipped);
        Assert.Empty(result.AppraisalPaused);
        Assert.Empty(result.Promotions);

        // MUST NOT 补判：此后 35 个月不再消费随机、不升迁。
        for (var index = 0; index < GameConfig.Career.AppraisalPeriodMonths - 1; index++)
        {
            OfficialCareerAdvance.Run(family, Date, random);
        }

        Assert.Equal(15, person.Rank!.Value.Level);
        Assert.Equal(0, random.Doubles);

        // 本特性 MUST NOT 递减或清除禁升计时（逻辑轨 ⑥）。
        Assert.Equal(12, person.Timers.PromotionBanRemainingMonths.GetValueOrDefault(-1));
    }

    [Fact]
    public void 同种子两次推进的官阶待阙与政绩轨迹完全相同()
    {
        Assert.Equal(Trajectory(seed: 20261008), Trajectory(seed: 20261008));
        Assert.NotEqual(Trajectory(seed: 20261008), Trajectory(seed: 20261009));
    }

    /// <summary>一次判定的结果（官阶、政绩、晋升记录）。</summary>
    private sealed record AppraisalOutcome(int Level, int Merit, IReadOnlyList<CareerPromotion> Promotions);

    /// <summary>造一名在任官员并让其在**本月**正好到期判定。</summary>
    private static AppraisalOutcome Appraise(int level, int merit, double roll)
    {
        var person = RulesHarness.Official(
            1, level: level, monthsInOffice: GameConfig.Career.AppraisalPeriodMonths - 1, merit: merit);

        var result = OfficialCareerAdvance.Run(
            RulesHarness.FamilyOf(person), Date, new CountingRandomService(roll));

        return new AppraisalOutcome(person.Rank!.Value.Level, person.Merit, result.Promotions);
    }

    /// <summary>把连续 40 个月的官阶 / 政绩 / 在职月数 / 待阙剩余月数摊平成可比较的轨迹串。</summary>
    private static string Trajectory(int seed)
    {
        var official = RulesHarness.Official(
            1, level: 15, monthsInOffice: GameConfig.Career.AppraisalPeriodMonths - 6, merit: 0);
        var junior = RulesHarness.Official(2, level: 5, merit: 50);
        var graduate = RulesHarness.Member(3, Gender.Male, 40);

        graduate.AppendDegree(new DegreeRecord(
            DegreeLevel.JinShi,
            ImperialPlacement.ZhuangYuan,
            Date,
            DegreeChangeCause.ExamPass,
            ImperialClass.FirstClass));

        RulesHarness.Awaiting(graduate, remainingMonths: GameConfig.Career.AwaitingPostMinMonths);

        var family = RulesHarness.FamilyOf(official, junior, graduate);
        var random = new SeededRandomService(seed);
        var lines = new List<string>();

        for (var month = 0; month < 40; month++)
        {
            OfficialCareerAdvance.Run(family, Date, random);
            lines.Add(Describe(official));
            lines.Add(Describe(junior));
            lines.Add(Describe(graduate));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string Describe(Person person) => string.Join(
        "|",
        person.Rank?.Level ?? 0,
        person.Merit,
        person.MonthsInOffice,
        person.Timers.AwaitingPostRemainingMonths ?? -1);

    /// <summary>包一层计数：统计 <see cref="IRandomService.NextDouble"/> 的调用次数，取值由构造给出。</summary>
    private sealed class CountingRandomService : IRandomService
    {
        private readonly double _value;
        private readonly FixedRandomService _next = new();

        /// <summary>构造：<see cref="NextDouble"/> 恒返回给定取值。</summary>
        /// <param name="value">返回值。</param>
        public CountingRandomService(double value) => _value = value;

        /// <summary>至今被请求的 <see cref="NextDouble"/> 次数。</summary>
        public int Doubles { get; private set; }

        /// <inheritdoc />
        public double NextDouble()
        {
            Doubles++;
            return _value;
        }

        /// <inheritdoc />
        public int Next(int minInclusive, int maxExclusive) => _next.Next(minInclusive, maxExclusive);

        /// <inheritdoc />
        public void NextBytes(Span<byte> destination) => _next.NextBytes(destination);
    }
}
