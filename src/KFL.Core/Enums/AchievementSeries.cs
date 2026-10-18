namespace KFL.Core.Enums;

/// <summary>
/// 成就的七个系列（规格书 §14.1 的七个系列标题；data-model §2.1）。
/// </summary>
/// <remarks>
/// <para>
/// **系列归属的唯一真源**是规格书 §14.1 的七个系列标题；每个系列覆盖一段**连续**的
/// <see cref="AchievementId"/> 值区间，区间两两不交、并集恰为 `1~68`。
/// </para>
/// <para>
/// **计数不变量**：<c>12 + 8 + 11 + 17 + 11 + 4 + 5 == 68</c>，由
/// <c>AchievementCatalogTests</c> 逐系列断言（MUST NOT 只在别处写死数字）。
/// </para>
/// </remarks>
public enum AchievementSeries
{
    /// <summary>初露锋芒（ACH-001~012，12 条）。</summary>
    Emerging = 0,

    /// <summary>科名累进（ACH-013~020，8 条）。</summary>
    ExamProgression = 1,

    /// <summary>宦海沉浮（ACH-021~031，11 条）。</summary>
    OfficialCareer = 2,

    /// <summary>宗族血脉（ACH-032~048，17 条）。</summary>
    Lineage = 3,

    /// <summary>覆巢之下（ACH-049~059，11 条）。</summary>
    CollectiveRuin = 4,

    /// <summary>饥馑求生（ACH-060~063，4 条）。</summary>
    Famine = 5,

    /// <summary>调试秘辛（ACH-064~068，5 条）。</summary>
    DebugSecrets = 6,
}
