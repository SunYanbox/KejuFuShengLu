using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Achievements;
using KFL.Rules.Achievements;
using KFL.Rules.Career;
using KFL.Rules.Config;
using KFL.Rules.Exam;
using KFL.Rules.FamilyLifecycle;
using KFL.Rules.Punishment;
using KFL.Rules.Settlement;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// T030 / SC-001：68 条成就判据的**成立边界**与**差一邻位**（契约二十二 §2、§5）。
/// </summary>
/// <remarks>
/// <para>
/// 每条成就两条用例（共 68 × 2 = 136 条）：一条取「恰好满足」的边界，一条取「差一」的邻位。
/// 阈值一律经 <see cref="AchievementPolicy"/> 一类的配置成员取得（SC-008），
/// 本文件 MUST NOT 另存规则数值。
/// </para>
/// <para>
/// **三条判据恒 <c>false</c>**：<see cref="AchievementId.FamilyExtinct"/> 与
/// <see cref="AchievementId.HellEnding"/> 的成立条件含「绝嗣触发」这一**状态迁移**，
/// 由 <c>ExtinctionJudge.TryTrigger</c> 补判；<see cref="AchievementId.TradeLoss"/> 与
/// 调试秘辛五条是「待接线」条目（契约二十二 §4 条款 12、规格书 §14.2），在**未接线**
/// （无假数据）的上下文里一律不解锁，本文件按此断言。
/// </para>
/// <para>
/// <see cref="AchievementId.FoundationLaid"/> 判据恒 <c>true</c>，不存在差一邻位，
/// 其两条用例分别取基线上下文与「无成员的空家族」。
/// </para>
/// </remarks>
public class AchievementPredicateTests
{
    /// <summary>固定参照年月（口径同 <see cref="RulesHarness.Date"/>）。</summary>
    private static readonly GameDate 基准年月 = RulesHarness.Date;

    /// <summary>功名变迁的固定年月（档案只读，与判定时点无关）。</summary>
    private static readonly GameDate 及第年月 = new(70, 1);

    /// <summary>确定性存档标识（架构守卫 G-07：MUST NOT 使用全局随机标识源）。</summary>
    private static readonly Guid 存档标识 = new("00000000-0000-0000-0000-0000000000A5");

    /// <summary>空成就档案。</summary>
    private static readonly AchievementMark[] 无成就档案 = [];

    /// <summary>空出身档案。</summary>
    private static readonly OriginRecord[] 无出身档案 = [];

    /// <summary>
    /// 136 条判据用例：68 条成就各取「正侧」与「邻位侧」。
    /// </summary>
    /// <returns>(成就, 侧别, 用例说明, 期望结果) 四元组。</returns>
    public static TheoryData<AchievementId, int, string, bool> 判据用例()
    {
        var 数据 = new TheoryData<AchievementId, int, string, bool>();

        foreach (var id in Enum.GetValues<AchievementId>())
        {
            switch (id)
            {
                case AchievementId.FoundationLaid:
                    数据.Add(id, 0, "无条件成立（基线）", true);
                    数据.Add(id, 1, "无条件成立（空家族上下文）", true);
                    break;

                case AchievementId.TradeLoss:
                case AchievementId.DebugWakeOne:
                case AchievementId.DebugWakeTen:
                case AchievementId.DebugWakeHundred:
                case AchievementId.DebugWakeThousand:
                case AchievementId.DebugWakeTenThousand:
                    数据.Add(id, 0, "未接线 ⇒ 不解锁（无假数据）", false);
                    数据.Add(id, 1, "未接线 ⇒ 不解锁（邻位）", false);
                    break;

                case AchievementId.FamilyExtinct:
                case AchievementId.HellEnding:
                    数据.Add(id, 0, "恒假（由绝嗣补判接管）", false);
                    数据.Add(id, 1, "恒假（邻位上下文）", false);
                    break;

                default:
                    数据.Add(id, 0, "成立边界", true);
                    数据.Add(id, 1, "差一邻位", false);
                    break;
            }
        }

        return 数据;
    }

