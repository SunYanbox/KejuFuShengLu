using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Rules.FamilyLifecycle;

/// <summary>
/// ⑧-g 死亡结算：把 ⑧-b~⑧-f 登记的死亡**统一**落到成员状态与账本（契约二十 §7、不变量 11）。
/// </summary>
/// <remarks>
/// <para>
/// 当月**唯一**置 <see cref="StatusFlag.Deceased"/> 的处所；按 <see cref="Person.Id"/> 升序对
/// 登记表去重后，每人：置位 <see cref="StatusFlag.Deceased"/>（归档由
/// <c>Family.ArchivedMembers</c> 派生，无需另写入）→ 若有配偶则经
/// <see cref="Family.EndMarriage"/> 终止婚姻（亡者 <c>SpouseId</c> 置空、双方 <c>FormerSpouseIds</c>
/// 追加，FR-021）→ 落 1 条 <see cref="LedgerCategory.MemberDeceased"/> 事件条目（金额 0）。
/// 已含 <see cref="StatusFlag.Deceased"/> 者幂等跳过（故同日双亡时第二人不再触发终止）。
/// </para>
/// <para>
/// 「死亡原因」（<see cref="DeathCause"/>）只随返回值进入当月事实段，
/// MUST NOT 落到 <see cref="Person"/> 的任何字段或账本条目上。
/// </para>
/// </remarks>
public static class DeathSettlement
{
    /// <summary>结算当月登记的死亡。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="deaths">当月死亡登记表（按 <see cref="Person.Id"/> 升序去重）。</param>
    /// <param name="date">结算月份。</param>
    /// <returns>按 <see cref="Person.Id"/> 升序的死亡事实段。</returns>
    /// <exception cref="ArgumentNullException">任一必需参数为 <c>null</c>。</exception>
    public static IReadOnlyList<DeathFact> Run(GameState state, DeathRegister deaths, GameDate date)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(deaths);

        var facts = new List<DeathFact>();

        foreach (var (person, cause) in deaths.Ordered)
        {
            if ((person.Status & StatusFlag.Deceased) != StatusFlag.None)
            {
                continue;
            }

            person.Status |= StatusFlag.Deceased;

            // FR-021：丧偶 MUST 经 Family.EndMarriage——配偶 SpouseId 置空、双方 FormerSpouseIds 追加。
            // 夫妻同日双亡时先处理者已清空两人的 SpouseId，后者自然跳过（幂等）。
            if (person.SpouseId is { } spouseId && state.Family.TryGet(spouseId) is not null)
            {
                state.Family.EndMarriage(person.Id);
            }

            state.Economy.Apply(LedgerCategory.MemberDeceased, person.Id, Money.Zero, date);

            facts.Add(new DeathFact(person.Id, cause));
        }

        return facts;
    }
}
