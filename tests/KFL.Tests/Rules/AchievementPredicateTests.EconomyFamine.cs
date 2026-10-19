using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Achievements;
using KFL.Rules.Config;
using KFL.Rules.FamilyLifecycle;

namespace KFL.Tests.Rules;

public partial class AchievementPredicateTests
{
    /// <summary>取「在册资产达到某档」的上下文（正侧恰好达线，邻位差一贯）。</summary>
    private static AchievementContext 豪门(int 门槛贯, bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        var 现金 = RulesHarness.Guan(正侧 ? 门槛贯 : 门槛贯 - 1);
        return 上下文(家庭(成员), 家产: 建家产(资金池(现金)));
    }

    /// <summary>取「田产达线」的上下文。</summary>
    private static AchievementContext 田产(int 门槛亩, bool 正侧) => 上下文(
        家庭(RulesHarness.Member(1, Gender.Male, 30)),
        家产: 建家产(产业: RulesHarness.HoldingsOf(farmlandMu: 正侧 ? 门槛亩 : 门槛亩 - 1)));

    /// <summary>取「债台高筑」的上下文（严格大于门槛）。</summary>
    private static AchievementContext 债台高筑(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        return 上下文(
            家庭(成员),
            装配: state => state.Economy.Treasury.Loan.Principal = Money.FromGuan(
                正侧 ? AchievementPolicy.DebtBurdenGuan + 1 : AchievementPolicy.DebtBurdenGuan));
    }

    /// <summary>取「无债一身轻」的上下文（借过款 ∧ 已结清）。</summary>
    private static AchievementContext 无债一身轻(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        return 上下文(
            家庭(成员),
            装配: state =>
            {
                state.Achievements.RecordLoanOriginated();
                state.Economy.Treasury.Loan.Principal = 正侧 ? Money.Zero : Money.FromWen(1m);
            });
    }

    /// <summary>取「存续年数达线」的上下文（存续年数 = 当前年 − 1，门槛为严格大于）。</summary>
    private static AchievementContext 存续世家(int 门槛年, bool 正侧) => 上下文(
        家庭(RulesHarness.Member(1, Gender.Male, 30)),
        当刻: new GameDate(门槛年 + (正侧 ? 2 : 1), 1));

    /// <summary>取「饥馑首次转入救济」的上下文。</summary>
    private static AchievementContext 嗟来之食(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        return 上下文(
            家庭(成员),
            当月报告: 报告(
                饥馑前: 饥馑(FamineStage.None),
                饥馑后: 饥馑(正侧 ? FamineStage.Relief : FamineStage.None)));
    }

    /// <summary>取「救济解除且足额付清」的上下文。</summary>
    private static AchievementContext 枯木逢春(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        return 上下文(
            家庭(成员),
            当月报告: 报告(
                饥馑前: 饥馑(FamineStage.Relief),
                饥馑后: 饥馑(FamineStage.None),
                应付: Money.FromGuan(2m),
                实付: 正侧 ? Money.FromGuan(2m) : Money.FromGuan(1m)));
    }

    /// <summary>取「救济解除但未足额付清」的上下文。</summary>
    private static AchievementContext 沉疴难起(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        return 上下文(
            家庭(成员),
            当月报告: 报告(
                饥馑前: 饥馑(FamineStage.Relief),
                饥馑后: 饥馑(FamineStage.None),
                应付: Money.FromGuan(2m),
                实付: 正侧 ? Money.FromGuan(1m) : Money.FromGuan(2m)));
    }

    /// <summary>取「当月饥馑致死」的上下文。</summary>
    private static AchievementContext 路有冻骨(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        var 死亡 = new DeathFact(成员.Id, 正侧 ? DeathCause.Famine : DeathCause.OldAge);
        return 上下文(
            家庭(成员),
            当月报告: 报告(人生: LifecycleSnapshot.None(基准年月) with { Deaths = new[] { 死亡 } }));
    }

    /// <summary>按显式组成装配家族经济（米价系数取夹具值 1）。</summary>
    private static FamilyEconomy 建家产(Treasury? 资金 = null, Holdings? 产业 = null) =>
        new(LivingStandard.Normal, new GrainPriceIndex(1m), 资金, 产业);

    /// <summary>按显式组成装配资金池。</summary>
    private static Treasury 资金池(
        Money 现金 = default,
        Money 储蓄 = default,
        Money 商本 = default) => new(现金, 储蓄, 商本);

    /// <summary>构造一个处于指定阶段的饥馑快照。</summary>
    private static FamineState 饥馑(FamineStage 阶段)
    {
        var 状态 = new FamineState();
        状态.TransitionTo(阶段);
        return 状态;
    }
}
