using KFL.Core.Enums;

namespace KFL.Presentation;

/// <summary>
/// 68 条成就的中文「达成条件描述」只读表（规格书 §14.1）。
/// </summary>
/// <remarks>
/// <para>
/// <b>数值真源</b>：全部阈值逐条抄自规格书 §14.1「成就」小节；本表 MUST NOT 自造数值，
/// 规格书更新时须同步修正。
/// </para>
/// <para>
/// <b>键</b>：只按 <see cref="AchievementId"/> 建映射（成员名以
/// <c>src/KFL.Core/Enums/AchievementId.cs</c> 为准，序号与 <c>AchievementCatalog.All</c> 一一对应）。
/// </para>
/// <para>
/// <b>只读</b>：类型初始化时一次成型，无 LINQ / 反射 / 文件读取 / 时钟读取。
/// </para>
/// </remarks>
public static class AchievementDescriptions
{
    private static readonly Dictionary<AchievementId, string> Texts = new()
    {
        // 系列一：初露锋芒（ACH-001~012）
        [AchievementId.FoundationLaid] = "达成条件：创建新存档（开局即达成）。",
        [AchievementId.FirstJuRen] = "达成条件：家族中产生第一名举人。",
        [AchievementId.FirstJinShi] = "达成条件：家族中产生第一名进士。",
        [AchievementId.GoldenList] = "达成条件：家族成员在殿试中获得一甲（状元/榜眼/探花）。",
        [AchievementId.ThreeGenerations] = "达成条件：家族中同时存在祖、父、子三代存活成员。",
        [AchievementId.FiveGenerations] = "达成条件：家族传承至第五代（辈分序号 ≥ 4）且有在册成员存活。",
        [AchievementId.TenThousandGuan] = "达成条件：家族总资产（现金+储蓄+田宅铺市值-贷款）≥10,000 贯。",
        [AchievementId.WealthOfTheRegion] = "达成条件：家族总资产（现金+储蓄+田宅铺市值-贷款）≥50,000 贯。",
        [AchievementId.Longevity] = "达成条件：家族中任一成员年龄达到或超过 80 岁。",
        [AchievementId.CleanRecord] = "达成条件：家族中产生进士，且该进士从出生到及第全程未进行过任何行贿。",
        [AchievementId.PrisonRelease] = "达成条件：家族中有成员服刑期满并释放。",
        [AchievementId.NarrowEscape] = "达成条件：在地狱难度下，家族连续存活 100 年（≥ 100 年）。",

        // 系列二：科名累进（ACH-013~020）
        [AchievementId.JuRenThree] = "达成条件：家族累计产生 3 名举人。",
        [AchievementId.JuRenTen] = "达成条件：家族累计产生 10 名举人。",
        [AchievementId.JuRenHundred] = "达成条件：家族累计产生 100 名举人。",
        [AchievementId.JuRenThousand] = "达成条件：家族累计产生 1000 名举人。",
        [AchievementId.JinShiThree] = "达成条件：家族累计产生 3 名进士。",
        [AchievementId.JinShiTen] = "达成条件：家族累计产生 10 名进士。",
        [AchievementId.JinShiHundred] = "达成条件：家族累计产生 100 名进士。",
        [AchievementId.JinShiThousand] = "达成条件：家族累计产生 1000 名进士。",

        // 系列三：宦海沉浮（ACH-021~031）
        [AchievementId.FirstAppointment] = "达成条件：家族成员首次结束“待阙”期，正式授官。",
        [AchievementId.Prefect] = "达成条件：家族成员考课晋升至中品官（L8 ~ L5）。",
        [AchievementId.Chancellor] = "达成条件：家族成员升至 L1（宰相）。",
        [AchievementId.Retirement] = "达成条件：家族成员年满 70 岁自动致仕，领半俸。",
        [AchievementId.OfficeHolders] = "达成条件：家族同一月份内，有至少 5 名成员在职为官。",
        [AchievementId.Merit] = "达成条件：家族成员政绩达到 100（上限）。",
        [AchievementId.JieShiFailuresThree] = "达成条件：家族成员解试累计落榜 3 次，触发“免解”资格。",
        [AchievementId.VenerableScholar] = "达成条件：家族成员满 50 岁且省试累计落榜 6 次，触发“特奏名”授官（L18）。",
        [AchievementId.ImperialSweep] = "达成条件：家族历史上集齐过状元、榜眼、探花（一甲三名）。",
        [AchievementId.ExamFailuresTwenty] = "达成条件：家族成员累计落榜（解试/省试）达 20 次。",
        [AchievementId.FallenAtShengShi] = "达成条件：家族成员在学业 ≥80 的情况下，省试依然落榜。",

        // 系列四：宗族血脉（ACH-032~048）
        [AchievementId.LargeHousehold] = "达成条件：家族同时在册存活成员达到 20 人。",
        [AchievementId.FiftyBirths] = "达成条件：家族历史上累计出生（含夭折）达到 50 人。",
        [AchievementId.ImperialGroomPick] = "达成条件：触发新科一甲成员被富商提亲事件（含被拒）。",
        [AchievementId.DegreeMarriage] = "达成条件：完成一次“买功名婚姻”（娶入或嫁出，需仕族现任官员）。",
        [AchievementId.DeathInChildbirth] = "达成条件：家族女性成员因分娩死亡。",
        [AchievementId.InfantDeath] = "达成条件：家族婴儿在三岁内夭折。",
        [AchievementId.FarmlandHundred] = "达成条件：家族拥有田地达到 100 亩。",
        [AchievementId.FarmlandFiveHundred] = "达成条件：家族拥有田地达到 500 亩。",
        [AchievementId.FirstShop] = "达成条件：家族购入并拥有至少 1 间铺面（需承担风险事件）。",
        [AchievementId.TradeLoss] = "达成条件：触发经商亏损事件（损失本金 10~30%）。（本版本恒不解锁）",
        [AchievementId.DebtBurden] = "达成条件：家族产生贷款，且本金超过 100 贯。",
        [AchievementId.DebtFree] = "达成条件：在产生贷款后，彻底结清本金与欠息。",
        [AchievementId.FamilyExtinct] = "达成条件：触发家族绝嗣结局（游戏唯一终点）。",
        [AchievementId.CenturyHouse] = "达成条件：家族存续时间超过 100 年（且未绝嗣）。",
        [AchievementId.MillenniumHouse] = "达成条件：家族存续时间超过 300 年。",
        [AchievementId.AllFourOrigins] = "达成条件：在四个不同出身（农、工、商、士）的存档中，均触发过绝嗣或达成“百年世家”。",
        [AchievementId.HellEnding] = "达成条件：在“地狱”难度下触发家族绝嗣结局。",

        // 系列五：覆巢之下（ACH-049~059）
        [AchievementId.FirstSuccessfulBribe] = "达成条件：家族成员首次成功行贿。",
        [AchievementId.BribeThree] = "达成条件：家族累计行贿成功 3 次。",
        [AchievementId.BribeTen] = "达成条件：家族累计行贿成功 10 次。",
        [AchievementId.BribeHundred] = "达成条件：家族累计行贿成功 100 次。",
        [AchievementId.BribeThousand] = "达成条件：家族累计行贿成功 1000 次。",
        [AchievementId.BribeCapReached] = "达成条件：某级别考试贿赂乘数达到封顶值（89.9999%），该级别行贿入口置灰。",
        [AchievementId.ExecutedForBribe] = "达成条件：家族成员被判处死刑并立即执行。",
        [AchievementId.CollectivePunishment] = "达成条件：触发地狱难度殿试贿赂被抓的“连坐”机制，导致亲属死亡或降级。",
        [AchievementId.MarriedOutPunished] = "达成条件：已外嫁归档的女儿，因娘家连坐被跨族追及（处死或降功名）。",
        [AchievementId.FamilyWideBan] = "达成条件：家族触发“全族禁考禁升职”惩罚。",
        [AchievementId.ThreePrisoners] = "达成条件：家族同一月份内，有至少 3 名成员处于“服刑”状态。",

        // 系列六：饥馑求生（ACH-060~063）
        [AchievementId.FirstRelief] = "达成条件：家族首次因现金不足支付生活费而进入“救济模式”。",
        [AchievementId.ReliefRecoveredPaidInFull] = "达成条件：在“救济模式”结束后，当月结算能正常付得起全额生活支出。",
        [AchievementId.ReliefRecoveredShortfall] = "达成条件：在“救济模式”结束后，当月结算仍无法付得起全额生活支出（导致体质继续下降或触发死亡判定）。",
        [AchievementId.FamineDeath] = "达成条件：家族中有成员因饥馑状态（体质降至 0）而死亡。",

        // 系列七：调试秘辛（ACH-064~068，本版本恒不解锁）
        [AchievementId.DebugWakeOne] = "达成条件：首次按 S-U-N-T-I-O-N 唤醒调试控制台。（本版本恒不解锁）",
        [AchievementId.DebugWakeTen] = "达成条件：累计唤醒调试控制台 10 次。（本版本恒不解锁）",
        [AchievementId.DebugWakeHundred] = "达成条件：累计唤醒调试控制台 100 次。（本版本恒不解锁）",
        [AchievementId.DebugWakeThousand] = "达成条件：累计唤醒调试控制台 1000 次。（本版本恒不解锁）",
        [AchievementId.DebugWakeTenThousand] = "达成条件：累计唤醒调试控制台 10000 次。（本版本恒不解锁）",
    };

    /// <summary>取一条成就的「达成条件」中文描述。</summary>
    /// <param name="id">成就标识。</param>
    /// <returns>该成就的达成条件描述；未登记时返回兜底文案。</returns>
    public static string Describe(AchievementId id) =>
        Texts.TryGetValue(id, out var text) ? text : "达成条件：见规格书 §14.1。";
}
