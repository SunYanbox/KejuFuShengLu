using System.Diagnostics.CodeAnalysis;

namespace KFL.Core.Enums;

/// <summary>
/// 成员状态标记（规格书 §4.1 九种状态；§7.5 可并存）。
/// </summary>
/// <remarks>
/// <see cref="None"/> 是零值，表示「九个标记全不成立」的空集合；它不是第九种状态，
/// 九种状态见其余九个成员。
/// </remarks>
[Flags]
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "类型名 StatusFlag 由 data-model §1.3 与 spec FR-010 固定（规格书 §4.1「状态标记」），改名会偏离设计真源。")]
public enum StatusFlag
{
    /// <summary>空集合。</summary>
    None = 0,

    /// <summary>患病。</summary>
    Ill = 1 << 0,

    /// <summary>饥馑。</summary>
    Famine = 1 << 1,

    /// <summary>服刑（规格书 §7.5：以月为单位，5 年 = 60 月）。</summary>
    ServingSentence = 1 << 2,

    /// <summary>待阙。</summary>
    AwaitingPost = 1 << 3,

    /// <summary>禁考（与服刑独立并行计时，规格书 §7.5）。</summary>
    ExamBanned = 1 << 4,

    /// <summary>禁升（规格书 §7.5、§8.2）。</summary>
    PromotionBanned = 1 << 5,

    /// <summary>外嫁。</summary>
    MarriedOut = 1 << 6,

    /// <summary>致仕。</summary>
    Retired = 1 << 7,

    /// <summary>已亡。</summary>
    Deceased = 1 << 8,
}
