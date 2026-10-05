namespace KFL.Core.Enums;

/// <summary>
/// 难度（规格书 §11）。取值次序即强弱次序：简单 &lt; 普通 &lt; 困难 &lt; 地狱。
/// </summary>
public enum Difficulty
{
    /// <summary>简单。</summary>
    Easy,

    /// <summary>普通。</summary>
    Normal,

    /// <summary>困难。</summary>
    Hard,

    /// <summary>地狱。</summary>
    Hell,
}
