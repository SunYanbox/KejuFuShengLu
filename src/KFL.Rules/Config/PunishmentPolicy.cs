using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Rules.Config;

/// <summary>刑罚的执行形态（规格书 §7.5；契约十七 §2.2）。</summary>
/// <remarks>
/// 「死刑立即执行」**不是**服刑的特殊时长——它不建立任何服刑计时（<see cref="PunishmentPolicy"/> 的死刑格
/// `SentenceMonths == 0`），故必须是独立字段而不是 `SentenceMonths == int.MaxValue` 一类的编码。
/// </remarks>
public enum ExecutionKind
{
    /// <summary>不执行死刑（服刑格 / 罚金格）。</summary>
    None = 0,

    /// <summary>当事人死刑，立即执行：置已亡、归档、进族谱（契约十六 §6.1）。</summary>
    ImmediateSentence = 1,
}

/// <summary>罚金的计法（规格书 §7.5；契约十七 §2.1）。</summary>
public enum FineKind
{
    /// <summary>该格不罚金（简单档解试）。</summary>
    None = 0,

    /// <summary>比例罚金：<c>比例 × 总资产 × 支出系数</c>，再受 <see cref="PunishmentPolicy.RatioFineCapGuan"/> 封顶。</summary>
    Ratio = 1,

    /// <summary>固定罚金：<c>固定额 × 支出系数</c>（不再受比例封顶）。</summary>
    Fixed = 2,
}

/// <summary>
/// 12 格惩罚矩阵中的**一格**（规格书 §7.3；契约十七 §2.1、research R-07）。
/// </summary>
/// <remarks>
/// <para>
/// 「无禁考」（简单档三格）与「只禁考」（困难 / 地狱的解试与省试）这两类**结构性**差异
/// 由字段取值表达，MUST NOT 由调用方按难度分支合出（口径 1）。
/// </para>
/// <para>
/// 死刑格的 <see cref="SentenceMonths"/> 恒为 <c>0</c> 且 <see cref="Execution"/> 为
/// <see cref="ExecutionKind.ImmediateSentence"/>；两者不可同时非空（口径 3）。
/// </para>
/// </remarks>
/// <param name="SentenceMonths">服刑月数（年数 × 12）；<c>0</c> = 不建立服刑计时。</param>
/// <param name="Execution">执行形态；死刑格为 <see cref="ExecutionKind.ImmediateSentence"/>。</param>
/// <param name="CollectivePunishment">是否连坐亲属；**仅**地狱殿试为真。</param>
/// <param name="Fine">罚金计法。</param>
/// <param name="FineRatio"><see cref="FineKind.Ratio"/> 时的比例；其余情况无意义。</param>
/// <param name="FineFixedGuan"><see cref="FineKind.Fixed"/> 时的固定额（贯）；其余情况无意义。</param>
/// <param name="RelativeBanMonths">相关亲属禁考月数；<c>0</c> = 不建立禁考计时。</param>
/// <param name="RelativePromotionBan">相关亲属是否同时禁升；<c>false</c> = 该格「只禁考」。</param>
/// <param name="FamilyWideBan">是否施加全族禁考禁升 <see cref="PunishmentPolicy.FamilyWideBanMonths"/> 月；仅地狱殿试为真。</param>
public readonly record struct PunishmentCell(
    int SentenceMonths,
    ExecutionKind Execution,
    bool CollectivePunishment,
    FineKind Fine,
    decimal FineRatio,
    Money FineFixedGuan,
    int RelativeBanMonths,
    bool RelativePromotionBan,
    bool FamilyWideBan);

/// <summary>
/// 惩罚政策（规格书 §7.3、§7.4、§7.5、§11；契约十七 §2）——这批数值的**唯一**声明处。
/// </summary>
/// <remarks>
/// <para>
/// 本类是「被抓之后」这条链路的数值单点：12 格矩阵、比例罚金封顶、全族禁考禁升月数、
/// 释放降级级数与服刑年 → 月的换算。读取时机是**每次被抓**（每次施加惩罚各一次）。
/// </para>
/// <para>
/// **明确不含**（契约十七 §2.2 口径 3、4；MUST NOT 另立第二处真源）：
/// 难度支出系数复用 <see cref="DifficultyRates.ExpenseFactor"/>；
/// 释放降级下界复用 <c>SalaryTable.LowestLevel</c>；
/// 「孩子未成年」判据复用 <c>AgeBracketPolicy</c>；服刑年 → 月复用
/// <see cref="IncomeRateTable.MonthsPerYear"/>。
/// </para>
/// <para>
/// **明确不含**（契约十七 §3）：成长 / 衰老 / 疾病 / 死亡的概率与年龄线、成就与绝嗣阈值、
/// 婚育与买人口数值、按调用方分支的开关。服刑 / 禁考 / 禁升的**建立与递减**是
/// <c>PunishmentImposer</c> / <c>PunishmentAdvance</c> 的行为，不属本表。
/// </para>
/// </remarks>
public static class PunishmentPolicy
{
    /// <summary>比例罚金的**全域封顶**（贯），对**最终金额**生效（先乘系数、再封顶）。</summary>
    public const int RatioFineCapGuan = 50000;

