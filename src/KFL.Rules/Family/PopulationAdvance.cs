using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;
using KFL.Rules.Settlement;

namespace KFL.Rules.FamilyLifecycle;

/// <summary>
/// ⑧ 人口与健康推进（契约十八 §2；FR-007）：把八个子步按**固定次序**串联起来。
/// </summary>
/// <remarks>
/// <para>
/// 次序 MUST NOT 增删或穿插：⑧-a 婚配事件 → ⑧-b 分娩 → ⑧-c 患病与病程 → ⑧-d 成长 →
/// ⑧-e 饥馑死亡判定 → ⑧-f 寿终与夭折 → ⑧-g 死亡结算 → ⑧-h 家主继任。
/// </para>
/// <para>
/// 子步之间只经 <see cref="DeathRegister"/> 与各执行器的返回值通信：⑧-b~⑧-f **只登记**死亡，
/// ⑧-g 才统一结算（不变量 11）。「0 次」子步 MUST NOT 调用 <see cref="IRandomService"/>。
/// </para>
/// <para>
/// 可见性：本类与 <see cref="PopulationAdvanceResult"/> 均为 <c>public</c>（T022 要求可被直接调用；
/// 唯一产品调用者是 <c>MonthlySettlementEngine</c>，测试经 <c>SettlementResult.Lifecycle</c> 观察。
/// 只有 <see cref="DeathRegister"/> 保持 <c>internal</c>——它纯属子步之间的传参通道）。
/// </para>
/// </remarks>
public static class PopulationAdvance
{
    /// <summary>按固定次序推进一个月的人口与健康，返回当月事实段。</summary>
    /// <param name="state">游戏状态（读 <c>CurrentDate</c> 为当月）。</param>
    /// <param name="random">随机服务（子步内消费，次序见契约十八 §3）。</param>
    /// <param name="names">取名服务（新生儿、候选者与被收养者）。</param>
    /// <returns>当月事实段。</returns>
    public static PopulationAdvanceResult Run(GameState state, IRandomService random, INameGenerator names)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(names);

        var facts = LifecycleSnapshot.None(state.CurrentDate);

        // ⑧-a0 过期提议清理（**0 次随机**、0 金额、0 状态变更）：上月的提议若已超出应答窗口
        //      （见 `LifecycleQueries.PendingProposal` 的「当月或次月」口径），MUST 先清空——
        //      否则它会以「无人应答」的形态长期滞留存档，一旦该成员此后经其他路径成婚，
        //      存档就会变成「提议指向已有配偶的成员」而被第 13 条校验拒绝。
        if (state.Family.Lifecycle.Proposal is not null
            && LifecycleQueries.PendingProposal(state.Family, state.CurrentDate) is null)
        {
            state.Family.Lifecycle.SetProposal(null);
        }

        // ⑧-a0′ 生育簿记一致性（**0 次随机**、0 金额、0 状态变更）：已申请本年生育的夫妇若在上一月
        //       离异 / 丧偶 / 有一方外嫁，其夫妇键已不再两端互为配偶——不清掉的话，存档一旦写盘
        //       就再也读不回来（R-04 第 14 条）。写入侧已在 Family.EndMarriage / Marry 收口，
        //       这里是兜底（例如经其它路径离开在册集合）。
        DropUnmarriedBirthCouples(state.Family);

        // ⑧-a 婚配事件（先捉婿、后提亲）：只写 Lifecycle.Proposal，不改任何 Person 状态
        var market = MarriageMarket.Run(state, random, names);
        facts = facts with { PendingProposal = market.PendingProposal };

        // ⑧-b 分娩（消费 PendingBirths，按母亲升序；只登记死亡不改状态）
        var deaths = new DeathRegister();
        var births = Childbirth.Run(state, random, names, deaths);
        facts = facts with { Births = births };

