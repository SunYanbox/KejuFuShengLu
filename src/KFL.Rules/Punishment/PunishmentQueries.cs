using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Bribery;

namespace KFL.Rules.Punishment;

/// <summary>
/// 贿赂与惩罚的**查询面**（规格书 FR-029、FR-030；契约十六 §8）——全纯函数。
/// </summary>
/// <remarks>
/// <para>
/// 界面与调试入口 MUST 只经本面取值；本类 MUST NOT 改动任何状态、MUST NOT 消费随机
/// （SC-008：调用前后 <c>GameState</c> 逐字段差异项 = 0、随机消费 0）。
/// </para>
/// <para>
/// 窗口、金额、概率、触顶判据一律转调 <see cref="BribeRules"/>，本类 MUST NOT 复制公式。
/// </para>
/// </remarks>
public static class PunishmentQueries
{
    /// <summary>该日期是否落在该级别的行贿窗口内（转调 <see cref="BribeRules.IsWithinWindow"/>）。</summary>
    /// <param name="date">待判定的月份。</param>
    /// <param name="level">考试级别。</param>
    /// <returns>在窗口内为 <see langword="true"/>。</returns>
    public static bool IsWithinBribeWindow(GameDate date, ExamLevel level) =>
        BribeRules.IsWithinWindow(date, level);

    /// <summary>当前的应付金额 / 被抓概率 / 是否已触顶 / 拒绝原因（纯查询）。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="examinee">应考人。</param>
    /// <param name="level">考试级别。</param>
    /// <returns>可行性快照。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 或 <paramref name="examinee"/> 为 <see langword="null"/>。</exception>
    public static BribeFeasibility Of(GameState state, Person examinee, ExamLevel level)
    {
        BribeRules.CanBribe(state, examinee, level, out var feasibility);
        return feasibility;
    }

    /// <summary>该级别的最终录取率 / 进甲率是否已触顶（转调 <see cref="BribeRules.IsLevelCapReached"/>）。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="examinee">应考人。</param>
    /// <param name="level">考试级别。</param>
    /// <returns>已触顶为 <see langword="true"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 或 <paramref name="examinee"/> 为 <see langword="null"/>。</exception>
    public static bool IsLevelCapReached(GameState state, Person examinee, ExamLevel level) =>
        BribeRules.IsLevelCapReached(state, examinee, level);

    /// <summary>家族**累计行贿次数**：全部成员（**含归档**）三个级别计数之和（FR-030）。</summary>
    /// <param name="family">家族。</param>
    /// <returns>累计次数。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="family"/> 为 <see langword="null"/>。</exception>
    public static int FamilyCumulativeBribes(Family family)
    {
        ArgumentNullException.ThrowIfNull(family);

        var total = 0;
        foreach (var member in family.Members)
        {
            total += member.JieShiBribeCount;
            total += member.ShengShiBribeCount;
            total += member.DianShiBribeCount;
        }

        return total;
    }

    /// <summary>
    /// 该成员是否「及第全程未行贿」（FR-030「清白传家」）：
    /// 存在 <see cref="DegreeChangeCause.ExamPass"/> 的进士记录 ∧ 三级别计数皆为 0。
    /// </summary>
    /// <param name="person">成员。</param>
    /// <returns>满足派生谓词为 <see langword="true"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 为 <see langword="null"/>。</exception>
    /// <remarks>
    /// **派生谓词，MUST NOT 入档**（plan.md 勘误 #2 / #3）：它可由功名变迁史与三个计数完全重算，
    /// 落盘会成为第二真源。
    /// </remarks>
    public static bool WasBribeFreeAtImperialPass(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        var passedImperial = false;
        foreach (var record in person.DegreeHistory)
        {
            if (record.Cause == DegreeChangeCause.ExamPass && record.Level == DegreeLevel.JinShi)
            {
                passedImperial = true;
                break;
            }
        }

        return passedImperial
            && person.JieShiBribeCount == 0
            && person.ShengShiBribeCount == 0
            && person.DianShiBribeCount == 0;
    }

