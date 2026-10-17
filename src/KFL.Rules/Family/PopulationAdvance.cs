using KFL.Core.Entities;
using KFL.Infrastructure.Abstractions;

namespace KFL.Rules.FamilyLifecycle;

/// <summary>
/// ⑧ 人口与健康推进（契约十八 §2；FR-007）：把八个子步按**固定次序**串联起来。
/// </summary>
/// <remarks>
/// <para>
/// 次序 MUST NOT 增删或穿插：⑧-a 婚配事件 → ⑧-b 分娩 → ⑧-c 患病与病程 → ⑧-d 成长 →
/// ⑧-e 饥馑死亡判定 → ⑧-f 寿终与夭折 → ⑧-g 死亡结算 → ⑧-h 家主继任。
/// </para>
/// <para>
/// 子步之间只经 <see cref="DeathRegister"/> 与各执行器的返回值通信：⑧-b~⑧-f **只登记**死亡，
/// ⑧-g 才统一结算（不变量 11）。「0 次」子步 MUST NOT 调用 <see cref="IRandomService"/>。
/// </para>
/// <para>
/// 可见性偏差记录：本类与 <see cref="PopulationAdvanceResult"/> 均为 <c>internal</c>——任务书 T022
/// 写作 <c>public static class</c>，但同一任务要求返回 <c>internal sealed record</c>，二者不可兼得
/// （CS0050）。唯一调用者是同程序集的 <c>MonthlySettlementEngine</c>，测试经
/// <c>SettlementResult.Lifecycle</c> 间接观察；差异已登记在回写范围（Phase 9）。
/// </para>
/// </remarks>
internal static class PopulationAdvance
{
    /// <summary>按固定次序推进一个月的人口与健康，返回当月事实段。</summary>
    /// <param name="state">游戏状态（读 <c>CurrentDate</c> 为当月）。</param>
    /// <param name="random">随机服务（子步内消费，次序见契约十八 §3）。</param>
    /// <param name="names">取名服务（新生儿、候选者与买来者）。</param>
    /// <returns>当月事实段。</returns>
    internal static PopulationAdvanceResult Run(GameState state, IRandomService random, INameGenerator names)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(names);

        // ⑧-a 婚配事件（捉婿 → 提亲）：MarriageMarket.Run
        // ⑧-b 分娩（消费 PendingBirths）：Childbirth.Run
        // ⑧-c 患病与病程：IllnessAdvance.Run
        // ⑧-d 成长与体质恢复（0 随机）：GrowthAdvance.Run
        // ⑧-e 饥馑死亡判定（严重期）：FamineHealth.Run
        // ⑧-f 寿终与夭折：LifespanAdvance.Run
        // ⑧-g 死亡结算：DeathSettlement.Run
        // ⑧-h 家主继任（0 随机）：HeadSuccession.Run
        return new PopulationAdvanceResult { Lifecycle = LifecycleSnapshot.None(state.CurrentDate) };
    }
}

/// <summary>
/// ⑧ 子步的汇总结果（data-model §3.1）；<c>MonthlySettlementEngine</c> 只取其中的
/// <see cref="Lifecycle"/> 展开到 <c>SettlementResult.Lifecycle</c>。
/// </summary>
internal sealed record PopulationAdvanceResult
{
    /// <summary>当月事实段。</summary>
    public required LifecycleSnapshot Lifecycle { get; init; }
}
