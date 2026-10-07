using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Core.Entities;

/// <summary>
/// 家族经济聚合（规格书 §5.1~§5.4；data-model §3.6）——**资金流动的唯一入口**。
/// </summary>
/// <remarks>
/// <para>
/// 本类型把资金池、流水账、资产、米价系数与饥馑状态收在**同一个聚合**之下，使
/// <see cref="Apply"/> / <see cref="RepayLoan"/> / <see cref="AccrueLoanInterest"/> /
/// <see cref="EnterFamineStage"/> 成为**唯一**能改动这些状态的产品路径
/// （contracts/ledger.md §3）。
/// </para>
/// <para>
/// **不变量**
/// </para>
/// <list type="number">
/// <item><description>
/// **资金池不为负**：任何 <see cref="Apply"/> / <see cref="RepayLoan"/> 结束后，
/// <c>Cash</c>、<c>Savings</c>、<c>MerchantCapital</c> 皆 <c>&gt;= 0</c>
/// （FR/US4「不能默默把钱扣成负数」）。
/// </description></item>
/// <item><description>
/// **资金流动必落条目**：资金池的任何变动都只经 <see cref="Apply"/> / <see cref="RepayLoan"/>，
/// 二者都在同一次调用里追加条目——「只动资金池而不落条目」在类型层面不可表达
/// （FR-021、SC-005）。
/// </description></item>
/// <item><description>
/// <see cref="PendingLivingStandard"/> 仅由「切换档位」的入口设置，结算第①步提升为
/// <see cref="LivingStandard"/> 后置 <c>null</c>（R-09）。
/// </description></item>
/// <item><description>
/// **当前档位由构造显式传入、不设默认值**：`KFL.Infrastructure` 看不到 `KFL.Rules`（G-05），
/// 新建存档的初值（<c>LivingCostTable.InitialStandard</c>）必须由调用方给出——在
/// `KFL.Core` 里抄一份「普通」当默认值会让同一个规则数值出现第二个出处（SC-008）。
/// 同理，<see cref="GrainPriceIndex"/> 的初值（<c>1.0</c>）单点在
/// <c>KFL.Rules/Config/GrainPricePolicy</c>，故它也是构造必需参数。
/// </description></item>
/// </list>
/// <para>
/// **明确不含**：年龄档归属（依赖 12/14/18/60 等规则数值，属 `KFL.Rules/Config/AgeBracketPolicy`，
/// R-06）、生活费与收入的任何系数、饥馑阈值与救济折扣（属 Rules）——本类型只承载**状态**与
/// **状态转移的写入通道**，不承载任何平衡数值（data-model §3.6 的「不在此处」）。
/// </para>
/// </remarks>
public sealed class FamilyEconomy
{
    /// <summary>构造并给出经济状态的初值。</summary>
    /// <param name="livingStandard">
    /// 当前**生效**的生活费档位。**构造必需、无默认值**：新建存档的初值单点在
    /// <c>KFL.Rules/Config/LivingCostTable.InitialStandard</c>（G-05 使 Infrastructure 看不到 Rules）。
    /// </param>
    /// <param name="grainPriceIndex">
    /// 米价系数初值。**构造必需**：初值 <c>1.0</c> 单点在 <c>KFL.Rules/Config/GrainPricePolicy</c>。
    /// </param>
    /// <param name="treasury">资金池；<c>null</c> = 零资金的空池。</param>
    /// <param name="holdings">资产组合；<c>null</c> = 零资产。</param>
    /// <param name="ledger">流水账；<c>null</c> = 空账。</param>
    public FamilyEconomy(
        LivingStandard livingStandard,
        GrainPriceIndex grainPriceIndex,
        Treasury? treasury = null,
        Holdings? holdings = null,
        Ledger? ledger = null)
    {
        LivingStandard = livingStandard;
        GrainPriceIndex = grainPriceIndex;
        Treasury = treasury ?? new Treasury();
        Holdings = holdings ?? new Holdings();
        Ledger = ledger ?? new Ledger();
    }

    /// <summary>家族资金（现金 / 储蓄 / 商本 / 贷款 / 当年储蓄利率）。</summary>
    public Treasury Treasury { get; }

