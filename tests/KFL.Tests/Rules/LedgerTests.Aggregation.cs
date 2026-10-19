using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using Xunit;

namespace KFL.Tests.Rules;

public partial class LedgerTests
{
    [Fact]
    public void 任意单月与任意连续区间的资金类条目合计等于资金池变动()
    {
        var run = RunFixture(RunMonths, Origin.Artisan);
        var ledger = run.State.Economy.Ledger;

        foreach (var step in run.Steps)
        {
            // 快照口径与外部观测一致（TreasuryPoolBefore 是引擎在动手之前取的值）。
            Assert.Equal(step.PoolBefore, step.Result.TreasuryPoolBefore);
        }

        // 全部「单月」与全部「连续区间」（含跨年、跨年度项的区间）逐一断言。
        for (var start = 0; start < run.Steps.Count; start++)
        {
            for (var end = start; end < run.Steps.Count; end++)
            {
                var from = run.Steps[start].Month;
                var to = run.Steps[end].Month;
                var expected = run.Steps[end].PoolAfter - run.Steps[start].PoolBefore;

                AssertMoneyIdentity(expected, ledger.TreasuryDeltaIn(from, to));
            }
        }
    }

    [Fact]
    public void 结算结果条目与账本新增条目逐条相同()
    {
        var run = RunFixture(RunMonths, Origin.Artisan);
        var appended = 0;

        foreach (var step in run.Steps)
        {
            AssertSameEntries(step.Appended, step.Result.Entries);

            // 快照里的条目 MUST 是**本次**追加的那一段，不是整册账本的重复切片。
            Assert.Equal(appended, IndexOf(run.State.Economy.Ledger, step.Appended));
            appended += step.Appended.Count;
        }

        Assert.Equal(appended, run.State.Economy.Ledger.Entries.Count);
    }

    [Fact]
    public void 家族维度与角色维度聚合的是同一批条目()
    {
        var run = RunFixture(RunMonths, Origin.Artisan);
        var ledger = run.State.Economy.Ledger;
        var from = run.Steps[0].Month;
        var to = run.Steps[^1].Month;
        var delta = ledger.TreasuryDeltaIn(from, to);

        // ① 逐类别合计 = 家族维度合计（收益来源明细与支出明细同一批条目）。
        var byCategory = Money.Zero;

        foreach (var (_, total) in ledger.TotalsByCategory(from, to))
        {
            byCategory += total;
        }

        AssertMoneyIdentity(delta, byCategory);

        // ② 角色维度之和 + 家族级之和 = 家族维度合计（既不重复计、也不丢失）。
        var familyLevel = Money.Zero;

        foreach (var entry in ledger.EntriesIn(from, to))
        {
            if (IsTreasury(entry) && entry.PersonId is null)
            {
                familyLevel += entry.Amount;
            }
        }

        var byPerson = Money.Zero;

        foreach (var member in run.State.Family.Members)
        {
            byPerson += ledger.TotalsByPerson(member.Id, from, to);
        }

        AssertMoneyIdentity(delta, byPerson + familyLevel);

        // ③ 每名成员的逐月合计之和 = 其区间累计；逐月按月升序且不重复。
        foreach (var member in run.State.Family.Members)
        {
            var months = ledger.MonthlyByPerson(member.Id, from, to);
            var monthly = Money.Zero;

            for (var index = 0; index < months.Count; index++)
            {
                monthly += months[index].Total;

                if (index > 0)
                {
                    Assert.True(
                        months[index - 1].Month < months[index].Month,
                        "逐月合计 MUST 按年月升序且同月至多出现一次。");
                }
            }

            AssertMoneyIdentity(ledger.TotalsByPerson(member.Id, from, to), monthly);
        }
    }
}
