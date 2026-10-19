using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;
using KFL.Rules.Exam;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// T023 / SC-002 / SC-012：科举录取与殿试判定的纯函数（规格书 §6、§7.1；契约十三 §4）——
/// 解试 / 省试录取率（基础率 × 学业系数 × 仕天赋 × 贿赂乘数 × 仕族加成）、
/// 殿试进甲率（**不含**仕族项）、甲第归属与一甲名次 roll。
/// </summary>
/// <remarks>
/// <para>
/// 期望值一律经 <c>KFL.Rules/Config</c> 的成员（<see cref="GameConfig.Exam"/>）取得，
/// 本文件 MUST NOT 另存规则数值（SC-008）；确需的用例输入字面量逐行以
/// <c>// arch-guard:allow</c> 标注理由。
/// </para>
/// <para>
/// 免解（Exemption）分区由 T023 的另一份测试单独覆盖，本文件 MUST NOT 涉及。
/// </para>
/// </remarks>
public class ExamAdmissionTests
{
    /// <summary>录取率用例的贿赂乘数：一个刻意不同于中性值的输入（非规则数值副本）。</summary>
    private const decimal BribeMultiplier = 1.5m; // arch-guard:allow 用例输入：验证贿赂乘数作为乘区生效（非规则数值副本）

    /// <summary>把概率放大到封顶点的用例输入（非规则数值副本）。</summary>
    private const decimal HugeBribeMultiplier = 100m; // arch-guard:allow 用例输入：把概率推过 DiceRateCap 以断言封顶（非规则数值副本）

    /// <summary>覆盖学业系数低 / 中 / 高的用例学业值。</summary>
    private static readonly int[] StudyValues = [0, 25, 60, 100];

    /// <summary>解试录取率 = 基础率 × 学业系数 × (1 + 仕天赋 / 400) × 贿赂乘数（无仕族加成）。</summary>
    [Fact]
    public void 解试录取率由基础率学业系数仕天赋与贿赂乘数相乘()
    {
        var person = RulesHarness.Member(101, Gender.Male, 20, officialdom: 80);
        var officialdom = person.Talents.Officialdom;

        foreach (var study in StudyValues)
        {
            person.Study = study;

            var expected = Math.Min(
                GameConfig.Exam.JieShiBaseRate
                    * GameConfig.Exam.StudyFactor(study)
                    * (decimal.One + (officialdom / (decimal)GameConfig.Exam.OfficialdomDivisor))
                    * BribeMultiplier,
                GameConfig.Exam.DiceRateCap);

            Assert.Equal(
                expected,
                ExamAdmission.AdmissionRate(
                    ExamLevel.JieShi, person, hasShiStatus: false, bribeMultiplier: BribeMultiplier));
        }
    }

    /// <summary>
    /// T055 / FR-014 / SC-002：spec US1 AS1 的示例录取率——**2026-10-19 平衡性裁决后**为
    /// <c>0.25 × (0.4 + 0.012 × 60) × (1 + 60/400) × 1.0 × 1.0 = 0.322</c>（即 32.2%），
    /// 原「勘误后 1.288%」因基础率上调而作废。
    /// </summary>
    /// <remarks>
    /// 期望值经 <see cref="GameConfig.Exam"/> 的数值成员组合（唯一数值来源仍是
    /// <c>KFL.Rules/Config/ExamPolicy</c>）；本用例只出现被验证对象本身 <c>0.322m</c>，
    /// 不书写任何规则数值。
    /// </remarks>
    [Fact]
    public void 科举示例录取率等于当前基础率下的示例值()
    {
        var person = RulesHarness.Member(102, Gender.Male, 20, officialdom: 60);
        person.Study = 60;

        var expected = GameConfig.Exam.JieShiBaseRate
            * GameConfig.Exam.StudyFactor(person.Study)
            * (decimal.One + (person.Talents.Officialdom / (decimal)GameConfig.Exam.OfficialdomDivisor))
            * GameConfig.Bribe.UnitMultiplier;

        // arch-guard:allow 锚点即被验证对象：当前基础率下示例值（非规则数值副本）
        Assert.Equal(0.322m, expected);
        Assert.Equal(
            expected,
            ExamAdmission.AdmissionRate(
                ExamLevel.JieShi, person, hasShiStatus: false, bribeMultiplier: GameConfig.Bribe.UnitMultiplier));
    }

    /// <summary>省试录取率与解试同形，只把基础率换成 <c>ShengShiBaseRate</c>。</summary>
    [Fact]
    public void 省试录取率使用省试基础率()
    {
        var person = RulesHarness.Member(102, Gender.Male, 20, officialdom: 100);
        var officialdom = person.Talents.Officialdom;

        foreach (var study in StudyValues)
        {
            person.Study = study;

            var expected = Math.Min(
                GameConfig.Exam.ShengShiBaseRate
                    * GameConfig.Exam.StudyFactor(study)
                    * (decimal.One + (officialdom / (decimal)GameConfig.Exam.OfficialdomDivisor))
                    * BribeMultiplier,
                GameConfig.Exam.DiceRateCap);

            Assert.Equal(
                expected,
                ExamAdmission.AdmissionRate(
                    ExamLevel.ShengShi, person, hasShiStatus: false, bribeMultiplier: BribeMultiplier));
        }
    }

