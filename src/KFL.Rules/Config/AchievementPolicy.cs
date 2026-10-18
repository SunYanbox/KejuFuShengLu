namespace KFL.Rules.Config;

/// <summary>
/// **68 条成就阈值**的唯一真源（规格书 §14.1；契约二十二 §2；R-18）。
/// </summary>
/// <remarks>
/// <para>
/// 本类的每个常量都在契约二十二 §2 的「阈值」列有唯一出处；判据类
/// （<c>AchievementPredicates</c>）MUST NOT 另写数值副本（SC-008）。
/// </para>
/// <para>
/// **分档符号是口径的一部分**，逐条写在成员注释里：`>` 与 `>=` 在边界月差一个月，
/// 历史上已被 E-08 钉过一次（`百年世家` 与 `绝处逢生` 同为 100，但前者严格大于、后者取等）。
/// </para>
/// <para>
/// **金额阈值（<c>*Guan</c>）**一律经 <c>Money.FromGuan(AchievementPolicy.XxxGuan)</c> 使用，
/// 判据里 MUST NOT 写金额字面量（契约二十二 §2 条款 4；<c>ConfigLiteralRules.EvaluateMoneyLiterals</c>）。
/// </para>
/// </remarks>
public static class AchievementPolicy
{
    // ---- ACH-045 / ACH-046 / ACH-012：存续年数 -------------------------------------------------

    /// <summary>`百年世家` 的存续年数门槛；**严格 `>`**（§14.1:611「超过 100 年」）。</summary>
    public const int CenturyFamilySurvivedYears = 100;

    /// <summary>`千秋万代` 的存续年数门槛；**严格 `>`**。</summary>
    public const int MillenniumFamilySurvivedYears = 300;

    /// <summary>`绝处逢生` 的存续年数门槛；**取等 `>=`**（§14.1:570 无「超过」字样）。</summary>
    public const int NarrowEscapeSurvivedYears = 100;

    // ---- 家族与人口 ---------------------------------------------------------------------------

    /// <summary>`五世其昌` 的辈分门槛；**取等 `>=`**（辈分为 0 基准）。</summary>
    public const int FiveGenerationsMinGeneration = 4;

    /// <summary>`瓜瓞绵绵` 的累计出生人数门槛；**取等 `>=`**（含夭折）。</summary>
    public const int ClanBirthCount = 50;

    /// <summary>`钟鸣鼎食` 的总资产门槛（贯）；**取等 `>=`**。</summary>
    public const int RichAssetsGuan = 10_000;

    /// <summary>`富可敌国` 的总资产门槛（贯）；**取等 `>=`**。</summary>
    public const int WealthyAssetsGuan = 50_000;

    /// <summary>`儿孙满堂` 的家口数门槛（在册 + 归档的全量成员）；**取等 `>=`**。</summary>
    public const int LargeHouseholdMembers = 20;

    // ---- 寿命、学业与仕途 ---------------------------------------------------------------------

    /// <summary>`寿比南山` 的享年门槛；**取等 `>=`**。</summary>
    public const int LongevityAge = 80;

    /// <summary>`大器晚成` 的年龄门槛；**取等 `>=`**。</summary>
    public const int VenerableStudyAge = 50;

    /// <summary>`大器晚成` 的省试累计失败门槛；**取等 `>=`**。</summary>
    public const int VenerableShengShiFailures = 6;

    /// <summary>`天资卓绝` 的学业门槛；**取等 `>=`**。</summary>
    public const int GeniusStudy = 80;

    /// <summary>`债台高筑` 的贷款本息门槛（贯）；**严格 `>`**。</summary>
    public const int DebtBurdenGuan = 100;

    /// <summary>`屡败屡战` 的累计落第门槛（三场之和）；**取等 `>=`**。</summary>
    public const int ExamFailuresPileUp = 20;

    /// <summary>`五人在朝` 的在职官员数门槛；**取等 `>=`**。</summary>
    public const int OfficeHoldersCount = 5;

    /// <summary>`囹圄之灾` 的服刑人数门槛；**取等 `>=`**。</summary>
    public const int PrisonerCount = 3;

    /// <summary>`政绩卓著` 的政绩门槛；**取等 `>=`**。</summary>
    public const int MeritThreshold = 100;

