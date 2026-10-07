using KFL.Core.Enums;

namespace KFL.Core.Entities;

/// <summary>
/// 家族饥馑状态（规格书 §5.4；data-model §3.5、§6.1）。
/// </summary>
/// <remarks>
/// <para>
/// **不变量**：<c><see cref="Stage"/> == <see cref="FamineStage.None"/> ⇔ <see cref="ElapsedMonths"/> == 0</c>
/// ——两条写入通道（三个转移方法）都同时维护两侧，故不存在「阶段为零而计时非零」的中间态。
/// </para>
/// <para>
/// **明确不含**：饥馑 3 月转救济、救济 12 月的阈值、救济折扣 20%（属
/// <c>KFL.Rules/Config/FamineTimeline</c>），以及体质下降与死亡判定（属阶段⑧，FR-019）。
/// 「阶段与剩余月数可读」由本类型的两个只读属性满足（剩余月数 = 时限 − 已持续月数，由消费方算）。
/// </para>
/// <para>
/// **复制构造的存在理由**：阶段迁移必须快照（<c>SettlementResult.FamineBefore</c> /
/// <c>FamineAfter</c>，data-model §4.2），而本类型是可变的家族级状态；故提供显式复制入口，
/// 让「结算前后快照」不必经由公开 setter 绕过任何不变量校验。
/// </para>
/// </remarks>
public sealed class FamineState
{
    /// <summary>构造初始状态：<see cref="FamineStage.None"/>、计时 0。</summary>
    public FamineState()
    {
    }

    /// <summary>复制构造（**仅供快照**：<c>SettlementResult.FamineBefore</c> / <c>FamineAfter</c>）。</summary>
    /// <param name="source">被复制的状态。</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> 为 <c>null</c>。</exception>
    public FamineState(FamineState source)
    {
        ArgumentNullException.ThrowIfNull(source);

        Stage = source.Stage;
        ElapsedMonths = source.ElapsedMonths;
    }

    /// <summary>当前阶段。</summary>
    public FamineStage Stage { get; private set; }

    /// <summary>**本阶段**已持续月数（转入当月记 1；<see cref="FamineStage.None"/> 时为 0）。</summary>
    public int ElapsedMonths { get; private set; }

    /// <summary>
    /// 转入指定阶段并把计时置 1（§5.4；data-model §3.5）。
    /// </summary>
    /// <remarks>
    /// 传入 <see cref="FamineStage.None"/> 时按 <see cref="Clear"/> 的语义处置（计时归 0）——
    /// 否则会与「<c>None</c> ⇔ 计时 0」的不变量冲突；「解除」的专用入口是 <see cref="Clear"/>。
    /// </remarks>
    /// <param name="stage">目标阶段。</param>
    public void TransitionTo(FamineStage stage)
    {
        if (stage == FamineStage.None)
        {
            Clear();
            return;
        }

        Stage = stage;
        ElapsedMonths = 1;
    }

    /// <summary>阶段非 <see cref="FamineStage.None"/> 时把本阶段计时 +1（§5.4）。</summary>
    public void Tick()
    {
        if (Stage != FamineStage.None)
        {
            ElapsedMonths++;
        }
    }

    /// <summary>归 <see cref="FamineStage.None"/> / 计时 0（饥馑全部解除，§5.4；R-12「解除优先」）。</summary>
    public void Clear()
    {
        Stage = FamineStage.None;
        ElapsedMonths = 0;
    }
}
