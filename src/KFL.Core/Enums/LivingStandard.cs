namespace KFL.Core.Enums;

/// <summary>
/// 生活费档位（规格书 §5.1 的三档）。
/// </summary>
/// <remarks>
/// **明确不含**：三档 × 四年龄档的日耗数值、新建存档的初始档位——两者同属
/// <c>KFL.Rules/Config/LivingCostTable</c>（其 <c>InitialStandard</c> 是初始档位的**唯一出处**，
/// research R-09；SC-008）。
/// </remarks>
public enum LivingStandard
{
    /// <summary>拮据。</summary>
    Frugal,

    /// <summary>普通（新建存档的初始档位）。</summary>
    Normal,

    /// <summary>体面。</summary>
    Comfortable,
}
