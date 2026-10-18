namespace KFL.Core.Enums;

/// <summary>
/// 成就标识（规格书 §14.1 的七个系列、68 条成就；data-model §2.1）。
/// </summary>
/// <remarks>
/// <para>
/// **值约定**：数值 = `ACH-xxx` 的 `xxx`（十进制、从 1 起、连续无洞）。目录断言（
/// <c>AchievementCatalogTests</c>）用它做「齐全性」核对。
/// </para>
/// <para>
/// **唯一字典**：成员名 ↔ `ACH-xxx` ↔ 中文名的三元组以
/// <c>specs/008-achievements-extinction/contracts/achievement-catalog.md</c> §1 与
/// <c>data-model.md</c> §2.1 为唯一真源，MUST NOT 在别处另立副本。中文名只作为注释，
/// **MUST NOT** 出现在线上键或判据里。
/// </para>
/// <para>
/// **系列归属**（与 <see cref="AchievementSeries"/> 的区间严格一致）：
/// 初露锋芒 `001~012`、科名累进 `013~020`、宦海沉浮 `021~031`、宗族血脉 `032~048`、
/// 覆巢之下 `049~059`、饥馑求生 `060~063`、调试秘辛 `064~068`。
/// </para>
/// </remarks>
public enum AchievementId
{
    // 系列一：初露锋芒（ACH-001~012）
    FoundationLaid = 1,        // ACH-001 开基立业
    FirstJuRen = 2,            // ACH-002 首个举人
    FirstJinShi = 3,           // ACH-003 首个进士
    GoldenList = 4,            // ACH-004 金榜题名
    ThreeGenerations = 5,      // ACH-005 三代同堂
    FiveGenerations = 6,       // ACH-006 五世其昌
    TenThousandGuan = 7,       // ACH-007 家资万贯
    WealthOfTheRegion = 8,     // ACH-008 富甲一方
    Longevity = 9,             // ACH-009 寿比南山
    CleanRecord = 10,          // ACH-010 清白传家
    PrisonRelease = 11,        // ACH-011 铁窗生涯
    NarrowEscape = 12,         // ACH-012 绝处逢生

    // 系列二：科名累进（ACH-013~020）
    JuRenThree = 13,           // ACH-013 杏林初茂
    JuRenTen = 14,             // ACH-014 桂子飘香
    JuRenHundred = 15,         // ACH-015 满门朱紫
    JuRenThousand = 16,        // ACH-016 文运昌隆
    JinShiThree = 17,          // ACH-017 雁塔题名
    JinShiTen = 18,            // ACH-018 簪缨世族
    JinShiHundred = 19,        // ACH-019 朝堂半壁
    JinShiThousand = 20,       // ACH-020 天下文宗

    // 系列三：宦海沉浮（ACH-021~031）
    FirstAppointment = 21,     // ACH-021 释褐登仕
    Prefect = 22,              // ACH-022 牧守一方
    Chancellor = 23,           // ACH-023 位极人臣
    Retirement = 24,           // ACH-024 悬车致仕
    OfficeHolders = 25,        // ACH-025 袍笏登场
    Merit = 26,                // ACH-026 政通人和
    JieShiFailuresThree = 27,  // ACH-027 屡挫屡奋
    VenerableScholar = 28,     // ACH-028 皓首穷经
    ImperialSweep = 29,        // ACH-029 鼎甲包揽
    ExamFailuresTwenty = 30,   // ACH-030 科场遗珠
    FallenAtShengShi = 31,     // ACH-031 名落孙山

    // 系列四：宗族血脉（ACH-032~048）
    LargeHousehold = 32,       // ACH-032 人丁兴旺
    FiftyBirths = 33,          // ACH-033 百子千孙
    ImperialGroomPick = 34,    // ACH-034 榜下捉婿
    DegreeMarriage = 35,       // ACH-035 秦晋之好
    DeathInChildbirth = 36,    // ACH-036 红颜薄命
    InfantDeath = 37,          // ACH-037 襁褓之殇
    FarmlandHundred = 38,      // ACH-038 阡陌交通
    FarmlandFiveHundred = 39,  // ACH-039 良田万顷
    FirstShop = 40,            // ACH-040 旺铺之主
    TradeLoss = 41,            // ACH-041 商海浮沉（待接线，恒 false）
    DebtBurden = 42,           // ACH-042 债台高筑
    DebtFree = 43,             // ACH-043 无债一身轻
    FamilyExtinct = 44,        // ACH-044 烟消云散
    CenturyHouse = 45,         // ACH-045 百年世家
    MillenniumHouse = 46,      // ACH-046 千秋万代
    AllFourOrigins = 47,       // ACH-047 四民皆历
    HellEnding = 48,           // ACH-048 大梦一场

    // 系列五：覆巢之下（ACH-049~059）
    FirstSuccessfulBribe = 49, // ACH-049 投石问路
    BribeThree = 50,           // ACH-050 暗度陈仓
    BribeTen = 51,             // ACH-051 权钱交易
    BribeHundred = 52,         // ACH-052 只手遮天
    BribeThousand = 53,        // ACH-053 买断功名
    BribeCapReached = 54,      // ACH-054 无法无天
    ExecutedForBribe = 55,     // ACH-055 身首异处
    CollectivePunishment = 56, // ACH-056 覆巢之下
    MarriedOutPunished = 57,   // ACH-057 殃及池鱼
    FamilyWideBan = 58,        // ACH-058 斯文扫地
    ThreePrisoners = 59,       // ACH-059 囹圄之灾

    // 系列六：饥馑求生（ACH-060~063）
    FirstRelief = 60,          // ACH-060 嗟来之食
    ReliefRecoveredPaidInFull = 61,   // ACH-061 枯木逢春
    ReliefRecoveredShortfall = 62,    // ACH-062 沉疴难起
    FamineDeath = 63,          // ACH-063 路有冻骨

    // 系列七：调试秘辛（ACH-064~068，待接线）
    DebugWakeOne = 64,         // ACH-064 天机泄露
    DebugWakeTen = 65,         // ACH-065 窥探虚实
    DebugWakeHundred = 66,     // ACH-066 篡改命数
    DebugWakeThousand = 67,    // ACH-067 虚空主宰
    DebugWakeTenThousand = 68, // ACH-068 代码飞升
}
