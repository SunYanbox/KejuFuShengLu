using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Services;
using KFL.Rules.Config;
using KFL.Rules.Economy;
using KFL.Rules.Settlement;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// US2 / quickstart S3、SC-002、规格书 §16 必测三项之一：贷款计息节点、划扣比例（20/40/80）、
/// 先本后息、封顶、计时按自然月与支付原语（FR-013~FR-017）。
/// </summary>
/// <remarks>
/// 期望值一律经 <c>KFL.Rules/Config</c> 的成员取得，本文件 MUST NOT 另存规则数值（SC-008）。
/// 夹具只用 <c>KFL.Core</c> 的公开 API 构造（MUST NOT 走反射绕过不变量校验）。
/// </remarks>
public class LoanTests
{
    private static readonly Guid StateId = Guid.Parse("9b2d4e6a-1c3f-4a5b-8d7e-0f1a2b3c4d5e");
    private static readonly GameDate Date = RulesHarness.Date;

    /// <summary>确定性复跑用的固定种子（不是规则数值）。</summary>
    private const int Seed = 42;

    /// <summary>复跑月数（覆盖两个计息周期）。</summary>
    private const int SequenceMonths = 26;

    [Fact]
    public void 计息节点只由距上次计息的月数决定()
    {
        var period = InterestPolicy.InterestPeriodMonths;

        Assert.False(LoanSettlement.IsInterestDue(LoanOf(Guan(10m), monthsSinceInterest: 0)));
        Assert.False(LoanSettlement.IsInterestDue(LoanOf(Guan(10m), monthsSinceInterest: period - 1)));
        Assert.True(LoanSettlement.IsInterestDue(LoanOf(Guan(10m), monthsSinceInterest: period)));
        Assert.True(LoanSettlement.IsInterestDue(LoanOf(Guan(10m), monthsSinceInterest: period + 1)));

        // 与债务规模无关：本金为 0 的贷款照样命中节点（计息额为 0，US2 的 ⑦ 边界）。
        Assert.True(LoanSettlement.IsInterestDue(LoanOf(Money.Zero, monthsSinceInterest: period)));
    }

    [Fact]
    public void 利率映射共用一张表且区间两端可断言()
    {
        Assert.Equal(InterestPolicy.SavingsRate, InterestPolicy.LoanRate);
        Assert.True(InterestPolicy.SavingsRate.Min < InterestPolicy.SavingsRate.Max);

        Assert.Equal(InterestPolicy.SavingsRate.Min, InterestPolicy.RateFor(0d));
        Assert.Equal(InterestPolicy.SavingsRate.Max, InterestPolicy.RateFor(1d));

        // 极大值同样落在区间端点（不因 (decimal) 换算溢出而抛异常）。
        Assert.Equal(InterestPolicy.SavingsRate.Max, InterestPolicy.RateFor(double.MaxValue));
        Assert.Equal(InterestPolicy.SavingsRate.Min, InterestPolicy.RateFor(-1d));

        // RollRate 消费 1 次 NextDouble()，映射与 RateFor 一致。
        Assert.Equal(InterestPolicy.RateFor(0.5d), LoanSettlement.RollRate(new FixedRandomService(0.5d)));  // arch-guard:allow 夹具金额与随机取值（非规则数值副本）
    }

    [Fact]
    public void 划扣额为净利润乘比例并封顶于债务总额()
    {
        var loan = LoanOf(Guan(10m), Guan(5m));

        Assert.Equal(
            Guan(20m * LoanPolicy.CommonerRepaymentRatio),
            LoanSettlement.ComputeRepayment(Guan(20m), hasShiStatus: false, origin: Origin.Farmer, loan: loan));

        // 封顶：多出部分 MUST NOT 变成规格书没有的「退款」资金流。
        Assert.Equal(
            loan.Total,
            LoanSettlement.ComputeRepayment(Guan(1000m), hasShiStatus: false, origin: Origin.Farmer, loan: loan));

        Assert.Equal(
            Money.Zero,
            LoanSettlement.ComputeRepayment(Money.Zero, hasShiStatus: false, origin: Origin.Farmer, loan: loan));
        Assert.Equal(
            Money.Zero,
            LoanSettlement.ComputeRepayment(-Guan(3m), hasShiStatus: false, origin: Origin.Farmer, loan: loan));

        // 已结清 → 不再划扣。
        Assert.Equal(
            Money.Zero,
            LoanSettlement.ComputeRepayment(Guan(20m), hasShiStatus: false, origin: Origin.Farmer, loan: LoanOf(Money.Zero)));
    }

