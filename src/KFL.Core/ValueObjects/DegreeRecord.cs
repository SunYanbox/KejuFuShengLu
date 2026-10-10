using KFL.Core.Enums;

namespace KFL.Core.ValueObjects;

/// <summary>
/// **一条功名变迁记录**：何时、变成什么功名、因为什么（规格书 §4.1「功名变迁历史」）。
/// </summary>
/// <remarks>
/// <para>
/// 前四个成员**全部必需**，都不是可选或可省略的：§7.4 的连坐会**降一级功名**
/// （进士 → 贡士 → 举人 → 白身）。若只记录考成功的功名，被降级者的历史末条会一直写着
/// 「进士」，与实际矛盾——历史本身会变成假的。带 <see cref="Cause"/> 后，一次中式与一次
/// 降级都能被如实读出（research R-14）。
/// </para>
/// <para>
/// 第 5 个成员 <see cref="Class"/>（甲第）是 **003 的向后兼容扩展**（FR-014；规格书 §17
/// 裁决回写 2026-10-08）：可选、默认 <c>null</c>，故 001 时代的四参调用保持合法。
/// 「进士但 <c>Class == null</c>」是**合法状态**（该记录未表达甲第），由授官入口拒绝，
/// 本类型 MUST NOT 代它猜一个等级。
/// </para>
/// </remarks>
public readonly record struct DegreeRecord
{
    /// <summary>构造并校验。</summary>
    /// <param name="level">变化后的功名。</param>
    /// <param name="placement">一甲名次；仅进士可非空（规格书 §6）。</param>
    /// <param name="changedAt">本次变化的年月。</param>
    /// <param name="cause">变化原因。</param>
    /// <param name="imperialClass">
    /// 甲第；<c>null</c> = 本条记录未表达甲第（001 时代的记录、或科举尚未产出的中间态）。
    /// </param>
    /// <exception cref="ArgumentException">违反四条甲第相容性不变量中的任一条。</exception>
    public DegreeRecord(
        DegreeLevel level,
        ImperialPlacement? placement,
        GameDate changedAt,
        DegreeChangeCause cause,
        ImperialClass? imperialClass = null)
    {
        EnsureCompatible(level, placement, imperialClass);

        Level = level;
        Placement = placement;
        ChangedAt = changedAt;
        Cause = cause;
        Class = imperialClass;
    }

    /// <summary>变化后的功名。</summary>
    public DegreeLevel Level { get; }

    /// <summary>一甲名次；仅进士可有。</summary>
    public ImperialPlacement? Placement { get; }

    /// <summary>本次变化的年月。</summary>
    public GameDate ChangedAt { get; }

    /// <summary>变化原因。</summary>
    public DegreeChangeCause Cause { get; }

    /// <summary>
    /// 甲第；<c>null</c> = 本条记录**未表达**甲第。二甲 / 三甲的**唯一判据**（FR-014）。
    /// </summary>
    public ImperialClass? Class { get; }

    /// <summary>
    /// 四条相容性不变量（规格书 §4.1；FR-014；data-model §1.3）：
    /// ① 甲第非空 ⇒ 功名是进士；② 名次非空 ⇒ 甲第 = 一甲；③ 甲第 = 一甲 ⇒ 名次非空；
    /// ④ 甲第 ∈ {二甲, 三甲} ⇒ 名次为空。②③ 合起来即「名次非空 ⟺ 甲第 = 一甲」。
    /// </summary>
    private static void EnsureCompatible(
        DegreeLevel level, ImperialPlacement? placement, ImperialClass? imperialClass)
    {
        if (imperialClass is not null && level != DegreeLevel.JinShi)
        {
            throw new ArgumentException(
                "甲第只属于进士（规格书 §4.1）。", nameof(imperialClass));
        }

        if (placement is not null && imperialClass != ImperialClass.FirstClass)
        {
            throw new ArgumentException(
                "一甲名次非空 ⇒ 甲第 MUST 为一甲（规格书 §6、§4.1）。", nameof(imperialClass));
        }

        if (imperialClass == ImperialClass.FirstClass && placement is null)
        {
            throw new ArgumentException(
                "甲第 = 一甲 ⇒ 一甲名次 MUST 非空（规格书 §6、§4.1）。", nameof(placement));
        }

        if (imperialClass is ImperialClass.SecondClass or ImperialClass.ThirdClass && placement is not null)
        {
            throw new ArgumentException(
                "二甲 / 三甲 MUST NOT 带一甲名次（规格书 §6）。", nameof(placement));
        }
    }
}
