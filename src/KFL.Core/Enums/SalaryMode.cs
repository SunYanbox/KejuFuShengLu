namespace KFL.Core.Enums;

/// <summary>
/// 俸禄三态（规格书 §8.1、§8.2；FR-021）。
/// </summary>
/// <remarks>
/// <para>
/// **派生量、不落字段**：由 <c>KFL.Rules/Career/SalaryModePolicy.Of</c> 从
/// <c>Person.Rank</c> 与 <c>Person.Status</c> 只读判出，故不存在「与档案并存的第二真源」。
/// </para>
/// <para>
/// <see cref="None"/> 与 <see cref="AwaitingPost"/> 都无俸禄，分开是为了让断言能区分
/// **无官**与**待阙**（待阙是「已及第、未授官」的中间态）。
/// 半俸比例与一切金额乘区**不在本类型**（单点在 <c>OfficialCareerPolicy</c> 与
/// <c>IncomeCalculator</c>）。
/// </para>
/// </remarks>
public enum SalaryMode
{
    /// <summary>无官职（也无待阙）：无俸禄。</summary>
    None,

    /// <summary>在任：全俸。</summary>
    Active,

    /// <summary>待阙：无俸禄。</summary>
    AwaitingPost,

    /// <summary>已致仕：半俸（官阶保留）。</summary>
    Retired,
}
