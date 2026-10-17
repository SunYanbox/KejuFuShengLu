using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;
using KFL.Rules.Start;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// 规格书数值的**锚点断言**（SC-002/SC-003/SC-004/SC-005/SC-007；契约八 §1/§2 的「断言锚点」列）：
/// 把规格书里的字面量直接钉在 <c>KFL.Rules/Config</c> 的对应成员上。
/// </summary>
/// <remarks>
/// <para>
/// **为何另立本文件**：其余测试一律经配置成员取期望（SC-009）；当同一个配置成员既出现在被测代码、
/// 又出现在期望值一侧时，断言会退化成「配置等于自己」——例如把
/// <c>OriginStartTable.InitialCashGuan(Merchant)</c> 从 500 改成 5，其余断言仍然全绿。
/// 本文件用**字面量**（规格书 §10.1 / §8.2 / §4.1 上的那些数）把成员的值钉住，
/// 故每行锚点都带 <c>// arch-guard:allow 锚点即被验证对象</c>——这是契约八 §3 条款 2
/// 为锚点留的口子，与 <c>SalaryTableTests</c>（72/420/5100）、<c>SalaryModeTests</c> 同款。
/// </para>
/// <para>
/// **只锚定数值本身**：行为断言（待阙递减、授官、考课、致仕与半俸的实际推进）仍归
/// <c>AppointmentTests</c> / <c>CareerAdvanceTests</c> / <c>RetirementTests</c> / <c>SalaryModeTests</c>。
/// </para>
/// </remarks>
public class SpecAnchorTests
{
    /// <summary>固定随机替身使用的常量取值（非规则数值，只是夹具输入）。</summary>
    private const double ConstantRoll = 0.3d;  // arch-guard:allow 随机替身的夹具输入（非规则数值副本）

    /// <summary>
    /// SC-002：四出身的初始资产与规格书 §10.1 的表格**逐格**一致（现金 / 田 / 宅 / 商本）。
    /// </summary>
    [Fact]
    public void 四出身的初始资产逐格等于规格书()
    {
        // 现金（贯）：农 80 / 工 80 / 商 500 / 士 200（§10.1）。
        Assert.Equal(80m, GameConfig.NewGame.InitialCashGuan(Origin.Farmer));
        Assert.Equal(80m, GameConfig.NewGame.InitialCashGuan(Origin.Artisan));
        Assert.Equal(500m, GameConfig.NewGame.InitialCashGuan(Origin.Merchant));
        Assert.Equal(200m, GameConfig.NewGame.InitialCashGuan(Origin.Scholar));

        // 田（亩）：仅农 40（§10.1）。
        Assert.Equal(40, GameConfig.NewGame.InitialFarmlandMu(Origin.Farmer));
        Assert.Equal(0, GameConfig.NewGame.InitialFarmlandMu(Origin.Artisan));
        Assert.Equal(0, GameConfig.NewGame.InitialFarmlandMu(Origin.Merchant));
        Assert.Equal(0, GameConfig.NewGame.InitialFarmlandMu(Origin.Scholar));

        // 农村宅（座）：农 / 工 / 士各 1（「农舍」与「农村宅」同价，§5.3）。
        Assert.Equal(1, GameConfig.NewGame.InitialRuralHouses(Origin.Farmer));
        Assert.Equal(1, GameConfig.NewGame.InitialRuralHouses(Origin.Artisan));
        Assert.Equal(0, GameConfig.NewGame.InitialRuralHouses(Origin.Merchant));
        Assert.Equal(1, GameConfig.NewGame.InitialRuralHouses(Origin.Scholar));

        // 城市宅（座）：仅商 1；商本（贯）：仅商 300（§10.1、§5.2）。
        Assert.Equal(0, GameConfig.NewGame.InitialUrbanHouses(Origin.Farmer));
        Assert.Equal(0, GameConfig.NewGame.InitialUrbanHouses(Origin.Artisan));
        Assert.Equal(1, GameConfig.NewGame.InitialUrbanHouses(Origin.Merchant));
        Assert.Equal(0, GameConfig.NewGame.InitialUrbanHouses(Origin.Scholar));

        Assert.Equal(0m, GameConfig.NewGame.InitialMerchantCapitalGuan(Origin.Farmer));
        Assert.Equal(0m, GameConfig.NewGame.InitialMerchantCapitalGuan(Origin.Artisan));
        Assert.Equal(300m, GameConfig.NewGame.InitialMerchantCapitalGuan(Origin.Merchant));
        Assert.Equal(0m, GameConfig.NewGame.InitialMerchantCapitalGuan(Origin.Scholar));
    }

