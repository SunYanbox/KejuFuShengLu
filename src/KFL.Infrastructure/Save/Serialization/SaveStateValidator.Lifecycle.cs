// SaveStateValidator 第 13~15 条：家族生命周期一致性。

using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Infrastructure.Save.Serialization;

public static partial class SaveStateValidator
{
    /// <summary>
    /// 第 13 条：待应答提议的 <c>OurMemberId</c> MUST 指向本家族的在册未婚成员（007-family-lifecycle）。
    /// </summary>
    /// <remarks>
    /// data-model §6 不变量 3：提议至多一条，且 <c>OurMemberId</c> MUST 指向在册成员
    /// （已亡与外嫁者 MUST NOT 承载提议）。
    /// </remarks>
    private static void EnsureLifecycleProposalReferencesMember(Family family)
    {
        if (family.Lifecycle.Proposal is not { } proposal)
        {
            return;
        }

        var member = Find(family.Members, proposal.OurMemberId)
            ?? throw Invalid($"提议的 OurMemberId {proposal.OurMemberId} 不是本家族成员（R-04 第 13 条）。");

        if ((member.Status & (StatusFlag.Deceased | StatusFlag.MarriedOut)) != StatusFlag.None)
        {
            throw Invalid(
                $"提议的 OurMemberId {proposal.OurMemberId} 指向已亡或外嫁的成员（R-04 第 13 条）。");
        }

        if (member.SpouseId is not null)
        {
            throw Invalid($"提议的 OurMemberId {proposal.OurMemberId} 已有配偶（R-04 第 13 条）。");
        }
    }

    /// <summary>
    /// 第 14 条：每条待分娩记录都 MUST 出自同年的已申请夫妇集合（007-family-lifecycle）。
    /// </summary>
    /// <remarks>
    /// data-model §6 不变量：<c>PendingBirths ⊆ AppliedBirthCouples</c>，且每个夫妇键 MUST 指向
    /// **互为配偶**的两名在册成员（半截清空或错配的损坏档在此拒掉）。簿记在跨年时会同时清空两侧，
    /// 故本条只在被清空到一半的损坏档上生效。
    /// </remarks>
    private static void EnsurePendingBirthsApplied(Family family)
    {
        var lifecycle = family.Lifecycle;

        foreach (var birth in lifecycle.PendingBirths)
        {
            if (!lifecycle.AppliedBirthCouples.Contains(birth.Couple))
            {
                throw Invalid(
                    $"待分娩记录的夫妇（夫 {birth.Couple.HusbandId}、妻 {birth.Couple.WifeId}）不在已申请本年生育的夫妇集合内（R-04 第 14 条）。");
            }

            EnsureMember(birth.Couple.HusbandId, "待分娩记录的夫");
            EnsureMember(birth.Couple.WifeId, "待分娩记录的妻");
        }

        foreach (var couple in lifecycle.AppliedBirthCouples)
        {
            var husband = EnsureMember(couple.HusbandId, "已申请生育的夫");
            var wife = EnsureMember(couple.WifeId, "已申请生育的妻");

            if (husband.SpouseId != couple.WifeId || wife.SpouseId != couple.HusbandId)
            {
                throw Invalid(
                    $"已申请生育的夫妇（夫 {couple.HusbandId}、妻 {couple.WifeId}）的配偶关系非双向一致（R-04 第 14 条）。");
            }
        }

        Person EnsureMember(PersonId id, string what)
        {
            return Find(family.Members, id)
                ?? throw Invalid($"{what} {id} 不是本家族成员（R-04 第 14 条）。");
        }
    }

    /// <summary>
    /// 第 15 条：生命周期计数字段合法，且当年购人计数与账本自洽（007-family-lifecycle；data-model §6 不变量 7）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 判据（全部可由盘上事实复核）：<c>PurchaseCount &gt;= 0</c>；<c>PurchaseYear</c> 与
    /// <c>BirthApplicationYear</c> 落在 <c>1~当年</c>；**当年**购人计数按「读时口径」与
    /// <see cref="FamilyLifecycleState.PeekPurchaseOrdinal"/> 一致——即
    /// <c>PurchaseYear == 当年 ? PurchaseCount : 0</c> MUST 等于当年 <see cref="LedgerCategory.PersonPurchase"/>
    /// 条目的条数；同年内这些条目的金额 MUST 严格递增（报价 = 基准价 × 第 n 名）。
    /// </para>
    /// <para>
    /// 报价的**基准价本身**在 <c>KFL.Rules.Config.PopulationPurchasePolicy</c>，而本层
    /// （<c>KFL.Infrastructure</c>）不得反向引用 <c>KFL.Rules</c>——故这里只验证「名次 ⇒ 单调价格」
    /// 这一结构性后果，不验证基准价数值。
    /// </para>
    /// </remarks>
    private static void EnsureLifecycleCountersLegal(Family family, FamilyEconomy economy, GameDate today)
    {
        var lifecycle = family.Lifecycle;

        if (lifecycle.PurchaseCount < 0)
        {
            throw Invalid($"购人计数为负（{lifecycle.PurchaseCount}，R-04 第 15 条）。");
        }

        if (lifecycle.PurchaseYear < 1 || lifecycle.PurchaseYear > today.Year)
        {
            throw Invalid(
                $"购人年份 {lifecycle.PurchaseYear} 不在 1~{today.Year} 区间内（R-04 第 15 条）。");
        }

        if (lifecycle.BirthApplicationYear < 1 || lifecycle.BirthApplicationYear > today.Year)
        {
            throw Invalid(
                $"生育申请年份 {lifecycle.BirthApplicationYear} 不在 1~{today.Year} 区间内（R-04 第 15 条）。");
        }

        var boughtThisYear = 0;
        var previousYear = 0;
        decimal? previousMagnitude = null;

        foreach (var entry in economy.Ledger.Entries)
        {
            if (entry.Category != LedgerCategory.PersonPurchase)
            {
                continue;
            }

            if (entry.Date.Year != previousYear)
            {
                previousYear = entry.Date.Year;
                previousMagnitude = null;
            }

            // 支出类条目恒为负额（`PaymentPrimitive.Pay` 以 `-fromTreasury` 落账），故比**金额绝对值**。
            var magnitude = decimal.Abs(entry.Amount.Wen);

            if (previousMagnitude is { } previous && magnitude <= previous)
            {
                throw Invalid(
                    $"第 {entry.Date.Year} 年的人丁购价未随名次递增（{previous} → {magnitude}，R-04 第 15 条）。");
            }

            previousMagnitude = magnitude;

            if (entry.Date.Year == today.Year)
            {
                boughtThisYear++;
            }
        }

        var expected = lifecycle.PurchaseYear == today.Year ? lifecycle.PurchaseCount : 0;

        if (boughtThisYear != expected)
        {
            throw Invalid(
                $"当年购人计数与账本不符：记为 {expected}（购人年份 {lifecycle.PurchaseYear}），账本有 {boughtThisYear} 条 PersonPurchase（R-04 第 15 条）。");
        }
    }
}
