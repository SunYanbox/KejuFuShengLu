namespace KFL.Core.Enums;

/// <summary>
/// 婚配阶层（规格书 §9.1 的聘礼 / 嫁妆三档；007 的 R-05）。
/// </summary>
/// <remarks>
/// <para>
/// 阶层**判据只有一处**（<c>KFL.Rules/Family/LifecycleQueries.ClassOf</c>）：
/// <c>Family.HasShiStatus == true</c> ⇒ <see cref="Official"/>（**优先于** <see cref="Origin"/> 判定）；
/// 否则 <c>Origin.Commerce</c> / <c>Origin.Scholar</c> ⇒ <see cref="Scholar"/>；否则
/// <see cref="Commoner"/>。
/// </para>
/// <para>
/// **明确不含**：家族声望（本阶段 MUST NOT 新增）；阶层只用于选聘礼 / 嫁妆金额档，
/// MUST NOT 参与其它判定。
/// </para>
/// </remarks>
public enum MarriageClass
{
    /// <summary>平民档（<c>Origin.Farming</c> / <c>Origin.Artisan</c>）。</summary>
    Commoner = 0,

    /// <summary>士人档（<c>Origin.Commerce</c> / <c>Origin.Scholar</c>）。</summary>
    Scholar = 1,

    /// <summary>仕族档（<c>Family.HasShiStatus == true</c>）。</summary>
    Official = 2,
}