    [Fact]
    public void 三档划扣比例各自可读出并在结算中生效()
    {
        Assert.Equal(LoanPolicy.ShiRepaymentRatio, LoanPolicy.RepaymentRatio(hasShiStatus: true, origin: Origin.Farmer));
        Assert.Equal(LoanPolicy.ShiRepaymentRatio, LoanPolicy.RepaymentRatio(hasShiStatus: true, origin: Origin.Merchant));
        Assert.Equal(LoanPolicy.MerchantRepaymentRatio, LoanPolicy.RepaymentRatio(hasShiStatus: false, origin: Origin.Merchant));

        foreach (var origin in new[] { Origin.Farmer, Origin.Artisan, Origin.Scholar })
        {
            Assert.Equal(LoanPolicy.CommonerRepaymentRatio, LoanPolicy.RepaymentRatio(hasShiStatus: false, origin: origin));
        }

        var shi = SettleWithLoan(Origin.Scholar, hasShiStatus: true);
        var merchant = SettleWithLoan(Origin.Merchant, hasShiStatus: false);
        var farmer = SettleWithLoan(Origin.Farmer, hasShiStatus: false);

        Assert.Equal(shi.Result.NetProfit * LoanPolicy.ShiRepaymentRatio, shi.Repaid);
        Assert.Equal(merchant.Result.NetProfit * LoanPolicy.MerchantRepaymentRatio, merchant.Repaid);
        Assert.Equal(farmer.Result.NetProfit * LoanPolicy.CommonerRepaymentRatio, farmer.Repaid);
    }

    [Fact]
    public void 净利润不为正时不划扣不罚而计时照常推进()
    {
        // 负净利润：儿童成员无收入、现金足够付口粮 → 实付 > 收入。
        var loan = LoanOf(Guan(10m), monthsSinceInterest: 7);
        var monthsBefore = loan.MonthsSinceInterest;
        var result = Settle(NewState(loan, FamilyOf(0, 1), cash: Guan(5m)));

        Assert.True(result.NetProfit.IsNegative);
        Assert.Equal(Money.Zero, result.LoanRepayment.PrincipalPart);
        Assert.Equal(Money.Zero, result.LoanRepayment.InterestPart);
        Assert.DoesNotContain(
            result.Entries,
            e => e.Category is LedgerCategory.LoanPrincipalRepaid or LedgerCategory.LoanInterestRepaid);
        Assert.Equal(Guan(10m), loan.Principal);
        Assert.Equal(monthsBefore + 1, loan.MonthsSinceInterest);

        // 净利润恰为 0：MUST NOT 产生 0 元划扣条目（资金类条目金额非 0 的不变量）。
        var zeroLoan = LoanOf(Guan(10m), monthsSinceInterest: 7);
        var zeroBefore = zeroLoan.MonthsSinceInterest;
        var zeroResult = Settle(NewState(zeroLoan, FamilyOf(0, 1)));

        Assert.Equal(Money.Zero, zeroResult.NetProfit);
        Assert.DoesNotContain(
            zeroResult.Entries,
            e => e.Category is LedgerCategory.LoanPrincipalRepaid or LedgerCategory.LoanInterestRepaid);
        Assert.Equal(zeroResult.TreasuryPoolBefore, zeroResult.TreasuryPoolAfter);
        Assert.Equal(zeroBefore + 1, zeroLoan.MonthsSinceInterest);
    }