    /// <summary>SC-002：成员构成、年龄与学业/体质区间逐格等于规格书 §10.1。</summary>
    [Fact]
    public void 四出身的成员构成与年龄学业体质区间等于规格书()
    {
        // 配偶 1 名；孩子 农 2 / 工 1 / 商 2 / 士 1（§10.1、FR-003）。
        Assert.Equal(1, GameConfig.NewGame.SpouseCount);
        Assert.Equal(2, GameConfig.NewGame.ChildCount(Origin.Farmer));
        Assert.Equal(1, GameConfig.NewGame.ChildCount(Origin.Artisan));
        Assert.Equal(2, GameConfig.NewGame.ChildCount(Origin.Merchant));
        Assert.Equal(1, GameConfig.NewGame.ChildCount(Origin.Scholar));

        // 家主 28±5（士 30±5）、配偶 25±5、孩子 0~8（整数均匀、含端点）。
        Assert.Equal(5, OriginStartTable.AgeSpread);
        Assert.Equal(23, OriginStartTable.HeadAgeMin(Origin.Farmer));
        Assert.Equal(33, OriginStartTable.HeadAgeMax(Origin.Farmer));
        Assert.Equal(23, OriginStartTable.HeadAgeMin(Origin.Artisan));
        Assert.Equal(33, OriginStartTable.HeadAgeMax(Origin.Artisan));
        Assert.Equal(23, OriginStartTable.HeadAgeMin(Origin.Merchant));
        Assert.Equal(33, OriginStartTable.HeadAgeMax(Origin.Merchant));
        Assert.Equal(25, OriginStartTable.HeadAgeMin(Origin.Scholar));
        Assert.Equal(35, OriginStartTable.HeadAgeMax(Origin.Scholar));
        Assert.Equal(25, OriginStartTable.SpouseAgeMean);
        Assert.Equal(0, OriginStartTable.ChildAgeMin);
        Assert.Equal(8, OriginStartTable.ChildAgeMax);

        // 家主学业：士 60（常量、不掷骰）/ 其余 10~30；孩子学业 0（常量）。
        Assert.Equal(60, OriginStartTable.ScholarHeadStudy);
        Assert.Equal(10, OriginStartTable.CommonerHeadStudyMin);
        Assert.Equal(30, OriginStartTable.CommonerHeadStudyMax);
        Assert.Equal(0, OriginStartTable.ChildStudyValue);

        // 体质：家主 80~100；孩子 90~100。
        Assert.Equal(80, OriginStartTable.HeadHealthMin);
        Assert.Equal(100, OriginStartTable.HeadHealthMax);
        Assert.Equal(90, OriginStartTable.ChildHealthMin);
        Assert.Equal(100, OriginStartTable.ChildHealthMax);
    }

