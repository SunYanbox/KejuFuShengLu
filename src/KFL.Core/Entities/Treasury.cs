using KFL.Core.ValueObjects;

namespace KFL.Core.Entities;

/// <summary>
/// 家族资金（规格书 §5.4；data-model §3.1）：现金、储蓄、商本、单笔贷款、当年储蓄利率。
/// </summary>
/// <remarks>
/// <para>
/// **不变量**：<see cref="Cash"/>、<see cref="Savings"/>、<see cref="MerchantCapital"/> 皆 <c>&gt;= 0</c>；
/// <see cref="SavingsRate"/> 非空时 MUST 与 <see cref="SavingsRateYear"/> **同时非空**
/// （利率取值区间 0.5%~2.4% 的校验在 <c>KFL.Rules/Config/InterestPolicy</c>——区间是规格书数值，
/// Core 不复制，data-model §3.1）。
/// </para>
/// <para>
/// **三个金额池的写入通道只有两条**（contracts/ledger.md §3）：
/// ① 内部转账方法（现金 ↔ 储蓄、商本注入 / 撤回）——**不落条目、无手续费**（R-05：池的合计不变）；
/// ② 聚合 <c>FamilyEconomy</c> 的资金流动方法（<c>Apply</c> / <c>RepayLoan</c>）——**必落条目**。
/// 故三个属性对 <c>KFL.Core</c> 之外**不可写**（<c>internal</c> setter），
/// 「只动资金池而不落条目」在类型层面不可表达；初值只经构造函数给出。
/// </para>
/// <para>
/// **明确不含**：<c>现金 + 储蓄 + 商本</c> 的资金池口径（派生属性 <c>TreasuryPool</c> 属
/// <c>FamilyEconomy</c>，data-model §3.6），以及利率取值区间的校验。
/// </para>
/// </remarks>
public sealed class Treasury
{
    private Money _cash;
    private Money _savings;
    private Money _merchantCapital;
    private Loan _loan = new();
    private decimal? _savingsRate;
    private int? _savingsRateYear;

    /// <summary>构造并给出三个资金池的初值（资金流动仍 MUST 走两条写入通道）。</summary>
    /// <param name="cash">现金，<c>&gt;= 0</c>。</param>
    /// <param name="savings">储蓄，<c>&gt;= 0</c>。</param>
    /// <param name="merchantCapital">商本，<c>&gt;= 0</c>。</param>
    /// <exception cref="ArgumentOutOfRangeException">任一初值为负。</exception>
    public Treasury(Money cash = default, Money savings = default, Money merchantCapital = default)
    {
        Cash = cash;
        Savings = savings;
        MerchantCapital = merchantCapital;
    }

    /// <summary>现金，<c>&gt;= 0</c>。**写入通道受限于本类型的方法与 <c>KFL.Core</c> 内的聚合**。</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> 为负。</exception>
    public Money Cash
    {
        get => _cash;
        internal set
        {
            EnsureNonNegative(value, nameof(value), "现金");
            _cash = value;
        }
    }

    /// <summary>储蓄，<c>&gt;= 0</c>。储蓄利息并入此处（§5.4）。</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> 为负。</exception>
    public Money Savings
    {
        get => _savings;
        internal set
        {
            EnsureNonNegative(value, nameof(value), "储蓄");
            _savings = value;
        }
    }

    /// <summary>商本池，<c>&gt;= 0</c>（≥100 贯才产生经商收益，门槛在 Rules；§5.2）。</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> 为负。</exception>
    public Money MerchantCapital
    {
        get => _merchantCapital;
        internal set
        {
            EnsureNonNegative(value, nameof(value), "商本");
            _merchantCapital = value;
        }
    }

