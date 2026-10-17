namespace KFL.Rules.Config;

/// <summary>
/// 婚配登记（规格书 §9.1~§9.3；契约二十一 §3.1）——本特性的**唯一**声明处。
/// </summary>
/// <remarks>
/// <para>
/// 托媒费与成功率、候选者年龄差、提亲与捉婿的基础概率、捉婿支付区间与官员压力权重、
/// 功名分档阈值、买功名婚姻费率、阶层加权三档权重与 24 格聘礼 / 嫁妆金额表。
/// </para>
/// <para>
/// **明确不含**：支出系数与负面事件系数（复用 <see cref="DifficultyRates"/>）、
/// 年俸（复用 <see cref="SalaryTable"/>）、总资产（复用
/// <c>KFL.Rules/Economy/TotalAssetsCalculator</c>）。
/// </para>
/// </remarks>
public static class MarriagePolicy
{
    /// <summary>托媒费（贯；再乘支出系数）。</summary>
    public const int MatchmakerFeeGuan = 5;

    /// <summary>托媒成功概率。</summary>
    public const decimal MatchmakerSuccessChance = 0.50m;

    /// <summary>候选者与对象的年龄差上限（含）。</summary>
    public const int CandidateAgeSpread = 5;

    /// <summary>随机提亲的基础概率（再乘负面事件系数）。</summary>
    public const decimal ProposalBaseChance = 0.05m;

    /// <summary>榜下捉婿的基础概率（再乘系数、除以 <c>1 + P</c>）。</summary>
    public const decimal ImperialProposalBaseChance = 0.30m;

    /// <summary>捉婿支付区间下界（贯，含；整数均匀取样）。</summary>
    public const int ImperialPaymentMinGuan = 500;

    /// <summary>捉婿支付区间上界（贯，含）。</summary>
    public const int ImperialPaymentMaxGuan = 1000;

    /// <summary>官员压力权重：最低官阶（L18）。</summary>
    public const decimal PressureWeightBottom = 0.02m;

    /// <summary>官员压力权重：最高官阶（L1）。</summary>
    public const decimal PressureWeightTop = 1.50m;

    /// <summary>最高官阶级数（L1 最高）。</summary>
    public const int PressureHighestLevel = 1;

    /// <summary>最低官阶级数。</summary>
    public const int PressureLowestLevel = 18;

    /// <summary>功名分档：官员压力达到本值 ⇒ 举人。</summary>
    public const decimal SpouseDegreeScholarThreshold = 1m;

    /// <summary>功名分档：官员压力达到本值 ⇒ 贡士（**MUST NOT 进士**）。</summary>
    public const decimal SpouseDegreeTributeThreshold = 3m;

    /// <summary>买功名婚姻的费率（年俸 × 本比例）。</summary>
    public const decimal DegreeMarriageFeeRatio = 1.5m;

    /// <summary>降级为贡士后的配偶学业重掷均值。</summary>
    public const int TributeSpouseStudyMean = 60;

    /// <summary>降级为贡士后的配偶学业重掷标准差。</summary>
    public const int TributeSpouseStudySigma = 10;

    /// <summary>降级为举人后的配偶学业重掷均值。</summary>
    public const int JuRenSpouseStudyMean = 50;

    /// <summary>降级为举人后的配偶学业重掷标准差。</summary>
    public const int JuRenSpouseStudySigma = 10;

    /// <summary>平民档聘礼（贯）。</summary>
    public const int CommonerGiftGuan = 10;

    /// <summary>平民档嫁妆（贯）。</summary>
    public const int CommonerDowryGuan = 20;

    /// <summary>士人档聘礼（贯）。</summary>
    public const int ScholarGiftGuan = 20;

    /// <summary>士人档嫁妆（贯）。</summary>
    public const int ScholarDowryGuan = 30;

    /// <summary>仕族档聘礼（贯）。</summary>
    public const int OfficialGiftGuan = 50;

    /// <summary>仕族档嫁妆（贯）。</summary>
    public const int OfficialDowryGuan = 100;

    /// <summary>阶层加权分档：总资产下界（低于本值即「贫」）。</summary>
    public const int ClassWealthThresholdLow = 100;

    /// <summary>阶层加权分档：总资产上界（达本值即「富」）。</summary>
    public const int ClassWealthThresholdHighGuan = 1000;

    /// <summary>贫档阶层权重（顺序固定：平民 → 士人 → 仕族）。</summary>
    public static IReadOnlyList<decimal> ClassWeightsPoor { get; } = [0.70m, 0.25m, 0.05m];

    /// <summary>中档阶层权重（顺序固定：平民 → 士人 → 仕族）。</summary>
    public static IReadOnlyList<decimal> ClassWeightsMiddle { get; } = [0.45m, 0.40m, 0.15m];

    /// <summary>富档阶层权重（顺序固定：平民 → 士人 → 仕族）。</summary>
    public static IReadOnlyList<decimal> ClassWeightsRich { get; } = [0.20m, 0.50m, 0.30m];
}