    /// <summary>SC-002：属性分布参数等于规格书 §4.1/§4.2（天赋 / 学业 / 体质 / 天命寿数）。</summary>
    [Fact]
    public void 属性分布参数等于规格书()
    {
        Assert.Equal(60m, GameConfig.Attributes.TalentMean);
        Assert.Equal(20m, GameConfig.Attributes.TalentSigma);
        Assert.Equal(30m, GameConfig.Attributes.StudyMean);
        Assert.Equal(15m, GameConfig.Attributes.StudySigma);
        Assert.Equal(85m, GameConfig.Attributes.HealthMean);
        Assert.Equal(10m, GameConfig.Attributes.HealthSigma);

        Assert.Equal(60.7m, GameConfig.Attributes.LifespanMean(Gender.Male));      // arch-guard:allow 锚点即被验证对象
        Assert.Equal(8m, GameConfig.Attributes.LifespanSigma(Gender.Male));
        Assert.Equal(62.3m, GameConfig.Attributes.LifespanMean(Gender.Female));    // arch-guard:allow 锚点即被验证对象
        Assert.Equal(8m, GameConfig.Attributes.LifespanSigma(Gender.Female));
    }

    /// <summary>
    /// FR-006：天命寿数分布**与出身无关**——固定随机替身下，四种出身的家主寿数 MUST 相同
    /// （同一个 <c>N(mean, sigma)</c>，不受出身分支影响）。
    /// </summary>
    [Fact]
    public void 天命寿数分布与出身无关()
    {
        var expected = GameConfig.Attributes.NextLifespan(
            Gender.Male, new ConstantRandomService(ConstantRoll));

        foreach (var origin in new[] { Origin.Farmer, Origin.Artisan, Origin.Merchant, Origin.Scholar })
        {
            var setup = NewGameSetup.Create(
                new NewGameRequest(origin, Difficulty.Normal, "测", new GameDate(1, 1)),
                new ConstantRandomService(ConstantRoll),
                new FixedNameGenerator("测"));

            var head = setup.State.Family.TryGet(setup.HeadId!.Value);

            Assert.NotNull(head);
            Assert.Equal(expected, head.Lifespan);
        }
    }

    /// <summary>R-08 / 契约六 §3 条款 2：标准正态取样**恰好消耗 2 次** <c>NextDouble</c>（无静态缓存）。</summary>
    [Fact]
    public void 标准正态取样恰好消耗两次NextDouble()
    {
        var counting = new CountingRandomService(ConstantRoll);

        GameConfig.Attributes.NextNormal(0m, 1m, counting);

        Assert.Equal(2, counting.Doubles);

        // 无缓存：第二次取样同样恰好 2 次（有缓存时会退化成 0~1 次）。
        GameConfig.Attributes.NextNormal(0m, 1m, counting);

        Assert.Equal(4, counting.Doubles);
    }

    /// <summary>SC-003/SC-004/SC-005/SC-007：官吏体系的全部规则数值等于规格书 §8.2。</summary>
    [Fact]
    public void 官吏体系数值等于规格书()
    {
        // 待阙 6~24 月（含端点）。
        Assert.Equal(6, GameConfig.Career.AwaitingPostMinMonths);
        Assert.Equal(24, GameConfig.Career.AwaitingPostMaxMonths);

        // 初始官阶：一甲 L11 / 二甲 L13 / 三甲 L15 / 特奏名 L18。
        Assert.Equal(11, GameConfig.Career.InitialRankOf(AppointmentTrack.FirstClass));
        Assert.Equal(13, GameConfig.Career.InitialRankOf(AppointmentTrack.SecondClass));
        Assert.Equal(15, GameConfig.Career.InitialRankOf(AppointmentTrack.ThirdClass));
        Assert.Equal(18, GameConfig.Career.InitialRankOf(AppointmentTrack.SpecialTribute));

        // 政绩 +1/月、上限 100；考课周期 36 月。
        Assert.Equal(1, GameConfig.Career.MeritPerMonth);
        Assert.Equal(100, GameConfig.Career.MeritMaximum);
        Assert.Equal(36, GameConfig.Career.AppraisalPeriodMonths);

        // 升级概率：基础 25%、每点政绩 +0.3%、封顶 70%。
        Assert.Equal(0.25m, GameConfig.Career.PromotionBaseChance);        // arch-guard:allow 锚点即被验证对象
        Assert.Equal(0.003m, GameConfig.Career.PromotionChancePerMerit);   // arch-guard:allow 锚点即被验证对象
        Assert.Equal(0.70m, GameConfig.Career.PromotionChanceCap);         // arch-guard:allow 锚点即被验证对象

        // 致仕 70 岁、半俸 50%；士出身当官 ×1.05。
        Assert.Equal(70, GameConfig.Career.RetirementAge);
        Assert.Equal(0.50m, GameConfig.Career.RetirementSalaryRatio);      // arch-guard:allow 锚点即被验证对象
        Assert.Equal(1.05m, GameConfig.Salary.ScholarOriginMultiplier);    // arch-guard:allow 锚点即被验证对象
    }