    [Fact]
    public void 满十二个月按当时本金计息一次且本金不增()
    {
        var principal = Guan(1_000_000m);
        var loan = LoanOf(principal, monthsSinceInterest: InterestPolicy.InterestPeriodMonths - 1);
        var state = NewState(loan, FamilyOf(1, 0), cash: Guan(1000m));
        var engine = new MonthlySettlementEngine(new FixedRandomService(), new GameStateClock(state));

        var first = engine.Settle(state);

        Assert.Equal(principal * InterestPolicy.RateFor(0.5d), first.LoanInterestAccrued);  // arch-guard:allow 夹具金额与随机取值（非规则数值副本）
        Assert.True(first.LoanInterestAccrued.IsPositive);

        // 计息额恰为欠息增量：本金 MUST NOT 因计息而增加（§5.4「利息永不滚入本金」）。
        Assert.Equal(first.LoanInterestAccrued, loan.AccruedInterest);
        Assert.Equal(principal - first.LoanRepayment.PrincipalPart, loan.Principal);

        // 之后的 11 个月都不再计息（第 13 个月不重复），第 12 个结算月再次命中。
        for (var index = 0; index < InterestPolicy.InterestPeriodMonths - 1; index++)
        {
            Assert.Equal(Money.Zero, engine.Settle(state).LoanInterestAccrued);
        }

        Assert.True(engine.Settle(state).LoanInterestAccrued.IsPositive);
    }

    [Fact]
    public void 同月既计息又划扣时计息本金取月初结余()
    {
        var principal = Guan(1m);
        var loan = LoanOf(principal, monthsSinceInterest: InterestPolicy.InterestPeriodMonths - 1);
        var result = Settle(NewState(loan, FamilyOf(1, 0), cash: Guan(1000m)));

        // 先计息：本金取**月初**结余（= 尚未被本月划扣冲减的 1 贯）。
        Assert.Equal(principal * InterestPolicy.RateFor(0.5d), result.LoanInterestAccrued);  // arch-guard:allow 夹具金额与随机取值（非规则数值副本）
        Assert.True(result.LoanRepayment.PrincipalPart.IsPositive);

        // 若次序相反（先划扣、后按剩余本金计息），计息额会是 (1 贯 − 划扣) × 利率。
        Assert.NotEqual(
            (principal - result.LoanRepayment.PrincipalPart) * InterestPolicy.RateFor(0.5d),  // arch-guard:allow 夹具金额与随机取值（非规则数值副本）
            result.LoanInterestAccrued);
        Assert.Equal(principal - result.LoanRepayment.PrincipalPart, loan.Principal);
    }

    [Fact]
    public void 先本后息且本金清零后转向欠息皆清后不再划扣()
    {
        var principal = Guan(0.005m);  // arch-guard:allow 夹具金额与随机取值（非规则数值副本）
        var interest = Guan(0.02m);  // arch-guard:allow 夹具金额与随机取值（非规则数值副本）
        var loan = LoanOf(principal, interest);
        var result = Settle(NewState(loan, FamilyOf(1, 0), cash: Guan(1000m)));

        // 名义划扣额远大于债务总额 → 封顶在 Total，且**先冲本金、再冲欠息**。
        Assert.Equal(principal, result.LoanRepayment.PrincipalPart);
        Assert.Equal(interest, result.LoanRepayment.InterestPart);
        Assert.True(loan.IsSettled);

        Assert.Equal(-principal, Assert.Single(result.Entries, e => e.Category == LedgerCategory.LoanPrincipalRepaid).Amount);
        Assert.Equal(-interest, Assert.Single(result.Entries, e => e.Category == LedgerCategory.LoanInterestRepaid).Amount);

        // 皆清之后：不再产生任何划扣条目。
        var after = Settle(NewState(loan, FamilyOf(1, 0), cash: Guan(1000m)));
        var settled = after.Entries;

        Assert.Equal(Money.Zero, after.LoanRepayment.PrincipalPart);
        Assert.Equal(Money.Zero, after.LoanRepayment.InterestPart);
        Assert.DoesNotContain(
            settled,
            e => e.Category is LedgerCategory.LoanPrincipalRepaid or LedgerCategory.LoanInterestRepaid);
        Assert.Equal(Money.Zero, after.LoanInterestAccrued);
    }

