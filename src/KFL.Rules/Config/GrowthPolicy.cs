namespace KFL.Rules.Config;

/// <summary>
/// 成长与体质登记（规格书 §4.3、§5.4；契约二十一 §1）——本特性的**唯一**声明处。
/// </summary>
/// <remarks>
/// <para>
/// 学业月增、名师加成与月费、士出身藏书加成、遗忘、患病期减半、体质自然恢复、
/// 饥馑体质扣减与死亡判定。
/// </para>
/// <para>
/// **明确不含**：<c>0~100</c> 的值域（复用 <c>KFL.Core/Config/AttributeLimits.Max</c>，
/// MUST NOT 在此另立上限）、成年边界（经 <c>GameConfig.Ages</c> / <c>AgeBracketPolicy</c> 读）。
/// </para>
/// </remarks>
public static class GrowthPolicy
{
    /// <summary>读书成员每月学业增量。</summary>
    public const decimal StudyPerMonth = 1.5m;

    /// <summary>延请名师后每月的额外学业增量。</summary>
    public const decimal TutorStudyBonusPerMonth = 2.25m;

    /// <summary>名师月费（贯；再乘支出系数）。</summary>
    public const int TutorFeeGuanPerMonth = 5;

    /// <summary>士出身藏书加成（乘区）。</summary>
    public const decimal ScholarStudyMultiplier = 1.10m;

    /// <summary>遗忘月扣减（**正值**，写入时取负）。</summary>
    public const decimal ForgetPerMonth = 0.5m;

    /// <summary>遗忘的年龄门槛：<c>AgeAt &gt; 本值</c> 才遗忘（16 岁不遗忘、17 岁起遗忘）。</summary>
    public const int ForgetAgeThreshold = 16;

    /// <summary>患病期学业变化的除数（减半）。</summary>
    public const decimal IllnessStudyPenaltyDivisor = 2m;

    /// <summary>体质每月自然恢复量。</summary>
    public const int HealthRecoverPerMonth = 1;

    /// <summary>饥馑严重期每月体质扣减量（正值）。</summary>
    public const int FamineHealthLossPerMonth = 10;

    /// <summary>饥馑严重期每人每月的死亡判定概率。</summary>
    public const decimal FamineDeathChance = 0.05m;
}
