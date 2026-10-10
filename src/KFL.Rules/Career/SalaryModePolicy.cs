using KFL.Core.Entities;
using KFL.Core.Enums;

namespace KFL.Rules.Career;

/// <summary>
/// 俸禄三态的**只读判定**（规格书 §8.1、§8.2；FR-021；data-model §3.6；契约七 §5）。
/// </summary>
/// <remarks>
/// <para>
/// **判定优先级（从上到下）**：<c>Rank != null ∧ Retired → Retired</c>；
/// <c>Rank != null → Active</c>；<c>Rank == null ∧ AwaitingPost → AwaitingPost</c>；否则 <c>None</c>。
/// <see cref="SalaryMode.Retired"/> 优先于 <see cref="SalaryMode.Active"/>，
/// 保证「致仕后仍持官阶」不会退回全俸。
/// </para>
/// <para>
/// **只读纯函数**：不改任何状态；只读 <c>Rank</c> 与 <c>Status</c>，与年月无关。
/// **MUST NOT** 接收 <c>Money</c> / <c>Origin</c> / <c>Difficulty</c> / <c>GameDate</c>，
/// MUST NOT 计算金额或任何乘区——半俸比例、难度收益系数与「士出身 ×1.05」的单点都在
/// <c>Settlement/IncomeCalculator</c>（契约七 §5 条款 3：半俸**只乘一次**）。
/// **MUST NOT** 读取 <c>Family.HasShiStatus</c>。
/// </para>
/// </remarks>
public static class SalaryModePolicy
{
    /// <summary>判定一名成员当月的俸禄三态。</summary>
    /// <param name="person">成员档案。</param>
    /// <returns>三态取值。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 为 <c>null</c>。</exception>
    public static SalaryMode Of(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        if (person.Rank is not null)
        {
            return person.Status.HasFlag(StatusFlag.Retired) ? SalaryMode.Retired : SalaryMode.Active;
        }

        return person.Status.HasFlag(StatusFlag.AwaitingPost) ? SalaryMode.AwaitingPost : SalaryMode.None;
    }
}
