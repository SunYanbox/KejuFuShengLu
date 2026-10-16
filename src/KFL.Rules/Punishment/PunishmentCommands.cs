using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Rules.Punishment;

/// <summary>
/// 惩罚的**调试 / 查询侧命令**（FR-031；契约十六 §7、§8）——被界面轨 U4 消费。
/// </summary>
/// <remarks>
/// <para>
/// 规则层 MUST NOT 按调用方分支：本类的每个入口都只有一个语义，且与结算路径**共用同一实现**
/// （<see cref="PunishmentAdvance"/> 的释放路径、<see cref="ClearBans"/> 的个人禁令路径），
/// MUST NOT 与「个人禁考 / 禁升」形成两个互不相认的真源。
/// </para>
/// <para>
/// 已亡 / 已归档成员不因本类复活：死刑立即执行（契约十六 §6.5），规则层**不存在**
/// 「死囚待决」的持久状态。
/// </para>
/// </remarks>
public static class PunishmentCommands
{
    /// <summary>
    /// 一次性解除该成员的**自身**禁考与禁升（服刑与待阙计时不受影响）。
    /// </summary>
    /// <param name="person">目标成员。</param>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 为 <see langword="null"/>。</exception>
    public static void ClearBans(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        var timers = person.Timers;
        if (timers.ExamBanRemainingMonths is null && timers.PromotionBanRemainingMonths is null)
        {
            return;
        }

        var status = person.Status & ~StatusFlag.ExamBanned & ~StatusFlag.PromotionBanned;

        // 清除次序：先清计时、后清状态位（Person 的交叉校验 setter 强制）。
        person.Timers = new StatusTimers(
            sentenceRemainingMonths: timers.SentenceRemainingMonths,
            examBanRemainingMonths: null,
            promotionBanRemainingMonths: null,
            awaitingPostRemainingMonths: timers.AwaitingPostRemainingMonths);
        person.Status = status;
    }

    /// <summary>
    /// 赦免死囚（FR-031 的调试面完整性）。
    /// </summary>
    /// <param name="person">目标成员。</param>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 为 <see langword="null"/>。</exception>
    /// <remarks>
    /// **任何状态下都是空操作**：死刑**立即执行**（契约十六 §6.5），规则层不存在「已判未决」的
    /// 持久状态，故没有可赦免的对象。本入口存在的意义是让调试面与 §13.2 的入口清单一一对应。
    /// </remarks>
    public static void PardonDeathRow(Person person) => ArgumentNullException.ThrowIfNull(person);

    /// <summary>
    /// 释放囚犯：清服刑计时与状态位，并按契约十六 §7 施加释放降级。
    /// </summary>
    /// <param name="person">目标成员。</param>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 为 <see langword="null"/>。</exception>
    /// <remarks>
    /// **复用结算第③步的释放路径**（<see cref="PunishmentAdvance.ClearSentence"/> +
    /// <see cref="PunishmentAdvance.DemoteOnRelease"/>）；与第③步的差别只有一处：本入口**不落**
    /// <see cref="ReleaseFact"/>（事实流归属结算当月，调试入口没有可归属的年月）。功名保留、
    /// 仍在禁考期则继续禁考。
    /// </remarks>
    public static void ReleasePrisoner(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        if (PunishmentAdvance.ClearSentence(person))
        {
            PunishmentAdvance.DemoteOnRelease(person);
        }
    }

    /// <summary>
    /// 一次性解除**全族**禁考禁升（FR-021、FR-031）：对每名在册成员走 <see cref="ClearBans"/>
    /// 同一入口。
    /// </summary>
    /// <param name="family">家族。</param>
    /// <exception cref="ArgumentNullException"><paramref name="family"/> 为 <see langword="null"/>。</exception>
    /// <remarks>
    /// 「全族禁考禁升」**没有**家族级计时字段（Q3 = A）——它落在每名成员的
    /// <c>StatusTimers</c> 上，故解除也必须逐人进行。
    /// </remarks>
    public static void ClearFamilyBans(Family family)
    {
        ArgumentNullException.ThrowIfNull(family);

        foreach (var member in family.RegisteredMembers)
        {
            ClearBans(member);
        }
    }
}
