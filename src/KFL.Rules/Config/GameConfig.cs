using KFL.Core.Enums;
using KFL.Core.ValueObjects;

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
/// **明确不含**：阶段⑤及以后才有的数值（科举录取率、贿赂与惩罚矩阵、买人口递增计价、考课与政绩等，
/// 契约五 §6）。<c>DifficultyRates</c> 里已存在的 <c>BriberyRiskFactor</c> / <c>NegativeEventFactor</c>
/// 属阶段⑤/⑥口径，故不在此处转发。
/// </para>
/// </remarks>
public static class GameConfig
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

    /// <summary>收入表（§5.2、§5.3、§8.1、§11；R-18/R-19）：自耕、田租、务农、做工、经商、天赋除数、工 bonus。</summary>
    public static class Income
    {
        /// <summary>一年月数（年额 → 月额的除数，亦为年度项节拍）。</summary>
        public static decimal MonthsPerYear => IncomeRateTable.MonthsPerYear;

        /// <summary>自耕亩产率（贯/亩/年）。</summary>
        public static decimal SelfFarmingGuanPerMuPerYear => IncomeRateTable.SelfFarmingGuanPerMuPerYear;

        /// <summary>每人自耕上限（亩）。</summary>
        public static int FarmlandPerCapitaMu => IncomeRateTable.FarmlandPerCapitaMu;

        /// <summary>田租亩产率（贯/亩/年）。</summary>
        public static decimal LandRentGuanPerMuPerYear => IncomeRateTable.LandRentGuanPerMuPerYear;

        /// <summary>务农月钱（贯/月）。</summary>
        public static decimal FarmingWageGuanPerMonth => IncomeRateTable.FarmingWageGuanPerMonth;

        /// <summary>做工月钱（贯/月）。</summary>
        public static decimal CraftingGuanPerMonth => IncomeRateTable.CraftingGuanPerMonth;

        /// <summary>城市宅做工加成（贯/月/份）。</summary>
        public static decimal UrbanHouseCraftingBonusGuanPerMonth =>
            IncomeRateTable.UrbanHouseCraftingBonusGuanPerMonth;

        /// <summary>经商利润率。</summary>
        public static decimal TradeProfitRate => IncomeRateTable.TradeProfitRate;

        /// <summary>经商本金门槛（贯）。</summary>
        public static decimal TradeCapitalThresholdGuan => IncomeRateTable.TradeCapitalThresholdGuan;

        /// <summary>商出身经商加成。</summary>
        public static decimal MerchantOriginMultiplier => IncomeRateTable.MerchantOriginMultiplier;

        /// <summary>工出身 bonus 比率。</summary>
        public static decimal ArtisanBonusRate => IncomeRateTable.ArtisanBonusRate;

        /// <summary>农天赋除数。</summary>
        public static decimal AgricultureTalentDivisor => IncomeRateTable.AgricultureTalentDivisor;

        /// <summary>工天赋除数。</summary>
        public static decimal CraftTalentDivisor => IncomeRateTable.CraftTalentDivisor;

        /// <summary>商天赋除数。</summary>
        public static decimal CommerceTalentDivisor => IncomeRateTable.CommerceTalentDivisor;

        /// <summary>农天赋乘数。</summary>
        public static decimal AgricultureFactor(TalentSet talents) => IncomeRateTable.AgricultureFactor(talents);

        /// <summary>工天赋乘数。</summary>
        public static decimal CraftFactor(TalentSet talents) => IncomeRateTable.CraftFactor(talents);

        /// <summary>商天赋乘数。</summary>
        public static decimal CommerceFactor(TalentSet talents) => IncomeRateTable.CommerceFactor(talents);

        /// <summary>自耕效率 = 天赋乘数 × 出身乘数。</summary>
        public static decimal FarmingEfficiency(TalentSet talents) => IncomeRateTable.FarmingEfficiency(talents);
    }

    /// <summary>资产价目（§5.3）：田、农村宅、城市宅、铺面（购售同价）与铺面年租率。</summary>
    public static class Assets
    {
        /// <summary>田价（贯/亩）。</summary>
        public static decimal FarmlandGuanPerMu => AssetPriceTable.FarmlandGuanPerMu;

        /// <summary>农村宅价（贯）。</summary>
        public static decimal RuralHouseGuan => AssetPriceTable.RuralHouseGuan;

        /// <summary>城市宅价（贯）。</summary>
        public static decimal UrbanHouseGuan => AssetPriceTable.UrbanHouseGuan;

        /// <summary>铺面价（贯）。</summary>
        public static decimal ShopGuan => AssetPriceTable.ShopGuan;

        /// <summary>铺面年租率。</summary>
        public static decimal ShopRentRate => AssetPriceTable.ShopRentRate;

        /// <summary>取单价（购售同价）。</summary>
        public static Money UnitPrice(AssetKind kind) => AssetPriceTable.UnitPrice(kind);

        /// <summary>铺面月租 = 单价 × 年租率 ÷ 月数。</summary>
        public static Money ShopMonthlyRent(int shops) => AssetPriceTable.ShopMonthlyRent(shops);
    }

    /// <summary>储蓄与贷款利率表（§5.4；R-08/R-11）：区间与计息周期。</summary>
    public static class Interest
    {
        /// <summary>储蓄利率区间（1 月 roll，当年不变）。</summary>
        public static (decimal Min, decimal Max) SavingsRate => InterestPolicy.SavingsRate;

        /// <summary>贷款利率区间（每满计息周期按当时本金 roll）。</summary>
        public static (decimal Min, decimal Max) LoanRate => InterestPolicy.LoanRate;

        /// <summary>贷款计息周期（自然月）。</summary>
        public static int InterestPeriodMonths => InterestPolicy.InterestPeriodMonths;

        /// <summary>把 <c>[0, 1]</c> 的随机取值映射为区间内的利率。</summary>
        public static decimal RateFor(double r) => InterestPolicy.RateFor(r);
    }

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

    /// <summary>俸禄表（§8.1）：18 级年俸、月摊与士出身加成。</summary>
    public static class Salary
    {
        /// <summary>最高品级。</summary>
        public static int HighestLevel => SalaryTable.HighestLevel;

        /// <summary>最低品级。</summary>
        public static int LowestLevel => SalaryTable.LowestLevel;

        /// <summary>士出身当官加成。</summary>
        public static decimal ScholarOriginMultiplier => SalaryTable.ScholarOriginMultiplier;

        /// <summary>该品级年俸（贯）。</summary>
        public static decimal AnnualSalaryGuan(int level) => SalaryTable.AnnualSalaryGuan(level);

        /// <summary>该品级月摊俸禄（贯）。</summary>
        public static decimal MonthlySalaryGuan(int level) => SalaryTable.MonthlySalaryGuan(level);

        /// <summary>该品级年俸（金额）。</summary>
        public static Money AnnualSalary(int level) => SalaryTable.AnnualSalary(level);
    }

    /// <summary>贷款划扣比例（§5.4）：仕 / 工农 / 商。</summary>
    public static class Loan
    {
        /// <summary>仕出身划扣比例。</summary>
        public static decimal ShiRepaymentRatio => LoanPolicy.ShiRepaymentRatio;

        /// <summary>工农出身划扣比例。</summary>
        public static decimal CommonerRepaymentRatio => LoanPolicy.CommonerRepaymentRatio;

        /// <summary>商出身划扣比例。</summary>
        public static decimal MerchantRepaymentRatio => LoanPolicy.MerchantRepaymentRatio;

        /// <summary>按身份与出身取划扣比例。</summary>
        public static decimal RepaymentRatio(bool hasShiStatus, Origin origin) =>
            LoanPolicy.RepaymentRatio(hasShiStatus, origin);
    }
}
