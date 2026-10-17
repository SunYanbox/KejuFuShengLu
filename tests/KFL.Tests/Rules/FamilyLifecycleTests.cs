using System.Reflection;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Services;
using KFL.Rules.Config;
using KFL.Rules.FamilyLifecycle;
using KFL.Rules.Settlement;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// 007 家族生命周期（月度结算第 ⑧ 步）的测试基座：
/// ① 八子步「进入前 / 离开后」快照断言辅助；
/// ② 空事实段的形状与 <see cref="LifecycleSnapshot.None(GameDate)"/> 的语义；
/// ③ data-model §6 不变量 11~13 的可证面（死亡原因只承载于当月事实段、⑧-g 之后才改状态）。
/// </summary>
/// <remarks>
/// <para>
/// 第 ⑧ 步的执行器（<c>PopulationAdvance</c> 及各子步）是 <c>KFL.Rules</c> 的 <c>internal</c> 类型，
/// 测试经「公开面」观察：一次 <see cref="MonthlySettlementEngine.Settle"/> 后取
/// <see cref="SettlementResult.Lifecycle"/>。故本基座提供 <see cref="SettleMonth"/> 与
/// <see cref="SettleMonths"/> 两个入口，后续相（T041/T047/T050/T059/T063）在其上断言差值。
/// </para>
/// <para>
/// 随机来源一律用常量 0.5（<see cref="FixedRandomService"/> 用尽后的回退值）：它高于感染率上限、
/// 低于提亲概率，使 ⑧ 的消费次数只由「在册且 <c>AgeAt &gt;= 3</c> 的人数」决定，逐月可比。
/// </para>
/// <para>
/// 期望值一律经 <c>KFL.Rules/Config</c> 的成员取得（SC-008）；本文件 MUST NOT 另存规则数值。
/// </para>
/// </remarks>
public class FamilyLifecycleTests
{
    private static readonly Guid StateId = Guid.Parse("6a1f9c24-8d3b-4e57-9a02-1b2c3d4e5f60");

    [Fact]
    public void 空事实段如实为空()
    {
        var snapshot = LifecycleSnapshot.None(RulesHarness.Date);

        Assert.Empty(snapshot.Births);
        Assert.Empty(snapshot.Deaths);
        Assert.Empty(snapshot.Marriages);
        Assert.Empty(snapshot.MarriedOut);
        Assert.Empty(snapshot.Purchases);
        Assert.Empty(snapshot.Illnesses);
        Assert.Empty(snapshot.Crossings);
        Assert.Null(snapshot.PendingProposal);
        Assert.Null(snapshot.NewHeadId);
        Assert.False(snapshot.HeadBecameVacant);
    }

    [Fact]
    public void 死亡原因只承载于当月事实段()
    {
        // data-model §6 不变量 12：DeathCause 只出现在 LifecycleSnapshot.Deaths 的 DeathFact 上；
        // MUST NOT 落 Person 字段、MUST NOT 落账本（006 的处死路径也不补取值）。
        // 以「类型 × 属性」的反射扫描表达：整个公开面上恰有一处承载它。
        var carriers = typeof(Person).Assembly.GetTypes()
            .Concat(typeof(LifecycleSnapshot).Assembly.GetTypes())
            .Where(type => type.IsPublic && !type.IsNested)
            .SelectMany(type => type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.PropertyType == typeof(DeathCause))
                .Select(property => $"{type.Name}.{property.Name}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["DeathFact.Cause"], carriers);
    }

    [Fact]
    public void 死亡事实与归档状态一一对应()
    {
        var run = SettleMonth();

        // data-model §6 不变量 11：死亡登记表在 ⑧-g 之前 MUST NOT 改 Person.Status；
        // 可证面是「凡是出现在当月事实段 Deaths 里的人，结算结束时必已置 Deceased 并归档」——
        // ⑧-g 是唯一的置位通道（上一条反射断言的结论），故不早不晚。
        foreach (var death in run.Lifecycle.Deaths)
        {
            var person = run.State.Family.TryGet(death.PersonId);

            Assert.NotNull(person);
            Assert.True(person.Status.HasFlag(StatusFlag.Deceased));
            Assert.DoesNotContain(person, run.State.Family.RegisteredMembers);
            Assert.Contains(person, run.State.Family.ArchivedMembers);
        }
    }

    [Fact]
    public void 结算一个月必产出一份生命周期事实段()
    {
        var run = SettleMonth();

        // Snapshot 属性恒非 null（即使空段），且当月无婚配 / 提亲时各段为空
        // ——常量随机源下 ⑧-a 提亲骰必不命中。
        Assert.NotNull(run.Result.Lifecycle);
        Assert.Null(run.Result.Lifecycle.PendingProposal);
        Assert.Equal(RulesHarness.Date, run.Result.Month);
    }

    // ------------------------------------------------------------------ 基座：一次结算

    /// <summary>结算一个月（常量随机源）并取回状态与生命周期事实段。</summary>
    /// <param name="months">结算月数。</param>
    private static LifecycleRun SettleMonths(int months)
    {
        var child = RulesHarness.Member(1, Gender.Male, 5);
        var family = RulesHarness.FamilyOf(child);
        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(),
            new Holdings());
        var state = new GameState(StateId, RulesHarness.Date, Difficulty.Normal, Origin.Artisan, family, economy);
        var random = new FixedRandomService();
        var engine = new MonthlySettlementEngine(random, new GameStateClock(state), new FixedNameGenerator());
        var lifecycle = LifecycleSnapshot.None(state.CurrentDate);
        SettlementResult? last = null;

        for (var index = 0; index < months; index++)
        {
            last = engine.Settle(state);
            lifecycle = last.Lifecycle;
        }

        Assert.NotNull(last);

        return new LifecycleRun(state, lifecycle, last);
    }

    /// <summary>结算一个月。</summary>
    private static LifecycleRun SettleMonth() => SettleMonths(1);

    /// <summary>一次结算的观测量：结算后状态、本月生命周期事实段、本月完整快照。</summary>
    private sealed record LifecycleRun(GameState State, LifecycleSnapshot Lifecycle, SettlementResult Result);
}
