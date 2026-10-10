using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Career;
using KFL.Rules.Config;
using KFL.Rules.Settlement;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// quickstart S5、SC-004 + FR-024：三态判定的四条优先级、四态月俸差异，以及俸禄锚点
/// <b>72 / 420 / 5100</b> 在**在任**与**致仕**两态下的复核（半俸不累乘）。
/// </summary>
/// <remarks>
/// 锚点字面量即被验证对象（契约八 §3 条款 2），故带行级豁免注释；其余期望值经
/// <c>KFL.Rules/Config</c> 的成员取得。
/// </remarks>
public class SalaryModeTests
{
    [Fact]
    public void 三态判定的四条优先级()
    {
        var none = RulesHarness.Member(1, Gender.Male, 40);
        Assert.Equal(SalaryMode.None, SalaryModePolicy.Of(none));

        var awaiting = RulesHarness.Awaiting(RulesHarness.Member(2, Gender.Male, 40), remainingMonths: 12);
        Assert.Equal(SalaryMode.AwaitingPost, SalaryModePolicy.Of(awaiting));

        var active = RulesHarness.Official(3, level: 15);
        Assert.Equal(SalaryMode.Active, SalaryModePolicy.Of(active));

        var retired = RulesHarness.Official(4, level: 15);
        retired.Status |= StatusFlag.Retired;
        Assert.Equal(SalaryMode.Retired, SalaryModePolicy.Of(retired));

        // Retired 优先于 Active：有官阶 ∧ 已致仕 ⇒ Retired（而非 Active）。
        Assert.NotNull(retired.Rank);
        Assert.NotEqual(SalaryMode.Active, SalaryModePolicy.Of(retired));

        // 判定是**只读**纯函数：判定前后状态逐项不变。
        Assert.Null(none.Rank);
        Assert.Equal(0, none.Merit);
        Assert.Equal(StatusFlag.None, none.Status);
        Assert.True(awaiting.Status.HasFlag(StatusFlag.AwaitingPost));
        Assert.Equal(12, awaiting.Timers.AwaitingPostRemainingMonths.GetValueOrDefault(-1));
    }

    [Fact]
    public void 四态的月俸条目差异且待阙与无官不落零金额条目()
    {
        var active = RulesHarness.Official(11, level: 15);
        var retired = RulesHarness.Official(12, level: 15);
        retired.Status |= StatusFlag.Retired;
        var awaiting = RulesHarness.Awaiting(RulesHarness.Official(13, level: 15), remainingMonths: 12);
        awaiting.Rank = null;                                 // 待阙者无官阶
        var none = RulesHarness.Member(14, Gender.Male, 40);

        var computation = RulesHarness.Income(
            RulesHarness.FamilyOf(active, retired, awaiting, none),
            RulesHarness.HoldingsOf(),
            RulesHarness.TreasuryWith(Money.Zero),
            Difficulty.Normal,
            Origin.Scholar);

        var lines = computation.Lines.Where(l => l.Category == LedgerCategory.OfficialSalary).ToList();

        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, l => l.PersonId == active.Id);
        Assert.Contains(lines, l => l.PersonId == retired.Id);
        Assert.DoesNotContain(lines, l => l.PersonId == awaiting.Id);
        Assert.DoesNotContain(lines, l => l.PersonId == none.Id);

