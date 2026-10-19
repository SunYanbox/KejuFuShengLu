using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Infrastructure.Save.Serialization;

/// <summary>
/// 读档自愈：把**历史缺陷**留下的、语义上可安全丢弃的字段就地归一，使既有存档仍可读取。
/// </summary>
/// <remarks>
/// <para>
/// 授权：规格书 §17 裁决块（2026-10-19，玩家反馈：曹家存档无法读取）——「过期提议」属于
/// 可安全丢弃的**派生数据**（提议本身是当月的一次性报价，下月即失效），故读档时清空而非拒绝；
/// 其余任何不一致一律仍由 <see cref="SaveStateValidator"/> 按原分类拒绝。
/// </para>
/// <para>
/// MUST NOT 扩大本类的职责：它只处理「提议的 <c>OurMemberId</c> 已不是可婚配者」与
/// 「已申请生育的夫妇已不再互为配偶」两种情形（同一次玩家反馈的两副面孔，均属生命周期簿记）。
/// 真正的**写入侧**收口在 <see cref="Family.Marry"/>（成婚即清提议）、
/// <see cref="Family.EndMarriage"/> 与月度结算第 ⑧-a0 子步。
/// </para>
/// </remarks>
internal static class SaveStateRepair
{
    /// <summary>清掉指向「已亡 / 已外嫁 / 已有配偶 / 非本族成员」的过期提议。</summary>
    /// <param name="state">已逐字段复原的状态（校验之前）。</param>
    /// <returns>确实清空了一条提议时为 <c>true</c>。</returns>
    public static bool ClearStaleProposal(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var family = state.Family;
        if (family.Lifecycle.Proposal is not { } proposal)
        {
            return false;
        }

        var member = Find(family.Members, proposal.OurMemberId);

        var stale = member is null
            || (member.Status & (StatusFlag.Deceased | StatusFlag.MarriedOut)) != StatusFlag.None
            || member.SpouseId is not null;

        if (stale)
        {
            family.Lifecycle.SetProposal(null);
        }

        return stale;
    }

    /// <summary>清掉「已不再互为配偶」的生育簿记（已申请本年生育的夫妇 + 由此变成孤儿的待分娩记录）。</summary>
    /// <param name="state">已逐字段复原的状态（校验之前）。</param>
    /// <returns>确实清掉了簿记时为 <c>true</c>。</returns>
    public static bool ClearStaleBirthBookkeeping(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var family = state.Family;
        var lifecycle = family.Lifecycle;
        var changed = false;

        foreach (var couple in lifecycle.AppliedBirthCouples.ToArray())
        {
            if (CoupleIsMutuallyMarried(family, couple))
            {
                continue;
            }

            // 按「成员」丢弃：同一成员在同一时刻至多只属于一个夫妇键，故只波及这一条。
            lifecycle.DropBirthCouplesInvolving(couple.HusbandId);
            changed = true;
        }

        if (lifecycle.DropPendingBirthsWithoutApplication())
        {
            changed = true;
        }

        return changed;
    }

    /// <summary>该夫妇键的两端是否仍互为配偶。</summary>
    private static bool CoupleIsMutuallyMarried(Family family, CoupleKey couple)
    {
        var husband = Find(family.Members, couple.HusbandId);
        var wife = Find(family.Members, couple.WifeId);

        return husband is not null
            && wife is not null
            && husband.SpouseId == wife.Id
            && wife.SpouseId == husband.Id;
    }

    private static Person? Find(IReadOnlyCollection<Person> members, PersonId id)
    {
        foreach (var member in members)
        {
            if (member.Id == id)
            {
                return member;
            }
        }

        return null;
    }
}
