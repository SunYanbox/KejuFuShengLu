using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;

namespace KFL.Rules.Punishment;

/// <summary>
/// 惩罚推进（结算**第③步**；契约十六 §1.2）——把上月的惩罚事实投影成快照，并把服刑 / 禁考 /
/// 禁升三个计时各推进 1 个月，处理归零（释放与解除）。
/// </summary>
/// <remarks>
/// <para>
/// 步内固定次序：① **投影并清空**日志（恰好一遍）→ ② 按 <c>PersonId.Value</c> **升序**逐人递减
/// （三个计时互相独立、并行；**只**作用于 <c>StatusTimers</c> 的三个字段，MUST NOT 触碰
/// <c>AwaitingPostRemainingMonths</c>）→ ③ 归零处理（服刑 ⇒ 释放 → <see cref="DemoteOnRelease"/>；
/// 禁考 / 禁升 ⇒ 清对应计时与状态位）→ ④ 统计当月在册服刑人数。
/// </para>
/// <para>
/// 本步 MUST NOT 调用 <c>AdvanceMonth</c>、MUST NOT 修改 <c>CurrentDate</c>、
/// **消费随机恰好 0 次**（契约十六 §1.2 条款 4 / 5）。
/// </para>
/// <para>
/// **归零产生的 <see cref="ReleaseFact"/> 只进本次快照、MUST NOT 回写日志**：日志是「结算之外发生的
/// 事实」的缓冲区，第③步若把释放事实写回日志，下一个月的投影会把它重复上报一次并跨月错位。
/// </para>
/// <para>
/// **已亡者不再推进计时**：连坐处死会使被处死者归档，其残留计时不构成「在册成员的状态」，
/// 继续递减会产出针对亡者的释放事实。
/// </para>
/// </remarks>
public static class PunishmentAdvance
{
    /// <summary>
    /// 执行一次惩罚推进。
    /// </summary>
    /// <param name="state">游戏状态。</param>
    /// <returns>当月的惩罚快照（含日志投影与本次归零产生的释放事实）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 <see langword="null"/>。</exception>
    public static PunishmentSnapshot Run(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var month = state.CurrentDate;
        var journal = state.Family.PunishmentJournal;

        // ① 投影并清空：先把集合复制出来（Clear 会清空内部 List，直接引用会投影为空）。
        var bribes = journal.Bribes.ToArray();
        var sentences = journal.Sentences.ToArray();
        var fines = journal.Fines.ToArray();
        var relativeBans = journal.RelativeBans.ToArray();
        var collectivePunishments = journal.CollectivePunishments.ToArray();
        var releases = new List<ReleaseFact>(journal.Releases);
        var familyBanDeclared = journal.FamilyBanDeclared;
        journal.Clear();

        // ② 逐人递减 + ③ 归零处理。
        foreach (var person in ByPersonId(state.Family.Members))
        {
            if (person.Status.HasFlag(StatusFlag.Deceased))
            {
                continue;
            }

            var timers = person.Timers;
            var sentence = Decrement(timers.SentenceRemainingMonths);
            var examBan = Decrement(timers.ExamBanRemainingMonths);
            var promotionBan = Decrement(timers.PromotionBanRemainingMonths);

            // 只有**非空计时归零**才拆掉状态位：计时本来就是 null 的「外部置位」（如致仕专项、
            // 调试入口写入的并存状态）MUST NOT 被本步顺手清掉（规格书 §4.1「九位可并存」）。
            var examBanExpired = examBan is 0;
            var promotionBanExpired = promotionBan is 0;

            if (examBanExpired)
            {
                examBan = null;
            }

            if (promotionBanExpired)
            {
                promotionBan = null;
            }

            var status = person.Status;
            if (examBanExpired)
            {
                status &= ~StatusFlag.ExamBanned;
            }

            if (promotionBanExpired)
            {
                status &= ~StatusFlag.PromotionBanned;
            }

            // 清除次序：**先清计时（写 null）、后清状态位**——Person 的交叉校验 setter 强制该次序。
            // 服刑为 0 时仍先写 0（非空），再由 ReleaseCore 统一清掉，使释放只有一条代码路径。
            person.Timers = new StatusTimers(
                sentenceRemainingMonths: sentence,
                examBanRemainingMonths: examBan,
                promotionBanRemainingMonths: promotionBan,
                awaitingPostRemainingMonths: timers.AwaitingPostRemainingMonths);
            person.Status = status;

            if (sentence is 0)
            {
                var release = ReleaseCore(person, month);
                if (release is { } released)
                {
                    releases.Add(released);
                }
            }
        }

        // ④ 统计当月在册服刑人数。
        return new PunishmentSnapshot
        {
            Month = month,
            Bribes = bribes,
            Sentences = sentences,
            Fines = fines,
            RelativeBans = relativeBans,
            CollectivePunishments = collectivePunishments,
            Releases = releases,
            FamilyBanDeclared = familyBanDeclared,
            PrisonerCount = PunishmentQueries.PrisonerCount(state.Family, month),
        };
    }

