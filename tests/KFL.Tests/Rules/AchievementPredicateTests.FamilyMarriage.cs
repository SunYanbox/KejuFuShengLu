using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Achievements;
using KFL.Rules.Achievements;
using KFL.Rules.Config;
using KFL.Rules.FamilyLifecycle;
using KFL.Tests.Fixtures;

namespace KFL.Tests.Rules;

public partial class AchievementPredicateTests
{
    /// <summary>取「百子千孙」的上下文。</summary>
    private static AchievementContext 百子千孙(bool 正侧) => 上下文(
        家庭(RulesHarness.Member(1, Gender.Male, 30)),
        装配: state => state.Achievements.RecordBirths(
            正侧 ? AchievementPolicy.ClanBirthCount : AchievementPolicy.ClanBirthCount - 1));

    /// <summary>取「榜下捉婿」的上下文（当月待应答提议的种类）。</summary>
    private static AchievementContext 榜下捉婿(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 25);
        var 提议 = 建提议(
            成员.Id,
            正侧 ? MarriageProposalKind.ImperialProposal : MarriageProposalKind.Matchmaker);
        return 上下文(
            家庭(成员),
            当月报告: 报告(人生: LifecycleSnapshot.None(基准年月) with { PendingProposal = 提议 }));
    }

    /// <summary>取「秦晋之好」的上下文。</summary>
    private static AchievementContext 秦晋之好(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        var 婚配 = new MarriageFact(
            成员.Id,
            FamilyFixtures.Id(2),
            MarriageClass.Scholar,
            IsDegreeMarriage: 正侧,
            BetrothalGift: Money.Zero,
            Dowry: Money.Zero);
        return 上下文(
            家庭(成员),
            当月报告: 报告(人生: LifecycleSnapshot.None(基准年月) with { Marriages = new[] { 婚配 } }));
    }

    /// <summary>取「红颜薄命」的上下文（分娩而亡的性别边界）。</summary>
    private static AchievementContext 红颜薄命(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, 正侧 ? Gender.Female : Gender.Male, 25);
        var 死亡 = new DeathFact(成员.Id, DeathCause.Childbirth);
        return 上下文(
            家庭(成员),
            当月报告: 报告(人生: LifecycleSnapshot.None(基准年月) with { Deaths = new[] { 死亡 } }));
    }

    /// <summary>取「襁褓之殇」的上下文（用**事实月**而非当前月的年龄）。</summary>
    private static AchievementContext 襁褓之殇(bool 正侧)
    {
        var 年龄 = 正侧 ? 0 : AchievementPolicy.InfantDeathMaxAge;
        var 成员 = RulesHarness.Member(1, Gender.Male, 年龄);
        var 死亡 = new DeathFact(成员.Id, DeathCause.Infancy);
        return 上下文(
            家庭(成员),
            当月报告: 报告(人生: LifecycleSnapshot.None(基准年月) with { Deaths = new[] { 死亡 } }));
    }

    /// <summary>
    /// 取调试秘辛的上下文：正侧 = **未接线基线**（唤醒计数 0，无假数据）；
    /// 邻位 = 距阈值恰好差一。
    /// </summary>
    private static AchievementContext 调试验收(int 阈值, bool 正侧) => 上下文(
        家庭(RulesHarness.Member(1, Gender.Male, 30)),
        装配: state => state.Achievements.ReportDebugWake(正侧 ? 0 : 阈值 - 1));

    // ---- 判定上下文与快照 ----------------------------------------------------

    /// <summary>构造一条待应答婚配提议（候选者取值固定，仅种类可变）。</summary>
    private static MarriageProposal 建提议(PersonId 我方成员, MarriageProposalKind 种类) => new()
    {
        Kind = 种类,
        OurMemberId = 我方成员,
        CandidateName = "候选",
        CandidateGender = Gender.Female,
        CandidateAge = 18,
        TalentAgriculture = 50,
        TalentCommerce = 50,
        TalentScholar = 50,
        TalentCraft = 50,
        CandidateStudy = 50,
        CandidateHealth = 50,
        CandidateLifespan = 60,
        CandidateDegree = DegreeLevel.BaiShen,
        Class = MarriageClass.Commoner,
        BetrothalGift = Money.Zero,
        Dowry = Money.Zero,
        ProposedAt = 基准年月,
    };

    /// <summary>四民出身档案；<paramref name="齐四民"/> 为假时只有前三民。</summary>
    private static List<OriginRecord> 四民档案(bool 齐四民)
    {
        Origin[] 全部 = [Origin.Farmer, Origin.Artisan, Origin.Merchant, Origin.Scholar];
        var 取用 = 齐四民 ? 全部 : 全部[..3];
        var 档案 = new List<OriginRecord>(取用.Length);

        foreach (var 出身 in 取用)
        {
            档案.Add(new OriginRecord(出身, OriginMilestone.Extinction, 基准年月, 存档标识));
        }

        return 档案;
    }
}
