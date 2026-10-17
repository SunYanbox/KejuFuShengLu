using KFL.Core.Enums;

namespace KFL.Rules.Config;


/// <summary>
/// 生育与遗传登记（规格书 §9.4、§4.2；契约二十一 §3.2）——本特性的**唯一**声明处。
/// </summary>
/// <remarks>
/// <para>
/// 生育申请费、女方生育窗口、分娩死亡概率、遗传标准差基数与学业均值折半、遗传体质下限、
/// 夭折概率与判定次数。
/// </para>
/// <para>
/// **明确不含**：难度分档（一律经 <c>DifficultyRates</c> 与 <c>GameState.Difficulty</c> 读）、
/// 天赋 / 学业 / 体质 / 寿数的无父母参照者分布（复用 <see cref="AttributePolicy"/>）。
/// </para>
/// </remarks>
public static class ChildbirthPolicy
{
    /// <summary>生育申请费（贯；再乘支出系数）。</summary>
    public const int ChildbirthExpenseGuan = 10;

    /// <summary>女方生育窗口下界（含）。</summary>
    public const int FertilityAgeMin = 16;

    /// <summary>女方生育窗口上界（含）。</summary>
    public const int FertilityAgeMax = 45;

    /// <summary>分娩死亡概率（再乘难度分档）。</summary>
    public const decimal ChildbirthDeathChance = 0.05m;

    /// <summary>遗传标准差基数（<c>σ = |父 − 母| / 2 + 本值</c>）。</summary>
    public const int InheritanceSigmaBase = 5;

    /// <summary>遗传学业均值的折半系数。</summary>
    public const decimal StudyMeanHalving = 0.5m;

    /// <summary>遗传体质的下限。</summary>
    public const int InheritedHealthFloor = 20;

    /// <summary>单次夭折判定概率（再乘难度分档）。</summary>
    public const decimal InfantDeathChance = 0.10m;

    /// <summary>夭折判定次数（出生当月 + 满 1 岁 + 满 2 岁）。</summary>
    public const int InfantCheckCount = 3;

    /// <summary>夭折判定的年龄豁免：<c>AgeAt &gt;= 本值</c> 不再判定。</summary>
    public const int InfantExemptAge = 3;

    /// <summary>
    /// 夭折概率的难度分档（规格书 §9.4 明示端点：简单 5% / 地狱 12%；普通 10%）。
    /// </summary>
    /// <param name="difficulty">难度。</param>
    /// <returns>概率乘数。</returns>
    /// <remarks>
    /// <b>Must NOT 改用 <c>DifficultyRates.NegativeEventFactor</c></b>：后者的地狱值为 <c>1.4</c>，
    /// 会得到 14%，与规格书 §9.4 明示的「地狱 12%」冲突。困难档（<c>1.1</c>）无工件依据，
    /// 取普通与地狱之间的插值，已在 007 的 Phase 9 回写登记中列出待规格书 §17 裁决。
    /// </remarks>
    public static decimal InfantDeathFactor(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => 0.5m,
        Difficulty.Normal => 1.0m,
        Difficulty.Hard => 1.1m,
        Difficulty.Hell => 1.2m,
        _ => throw new ArgumentOutOfRangeException(
            nameof(difficulty), difficulty, "未知难度（data-model §2.2 共四个取值）。"),
    };
}