    [Theory]
    [MemberData(nameof(判据用例))]
    public void 每条成就的成立边界为真而差一邻位为假(AchievementId id, int 侧, string 说明, bool 期望)
    {
        var 判定上下文 = Context(id, 侧);
        var 实得 = AchievementPredicates.IsSatisfied(id, 判定上下文);
        var 条目 = AchievementCatalog.Of(id);

        Assert.True(
            期望 == 实得,
            $"{条目.Code} {条目.Name}（{说明}）：期望 {期望}，实得 {实得}。");
    }

    [Fact]
    public void 判据用例恰好一百三十六条且每个标识各两条()
    {
        var 用例 = 判据用例().ToArray();

        Assert.Equal(AchievementCatalog.All.Count * 2, 用例.Length);

        var 逐条计数 = 用例
            .GroupBy(行 => (AchievementId)行[0])
            .ToDictionary(组 => 组.Key, 组 => 组.Count());

        Assert.Equal(AchievementCatalog.All.Count, 逐条计数.Count);
        Assert.All(逐条计数.Values, 次数 => Assert.Equal(2, 次数));
    }

    [Fact]
    public void 六个待接线成就未接线时一律不解锁()
    {
        var 待接线 = new[]
        {
            AchievementId.TradeLoss,
            AchievementId.DebugWakeOne,
            AchievementId.DebugWakeTen,
            AchievementId.DebugWakeHundred,
            AchievementId.DebugWakeThousand,
            AchievementId.DebugWakeTenThousand,
        };

        foreach (var id in 待接线)
        {
            Assert.Equal(AchievementSource.Pending, AchievementCatalog.Of(id).Source);

            var 判定上下文 = Context(id, 0);

            // 「无假数据」：调试唤醒计数仍为 0，且没有任何当月报告被塞进来。
            Assert.Equal(0, 判定上下文.State.Achievements.DebugWakeCount);
            Assert.Null(判定上下文.Settlement);
            Assert.False(AchievementPredicates.IsSatisfied(id, 判定上下文));
        }
    }