    [Fact]
    public void 本金为零而欠息为正时计息额为零()
    {
        var interest = Guan(5m);
        var loan = LoanOf(Money.Zero, interest, monthsSinceInterest: InterestPolicy.InterestPeriodMonths - 1);

        // 儿童成员、零资金：净利润恰为 0，故本月无划扣，欠息不受影响。
        var result = Settle(NewState(loan, FamilyOf(0, 1)));

        Assert.Equal(Money.Zero, result.LoanInterestAccrued);
        Assert.Equal(Money.Zero, loan.Principal);
        Assert.Equal(interest, loan.AccruedInterest);

        // 节点仍被命中（只由计数决定），但金额为 0 的事件条目仍然可读。
        Assert.Equal(Money.Zero, Assert.Single(result.Entries, e => e.Category == LedgerCategory.LoanInterestAccrued).Amount);

        // 计数已归零，故次月不再命中。
        Assert.Equal(Money.Zero, Settle(NewState(loan, FamilyOf(0, 1))).LoanInterestAccrued);
    }

    [Fact]
    public void 固定种子下贷款结算序列逐项一致()
    {
        var first = RunLoanSequence();
        var second = RunLoanSequence();

        Assert.Equal(SequenceMonths, first.Count);
        Assert.Equal(first, second);

        // 序列确实覆盖了计息与划扣两个分支（否则「一致」没有意义）。
        Assert.Contains(first, step => step.Interest.IsPositive);
        Assert.Contains(first, step => step.PrincipalPart.IsPositive);

        static List<(Money Interest, Money PrincipalPart, Money InterestPart, Money Principal, Money Accrued, int Clock)>
            RunLoanSequence()
        {
            // 本金取到「26 个月内还不清」的量级，使计息与划扣两个分支在序列里都真实出现。
            var fixture = EconomyFixtures.Create(new EconomyFixtureSettings
            {
                LoanPrincipal = Money.FromGuan(1_000_000m),
                LoanAccruedInterest = Money.Zero,
                LoanMonthsSinceInterest = 0,
            });
            var state = new GameState(
                StateId, fixture.Date, Difficulty.Normal, Origin.Artisan, fixture.Family, fixture.Economy);
            var engine = new MonthlySettlementEngine(new SeededRandomService(Seed), new GameStateClock(state));
            var loan = fixture.Economy.Treasury.Loan;
            var trace = new List<(Money, Money, Money, Money, Money, int)>();

            for (var month = 0; month < SequenceMonths; month++)
            {
                var result = engine.Settle(state);

                trace.Add((
                    result.LoanInterestAccrued,
                    result.LoanRepayment.PrincipalPart,
                    result.LoanRepayment.InterestPart,
                    loan.Principal,
                    loan.AccruedInterest,
                    loan.MonthsSinceInterest));
            }

            return trace;
        }
    }

