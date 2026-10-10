namespace KFL.Core.ValueObjects;

/// <summary>
/// 状态计时（规格书 §7.5：服刑以月为单位；禁考与服刑独立并行计时）。
/// </summary>
/// <remarks>
/// <para>
/// **不变量**：四个字段非空时 MUST <c>&gt;= 0</c>；且**仅在对应状态位为真时可非空**——
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
    /// <exception cref="ArgumentOutOfRangeException">任一非空值 &lt; 0。</exception>
    public StatusTimers(
        int? sentenceRemainingMonths = null,
        int? examBanRemainingMonths = null,
        int? promotionBanRemainingMonths = null,
        int? awaitingPostRemainingMonths = null)
    {
        EnsureNonNegative(sentenceRemainingMonths, nameof(sentenceRemainingMonths));
        EnsureNonNegative(examBanRemainingMonths, nameof(examBanRemainingMonths));
        EnsureNonNegative(promotionBanRemainingMonths, nameof(promotionBanRemainingMonths));
        EnsureNonNegative(awaitingPostRemainingMonths, nameof(awaitingPostRemainingMonths));

        SentenceRemainingMonths = sentenceRemainingMonths;
        ExamBanRemainingMonths = examBanRemainingMonths;
        PromotionBanRemainingMonths = promotionBanRemainingMonths;
        AwaitingPostRemainingMonths = awaitingPostRemainingMonths;
    }

    /// <summary>服刑剩余月数。</summary>
    public int? SentenceRemainingMonths { get; }

    /// <summary>禁考剩余月数。</summary>
    public int? ExamBanRemainingMonths { get; }

    /// <summary>禁升剩余月数。</summary>
    public int? PromotionBanRemainingMonths { get; }

    /// <summary>待阙剩余月数；递减到 0 的当月授官（对应 <c>StatusFlag.AwaitingPost</c>）。</summary>
    public int? AwaitingPostRemainingMonths { get; }

    private static void EnsureNonNegative(int? value, string paramName)
    {
        if (value is < 0)
        {
            throw new ArgumentOutOfRangeException(
                paramName, value, "剩余月数 MUST >= 0（规格书 §7.5）。");
        }
    }
}