    /// <summary>
    /// SC-005：概率公式 <c>min(25% + 政绩 × 0.3%, 70%)</c> 逐点等于规格书
    /// （含政绩 100 的不封顶点与超出政绩区间的封顶分支）。
    /// </summary>
    [Fact]
    public void 考课概率公式逐点等于规格书()
    {
        Assert.Equal(0.25m, GameConfig.Career.PromotionChance(0));      // arch-guard:allow 锚点即被验证对象
        Assert.Equal(0.55m, GameConfig.Career.PromotionChance(100));
        Assert.Equal(0.70m, GameConfig.Career.PromotionChance(200));    // arch-guard:allow 锚点即被验证对象
    }

    /// <summary>
    /// SC-002/SC-004/SC-006/SC-007（005）：科举体系的全部规则数值等于规格书 §6 / §7.1 / §7.2。
    /// </summary>
    [Fact]
    public void 科举体系数值等于规格书()
    {
        // 报考年龄下限 14 岁、免解阈值 3 败（§6）。
        Assert.Equal(14, GameConfig.Exam.MinimumAge);
        Assert.Equal(3, GameConfig.Exam.JieShiExemptionFailureThreshold);

        // 三级基础率：解试 1%、省试 10%、殿试进甲 5%（§6）。
        Assert.Equal(0.01m, GameConfig.Exam.JieShiBaseRate);   // arch-guard:allow 锚点即被验证对象
        Assert.Equal(0.10m, GameConfig.Exam.ShengShiBaseRate); // arch-guard:allow 锚点即被验证对象
        Assert.Equal(0.05m, GameConfig.Exam.ImperialPromotionBaseRate); // arch-guard:allow 锚点即被验证对象

        // 学业系数 0.4 + 0.012 × 学业（§6）：0 → 0.4、50 → 1.0、100 → 1.6。
        Assert.Equal(0.4m, GameConfig.Exam.StudyFactorBase);       // arch-guard:allow 锚点即被验证对象
        Assert.Equal(0.012m, GameConfig.Exam.StudyFactorPerPoint); // arch-guard:allow 锚点即被验证对象
        Assert.Equal(0.4m, GameConfig.Exam.StudyFactor(0));        // arch-guard:allow 锚点即被验证对象
        Assert.Equal(1.0m, GameConfig.Exam.StudyFactor(50));       // arch-guard:allow 锚点即被验证对象
        Assert.Equal(1.6m, GameConfig.Exam.StudyFactor(100));      // arch-guard:allow 锚点即被验证对象

        // 仕天赋除数 400、仕族加成 ×1.1、未进甲的二甲学业线 50（§6）。
        Assert.Equal(400, GameConfig.Exam.OfficialdomDivisor);
        Assert.Equal(1.1m, GameConfig.Exam.ShiStatusAdmissionBonus); // arch-guard:allow 锚点即被验证对象
        Assert.Equal(50, GameConfig.Exam.SecondClassStudyThreshold);

        // 需掷骰概率的封顶 89.9999%（§7.1）；行贿乘数 k⁰ 的恒等元 = 1.0（§7.1，006 起由 BribePolicy 唯一给出）。
        Assert.Equal(0.899999m, GameConfig.Exam.DiceRateCap);          // arch-guard:allow 锚点即被验证对象
        Assert.Equal(1.0m, GameConfig.Bribe.UnitMultiplier);           // arch-guard:allow 锚点即被验证对象

        // 特奏名两门槛：50 岁、省试累计 6 败（§7.2）。
        Assert.Equal(50, GameConfig.Exam.SpecialTributeMinimumAge);
        Assert.Equal(6, GameConfig.Exam.SpecialTributeFailureThreshold);

        // 省试路费四档 10 / 20 / 40 / 80 贯（§6）——**不乘**难度支出系数。
        Assert.Equal(10, GameConfig.Exam.ExamTravelCostGuan(Difficulty.Easy));
        Assert.Equal(20, GameConfig.Exam.ExamTravelCostGuan(Difficulty.Normal));
        Assert.Equal(40, GameConfig.Exam.ExamTravelCostGuan(Difficulty.Hard));
        Assert.Equal(80, GameConfig.Exam.ExamTravelCostGuan(Difficulty.Hell));

        // 路费与难度支出系数**同档不同义**：路费只按难度换档，MUST NOT 再乘支出系数。
        // 「普通」档支出系数恰为 1.0，二者同值属预期，故只对系数 ≠ 1 的三档作证（契约十三 §3.1）。
        foreach (var difficulty in new[] { Difficulty.Easy, Difficulty.Hard, Difficulty.Hell })
        {
            var guan = (decimal)GameConfig.Exam.ExamTravelCostGuan(difficulty);
            var expenseFactor = GameConfig.DifficultyRate.ExpenseFactor(difficulty);

            Assert.NotEqual(1.0m, expenseFactor);
            Assert.NotEqual(guan, guan * expenseFactor);
        }
    }