    [Fact]
    public void 支付原语按现金到储蓄到贷款三步执行()
    {
        var loan = new Loan();
        var economy = EconomyOf(loan, cash: Guan(10m), savings: Guan(5m));

        // 第一步：现金足额。
        var first = PaymentPrimitive.Pay(economy, LedgerCategory.LivingCost, Date, Guan(4m));

        Assert.Equal(Guan(4m), first.FromCash);
        Assert.Equal(Money.Zero, first.FromSavings);
        Assert.Equal(Money.Zero, first.ToLoan);
        Assert.Equal(Guan(6m), economy.Treasury.Cash);
        Assert.Equal(Guan(5m), economy.Treasury.Savings);
        Assert.Equal(Money.Zero, loan.Principal);
        Assert.Equal(-Guan(4m), Assert.Single(economy.Ledger.Entries).Amount);

        // 第二步：现金不足 → 差额由储蓄补足。
        var second = PaymentPrimitive.Pay(economy, LedgerCategory.LivingCost, Date, Guan(8m));

        Assert.Equal(Guan(6m), second.FromCash);
        Assert.Equal(Guan(2m), second.FromSavings);
        Assert.Equal(Money.Zero, second.ToLoan);
        Assert.Equal(Money.Zero, economy.Treasury.Cash);
        Assert.Equal(Guan(3m), economy.Treasury.Savings);
        Assert.Equal(Money.Zero, loan.Principal);

        // 第三步：现金 + 储蓄都不足 → 缺口转贷款本金（不改资金池，故不落资金类条目）。
        var third = PaymentPrimitive.Pay(economy, LedgerCategory.LivingCost, Date, Guan(10m));

        Assert.Equal(Money.Zero, third.FromCash);
        Assert.Equal(Guan(3m), third.FromSavings);
        Assert.Equal(Guan(7m), third.ToLoan);
        Assert.Equal(Money.Zero, economy.Treasury.Savings);
        Assert.Equal(Guan(7m), loan.Principal);
        Assert.Equal(-third.FromTreasury, economy.Ledger.Entries[^1].Amount);
        Assert.Equal(third.FromTreasury, third.FromCash + third.FromSavings);
        Assert.Equal(Guan(10m), third.Total);

        // 0 金额：无副作用、不落条目。
        var count = economy.Ledger.Entries.Count;
        var zero = PaymentPrimitive.Pay(economy, LedgerCategory.LivingCost, Date, Money.Zero);

        Assert.Equal(PaymentResult.None, zero);
        Assert.Equal(count, economy.Ledger.Entries.Count);
        Assert.Equal(Guan(7m), loan.Principal);
    }

    [Fact]
    public void 任意金额手动提前还款封顶且零金额无副作用()
    {
        var loan = LoanOf(Guan(30m), Guan(2m));
        var economy = EconomyOf(loan, cash: Guan(100m), savings: Guan(50m));

        // 额度小于应划额：只冲本金（先本后息）。
        var small = economy.RepayLoan(Guan(10m), Date);

        Assert.Equal(Guan(10m), small.PrincipalPart);
        Assert.Equal(Money.Zero, small.InterestPart);
        Assert.Equal(Guan(20m), loan.Principal);
        Assert.Equal(Guan(2m), loan.AccruedInterest);

        // 额度大于应划额：结清时封顶 Total，不产生退款资金流。
        var rest = economy.RepayLoan(loan.Total, Date);

        Assert.Equal(Guan(20m), rest.PrincipalPart);
        Assert.Equal(Guan(2m), rest.InterestPart);
        Assert.True(loan.IsSettled);

        // 已结清：再多还一文都不接受。
        Assert.Throws<ArgumentOutOfRangeException>(() => economy.RepayLoan(Guan(1m), Date));

        // 0 金额：无副作用、不落条目。
        var count = economy.Ledger.Entries.Count;
        var zero = economy.RepayLoan(Money.Zero, Date);

        Assert.Equal(Money.Zero, zero.PrincipalPart);
        Assert.Equal(Money.Zero, zero.InterestPart);
        Assert.Equal(count, economy.Ledger.Entries.Count);

        // 超过现金 + 储蓄（可付额口径**不含**商本）：抛异常且不留条目、不出现负余额。
        var poorLoan = LoanOf(Guan(30m));
        var poor = EconomyOf(poorLoan, cash: Guan(1m), merchant: Guan(1000m));
        var poorCount = poor.Ledger.Entries.Count;

        Assert.Throws<InvalidOperationException>(() => poor.RepayLoan(Guan(5m), Date));
        Assert.Equal(poorCount, poor.Ledger.Entries.Count);
        Assert.Equal(Guan(30m), poorLoan.Principal);
        Assert.Equal(Guan(1m), poor.Treasury.Cash);
    }

