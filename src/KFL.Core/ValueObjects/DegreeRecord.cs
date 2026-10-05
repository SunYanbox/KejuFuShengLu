using KFL.Core.Enums;

namespace KFL.Core.ValueObjects;

/// <summary>
/// **一条功名变迁记录**：何时、变成什么功名、因为什么（规格书 §4.1「功名变迁历史」）。
/// </summary>
/// <remarks>
/// 四个成员**全部必需**，都不是可选或可省略的：§7.4 的连坐会**降一级功名**
/// （进士 → 贡士 → 举人 → 白身）。若只记录考成功的功名，被降级者的历史末条会一直写着
/// 「进士」，与实际矛盾——历史本身会变成假的。带 <see cref="Cause"/> 后，一次中式与一次
/// 降级都能被如实读出（research R-14）。
/// </remarks>
public readonly record struct DegreeRecord
{
    /// <summary>构造并校验。</summary>
    /// <param name="level">变化后的功名。</param>
    /// <param name="placement">一甲名次；仅进士可非空（规格书 §6）。</param>
    /// <param name="changedAt">本次变化的年月。</param>
    /// <param name="cause">变化原因。</param>
    /// <exception cref="ArgumentException"><paramref name="placement"/> 非空但 <paramref name="level"/> 不是进士。</exception>
    public DegreeRecord(
        DegreeLevel level,
        ImperialPlacement? placement,
        GameDate changedAt,
        DegreeChangeCause cause)
    {
        if (placement is not null && level != DegreeLevel.JinShi)
        {
            throw new ArgumentException(
                "一甲名次只属于进士（规格书 §6）。", nameof(placement));
        }

        Level = level;
        Placement = placement;
        ChangedAt = changedAt;
        Cause = cause;
    }

    /// <summary>变化后的功名。</summary>
    public DegreeLevel Level { get; }

    /// <summary>一甲名次；仅进士可有。</summary>
    public ImperialPlacement? Placement { get; }

    /// <summary>本次变化的年月。</summary>
    public GameDate ChangedAt { get; }

    /// <summary>变化原因。</summary>
    public DegreeChangeCause Cause { get; }
}
