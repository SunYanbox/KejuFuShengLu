using KFL.Core.Entities;
using KFL.Core.Enums;

namespace KFL.Rules.Config;

public static partial class GameConfig
{
    /// <summary>饥馑时间线（§5.4；E-14）：转入救济与恶化的月数、救济期折扣。</summary>
    public static class Famine
    {
        /// <summary>饥馑 → 救济所需月数。</summary>
        public static int MonthsUntilRelief => FamineTimeline.MonthsUntilRelief;

        /// <summary>救济 → 严重所需月数。</summary>
        public static int ReliefMonthsUntilSevere => FamineTimeline.ReliefMonthsUntilSevere;

        /// <summary>救济期生活费折扣。</summary>
        public static decimal ReliefExpenseDiscount => FamineTimeline.ReliefExpenseDiscount;

        /// <summary>该阶段的可持续月数（<c>null</c> = 无上限）。</summary>
        public static int? LimitMonths(FamineStage stage) => FamineTimeline.LimitMonths(stage);

        /// <summary>本阶段的剩余月数（不为负；无时限阶段为 0）。</summary>
        public static int RemainingMonths(FamineState state) => FamineTimeline.RemainingMonths(state);
    }
    /// <summary>米价政策（§5.1）：初始系数、游走幅度、clamp 区间与派生值系数。</summary>
    public static class GrainPrice
    {
        /// <summary>初始米价系数。</summary>
        public static decimal Initial => GrainPricePolicy.Initial;

        /// <summary>clamp 下限。</summary>
        public static decimal Min => GrainPricePolicy.Min;

        /// <summary>clamp 上限。</summary>
        public static decimal Max => GrainPricePolicy.Max;

        /// <summary>游走幅度。</summary>
        public static decimal WalkAmplitude => GrainPricePolicy.WalkAmplitude;

        /// <summary>派生值分子偏移。</summary>
        public static decimal PriceOffset => GrainPricePolicy.PriceOffset;

        /// <summary>派生值分母。</summary>
        public static decimal PriceScale => GrainPricePolicy.PriceScale;

        /// <summary>夹到合法区间。</summary>
        public static decimal Clamp(decimal value) => GrainPricePolicy.Clamp(value);

        /// <summary>按随机取值游走一步。</summary>
        public static decimal Walk(decimal current, double r) => GrainPricePolicy.Walk(current, r);

        /// <summary>系数 → 米价派生值。</summary>
        public static decimal MarketPrice(decimal index) => GrainPricePolicy.MarketPrice(index);
    }
    /// <summary>难度系数（§5.1、§11）：支出系数与收益系数。</summary>
    public static class DifficultyRate
    {
        /// <summary>收益系数。</summary>
        public static decimal RevenueFactor(Difficulty difficulty) => DifficultyRates.RevenueFactor(difficulty);

        /// <summary>支出系数。</summary>
        public static decimal ExpenseFactor(Difficulty difficulty) => DifficultyRates.ExpenseFactor(difficulty);
    }
}
