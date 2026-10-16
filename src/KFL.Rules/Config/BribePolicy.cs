using KFL.Core.Enums;

namespace KFL.Rules.Config;

/// <summary>
/// 行贿政策（规格书 §7.1、§7.2、§11；契约十七 §1）——这批数值的**唯一**声明处。
/// </summary>
/// <remarks>
/// <para>
/// 本类是「行贿」这条链路的数值单点：窗口前移月数、累进步长、三级基础金额、三级基础被抓率、
/// 三级效果乘数 <c>k</c>、被抓概率封顶与 <c>k⁰</c> 恒等元。
/// 读取时机是**每次行贿**（金额与概率各一次、乘数每次取用）。
/// </para>
/// <para>
/// **明确不含**（契约十七 §1 口径 3、4；MUST NOT 另立第二处真源）：
/// 难度支出系数 <c>0.6 / 1.0 / 1.1 / 1.3</c> 与贿赂风险系数 <c>0.2 / 1.0 / 1.5 / 3.0</c> 由 002 的
/// <see cref="DifficultyRates"/> 单点决定；掷骰概率封顶 <c>0.899999m</c> 由 005 的
/// <see cref="ExamPolicy.DiceRateCap"/> 单点决定；录取率 / 进甲率公式留在 <c>ExamAdmission</c>。
/// </para>
/// <para>
/// **明确不含**（契约十六 §2、契约十七 §3）：惩罚矩阵、服刑月数、罚金比例 / 固定额、
/// 列席禁考月数一概不在此处，它们属 <see cref="PunishmentPolicy"/>。
/// </para>
/// </remarks>
public static class BribePolicy
{
    /// <summary>
    /// 行贿窗口在考试月**之前**的前移月数（规格书 §7.1）：窗口 = 考试当月及其前一月。
    /// </summary>
    public const int WindowPreviousMonths = 1;

    /// <summary>
    /// 累进步长 <c>0.2</c>——**金额与被抓概率共用**同一因子 <c>(1 + 0.2 × (n − 1))</c>（§7.1）。
    /// </summary>
    public const decimal ProgressionStep = 0.2m;

    /// <summary>被抓概率的**封顶值** <c>1.0</c>：<c>&ge; 100%</c> 即必定被抓（契约十五 §3 条款 3）。</summary>
    /// <remarks>
    /// 上界 MUST NOT 复用 <see cref="ExamPolicy.DiceRateCap"/>——「被抓概率」与「录取率」是两个量
    /// （0.899999 是录取率的封顶；达标即触顶置灰）。
    /// </remarks>
    public const decimal CaughtProbabilityCap = 1.0m;

    /// <summary>
    /// 效果乘数 <c>k</c> 的**零次幂恒等元** <c>1.0</c>——从未行贿（<c>n = 0</c>）时乘数取它
    /// （契约十五 §4.1 条款 3）。它**不是** <c>neutral</c> 占位，而是 <c>k⁰</c> 的唯一出处。
    /// </summary>
    public const decimal UnitMultiplier = 1m;

    /// <summary>三级考试的**基础行贿金额**（贯；未乘累进因子与难度支出系数）。</summary>
    /// <param name="level">考试级别。</param>
    /// <returns>基础金额（贯）：解试 <c>100</c> / 省试 <c>500</c> / 殿试 <c>2000</c>。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> 不在三级之内。</exception>
    public static int BaseAmountGuan(ExamLevel level) => level switch
    {
        ExamLevel.JieShi => 100,
        ExamLevel.ShengShi => 500,
        ExamLevel.DianShi => 2000,
        _ => throw new ArgumentOutOfRangeException(
            nameof(level), level, "未登记的考试级别（规格书 §6 共三级）。"),
    };

    /// <summary>三级考试的**基础被抓率**（未乘累进因子与难度贿赂风险系数）。</summary>
    /// <param name="level">考试级别。</param>
    /// <returns>基础被抓率：解试 <c>0.05</c> / 省试 <c>0.15</c> / 殿试 <c>0.35</c>。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> 不在三级之内。</exception>
    public static decimal BaseCaughtRate(ExamLevel level) => level switch
    {
        ExamLevel.JieShi => 0.05m,
        ExamLevel.ShengShi => 0.15m,
        ExamLevel.DianShi => 0.35m,
        _ => throw new ArgumentOutOfRangeException(
            nameof(level), level, "未登记的考试级别（规格书 §6 共三级）。"),
    };

    /// <summary>
    /// 三级考试的**效果乘数底数** <c>k</c>：第 <c>n</c> 次行贿后录取率（殿试为进甲率）乘 <c>kⁿ</c>。
    /// </summary>
    /// <param name="level">考试级别。</param>
    /// <returns>效果乘数底数：解试 <c>1.6</c> / 省试 <c>1.3</c> / 殿试 <c>1.1</c>。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> 不在三级之内。</exception>
    /// <remarks>
    /// <c>n = 0</c> 时乘数由乘法循环的恒等初值 <see cref="UnitMultiplier"/> 给出，MUST NOT 在此分支里内联
    /// <c>k⁰</c>；乘数本身**无上限**（只有最终概率达到 <see cref="ExamPolicy.DiceRateCap"/> 才触顶）。
    /// </remarks>
    public static decimal EffectMultiplierK(ExamLevel level) => level switch
    {
        ExamLevel.JieShi => 1.6m,
        ExamLevel.ShengShi => 1.3m,
        ExamLevel.DianShi => 1.1m,
        _ => throw new ArgumentOutOfRangeException(
            nameof(level), level, "未登记的考试级别（规格书 §6 共三级）。"),
    };
}