    /// <summary>单笔贷款（R-08：规格书只有「本金 + 欠息」单数结构）。</summary>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> 为 <c>null</c>。</exception>
    public Loan Loan
    {
        get => _loan;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _loan = value;
        }
    }

    /// <summary>
    /// 当年储蓄利率（1 月 roll 出后**当年不变**，§5.4；R-11）。
    /// </summary>
    /// <remarks>
    /// 赋值次序固定：先置 <see cref="SavingsRateYear"/>，再置本属性；清空则相反。
    /// 这样「非空 ⇔ 两者皆非空」的不变量在**任意时刻**都成立。
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="value"/> 非空而 <see cref="SavingsRateYear"/> 为空。</exception>
    public decimal? SavingsRate
    {
        get => _savingsRate;
        set
        {
            if (value is not null && _savingsRateYear is null)
            {
                throw new ArgumentException(
                    "SavingsRate 非空时 SavingsRateYear MUST 同时非空（先置年份，再置利率）。", nameof(value));
            }

            _savingsRate = value;
        }
    }

    /// <summary><see cref="SavingsRate"/> 所属年份（`null` = 尚未 roll 过）。</summary>
    /// <remarks>
    /// 清空本属性前 MUST 先清空 <see cref="SavingsRate"/>（否则会出现「有年份无利率」的中间态）。
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="value"/> 为空而 <see cref="SavingsRate"/> 非空。</exception>
    public int? SavingsRateYear
    {
        get => _savingsRateYear;
        set
        {
            if (value is null && _savingsRate is not null)
            {
                throw new ArgumentException(
                    "清空 SavingsRateYear 前 MUST 先清空 SavingsRate（data-model §3.1）。", nameof(value));
            }

            _savingsRateYear = value;
        }
    }

    /// <summary>现金 → 储蓄（内部转账：**不落条目、无手续费**，R-05）。</summary>
    /// <param name="amount">转账额，MUST <c>&gt;= 0</c> 且不超过现金。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> 为负。</exception>
    /// <exception cref="InvalidOperationException"><paramref name="amount"/> 超过现金（不允许隐式透支）。</exception>
    public void TransferToSavings(Money amount)
    {
        EnsureTransferable(amount, _cash, "现金");
        _cash -= amount;
        _savings += amount;
    }

    /// <summary>储蓄 → 现金（内部转账：**不落条目、无手续费**，R-05）。</summary>
    /// <param name="amount">转账额，MUST <c>&gt;= 0</c> 且不超过储蓄。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> 为负。</exception>
    /// <exception cref="InvalidOperationException"><paramref name="amount"/> 超过储蓄（不允许隐式透支）。</exception>
    public void TransferToCash(Money amount)
    {
        EnsureTransferable(amount, _savings, "储蓄");
        _savings -= amount;
        _cash += amount;
    }

    /// <summary>现金 → 商本（内部转账：**不落条目、无手续费**，R-05）。</summary>
    /// <param name="amount">注入额，MUST <c>&gt;= 0</c> 且不超过现金。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> 为负。</exception>
    /// <exception cref="InvalidOperationException"><paramref name="amount"/> 超过现金（不允许隐式透支）。</exception>
    public void InjectMerchantCapital(Money amount)
    {
        EnsureTransferable(amount, _cash, "现金");
        _cash -= amount;
        _merchantCapital += amount;
    }

    /// <summary>商本 → 现金（内部转账：**不落条目、无手续费**，R-05）。</summary>
    /// <param name="amount">撤回额，MUST <c>&gt;= 0</c> 且不超过商本。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> 为负。</exception>
    /// <exception cref="InvalidOperationException"><paramref name="amount"/> 超过商本（不允许隐式透支）。</exception>
    public void WithdrawMerchantCapital(Money amount)
    {
        EnsureTransferable(amount, _merchantCapital, "商本");
        _merchantCapital -= amount;
        _cash += amount;
    }

    private static void EnsureNonNegative(Money value, string paramName, string what)
    {
        if (value.IsNegative)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"{what} MUST >= 0（data-model §3.1）。");
        }
    }

    private static void EnsureTransferable(Money amount, Money source, string sourceName)
    {
        if (amount.IsNegative)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount), amount, "转账额 MUST >= 0（反向转账请调用对应的方法）。");
        }

        if (amount > source)
        {
            throw new InvalidOperationException(
                $"转账额 {amount.Guan} 贯超过{sourceName}的 {source.Guan} 贯（R-05：不允许隐式透支）。");
        }
    }
}
