using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;

namespace KFL.Rules.FamilyLifecycle;

/// <summary>
/// ⑧-f 寿终与夭折（契约二十 §7.2~§7.3）。
/// </summary>
/// <remarks>
/// <para>
/// 遍历集合 = <b>在册成员 ∪ 已外嫁归档成员</b>（<see cref="Family.RegisteredMembers"/> ∪
/// {<see cref="Family.ArchivedMembers"/> 中 <see cref="StatusFlag.MarriedOut"/> 置位者}），
/// 按 <see cref="Person.Id"/> 升序；<b>已亡成员 MUST 被显式排除</b>，否则会被逐月重复判定。
/// 外嫁成员<b>只承载本判定</b>，其余月度判定 MUST NOT 施加。
/// </para>
/// <para>
/// <b>寿终</b>：<c>AgeAt(date) &gt;= Lifespan</c> ⇒ 登记 <see cref="DeathCause.OldAge"/>——
/// 服刑 / 患病 / 外嫁均不豁免；<c>Lifespan</c> MUST NOT 重掷、MUST NOT 设上限。
/// </para>
/// <para>
/// <b>夭折</b>：派生判据 <c>AgeAt(date) &lt; 3 且 BirthDate.Month == date.Month</c>
/// （出生当月 + 满 1 岁 + 满 2 岁生日月，共 3 次，<b>零新增字段</b>）⇒ 每月 <b>1 次</b>
/// <c>NextDouble</c> 与 <c>InfantDeathChance × InfantDeathFactor</c> 比较，命中 ⇒ 登记
/// <see cref="DeathCause.Infancy"/>；<c>AgeAt &gt;= 3</c> 后不再判定、<b>不消费随机</b>。
/// </para>
/// <para>
/// 同一成员两条件同时成立（<c>Lifespan &lt; 3</c>）时以<b>寿终优先</b>，只登记一次。
/// 本子步只登记死亡，<b>MUST NOT</b> 改 <c>Status</c>（由 ⑧-g 统一结算）。
/// </para>
/// </remarks>
public static class LifespanAdvance
{
    /// <summary>推进一个月的寿终与夭折判定。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="date">当月。</param>
    /// <param name="random">随机源。</param>
    /// <param name="deaths">死亡登记表。</param>
    public static void Run(GameState state, GameDate date, IRandomService random, DeathRegister deaths)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(deaths);

        var family = state.Family;
        var chance = (double)(ChildbirthPolicy.InfantDeathChance
                              * ChildbirthPolicy.InfantDeathFactor(state.Difficulty));

        var members = new List<Person>(family.RegisteredMembers);

        members.AddRange(family.ArchivedMembers.Where(
            person => (person.Status & StatusFlag.MarriedOut) == StatusFlag.MarriedOut));

        foreach (var person in members.OrderBy(member => member.Id.Value))
        {
            if ((person.Status & StatusFlag.Deceased) == StatusFlag.Deceased)
            {
                continue;
            }

            var age = person.AgeAt(date);

            if (age >= person.Lifespan)
            {
                deaths.Add(person, DeathCause.OldAge);
                continue;
            }

            if (age < ChildbirthPolicy.InfantExemptAge && person.BirthDate.Month == date.Month
                && random.NextDouble() < chance)
            {
                deaths.Add(person, DeathCause.Infancy);
            }
        }
    }
}
