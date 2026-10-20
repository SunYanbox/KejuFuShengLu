using KFL.Core.Config;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;
using KFL.Rules.Settlement;

namespace KFL.Rules.FamilyLifecycle;

/// <summary>
/// ⑧-b 分娩：消费上月的生育申请，逐个产妇按固定次序产出新生儿（契约十九 §7、契约二十 §6）。
/// </summary>
/// <remarks>
/// <para>
/// 处理顺序 = <see cref="FamilyLifecycleState.PendingBirths"/> 中**已到期**者（
/// <see cref="PendingBirth.AppliedAt"/> 早于当月）按<b>母亲</b>
/// （<see cref="CoupleKey.WifeId"/>）升序；每个婴儿的固定次序（MUST NOT 交换）：
/// ①掷分娩死亡（恰 1 次 <see cref="IRandomService.NextDouble"/>，命中 ⇒ 母亲登记
/// <see cref="DeathCause.Childbirth"/>，<b>新生儿照常出生</b>）→ ②性别
/// <see cref="IRandomService.Next"/>(0, 2) → ③遗传（天赋 ×4 → 学业 → 体质）→
/// ④寿数 → ⑤姓名（<see cref="INameGenerator.NextGivenName"/>，冠家族姓氏）→
/// ⑥<see cref="Family.AddChild"/>（辈分 = 父母辈分 + 1）→ ⑦落 1 条
/// <see cref="LedgerCategory.ChildBorn"/> 事件条目（金额 0）→ ⑧母亲死亡时经
/// <see cref="Family.EndMarriage"/> 置空其配偶的 <see cref="Person.SpouseId"/>。
/// </para>
/// <para>
/// <b>夭折 MUST NOT 在本子步内判定</b>（同一结算的 ⑧-f 才做，spec.md:394）。
/// 本子步只登记死亡、不改 <see cref="Person.Status"/>（不变量 11，⑧-g 是当月唯一置
/// <see cref="StatusFlag.Deceased"/> 的处所）。
/// </para>
/// <para>
/// 处理完按当月清除**已到期**记录（<see cref="FamilyLifecycleState.ConsumeBirths(GameDate)"/>）：
/// 当月新提交的申请不参与本次分娩，留待次月（契约十九 §7「申请当月不分娩」）。
/// 若申请提交后父母任一方已不在册（亡故/外嫁）⇒ 该申请作废、不消费随机、不产出婴儿
/// （无工件明文，取「不产出」）。
/// </para>
/// </remarks>
public static class Childbirth
{
    /// <summary>结算当月的全部分娩申请。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="random">随机来源。</param>
    /// <param name="names">姓名生成器。</param>
    /// <param name="deaths">当月死亡登记表（只登记，不改状态）。</param>
    /// <returns>按母亲升序的出生事实段。</returns>
    public static IReadOnlyList<BirthFact> Run(
        GameState state,
        IRandomService random,
        INameGenerator names,
        DeathRegister deaths)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(deaths);

        var family = state.Family;
        var month = state.CurrentDate;
        var facts = new List<BirthFact>();

        var pending = family.Lifecycle.PendingBirths
            .Where(entry => entry.AppliedAt < month)
            .OrderBy(entry => entry.Couple.WifeId.Value)
            .ToList();

        foreach (var entry in pending)
        {
            var husband = family.TryGet(entry.Couple.HusbandId);
            var wife = family.TryGet(entry.Couple.WifeId);

            if (husband is null || wife is null
                || !CountedMembers.IsRegistered(husband)
                || !CountedMembers.IsRegistered(wife))
            {
                continue;
            }

            // ① 分娩死亡判定（命中后母亲仍会产下婴儿）。
            var deathChance = ChildbirthPolicy.ChildbirthDeathChance
                * DifficultyRates.NegativeEventFactor(state.Difficulty);
            var motherDied = random.NextDouble() < (double)deathChance;

            if (motherDied)
            {
                deaths.Add(wife, DeathCause.Childbirth);
            }

            // ②~⑦ 由 CreateChild 统一产出：性别 → 遗传 → 寿数 → 姓名 → 入族 → 出生事件。
            var fact = CreateChild(state, husband, wife, random, names);

            // ⑧ 母亲死亡 ⇒ 其配偶的 SpouseId 置空（一夫一妻的婚姻关系解除）。
            if (motherDied)
            {
                family.EndMarriage(wife.Id);
            }

            facts.Add(fact);
        }

