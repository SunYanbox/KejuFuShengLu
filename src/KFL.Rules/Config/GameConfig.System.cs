
namespace KFL.Rules.Config;

public static partial class GameConfig
{
    /// <summary>存档节奏（§14；004）：纪元与自动备份间隔（只转发）。</summary>
    public static class Save
    {
        /// <summary>模拟纪元年——模拟总是从这一年的第一个月开始（§3）。</summary>
        public static int EpochYear => SaveCadencePolicy.EpochYear;

        /// <summary>模拟纪元月。</summary>
        public static int EpochMonth => SaveCadencePolicy.EpochMonth;

        /// <summary>自动备份间隔（回合数；§14「每 3 回合」）。</summary>
        public static int BackupIntervalTurns => SaveCadencePolicy.BackupIntervalTurns;
    }
    /// <summary>
    /// 成就阈值视图（§14.1；008）：68 条成就的门槛（**只转发** <see cref="AchievementPolicy"/>）。
    /// </summary>
    /// <remarks>
    /// 本视图是 data-model §3.10 的视图：本特性**有**同名策略类 <c>AchievementPolicy</c>，
    /// 视图的作用是让「规则数值只住在 <c>Config/</c>」这一断言在调用侧也成立（SC-008），
    /// 因此逐成员纯转发、**零字面量**，分档符号（`>` 与 `>=`）以原文注释转述。
    /// </remarks>
    public static class Achievements
    {
        /// <summary>`百年世家` 存续年数；严格 `>`。</summary>
        public static int CenturyFamilySurvivedYears => AchievementPolicy.CenturyFamilySurvivedYears;

        /// <summary>`千秋万代` 存续年数；严格 `>`。</summary>
        public static int MillenniumFamilySurvivedYears => AchievementPolicy.MillenniumFamilySurvivedYears;

        /// <summary>`绝处逢生` 存续年数；`>=`。</summary>
        public static int NarrowEscapeSurvivedYears => AchievementPolicy.NarrowEscapeSurvivedYears;

        /// <summary>`五世其昌` 辈分门槛；`>=`。</summary>
        public static int FiveGenerationsMinGeneration => AchievementPolicy.FiveGenerationsMinGeneration;

        /// <summary>`瓜瓞绵绵` 累计出生人数；`>=`。</summary>
        public static int ClanBirthCount => AchievementPolicy.ClanBirthCount;

        /// <summary>`钟鸣鼎食` 总资产（贯）；`>=`。</summary>
        public static int RichAssetsGuan => AchievementPolicy.RichAssetsGuan;

        /// <summary>`富可敌国` 总资产（贯）；`>=`。</summary>
        public static int WealthyAssetsGuan => AchievementPolicy.WealthyAssetsGuan;

        /// <summary>`儿孙满堂` 家口数；`>=`。</summary>
        public static int LargeHouseholdMembers => AchievementPolicy.LargeHouseholdMembers;

        /// <summary>`寿比南山` 享年；`>=`。</summary>
        public static int LongevityAge => AchievementPolicy.LongevityAge;

        /// <summary>`大器晚成` 年龄；`>=`。</summary>
        public static int VenerableStudyAge => AchievementPolicy.VenerableStudyAge;

        /// <summary>`大器晚成` 省试累计失败；`>=`。</summary>
        public static int VenerableShengShiFailures => AchievementPolicy.VenerableShengShiFailures;

        /// <summary>`天资卓绝` 学业；`>=`。</summary>
        public static int GeniusStudy => AchievementPolicy.GeniusStudy;

        /// <summary>`债台高筑` 贷款本息（贯）；严格 `>`。</summary>
        public static int DebtBurdenGuan => AchievementPolicy.DebtBurdenGuan;

        /// <summary>`屡败屡战` 累计落第；`>=`。</summary>
        public static int ExamFailuresPileUp => AchievementPolicy.ExamFailuresPileUp;

        /// <summary>`五人在朝` 在职官员数；`>=`。</summary>
        public static int OfficeHoldersCount => AchievementPolicy.OfficeHoldersCount;

        /// <summary>`囹圄之灾` 服刑人数；`>=`。</summary>
        public static int PrisonerCount => AchievementPolicy.PrisonerCount;

