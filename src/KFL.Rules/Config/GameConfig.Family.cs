
namespace KFL.Rules.Config;

public static partial class GameConfig
{
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
    /// <summary>收养政策（§9.5；007）：递增计价与被收养者年龄区间（只转发）。</summary>
    public static class Purchase
    {
        /// <summary>递增计价基数（贯）。</summary>
        public static int BasePriceGuan => PopulationPurchasePolicy.BasePriceGuan;

        /// <summary>被收养者的年龄下界（含）。</summary>
        public static int AgeMin => PopulationPurchasePolicy.AgeMin;

        /// <summary>被收养者的年龄上界（含）。</summary>
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
}
