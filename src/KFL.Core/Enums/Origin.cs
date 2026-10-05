namespace KFL.Core.Enums;

/// <summary>
/// 出身（规格书 §10.1 四出身），存档级、终身特性。
/// 与「仕身份」（<c>Family.HasShiStatus</c>，进士直系血统）是两个彼此独立、可并存的属性
/// （规格书 §10.2）。
/// </summary>
public enum Origin
{
    /// <summary>农。</summary>
    Farmer,

    /// <summary>工。</summary>
    Artisan,

    /// <summary>商。</summary>
    Merchant,

    /// <summary>士。</summary>
    Scholar,
}