    /// <summary>
    /// 「身首异处」的派生事实：该成员**本人**是否被处死（FR-030）。
    /// </summary>
    /// <param name="person">成员。</param>
    /// <returns>已亡位为真时为 <see langword="true"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 为 <see langword="null"/>。</exception>
    /// <remarks>
    /// **状态派生，不经当月快照的六类事实行**（契约十六 §8/§9）：规格书 §7.5 的死刑**立即执行**，
    /// 规则层不存在「已判未决」的持久状态，故 <see cref="StatusFlag.Deceased"/> 即事实。
    /// 被**连坐**的亲属之死则由 <see cref="CollectivePunishmentFact.Outcome"/> 的
    /// <see cref="CollectiveOutcome.Executed"/> 表达。⑧ 与 U2 MUST 只经本入口取值。
    /// </remarks>
    public static bool WasExecutedByState(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        return person.Status.HasFlag(StatusFlag.Deceased);
    }

    /// <summary>
    /// 一名成员的惩罚状态面（FR-029②）：三个计时的**剩余月数**与「是否有官职」。
    /// </summary>
    /// <param name="person">待查询的成员。</param>
    /// <returns>剩余月数逐项对应 <c>StatusTimers</c>；<c>null</c> = 该计时不存在。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 为 <see langword="null"/>。</exception>
    public static PunishmentStatus StatusOf(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        var timers = person.Timers;
        return new PunishmentStatus(
            timers.SentenceRemainingMonths,
            timers.ExamBanRemainingMonths,
            timers.PromotionBanRemainingMonths,
            person.Rank is not null);
    }

    /// <summary>
    /// 给定年月的**在册服刑人数**（FR-029④）——界面「囹圄之灾」与快照的同一入口。
    /// </summary>
    /// <param name="family">家族。</param>
    /// <param name="date">查询年月。</param>
    /// <returns>在册且在服刑中的成员数。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="family"/> 为 <see langword="null"/>。</exception>
    /// <remarks>
    /// 只读计数：服刑者**保留在家族列表**上（FR-017），但归档（已亡 / 外嫁）者不在此列。
    /// <paramref name="date"/> 只用于调用点可读性——本查询是**当刻**状态快照，不做时间重放。
    /// </remarks>
    public static int PrisonerCount(Family family, GameDate date)
    {
        ArgumentNullException.ThrowIfNull(family);

        var count = 0;
        foreach (var member in family.RegisteredMembers)
        {
            if (member.Status.HasFlag(StatusFlag.ServingSentence))
            {
                count++;
            }
        }

        return count;
    }
}

/// <summary>
/// 一名成员的惩罚状态快照（FR-029②；契约十六 §8）——三计时剩余月数 + 是否有官职。
/// </summary>
/// <param name="SentenceRemainingMonths">服刑剩余月数；<c>null</c> = 未在服刑。</param>
/// <param name="ExamBanRemainingMonths">禁考剩余月数；<c>null</c> = 未禁考。</param>
/// <param name="PromotionBanRemainingMonths">禁升剩余月数；<c>null</c> = 未禁升。</param>
/// <param name="HasOffice">当刻是否有官职（<c>Rank</c> 非 <c>null</c>）。</param>
public readonly record struct PunishmentStatus(
    int? SentenceRemainingMonths,
    int? ExamBanRemainingMonths,
    int? PromotionBanRemainingMonths,
    bool HasOffice)
{
    /// <summary>是否在服刑中（计时与状态位同真）。</summary>
    public bool IsServingSentence => SentenceRemainingMonths is not null;

    /// <summary>是否在禁考期。</summary>
    public bool IsExamBanned => ExamBanRemainingMonths is not null;

    /// <summary>是否在禁升期。</summary>
    public bool IsPromotionBanned => PromotionBanRemainingMonths is not null;
}