    [Fact]
    public void 计息条目为事件类而划扣条目为资金类且池变动等于条目之和()
    {
        var loan = LoanOf(Guan(1m), Guan(0.02m), monthsSinceInterest: InterestPolicy.InterestPeriodMonths - 1);  // arch-guard:allow 夹具金额与随机取值（非规则数值副本）
        var result = Settle(NewState(loan, FamilyOf(1, 0), cash: Guan(1000m)));

        Assert.Equal(LedgerEntryKind.Event, LedgerCategoryMetadata.KindOf(LedgerCategory.LoanInterestAccrued));
        Assert.Equal(LedgerEntryKind.Treasury, LedgerCategoryMetadata.KindOf(LedgerCategory.LoanPrincipalRepaid));
        Assert.Equal(LedgerEntryKind.Treasury, LedgerCategoryMetadata.KindOf(LedgerCategory.LoanInterestRepaid));

        Assert.True(result.LoanInterestAccrued.IsPositive);
        Assert.True(result.LoanRepayment.PrincipalPart.IsPositive);

        // 资金类条目金额非 0（事件类的 0 元语义不在此列）。
        Assert.DoesNotContain(
            result.Entries,
            e => LedgerCategoryMetadata.KindOf(e.Category) == LedgerEntryKind.Treasury && e.Amount == Money.Zero);

        // SC-005：池变动 = 资金类条目之和（事件类的计息额被排除在外）。
        Assert.Equal(result.TreasuryPoolAfter - result.TreasuryPoolBefore, TreasuryDelta(result.Entries));
    }

    private static (SettlementResult Result, Money Repaid, Loan Loan) SettleWithLoan(Origin origin, bool hasShiStatus)
    {
        var loan = LoanOf(Guan(1_000_000m));
        var state = NewState(loan, FamilyOf(1, 0), cash: Guan(1000m), origin: origin, hasShiStatus: hasShiStatus);
        var result = Settle(state);

        return (result, result.LoanRepayment.PrincipalPart + result.LoanRepayment.InterestPart, loan);
    }

    private static Family FamilyOf(int adults, int children)
    {
        var members = new List<Person>();

        for (var index = 0; index < adults; index++)
        {
            members.Add(RulesHarness.Member(71 + index, Gender.Male, 40));
        }

        for (var index = 0; index < children; index++)
        {
            members.Add(RulesHarness.Member(81 + index, Gender.Male, 8));
        }

        return RulesHarness.FamilyOf(members.ToArray());
    }

    private static GameState NewState(
        Loan loan,
        Family family,
        Money cash = default,
        Origin origin = Origin.Artisan,
        bool hasShiStatus = false)
    {
        var economy = EconomyOf(loan, cash);
        family.HasShiStatus = hasShiStatus;

        return new GameState(StateId, Date, Difficulty.Normal, origin, family, economy);
    }

    private static FamilyEconomy EconomyOf(
        Loan loan, Money cash = default, Money savings = default, Money merchant = default)
    {
        var treasury = new Treasury(cash, savings, merchant) { Loan = loan };

        return new FamilyEconomy(
            LivingCostTable.InitialStandard, new GrainPriceIndex(GrainPricePolicy.Initial), treasury);
    }

    private static Loan LoanOf(Money principal, Money interest = default, int monthsSinceInterest = 0)
    {
        var loan = new Loan { Principal = principal, MonthsSinceInterest = monthsSinceInterest };

        // 欠息的唯一写入通道（AccruedInterest 只读，data-model §3.2）。
        loan.AccrueInterest(interest);

        return loan;
    }

    private static SettlementResult Settle(GameState state) =>
        new MonthlySettlementEngine(new FixedRandomService(0.5d), new GameStateClock(state)).Settle(state);  // arch-guard:allow 夹具金额与随机取值（非规则数值副本）

    private static Money TreasuryDelta(IReadOnlyList<LedgerEntry> entries)
    {
        var delta = Money.Zero;

        foreach (var entry in entries)
        {
            if (LedgerCategoryMetadata.KindOf(entry.Category) == LedgerEntryKind.Treasury)
            {
                delta += entry.Amount;
            }
        }

        return delta;
    }

    private static Money Guan(decimal value) => Money.FromGuan(value);
}