    /// <summary>
    /// 释放的降级（契约十六 §7）：释放时 <c>Rank</c> 非 <c>null</c> ⇒ 降
    /// <see cref="PunishmentPolicy.ReleaseDemotionLevels"/> 级；已是 <c>L18</c> 则**维持**。
    /// </summary>
    /// <param name="person">被释放的成员。</param>
    /// <returns>释放后的官阶级数；无官职时为 <see langword="null"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 为 <see langword="null"/>。</exception>
    /// <remarks>
    /// 判据是「释放时 <c>Rank</c> 非 <c>null</c>」，MUST NOT 新增「曾为官」标记（否则与
    /// <c>Rank</c> 构成第二真源）；功名保留、可继续考试。
    /// </remarks>
    public static int? DemoteOnRelease(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        if (person.Rank is not { } rank)
        {
            return null;
        }

        var target = rank.Level >= SalaryTable.LowestLevel
            ? rank.Level
            : rank.Level + PunishmentPolicy.ReleaseDemotionLevels;

        if (target != rank.Level)
        {
            person.Rank = new OfficialRank(target);
        }

        return target;
    }

    /// <summary>把一个非空计时递减 1（<c>null</c> 保持 <c>null</c>；不越界到负数）。</summary>
    private static int? Decrement(int? remainingMonths) =>
        remainingMonths is { } value ? Math.Max(value - 1, 0) : null;

    /// <summary>
    /// 清除服刑计时与 <see cref="StatusFlag.ServingSentence"/> 状态位（次序：先清计时、后清状态位）。
    /// </summary>
    /// <param name="person">被释放的成员。</param>
    /// <returns>原本是否在服刑中。</returns>
    internal static bool ClearSentence(Person person)
    {
        var timers = person.Timers;
        if (timers.SentenceRemainingMonths is null)
        {
            return false;
        }

        var status = person.Status & ~StatusFlag.ServingSentence;
        person.Timers = new StatusTimers(
            sentenceRemainingMonths: null,
            examBanRemainingMonths: timers.ExamBanRemainingMonths,
            promotionBanRemainingMonths: timers.PromotionBanRemainingMonths,
            awaitingPostRemainingMonths: timers.AwaitingPostRemainingMonths);
        person.Status = status;
        return true;
    }

    /// <summary>
    /// 释放一条服刑（**唯一**的释放代码路径）：清计时与状态位 → <see cref="DemoteOnRelease"/> →
    /// 生成释放事实；仍在禁考期则继续禁考（不清禁考 / 禁升计时）。
    /// </summary>
    private static ReleaseFact? ReleaseCore(Person person, GameDate month) =>
        ClearSentence(person) ? new ReleaseFact(month, person.Id, DemoteOnRelease(person)) : null;

    /// <summary>把成员集合按 <c>PersonId.Value</c> 升序排列（契约十六 §1.2 条款 2）。</summary>
    private static List<Person> ByPersonId(IReadOnlyCollection<Person> members)
    {
        var ordered = new List<Person>(members);
        ordered.Sort(static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
        return ordered;
    }
}