        // 资金类条目的金额 MUST 非 0（「不发」而不是「发 0」）。
        Assert.All(lines, l => Assert.True(l.Amount.IsPositive));
    }

    [Fact]
    public void 俸禄锚点在两态下复核()
    {
        AssertAnchorInBothModes(SalaryTable.HighestLevel, 5100m);              // arch-guard:allow 锚点即被验证对象
        AssertAnchorInBothModes(SalaryTable.LowestLevel - 3, 420m);           // arch-guard:allow 锚点即被验证对象
        AssertAnchorInBothModes(SalaryTable.LowestLevel, 72m);               // arch-guard:allow 锚点即被验证对象
    }

    [Fact]
    public void 半俸不累乘且恰为在任态的俸禄比例倍()
    {
        const int level = 15;

        var active = RulesHarness.Official(21, level: level);
        var retired = RulesHarness.Official(22, level: level);
        retired.Status |= StatusFlag.Retired;

        var computation = RulesHarness.Income(
            RulesHarness.FamilyOf(active, retired),
            RulesHarness.HoldingsOf(),
            RulesHarness.TreasuryWith(Money.Zero),
            Difficulty.Normal,
            Origin.Scholar);

        var activeAmount = computation.Lines.Single(
            l => l.Category == LedgerCategory.OfficialSalary && l.PersonId == active.Id).Amount;
        var retiredAmount = computation.Lines.Single(
            l => l.Category == LedgerCategory.OfficialSalary && l.PersonId == retired.Id).Amount;

        // 半俸 MUST 恰为在任态的 RetirementSalaryRatio 倍（若两处各乘一次会变成 0.25 倍）。
        Assert.Equal(activeAmount * OfficialCareerPolicy.RetirementSalaryRatio, retiredAmount);
        Assert.NotEqual(activeAmount * OfficialCareerPolicy.RetirementSalaryRatio * OfficialCareerPolicy.RetirementSalaryRatio, retiredAmount);
    }

    [Fact]
    public void 三态都乘难度收益系数且士出身再加成而仕身份无关()
    {
        const int level = 15;

        foreach (var difficulty in Enum.GetValues<Difficulty>())
        {
            var active = RulesHarness.Official(31, level: level);
            var retired = RulesHarness.Official(32, level: level);
            retired.Status |= StatusFlag.Retired;

            var family = RulesHarness.FamilyOf(active, retired);

            // 仕身份（family.HasShiStatus）为真：俸禄 MUST NOT 受它影响（FR-021）。
            family.HasShiStatus = true;

            var scholar = RulesHarness.Income(
                family,
                RulesHarness.HoldingsOf(),
                RulesHarness.TreasuryWith(Money.Zero),
                difficulty,
                Origin.Scholar);

            var artisan = RulesHarness.Income(
                RulesHarness.FamilyOf(
                    RulesHarness.Official(31, level: level),
                    RulesHarness.Official(32, level: level)),
                RulesHarness.HoldingsOf(),
                RulesHarness.TreasuryWith(Money.Zero),
                difficulty,
                Origin.Artisan);

            var scholarActive = scholar.Lines.Single(
                l => l.Category == LedgerCategory.OfficialSalary && l.PersonId == active.Id).Amount;
            var artisanActive = artisan.Lines.Single(
                l => l.Category == LedgerCategory.OfficialSalary && l.PersonId == active.Id).Amount;

            Assert.Equal(
                RulesHarness.Guan(
                    SalaryTable.MonthlySalaryGuan(level)
                    * DifficultyRates.RevenueFactor(difficulty)
                    * SalaryTable.ScholarOriginMultiplier),
                scholarActive);

            Assert.Equal(
                RulesHarness.Guan(SalaryTable.MonthlySalaryGuan(level) * DifficultyRates.RevenueFactor(difficulty)),
                artisanActive);

            // 三态都乘：致仕态同式再半俸。
            var scholarRetired = scholar.Lines.Single(
                l => l.Category == LedgerCategory.OfficialSalary && l.PersonId == retired.Id).Amount;

            Assert.Equal(
                RulesHarness.Guan(
                    SalaryTable.MonthlySalaryGuan(level)
                    * DifficultyRates.RevenueFactor(difficulty)
                    * SalaryTable.ScholarOriginMultiplier
                    * OfficialCareerPolicy.RetirementSalaryRatio),
                scholarRetired);
        }
    }

    /// <summary>
    /// 同一品级下复核：在任态 = 年俸 ÷ 12 × 难度收益系数 × 士出身加成；
    /// 致仕态 = 上式 × <see cref="OfficialCareerPolicy.RetirementSalaryRatio"/>。
    /// </summary>
    private static void AssertAnchorInBothModes(int level, decimal annualSalaryGuan)
    {
        var active = RulesHarness.Official(level + 100, level: level);
        var retired = RulesHarness.Official(level + 200, level: level);
        retired.Status |= StatusFlag.Retired;

        var computation = RulesHarness.Income(
            RulesHarness.FamilyOf(active, retired),
            RulesHarness.HoldingsOf(),
            RulesHarness.TreasuryWith(Money.Zero),
            Difficulty.Normal,
            Origin.Scholar);

        var monthly = annualSalaryGuan
            / IncomeRateTable.MonthsPerYear
            * DifficultyRates.RevenueFactor(Difficulty.Normal)
            * SalaryTable.ScholarOriginMultiplier;

        Assert.Equal(annualSalaryGuan, SalaryTable.AnnualSalaryGuan(level));

        Assert.Equal(
            RulesHarness.Guan(monthly),
            computation.Lines.Single(
                l => l.Category == LedgerCategory.OfficialSalary && l.PersonId == active.Id).Amount);

        Assert.Equal(
            RulesHarness.Guan(monthly * OfficialCareerPolicy.RetirementSalaryRatio),
            computation.Lines.Single(
                l => l.Category == LedgerCategory.OfficialSalary && l.PersonId == retired.Id).Amount);
    }
}
