namespace KFL.Core.Enums;

/// <summary>
/// 饥馑阶段（规格书 §5.4 的四阶段；data-model §6.1 的转移表）。
/// </summary>
/// <remarks>
/// **明确不含**：饥馑 3 月转救济、救济 12 月、救济折扣 20% 三个数值（属
/// <c>KFL.Rules/Config/FamineTimeline</c>），以及体质下降与死亡判定（属阶段⑧，FR-019）。
/// 本枚举 MUST NOT 与既有的 <see cref="StatusFlag.Famine"/> 混用：后者是**成员**状态位
/// （规格书 §4.1），前者是**家族级**阶段（§5.4）；两者是不同口径，MUST NOT 互相顶替。
/// </remarks>
public enum FamineStage
{
    /// <summary>无（未进入饥馑）。</summary>
    None,

    /// <summary>饥馑。</summary>
    Famine,

    /// <summary>救济（支出减免期）。</summary>
    Relief,

    /// <summary>第三阶段（不再减免）。</summary>
    Severe,
}