        /// <summary>`政绩卓著` 政绩；`>=`。</summary>
        public static int MeritThreshold => AchievementPolicy.MeritThreshold;

        /// <summary>`位列三公` 官阶下界（含）。</summary>
        public static int PrefectRankMin => AchievementPolicy.PrefectRankMin;

        /// <summary>`位列三公` 官阶上界（含）。</summary>
        public static int PrefectRankMax => AchievementPolicy.PrefectRankMax;

        /// <summary>`良田百亩` 田亩；`>=`。</summary>
        public static int FarmlandHundredMu => AchievementPolicy.FarmlandHundredMu;

        /// <summary>`阡陌纵横` 田亩；`>=`。</summary>
        public static int FarmlandFiveHundredMu => AchievementPolicy.FarmlandFiveHundredMu;

        /// <summary>举人累进第一档；`>=`。</summary>
        public static int JuRenThreeThreshold => AchievementPolicy.JuRenThreeThreshold;

        /// <summary>举人累进第二档；`>=`。</summary>
        public static int JuRenTenThreshold => AchievementPolicy.JuRenTenThreshold;

        /// <summary>举人累进第三档；`>=`。</summary>
        public static int JuRenHundredThreshold => AchievementPolicy.JuRenHundredThreshold;

        /// <summary>举人累进第四档；`>=`。</summary>
        public static int JuRenThousandThreshold => AchievementPolicy.JuRenThousandThreshold;

        /// <summary>进士累进第一档；`>=`。</summary>
        public static int JinShiThreeThreshold => AchievementPolicy.JinShiThreeThreshold;

        /// <summary>进士累进第二档；`>=`。</summary>
        public static int JinShiTenThreshold => AchievementPolicy.JinShiTenThreshold;

        /// <summary>进士累进第三档；`>=`。</summary>
        public static int JinShiHundredThreshold => AchievementPolicy.JinShiHundredThreshold;

        /// <summary>进士累进第四档；`>=`。</summary>
        public static int JinShiThousandThreshold => AchievementPolicy.JinShiThousandThreshold;

        /// <summary>贿赂累进第一档；`>=`。</summary>
        public static int BribeThreeThreshold => AchievementPolicy.BribeThreeThreshold;

        /// <summary>贿赂累进第二档；`>=`。</summary>
        public static int BribeTenThreshold => AchievementPolicy.BribeTenThreshold;

        /// <summary>贿赂累进第三档；`>=`。</summary>
        public static int BribeHundredThreshold => AchievementPolicy.BribeHundredThreshold;

        /// <summary>贿赂累进第四档；`>=`。</summary>
        public static int BribeThousandThreshold => AchievementPolicy.BribeThousandThreshold;

        /// <summary>调试唤醒第一档；`>=`。</summary>
        public static int DebugWakeOne => AchievementPolicy.DebugWakeOne;

        /// <summary>调试唤醒第二档；`>=`。</summary>
        public static int DebugWakeTen => AchievementPolicy.DebugWakeTen;

        /// <summary>调试唤醒第三档；`>=`。</summary>
        public static int DebugWakeHundred => AchievementPolicy.DebugWakeHundred;

        /// <summary>调试唤醒第四档；`>=`。</summary>
        public static int DebugWakeThousand => AchievementPolicy.DebugWakeThousand;

        /// <summary>调试唤醒第五档；`>=`。</summary>
        public static int DebugWakeTenThousand => AchievementPolicy.DebugWakeTenThousand;

        /// <summary>`商海浮沉` 亏损幅度下界（百分比）；待接线。</summary>
        public static int TradeLossMinPercent => AchievementPolicy.TradeLossMinPercent;

        /// <summary>`商海浮沉` 亏损幅度上界（百分比）；待接线。</summary>
        public static int TradeLossMaxPercent => AchievementPolicy.TradeLossMaxPercent;

        /// <summary>`幼年夭折` 年龄门槛；严格 `<`。</summary>
        public static int InfantDeathMaxAge => AchievementPolicy.InfantDeathMaxAge;

        /// <summary>平均寿命保留小数位。</summary>
        public static int AverageLifespanDecimals => AchievementPolicy.AverageLifespanDecimals;
    }
}
