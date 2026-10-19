using System.Collections.ObjectModel;
using System.Reflection;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Services;
using KFL.Rules.Config;
using KFL.Rules.Settlement;
using Xunit;

namespace KFL.Tests.Rules;

public partial class LedgerTests
{
    [Fact]
    public void 事件类条目存在且可读但不参与求和()
    {
        // ① 饥馑四类迁移：付不起时不落生活费条目，只落**事件类**条目。
        var famine = NewFamineState();
        var engine = new MonthlySettlementEngine(new SeededRandomService(Seed), new GameStateClock(famine), new FixedNameGenerator());
        var steps = new List<MonthStep>();

        var monthsToSevere = FamineTimeline.MonthsUntilRelief + FamineTimeline.ReliefMonthsUntilSevere - 1;

        for (var index = 0; index < monthsToSevere; index++)
        {
            steps.Add(Settle(engine, famine));
        }

        Assert.Equal(FamineStage.Severe, famine.Economy.Famine.Stage);

        var ledger = famine.Economy.Ledger;
        var from = steps[0].Month;
        var to = steps[^1].Month;

        // 恰好三条迁移条目（进入、转救济、转第三阶段），且全部为事件类。
        Assert.Equal(3, ledger.Entries.Count);
        Assert.All(ledger.Entries, e => Assert.Equal(LedgerEntryKind.Event, LedgerCategoryMetadata.KindOf(e.Category)));
        Assert.Single(ledger.Entries, e => e.Category == LedgerCategory.FamineEntered);
        Assert.Single(ledger.Entries, e => e.Category == LedgerCategory.FamineReliefEntered);
        Assert.Single(ledger.Entries, e => e.Category == LedgerCategory.FamineSevereEntered);

        // 金额与归属可读（迁移一律 0、家族级），但不参与求和：区间合计仍为 0。
        Assert.All(ledger.Entries, e => Assert.Equal(Money.Zero, e.Amount));
        Assert.All(ledger.Entries, e => Assert.Null(e.PersonId));
        Assert.Equal(Money.Zero, ledger.TreasuryDeltaIn(from, to));
        Assert.Empty(ledger.TotalsByCategory(from, to));

        // 第四类迁移：补足资金 → 全部解除，同样只落一条事件类条目。
        famine.Economy.Apply(LedgerCategory.AssetSale, null, Guan(1000m), famine.CurrentDate);

        var resolved = Settle(engine, famine);

        Assert.Equal(FamineStage.None, famine.Economy.Famine.Stage);
        Assert.True(resolved.Result.LivingCostPaid.IsPositive);
        Assert.Equal(Money.Zero, Assert.Single(ledger.Entries, e => e.Category == LedgerCategory.FamineResolved).Amount);

        // ② 贷款计息入欠息：同为事件类条目，可读但不参与求和。
        var loanRun = RunLoanInterestMonth();
        var loanStep = Assert.Single(loanRun.Steps);
        var accrual = Assert.Single(loanRun.State.Economy.Ledger.Entries, e => e.Category == LedgerCategory.LoanInterestAccrued);

        Assert.True(loanStep.Result.LoanInterestAccrued.IsPositive);
        Assert.Equal(loanStep.Result.LoanInterestAccrued, accrual.Amount);
        Assert.Equal(LedgerEntryKind.Event, LedgerCategoryMetadata.KindOf(accrual.Category));
        Assert.Null(accrual.PersonId);

        // 计息只增加负债：事件类条目不参与求和，故区间合计仍等于资金池变动。
        AssertMoneyIdentity(
            loanStep.PoolAfter - loanStep.PoolBefore,
            loanRun.State.Economy.Ledger.TreasuryDeltaIn(loanStep.Month, loanStep.Month));
        Assert.DoesNotContain(
            loanRun.State.Economy.Ledger.TotalsByCategory(loanStep.Month, loanStep.Month),
            t => t.Category == LedgerCategory.LoanInterestAccrued);
    }

    [Fact]
    public void 资金类条目金额非零而事件类可为零()
    {
        var run = RunFixture(RunMonths, Origin.Artisan);

        foreach (var step in run.Steps)
        {
            foreach (var entry in step.Result.Entries)
            {
                if (IsTreasury(entry))
                {
                    Assert.NotEqual(Money.Zero, entry.Amount);
                }
                else
                {
                    Assert.Equal(LedgerEntryKind.Event, LedgerCategoryMetadata.KindOf(entry.Category));
                }
            }
        }

        // 事件类条目允许 0 元（饥馑迁移一律 0），且仍入账可读——与资金类的不变量形成对照。
        var famine = NewFamineState();
        Settle(new MonthlySettlementEngine(new SeededRandomService(Seed), new GameStateClock(famine), new FixedNameGenerator()), famine);

        var entered = Assert.Single(
            famine.Economy.Ledger.Entries, e => e.Category == LedgerCategory.FamineEntered);

        Assert.Equal(Money.Zero, entered.Amount);
    }

