using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;
using KFL.Rules.Settlement;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// US1 / quickstart S2、SC-004、§16 必测三项之一：俸禄 18 级锚点 72 / 420 / 5100 误差为 0。
/// </summary>
public class SalaryTableTests
{
    [Fact]
    public void 十八级年俸逐级可读且自高至低严格递减()
    {
        Assert.Equal(1, SalaryTable.HighestLevel);
        Assert.Equal(18, SalaryTable.LowestLevel);

        var previous = decimal.MaxValue;

        for (var level = SalaryTable.HighestLevel; level <= SalaryTable.LowestLevel; level++)
        {
            var annual = SalaryTable.AnnualSalaryGuan(level);

            Assert.True(annual > 0m);
            Assert.True(annual < previous, $"L{level} 的年俸 MUST 低于 L{level - 1}（规格书 §8.1）。");
            previous = annual;
        }
    }

    [Fact]
    public void 俸禄锚点误差为零()
    {
        // 锚点断言是 SC-004 的**被验证对象本身**，故允许写字面量（contracts/config-registry.md §5 条款 2）。
        Assert.Equal(5100m, SalaryTable.AnnualSalaryGuan(SalaryTable.HighestLevel)); // config-literal:allow SC-004 锚点
        Assert.Equal(420m, SalaryTable.AnnualSalaryGuan(SalaryTable.LowestLevel - 3)); // config-literal:allow SC-004 锚点
        Assert.Equal(72m, SalaryTable.AnnualSalaryGuan(SalaryTable.LowestLevel)); // config-literal:allow SC-004 锚点
    }

    [Fact]
    public void 月摊为年俸除以十二月而越界品级被拒()
    {
        for (var level = SalaryTable.HighestLevel; level <= SalaryTable.LowestLevel; level++)
        {
            Assert.Equal(
                SalaryTable.AnnualSalaryGuan(level) / IncomeRateTable.MonthsPerYear,
                SalaryTable.MonthlySalaryGuan(level));
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            () => SalaryTable.AnnualSalaryGuan(SalaryTable.HighestLevel - 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SalaryTable.MonthlySalaryGuan(SalaryTable.LowestLevel + 1));
    }

    [Fact]
    public void 士出身当官乘俸禄加成而士出身无官职不产生官俸()
    {
        const int level = 5;

        var ranked = RulesHarness.Member(21, Gender.Male, 40);
        ranked.Rank = new OfficialRank(level);

        var unranked = RulesHarness.Member(22, Gender.Male, 38);

        var scholar = RulesHarness.Income(
            RulesHarness.FamilyOf(ranked, unranked),
            RulesHarness.HoldingsOf(),
            RulesHarness.TreasuryWith(Money.Zero),
            Difficulty.Normal,
            Origin.Scholar);

        var line = Assert.Single(scholar.Lines, l => l.Category == LedgerCategory.OfficialSalary);

        Assert.Equal(ranked.Id, line.PersonId);
        Assert.Equal(
            RulesHarness.Guan(
                SalaryTable.MonthlySalaryGuan(level)
                * SalaryTable.ScholarOriginMultiplier
                * DifficultyRates.RevenueFactor(Difficulty.Normal)),
            line.Amount);

        var artisan = RulesHarness.Income(
            RulesHarness.FamilyOf(ranked, unranked),
            RulesHarness.HoldingsOf(),
            RulesHarness.TreasuryWith(Money.Zero),
            Difficulty.Normal,
            Origin.Artisan);

        var plain = Assert.Single(artisan.Lines, l => l.Category == LedgerCategory.OfficialSalary);

        Assert.Equal(
            RulesHarness.Guan(SalaryTable.MonthlySalaryGuan(level) * DifficultyRates.RevenueFactor(Difficulty.Normal)),
            plain.Amount);
        Assert.True(plain.Amount < line.Amount);
    }

    [Fact]
    public void 官俸乘难度收益系数()
    {
        const int level = 5;

        var ranked = RulesHarness.Member(23, Gender.Male, 40);
        ranked.Rank = new OfficialRank(level);

        foreach (var difficulty in Enum.GetValues<Difficulty>())
        {
            var computation = RulesHarness.Income(
                RulesHarness.FamilyOf(ranked),
                RulesHarness.HoldingsOf(),
                RulesHarness.TreasuryWith(Money.Zero),
                difficulty,
                Origin.Artisan);

            var line = computation.Lines.Single(l => l.Category == LedgerCategory.OfficialSalary);

            Assert.Equal(
                RulesHarness.Guan(
                    SalaryTable.MonthlySalaryGuan(level) * DifficultyRates.RevenueFactor(difficulty)),
                line.Amount);
        }
    }
}
