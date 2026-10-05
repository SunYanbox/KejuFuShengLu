namespace KFL.Core.ValueObjects;

/// <summary>
/// 状态计时（规格书 §7.5：服刑以月为单位；禁考与服刑独立并行计时）。
/// </summary>
/// <remarks>
/// <para>
/// **不变量**：三个字段非空时 MUST <c>&gt;= 0</c>；且**仅在对应状态位为真时可非空**——
/// 后一条一致性由 <see cref="Entities.Person"/> 承担（见 <c>Person.Timers</c> / <c>Person.Status</c>）。
/// </para>
/// <para>
/// **明确不含**：计时递减逻辑（阶段⑥）。
/// </para>
/// </remarks>
public readonly record struct StatusTimers
{
    /// <summary>构造并校验。</summary>
    /// <param name="sentenceRemainingMonths">服刑剩余月数。</param>
    /// <param name="examBanRemainingMonths">禁考剩余月数。</param>
    /// <param name="promotionBanRemainingMonths">禁升剩余月数。</param>
    /// <exception cref="ArgumentOutOfRangeException">任一非空值 &lt; 0。</exception>
    public StatusTimers(
        int? sentenceRemainingMonths = null,
        int? examBanRemainingMonths = null,
        int? promotionBanRemainingMonths = null)
    {
        EnsureNonNegative(sentenceRemainingMonths, nameof(sentenceRemainingMonths));
        EnsureNonNegative(examBanRemainingMonths, nameof(examBanRemainingMonths));
        EnsureNonNegative(promotionBanRemainingMonths, nameof(promotionBanRemainingMonths));

        SentenceRemainingMonths = sentenceRemainingMonths;
        ExamBanRemainingMonths = examBanRemainingMonths;
        PromotionBanRemainingMonths = promotionBanRemainingMonths;
    }

    /// <summary>服刑剩余月数。</summary>
    public int? SentenceRemainingMonths { get; }

    /// <summary>禁考剩余月数。</summary>
    public int? ExamBanRemainingMonths { get; }

    /// <summary>禁升剩余月数。</summary>
    public int? PromotionBanRemainingMonths { get; }

    private static void EnsureNonNegative(int? value, string paramName)
    {
        if (value is < 0)
        {
            throw new ArgumentOutOfRangeException(
                paramName, value, "剩余月数 MUST >= 0（规格书 §7.5）。");
        }
    }
}
