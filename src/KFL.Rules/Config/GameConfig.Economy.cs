using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Economy;

namespace KFL.Rules.Config;

public static partial class GameConfig
{
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

        /// <summary>田宅铺市值（贯；**不含商本池**）。</summary>
        public static Money MarketValue(Holdings holdings) => AssetPriceTable.MarketValue(holdings);

        /// <summary>总资产（现金 + 储蓄 + 田宅铺市值 − 贷款本金 − 欠息；为负或 0 ⇒ 0）。</summary>
        public static Money TotalAssets(FamilyEconomy economy) => TotalAssetsCalculator.Of(economy);

        /// <summary>总资产（同上，按「国库 + 资产」两段取）。</summary>
        public static Money TotalAssets(Treasury treasury, Holdings holdings) =>
            TotalAssetsCalculator.Of(treasury, holdings);
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
