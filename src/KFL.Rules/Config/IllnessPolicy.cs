namespace KFL.Rules.Config;

/// <summary>
/// 疾病登记（规格书 §4.3；契约二十一 §2）——本特性的**唯一**声明处。
/// </summary>
/// <remarks>
/// <para>
/// 感染概率的线性插值端点、四组主导天赋乘数、每月体质扣减与药费、清除概率系数、
/// 三段年龄增益、疗程月数与程度的万分之换算。
/// </para>
/// <para>
/// **明确不含**：难度负面事件系数——患病概率 MUST NOT 引用
/// <c>DifficultyRates.NegativeEventFactor</c>（本类 MUST NOT 出现该符号，SC-014）。
/// </para>
/// </remarks>
public static class IllnessPolicy
{
    /// <summary>体质满值（100）时的月患病率。</summary>
    public const decimal InfectionChanceAtFullHealth = 0.0025m;

    /// <summary>体质下限（1）时的月患病率。</summary>
    public const decimal InfectionChanceAtFloorHealth = 0.40m;

    /// <summary>插值分母（<c>体质上界 − 体质下界</c>）。</summary>
    public const int InterpolationSpan = 99;

    /// <summary>概率入参的体质下界。</summary>
    public const int HealthFloor = 1;

    /// <summary>概率入参的体质上界。</summary>
    public const int HealthCeiling = 100;

    /// <summary>农出身主导天赋乘数：感染（<c>m_p</c>）。</summary>
    public const decimal TalentMultiplierFarmer = 0.80m;

    /// <summary>农出身主导天赋乘数：清除（<c>m_q</c>）。</summary>
    public const decimal FarmerRecoveryMultiplier = 1.20m;

    /// <summary>仕出身主导天赋乘数：感染。</summary>
    public const decimal TalentMultiplierScholar = 0.95m;

    /// <summary>仕出身主导天赋乘数：清除。</summary>
    public const decimal ScholarRecoveryMultiplier = 1.10m;

    /// <summary>工出身主导天赋乘数：感染。</summary>
    public const decimal TalentMultiplierArtisan = 1.10m;

    /// <summary>工出身主导天赋乘数：清除。</summary>
    public const decimal ArtisanRecoveryMultiplier = 1.10m;

    /// <summary>商出身主导天赋乘数：感染。</summary>
    public const decimal TalentMultiplierMerchant = 1.05m;

    /// <summary>商出身主导天赋乘数：清除。</summary>
    public const decimal MerchantRecoveryMultiplier = 0.95m;

    /// <summary>患病每月体质扣减的基数。</summary>
    public const int SeverityLossBase = 2;

    /// <summary>患病每月体质扣减的程度系数（<c>基数 + 系数 × s</c>）。</summary>
    public const int SeverityLossSpan = 8;

    /// <summary>药费基数（贯）。</summary>
    public const decimal MedicineBaseGuan = 0.05m;

    /// <summary>药费的程度系数（贯；<c>基数 + 系数 × s</c>）。</summary>
    public const decimal MedicineSeverityGuan = 0.45m;

    /// <summary>未治疗时的清除系数（<c>体质 × 本值 / 100</c>）。</summary>
    public const decimal BaseRecoveryCoefficient = 1.00m;

    /// <summary>疗程中的清除系数。</summary>
    public const decimal TreatedRecoveryCoefficient = 1.10m;

    /// <summary>疗程中的清除加项。</summary>
    public const decimal TreatedRecoveryOffset = 0.20m;

    /// <summary>疗程月数。</summary>
    public const int TreatmentMonths = 3;

    /// <summary>儿童段（<c>3 &lt;= AgeAt &lt; 12</c>）的清除增益。</summary>
    public const decimal ClearBonusChild = 0.60m;

    /// <summary>青年段（<c>12 &lt;= AgeAt &lt; 24</c>）的清除增益。</summary>
    public const decimal ClearBonusYouth = 0.30m;

    /// <summary>儿童段下界（含）。</summary>
    public const int ClearBonusBoundaryChild = 3;

    /// <summary>青年段下界（含）。</summary>
    public const int Youth = 12;

    /// <summary>成人段下界（含；此年龄起无清除增益）。</summary>
    public const int Adult = 24;

    /// <summary>免病年龄：<c>AgeAt &lt; 本值</c> 不掷感染判定且**零随机消费**。</summary>
    public const int IllnessExemptAge = 3;

    /// <summary>疾病程度 <c>s</c> 与存档字段（万分之整数）的换算单点。</summary>
    public const int SeverityBasisPointsScale = 10000;

    /// <summary>把疾病程度 <c>s</c>（<c>0~1</c>）换算成万分之整数（四舍五入、远离零）。</summary>
    /// <param name="severity">疾病程度。</param>
    /// <returns>万分之整数。</returns>
    public static int ToBasisPoints(double severity) =>
        (int)Math.Round(severity * SeverityBasisPointsScale, MidpointRounding.AwayFromZero);

    /// <summary>把万分之整数换算回疾病程度 <c>s</c>。</summary>
    /// <param name="basisPoints">万分之整数。</param>
    /// <returns>疾病程度。</returns>
    public static double ToSeverity(int basisPoints) =>
        (double)basisPoints / SeverityBasisPointsScale;
}
