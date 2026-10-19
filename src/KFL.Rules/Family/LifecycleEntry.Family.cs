using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;

namespace KFL.Rules.FamilyLifecycle;

public static partial class LifecycleEntry
{
    /// <summary>
    /// 族内通婚（玩家反馈 1）：把两名**同代**的在册未婚成年异性成员结为夫妻。
    /// **零费用、零随机**——族内婚不产生聘礼 / 嫁妆（推断见 <c>推断.md</c> §9）。
    /// </summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="first">一方（须在册未婚成年）。</param>
    /// <param name="second">另一方（须在册未婚成年、与 <paramref name="first"/> 同代异性）。</param>
    /// <returns>入口结果；被拒时 <see cref="LifecycleOutcome.RejectReason"/> 区分
    /// 「不是可婚配成员」（<c>NotMarriageable</c>）与「不同代」（<c>NotSameGeneration</c>）。</returns>
    /// <exception cref="ArgumentNullException">任一必需参数为 <c>null</c>。</exception>
    public static LifecycleOutcome TryMarryWithinFamily(GameState state, Person first, Person second)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var family = state.Family;
        var month = state.CurrentDate;

        if (first.Generation != second.Generation)
        {
            return LifecycleOutcome.Rejected(LifecycleRejectReason.NotSameGeneration);
        }

        if (!LifecycleQueries.CanMarryWithin(family, first, second, month))
        {
            return LifecycleOutcome.Rejected(LifecycleRejectReason.NotMarriageable);
        }

        family.Marry(first.Id, second.Id);

        return new LifecycleOutcome
        {
            Accepted = true,
            RejectReason = LifecycleRejectReason.None,
            Amount = Money.Zero,
            FromCash = Money.Zero,
            FromSavings = Money.Zero,
            Entry = null,
        };
    }

    /// <summary>
    /// 离异（玩家反馈 1）：终止双方婚姻、把对方记入**前任配偶**，子女的父母引用不动
    /// （<see cref="Family.EndMarriage"/> 的既有语义）。**零费用、零随机**。
    /// </summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="member">有配偶的一方。</param>
    /// <returns>入口结果；无配偶时为 <c>NotSpouse</c>，非在册时为 <c>NotMarriageable</c>。</returns>
    /// <exception cref="ArgumentNullException">任一必需参数为 <c>null</c>。</exception>
    public static LifecycleOutcome Divorce(GameState state, Person member)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(member);

        if (member.SpouseId is null)
        {
            return LifecycleOutcome.Rejected(LifecycleRejectReason.NotSpouse);
        }

        if (!LifecycleQueries.CanDivorce(member) || state.Family.TryGet(member.Id) is null)
        {
            return LifecycleOutcome.Rejected(LifecycleRejectReason.NotMarriageable);
        }

        state.Family.EndMarriage(member.Id);

        return new LifecycleOutcome
        {
            Accepted = true,
            RejectReason = LifecycleRejectReason.None,
            Amount = Money.Zero,
            FromCash = Money.Zero,
            FromSavings = Money.Zero,
            Entry = null,
        };
    }

    /// <summary>
    /// 发起一次疗程（FR-030）：仅对 <see cref="StatusFlag.Ill"/> 置位者可用，
    /// 置 <see cref="StatusTimers.TreatmentRemainingMonths"/> = <c>3</c>（<b>零随机、零支付</b>）。
    /// </summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="patient">患者。</param>
    /// <returns>入口结果；<c>Accepted = false</c> 时 <see cref="LifecycleOutcome.RejectReason"/> 为 <c>NotMarriageable</c>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 或 <paramref name="patient"/> 为 <c>null</c>。</exception>
    /// <remarks>
    /// <para>
    /// <b>宽限月内 MUST NOT 支付药费</b>：药费只在 ⑧-c 第一段的疗程月产生，而 ⑧-c 在确诊当月
    /// 已先于感染判定执行完，故确诊当月调用本入口不会产生任何支付——疗程自患病<b>次月</b>起算。
    /// </para>
    /// <para>
    /// 疗程结束后若仍未愈可<b>再次发起</b>（本入口只重置月数，不区分是否曾治疗过）。
    /// MUST NOT 自创新枚举值：非患病者一律用 <see cref="LifecycleRejectReason.NotMarriageable"/>。
    /// </para>
    /// </remarks>
    public static LifecycleOutcome StartTreatment(GameState state, Person patient)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(patient);

        var registered = (patient.Status & StatusFlag.Deceased) == StatusFlag.None
                         && (patient.Status & StatusFlag.MarriedOut) == StatusFlag.None
                         && state.Family.TryGet(patient.Id) is not null;

        if (!registered || (patient.Status & StatusFlag.Ill) != StatusFlag.Ill)
        {
            return LifecycleOutcome.Rejected(LifecycleRejectReason.NotMarriageable);
        }

        patient.Timers = new StatusTimers(
            sentenceRemainingMonths: patient.Timers.SentenceRemainingMonths,
            examBanRemainingMonths: patient.Timers.ExamBanRemainingMonths,
            promotionBanRemainingMonths: patient.Timers.PromotionBanRemainingMonths,
            awaitingPostRemainingMonths: patient.Timers.AwaitingPostRemainingMonths,
            illnessSeverityBasisPoints: patient.Timers.IllnessSeverityBasisPoints,
            treatmentRemainingMonths: IllnessPolicy.TreatmentMonths);

        return new LifecycleOutcome
        {
            Accepted = true,
            RejectReason = LifecycleRejectReason.None,
            Amount = Money.Zero,
            FromCash = Money.Zero,
            FromSavings = Money.Zero,
            Entry = null,
        };
    }
}