    /// <summary>取某条成就在给定侧别的判定上下文。</summary>
    /// <param name="id">成就标识。</param>
    /// <param name="侧">0 = 正侧（成立边界 / 待接线基线）；非 0 = 差一邻位。</param>
    /// <returns>判定上下文。</returns>
    private static AchievementContext Context(AchievementId id, int 侧)
    {
        var 正侧 = 侧 == 0;

        return id switch
        {
            // 系列一：初露锋芒（ACH-001~012）
            AchievementId.FoundationLaid => 正侧
                ? 上下文(家庭(RulesHarness.Member(1, Gender.Male, 30)))
                : 上下文(new Family("空")),
            AchievementId.FirstJuRen => 上下文(家庭(正侧
                ? 及第成员(1, DegreeLevel.JuRen)
                : RulesHarness.Member(1, Gender.Male, 30))),
            AchievementId.FirstJinShi => 上下文(家庭(
                及第成员(1, 正侧 ? DegreeLevel.JinShi : DegreeLevel.JuRen))),
            AchievementId.GoldenList => 上下文(家庭(正侧
                ? 一甲成员(1, ImperialPlacement.ZhuangYuan)
                : 及第成员(1, DegreeLevel.JinShi))),
            AchievementId.ThreeGenerations => 上下文(直系家族(正侧 ? 2 : 1)),
            AchievementId.FiveGenerations => 上下文(直系家族(正侧
                ? AchievementPolicy.FiveGenerationsMinGeneration
                : AchievementPolicy.FiveGenerationsMinGeneration - 1)),
            AchievementId.TenThousandGuan => 豪门(AchievementPolicy.RichAssetsGuan, 正侧),
            AchievementId.WealthOfTheRegion => 豪门(AchievementPolicy.WealthyAssetsGuan, 正侧),
            AchievementId.Longevity => 上下文(家庭(RulesHarness.Member(
                1,
                Gender.Male,
                正侧 ? AchievementPolicy.LongevityAge : AchievementPolicy.LongevityAge - 1))),
            AchievementId.CleanRecord => 清白传家(正侧),
            AchievementId.PrisonRelease => 铁窗生涯(正侧),
            AchievementId.NarrowEscape => 上下文(
                家庭(RulesHarness.Member(1, Gender.Male, 30)),
                当刻: new GameDate(
                    正侧
                        ? AchievementPolicy.NarrowEscapeSurvivedYears + 1
                        : AchievementPolicy.NarrowEscapeSurvivedYears,
                    1),
                难度: Difficulty.Hell),

            // 系列二：科名累进（ACH-013~020）
            AchievementId.JuRenThree => 科名累进(AchievementPolicy.JuRenThreeThreshold, DegreeLevel.JuRen, 正侧),
            AchievementId.JuRenTen => 科名累进(AchievementPolicy.JuRenTenThreshold, DegreeLevel.JuRen, 正侧),
            AchievementId.JuRenHundred => 科名累进(AchievementPolicy.JuRenHundredThreshold, DegreeLevel.JuRen, 正侧),
            AchievementId.JuRenThousand => 科名累进(AchievementPolicy.JuRenThousandThreshold, DegreeLevel.JuRen, 正侧),
            AchievementId.JinShiThree => 科名累进(AchievementPolicy.JinShiThreeThreshold, DegreeLevel.JinShi, 正侧),
            AchievementId.JinShiTen => 科名累进(AchievementPolicy.JinShiTenThreshold, DegreeLevel.JinShi, 正侧),
            AchievementId.JinShiHundred => 科名累进(AchievementPolicy.JinShiHundredThreshold, DegreeLevel.JinShi, 正侧),
            AchievementId.JinShiThousand => 科名累进(AchievementPolicy.JinShiThousandThreshold, DegreeLevel.JinShi, 正侧),

            // 系列三：宦海沉浮（ACH-021~031）
            AchievementId.FirstAppointment => 释褐登仕(正侧),
            AchievementId.Prefect => 牧守一方(正侧),
            AchievementId.Chancellor => 位极人臣(正侧),
            AchievementId.Retirement => 悬车致仕(正侧),
            AchievementId.OfficeHolders => 上下文(在官家族(正侧
                ? AchievementPolicy.OfficeHoldersCount
                : AchievementPolicy.OfficeHoldersCount - 1)),
            AchievementId.Merit => 政通人和(正侧),
            AchievementId.JieShiFailuresThree => 屡挫屡奋(正侧),
            AchievementId.VenerableScholar => 皓首穷经(正侧),
            AchievementId.ImperialSweep => 鼎甲包揽(正侧),
            AchievementId.ExamFailuresTwenty => 科场遗珠(正侧),
            AchievementId.FallenAtShengShi => 名落孙山(正侧),

            // 系列四：宗族血脉（ACH-032~048）
            AchievementId.LargeHousehold => 上下文(多人家族(正侧
                ? AchievementPolicy.LargeHouseholdMembers
                : AchievementPolicy.LargeHouseholdMembers - 1)),
            AchievementId.FiftyBirths => 百子千孙(正侧),
            AchievementId.ImperialGroomPick => 榜下捉婿(正侧),
            AchievementId.DegreeMarriage => 秦晋之好(正侧),
            AchievementId.DeathInChildbirth => 红颜薄命(正侧),
            AchievementId.InfantDeath => 襁褓之殇(正侧),
            AchievementId.FarmlandHundred => 田产(AchievementPolicy.FarmlandHundredMu, 正侧),
            AchievementId.FarmlandFiveHundred => 田产(AchievementPolicy.FarmlandFiveHundredMu, 正侧),
            AchievementId.FirstShop => 上下文(
                家庭(RulesHarness.Member(1, Gender.Male, 30)),
                家产: 建家产(产业: RulesHarness.HoldingsOf(shops: 正侧 ? 1 : 0))),
            AchievementId.TradeLoss => 上下文(
                家庭(RulesHarness.Member(1, Gender.Male, 30)),
                家产: 建家产(产业: RulesHarness.HoldingsOf(shops: 正侧 ? 1 : 0))),
            AchievementId.DebtBurden => 债台高筑(正侧),
            AchievementId.DebtFree => 无债一身轻(正侧),
            AchievementId.FamilyExtinct => 正侧
                ? 上下文(new Family("绝"))
                : 上下文(家庭(RulesHarness.Member(1, Gender.Male, 30))),
            AchievementId.CenturyHouse => 存续世家(AchievementPolicy.CenturyFamilySurvivedYears, 正侧),
            AchievementId.MillenniumHouse => 存续世家(AchievementPolicy.MillenniumFamilySurvivedYears, 正侧),
            AchievementId.AllFourOrigins => 上下文(
                家庭(RulesHarness.Member(1, Gender.Male, 30)),
                出身档案: 四民档案(正侧)),
            AchievementId.HellEnding => 上下文(
                new Family("空"),
                难度: 正侧 ? Difficulty.Hell : Difficulty.Normal),

            // 系列五：覆巢之下（ACH-049~059）
            AchievementId.FirstSuccessfulBribe => 投石问路(正侧),
            AchievementId.BribeThree => 成功行贿(AchievementPolicy.BribeThreeThreshold, 正侧),
            AchievementId.BribeTen => 成功行贿(AchievementPolicy.BribeTenThreshold, 正侧),
            AchievementId.BribeHundred => 成功行贿(AchievementPolicy.BribeHundredThreshold, 正侧),
            AchievementId.BribeThousand => 成功行贿(AchievementPolicy.BribeThousandThreshold, 正侧),
            AchievementId.BribeCapReached => 无法无天(正侧),
            AchievementId.ExecutedForBribe => 身首异处(正侧),
            AchievementId.CollectivePunishment => 覆巢之下(正侧),
            AchievementId.MarriedOutPunished => 殃及池鱼(正侧),
            AchievementId.FamilyWideBan => 斯文扫地(正侧),
            AchievementId.ThreePrisoners => 囹圄之灾(正侧),

            // 系列六：饥馑求生（ACH-060~063）
            AchievementId.FirstRelief => 嗟来之食(正侧),
            AchievementId.ReliefRecoveredPaidInFull => 枯木逢春(正侧),
            AchievementId.ReliefRecoveredShortfall => 沉疴难起(正侧),
            AchievementId.FamineDeath => 路有冻骨(正侧),

            // 系列七：调试秘辛（ACH-064~068）
            AchievementId.DebugWakeOne => 调试验收(AchievementPolicy.DebugWakeOne, 正侧),
            AchievementId.DebugWakeTen => 调试验收(AchievementPolicy.DebugWakeTen, 正侧),
            AchievementId.DebugWakeHundred => 调试验收(AchievementPolicy.DebugWakeHundred, 正侧),
            AchievementId.DebugWakeThousand => 调试验收(AchievementPolicy.DebugWakeThousand, 正侧),
            AchievementId.DebugWakeTenThousand => 调试验收(AchievementPolicy.DebugWakeTenThousand, 正侧),

            _ => throw new ArgumentOutOfRangeException(nameof(id), id, "未登记的成就标识。"),
        };
    }

