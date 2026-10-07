using KFL.Core.ValueObjects;

namespace KFL.Core.Entities;

/// <summary>
/// 单笔贷款（规格书 §5.4；research R-08）：本金与**独立**的「欠息」字段。
/// </summary>
/// <remarks>
/// <para>
/// **不变量**：<see cref="Principal"/>、<see cref="AccruedInterest"/> 皆 <c>&gt;= 0</c>；
/// <see cref="MonthsSinceInterest"/> <c>&gt;= 0</c>。
/// </para>
/// <para>
/// **利息永不滚入本金**（§5.4）：<see cref="AccrueInterest"/> 只增 <see cref="AccruedInterest"/>，
/// MUST NOT 改 <see cref="Principal"/>。
/// </para>
/// <para>
/// <see cref="Principal"/> 有公开 setter，因为本阶段它**唯一**的产生路径在聚合之外：
/// <c>PaymentPrimitive.Pay</c> 在「现金 + 储蓄不足」时把差额计入本金（data-model §3.2 的注、FR-017）；
/// 罚金触发入口属阶段⑥，本阶段 MUST NOT 实现。
/// </para>
/// <para>
/// **明确不含**：划扣比例（仕 20% / 工农 40% / 商 80%）、计息周期 12 月、利率区间 0.5%~2.4%——
/// 全部属 <c>KFL.Rules/Config</c>（<c>LoanPolicy</c> / <c>InterestPolicy</c>）；
/// 「是否到达计息节点」由 <c>LoanSettlement.IsInterestDue</c> 按 <see cref="MonthsSinceInterest"/> 判定。
/// </para>
/// </remarks>
public sealed class Loan
{
    private Money _principal;
    private Money _accruedInterest;
    private int _monthsSinceInterest;

    /// <summary>本金，<c>&gt;= 0</c>。</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> 为负。</exception>
    public Money Principal
    {
        get => _principal;
        set
        {
            EnsureNonNegative(value, nameof(value), "本金");
            _principal = value;
        }
    }

    /// <summary>欠息，<c>&gt;= 0</c>。**独立字段**：利息 MUST NOT 滚入本金（§5.4）。</summary>
    public Money AccruedInterest => _accruedInterest;

    /// <summary>
    /// 距上次计息的**自然月**数，<c>&gt;= 0</c>（E-07）。
    /// </summary>
    /// <remarks>
    /// 计时按自然月推进，**与当月净利润是否为负无关**：每个结算月 +1（R-08、契约三 §7），
    /// 由结算编排推进；<see cref="ResetInterestClock"/> 在计息后归零。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> 为负。</exception>
    public int MonthsSinceInterest
    {
        get => _monthsSinceInterest;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), value, "距上次计息的月数 MUST >= 0（data-model §3.2）。");
            }

            _monthsSinceInterest = value;
        }
    }

    /// <summary>是否已结清：本金与欠息**皆为 0**（§5.4）。</summary>
    public bool IsSettled => _principal == Money.Zero && _accruedInterest == Money.Zero;

    /// <summary>债务总额 = 本金 + 欠息（§5.4）。</summary>
    public Money Total => _principal + _accruedInterest;

    /// <summary>
    /// 把一笔利息累入**欠息**（§5.4「利息永不滚入本金」）。
    /// </summary>
    /// <param name="interest">本次计息额，MUST <c>&gt;= 0</c>。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interest"/> 为负。</exception>
    public void AccrueInterest(Money interest)
    {
        EnsureNonNegative(interest, nameof(interest), "计息额");
        _accruedInterest += interest;
    }

    /// <summary>把距上次计息的月数归零（计息后调用，§5.4）。</summary>
    public void ResetInterestClock() => _monthsSinceInterest = 0;

    /// <summary>
    /// 还款并返回**先本后息**的拆分（§5.4）。
    /// </summary>
    /// <remarks>
    /// 封顶由调用方按 R-08 计算（<c>min(净利润 × 比例, 本金 + 欠息)</c>）：不封顶会在债务只剩
    /// 0.1 贯时划走全部应划额并产生规格书没有的「退款」资金流。
    /// </remarks>
    /// <param name="amount">还款额，MUST <c>&gt;= 0</c> 且 <c>&lt;= <see cref="Total"/></c>。</param>
    /// <returns>本次冲减的本金段与欠息段。</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="amount"/> 为负，或大于还款前的 <see cref="Total"/>。
    /// </exception>
    public LoanRepayment Repay(Money amount)
    {
        if (amount.IsNegative)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "还款额 MUST >= 0（data-model §3.2）。");
        }

        if (amount > Total)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount), amount, "还款额 MUST <= 本金 + 欠息（封顶由调用方按 R-08 计算）。");
        }

        // 先本后息：本金优先冲减，不足部分再冲欠息。
        var principalPart = amount < _principal ? amount : _principal;
        var interestPart = amount - principalPart;

        _principal -= principalPart;
        _accruedInterest -= interestPart;

        return new LoanRepayment(principalPart, interestPart);
    }

    private static void EnsureNonNegative(Money value, string paramName, string what)
    {
        if (value.IsNegative)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"{what} MUST >= 0（data-model §3.2）。");
        }
    }
}

/// <summary>
/// 一次还款的**先本后息**拆分（data-model §3.2）。
/// </summary>
/// <remarks>
/// **不变量**：两部分皆 <c>&gt;= 0</c>；「不超过还款前的本金 / 欠息」由
/// <see cref="Loan.Repay"/> 的封顶逻辑保证（本类型无法单独校验后一条）。
/// </remarks>
public readonly record struct LoanRepayment
{
    /// <summary>构造并校验。</summary>
    /// <param name="principalPart">冲减的本金，MUST <c>&gt;= 0</c>。</param>
    /// <param name="interestPart">冲减的欠息，MUST <c>&gt;= 0</c>。</param>
    /// <exception cref="ArgumentOutOfRangeException">任一部分为负。</exception>
    public LoanRepayment(Money principalPart, Money interestPart)
    {
        if (principalPart.IsNegative)
        {
            throw new ArgumentOutOfRangeException(
                nameof(principalPart), principalPart, "本金拆分 MUST >= 0（data-model §3.2）。");
        }

        if (interestPart.IsNegative)
        {
            throw new ArgumentOutOfRangeException(
                nameof(interestPart), interestPart, "欠息拆分 MUST >= 0（data-model §3.2）。");
        }

        PrincipalPart = principalPart;
        InterestPart = interestPart;
    }

    /// <summary>本次冲减的本金。</summary>
    public Money PrincipalPart { get; }

    /// <summary>本次冲减的欠息。</summary>
    public Money InterestPart { get; }
}
