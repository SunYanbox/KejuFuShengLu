using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;

namespace KFL.Rules.Settlement;

/// <summary>
/// 月度结算引擎（规格书 §3、§5.1~§5.4；data-model §4.3；契约三 §1）。
/// </summary>
/// <remarks>
/// <para>
/// **唯一的编排入口**（FR-020）：按契约三 §1 的六步顺序执行一次，**就地**推进传入的
/// <see cref="GameState"/> 与 <see cref="GameState.CurrentDate"/>，并返回可断言的快照。
/// </para>
/// <list type="number">
/// <item><description>提升待生效的难度 / 生活费档位并清空待生效位（R-09）。</description></item>
/// <item><description>米价系数游走并 clamp（消耗 1 次 <see cref="IRandomService.NextDouble"/>，当月生效）。</description></item>
/// <item><description>收入：1 月 roll 当年储蓄利率、12 月计息并入本金 + 工出身 bonus，再按 §6 各来源入账。</description></item>
/// <item><description>生活费：足额或部分支付 + 饥馑四阶段状态机（先解除、后升级；R-12）。</description></item>
/// <item><description>贷款：**先计息、后划扣**（E-05；US2 已接入）。</description></item>
/// <item><description><see cref="GameState.AdvanceMonth"/> 并返回快照。</description></item>
/// </list>
/// <para>
/// **MUST NOT 存在**的步骤（各属其他阶段）：随机事件、属性成长 / 衰老 / 疾病 / 死亡判定、
/// 科举季触发、绝嗣判定、存档写入（契约三 §1 的「MUST NOT 存在」清单）。
/// </para>
/// <para>
/// 随机与「现在」都只能来自构造函数注入的接缝（章程原则 II、契约二）：
/// MUST NOT 读系统时钟、全局随机源或文件系统。
/// </para>
/// </remarks>
public sealed class MonthlySettlementEngine
{
    /// <summary>年度储蓄利率的 roll 月份（每年 1 月，契约三 §7；与上年取值无关）。</summary>
    private const int SavingsRollMonth = 1;

    /// <summary>储蓄计息与工出身 bonus 的结算月份（每年 12 月末，契约三 §7）。</summary>
    private const int SavingsAccrualMonth = 12;

    private readonly IRandomService _randomService;
    private readonly IGameClock _clock;

    /// <summary>构造并注入两组接缝。</summary>
    /// <param name="randomService">随机来源（米价 / 储蓄利率 / 贷款计息利率）。</param>
    /// <param name="clock">游戏时间来源（结算对象是它给出的当月）。</param>
    /// <exception cref="ArgumentNullException">任一参数为 <c>null</c>。</exception>
    public MonthlySettlementEngine(IRandomService randomService, IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(randomService);
        ArgumentNullException.ThrowIfNull(clock);

        _randomService = randomService;
        _clock = clock;
    }

