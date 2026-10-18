using KFL.Core.ValueObjects;

namespace KFL.Core.Entities;

/// <summary>
/// 终局状态：家族是否已绝嗣，以及触发当刻冻结的编年史快照（FR-016/FR-017；data-model §2.5）。
/// </summary>
/// <remarks>
/// <para>
/// **唯一终局**：规格书 §15:628「本作无胜利条件，唯一的终止条件是**家族绝嗣**」，
/// 故不引入 `EndingReason` 之类的枚举（data-model §2.7）。
/// </para>
/// <para>
/// **不变量**：`IsEnded ⇔ EndedAt.HasValue ∧ Chronicle is not null`。它由
/// <see cref="End"/> 是唯一置位路径 + 存档校验第 ⑳ 条共同保证。
/// </para>
/// <para>
/// <see cref="End"/> **幂等**（SC-011 的前提）：读档后再推进到达同一终局时 MUST NOT 改写编年史，
/// 返回 `false`。
/// </para>
/// <para>
/// 复原通道 <see cref="Restore"/> 是 `internal`（`KFL.Core` 经 `AssemblyInfo.cs` 对
/// `KFL.Infrastructure` 开放），**MUST NOT** 新增 `public` 面。
/// </para>
/// </remarks>
public sealed class EndingState
{
    /// <summary>家族是否已绝嗣（终局）。</summary>
    public bool IsEnded { get; private set; }

    /// <summary>终局触发年月；未结束为 <c>null</c>。</summary>
    public GameDate? EndedAt { get; private set; }

    /// <summary>终局触发当刻冻结的编年史；未结束为 <c>null</c>。</summary>
    public ChronicleSnapshot? Chronicle { get; private set; }

    /// <summary>置位终局并冻结编年史快照（幂等）。</summary>
    /// <param name="at">触发年月。</param>
    /// <param name="chronicle">当刻冻结的编年史快照。</param>
    /// <returns>本次是新置位 ⇒ `true`；已结束 ⇒ `false` 且**原样返回、不改写快照**。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="chronicle"/> 为 <c>null</c>。</exception>
    public bool End(GameDate at, ChronicleSnapshot chronicle)
    {
        ArgumentNullException.ThrowIfNull(chronicle);

        if (IsEnded)
        {
            return false;
        }

        IsEnded = true;
        EndedAt = at;
        Chronicle = chronicle;
        return true;
    }

    /// <summary>存档层复原通道（`internal`；只在 <c>KFL.Infrastructure</c> 内可见）。</summary>
    /// <param name="isEnded">是否已结束。</param>
    /// <param name="endedAt">终局触发年月。</param>
    /// <param name="chronicle">冻结的编年史快照。</param>
    internal void Restore(bool isEnded, GameDate? endedAt, ChronicleSnapshot? chronicle)
    {
        IsEnded = isEnded;
        EndedAt = endedAt;
        Chronicle = chronicle;
    }
}
