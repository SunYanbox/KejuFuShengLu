using KFL.Core.Enums;

namespace KFL.Rules.Config;

/// <summary>
/// 难度系数表（规格书 §11；data-model §4.1）。
/// </summary>
/// <remarks>
/// <para>
/// 收益系数与支出系数在**本阶段**被结算路径读取；贿赂风险系数与负面事件系数在本阶段
/// **MUST NOT 被任何计算路径读取**（分别属阶段⑥ 与阶段⑧），此处登记只为 §11 的数值单点。
/// </para>
/// <para>
/// 本类是这四组数值的唯一出处（SC-008）。
/// </para>
/// </remarks>
public static class DifficultyRates
{
    /// <summary>收益系数（除储蓄利息外的全部收入乘之，§5.2 括注、§11）。</summary>
    /// <param name="difficulty">难度。</param>
    /// <returns>收益系数。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="difficulty"/> 不在四档之内。</exception>
    public static decimal RevenueFactor(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => 1.4m,
        Difficulty.Normal => 1.0m,
        Difficulty.Hard => 0.9m,
        Difficulty.Hell => 0.8m,
        _ => throw Unknown(difficulty),
    };

    /// <summary>支出系数（全部支出乘之，§11）。</summary>
    /// <param name="difficulty">难度。</param>
    /// <returns>支出系数。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="difficulty"/> 不在四档之内。</exception>
    public static decimal ExpenseFactor(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => 0.6m,
        Difficulty.Normal => 1.0m,
        Difficulty.Hard => 1.1m,
        Difficulty.Hell => 1.3m,
        _ => throw Unknown(difficulty),
    };

    /// <summary>贿赂风险系数（阶段⑥ 使用；本阶段 MUST NOT 被读取）。</summary>
    /// <param name="difficulty">难度。</param>
    /// <returns>贿赂风险系数。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="difficulty"/> 不在四档之内。</exception>
    public static decimal BriberyRiskFactor(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => 0.2m,
        Difficulty.Normal => 1.0m,
        Difficulty.Hard => 1.5m,
        Difficulty.Hell => 3.0m,
        _ => throw Unknown(difficulty),
    };

    /// <summary>负面事件系数（阶段⑧ 使用；本阶段 MUST NOT 被读取）。</summary>
    /// <param name="difficulty">难度。</param>
    /// <returns>负面事件系数。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="difficulty"/> 不在四档之内。</exception>
    public static decimal NegativeEventFactor(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => 0.5m,
        Difficulty.Normal => 1.0m,
        Difficulty.Hard => 1.3m,
        Difficulty.Hell => 1.4m,
        _ => throw Unknown(difficulty),
    };

    private static ArgumentOutOfRangeException Unknown(Difficulty difficulty) =>
        new(nameof(difficulty), difficulty, "未登记的难度（规格书 §11 共四档）。");
}
