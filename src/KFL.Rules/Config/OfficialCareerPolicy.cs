using KFL.Core.Enums;
using KFL.Infrastructure.Abstractions;

namespace KFL.Rules.Config;

/// <summary>
/// 官吏政策（规格书 §8.2；FR-013~FR-021；data-model §3.3）——§8.2 的**单一出处**。
/// </summary>
/// <remarks>
/// <para>
/// 本类是本特性全部**仕途数值**的唯一出处（SC-009）：待阙时长区间、初始官阶映射、
/// 政绩月增与上限、考课周期与升级概率、致仕年龄与半俸比例。
/// </para>
/// <para>
/// **明确不含**：禁升计时的建立与递减（逻辑轨 ⑥）、官名（规格书未定义，§17 禁止自创）、
/// 考课的事件文本；本特性**只消费** <c>StatusFlag.PromotionBanned</c> 与
/// <c>StatusTimers.PromotionBanRemainingMonths</c>。
/// </para>
/// </remarks>
public static class OfficialCareerPolicy
{
    /// <summary>待阙剩余月数下界（含）。</summary>
    public const int AwaitingPostMinMonths = 6;

    /// <summary>待阙剩余月数上界（含）。</summary>
    public const int AwaitingPostMaxMonths = 24;

    /// <summary>在任者每月政绩增量。</summary>
    public const int MeritPerMonth = 1;

    /// <summary>政绩上限（与 <c>AttributeLimits.Max</c> 同名不同义，两处 MUST NOT 互相引用）。</summary>
    public const int MeritMaximum = 100;

    /// <summary>考课周期（在职月数）。</summary>
    public const int AppraisalPeriodMonths = 36;

    /// <summary>考课基础升级概率。</summary>
    public const decimal PromotionBaseChance = 0.25m;

    /// <summary>每点政绩的升级概率加成（<c>0.3%</c>）。</summary>
    public const decimal PromotionChancePerMerit = 0.003m;

    /// <summary>升级概率封顶。</summary>
    public const decimal PromotionChanceCap = 0.70m;

    /// <summary>致仕年龄（生日当月即算）。</summary>
    public const int RetirementAge = 70;

    /// <summary>致仕后的俸禄比例（半俸）。</summary>
    public const decimal RetirementSalaryRatio = 0.50m;

    /// <summary>入仕途径 → 初始官阶级数（§8.2；FR-014）。</summary>
    /// <param name="track">入仕途径。</param>
    /// <returns>初始官阶，MUST 落在 <c>SalaryTable.HighestLevel~LowestLevel</c>。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="track"/> 不在四种途径之内。</exception>
    public static int InitialRankOf(AppointmentTrack track) => track switch
    {
        AppointmentTrack.FirstClass => 11,
        AppointmentTrack.SecondClass => 13,
        AppointmentTrack.ThirdClass => 15,
        AppointmentTrack.SpecialTribute => 18,
        _ => throw new ArgumentOutOfRangeException(
            nameof(track), track, "未登记的入仕途径（规格书 §8.2 共四种）。"),
    };

    /// <summary>
    /// 考课升级概率：<c>min(25% + 政绩 × 0.3%, 70%)</c>（§8.2；FR-017）。
    /// </summary>
    /// <param name="merit">政绩（<c>&gt;= 0</c>）。</param>
    /// <returns>升级概率。</returns>
    public static decimal PromotionChance(int merit) =>
        Math.Min(PromotionBaseChance + (merit * PromotionChancePerMerit), PromotionChanceCap);

    /// <summary>
    /// 掷一次待阙时长：<c>Next(6, 24 + 1)</c>——**整数均匀、含两端点**，恰好消耗 1 次 <c>Next</c>。
    /// </summary>
    /// <param name="random">随机来源。</param>
    /// <returns>落在 <see cref="AwaitingPostMinMonths"/>~<see cref="AwaitingPostMaxMonths"/> 的月数。</returns>
    public static int NextAwaitingPostMonths(IRandomService random) =>
        random.Next(AwaitingPostMinMonths, AwaitingPostMaxMonths + 1);
}
