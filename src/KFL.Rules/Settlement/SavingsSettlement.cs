using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;

namespace KFL.Rules.Settlement;

/// <summary>
/// 储蓄利率 roll 与计息的**纯函数**（规格书 §5.4；research R-11；契约三 §7）。
/// </summary>
/// <remarks>
/// <para>
/// **与 <c>LoanSettlement</c> 共用同一张利率表与同一个映射**（<see cref="InterestPolicy.RateFor"/>）：
/// 储蓄利率与贷款利率的区间同为 <c>0.5%~2.4%</c>，双方 MUST NOT 各自复制一份公式或字面量
/// （tasks T047；SC-008）。
/// </para>
/// <para>
/// **不碰任何状态**：「1 月 roll、当年不变、12 月计息并入本金」的时点属结算编排
/// （<c>MonthlySettlementEngine</c>），本类只算「利率是多少」与「利息是几贯」。
/// </para>
/// </remarks>
public static class SavingsSettlement
{
    /// <summary>
    /// roll 一次当年储蓄利率（区间 <c>0.5%~2.4%</c>，消费 1 次
    /// <see cref="IRandomService.NextDouble"/>）。
    /// </summary>
    /// <param name="randomService">随机来源接缝。</param>
    /// <returns>当年储蓄利率。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="randomService"/> 为 <c>null</c>。</exception>
    public static decimal RollRate(IRandomService randomService)
    {
        ArgumentNullException.ThrowIfNull(randomService);

        return InterestPolicy.RateFor(randomService.NextDouble());
    }

    /// <summary>
    /// 12 月末的储蓄利息 = <c>储蓄本金 × 当年利率</c>（**并入储蓄本金**，即复利）。
    /// </summary>
    /// <remarks>
    /// 返回值是**利息额**本身；并入本金由 <c>FamilyEconomy.Apply(SavingsInterest, …)</c> 完成
    /// （<c>LedgerCategoryMetadata</c> 把该类别登记为「正额入储蓄」）。
    /// 本方法 MUST NOT 乘难度收益系数——「储蓄利息除外」是 §5.2 明文（FR-012）。
    /// </remarks>
    /// <param name="savings">当年 12 月末的储蓄本金，MUST <c>&gt;= 0</c>。</param>
    /// <param name="rate">当年储蓄利率，MUST <c>&gt;= 0</c>。</param>
    /// <returns>本次利息额（储蓄本金为 0 时为 0）。</returns>
    /// <exception cref="ArgumentOutOfRangeException">储蓄本金或利率为负。</exception>
    public static Money Accrue(Money savings, decimal rate)
    {
        if (savings.IsNegative)
        {
            throw new ArgumentOutOfRangeException(
                nameof(savings), savings, "储蓄本金 MUST >= 0（data-model §3.1）。");
        }

        if (rate < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "储蓄利率 MUST >= 0（§5.4）。");
        }

        return savings * rate;
    }
}
