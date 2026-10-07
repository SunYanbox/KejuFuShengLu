using KFL.Core.Entities;
using KFL.Core.Enums;

namespace KFL.Rules.Config;

/// <summary>
/// 饥馑时间线与救济折扣（规格书 §5.4；research R-12；contracts/monthly-settlement.md §8）。
/// </summary>
/// <remarks>
/// <para>
/// 本类是「饥馑满 <b>3</b> 个月转救济」「救济期 <b>12</b> 个月」「救济期支出折扣 <b>20%</b>」
/// 三个数值的**唯一出处**（SC-008）：<c>LivingCostTable</c> 的救济期修正项、
/// <c>FamineController</c> 的阈值比较与测试的期望值都必须经本类成员取得。
/// </para>
/// <para>
/// **本类只存时限常量，不判阈值**（tasks T050）：阈值比较（`≥`）与「至多迁移一次」的次序属
/// <c>FamineController.Evaluate</c>；阶段与计时的状态属 <c>KFL.Core</c> 的 <see cref="FamineState"/>。
/// </para>
/// <para>
/// **阈值语义（E-14）**：转入当月记 1、判定前先 <see cref="FamineState.Tick"/>，故「满 3 月」=
/// **第 3 个饥馑月当月**、「满 12 月」= **第 12 个救济月当月**。
/// </para>
/// </remarks>
public static class FamineTimeline
{
    /// <summary>饥馑满 3 个月转救济（§5.4）。</summary>
    public const int MonthsUntilRelief = 3;

    /// <summary>救济期 12 个月，期满仍付不起正常档支出则转第三阶段（§5.4）。</summary>
    public const int ReliefMonthsUntilSevere = 12;

    /// <summary>救济期全体支出折扣 20%（§5.4；进生活费**一般乘区**，加算）。</summary>
    public const decimal ReliefExpenseDiscount = 0.20m;

    /// <summary>取某阶段的时限月数；<see cref="FamineStage.None"/> 与第三阶段无时限（`null`）。</summary>
    /// <param name="stage">饥馑阶段。</param>
    /// <returns>该阶段的时限月数；无时限时为 <c>null</c>。</returns>
    public static int? LimitMonths(FamineStage stage) => stage switch
    {
        FamineStage.Famine => MonthsUntilRelief,
        FamineStage.Relief => ReliefMonthsUntilSevere,
        _ => null,
    };

    /// <summary>
    /// 本阶段的**剩余月数** = 时限 − 已持续月数（FR-019 的可读口径；不为负）。
    /// </summary>
    /// <param name="state">饥馑状态。</param>
    /// <returns>剩余月数；无时限的阶段（`None` / 第三阶段）为 0。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 <c>null</c>。</exception>
    public static int RemainingMonths(FamineState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (LimitMonths(state.Stage) is not { } limit)
        {
            return 0;
        }

        var remaining = limit - state.ElapsedMonths;

        return remaining > 0 ? remaining : 0;
    }
}
