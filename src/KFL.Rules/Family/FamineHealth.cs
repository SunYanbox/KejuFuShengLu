using KFL.Core.Config;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;
using KFL.Rules.Settlement;

namespace KFL.Rules.FamilyLifecycle;

/// <summary>
/// ⑧-e 饥馑体质扣减与死亡判定（契约二十 §7.1；FR-031）。
/// </summary>
/// <remarks>
/// <para>
/// 只<b>读</b> <see cref="FamineState.Stage"/>（002 已交付的状态机）：<see cref="FamineStage.Severe"/>
/// 时对计口成员每人 <c>−10</c> 体质，随后每人 <b>1 次</b> <c>NextDouble</c> 与
/// <c>FamineDeathChance</c> 比较，命中 ⇒ 登记 <see cref="DeathCause.Famine"/>；
/// 体质因扣减归零 ⇒ 同样登记饥馑死亡（该成员不再消费死亡判定随机，与 ⑧-c 的「先扣后判」口径一致）。
/// </para>
/// <para>
/// <see cref="FamineStage.Relief"/>（救济期）与 <see cref="FamineStage.None"/> ⇒ 体质<b>不减</b>、
/// 死亡判定<b>不掷、不消费随机</b>。MUST NOT 改写 002 的阶段迁移、时限与可读契约。
/// </para>
/// <para>
/// 遍历集合 = <see cref="CountedMembers.Counted"/>；患病与饥馑<b>各扣一次、不得互替</b>。
/// </para>
/// </remarks>
public static class FamineHealth
{
    /// <summary>推进一个月的饥馑体质效果。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="random">随机源。</param>
    /// <param name="deaths">死亡登记表（本子步只登记，MUST NOT 改 <c>Status</c>）。</param>
    public static void Run(GameState state, IRandomService random, DeathRegister deaths)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(deaths);

        if (state.Economy.Famine.Stage != FamineStage.Severe)
        {
            return;
        }

        var chance = (double)GrowthPolicy.FamineDeathChance;

        foreach (var person in CountedMembers.Counted(state.Family).OrderBy(member => member.Id.Value))
        {
            person.Health = Math.Max(AttributeLimits.Min, person.Health - GrowthPolicy.FamineHealthLossPerMonth);

            if (person.Health <= AttributeLimits.Min)
            {
                deaths.Add(person, DeathCause.Famine);
                continue;
            }

            if (random.NextDouble() < chance)
            {
                deaths.Add(person, DeathCause.Famine);
            }
        }
    }
}