    [Fact]
    public void 条目数与资金事件一一对应不存在只动资金池而不落条目的路径()
    {
        var run = RunFixture(RunMonths, Origin.Artisan);

        foreach (var step in run.Steps)
        {
            var treasury = new List<LedgerEntry>();
            var events = new List<LedgerEntry>();

            foreach (var entry in step.Result.Entries)
            {
                (IsTreasury(entry) ? treasury : events).Add(entry);
            }

            // 资金类条目逐条对应一个已发生的资金事件：收入分项（含年度项）+ 实付生活费 + 先本后息两段。
            var expected = step.Result.Incomes.Count
                + (step.Result.LivingCostPaid.IsPositive ? 1 : 0)
                + (step.Result.LoanRepayment.PrincipalPart.IsPositive ? 1 : 0)
                + (step.Result.LoanRepayment.InterestPart.IsPositive ? 1 : 0);

            Assert.Equal(expected, treasury.Count);

            // 资金池动了就必然有资金类条目——「只动池而不落条目」在本阶段不可表达（FR-021）。
            if (step.PoolAfter != step.PoolBefore)
            {
                Assert.NotEmpty(treasury);
            }

            // 事件类条目只可能是五类迁移之一（种类由 LedgerCategoryMetadata 唯一决定）。
            Assert.All(events, e => Assert.Equal(LedgerEntryKind.Event, LedgerCategoryMetadata.KindOf(e.Category)));

            // 每条条目都落在本月的年月下（可读、可定位）。
            Assert.All(step.Result.Entries, e => Assert.Equal(step.Month, e.Date));
        }

        // 账本本身不含任何「未落条目」的池变动：全区间恒等。
        var from = run.Steps[0].Month;
        var to = run.Steps[^1].Month;

        AssertMoneyIdentity(
            run.Steps[^1].PoolAfter - run.Steps[0].PoolBefore,
            run.State.Economy.Ledger.TreasuryDeltaIn(from, to));
    }

    [Fact]
    public void 不存在任何月度汇总类型或字段且聚合每次从条目重算()
    {
        // 契约四 §4 不变量 6 / FR-021：源码级断言「不存在月度汇总类型/字段」。
        var core = typeof(Ledger).Assembly;

        var summaryTypes = core.GetTypes()
            .Where(t => t.Name.Contains("Summary", StringComparison.Ordinal)
                || t.Name.Contains("Monthly", StringComparison.Ordinal))
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        Assert.Empty(summaryTypes);

        // 字段层面：任何实体都 MUST NOT 出现形似月度汇总的字段（含编译期生成的支持字段）。
        foreach (var type in core.GetTypes())
        {
            foreach (var field in type.GetFields(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Assert.False(
                    field.Name.Contains("Summary", StringComparison.Ordinal)
                        || field.Name.Contains("Monthly", StringComparison.Ordinal),
                    $"{type.FullName}.{field.Name} 形似月度汇总字段（契约四 §4 不变量 6）。");
            }
        }

        // Ledger 自身的存储只有「条目列表 + 其只读视图」——没有可存放汇总的第二处。
        var storage = typeof(Ledger).GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.Equal(2, storage.Length);

        foreach (var field in storage)
        {
            Assert.True(
                field.FieldType == typeof(List<LedgerEntry>)
                    || field.FieldType == typeof(ReadOnlyCollection<LedgerEntry>),
                $"Ledger 只能持有条目本身，实际出现 {field.Name}（{field.FieldType}）。");
        }

        // 「每次从条目算」：追加一条同月条目后，同一区间的合计随之改变（不是预先存好的值）。
        var run = RunFixture(1);
        var ledger = run.State.Economy.Ledger;
        var month = run.Steps[0].Month;
        var before = ledger.TreasuryDeltaIn(month, month);

        run.State.Economy.Apply(LedgerCategory.LandRentIncome, null, Guan(1000m), month);

        AssertMoneyIdentity(before + Guan(1000m), ledger.TreasuryDeltaIn(month, month));
        Assert.Contains(ledger.TotalsByCategory(month, month), t => t.Category == LedgerCategory.LandRentIncome);
    }
}
