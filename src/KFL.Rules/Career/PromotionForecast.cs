using KFL.Core.Entities;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;

namespace KFL.Rules.Career;

/// <summary>
/// 成员「升官概率」的**只读预测**（2026-10-19 第九轮玩家反馈：需要地方查看成员每月升官职的概率与计算详情）。
/// </summary>
/// <remarks>
/// <para>
/// 它复刻 <see cref="OfficialCareerAdvance"/> 考课判定所用的**全部输入**，但不掷骰、不写状态、不消费随机，
/// 因此可以与真实轨迹并行显示而不扰动确定性（规格书 §17 第九轮裁决）。
/// </para>
/// <para>
/// 口径与 <c>TryPromote</c> 严格一致：先看功名职级上限（到顶者不掷骰），再看
/// <see cref="OfficialCareerPolicy.PromotionChance"/>（政绩线性加成并封顶 0.70）。
/// </para>
/// </remarks>
/// <param name="Rank">当前官阶。</param>
/// <param name="Merit">当前政绩。</param>
/// <param name="Chance">若本月参与考课，升一级的概率（已含政绩加成与封顶）。</param>
/// <param name="CapLevel">功名允许升到的最高品位（数字越小官越大）。</param>
/// <param name="AtLevelCap">是否已到功名上限或最高品 L1（到顶者不掷骰）。</param>
/// <param name="NextLevel">晋升后的官阶；到顶时为 <c>null</c>。</param>
/// <param name="PromotionBanned">是否处于「禁升」处罚期。</param>
/// <param name="ServingSentence">是否正在服刑（仕途整体暂停）。</param>
/// <param name="MonthsInOffice">当前官阶的连续在职月数。</param>
/// <param name="MonthsUntilAppraisal">距下一次考课还差几个月（<c>AppraisalPeriodMonths - MonthsInOffice</c>，到点或超额为 0）。</param>
/// <param name="CanPromoteThisMonth">本月是否**可能**晋升（未到顶 ∧ 未禁升 ∧ 未服刑 ∧ 已到考课周期）。</param>
public sealed record PromotionForecast(
    OfficialRank Rank,
    int Merit,
    decimal Chance,
    int CapLevel,
    bool AtLevelCap,
    OfficialRank? NextLevel,
    bool PromotionBanned,
    bool ServingSentence,
    int MonthsInOffice,
    int MonthsUntilAppraisal,
    bool CanPromoteThisMonth)
{
    /// <summary>推算选中成员的升官概率明细；无官职（<see cref="Person.Rank"/> 为 <c>null</c>）时返回 <c>null</c>。</summary>
    /// <param name="person">成员档案（只读）。</param>
    /// <returns>升官概率预测；该成员无官职时为 <c>null</c>。</returns>
    public static PromotionForecast? Of(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        if (person.Rank is not { } rank)
        {
            return null;
        }

        // 与 OfficialCareerAdvance.TryPromote 同一口径：功名只限制「能升到哪一品」。
        var cap = OfficialCareerPolicy.PromotionCapLevelOf(
            person.CurrentDegree,
            person.DegreeHistory.Count > 0 ? person.DegreeHistory[^1].Class : null);

        var atCap = rank.Level <= SalaryTable.HighestLevel || rank.Level - 1 < cap;
        var banned = person.Status.HasFlag(KFL.Core.Enums.StatusFlag.PromotionBanned);
        var serving = person.Status.HasFlag(KFL.Core.Enums.StatusFlag.ServingSentence);
        var monthsLeft = Math.Max(0, OfficialCareerPolicy.AppraisalPeriodMonths - person.MonthsInOffice);

        return new PromotionForecast(
            Rank: rank,
            Merit: person.Merit,
            Chance: OfficialCareerPolicy.PromotionChance(person.Merit),
            CapLevel: cap,
            AtLevelCap: atCap,
            NextLevel: atCap ? null : new OfficialRank(rank.Level - 1),
            PromotionBanned: banned,
            ServingSentence: serving,
            MonthsInOffice: person.MonthsInOffice,
            MonthsUntilAppraisal: monthsLeft,
            CanPromoteThisMonth: !atCap && !banned && !serving && monthsLeft == 0);
    }
}