    /// <summary>
    /// SC-002/SC-003/SC-007（006）：贿赂与惩罚的规则数值等于规格书 §7.1 / §7.4 / §7.5。
    /// </summary>
    [Fact]
    public void 贿赂与惩罚体系数值等于规格书()
    {
        // 窗口 = 本级别考试当月与**前一月**（§7.1）。
        Assert.Equal(1, GameConfig.Bribe.WindowPreviousMonths);

        // 行贿金额与被抓率的共用递进步长 20%；被抓概率封顶 100%；k⁰ = 1.0（§7.1）。
        Assert.Equal(0.2m, GameConfig.Bribe.ProgressionStep);      // arch-guard:allow 锚点即被验证对象
        Assert.Equal(1.0m, GameConfig.Bribe.CaughtProbabilityCap); // arch-guard:allow 锚点即被验证对象
        Assert.Equal(1.0m, GameConfig.Bribe.UnitMultiplier);       // arch-guard:allow 锚点即被验证对象

        // 基础金额：解试 100 / 省试 500 / 殿试 2000 贯（§7.1）。
        Assert.Equal(100, GameConfig.Bribe.BaseAmountGuan(ExamLevel.JieShi));
        Assert.Equal(500, GameConfig.Bribe.BaseAmountGuan(ExamLevel.ShengShi));
        Assert.Equal(2000, GameConfig.Bribe.BaseAmountGuan(ExamLevel.DianShi));

        // 基础被抓率：解试 5% / 省试 15% / 殿试 35%（§7.1）。
        Assert.Equal(0.05m, GameConfig.Bribe.BaseCaughtRate(ExamLevel.JieShi));   // arch-guard:allow 锚点即被验证对象
        Assert.Equal(0.15m, GameConfig.Bribe.BaseCaughtRate(ExamLevel.ShengShi)); // arch-guard:allow 锚点即被验证对象
        Assert.Equal(0.35m, GameConfig.Bribe.BaseCaughtRate(ExamLevel.DianShi));  // arch-guard:allow 锚点即被验证对象

        // 效果乘数底数 k：解试 1.6 / 省试 1.3 / 殿试 1.1（§7.1）。
        Assert.Equal(1.6m, GameConfig.Bribe.EffectMultiplierK(ExamLevel.JieShi));   // arch-guard:allow 锚点即被验证对象
        Assert.Equal(1.3m, GameConfig.Bribe.EffectMultiplierK(ExamLevel.ShengShi)); // arch-guard:allow 锚点即被验证对象
        Assert.Equal(1.1m, GameConfig.Bribe.EffectMultiplierK(ExamLevel.DianShi));  // arch-guard:allow 锚点即被验证对象

        // 通则：比例罚金封顶 50000 贯、全族禁考禁升 48 月、释放升 1 级（§7.4、§7.5）。
        Assert.Equal(50000, GameConfig.Punishment.RatioFineCapGuan);
        Assert.Equal(48, GameConfig.Punishment.FamilyWideBanMonths);
        Assert.Equal(1, GameConfig.Punishment.ReleaseDemotionLevels);

        // ---- 12 格矩阵：行 = 难度，列 = 殿试 / 省试 / 解试（§7.4）----

        // 简单：60 月 / 比例 2%，36 月 / 比例 1%，12 月 / 无罚金；三格皆无禁考、无禁升。
        AssertCell(GameConfig.Punishment.CellOf(Difficulty.Easy, ExamLevel.DianShi), 60, ExecutionKind.None, false, FineKind.Ratio, 0.02m, 0, 0, false, false); // arch-guard:allow 锚点即被验证对象
        AssertCell(GameConfig.Punishment.CellOf(Difficulty.Easy, ExamLevel.ShengShi), 36, ExecutionKind.None, false, FineKind.Ratio, 0.01m, 0, 0, false, false); // arch-guard:allow 锚点即被验证对象
        AssertCell(GameConfig.Punishment.CellOf(Difficulty.Easy, ExamLevel.JieShi), 12, ExecutionKind.None, false, FineKind.None, 0m, 0, 0, false, false);

        // 普通：180 月 / 比例 15% / 禁考 36 / 禁升；60 月 / 固定 200 贯 / 禁考 24 / 禁升；
        //       36 月 / 固定 100 贯 / 无禁考。
        AssertCell(GameConfig.Punishment.CellOf(Difficulty.Normal, ExamLevel.DianShi), 180, ExecutionKind.None, false, FineKind.Ratio, 0.15m, 0, 36, true, false); // arch-guard:allow 锚点即被验证对象
        AssertCell(GameConfig.Punishment.CellOf(Difficulty.Normal, ExamLevel.ShengShi), 60, ExecutionKind.None, false, FineKind.Fixed, 0m, 200, 24, true, false);
        AssertCell(GameConfig.Punishment.CellOf(Difficulty.Normal, ExamLevel.JieShi), 36, ExecutionKind.None, false, FineKind.Fixed, 0m, 100, 0, false, false);

        // 困难：殿试**死刑** / 比例 20% / 禁考 48 / 禁升；省试 240 月 / 比例 10% / **只**禁考 36；
        //       解试 120 月 / 比例 5% / **只**禁考 36。
        AssertCell(GameConfig.Punishment.CellOf(Difficulty.Hard, ExamLevel.DianShi), 0, ExecutionKind.ImmediateSentence, false, FineKind.Ratio, 0.20m, 0, 48, true, false); // arch-guard:allow 锚点即被验证对象
        AssertCell(GameConfig.Punishment.CellOf(Difficulty.Hard, ExamLevel.ShengShi), 240, ExecutionKind.None, false, FineKind.Ratio, 0.10m, 0, 36, false, false); // arch-guard:allow 锚点即被验证对象
        AssertCell(GameConfig.Punishment.CellOf(Difficulty.Hard, ExamLevel.JieShi), 120, ExecutionKind.None, false, FineKind.Ratio, 0.05m, 0, 36, false, false); // arch-guard:allow 锚点即被验证对象

        // 地狱：殿试**死刑 + 连坐** / 比例 20% / **全族**禁考禁升 48；省试与解试同困难档。
        AssertCell(GameConfig.Punishment.CellOf(Difficulty.Hell, ExamLevel.DianShi), 0, ExecutionKind.ImmediateSentence, true, FineKind.Ratio, 0.20m, 0, 48, true, true); // arch-guard:allow 锚点即被验证对象
        AssertCell(GameConfig.Punishment.CellOf(Difficulty.Hell, ExamLevel.ShengShi), 240, ExecutionKind.None, false, FineKind.Ratio, 0.10m, 0, 36, false, false); // arch-guard:allow 锚点即被验证对象
        AssertCell(GameConfig.Punishment.CellOf(Difficulty.Hell, ExamLevel.JieShi), 120, ExecutionKind.None, false, FineKind.Ratio, 0.05m, 0, 36, false, false); // arch-guard:allow 锚点即被验证对象

        // 死刑格的口径：服刑月数为 0 且执行方式非 None（契约十七 §3 条款 2）。
        Assert.Equal(0, GameConfig.Punishment.CellOf(Difficulty.Hard, ExamLevel.DianShi).SentenceMonths);
        Assert.NotEqual(ExecutionKind.None, GameConfig.Punishment.CellOf(Difficulty.Hard, ExamLevel.DianShi).Execution);
    }

