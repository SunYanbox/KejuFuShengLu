using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;

namespace KFL.Rules.Settlement;

/// <summary>
/// 一次饥馑判定的结果（data-model §6.1；契约三 §8）。
/// </summary>
/// <remarks>
/// **不变量**：<see cref="Paid"/> = `min(应付额, 可付额)` 且 <c>&gt;= 0</c>；
/// <see cref="Transition"/> 非空时表示**本月发生了一次**阶段迁移，其取值即要落的事件条目所对应的阶段
/// （<see cref="FamineStage.None"/> = 全部解除 → `FamineResolved`）。
/// </remarks>
/// <param name="IsFullPayment">本月是否**足额**支付（可付额 ≥ 应付额）。</param>
/// <param name="Paid">本月实际支付额（部分支付时 = 可付额）。</param>
/// <param name="Transition">本月发生的阶段迁移；<c>null</c> = 本月未迁移。</param>
public readonly record struct FamineDecision(bool IsFullPayment, Money Paid, FamineStage? Transition);

/// <summary>
/// 饥馑状态机的**纯函数**（规格书 §5.4；R-12「解除优先」；契约三 §8 的转移表）。
/// </summary>
/// <remarks>
/// <para>
/// **判定次序固定**（每个月至多迁移一次）：
/// ⓪ 先把本阶段计时 +1（<see cref="FamineState.Tick"/>，阶段为 <see cref="FamineStage.None"/> 时无动作）
/// ——MUST 在阈值比较**之前**（E-14）；
/// ① 可付额 ≥ 当月应付额 → 足额支付、阶段归 `None`、计时清零（此前非 `None` 时报告一次解除）；
/// ② 否则按转移表推进：`None` → `Famine`（计时 1）、`Famine` 推进后计时 ≥ 3 → `Relief`（计时重算为 1）、
/// `Relief` 推进后计时 ≥ 12 → `Severe`（计时重算为 1）、`Severe` 保持。
/// </para>
/// <para>
/// **应付额由调用方按月初阶段算好传入**：救济期的 `−20%` 由
/// <c>LivingCostCalculator</c> 依传入的 <see cref="FamineStage"/> 处理（本类不重算生活费）。
/// </para>
/// <para>
/// **本类只改饥馑状态、不碰资金池**：扣付与落条目由 <c>MonthlySettlementEngine</c> 经
/// <c>FamilyEconomy.Apply</c> / <c>EnterFamineStage</c> 执行（FR-021）。
/// </para>
/// <para>
/// **明确不含**：体质 −10/月、每人每月 5% 死亡判定与救济期的体质减免（属阶段⑧，FR-019）。
/// </para>
/// </remarks>
public static class FamineController
{
    /// <summary>
    /// 判定本月饥馑状态并**就地**推进 <paramref name="state"/>。
    /// </summary>
    /// <param name="state">饥馑状态（**就地**被推进）。</param>
    /// <param name="payable">当月**应付**生活费（救济期即减免后的值）。</param>
    /// <param name="pool">
    /// **可付额** = 现金 + 储蓄（R-05；MUST NOT 含商本池，也不是 SC-005 求和的资金池）。
    /// </param>
    /// <returns>本次判定的结果（实付额与至多一次的阶段迁移）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 <c>null</c>。</exception>
    public static FamineDecision Evaluate(FamineState state, Money payable, Money pool)
    {
        ArgumentNullException.ThrowIfNull(state);

        // ⓪ 计时先推进、后比阈值（E-14）：转入当月已记 1，故「满 3 月」= 第 3 个饥馑月当月。
        state.Tick();

        var paid = pool < payable ? pool : payable;
        var isFullPayment = !(pool < payable);

        if (isFullPayment)
        {
            // ① 解除优先：付得起就一次性全部解除、计时清零，MUST NOT 留任何残留。
            if (state.Stage == FamineStage.None)
            {
                return new FamineDecision(true, paid, null);
            }

            state.Clear();

            return new FamineDecision(true, paid, FamineStage.None);
        }

        // ② 部分支付后按转移表推进——switch 至多命中一个分支，故同月至多迁移一次。
        switch (state.Stage)
        {
            case FamineStage.None:
                state.TransitionTo(FamineStage.Famine);

                return new FamineDecision(false, paid, FamineStage.Famine);

            case FamineStage.Famine when state.ElapsedMonths >= FamineTimeline.MonthsUntilRelief:
                state.TransitionTo(FamineStage.Relief);

                return new FamineDecision(false, paid, FamineStage.Relief);

            case FamineStage.Relief when state.ElapsedMonths >= FamineTimeline.ReliefMonthsUntilSevere:
                state.TransitionTo(FamineStage.Severe);

                return new FamineDecision(false, paid, FamineStage.Severe);

            default:
                return new FamineDecision(false, paid, null);
        }
    }
}