        // ⑧-c 患病与病程（两段式：先推进已患病者、再掷感染；只登记死亡不改状态）
        var illnesses = IllnessAdvance.Run(state, random, deaths);
        facts = facts with { Illnesses = illnesses };

        // ⑧-d 成长与体质恢复（**0 次随机**）；跨档事实由本子步顺带采集（生日当月跨档：成年 / 青年 / 老人）
        GrowthAdvance.Run(state, state.CurrentDate);
        facts = facts with { Crossings = Crossings(state, state.CurrentDate) };

        // ⑧-e 饥馑体质扣减与死亡判定（仅严重期；非严重期不掷、不消费随机）
        FamineHealth.Run(state, random, deaths);

        // ⑧-f 寿终与夭折（含已外嫁归档成员；AgeAt < 3 的生日月各 1 次）
        LifespanAdvance.Run(state, state.CurrentDate, random, deaths);

        // ⑧-g 死亡结算（当月唯一置 Deceased 的处所；幂等去重）
        var deathFacts = DeathSettlement.Run(state, deaths, state.CurrentDate);
        facts = facts with { Deaths = deathFacts };

        // ⑧-h 家主继任（**0 次随机**、MUST NOT 逐死继任）：仅当现任家主已不在在册集合时选一次
        var head = state.Family.HeadId is { } headId ? state.Family.TryGet(headId) : null;
        if (head is null || !CountedMembers.IsRegistered(head))
        {
            var successor = HeadSuccession.Select(HeadSuccession.Candidates(state.Family, state.CurrentDate));
            state.Family.SetHead(successor);
            facts = facts with { NewHeadId = successor, HeadBecameVacant = successor is null };
        }

        return new PopulationAdvanceResult { Lifecycle = facts };
    }

    /// <summary>丢弃「已不再互为配偶」的生育簿记（含其待分娩记录）。</summary>
    private static void DropUnmarriedBirthCouples(Family family)
    {
        var lifecycle = family.Lifecycle;

        foreach (var couple in lifecycle.AppliedBirthCouples.ToArray())
        {
            var husband = family.TryGet(couple.HusbandId);
            var wife = family.TryGet(couple.WifeId);

            var married = husband is not null
                && wife is not null
                && husband.SpouseId == wife.Id
                && wife.SpouseId == husband.Id;

            if (!married)
            {
                lifecycle.DropBirthCouplesInvolving(couple.HusbandId);
            }
        }

        lifecycle.DropPendingBirthsWithoutApplication();
    }

    /// <summary>当月跨年龄档的成员（对比上月与本月的 <see cref="AgeBracketPolicy.Of(Person, GameDate)"/>）。</summary>
    private static List<AgeBracketCrossFact> Crossings(GameState state, GameDate month)
    {
        var previous = month.Month == 1 ? new GameDate(month.Year - 1, 12) : new GameDate(month.Year, month.Month - 1);
        var crossings = new List<AgeBracketCrossFact>();

        foreach (var person in CountedMembers.Registered(state.Family).OrderBy(member => member.Id.Value))
        {
            // 当月出生的成员在上月尚不存在，MUST NOT 查询其出生之前的年龄档（AgeAt 会拒绝出生之前的时点）。
            if (person.BirthDate > previous)
            {
                continue;
            }

            var from = AgeBracketPolicy.Of(person, previous);
            var to = AgeBracketPolicy.Of(person, month);

            if (from != to)
            {
                crossings.Add(new AgeBracketCrossFact(person.Id, from, to));
            }
        }

        return crossings;
    }
}

/// <summary>
/// ⑧ 子步的汇总结果（data-model §3.1）；<c>MonthlySettlementEngine</c> 只取其中的
/// <see cref="Lifecycle"/> 展开到 <c>SettlementResult.Lifecycle</c>。
/// </summary>
public sealed record PopulationAdvanceResult
{
    /// <summary>当月事实段。</summary>
    public required LifecycleSnapshot Lifecycle { get; init; }
}