    /// <summary>资产组合（田宅铺数量）。</summary>
    public Holdings Holdings { get; }

    /// <summary>家族级追加式流水账（资金流动的唯一留痕处）。</summary>
    public Ledger Ledger { get; }

    /// <summary>
    /// 米价系数（可变结构体属性）。游走与 clamp 由结算第②步按
    /// <c>KFL.Rules/Config/GrainPricePolicy</c> 执行，本类型不解释其数值。
    /// </summary>
    public GrainPriceIndex GrainPriceIndex { get; set; }

    /// <summary>
    /// 当前**生效**的生活费档位。
    /// </summary>
    /// <remarks>
    /// 写入通道只有两个：① 构造（新建存档的初值，来自 <c>LivingCostTable.InitialStandard</c>）；
    /// ② 结算第①步把 <see cref="PendingLivingStandard"/> 提升上来（R-09）。
    /// **切换档位的入口 MUST 写 <see cref="PendingLivingStandard"/>**——直接赋值会绕过
    /// §5.1 的「次月生效」。
    /// </remarks>
    public LivingStandard LivingStandard { get; set; }

    /// <summary>
    /// 待生效的生活费档位；<c>null</c> = 无待生效切换。
    /// </summary>
    /// <remarks>
    /// 「家族级随时切换、次月生效」的落点（§5.1；R-09）：切换入口只写本属性，
    /// 结算第①步提升为 <see cref="LivingStandard"/> 并置回 <c>null</c>。
    /// </remarks>
    public LivingStandard? PendingLivingStandard { get; set; }

    /// <summary>
    /// 饥馑状态。**阶段迁移的判定**由 <c>FamineController</c>（Rules）执行，
    /// 本聚合只提供状态归属与 <see cref="EnterFamineStage"/> 的落条目通道
    /// （阈值 3/12 月与救济折扣 20% 在 <c>KFL.Rules/Config/FamineTimeline</c>）。
    /// </summary>
    public FamineState Famine { get; } = new();

    /// <summary>
    /// 资金池 = 现金 + 储蓄 + 商本（R-05；SC-005 的右侧口径）。
    /// **贷款是负债，不在池内**。
    /// </summary>
    public Money TreasuryPool => Treasury.Cash + Treasury.Savings + Treasury.MerchantCapital;

    /// <summary>
    /// **附录条目并在同一次调用内改动资金池**（contracts/ledger.md §3）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 分支严格按 <see cref="LedgerCategoryMetadata"/> 的结构事实：
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// **正额**：入 <see cref="LedgerCategoryMetadata.CreditTargetOf"/> 指定的池（现金或储蓄）；
    /// 该类别没有正额入账目标（恒为负的支出类，或事件类）时 **MUST 抛异常**。
    /// </description></item>
    /// <item><description>
    /// **负额**：按「现金 → 储蓄」扣付；**资金不足 MUST 抛异常**——E-06 要求调用方
    /// （生活费的部分支付、贷款的划扣封顶）先算出**可付额**，本方法不做部分支付。
    /// </description></item>
    /// <item><description>
    /// **事件类**（<see cref="LedgerEntryKind.Event"/>）：只追加条目，**MUST NOT** 动资金池
    /// （饥馑阶段迁移、贷款计息入欠息）。
    /// </description></item>
    /// <item><description>
    /// 资金类条目的 <c>Amount == 0</c> 在 <see cref="LedgerEntry"/> 构造时即被拒
    /// （`KFL.Core/ValueObjects/LedgerEntry.cs`）。
    /// </description></item>
    /// </list>
    /// <para>
    /// **失败原子性**：全部校验都在追加条目**之前**完成，故异常路径既不动资金池、
    /// 也不留条目——不存在「有变动无条目」或「有条目无变动」的中间态（不变量 2）。
    /// </para>
    /// </remarks>
    /// <param name="category">账本类别。</param>
    /// <param name="personId">归属角色；<c>null</c> = 家族级。</param>
    /// <param name="amount">变动额：正 = 流入，负 = 流出。</param>
    /// <param name="date">归属年月。</param>
    /// <returns>本次追加的条目（便于调用方汇总 <c>SettlementResult.Entries</c>）。</returns>
    /// <exception cref="ArgumentException"><paramref name="category"/> 为资金类而 <paramref name="amount"/> 为 0。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="category"/> 不在已登记取值内。</exception>
    /// <exception cref="InvalidOperationException">
    /// 正额而该类别无入账目标；或负额而现金 + 储蓄不足（E-06）。
    /// </exception>
    public LedgerEntry Apply(LedgerCategory category, PersonId? personId, Money amount, GameDate date)
    {
        var entry = new LedgerEntry(date, personId, category, amount);
        var kind = LedgerCategoryMetadata.KindOf(category);

        LedgerCreditTarget? creditTarget = null;

        if (kind == LedgerEntryKind.Treasury)
        {
            if (amount.IsPositive)
            {
                creditTarget = LedgerCategoryMetadata.CreditTargetOf(category)
                    ?? throw new InvalidOperationException(
                        $"类别 {category} 恒为负或为事件类，MUST NOT 以正额入账（data-model §2.1）。");
            }
            else
            {
                var need = -amount;
                var payable = Treasury.Cash + Treasury.Savings;

                if (need > payable)
                {
                    throw new InvalidOperationException(
                        $"资金不足：需 {need.Guan} 贯，现金 + 储蓄仅 {payable.Guan} 贯"
                        + "（E-06：调用方 MUST 先算可付额，本方法不做部分支付）。");
                }
            }
        }

        // 先落条目、后动池：两者都在本调用内完成，且经上一步校验后都不可能再失败。
        Ledger.Append(entry);

        if (kind == LedgerEntryKind.Treasury)
        {
            if (creditTarget is not null)
            {
                Credit(creditTarget.Value, amount);
            }
            else
            {
                PayFromCashThenSavings(-amount);
            }
        }

        return entry;
    }

