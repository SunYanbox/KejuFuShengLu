using KFL.Core.Config;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Economy;

namespace KFL.Rules.Config;

/// <summary>
/// 数值总表入口（data-model §4.1）：以**只读视图**聚合本阶段各张配置表，供核对与后续阶段的单点引用。
/// </summary>
/// <remarks>
/// <para>
/// 本文件**只有转发，没有数值**（SC-008）：所有常量与公式仍唯一住在各子表里，这里逐成员转发出去，
/// 因此「同一个数不出第二个出处」仍然成立。任何把 <c>0.5m</c>、<c>12</c> 之类的字面量写进本文件的
/// 改动都会立刻被 SC-008 的扫描测试拦下。
/// </para>
/// <para>
/// 子表分组名与类型名**刻意不同名**（<see cref="Ages"/>、<see cref="DifficultyRate"/>）：避免嵌套类
/// 遮蔽 <c>KFL.Core.Enums</c> 里的 <c>AgeBracket</c> / <c>Difficulty</c> 同名类型。
/// </para>
/// <para>
/// **明确不含**：尚未落地的阶段的数值（成就与绝嗣阈值等）。买人口递增计价、成长衰老疾病死亡的
/// 年龄线与概率**已由 ⑦ 交付**，见 <see cref="Growth"/>、<see cref="Illness"/>、
/// <see cref="Marriage"/>、<see cref="Childbirth"/>、<see cref="Purchase"/> 与
/// <see cref="Lifecycle"/>。提亲与捉婿的难度乘数经 <c>DifficultyRates.NegativeEventFactor</c>
/// 读取（患病概率 MUST NOT 用它），饥馑时间线仍留在 <see cref="Famine"/>。
/// </para>
/// </remarks>
public static partial class GameConfig
{
    /// <summary>生活费表（§5.1、§5.4、§10.1；R-17）：日耗、年龄档边界乘区、农出身与一般乘区。</summary>
    public static class LivingCost
    {
        /// <summary>月折算天数：<c>月 = 日耗 × 本值</c>。</summary>
        public static int DaysPerMonth => LivingCostTable.DaysPerMonth;

        /// <summary>开局档位（T005 裁决：普通）。</summary>
        public static LivingStandard InitialStandard => LivingCostTable.InitialStandard;

        /// <summary>儿童日耗（不随档位变化）。</summary>
        public static decimal ChildDailyCost => LivingCostTable.ChildDailyCost;

        /// <summary>老人日耗 = 成人日耗 × 本比例。</summary>
        public static decimal ElderRatioOfAdult => LivingCostTable.ElderRatioOfAdult;

        /// <summary>农出身独立乘区：成年。</summary>
        public static decimal FarmerMultiplierAdult => LivingCostTable.FarmerMultiplierAdult;

        /// <summary>农出身独立乘区：未成年。</summary>
        public static decimal FarmerMultiplierMinor => LivingCostTable.FarmerMultiplierMinor;

        /// <summary>非农出身独立乘区。</summary>
        public static decimal NonFarmerMultiplier => LivingCostTable.NonFarmerMultiplier;

        /// <summary>生活费一般乘区的加算修正项（未成年、救济期）。</summary>
        public static IReadOnlyList<GeneralZoneModifier> GeneralZoneModifiers => LivingCostTable.GeneralZoneModifiers;

        /// <summary>三档 × 四年龄档的日耗。</summary>
        public static decimal DailyCost(LivingStandard standard, AgeBracket bracket) =>
            LivingCostTable.DailyCost(standard, bracket);

        /// <summary>出身 → 独立乘区。</summary>
        public static decimal FarmerMultiplierOf(Origin origin, AgeBracket bracket) =>
            LivingCostTable.FarmerMultiplierOf(origin, bracket);

        /// <summary>一般乘区因子：<c>1 + Σ修正项</c>。</summary>
        public static decimal GeneralZoneFactor(bool isMinor, bool inRelief) =>
            LivingCostTable.GeneralZoneFactor(isMinor, inRelief);
    }

    /// <summary>年龄档政策（§5.1）：成年边界、青年上界、老人下界。</summary>
    public static class Ages
    {
        /// <summary>男性成年边界。</summary>
        public static int MaleAdulthoodAge => AgeBracketPolicy.MaleAdulthoodAge;

        /// <summary>女性成年边界。</summary>
        public static int FemaleAdulthoodAge => AgeBracketPolicy.FemaleAdulthoodAge;

        /// <summary>青年上界。</summary>
        public static int YouthUpperAge => AgeBracketPolicy.YouthUpperAge;

        /// <summary>老人下界。</summary>
        public static int ElderLowerAge => AgeBracketPolicy.ElderLowerAge;

        /// <summary>是否成年。</summary>
        public static bool IsAdult(Gender gender, int age) => AgeBracketPolicy.IsAdult(gender, age);

        /// <summary>按性别与年龄取年龄档。</summary>
        public static AgeBracket Of(Gender gender, int age) => AgeBracketPolicy.Of(gender, age);
    }
}
