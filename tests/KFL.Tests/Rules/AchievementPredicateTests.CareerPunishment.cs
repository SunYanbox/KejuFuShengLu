using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Achievements;
using KFL.Rules.Career;
using KFL.Rules.Config;
using KFL.Rules.Punishment;
using KFL.Tests.Fixtures;

namespace KFL.Tests.Rules;

public partial class AchievementPredicateTests
{
    /// <summary>取「当月首次授官」的上下文。</summary>
    private static AchievementContext 释褐登仕(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        var 授官 = 名单(
            正侧,
            new CareerAppointment(
                成员.Id,
                AppointmentTrack.FirstClass,
                new OfficialRank(SalaryTable.LowestLevel)));
        return 上下文(家庭(成员), 当月报告: 报告(官吏: 官吏推进(授官: 授官)));
    }

    /// <summary>取「当月晋升到牧守档」的上下文。</summary>
    private static AchievementContext 牧守一方(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        var 旧级 = new OfficialRank(AchievementPolicy.PrefectRankMax + 2);
        var 新级 = new OfficialRank(
            正侧 ? AchievementPolicy.PrefectRankMin : AchievementPolicy.PrefectRankMax + 1);
        var 晋升 = 名单(正侧, new CareerPromotion(成员.Id, 旧级, 新级));
        return 上下文(家庭(成员), 当月报告: 报告(官吏: 官吏推进(晋升: 晋升)));
    }

    /// <summary>取「位极人臣」的上下文。</summary>
    private static AchievementContext 位极人臣(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        成员.Rank = new OfficialRank(
            正侧 ? SalaryTable.HighestLevel : SalaryTable.HighestLevel + 1);
        return 上下文(家庭(成员));
    }

    /// <summary>取「当月致仕」的上下文。</summary>
    private static AchievementContext 悬车致仕(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        var 致仕 = 名单(正侧, 成员.Id);
        return 上下文(家庭(成员), 当月报告: 报告(官吏: 官吏推进(致仕: 致仕)));
    }

    /// <summary>取「政绩达线」的上下文。</summary>
    private static AchievementContext 政通人和(bool 正侧)
    {
        var 成员 = RulesHarness.Official(
            1,
            SalaryTable.LowestLevel,
            merit: 正侧 ? AchievementPolicy.MeritThreshold : AchievementPolicy.MeritThreshold - 1);
        return 上下文(家庭(成员));
    }

    /// <summary>取「当月有未被抓的行贿」的上下文。</summary>
    private static AchievementContext 投石问路(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        var 行贿 = new BribeFact(
            基准年月,
            成员.Id,
            ExamLevel.JieShi,
            Nth: 1,
            Amount: Money.FromGuan(1m),
            Caught: !正侧,
            CapReached: false);
        return 上下文(
            家庭(成员),
            当月报告: 报告(刑罚: PunishmentSnapshot.None(基准年月) with { Bribes = new[] { 行贿 } }));
    }

    /// <summary>取「累计成功行贿达线」的上下文。</summary>
    private static AchievementContext 成功行贿(int 门槛, bool 正侧) => 上下文(
        家庭(RulesHarness.Member(1, Gender.Male, 30)),
        装配: state => state.Achievements.RecordSuccessfulBribes(正侧 ? 门槛 : 门槛 - 1));

    /// <summary>取「行贿录取率触顶」的上下文。</summary>
    private static AchievementContext 无法无天(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        var 行贿 = new BribeFact(
            基准年月,
            成员.Id,
            ExamLevel.JieShi,
            Nth: 1,
            Amount: Money.FromGuan(1m),
            Caught: false,
            CapReached: 正侧);
        return 上下文(
            家庭(成员),
            当月报告: 报告(刑罚: PunishmentSnapshot.None(基准年月) with { Bribes = new[] { 行贿 } }));
    }

    /// <summary>取「殿试行贿被抓且该难度立即执行」的上下文。</summary>
    private static AchievementContext 身首异处(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        var 行贿 = new BribeFact(
            基准年月,
            成员.Id,
            ExamLevel.DianShi,
            Nth: 1,
            Amount: Money.FromGuan(1m),
            Caught: true,
            CapReached: false);
        return 上下文(
            家庭(成员),
            当月报告: 报告(刑罚: PunishmentSnapshot.None(基准年月) with { Bribes = new[] { 行贿 } }),
            难度: 正侧 ? Difficulty.Hard : Difficulty.Normal);
    }

