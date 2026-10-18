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

    /// <summary>四出身开局表（§10.1、§5.3；003）：初始资产、成员构成、年龄与属性口径（只转发）。</summary>
    public static class NewGame
    {
        /// <summary>配偶人数。</summary>
        public static int SpouseCount => OriginStartTable.SpouseCount;

        /// <summary>孩子人数。</summary>
        public static int ChildCount(Origin origin) => OriginStartTable.ChildCount(origin);

        /// <summary>初始现金（贯）。</summary>
        public static decimal InitialCashGuan(Origin origin) => OriginStartTable.InitialCashGuan(origin);

        /// <summary>初始田（亩）。</summary>
        public static int InitialFarmlandMu(Origin origin) => OriginStartTable.InitialFarmlandMu(origin);

        /// <summary>初始农村宅（座）。</summary>
        public static int InitialRuralHouses(Origin origin) => OriginStartTable.InitialRuralHouses(origin);

        /// <summary>初始城市宅（座）。</summary>
        public static int InitialUrbanHouses(Origin origin) => OriginStartTable.InitialUrbanHouses(origin);

        /// <summary>初始商本（贯，入商本池）。</summary>
        public static decimal InitialMerchantCapitalGuan(Origin origin) =>
            OriginStartTable.InitialMerchantCapitalGuan(origin);

        /// <summary>家主年龄（整数均匀、含端点）。</summary>
        public static int HeadAge(Origin origin, IRandomService random) =>
            OriginStartTable.NextHeadAge(origin, random);

        /// <summary>配偶年龄（整数均匀、含端点）。</summary>
        public static int SpouseAge(IRandomService random) => OriginStartTable.NextSpouseAge(random);

        /// <summary>孩子年龄（整数均匀、含端点）。</summary>
        public static int ChildAge(IRandomService random) => OriginStartTable.NextChildAge(random);

        /// <summary>家主学业（士为常量、不掷骰）。</summary>
        public static int HeadStudy(Origin origin, IRandomService random) =>
            OriginStartTable.NextHeadStudy(origin, random);

        /// <summary>家主体质（整数均匀、含端点）。</summary>
        public static int HeadHealth(IRandomService random) => OriginStartTable.NextHeadHealth(random);

        /// <summary>孩子学业（常量 0、不掷骰）。</summary>
        public static int ChildStudy => OriginStartTable.ChildStudyValue;

        /// <summary>孩子体质（整数均匀、含端点）。</summary>
        public static int ChildHealth(IRandomService random) => OriginStartTable.NextChildHealth(random);

        /// <summary>该出身是否带入一条「举人 / Initial」功名记录。</summary>
        public static bool ScholarOriginHasJuRenRecord(Origin origin) =>
            OriginStartTable.ScholarOriginHasJuRenRecord(origin);
    }

    /// <summary>属性分布（§4.1、§4.2；003）：四项属性与天命寿数的分布参数、值域与取样（只转发）。</summary>
    public static class Attributes
    {
        /// <summary>天赋正态均值。</summary>
        public static decimal TalentMean => AttributePolicy.TalentMean;

        /// <summary>天赋正态标准差。</summary>
        public static decimal TalentSigma => AttributePolicy.TalentSigma;

        /// <summary>学业正态均值（无父母参照者）。</summary>
        public static decimal StudyMean => AttributePolicy.StudyMean;

        /// <summary>学业正态标准差。</summary>
        public static decimal StudySigma => AttributePolicy.StudySigma;

        /// <summary>体质正态均值（无父母参照者）。</summary>
        public static decimal HealthMean => AttributePolicy.HealthMean;

        /// <summary>体质正态标准差。</summary>
        public static decimal HealthSigma => AttributePolicy.HealthSigma;

        /// <summary>属性值域下界（实体自不变量）。</summary>
        public static int Min => AttributeLimits.Min;

        /// <summary>属性值域上界（实体自不变量）。</summary>
        public static int Max => AttributeLimits.Max;

        /// <summary>天命寿数均值。</summary>
        public static decimal LifespanMean(Gender gender) =>
            gender == Gender.Male ? AttributePolicy.LifespanMeanMale : AttributePolicy.LifespanMeanFemale;

        /// <summary>天命寿数标准差。</summary>
        public static decimal LifespanSigma(Gender gender) =>
            gender == Gender.Male ? AttributePolicy.LifespanSigmaMale : AttributePolicy.LifespanSigmaFemale;

        /// <summary>标准正态取样（Box–Muller，恰好消耗 2 次 <c>NextDouble</c>）。</summary>
        public static double NextNormal(decimal mean, decimal sigma, IRandomService random) =>
            AttributePolicy.NextNormal((double)mean, (double)sigma, random);

        /// <summary>天赋取样（取整 + clamp 0~100）。</summary>
        public static int NextTalent(IRandomService random) => AttributePolicy.NextTalent(random);

        /// <summary>学业取样（取整 + clamp 0~100）。</summary>
        public static int NextStudy(IRandomService random) => AttributePolicy.NextStudy(random);

        /// <summary>体质取样（取整 + clamp 0~100）。</summary>
        public static int NextHealth(IRandomService random) => AttributePolicy.NextHealth(random);

        /// <summary>天命寿数取样（只 clamp 下界，无上限）。</summary>
        public static int NextLifespan(Gender gender, IRandomService random) =>
            AttributePolicy.NextLifespan(gender, random);
    }

    /// <summary>科举政策（§6、§7.1、§7.2；005）：报考资格、三级录取、免解、特奏名与省试路费（只转发）。</summary>
    public static class Exam
    {
        /// <summary>报考年龄下限（周岁）。</summary>
        public static int MinimumAge => ExamPolicy.MinimumAge;

        /// <summary>解试基础录取率。</summary>
        public static decimal JieShiBaseRate => ExamPolicy.JieShiBaseRate;

        /// <summary>省试基础录取率。</summary>
        public static decimal ShengShiBaseRate => ExamPolicy.ShengShiBaseRate;

        /// <summary>殿试基础进甲率。</summary>
        public static decimal ImperialPromotionBaseRate => ExamPolicy.ImperialPromotionBaseRate;

        /// <summary>学业系数基数。</summary>
        public static decimal StudyFactorBase => ExamPolicy.StudyFactorBase;

        /// <summary>学业系数每点增量。</summary>
        public static decimal StudyFactorPerPoint => ExamPolicy.StudyFactorPerPoint;

        /// <summary>仕天赋除数。</summary>
        public static int OfficialdomDivisor => ExamPolicy.OfficialdomDivisor;

        /// <summary>仕族录取率加成。</summary>
        public static decimal ShiStatusAdmissionBonus => ExamPolicy.ShiStatusAdmissionBonus;

        /// <summary>未进甲者的二甲学业线。</summary>
        public static int SecondClassStudyThreshold => ExamPolicy.SecondClassStudyThreshold;

        /// <summary>需掷骰概率的封顶。</summary>
        public static decimal DiceRateCap => ExamPolicy.DiceRateCap;

        /// <summary>免解的解试累计失败数阈值。</summary>
        public static int JieShiExemptionFailureThreshold => ExamPolicy.JieShiExemptionFailureThreshold;

        /// <summary>特奏名年龄下限。</summary>
        public static int SpecialTributeMinimumAge => ExamPolicy.SpecialTributeMinimumAge;

        /// <summary>特奏名的省试累计失败数阈值。</summary>
        public static int SpecialTributeFailureThreshold => ExamPolicy.SpecialTributeFailureThreshold;

        /// <summary>省试路费（贯）。</summary>
        public static int ExamTravelCostGuan(Difficulty difficulty) => ExamPolicy.ExamTravelCostGuan(difficulty);

        /// <summary>解试 / 省试的基础录取率。</summary>
        public static decimal BaseRateOf(ExamLevel level) => ExamPolicy.BaseRateOf(level);

        /// <summary>学业系数。</summary>
        public static decimal StudyFactor(int study) => ExamPolicy.StudyFactor(study);
    }

    /// <summary>贿赂政策（§7.1；006）：窗口、递增步长、基础金额、基础被抓率与效果乘数 k（只转发）。</summary>
    public static class Bribe
    {
        /// <summary>贿赂窗口覆盖的考前月数。</summary>
        public static int WindowPreviousMonths => BribePolicy.WindowPreviousMonths;

        /// <summary>每次行贿的递进步长（金额与被抓率的共用步长）。</summary>
        public static decimal ProgressionStep => BribePolicy.ProgressionStep;

        /// <summary>被抓概率封顶。</summary>
        public static decimal CaughtProbabilityCap => BribePolicy.CaughtProbabilityCap;

        /// <summary>k⁰ 的单位乘数。</summary>
        public static decimal UnitMultiplier => BribePolicy.UnitMultiplier;

        /// <summary>基础贿赂金额（贯）。</summary>
        public static int BaseAmountGuan(ExamLevel level) => BribePolicy.BaseAmountGuan(level);

        /// <summary>基础被抓率。</summary>
        public static decimal BaseCaughtRate(ExamLevel level) => BribePolicy.BaseCaughtRate(level);

        /// <summary>效果乘数底数 k。</summary>
        public static decimal EffectMultiplierK(ExamLevel level) => BribePolicy.EffectMultiplierK(level);
    }

    /// <summary>惩罚政策（§7.4；006）：12 格矩阵与罚金 / 亲属禁考 / 全族禁考口径（只转发）。</summary>
    public static class Punishment
    {
        /// <summary>比例罚金的上限（贯）。</summary>
        public static int RatioFineCapGuan => PunishmentPolicy.RatioFineCapGuan;

        /// <summary>全族禁考禁升的月数。</summary>
        public static int FamilyWideBanMonths => PunishmentPolicy.FamilyWideBanMonths;

        /// <summary>释放时官职提升的级数。</summary>
        public static int ReleaseDemotionLevels => PunishmentPolicy.ReleaseDemotionLevels;

        /// <summary>一年的月数（复用收入表）。</summary>
        public static int MonthsPerYear => PunishmentPolicy.MonthsPerYear;

        /// <summary>取难度 × 被抓级别的处罚格。</summary>
        public static PunishmentCell CellOf(Difficulty difficulty, ExamLevel level) =>
            PunishmentPolicy.CellOf(difficulty, level);
    }

    /// <summary>官吏政策（§8.2；003）：待阙、授官、政绩、考课、致仕与半俸（只转发）。</summary>
    public static class Career
    {
        /// <summary>待阙剩余月数下界（含）。</summary>
        public static int AwaitingPostMinMonths => OfficialCareerPolicy.AwaitingPostMinMonths;

        /// <summary>待阙剩余月数上界（含）。</summary>
        public static int AwaitingPostMaxMonths => OfficialCareerPolicy.AwaitingPostMaxMonths;

        /// <summary>政绩月增。</summary>
        public static int MeritPerMonth => OfficialCareerPolicy.MeritPerMonth;

        /// <summary>政绩上限。</summary>
        public static int MeritMaximum => OfficialCareerPolicy.MeritMaximum;

        /// <summary>考课周期（在职月数）。</summary>
        public static int AppraisalPeriodMonths => OfficialCareerPolicy.AppraisalPeriodMonths;

        /// <summary>考课基础升级概率。</summary>
        public static decimal PromotionBaseChance => OfficialCareerPolicy.PromotionBaseChance;

        /// <summary>每点政绩的升级概率加成。</summary>
        public static decimal PromotionChancePerMerit => OfficialCareerPolicy.PromotionChancePerMerit;

        /// <summary>升级概率封顶。</summary>
        public static decimal PromotionChanceCap => OfficialCareerPolicy.PromotionChanceCap;

        /// <summary>致仕年龄。</summary>
        public static int RetirementAge => OfficialCareerPolicy.RetirementAge;

        /// <summary>致仕后的俸禄比例（半俸）。</summary>
        public static decimal RetirementSalaryRatio => OfficialCareerPolicy.RetirementSalaryRatio;

        /// <summary>入仕途径 → 初始官阶级数。</summary>
        public static int InitialRankOf(AppointmentTrack track) => OfficialCareerPolicy.InitialRankOf(track);

        /// <summary>考课升级概率。</summary>
        public static decimal PromotionChance(int merit) => OfficialCareerPolicy.PromotionChance(merit);

        /// <summary>掷一次待阙时长（整数均匀、含端点）。</summary>
        public static int NextAwaitingPostMonths(IRandomService random) =>
            OfficialCareerPolicy.NextAwaitingPostMonths(random);
    }

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

    /// <summary>成长与体质政策（§4.3、§5.4；007）：学业、名师、遗忘、恢复与饥馑健康影响（只转发）。</summary>
    public static class Growth
    {
        /// <summary>读书成员每月学业增量。</summary>
        public static decimal StudyPerMonth => GrowthPolicy.StudyPerMonth;

        /// <summary>延请名师后每月的额外学业增量。</summary>
        public static decimal TutorStudyBonusPerMonth => GrowthPolicy.TutorStudyBonusPerMonth;

        /// <summary>名师月费（贯；再乘支出系数）。</summary>
        public static int TutorFeeGuanPerMonth => GrowthPolicy.TutorFeeGuanPerMonth;

        /// <summary>士出身藏书加成（乘区）。</summary>
        public static decimal ScholarStudyMultiplier => GrowthPolicy.ScholarStudyMultiplier;

        /// <summary>遗忘月扣减（正值，写入时取负）。</summary>
        public static decimal ForgetPerMonth => GrowthPolicy.ForgetPerMonth;

        /// <summary>遗忘的年龄门槛（严格大于）。</summary>
        public static int ForgetAgeThreshold => GrowthPolicy.ForgetAgeThreshold;

        /// <summary>患病期学业变化的除数（减半）。</summary>
        public static decimal IllnessStudyPenaltyDivisor => GrowthPolicy.IllnessStudyPenaltyDivisor;

        /// <summary>体质每月自然恢复量。</summary>
        public static int HealthRecoverPerMonth => GrowthPolicy.HealthRecoverPerMonth;

        /// <summary>饥馑严重期每月体质扣减量（正值）。</summary>
        public static int FamineHealthLossPerMonth => GrowthPolicy.FamineHealthLossPerMonth;

        /// <summary>饥馑严重期每人每月的死亡判定概率。</summary>
        public static decimal FamineDeathChance => GrowthPolicy.FamineDeathChance;
    }

    /// <summary>疾病政策（§4.3；007）：感染与清除概率、每月扣减与药费、疗程（只转发）。</summary>
    public static class Illness
    {
        /// <summary>体质满值（100）时的月患病率。</summary>
        public static decimal InfectionChanceAtFullHealth => IllnessPolicy.InfectionChanceAtFullHealth;

        /// <summary>体质下限（1）时的月患病率。</summary>
        public static decimal InfectionChanceAtFloorHealth => IllnessPolicy.InfectionChanceAtFloorHealth;

        /// <summary>插值分母。</summary>
        public static int InterpolationSpan => IllnessPolicy.InterpolationSpan;

        /// <summary>概率入参的体质下界。</summary>
        public static int HealthFloor => IllnessPolicy.HealthFloor;

        /// <summary>概率入参的体质上界。</summary>
        public static int HealthCeiling => IllnessPolicy.HealthCeiling;

        /// <summary>农出身主导天赋乘数：感染。</summary>
        public static decimal TalentMultiplierFarmer => IllnessPolicy.TalentMultiplierFarmer;

        /// <summary>农出身主导天赋乘数：清除。</summary>
        public static decimal FarmerRecoveryMultiplier => IllnessPolicy.FarmerRecoveryMultiplier;

        /// <summary>仕出身主导天赋乘数：感染。</summary>
        public static decimal TalentMultiplierScholar => IllnessPolicy.TalentMultiplierScholar;

        /// <summary>仕出身主导天赋乘数：清除。</summary>
        public static decimal ScholarRecoveryMultiplier => IllnessPolicy.ScholarRecoveryMultiplier;

        /// <summary>工出身主导天赋乘数：感染。</summary>
        public static decimal TalentMultiplierArtisan => IllnessPolicy.TalentMultiplierArtisan;

        /// <summary>工出身主导天赋乘数：清除。</summary>
        public static decimal ArtisanRecoveryMultiplier => IllnessPolicy.ArtisanRecoveryMultiplier;

        /// <summary>商出身主导天赋乘数：感染。</summary>
        public static decimal TalentMultiplierMerchant => IllnessPolicy.TalentMultiplierMerchant;

        /// <summary>商出身主导天赋乘数：清除。</summary>
        public static decimal MerchantRecoveryMultiplier => IllnessPolicy.MerchantRecoveryMultiplier;

        /// <summary>患病每月体质扣减的基数。</summary>
        public static int SeverityLossBase => IllnessPolicy.SeverityLossBase;

        /// <summary>患病每月体质扣减的程度系数。</summary>
        public static int SeverityLossSpan => IllnessPolicy.SeverityLossSpan;

        /// <summary>药费基数（贯）。</summary>
        public static decimal MedicineBaseGuan => IllnessPolicy.MedicineBaseGuan;

        /// <summary>药费的程度系数（贯）。</summary>
        public static decimal MedicineSeverityGuan => IllnessPolicy.MedicineSeverityGuan;

        /// <summary>未治疗时的清除系数。</summary>
        public static decimal BaseRecoveryCoefficient => IllnessPolicy.BaseRecoveryCoefficient;

        /// <summary>疗程中的清除系数。</summary>
        public static decimal TreatedRecoveryCoefficient => IllnessPolicy.TreatedRecoveryCoefficient;

        /// <summary>疗程中的清除加项。</summary>
        public static decimal TreatedRecoveryOffset => IllnessPolicy.TreatedRecoveryOffset;

        /// <summary>疗程月数。</summary>
        public static int TreatmentMonths => IllnessPolicy.TreatmentMonths;

        /// <summary>儿童段的清除增益。</summary>
        public static decimal ClearBonusChild => IllnessPolicy.ClearBonusChild;

        /// <summary>青年段的清除增益。</summary>
        public static decimal ClearBonusYouth => IllnessPolicy.ClearBonusYouth;

        /// <summary>儿童段下界（含）。</summary>
        public static int ClearBonusBoundaryChild => IllnessPolicy.ClearBonusBoundaryChild;

        /// <summary>青年段下界（含）。</summary>
        public static int Youth => IllnessPolicy.Youth;

        /// <summary>成人段下界（含）。</summary>
        public static int Adult => IllnessPolicy.Adult;

        /// <summary>免病年龄。</summary>
        public static int IllnessExemptAge => IllnessPolicy.IllnessExemptAge;

        /// <summary>疾病程度与存档字段的换算单点。</summary>
        public static int SeverityBasisPointsScale => IllnessPolicy.SeverityBasisPointsScale;

        /// <summary>把疾病程度换算成万分之整数。</summary>
        public static int ToBasisPoints(double severity) => IllnessPolicy.ToBasisPoints(severity);

        /// <summary>把万分之整数换算回疾病程度。</summary>
        public static double ToSeverity(int basisPoints) => IllnessPolicy.ToSeverity(basisPoints);
    }

    /// <summary>婚配政策（§9.1~§9.3；007）：托媒、提亲、捉婿、阶层权重与聘礼 / 嫁妆（只转发）。</summary>
    public static class Marriage
    {
        /// <summary>托媒费（贯；再乘支出系数）。</summary>
        public static int MatchmakerFeeGuan => MarriagePolicy.MatchmakerFeeGuan;

        /// <summary>托媒成功概率。</summary>
        public static decimal MatchmakerSuccessChance => MarriagePolicy.MatchmakerSuccessChance;

        /// <summary>候选者与对象的年龄差上限。</summary>
        public static int CandidateAgeSpread => MarriagePolicy.CandidateAgeSpread;

        /// <summary>随机提亲的基础概率。</summary>
        public static decimal ProposalBaseChance => MarriagePolicy.ProposalBaseChance;

        /// <summary>榜下捉婿的基础概率。</summary>
        public static decimal ImperialProposalBaseChance => MarriagePolicy.ImperialProposalBaseChance;

        /// <summary>捉婿支付区间下界（贯，含）。</summary>
        public static int ImperialPaymentMinGuan => MarriagePolicy.ImperialPaymentMinGuan;

        /// <summary>捉婿支付区间上界（贯，含）。</summary>
        public static int ImperialPaymentMaxGuan => MarriagePolicy.ImperialPaymentMaxGuan;

        /// <summary>官员压力权重：最低官阶。</summary>
        public static decimal PressureWeightBottom => MarriagePolicy.PressureWeightBottom;

        /// <summary>官员压力权重：最高官阶。</summary>
        public static decimal PressureWeightTop => MarriagePolicy.PressureWeightTop;

        /// <summary>最高官阶级数。</summary>
        public static int PressureHighestLevel => MarriagePolicy.PressureHighestLevel;

        /// <summary>最低官阶级数。</summary>
        public static int PressureLowestLevel => MarriagePolicy.PressureLowestLevel;

        /// <summary>功名分档：举人阈值。</summary>
        public static decimal SpouseDegreeScholarThreshold => MarriagePolicy.SpouseDegreeScholarThreshold;

        /// <summary>功名分档：贡士阈值。</summary>
        public static decimal SpouseDegreeTributeThreshold => MarriagePolicy.SpouseDegreeTributeThreshold;

        /// <summary>买功名婚姻的费率。</summary>
        public static decimal DegreeMarriageFeeRatio => MarriagePolicy.DegreeMarriageFeeRatio;

        /// <summary>降级为贡士后的配偶学业重掷均值。</summary>
        public static int TributeSpouseStudyMean => MarriagePolicy.TributeSpouseStudyMean;

        /// <summary>降级为贡士后的配偶学业重掷标准差。</summary>
        public static int TributeSpouseStudySigma => MarriagePolicy.TributeSpouseStudySigma;

        /// <summary>降级为举人后的配偶学业重掷均值。</summary>
        public static int JuRenSpouseStudyMean => MarriagePolicy.JuRenSpouseStudyMean;

        /// <summary>降级为举人后的配偶学业重掷标准差。</summary>
        public static int JuRenSpouseStudySigma => MarriagePolicy.JuRenSpouseStudySigma;

        /// <summary>平民档聘礼（贯）。</summary>
        public static int CommonerGiftGuan => MarriagePolicy.CommonerGiftGuan;

        /// <summary>平民档嫁妆（贯）。</summary>
        public static int CommonerDowryGuan => MarriagePolicy.CommonerDowryGuan;

        /// <summary>士人档聘礼（贯）。</summary>
        public static int ScholarGiftGuan => MarriagePolicy.ScholarGiftGuan;

        /// <summary>士人档嫁妆（贯）。</summary>
        public static int ScholarDowryGuan => MarriagePolicy.ScholarDowryGuan;

        /// <summary>仕族档聘礼（贯）。</summary>
        public static int OfficialGiftGuan => MarriagePolicy.OfficialGiftGuan;

        /// <summary>仕族档嫁妆（贯）。</summary>
        public static int OfficialDowryGuan => MarriagePolicy.OfficialDowryGuan;

        /// <summary>阶层加权分档：总资产下界。</summary>
        public static int ClassWealthThresholdLow => MarriagePolicy.ClassWealthThresholdLow;

        /// <summary>阶层加权分档：总资产上界。</summary>
        public static int ClassWealthThresholdHighGuan => MarriagePolicy.ClassWealthThresholdHighGuan;

        /// <summary>贫档阶层权重（平民 → 士人 → 仕族）。</summary>
        public static IReadOnlyList<decimal> ClassWeightsPoor => MarriagePolicy.ClassWeightsPoor;

        /// <summary>中档阶层权重（平民 → 士人 → 仕族）。</summary>
        public static IReadOnlyList<decimal> ClassWeightsMiddle => MarriagePolicy.ClassWeightsMiddle;

        /// <summary>富档阶层权重（平民 → 士人 → 仕族）。</summary>
        public static IReadOnlyList<decimal> ClassWeightsRich => MarriagePolicy.ClassWeightsRich;
    }

    /// <summary>生育政策（§9.4、§4.2；007）：申请费、生育窗口、分娩风险与遗传参数（只转发）。</summary>
    public static class Childbirth
    {
        /// <summary>生育申请费（贯；再乘支出系数）。</summary>
        public static int ChildbirthExpenseGuan => ChildbirthPolicy.ChildbirthExpenseGuan;

        /// <summary>女方生育窗口下界（含）。</summary>
        public static int FertilityAgeMin => ChildbirthPolicy.FertilityAgeMin;

        /// <summary>女方生育窗口上界（含）。</summary>
        public static int FertilityAgeMax => ChildbirthPolicy.FertilityAgeMax;

        /// <summary>分娩死亡概率。</summary>
        public static decimal ChildbirthDeathChance => ChildbirthPolicy.ChildbirthDeathChance;

        /// <summary>遗传标准差基数。</summary>
        public static int InheritanceSigmaBase => ChildbirthPolicy.InheritanceSigmaBase;

        /// <summary>遗传学业均值的折半系数。</summary>
        public static decimal StudyMeanHalving => ChildbirthPolicy.StudyMeanHalving;

        /// <summary>遗传体质的下限。</summary>
        public static int InheritedHealthFloor => ChildbirthPolicy.InheritedHealthFloor;

        /// <summary>单次夭折判定概率。</summary>
        public static decimal InfantDeathChance => ChildbirthPolicy.InfantDeathChance;

        /// <summary>夭折判定次数。</summary>
        public static int InfantCheckCount => ChildbirthPolicy.InfantCheckCount;

        /// <summary>夭折判定的年龄豁免。</summary>
        public static int InfantExemptAge => ChildbirthPolicy.InfantExemptAge;
    }

    /// <summary>买人口政策（§9.5；007）：递增计价与买来者年龄区间（只转发）。</summary>
    public static class Purchase
    {
        /// <summary>递增计价基数（贯）。</summary>
        public static int BasePriceGuan => PopulationPurchasePolicy.BasePriceGuan;

        /// <summary>买来者的年龄下界（含）。</summary>
        public static int AgeMin => PopulationPurchasePolicy.AgeMin;

        /// <summary>买来者的年龄上界（含）。</summary>
        public static int AgeMax => PopulationPurchasePolicy.AgeMax;
    }

    /// <summary>
    /// 人口推进的年龄门槛视图（§4.3、§9.4；007）：⑧-c 免病与 ⑧-f 免夭折的门槛（只转发）。
    /// </summary>
    /// <remarks>
    /// 本视图是 data-model §3.7 的第六视图：本特性**没有**与它同名的策略类，成员分别来自
    /// <c>IllnessPolicy</c> 与 <c>ChildbirthPolicy</c>（详见 Phase 9 的回写范围登记）。
    /// </remarks>
    public static class Lifecycle
    {
        /// <summary>免病年龄：<c>AgeAt &lt; 本值</c> 不掷感染判定且零随机消费。</summary>
        public static int IllnessExemptAge => IllnessPolicy.IllnessExemptAge;

        /// <summary>夭折判定的年龄豁免：<c>AgeAt &gt;= 本值</c> 不再判定。</summary>
        public static int InfantExemptAge => ChildbirthPolicy.InfantExemptAge;
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