    /// <summary>仕族加成只乘在录取率上：同人同参时恰好差一个 <c>ShiStatusAdmissionBonus</c>。</summary>
    [Fact]
    public void 仕族加成只乘在录取率上()
    {
        var person = RulesHarness.Member(103, Gender.Male, 20, officialdom: 60);
        person.Study = 40;
        var officialdom = person.Talents.Officialdom;

        var withoutShi = ExamAdmission.AdmissionRate(
            ExamLevel.JieShi, person, hasShiStatus: false, bribeMultiplier: GameConfig.Bribe.UnitMultiplier);
        var withShi = ExamAdmission.AdmissionRate(
            ExamLevel.JieShi, person, hasShiStatus: true, bribeMultiplier: GameConfig.Bribe.UnitMultiplier);

        Assert.NotEqual(withoutShi, withShi);
        Assert.Equal(withoutShi * GameConfig.Exam.ShiStatusAdmissionBonus, withShi);

        // 进甲率不带仕族乘区：结果必须恰好等于「无仕族加成」的同形公式。
        var promotionExpected = GameConfig.Exam.ImperialPromotionBaseRate
            * GameConfig.Exam.StudyFactor(person.Study)
            * (decimal.One + (officialdom / (decimal)GameConfig.Exam.OfficialdomDivisor))
            * GameConfig.Bribe.UnitMultiplier;

        Assert.Equal(
            promotionExpected,
            ExamAdmission.ImperialPromotionRate(person, GameConfig.Bribe.UnitMultiplier));
    }

    /// <summary>殿试走进甲率入口，录取率入口对殿试显式拒绝。</summary>
    [Fact]
    public void 殿试走进甲率入口而录取率入口拒绝()
    {
        var person = RulesHarness.Member(104, Gender.Male, 20, officialdom: 40);
        person.Study = 70;

        Assert.Throws<ArgumentOutOfRangeException>(() => ExamAdmission.AdmissionRate(
            ExamLevel.DianShi, person, hasShiStatus: false, bribeMultiplier: GameConfig.Bribe.UnitMultiplier));

        // 对照：同一个人走殿试进甲率入口是合法的。
        Assert.InRange(
            ExamAdmission.ImperialPromotionRate(person, GameConfig.Bribe.UnitMultiplier),
            decimal.Zero,
            GameConfig.Exam.DiceRateCap);
    }

    /// <summary>任意组合下两条概率都不超过 <c>DiceRateCap</c>，极大乘数时恰好等于封顶。</summary>
    [Fact]
    public void 录取率与进甲率都封顶在DiceRateCap()
    {
        var person = RulesHarness.Member(105, Gender.Male, 20, officialdom: 100);
        person.Study = 100;

        Assert.Equal(
            GameConfig.Exam.DiceRateCap,
            ExamAdmission.AdmissionRate(
                ExamLevel.JieShi, person, hasShiStatus: true, bribeMultiplier: HugeBribeMultiplier));
        Assert.Equal(
            GameConfig.Exam.DiceRateCap,
            ExamAdmission.ImperialPromotionRate(person, HugeBribeMultiplier));

        var multipliers = new[]
        {
            GameConfig.Bribe.UnitMultiplier,
            BribeMultiplier,
            HugeBribeMultiplier,
        };

        foreach (var study in StudyValues)
        {
            person.Study = study;

            foreach (var multiplier in multipliers)
            {
                foreach (var level in new[] { ExamLevel.JieShi, ExamLevel.ShengShi })
                {
                    foreach (var hasShiStatus in new[] { false, true })
                    {
                        var rate = ExamAdmission.AdmissionRate(level, person, hasShiStatus, multiplier);
                        Assert.InRange(rate, decimal.Zero, GameConfig.Exam.DiceRateCap);
                    }
                }

                Assert.InRange(
                    ExamAdmission.ImperialPromotionRate(person, multiplier),
                    decimal.Zero,
                    GameConfig.Exam.DiceRateCap);
            }
        }
    }

