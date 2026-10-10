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
    private const double ConstantRoll = 0.3d;

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
