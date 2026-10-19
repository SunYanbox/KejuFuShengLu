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
        AppointmentTrack.JuRen => 17,
        _ => throw new ArgumentOutOfRangeException(
            nameof(track), track, "未登记的入仕途径（规格书 §8.2 共五种）。"),
    };

    /// <summary>
    /// 功名对应的**职级上限**（能升到的最高品；数字越小官越大）。
    /// </summary>
    /// <remarks>
    /// 2026-10-19 平衡性裁决：功名只限制职级上限（影响俸禄上限），不阻止继续科举。
    /// 举人 L17 起步、最高升到 L16；特奏名 L18 起步、最高 L15；三甲 / 二甲 / 一甲依次到 L12 / L8 / L4。
    /// </remarks>
    /// <param name="degree">当前功名。</param>
    /// <param name="imperialClass">进士甲第（非进士传 <c>null</c>）。</param>
    /// <returns>可晋升到的最小级数。</returns>
    public static int PromotionCapLevelOf(DegreeLevel degree, ImperialClass? imperialClass) => degree switch
    {
        DegreeLevel.BaiShen => SalaryTable.LowestLevel,
        DegreeLevel.JuRen => 16,
        DegreeLevel.GongShi => 13,
        DegreeLevel.JinShi => imperialClass switch
        {
            ImperialClass.FirstClass => 4,
            ImperialClass.SecondClass => 8,
            _ => 12,
        },
        _ => SalaryTable.LowestLevel,
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