    /// <summary>锚点辅助：逐格断言处罚格的九个字段。</summary>
    private static void AssertCell(
        PunishmentCell cell,
        int sentenceMonths,
        ExecutionKind execution,
        bool collective,
        FineKind fine,
        decimal fineRatio,
        int fineFixedGuan,
        int relativeBanMonths,
        bool relativePromotionBan,
        bool familyWideBan)
    {
        Assert.Equal(sentenceMonths, cell.SentenceMonths);
        Assert.Equal(execution, cell.Execution);
        Assert.Equal(collective, cell.CollectivePunishment);
        Assert.Equal(fine, cell.Fine);
        Assert.Equal(fineRatio, cell.FineRatio);
        Assert.Equal(Money.FromGuan(fineFixedGuan), cell.FineFixedGuan);
        Assert.Equal(relativeBanMonths, cell.RelativeBanMonths);
        Assert.Equal(relativePromotionBan, cell.RelativePromotionBan);
        Assert.Equal(familyWideBan, cell.FamilyWideBan);
    }

    /// <summary>计数随机替身：统计 <see cref="IRandomService.NextDouble"/> 的调用次数，取值恒定。</summary>
    private sealed class CountingRandomService : IRandomService
    {
        private readonly double _value;

        /// <summary>构造：<see cref="NextDouble"/> 恒返回给定取值。</summary>
        /// <param name="value">返回值。</param>
        public CountingRandomService(double value) => _value = value;