    /// <summary>全族禁考禁升的月数：**逐人**各写 48 月禁考 + 48 月禁升（Q3 = A；契约十六 §3 条款 3）。</summary>
    public const int FamilyWideBanMonths = 48;

    /// <summary>释放降级的级数：释放时 <c>Rank</c> 非 <c>null</c> 者降 1 级（已是 L18 则维持）。</summary>
    public const int ReleaseDemotionLevels = 1;

    /// <summary>
    /// 服刑月数换算：<c>年数 × 12</c>。**复用** <see cref="IncomeRateTable.MonthsPerYear"/>
    /// （唯一的 <c>12</c> 出处），MUST NOT 在此重复声明字面量。
    /// </summary>
    public static int MonthsPerYear => (int)IncomeRateTable.MonthsPerYear;

    /// <summary>
    /// 取 12 格矩阵中的一格（规格书 §7.3；契约十七 §2.1）。
    /// </summary>
    /// <param name="difficulty">难度（行）。</param>
    /// <param name="level">考试级别（列：殿试 / 省试 / 解试）。</param>
    /// <returns>该格的全部惩罚参数。</returns>
    /// <exception cref="ArgumentOutOfRangeException">组合不在 12 格之内。</exception>
    /// <remarks>
    /// 12 格**逐格列明**：困难与地狱的省试 / 解试三格数值完全相同，但仍各占一行（口径 2——
    /// 「异地同值」便于审计时逐格核对规格书 §7.3，MUST NOT 互相引用）。
    /// </remarks>
    public static PunishmentCell CellOf(Difficulty difficulty, ExamLevel level) => (difficulty, level) switch
    {
        // 简单档：无禁考（RelativeBanMonths = 0 ∧ RelativePromotionBan = false）。
        (Difficulty.Easy, ExamLevel.DianShi) => new(60, ExecutionKind.None, false, FineKind.Ratio, 0.02m, Money.Zero, 0, false, false),
        (Difficulty.Easy, ExamLevel.ShengShi) => new(36, ExecutionKind.None, false, FineKind.Ratio, 0.01m, Money.Zero, 0, false, false),
        (Difficulty.Easy, ExamLevel.JieShi) => new(12, ExecutionKind.None, false, FineKind.None, 0m, Money.Zero, 0, false, false),

        // 普通档：殿试 / 省试禁考禁升，解试无亲属禁考。
        (Difficulty.Normal, ExamLevel.DianShi) => new(180, ExecutionKind.None, false, FineKind.Ratio, 0.15m, Money.Zero, 36, true, false),
        (Difficulty.Normal, ExamLevel.ShengShi) => new(60, ExecutionKind.None, false, FineKind.Fixed, 0m, Money.FromGuan(200m), 24, true, false),
        (Difficulty.Normal, ExamLevel.JieShi) => new(36, ExecutionKind.None, false, FineKind.Fixed, 0m, Money.FromGuan(100m), 0, false, false),

        // 困难档：殿试当事人死刑；省试 / 解试只禁考（RelativePromotionBan = false）。
        (Difficulty.Hard, ExamLevel.DianShi) => new(0, ExecutionKind.ImmediateSentence, false, FineKind.Ratio, 0.20m, Money.Zero, 48, true, false),
        (Difficulty.Hard, ExamLevel.ShengShi) => new(240, ExecutionKind.None, false, FineKind.Ratio, 0.10m, Money.Zero, 36, false, false),
        (Difficulty.Hard, ExamLevel.JieShi) => new(120, ExecutionKind.None, false, FineKind.Ratio, 0.05m, Money.Zero, 36, false, false),

        // 地狱档：殿试死刑 + 连坐 + 全族 48 月；省试 / 解试只禁考。
        (Difficulty.Hell, ExamLevel.DianShi) => new(0, ExecutionKind.ImmediateSentence, true, FineKind.Ratio, 0.20m, Money.Zero, 48, true, true),
        (Difficulty.Hell, ExamLevel.ShengShi) => new(240, ExecutionKind.None, false, FineKind.Ratio, 0.10m, Money.Zero, 36, false, false),
        (Difficulty.Hell, ExamLevel.JieShi) => new(120, ExecutionKind.None, false, FineKind.Ratio, 0.05m, Money.Zero, 36, false, false),

        _ => throw new ArgumentOutOfRangeException(
            nameof(difficulty), (difficulty, level), "未登记的惩罚格（规格书 §7.3 共 12 格：四档难度 × 三级考试）。"),
    };
}