    // ---- 上下文装配 ----------------------------------------------------------

    /// <summary>取「在册资产达到某档」的上下文（正侧恰好达线，邻位差一贯）。</summary>
    private static AchievementContext 豪门(int 门槛贯, bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        var 现金 = RulesHarness.Guan(正侧 ? 门槛贯 : 门槛贯 - 1);
        return 上下文(家庭(成员), 家产: 建家产(资金池(现金)));
    }

    /// <summary>取「及第人数达到某档」的上下文。</summary>
    private static AchievementContext 科名累进(int 门槛, DegreeLevel 功名, bool 正侧) =>
        上下文(及第家族(正侧 ? 门槛 : 门槛 - 1, 功名));

    /// <summary>取「及第全程未行贿」的上下文。</summary>
    private static AchievementContext 清白传家(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        var 家族 = 家庭(成员);
        成员.AppendDegree(及第记录(DegreeLevel.JinShi));

        if (!正侧)
        {
            成员.RecordBribe(ExamLevel.JieShi);
        }

        return 上下文(家族);
    }

    /// <summary>取「当月有释放」的上下文。</summary>
    private static AchievementContext 铁窗生涯(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        var 释放 = 名单(正侧, new ReleaseFact(基准年月, 成员.Id, null));
        return 上下文(
            家庭(成员),
            当月报告: 报告(刑罚: PunishmentSnapshot.None(基准年月) with { Releases = 释放 }));
    }

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

