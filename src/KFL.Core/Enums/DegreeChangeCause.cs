namespace KFL.Core.Enums;

/// <summary>
/// 一次功名变化的原因（规格书 §4.1「功名变迁历史」）。
/// 记录原因是为了让 §7.4 的连坐降级与科举中式在变迁史上可区分（research R-14）。
/// </summary>
public enum DegreeChangeCause
{
    /// <summary>开局带入，或买功名 / 婚姻带入。</summary>
    Initial,

    /// <summary>科举中式（规格书 §6）。</summary>
    ExamPass,

    /// <summary>连坐降级（规格书 §7.4：进士 → 贡士 → 举人 → 白身）。</summary>
    PunishmentDemotion,

    /// <summary>调试控制台改写（规格书 §13.2）。</summary>
    DebugEdit,
}