    /// <summary>
    /// 进甲率结构上不含仕族项（可证断言）：签名参数恰为 <c>(Person, decimal)</c>，
    /// 既无 <c>Family</c> 也无 <c>bool</c>，故方法体在类型层面就够不着 <c>Family.HasShiStatus</c>。
    /// </summary>
    [Fact]
    public void 进甲率结构上不含仕族项()
    {
        var parameters = typeof(ExamAdmission)
            .GetMethod(nameof(ExamAdmission.ImperialPromotionRate))!
            .GetParameters();

        Assert.Equal(
            new[] { typeof(Person), typeof(decimal) },
            parameters.Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(parameters, parameter => parameter.ParameterType == typeof(Family));
        Assert.DoesNotContain(parameters, parameter => parameter.ParameterType == typeof(bool));
    }

    /// <summary>甲第归属：进甲恒一甲；未进甲按学业与二甲线的比较分二甲 / 三甲。</summary>
    [Fact]
    public void 甲第归属进甲恒一甲否则按二甲学业线()
    {
        var threshold = GameConfig.Exam.SecondClassStudyThreshold;

        foreach (var study in StudyValues)
        {
            var promotedPerson = RulesHarness.Member(201, Gender.Male, 30);
            promotedPerson.Study = study;

            Assert.Equal(ImperialClass.FirstClass, ExamAdmission.ImperialClassFor(promotedPerson, promoted: true));
        }

        var atThreshold = RulesHarness.Member(202, Gender.Male, 30);
        atThreshold.Study = threshold;
        Assert.Equal(ImperialClass.SecondClass, ExamAdmission.ImperialClassFor(atThreshold, promoted: false));

        var belowThreshold = RulesHarness.Member(203, Gender.Male, 30);
        belowThreshold.Study = threshold - 1;
        Assert.Equal(ImperialClass.ThirdClass, ExamAdmission.ImperialClassFor(belowThreshold, promoted: false));
    }

    /// <summary>一甲名次由恰好一次 <c>Next(1, 4)</c> 映射：1 → 状元、2 → 榜眼、3 → 探花。</summary>
    [Fact]
    public void 一甲名次由恰好一次Next映射()
    {
        (int NextValue, ImperialPlacement Expected)[] cases =
        [
            (1, ImperialPlacement.ZhuangYuan),
            (2, ImperialPlacement.BangYan),
            (3, ImperialPlacement.TanHua),
        ];

        foreach (var (nextValue, expected) in cases)
        {
            var stub = new RecordingRandomService(nextValue);

            Assert.Equal(expected, ExamAdmission.RollPlacement(stub));
            Assert.Equal(1, stub.NextCallCount);
            Assert.Equal(1, stub.LastMinInclusive);
            Assert.Equal(4, stub.LastMaxExclusive);
        }
    }

    /// <summary>越界返回值必须抛 <see cref="ArgumentOutOfRangeException"/>，MUST NOT 静默落档。</summary>
    [Fact]
    public void 名次越界返回值被拒()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ExamAdmission.RollPlacement(new RecordingRandomService(4)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ExamAdmission.RollPlacement(new RecordingRandomService(0)));
    }

    /// <summary>名次与学业无关：学业天差地别的两人、同一替身取值得到同一名次。</summary>
    [Fact]
    public void 名次与学业无关()
    {
        var lowStudy = RulesHarness.Member(301, Gender.Male, 20);
        lowStudy.Study = 0;

        var highStudy = RulesHarness.Member(302, Gender.Male, 20);
        highStudy.Study = 100;

        Assert.NotEqual(lowStudy.Study, highStudy.Study);

        // roll 入口结构上只见 IRandomService（无 Person 参数），故两人不可能影响结果。
        var rollParameters = typeof(ExamAdmission)
            .GetMethod(nameof(ExamAdmission.RollPlacement))!
            .GetParameters();
        Assert.Equal(
            new[] { typeof(IRandomService) },
            rollParameters.Select(parameter => parameter.ParameterType));

        var first = ExamAdmission.RollPlacement(new RecordingRandomService(2));
        var second = ExamAdmission.RollPlacement(new RecordingRandomService(2));

        Assert.Equal(ImperialPlacement.BangYan, first);
        Assert.Equal(first, second);
    }

    /// <summary>
    /// 记录 <c>Next</c> 调用次数与入参、返回预设值的随机替身（非规则数值的真源，仅用于钉住调用形状）。
    /// </summary>
    private sealed class RecordingRandomService(int nextValue) : IRandomService
    {
        /// <summary><c>Next</c> 被调用的次数。</summary>
        public int NextCallCount { get; private set; }

        /// <summary>最近一次 <c>Next</c> 的入参下界。</summary>
        public int LastMinInclusive { get; private set; }

        /// <summary>最近一次 <c>Next</c> 的入参上界。</summary>
        public int LastMaxExclusive { get; private set; }

        /// <inheritdoc />
        public double NextDouble() => 0.5d; // arch-guard:allow 随机替身的固定回退值，本替身只用 Next（非规则数值副本）

        /// <inheritdoc />
        public int Next(int minInclusive, int maxExclusive)
        {
            NextCallCount++;
            LastMinInclusive = minInclusive;
            LastMaxExclusive = maxExclusive;

            return nextValue;
        }

        /// <inheritdoc />
        public void NextBytes(Span<byte> destination) => destination.Clear();
    }
}
