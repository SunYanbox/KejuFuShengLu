namespace KFL.Core.Enums;

/// <summary>
/// 账本条目的二分类（research R-04 的 §17 裁决 E-08）。
/// </summary>
/// <remarks>
/// <para>
/// **资金类**（<see cref="Treasury"/>）= 引起资金池变动的收支；**事件类**（<see cref="Event"/>）=
/// 不引起资金池变动的阶段 / 负债事件（饥馑阶段迁移、贷款计息入欠息）。
/// </para>
/// <para>
/// **SC-005 的求和口径** = 对 <see cref="Treasury"/> 类条目求和；事件类条目不参与求和，
/// 但携带自己的金额语义。类别 → 种类的唯一真源是 <c>LedgerCategoryMetadata.KindOf</c>。
/// </para>
/// </remarks>
public enum LedgerEntryKind
{
    /// <summary>资金类（参与 SC-005 求和）。</summary>
    Treasury,

    /// <summary>事件类（不参与 SC-005 求和）。</summary>
    Event,
}
