using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Services;
using KFL.Rules.Config;
using KFL.Rules.Exam;
using KFL.Rules.Settlement;

namespace KFL.Tests.Rules;

/// <summary>
/// T024 / T029 / T033：科举季结算步（契约十三）——非考试月零消费、三级判定、
/// 省试路费（SC-006）、免解（SC-004）、特奏名询问与仕族首产。
/// </summary>
/// <remarks>
/// 绝大多数用例**直接调用** <see cref="ExamSeasonRunner.Run"/>，以便把随机消费精确到
/// 「第几次 <c>NextDouble</c>」；只有「步位」两项走 <see cref="MonthlySettlementEngine"/>，
/// 因为那才是「第 ⑦ 步」这一断言的落点。
/// </remarks>
public partial class ExamSeasonTests
{
    private static readonly Guid StateId = Guid.Parse("3f1e7c2a-91b4-4d6e-8a55-77aa99bbccdd");

    /// <summary>解试月：79 年为科年（79 % 3 == 1）的 8 月。</summary>
    private static readonly GameDate JieShiMonth = new(79, 8);

    /// <summary>省试月：科年的次年 3 月。</summary>
    private static readonly GameDate ShengShiMonth = new(80, 3);

    /// <summary>殿试月：科年的次年 4 月。</summary>
    private static readonly GameDate DianShiMonth = new(80, 4);

    /// <summary>非考试月。</summary>
    private static readonly GameDate PlainMonth = new(81, 5);

    // ------------------------------------------------------------------ 非考试月

    private static SettlementResult Settle(GameState state) =>
        new MonthlySettlementEngine(new RecordingRandom(), new GameStateClock(state), new FixedNameGenerator()).Settle(state);

    private static GameState NewState(
        Family family,
        GameDate date,
        Difficulty difficulty = Difficulty.Normal,
        decimal cash = 0m)
    {
        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(RulesHarness.Guan(cash), Money.Zero, Money.Zero),
            new Holdings());

        return new GameState(StateId, date, difficulty, Origin.Scholar, family, economy);
    }

    private static Person BaiShen(int index, int study, int age = 20) =>
        SetStudy(RulesHarness.Member(index, Gender.Male, age, officialdom: 0), study);

    private static Person JuRen(int index, int study, int age = 20)
    {
        var person = SetStudy(RulesHarness.Member(index, Gender.Male, age), study);

        person.AppendDegree(new DegreeRecord(
            DegreeLevel.JuRen, null, RulesHarness.Date, DegreeChangeCause.ExamPass));

        return person;
    }

    private static Person GongShi(int index, int study, int age = 20)
    {
        var person = JuRen(index, study, age);

        person.AppendDegree(new DegreeRecord(
            DegreeLevel.GongShi, null, RulesHarness.Date, DegreeChangeCause.ExamPass));

        return person;
    }

    private static Person SetStudy(Person person, int study)
    {
        person.Study = study;
        return person;
    }

    private static Person Registered(Person person, ExamLevel level)
    {
        person.ApplyForExam(level);
        return person;
    }

    private static void RecordFailures(Person person, ExamLevel level, int count)
    {
        for (var index = 0; index < count; index++)
        {
            person.RecordExamFailure(level);
        }
    }

    /// <summary>
    /// 记录每次随机调用的替身：<see cref="NextDouble"/> 按给定序列给出（用尽后取 <see cref="Fallback"/>），
    /// <see cref="Next"/> 恒返回下界并逐次记账，从而「第几次掷了什么」可被断言。
    /// </summary>
    private sealed class RecordingRandom : IRandomService
    {
        /// <summary>序列用尽后的回退取值（取值须**不**等于任何已登记的规则数值）。</summary>
        private const double Fallback = 0.99d;

        private readonly Queue<double> _values;

        /// <summary>按给定次序构造。</summary>
        /// <param name="values">依次返回的 <see cref="NextDouble"/> 取值。</param>
        public RecordingRandom(params double[] values) => _values = new Queue<double>(values);

        /// <summary><see cref="NextDouble"/> 的累计调用次数。</summary>
        public int NextDoubleCalls { get; private set; }

        /// <summary><see cref="Next"/> 的逐次参数。</summary>
        public List<(int MinInclusive, int MaxExclusive)> NextCalls { get; } = [];

        /// <inheritdoc />
        public double NextDouble()
        {
            NextDoubleCalls++;
            return _values.Count > 0 ? _values.Dequeue() : Fallback;
        }

        /// <inheritdoc />
        public int Next(int minInclusive, int maxExclusive)
        {
            NextCalls.Add((minInclusive, maxExclusive));
            return minInclusive;
        }

        /// <inheritdoc />
        public void NextBytes(Span<byte> destination) => destination.Clear();
    }
}
