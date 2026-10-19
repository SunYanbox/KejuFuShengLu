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
public partial class AchievementPredicateTests
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
}
