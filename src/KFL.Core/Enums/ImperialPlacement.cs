namespace KFL.Core.Enums;

/// <summary>
/// 一甲名次（规格书 §6；§12.2 名次着色）。**仅进士可有**，由
/// <see cref="ValueObjects.DegreeRecord"/> 在构造期校验。
/// </summary>
public enum ImperialPlacement
{
    /// <summary>状元。</summary>
    ZhuangYuan,

    /// <summary>榜眼。</summary>
    BangYan,

    /// <summary>探花。</summary>
    TanHua,
}
