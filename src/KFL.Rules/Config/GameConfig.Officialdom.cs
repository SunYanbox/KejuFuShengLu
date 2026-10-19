using KFL.Core.Config;
using KFL.Core.Enums;
using KFL.Infrastructure.Abstractions;

namespace KFL.Rules.Config;

public static partial class GameConfig
{
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
}
