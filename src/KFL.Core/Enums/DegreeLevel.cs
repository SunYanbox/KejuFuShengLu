namespace KFL.Core.Enums;

/// <summary>
/// 功名等级（规格书 §6 功名链：白身 → 举人 → 贡士 → 进士）。
/// 取值顺序即链条顺序，可比较。
/// </summary>
public enum DegreeLevel
{
    /// <summary>白身。</summary>
    BaiShen,

    /// <summary>举人。</summary>
    JuRen,

    /// <summary>贡士。</summary>
    GongShi,

    /// <summary>进士。</summary>
    JinShi,
}