    /// <summary>结算当月账目、就地推进一个月，并返回本次结算的可断言快照。</summary>
    /// <param name="state">存档级状态（**就地**被推进）。</param>
    /// <returns>本次结算的快照。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 <c>null</c>。</exception>
    public SettlementResult Settle(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var economy = state.Economy;
        var month = _clock.Current;
        var firstEntryIndex = economy.Ledger.Entries.Count;
        var poolBefore = economy.TreasuryPool;
        var grainBefore = economy.GrainPriceIndex.Value;
        var famineBefore = new FamineState(economy.Famine);

        PromotePendingValues(state, economy);

        // ② 米价系数游走并 clamp（当月生效）。
        var grainAfter = GrainPricePolicy.Walk(grainBefore, _randomService.NextDouble());
        economy.GrainPriceIndex = new GrainPriceIndex(grainAfter);

        // ③ 收入：先处理年度项（1 月的储蓄利率 roll），再按 §6 各来源入账。
        //    随机消费次序在此被钉住：米价（②）→ 储蓄利率（仅 1 月，③）→ 贷款计息利率（⑤）。
        if (month.Month == SavingsRollMonth)
        {
            var rolledRate = SavingsSettlement.RollRate(_randomService);

            // 赋值次序固定：先置年份、再置利率（Treasury 的交叉校验，data-model §3.1）。
            economy.Treasury.SavingsRateYear = month.Year;
            economy.Treasury.SavingsRate = rolledRate;
        }

        var income = IncomeCalculator.Compute(
            state.Family, month, economy.Holdings, economy.Treasury, state.Difficulty, state.Origin);

        var incomeLines = new List<IncomeLine>(income.Lines);

        foreach (var line in income.Lines)
        {
            economy.Apply(line.Category, line.PersonId, line.Amount, month);
        }

        // 本月的年度收入合计（储蓄利息 + 工出身 bonus），计入净利润但不乘同一套系数。
        var annualIncome = Money.Zero;

        // ③-a 12 月末：利息 = 当时储蓄本金 × 当年利率，**并入储蓄本金**（复利）。
        //      **MUST NOT** 乘难度收益系数（FR-012「储蓄利息除外」）。
        var savingsInterest = Money.Zero;

        if (month.Month == SavingsAccrualMonth && economy.Treasury.SavingsRate is { } savingsRate)
        {
            savingsInterest = SavingsSettlement.Accrue(economy.Treasury.Savings, savingsRate);

            if (savingsInterest.IsPositive)
            {
                economy.Apply(LedgerCategory.SavingsInterest, null, savingsInterest, month);
                incomeLines.Add(new IncomeLine(LedgerCategory.SavingsInterest, null, savingsInterest));
                annualIncome += savingsInterest;
            }
        }

        // ③-b 12 月末工出身 bonus：基数 = 现金 + 储蓄 + 田宅铺市值 − 本金 − 欠息（**不含商本池**），
        //      为**正**才发，且**乘**难度收益系数（§5.2「储蓄利息除外」的反面）。
        var artisanBonus = Money.Zero;

        if (month.Month == SavingsAccrualMonth && state.Origin == Origin.Artisan)
        {
            var totalAssets = (economy.Treasury.Cash + economy.Treasury.Savings
                    + AssetPriceTable.MarketValue(economy.Holdings))
                - economy.Treasury.Loan.Principal - economy.Treasury.Loan.AccruedInterest;

            if (totalAssets.IsPositive)
            {
                artisanBonus = totalAssets
                    * IncomeRateTable.ArtisanBonusRate
                    * DifficultyRates.RevenueFactor(state.Difficulty);

                economy.Apply(LedgerCategory.ArtisanBonus, null, artisanBonus, month);
                incomeLines.Add(new IncomeLine(LedgerCategory.ArtisanBonus, null, artisanBonus));
                annualIncome += artisanBonus;
            }
        }

        // ④ 生活费 + 饥馑状态机：应付额按**月初**阶段算（转入当月的应付额已按饥馑阶段算出），
        //    再交 FamineController 判定「先解除、后升级」（Tick 在足额判定之前，E-14）。
        //    可付额 = 现金 + 储蓄，MUST NOT 含商本（R-05）。
        var counted = CountedMembers.Counted(state.Family);
        var livingCost = LivingCostCalculator.Compute(
            counted,
            month,
            economy.LivingStandard,
            grainAfter,
            state.Difficulty,
            state.Origin,
            economy.Famine.Stage);

        var payable = livingCost.Payable;
        var payablePool = economy.Treasury.Cash + economy.Treasury.Savings;
        var decision = FamineController.Evaluate(economy.Famine, payable, payablePool);
        var paid = decision.Paid;

        if (paid.IsPositive)
        {
            economy.Apply(LedgerCategory.LivingCost, null, -paid, month);
        }

        if (decision.Transition is { } transition)
        {
            // 阶段迁移一律落一条**事件类**条目（金额语义为 0；不参与 SC-005 的求和）。
            economy.EnterFamineStage(transition, Money.Zero, month);
        }

        // 净利润 = 本月全部收入（**含储蓄利息与工 bonus**） − 本月**实付**生活费
        // （E-04 与契约三 §7 的口径；不含划扣本身、不含资产买卖的现金流）。
        var netProfit = income.Total + annualIncome - paid;

        // ⑤ 贷款：**先计息、后划扣**（E-05）。
        var (interestAccrued, loanRepayment) = SettleLoan(economy, state, month, netProfit);

        var famineAfter = new FamineState(economy.Famine);

        // ⑥ 推进时间并返回快照。
        state.AdvanceMonth();

        return new SettlementResult
        {
            Month = month,
            GrainPriceIndexBefore = grainBefore,
            GrainPriceIndexAfter = grainAfter,
            LivingCosts = livingCost.Lines,
            LivingCostPayable = payable,
            LivingCostPaid = paid,
            Incomes = incomeLines,
            NetProfit = netProfit,
            LoanInterestAccrued = interestAccrued,
            LoanRepayment = loanRepayment,
            SavingsInterest = savingsInterest,
            ArtisanBonus = artisanBonus,
            FamineBefore = famineBefore,
            FamineAfter = famineAfter,
            Entries = Slice(economy.Ledger.Entries, firstEntryIndex),
            TreasuryPoolBefore = poolBefore,
            TreasuryPoolAfter = economy.TreasuryPool,
        };
    }

