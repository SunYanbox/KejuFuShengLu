using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;
using KFL.Rules.FamilyLifecycle;
using KFL.Rules.Punishment;

namespace KFL.Presentation;

public sealed partial class GameSession
{
    // ───────────────────────── 婚育 ─────────────────────────

    public string Matchmake(Person member, bool buyDegree)
    {
        var direction = LifecycleQueries.DirectionOf(member.Gender);
        var result = LifecycleEntry.TryMatchmake(State, member, direction, _random, _names, buyDegree);
        if (!result.Accepted)
        {
            return $"托媒失败：{Display.LifecycleReason(result.RejectReason)}。";
        }

        Note($"✉ 为 {member.Name} 托媒，花费 {Display.Money(result.Amount)}，静候佳音。");
        Changed?.Invoke();
        return string.Empty;
    }

    public string RespondToProposal(bool accept, bool buyDegree)
    {
        var result = LifecycleEntry.RespondToProposal(State, accept, _random, _names, buyDegree);
        if (result.RejectReason != LifecycleRejectReason.None)
        {
            return $"应答失败：{Display.LifecycleReason(result.RejectReason)}。";
        }

        Note(accept
            ? $"❤ 应允提亲，聘礼/嫁妆支出 {Display.Money(result.Amount)}。"
            : "✗ 婉拒提亲。");
        Changed?.Invoke();
        return string.Empty;
    }

    /// <summary>
    /// 族内通婚（玩家反馈 1）：把两名同代在册未婚成员结为夫妻，零费用。
    /// </summary>
    /// <param name="first">一方。</param>
    /// <param name="second">另一方（须同代异性）。</param>
    /// <returns>成功 ⇒ 空串；否则为给玩家看的原因。</returns>
    public string MarryWithinFamily(Person first, Person second)
    {
        var result = LifecycleEntry.TryMarryWithinFamily(State, first, second);
        if (!result.Accepted)
        {
            return $"族内通婚失败：{Display.LifecycleReason(result.RejectReason)}。";
        }

        Note($"❤ 族内通婚：{first.Name} 与 {second.Name} 结为夫妻（无聘礼 / 嫁妆）。");
        Changed?.Invoke();
        return string.Empty;
    }

    /// <summary>
    /// 离异（玩家反馈 1）：终止选中成员的婚姻，对方记入前任配偶，零费用。
    /// </summary>
    /// <param name="member">有配偶的一方。</param>
    /// <returns>成功 ⇒ 空串；否则为原因。</returns>
    public string Divorce(Person member)
    {
        var spouse = member.SpouseId is { } spouseId ? State.Family.TryGet(spouseId) : null;
        var result = LifecycleEntry.Divorce(State, member);
        if (!result.Accepted)
        {
            return $"离异失败：{Display.LifecycleReason(result.RejectReason)}。";
        }

        Note($"✂ {member.Name} 与 {(spouse is null ? "配偶" : spouse.Name)} 离异（子女的父母引用不变）。");
        Changed?.Invoke();
        return string.Empty;
    }

    public string ApplyChildbirth(CoupleKey couple)    {
        var result = LifecycleEntry.ApplyChildbirth(State, couple);
        if (!result.Accepted)
        {
            return $"生育申请失败：{Display.LifecycleReason(result.RejectReason)}。";
        }

        Note($"👶 申请生育，花费 {Display.Money(result.Amount)}，次月分娩。");
        Changed?.Invoke();
        return string.Empty;
    }

    public string BuyPerson()
    {
        var result = LifecycleEntry.BuyPerson(State, _random, _names);
        if (!result.Accepted)
        {
            return $"收养失败：{Display.LifecycleReason(result.RejectReason)}。";
        }

        Note($"⚑ 收养 {Display.PersonBrief(LastPurchasedPerson(), State.CurrentDate)}，花费 {Display.Money(result.Amount)}（现金 {Display.Money(result.FromCash)} + 储蓄 {Display.Money(result.FromSavings)}）。");
        Changed?.Invoke();
        return string.Empty;
    }

    /// <summary>最近一名收养成员（取账本里最新一条 <see cref="LedgerCategory.PersonBought"/> 事件条目）。</summary>
    /// <returns>该成员；账本里没有收养记录或其已不在家族时返回 <c>null</c>。</returns>
    private Person? LastPurchasedPerson()
    {
        var entries = State.Economy.Ledger.Entries;
        for (var index = entries.Count - 1; index >= 0; index--)
        {
            var entry = entries[index];
            if (entry.Category == LedgerCategory.PersonBought && entry.PersonId is { } id)
            {
                return State.Family.TryGet(id);
            }
        }

        return null;
    }

    public string StartTreatment(Person patient)
    {
        var result = LifecycleEntry.StartTreatment(State, patient);
        if (!result.Accepted)
        {
            return $"就医失败：{Display.LifecycleReason(result.RejectReason)}。";
        }

        Note($"✚ {patient.Name} 延医问药，花费 {Display.Money(result.Amount)}。");
        Changed?.Invoke();
        return string.Empty;
    }

    public string SetTutor(Person person, bool enabled)
    {
        if (!enabled)
        {
            person.IsTutoredByMaster = false;
            Note($"（{person.Name} 辞去名师）");
            Changed?.Invoke();
            return string.Empty;
        }

        var monthly = Money.FromGuan(GrowthTutorFeeGuan());
        person.IsTutoredByMaster = true;
        Note($"✎ {person.Name} 延请名师（约 {Display.Money(monthly)}/月，由月度结算扣缴）。");
        Changed?.Invoke();
        return string.Empty;
    }

    private static decimal GrowthTutorFeeGuan()
        => KFL.Rules.Config.GrowthPolicy.TutorFeeGuanPerMonth;

    public string SetHead(Person person)
    {
        State.Family.SetHead(person.Id);
        Note($"（家主改为 {person.Name}）");
        Changed?.Invoke();
        return string.Empty;
    }

    public string Release(Person person)
    {
        PunishmentCommands.ReleasePrisoner(person);
        Note($"（释放 {person.Name}）");
        Changed?.Invoke();
        return string.Empty;
    }

    public string Pardon(Person person)
    {
        PunishmentCommands.PardonDeathRow(person);
        Note($"（{person.Name} 免死）");
        Changed?.Invoke();
        return string.Empty;
    }

    public string ClearBans(Person person)
    {
        PunishmentCommands.ClearBans(person);
        Note($"（解除 {person.Name} 的禁考/禁升）");
        Changed?.Invoke();
        return string.Empty;
    }

    public string ClearFamilyBans()
    {
        PunishmentCommands.ClearFamilyBans(State.Family);
        Note("（解除全族禁令）");
        Changed?.Invoke();
        return string.Empty;
    }
}