    /// <summary>取「解试累计失败数」的上下文。</summary>
    private static AchievementContext 屡挫屡奋(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        var 家族 = 家庭(成员);
        var 次数 = 正侧
            ? ExamPolicy.JieShiExemptionFailureThreshold
            : ExamPolicy.JieShiExemptionFailureThreshold - 1;

        for (var i = 0; i < 次数; i++)
        {
            成员.RecordExamFailure(ExamLevel.JieShi);
        }

        return 上下文(家族);
    }

    /// <summary>取「皓首穷经」三要素齐备的上下文。</summary>
    private static AchievementContext 皓首穷经(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, AchievementPolicy.VenerableStudyAge);
        var 家族 = 家庭(成员);
        RulesHarness.Awaiting(成员, 1, AppointmentTrack.SpecialTribute);

        var 失败数 = 正侧
            ? AchievementPolicy.VenerableShengShiFailures
            : AchievementPolicy.VenerableShengShiFailures - 1;

        for (var i = 0; i < 失败数; i++)
        {
            成员.RecordExamFailure(ExamLevel.ShengShi);
        }

        return 上下文(家族);
    }

    /// <summary>取「鼎甲包揽」的上下文。</summary>
    private static AchievementContext 鼎甲包揽(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        var 家族 = 家庭(成员);
        成员.AppendDegree(一甲记录(ImperialPlacement.ZhuangYuan));
        成员.AppendDegree(一甲记录(ImperialPlacement.BangYan));

        if (正侧)
        {
            成员.AppendDegree(一甲记录(ImperialPlacement.TanHua));
        }

        return 上下文(家族);
    }

    /// <summary>取「科场遗珠」的上下文。</summary>
    private static AchievementContext 科场遗珠(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        var 家族 = 家庭(成员);
        var 次数 = 正侧
            ? AchievementPolicy.ExamFailuresPileUp
            : AchievementPolicy.ExamFailuresPileUp - 1;

        for (var i = 0; i < 次数; i++)
        {
            成员.RecordExamFailure(ExamLevel.ShengShi);
        }

        return 上下文(家族);
    }

    /// <summary>取「名落孙山」的上下文（省试落榜且学业达线）。</summary>
    private static AchievementContext 名落孙山(bool 正侧)
    {
        var 成员 = RulesHarness.Member(1, Gender.Male, 30);
        成员.Study = 正侧 ? AchievementPolicy.GeniusStudy : AchievementPolicy.GeniusStudy - 1;
        var 家族 = 家庭(成员);
        var 结果 = new ExamOutcomeLine(
            成员.Id,
            ExamLevel.ShengShi,
            Passed: false,
            Exempted: false,
            ImperialClass: null,
            Placement: null,
            FailureCountAfter: 1,
            DegreeAfter: DegreeLevel.JuRen);
        return 上下文(
            家族,
            当月报告: 报告(科举: ExamSeasonSnapshot.None(基准年月) with { Outcomes = new[] { 结果 } }));
    }

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

    /// <summary>
    /// 取调试秘辛的上下文：正侧 = **未接线基线**（唤醒计数 0，无假数据）；
    /// 邻位 = 距阈值恰好差一。
    /// </summary>
    private static AchievementContext 调试验收(int 阈值, bool 正侧) => 上下文(
        家庭(RulesHarness.Member(1, Gender.Male, 30)),
        装配: state => state.Achievements.ReportDebugWake(正侧 ? 0 : 阈值 - 1));

    // ---- 判定上下文与快照 ----------------------------------------------------

    /// <summary>按显式组成装配判定上下文。</summary>
    private static AchievementContext 上下文(
        Family 家族,
        SettlementResult? 当月报告 = null,
        GameDate? 当刻 = null,
        Difficulty 难度 = Difficulty.Normal,
        FamilyEconomy? 家产 = null,
        IReadOnlyList<OriginRecord>? 出身档案 = null,
        Action<GameState>? 装配 = null)
    {
        var 此刻 = 当刻 ?? 基准年月;
        var 状态 = new GameState(存档标识, 此刻, 难度, Origin.Farmer, 家族, 家产 ?? 建家产());
        装配?.Invoke(状态);
        return new AchievementContext(状态, 当月报告, 无成就档案, 出身档案 ?? 无出身档案, 此刻);
    }

    /// <summary>按显式组成装配当月报告。</summary>
    private static SettlementResult 报告(
        PunishmentSnapshot? 刑罚 = null,
        CareerAdvanceResult? 官吏 = null,
        ExamSeasonSnapshot? 科举 = null,
        LifecycleSnapshot? 人生 = null,
        FamineState? 饥馑前 = null,
        FamineState? 饥馑后 = null,
        Money 应付 = default,
        Money 实付 = default) => new()
        {
            Month = 基准年月,
            GrainPriceIndexBefore = 1m,
            GrainPriceIndexAfter = 1m,
            LivingCosts = [],
            LivingCostPayable = 应付,
            LivingCostPaid = 实付,
            Incomes = [],
            NetProfit = Money.Zero,
            LoanInterestAccrued = Money.Zero,
            LoanRepayment = default,
            SavingsInterest = Money.Zero,
            ArtisanBonus = Money.Zero,
            FamineBefore = 饥馑前 ?? new FamineState(),
            FamineAfter = 饥馑后 ?? new FamineState(),
            Entries = [],
            TreasuryPoolBefore = Money.Zero,
            TreasuryPoolAfter = Money.Zero,
            Punishment = 刑罚 ?? PunishmentSnapshot.None(基准年月),
            Career = 官吏 ?? 官吏推进(),
            ExamSeason = 科举 ?? ExamSeasonSnapshot.None(基准年月),
            Lifecycle = 人生 ?? LifecycleSnapshot.None(基准年月),
        };

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

    /// <summary>建立「人人都有同一级别及第记录」的家族。</summary>
    private static Family 及第家族(int 人数, DegreeLevel 功名)
    {
        var 成员 = new Person[人数];

        for (var i = 0; i < 人数; i++)
        {
            成员[i] = 及第成员(i + 1, 功名);
        }

        return RulesHarness.FamilyOf(成员);
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

    /// <summary>创建一名带及第记录的成员。</summary>
    private static Person 及第成员(int 序号, DegreeLevel 功名)
    {
        var 成员 = RulesHarness.Member(序号, Gender.Male, 30);
        成员.AppendDegree(及第记录(功名));
        return 成员;
    }

    /// <summary>创建一名一甲及第的成员。</summary>
    private static Person 一甲成员(int 序号, ImperialPlacement 名次)
    {
        var 成员 = RulesHarness.Member(序号, Gender.Male, 30);
        成员.AppendDegree(一甲记录(名次));
        return 成员;
    }

    /// <summary>一条「及第」功名变迁记录（无甲第、无名次）。</summary>
    private static DegreeRecord 及第记录(DegreeLevel 功名) =>
        new(功名, null, 及第年月, DegreeChangeCause.ExamPass);

    /// <summary>一条「进士一甲」功名变迁记录。</summary>
    private static DegreeRecord 一甲记录(ImperialPlacement 名次) =>
        new(DegreeLevel.JinShi, 名次, 及第年月, DegreeChangeCause.ExamPass, ImperialClass.FirstClass);

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
