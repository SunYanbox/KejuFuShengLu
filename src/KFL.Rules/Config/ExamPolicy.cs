using KFL.Core.Enums;

namespace KFL.Rules.Config;

/// <summary>
/// 科举政策（规格书 §6、§7.1、§7.2；契约十四 §1）——§6 科举数值的**唯一出处**。
/// </summary>
/// <remarks>
/// <para>
/// 本类是逻辑轨 ⑤ 全部**考试数值**的单点：报考年龄线、三级考试基础录取率、学业系数、
/// 仕天赋除数、仕族加成、未进甲者的二甲学业线、掷骰概率封顶、本轮贿赂乘数、
/// 免解失败阈值、特奏名两条门槛与省试路费四档。
/// </para>
/// <para>
/// **明确不含**（契约十四 §1 口径 3、4；MUST NOT 另立第二处真源）：
/// 待阙月数区间与初始官阶映射保留在 <see cref="OfficialCareerPolicy"/>（003 已登记），
/// 本类 MUST NOT 复制；官俸与考课数值同理。
/// </para>
/// <para>
/// **明确不含**（契约十四 §2）：贿赂的累进金额 / 被抓概率 / 惩罚矩阵 / 连坐系数（逻辑轨 ⑥）、
/// 成长衰老疾病死亡的任何概率或年龄线（逻辑轨 ⑦）、成就与绝嗣阈值（逻辑轨 ⑧）、
/// 「无限制模式」的任何按调用方分支的开关（章程原则 VI）。
/// </para>
/// </remarks>
public static class ExamPolicy
{
    /// <summary>报考年龄下限（周岁，生日当月生效；规格书 §6「报考资格」）。</summary>
    public const int MinimumAge = 14;

    /// <summary>解试基础录取率 1%（规格书 §6）。</summary>
    public const decimal JieShiBaseRate = 0.01m;

    /// <summary>省试基础录取率 10%（规格书 §6）。</summary>
    public const decimal ShengShiBaseRate = 0.10m;

    /// <summary>殿试基础进甲率 5%（规格书 §6）。</summary>
    public const decimal ImperialPromotionBaseRate = 0.05m;

    /// <summary>学业系数基数（规格书 §6）。</summary>
    public const decimal StudyFactorBase = 0.4m;

    /// <summary>学业系数每点增量（规格书 §6）。</summary>
    public const decimal StudyFactorPerPoint = 0.012m;

    /// <summary>仕天赋除数：<c>(1 + 仕天赋 / 400)</c>（规格书 §6）。</summary>
    public const int OfficialdomDivisor = 400;

    /// <summary>仕族（<c>Family.HasShiStatus</c>）录取率加成 ×1.1（规格书 §6）。</summary>
    public const decimal ShiStatusAdmissionBonus = 1.1m;

    /// <summary>未进甲者：学业 <c>&gt;= 50</c> ⇒ 二甲，否则三甲（规格书 §6）。</summary>
    public const int SecondClassStudyThreshold = 50;

    /// <summary>需掷骰概率的封顶 89.9999%（规格书 §7.1）。</summary>
    public const decimal DiceRateCap = 0.899999m;

    /// <summary>解试累计失败数达 3 ⇒ 此后免掷骰中第（规格书 §6「免解」）。</summary>
    public const int JieShiExemptionFailureThreshold = 3;

    /// <summary>特奏名年龄下限（规格书 §7.2）。</summary>
    public const int SpecialTributeMinimumAge = 50;

    /// <summary>特奏名省试累计失败数阈值（规格书 §7.2）。</summary>
    public const int SpecialTributeFailureThreshold = 6;

    /// <summary>
    /// 省试路费（贯；规格书 §6「省试路费」）——**不乘**难度支出系数。
    /// </summary>
    /// <param name="difficulty">难度。</param>
    /// <returns>路费（贯），简单 10 / 普通 20 / 困难 40 / 地狱 80。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="difficulty"/> 不在四档之内。</exception>
    /// <remarks>
    /// 支出系数的出口在 <see cref="DifficultyRates.ExpenseFactor"/>（0.6 / 1.0 / 1.1 / 1.3），
    /// 与本表**同档不同义**：路费 MUST NOT 被它乘（契约十三 §3.1 判据）。
    /// </remarks>
    public static int ExamTravelCostGuan(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => 10,
        Difficulty.Normal => 20,
        Difficulty.Hard => 40,
        Difficulty.Hell => 80,
        _ => throw new ArgumentOutOfRangeException(
            nameof(difficulty), difficulty, "未登记的难度（规格书 §11 共四档）。"),
    };

    /// <summary>
    /// 解试 / 省试的**基础**录取率（规格书 §6）。
    /// </summary>
    /// <param name="level">考试级别。</param>
    /// <returns>基础录取率。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> 不是解试或省试。</exception>
    public static decimal BaseRateOf(ExamLevel level) => level switch
    {
        ExamLevel.JieShi => JieShiBaseRate,
        ExamLevel.ShengShi => ShengShiBaseRate,
        _ => throw new ArgumentOutOfRangeException(
            nameof(level), level, "只有解试 / 省试按「录取率」判定（殿试走进甲率，规格书 §6）。"),
    };

    /// <summary>
    /// 学业系数：<c>0.4 + 0.012 × 学业</c>（规格书 §6）。
    /// </summary>
    /// <param name="study">学业（规格书 §4.1 的 0~100）。</param>
    /// <returns>学业系数。</returns>
    public static decimal StudyFactor(int study) => StudyFactorBase + (StudyFactorPerPoint * study);
}