    /// <summary>取「当月连坐」的上下文。</summary>
    private static AchievementContext 覆巢之下(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        var 亲属 = RulesHarness.Member(2, Gender.Male, 20);
        var 连坐 = new CollectivePunishmentFact(
            基准年月,
            成员.Id,
            正侧 ? CollectiveOutcome.Executed : CollectiveOutcome.FamilyWideBan,
            亲属.Id,
            DegreeBefore: null,
            DegreeAfter: null);
        return 上下文(
            家庭(成员, 亲属),
            当月报告: 报告(
                刑罚: PunishmentSnapshot.None(基准年月) with { CollectivePunishments = new[] { 连坐 } }));
    }

    /// <summary>取「亲属禁考落到外嫁成员头上」的上下文。</summary>
    private static AchievementContext 殃及池鱼(bool 正侧)
    {
        var 受罚者 = RulesHarness.Member(1, Gender.Male, 30);
        var 外嫁者 = RulesHarness.Member(2, Gender.Female, 25);
        var 家族 = 家庭(受罚者, 外嫁者);

        if (正侧)
        {
            外嫁者.Status |= StatusFlag.MarriedOut;
        }

        var 禁考 = new RelativeBanFact(基准年月, 受罚者.Id, 外嫁者.Id, ExamBanMonths: 12, PromotionBanMonths: null);
        return 上下文(
            家族,
            当月报告: 报告(刑罚: PunishmentSnapshot.None(基准年月) with { RelativeBans = new[] { 禁考 } }));
    }

    /// <summary>取「全族禁考禁升」的上下文。</summary>
    private static AchievementContext 斯文扫地(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        return 上下文(
            家庭(成员),
            当月报告: 报告(
                刑罚: PunishmentSnapshot.None(基准年月) with { FamilyBanDeclared = 正侧 }));
    }

    /// <summary>取「同月在册服刑人数」的上下文。</summary>
    private static AchievementContext 囹圄之灾(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        return 上下文(
            家庭(成员),
            当月报告: 报告(
                刑罚: PunishmentSnapshot.None(基准年月) with
                {
                    PrisonerCount = 正侧
                        ? AchievementPolicy.PrisonerCount
                        : AchievementPolicy.PrisonerCount - 1,
                }));
    }

    /// <summary>取「当月无官吏推进」的增量快照。</summary>
    private static CareerAdvanceResult 官吏推进(
        IReadOnlyList<CareerAppointment>? 授官 = null,
        IReadOnlyList<CareerPromotion>? 晋升 = null,
        IReadOnlyList<PersonId>? 致仕 = null) => new()
        {
            Appointments = 授官 ?? [],
            MeritGains = new Dictionary<PersonId, int>(),
            Promotions = 晋升 ?? [],
            Retirements = 致仕 ?? [],
            AppraisalSkipped = [],
            AppraisalPaused = [],
            SalaryModes = new Dictionary<PersonId, SalaryMode>(),
        };

    // ---- 夹具 ---------------------------------------------------------------

    /// <summary>建立单条列表：<paramref name="正侧"/> 为假时为空表。</summary>
    private static T[] 名单<T>(bool 正侧, T 项目) =>
        正侧 ? new[] { 项目 } : Array.Empty<T>();

    /// <summary>把成员加入一个新建家族（均以开局成员身份加入）。</summary>
    private static Family 家庭(params Person[] 成员) => RulesHarness.FamilyOf(成员);

    /// <summary>建立直系家族：辈分 0 到 <paramref name="最末代"/> 各一名男丁。</summary>
    private static Family 直系家族(int 最末代)
    {
        var 家族 = new Family("测");
        var 家长 = 家族.AddFoundingMember(
            FamilyFixtures.NewPerson(1, "祖", Gender.Male, 1, 1, generation: 0));
        var 上一代 = 家长;

        for (var 代 = 1; 代 <= 最末代; 代++)
        {
            上一代 = 家族.AddChild(FamilyFixtures.NewPerson(
                代 + 1,
                $"第{代}代",
                Gender.Male,
                1 + 代,
                1,
                generation: 代,
                fatherId: 上一代.Id));
        }

        家族.SetHead(家长.Id);
        return 家族;
    }

    /// <summary>建立 <paramref name="人数"/> 名白身成员的家族。</summary>
    private static Family 多人家族(int 人数)
    {
        var 成员 = new Person[人数];

        for (var i = 0; i < 人数; i++)
        {
            成员[i] = RulesHarness.Member(i + 1, Gender.Male, 30);
        }

        return RulesHarness.FamilyOf(成员);
    }

    /// <summary>建立 <paramref name="人数"/> 名在册在官成员的家族。</summary>
    private static Family 在官家族(int 人数)
    {
        var 成员 = new Person[人数];

        for (var i = 0; i < 人数; i++)
        {
            成员[i] = RulesHarness.Official(i + 1, SalaryTable.HighestLevel + 4);
        }

        return RulesHarness.FamilyOf(成员);
    }
}
