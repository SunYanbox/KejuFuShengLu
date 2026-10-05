namespace KFL.Core.Enums;

/// <summary>职业指派（规格书 §4.1「职业指派」；§4.3 读书指派；§5.2 收入来源）。</summary>
/// <remarks>
/// §17 裁决：§5.2 的「自耕」与「务农（无田雇工）」**不**拆成两个枚举值——两者的区别
/// 由田地持有量派生，属阶段②。本阶段只保留一个 <see cref="Farming"/>。
/// </remarks>
public enum Occupation
{
    /// <summary>无指派。</summary>
    None,

    /// <summary>读书（规格书 §4.3）。</summary>
    Studying,

    /// <summary>务农。</summary>
    Farming,

    /// <summary>做工。</summary>
    Crafting,

    /// <summary>经商。</summary>
    Trading,
}