    /// <summary>
    /// 偿还贷款：扣付资金池 + <see cref="Loan.Repay"/>（**先本后息**）+ 落 1~2 条**资金类**条目
    /// （data-model §3.6、§6.2）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// **金额为 0 的部分不落条目**（先本后息后只剩欠息时，只落 <see cref="LedgerCategory.LoanInterestRepaid"/>）。
    /// 两条条目都经 <see cref="Apply"/> 写入，故「扣付」与「留痕」天然同源。
    /// </para>
    /// <para>
    /// **封顶由调用方按 R-08 计算**（<c>min(净利润 × 比例, 本金 + 欠息)</c>）：
    /// 本方法只额外保证「不超出资金池」，超出时抛异常（与 <see cref="Apply"/> 同一口径）。
    /// </para>
    /// </remarks>
    /// <param name="amount">还款额，MUST <c>&gt;= 0</c> 且 <c>&lt;= 本金 + 欠息</c>，且不超过现金 + 储蓄。</param>
    /// <param name="date">归属年月。</param>
    /// <returns>本次**先本后息**的拆分。</returns>
    /// <exception cref="ArgumentOutOfRangeException">还款额为负，或大于还款前的本金 + 欠息。</exception>
    /// <exception cref="InvalidOperationException">还款额超过现金 + 储蓄（资金不足）。</exception>
    public LoanRepayment RepayLoan(Money amount, GameDate date)
    {
        if (amount.IsNegative)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount), amount, "还款额 MUST >= 0（data-model §3.6）。");
        }

        var payable = Treasury.Cash + Treasury.Savings;
        if (amount > payable)
        {
            throw new InvalidOperationException(
                $"还款额 {amount.Guan} 贯超过现金 + 储蓄的 {payable.Guan} 贯"
                + "（资金不足时由 LoanSettlement 按 R-08 封顶）。");
        }

        // 先过 Loan.Repay 的封顶校验（amount <= 本金 + 欠息）：失败时尚未动任何池或条目。
        var repayment = Treasury.Loan.Repay(amount);

        // 先本后息：两段各落一条，金额为 0 的一段不落条目。
        if (repayment.PrincipalPart.IsPositive)
        {
            Apply(LedgerCategory.LoanPrincipalRepaid, null, -repayment.PrincipalPart, date);
        }

        if (repayment.InterestPart.IsPositive)
        {
            Apply(LedgerCategory.LoanInterestRepaid, null, -repayment.InterestPart, date);
        }

        return repayment;
    }

    /// <summary>
    /// 把一笔利息累入**欠息**并落**一条**事件类条目 <see cref="LedgerCategory.LoanInterestAccrued"/>
    /// （§5.4「利息永不滚入本金」；E-05）。
    /// </summary>
    /// <remarks>
    /// **MUST NOT 触碰资金池**：计息只增加负债（事件类条目不参与 SC-005 的求和口径）。
    /// 计数归零由调用方在计息后执行（<see cref="Loan.ResetInterestClock"/>）——「是否到达 12 月节点」
    /// 属 <c>LoanSettlement.IsInterestDue</c>（Rules）。
    /// </remarks>
    /// <param name="interest">本次计息额，MUST <c>&gt;= 0</c>（本金为 0 时为 0）。</param>
    /// <param name="date">归属年月。</param>
    /// <returns><paramref name="interest"/>（本次累入欠息的金额，供 <c>SettlementResult.LoanInterestAccrued</c> 使用）。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interest"/> 为负。</exception>
    public Money AccrueLoanInterest(Money interest, GameDate date)
    {
        // 校验先于任何写入：负额在 Loan.AccrueInterest 内被拒，且不留条目。
        Treasury.Loan.AccrueInterest(interest);

        Apply(LedgerCategory.LoanInterestAccrued, null, interest, date);

        return interest;
    }

    /// <summary>
    /// 落**一条**饥馑阶段迁移的**事件类**条目（data-model §3.6、§6.1；spec US4 AS1）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 阶段 → 类别的对应：<see cref="FamineStage.None"/> = <see cref="LedgerCategory.FamineResolved"/>、
    /// <see cref="FamineStage.Famine"/> = <see cref="LedgerCategory.FamineEntered"/>、
    /// <see cref="FamineStage.Relief"/> = <see cref="LedgerCategory.FamineReliefEntered"/>、
    /// <see cref="FamineStage.Severe"/> = <see cref="LedgerCategory.FamineSevereEntered"/>。
    /// </para>
    /// <para>
    /// **本方法只落条目、不改 <see cref="Famine"/>**：阶段迁移的判定（含「先解除、后升级」与
    /// 3/12 月阈值）属 <c>FamineController</c>（Rules），它负责调用
    /// <see cref="FamineState.TransitionTo"/> / <see cref="FamineState.Clear"/>；
    /// 在此重复迁移会让「每月至多迁移一次」失效（契约三 §8 条款 4）。
    /// </para>
    /// </remarks>
    /// <param name="stage">迁移到的阶段（<see cref="FamineStage.None"/> = 全部解除）。</param>
    /// <param name="amount">该事件的金额语义；阶段迁移按契约一律传 0（contracts/ledger.md §1）。</param>
    /// <param name="date">归属年月。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stage"/> 不在四阶段之内。</exception>
    public void EnterFamineStage(FamineStage stage, Money amount, GameDate date)
    {
        var category = stage switch
        {
            FamineStage.None => LedgerCategory.FamineResolved,
            FamineStage.Famine => LedgerCategory.FamineEntered,
            FamineStage.Relief => LedgerCategory.FamineReliefEntered,
            FamineStage.Severe => LedgerCategory.FamineSevereEntered,
            _ => throw new ArgumentOutOfRangeException(
                nameof(stage), stage, "未登记的饥馑阶段（规格书 §5.4 共四种）。"),
        };

        Apply(category, null, amount, date);
    }

    private void Credit(LedgerCreditTarget target, Money amount)
    {
        switch (target)
        {
            case LedgerCreditTarget.Cash:
                Treasury.Cash += amount;
                break;
            case LedgerCreditTarget.Savings:
                Treasury.Savings += amount;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(target), target, "未登记的正额入账目标（data-model §2.1）。");
        }
    }

    /// <summary>按「现金 → 储蓄」扣付（调用方已保证 <paramref name="amount"/> 不超过两者之和）。</summary>
    private void PayFromCashThenSavings(Money amount)
    {
        var fromCash = amount < Treasury.Cash ? amount : Treasury.Cash;
        Treasury.Cash -= fromCash;
        Treasury.Savings -= amount - fromCash;
    }
}