    /// <summary>第①步：把待生效的难度与生活费档位提升为生效值并清空待生效位（R-09）。</summary>
    private static void PromotePendingValues(GameState state, FamilyEconomy economy)
    {
        if (state.PendingDifficulty is { } pendingDifficulty)
        {
            state.Difficulty = pendingDifficulty;
            state.PendingDifficulty = null;
        }

        if (economy.PendingLivingStandard is { } pendingStandard)
        {
            economy.LivingStandard = pendingStandard;
            economy.PendingLivingStandard = null;
        }
    }

    /// <summary>
    /// 第⑤步：**先计息、后划扣**（E-05；契约三 §7）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// **计时按自然月推进**：先 +1、再比阈值（与 E-14 的饥馑计时同口径），故新建贷款在
    /// **第 12 个结算月当月**计息、第 13 个月不重复；阈值只看 <c>MonthsSinceInterest</c>，
    /// 与本月净利润无关（E-07）。
    /// </para>
    /// <para>
    /// **已结清**（本金与欠息皆 0）的贷款不进入本步：无债可计、无可划扣，也不产生 0 元噪声条目。
    /// 「不命中」仍由贷款自身状态决定而非随机，故不改变随机消费次数（契约三 §4 条款 2）。
    /// </para>
    /// </remarks>
    /// <param name="economy">家族经济聚合（计息与划扣的写入通道）。</param>
    /// <param name="state">存档级状态（取「仕」身份与出身以定划扣比例）。</param>
    /// <param name="month">归属年月。</param>
    /// <param name="netProfit">本月净利润（划扣基数）。</param>
    /// <returns>本月计息额与划扣的先本后息拆分。</returns>
    private (Money InterestAccrued, LoanRepayment Repayment) SettleLoan(
        FamilyEconomy economy, GameState state, GameDate month, Money netProfit)
    {
        var loan = economy.Treasury.Loan;

        if (loan.IsSettled)
        {
            return (Money.Zero, new LoanRepayment(Money.Zero, Money.Zero));
        }

        loan.MonthsSinceInterest += 1;

        var interestAccrued = Money.Zero;

        // ⓐ 先计息：本金取**月初结余**（尚未被本月划扣冲减，E-05）；利息只入欠息，本金不增。
        if (LoanSettlement.IsInterestDue(loan))
        {
            var rate = LoanSettlement.RollRate(_randomService);
            interestAccrued = economy.AccrueLoanInterest(loan.Principal * rate, month);
            loan.ResetInterestClock();
        }

        // ⓑ 后划扣：净利润 ≤ 0 → 不划扣、不罚、计时照常（E-07）；0 金额不落条目。
        var due = LoanSettlement.ComputeRepayment(netProfit, state.Family.HasShiStatus, state.Origin, loan);

        if (!due.IsPositive)
        {
            return (interestAccrued, new LoanRepayment(Money.Zero, Money.Zero));
        }

        return (interestAccrued, economy.RepayLoan(due, month));
    }

    /// <summary>取本次结算向账本追加的全部条目（含事件类），与 <c>Ledger</c> 逐条相同。</summary>
    private static List<LedgerEntry> Slice(IReadOnlyList<LedgerEntry> entries, int firstIndex)
    {

        var appended = new List<LedgerEntry>(entries.Count - firstIndex);

        for (var index = firstIndex; index < entries.Count; index++)
        {
            appended.Add(entries[index]);
        }

        return appended;
    }
}
