using KFL.Core.Entities;

namespace KFL.Rules.Achievements;

/// <summary>
/// 绝嗣判定与终局触发（规格书 §15:643-646；FR-002/FR-026/FR-032；契约二十四 §3/§6）。
/// </summary>
/// <remarks>
/// <para>
/// **判据唯一实现面**：<c>Family.RegisteredMembers.Count == 0</c>（契约二十四「条款 8」）。
/// 本类 MUST NOT 另立第二套「在册」定义，也 MUST NOT 把
/// <c>Lifecycle.HeadBecameVacant</c> / <c>HeadId == null</c> 当作绝嗣判据（「条款 10」）。
/// </para>
/// <para>
/// **次序**：绝嗣成立的当月，成就判定 MUST 先跑（<c>TurnRunner</c> 编排），随后才调
/// <see cref="TryTrigger"/> 冻结终局——这样触发绝嗣的当月事实能正常解锁（契约二十四「条款 25」）。
/// </para>
/// </remarks>
public static class ExtinctionJudge
{
    /// <summary>判定家族是否已绝嗣（在册成员为零）。</summary>
    /// <param name="state">当前游戏状态。</param>
    /// <returns>在册成员数为零 ⇒ `true`。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 <c>null</c>。</exception>
    public static bool IsExtinct(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Family.RegisteredMembers.Count == 0;
    }

    /// <summary>在满足条件时触发终局并冻结编年史。</summary>
    /// <param name="state">当前游戏状态。</param>
    /// <returns>本次**新**触发终局 ⇒ `true`；已结束或不绝嗣 ⇒ `false` 且**零状态变更**。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 <c>null</c>。</exception>
    public static bool TryTrigger(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Ending.IsEnded || !IsExtinct(state))
        {
            return false;
        }

        var chronicle = ChronicleBuilder.Build(state);
        state.Ending.End(state.CurrentDate, chronicle);
        return true;
    }
}