    /// <summary>`位列三公` 的官阶下界（含）。</summary>
    public const int PrefectRankMin = 5;

    /// <summary>`位列三公` 的官阶上界（含）。</summary>
    public const int PrefectRankMax = 8;

    // ---- 田产 ---------------------------------------------------------------------------------

    /// <summary>`良田百亩` 的田亩门槛；**取等 `>=`**。</summary>
    public const int FarmlandHundredMu = 100;

    /// <summary>`阡陌纵横` 的田亩门槛；**取等 `>=`**。</summary>
    public const int FarmlandFiveHundredMu = 500;

    // ---- 科名累进（举人 / 进士 / 行贿：3 / 10 / 100 / 1000）------------------------------------

    /// <summary>举人累进第一档门槛；**取等 `>=`**。</summary>
    public const int JuRenThreeThreshold = 3;

    /// <summary>举人累进第二档门槛；**取等 `>=`**。</summary>
    public const int JuRenTenThreshold = 10;

    /// <summary>举人累进第三档门槛；**取等 `>=`**。</summary>
    public const int JuRenHundredThreshold = 100;

    /// <summary>举人累进第四档门槛；**取等 `>=`**。</summary>
    public const int JuRenThousandThreshold = 1_000;

    /// <summary>进士累进第一档门槛；**取等 `>=`**。</summary>
    public const int JinShiThreeThreshold = 3;

    /// <summary>进士累进第二档门槛；**取等 `>=`**。</summary>
    public const int JinShiTenThreshold = 10;

    /// <summary>进士累进第三档门槛；**取等 `>=`**。</summary>
    public const int JinShiHundredThreshold = 100;

    /// <summary>进士累进第四档门槛；**取等 `>=`**。</summary>
    public const int JinShiThousandThreshold = 1_000;

    /// <summary>贿赂累进第一档门槛；**取等 `>=`**。</summary>
    public const int BribeThreeThreshold = 3;

    /// <summary>贿赂累进第二档门槛；**取等 `>=`**。</summary>
    public const int BribeTenThreshold = 10;

    /// <summary>贿赂累进第三档门槛；**取等 `>=`**。</summary>
    public const int BribeHundredThreshold = 100;

    /// <summary>贿赂累进第四档门槛；**取等 `>=`**。</summary>
    public const int BribeThousandThreshold = 1_000;

    // ---- 调试唤醒（触发源未交付：FR-043 的计数接缝）--------------------------------------------

    /// <summary>调试唤醒第一档门槛；**取等 `>=`**。</summary>
    public const int DebugWakeOne = 1;

    /// <summary>调试唤醒第二档门槛；**取等 `>=`**。</summary>
    public const int DebugWakeTen = 10;

    /// <summary>调试唤醒第三档门槛；**取等 `>=`**。</summary>
    public const int DebugWakeHundred = 100;

    /// <summary>调试唤醒第四档门槛；**取等 `>=`**。</summary>
    public const int DebugWakeThousand = 1_000;

    /// <summary>调试唤醒第五档门槛；**取等 `>=`**。</summary>
    public const int DebugWakeTenThousand = 10_000;

    // ---- 待接线：仅登记原文百分比，MUST NOT 由此实现掷骰（FR-043）--------------------------------

    /// <summary>`商海浮沉` 的亏损幅度下界（百分比）；**待接线**。</summary>
    /// <remarks>
    /// 本特性的交付范围**不含**经商亏损事件的概率与幅度规则（规格书 §17 禁止自创影响平衡的数值）；
    /// 这两个常量只是把 §14.1 的**原文百分比**登记在数值单点处，供触发源交付后接线。
    /// </remarks>
    public const int TradeLossMinPercent = 10;

    /// <summary>`商海浮沉` 的亏损幅度上界（百分比）；**待接线**。</summary>
    public const int TradeLossMaxPercent = 30;

    // ---- 其余 ---------------------------------------------------------------------------------

    /// <summary>`幼年夭折` 的年龄门槛；判据为 **严格 `<` 本值**。</summary>
    public const int InfantDeathMaxAge = 3;

    /// <summary>编年史 ⑥ 平均寿命的保留小数位（`Math.Round` 的第二参；契约二十四 条款 17）。</summary>
    public const int AverageLifespanDecimals = 3;
}