        family.Lifecycle.ConsumeBirths(month);

        return facts;
    }

    /// <summary>
    /// 立即产下一名子女（调试控制台；不掷分娩死亡、不消费待分娩记录）。
    /// </summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="couple">夫妇键（夫在前）。</param>
    /// <param name="random">随机来源。</param>
    /// <param name="names">姓名生成器。</param>
    /// <param name="gender">指定性别；<c>null</c> = 按正常分布掷骰。</param>
    /// <param name="talents">指定四项天赋；<c>null</c> = 按父母遗传掷骰。</param>
    /// <param name="study">指定学业；<c>null</c> = 按父母遗传掷骰。</param>
    /// <param name="health">指定体质；<c>null</c> = 按父母遗传掷骰。</param>
    /// <param name="lifespan">指定寿数；<c>null</c> = 按性别掷骰。</param>
    /// <returns>出生事实；夫妇任一方已不在册或已亡故时为 <c>null</c>。</returns>
    /// <remarks>
    /// 与 <see cref="Run"/> 共用同一套随机次序（性别 → 天赋 ×4 → 学业 → 体质 → 寿数），
    /// 只在被显式指定的项上跳过掷骰。正常月度结算 MUST NOT 调用本方法。
    /// </remarks>
    public static BirthFact? DeliverNow(
        GameState state,
        CoupleKey couple,
        IRandomService random,
        INameGenerator names,
        Gender? gender = null,
        TalentSet? talents = null,
        int? study = null,
        int? health = null,
        int? lifespan = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(names);

        var family = state.Family;
        var husband = family.TryGet(couple.HusbandId);
        var wife = family.TryGet(couple.WifeId);

        if (husband is null || wife is null
            || !CountedMembers.IsRegistered(husband)
            || !CountedMembers.IsRegistered(wife)
            || husband.Gender != Gender.Male
            || wife.Gender != Gender.Female
            || husband.SpouseId != wife.Id
            || wife.SpouseId != husband.Id)
        {
            return null;
        }

        return CreateChild(state, husband, wife, random, names, gender, talents, study, health, lifespan);
    }

    /// <summary>
    /// 在已校验的父母上产出新生儿（正常 ⑧-b 与调试入口共用）。
    /// </summary>
    private static BirthFact CreateChild(
        GameState state,
        Person husband,
        Person wife,
        IRandomService random,
        INameGenerator names,
        Gender? gender = null,
        TalentSet? talents = null,
        int? study = null,
        int? health = null,
        int? lifespan = null)
    {
        var family = state.Family;
        var month = state.CurrentDate;

        // ② 性别：0 ⇒ 男、1 ⇒ 女。
        var actualGender = gender ?? (random.Next(0, 2) == 0 ? Gender.Male : Gender.Female);

        // ③ 遗传：天赋（农、商、仕、工）→ 学业 → 体质。
        var actualTalents = talents ?? new TalentSet(
            Inheritance.Attribute(husband.Talents.Agriculture, wife.Talents.Agriculture, random),
            Inheritance.Attribute(husband.Talents.Commerce, wife.Talents.Commerce, random),
            Inheritance.Attribute(husband.Talents.Officialdom, wife.Talents.Officialdom, random),
            Inheritance.Attribute(husband.Talents.Craft, wife.Talents.Craft, random));
        var actualStudy = study ?? Inheritance.Study(husband.Study, wife.Study, random);
        var actualHealth = health ?? Inheritance.Health(husband.Health, wife.Health, random);

        // ④ 寿数（照常 roll，无上限）。
        var actualLifespan = lifespan ?? AttributePolicy.NextLifespan(actualGender, random);

        // ⑤ 姓名：冠家族姓氏。
        var name = family.Name + names.NextGivenName(actualGender);

        // ⑥ 入族（辈分 = 父母辈分 + 1；父亲为族内主轴）。
        var child = new Person(
            LifecycleIds.Next(family, "birth"),
            name,
            actualGender,
            month,
            actualTalents,
            actualLifespan,
            husband.Generation + 1,
            husband.Id,
            wife.Id)
        {
            Study = actualStudy,
            Health = actualHealth,
        };

        family.AddChild(child);

        // ⑦ 出生事件条目（事件类、金额 0、不动资金池）。
        state.Economy.Apply(LedgerCategory.ChildBorn, child.Id, Money.Zero, month);

        return new BirthFact(child.Id, actualGender, husband.Id, wife.Id);
    }
}
