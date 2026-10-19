namespace KFL.Rules.Exam;

/// <summary>
/// 一次录取率计算的**全部来源与取值**（2026-10-19 需求 18：界面双击展示明细）。
/// </summary>
/// <remarks>
/// 只读快照：由 <see cref="ExamAdmission.Breakdown"/> 产出，MUST NOT 被界面改写；
/// 它不参与判定（判定仍用 <see cref="ExamAdmission.AdmissionRate"/> 的单值），
/// 但两者 MUST 用同一份数值来源，否则界面会显示一个与掷骰不一致的概率。
/// </remarks>
/// <param name="Level">考试级别。</param>
/// <param name="BaseRate">基础录取率。</param>
/// <param name="Study">当月学业。</param>
/// <param name="StudyFactor">学业系数（<c>0.4 + 0.012 × 学业</c>）。</param>
/// <param name="Officialdom">仕天赋。</param>
/// <param name="OfficialdomFactor">仕天赋系数（<c>1 + 仕 / 400</c>）。</param>
/// <param name="BribeMultiplier">贿赂乘数（<c>kⁿ</c>）。</param>
/// <param name="BribeCount">该级累计行贿次数。</param>
/// <param name="ShiBonus">仕族加成（1.1 或 1.0）。</param>
/// <param name="DebugBonus">调试覆盖加成（百分点；未用调试时为 0）。</param>
/// <param name="RawRate">封顶前的乘积。</param>
/// <param name="Rate">最终录取率（已封顶）。</param>
public readonly record struct ExamRateBreakdown(
    Core.Enums.ExamLevel Level,
    decimal BaseRate,
    int Study,
    decimal StudyFactor,
    int Officialdom,
    decimal OfficialdomFactor,
    decimal BribeMultiplier,
    int BribeCount,
    decimal ShiBonus,
    decimal DebugBonus,
    decimal RawRate,
    decimal Rate);