        /// <summary>至今被请求的 <see cref="IRandomService.NextDouble"/> 次数。</summary>
        public int Doubles { get; private set; }

        /// <inheritdoc />
        public double NextDouble()
        {
            Doubles++;
            return _value;
        }

        /// <inheritdoc />
        public int Next(int minInclusive, int maxExclusive) => minInclusive;

        /// <inheritdoc />
        public void NextBytes(Span<byte> destination) => destination.Clear();
    }

    /// <summary>
    /// 恒定取值随机替身：<see cref="IRandomService.NextDouble"/> 恒返回给定值、<c>Next</c> 恒返回下界，
    /// <see cref="IRandomService.NextBytes"/> 写入**非零**字节（<c>Guid.Empty</c> 会被 <c>GameState</c> 拒绝）。
    /// </summary>
    private sealed class ConstantRandomService : IRandomService
    {
        private readonly double _value;

        /// <summary>构造：<see cref="NextDouble"/> 恒返回给定取值。</summary>
        /// <param name="value">返回值。</param>
        public ConstantRandomService(double value) => _value = value;

        /// <inheritdoc />
        public double NextDouble() => _value;

        /// <inheritdoc />
        public int Next(int minInclusive, int maxExclusive) => minInclusive;

        /// <inheritdoc />
        public void NextBytes(Span<byte> destination) => destination.Fill(1);
    }
}
