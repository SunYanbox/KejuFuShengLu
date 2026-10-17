namespace KFL.Core.ValueObjects;

/// <summary>
/// 状态计时（规格书 §7.5：服刑以月为单位；禁考与服刑独立并行计时）。
/// </summary>
/// <remarks>
/// <para>
/// **不变量**：四个「剩余月数」字段非空时 MUST <c>&gt;= 0</c>，疾病程度
/// <see cref="IllnessSeverityBasisPoints"/>  MUST 落在 <c>0~10000</c>；且**仅在对应状态位为真时可非空**——
/// 后一条一致性由 <see cref="Entities.Person"/> 承担（见 <c>Person.Timers</c> / <c>Person.Status</c>）。
/// </para>
/// <para>
/// <see cref="AwaitingPostRemainingMonths"/>（待阙剩余月数）是 **003 的新增项**，
/// 对应状态位 <c>StatusFlag.AwaitingPost</c>；递减到 <c>0</c> 的当月授官，
/// 故 MUST NOT 以「<c>0</c> + 状态位为真」的形态存续（FR-013）。
/// </para>
/// <para>
/// **明确不含**：计时递减逻辑（服刑与禁考/禁升属逻辑轨 ⑥、待阙属逻辑轨 ③ 的
/// <c>OfficialCareerAdvance</c>），以及待阙时长的 6~24 区间（属 <c>OfficialCareerPolicy</c>）。
/// </para>
/// </remarks>
public readonly record struct StatusTimers
{
    /// <summary>构造并校验。</summary>
    /// <param name="sentenceRemainingMonths">服刑剩余月数。</param>
    /// <param name="examBanRemainingMonths">禁考剩余月数。</param>
    /// <param name="promotionBanRemainingMonths">禁升剩余月数。</param>
    /// <param name="awaitingPostRemainingMonths">待阙剩余月数。</param>
    /// <param name="illnessSeverityBasisPoints">
    /// 疾病程度 <c>s</c> 的万分之整数（007 新增，<c>0~10000</c>）。
    /// </param>
    /// <param name="treatmentRemainingMonths">
    /// 疗程剩余月数（007 新增，<c>0~3</c>）。
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// 任一剩余月数非空且 &lt; 0，或疾病程度非空且不在 <c>0~10000</c>。
    /// </exception>
    /// <remarks>
    /// 两个 007 新增项 MUST 追加在**尾部**（位置式存档的 <c>Skip</c> 序列依赖次序）。
    /// </remarks>
    public StatusTimers(
        int? sentenceRemainingMonths = null,
        int? examBanRemainingMonths = null,
        int? promotionBanRemainingMonths = null,
        int? awaitingPostRemainingMonths = null,
        int? illnessSeverityBasisPoints = null,
        int? treatmentRemainingMonths = null)
    {
        EnsureNonNegative(sentenceRemainingMonths, nameof(sentenceRemainingMonths));
        EnsureNonNegative(examBanRemainingMonths, nameof(examBanRemainingMonths));
        EnsureNonNegative(promotionBanRemainingMonths, nameof(promotionBanRemainingMonths));
        EnsureNonNegative(awaitingPostRemainingMonths, nameof(awaitingPostRemainingMonths));
        EnsureSeverityInRange(illnessSeverityBasisPoints, nameof(illnessSeverityBasisPoints));

        SentenceRemainingMonths = sentenceRemainingMonths;
        ExamBanRemainingMonths = examBanRemainingMonths;
        PromotionBanRemainingMonths = promotionBanRemainingMonths;
        AwaitingPostRemainingMonths = awaitingPostRemainingMonths;
        IllnessSeverityBasisPoints = illnessSeverityBasisPoints;
        TreatmentRemainingMonths = treatmentRemainingMonths;
    }

    /// <summary>服刑剩余月数。</summary>
    public int? SentenceRemainingMonths { get; }

    /// <summary>禁考剩余月数。</summary>
    public int? ExamBanRemainingMonths { get; }

    /// <summary>禁升剩余月数。</summary>
    public int? PromotionBanRemainingMonths { get; }

    /// <summary>待阙剩余月数；递减到 0 的当月授官（对应 <c>StatusFlag.AwaitingPost</c>）。</summary>
    public int? AwaitingPostRemainingMonths { get; }

    /// <summary>
    /// 疾病程度 <c>s</c> 的万分之整数（<c>0~10000</c>）；与 <c>StatusFlag.Ill</c> **同生同灭**。
    /// </summary>
    /// <remarks>
    /// <c>s</c> 只在确诊当月掷一次，此后不逐月重掷；<c>null</c> 与
    /// <see cref="TreatmentRemainingMonths"/> 为 <c>null</c> 的组合即「未患病」。
    /// </remarks>
    public int? IllnessSeverityBasisPoints { get; }

    /// <summary>
    /// 疗程剩余月数（<c>0~3</c>）；<c>null</c> = 未被治疗。
    /// </summary>
    /// <remarks>
    /// 患病**次月**起算（确诊当月是宽限月：不付药费、不减体质、不掷清除）。
    /// 未治疗的疾病不过期，疗程结束后未愈可再次发起。
    /// </remarks>
    public int? TreatmentRemainingMonths { get; }

    private static void EnsureNonNegative(int? value, string paramName)
    {
        if (value is < 0)
        {
            throw new ArgumentOutOfRangeException(
                paramName, value, "剩余月数 MUST >= 0（规格书 §7.5）。");
        }
    }

    private static void EnsureSeverityInRange(int? value, string paramName)
    {
        if (value is < 0 or > 10000)
        {
            throw new ArgumentOutOfRangeException(
                paramName, value, "疾病程度 MUST 在 0~10000 之间（规格书 §4.3）。");
        }
    }
}
