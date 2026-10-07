namespace KFL.Core.Enums;

/// <summary>
/// 年龄档（规格书 §5.1 的四档）。
/// </summary>
/// <remarks>
/// 归属判定依赖 <c>12</c> / <c>14</c> / <c>18</c> / <c>60</c> 四个规则数值，故判定在
/// <c>KFL.Rules/Config/AgeBracketPolicy</c>（research R-06：数值 MUST 只在配置类里出现一次）；
/// 本枚举只承载档位本身。**生日当月即转档**由 <c>GameDate.AgeInYearsAt</c> 的既有语义保证。
/// </remarks>
public enum AgeBracket
{
    /// <summary>儿童（未成年：男 &lt;12 / 女 &lt;14）。</summary>
    Child,

    /// <summary>青年（已成年且 ≤18 岁）。</summary>
    Youth,

    /// <summary>成人。</summary>
    Adult,

    /// <summary>老人（≥60 岁）。</summary>
    Elder,
}
