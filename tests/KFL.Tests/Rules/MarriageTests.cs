using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;
using KFL.Rules.Economy;

namespace KFL.Tests.Rules;

/// <summary>
/// US1 婚配的验收测试（契约十九 §2~§5 / FR-014~FR-019）：
/// ① 婚嫁金额表 24 格逐格；② 托媒固定次序与不退费；③ 提亲与捉婿的概率、权重与功名分档；
/// ④ 提议的应答与拒绝；⑤ 买功名婚姻；⑥ 嫁出与外嫁归档。
/// </summary>
/// <remarks>
/// <para>
/// 期望值一律经 <c>KFL.Rules/Config</c> 的成员取得（SC-008），本文件 MUST NOT 另存规则数值；
/// 概率骰值用「档位中点」等运行期算出，避免与登记表撞车。
/// </para>
/// <para>
/// 随机源用 <see cref="ScriptedRandom"/>：给定序列按次序出队，用尽后回退常量，
/// <c>Next</c> 恒返回下界——与 <see cref="FixedRandomService"/> 同款，但额外统计消费次数。
/// </para>
/// </remarks>
public partial class MarriageTests
{
    private static readonly Guid StateId = Guid.Parse("2f5c8a10-9d47-4b6e-8f31-7a2b6c4d5e70");

    // ------------------------------------------------------------------ 婚嫁金额表 24 格

    /// <summary>托媒成功判定必过的骰值（小于成功率）。</summary>
    private static double SuccessRoll => (double)MarriagePolicy.MatchmakerSuccessChance / 2d;

    /// <summary>上月一甲及第的男成员（捉婿窗口 = 次月）。</summary>
    private static Person FirstClassGraduate()
    {
        var member = RulesHarness.Member(1, Gender.Male, 30);
        member.AppendDegree(new DegreeRecord(
            DegreeLevel.JinShi,
            ImperialPlacement.ZhuangYuan,
            PreviousMonth(RulesHarness.Date),
            DegreeChangeCause.ExamPass,
            ImperialClass.FirstClass));

        return member;
    }

    /// <summary>给定月份的上一个月。</summary>
    /// <param name="date">基准月份。</param>
    /// <returns>上一个月。</returns>
    private static GameDate PreviousMonth(GameDate date) =>
        date.Month > 1 ? new GameDate(date.Year, date.Month - 1) : new GameDate(date.Year - 1, 12);

    /// <summary>装配一个单成员（或给定成员）的游戏状态。</summary>
    /// <param name="difficulty">难度。</param>
    /// <param name="origin">出身。</param>
    /// <param name="cash">现银。</param>
    /// <param name="shiStatus">家族是否具仕身份。</param>
    /// <param name="members">成员（首个为家主）。</param>
    /// <returns>游戏状态。</returns>
    private static GameState State(
        Difficulty difficulty,
        Origin origin,
        Money cash,
        bool shiStatus,
        params Person[] members)
    {
        var family = RulesHarness.FamilyOf(members);
        family.HasShiStatus = shiStatus;

        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(cash),
            new Holdings());

        return new GameState(StateId, RulesHarness.Date, difficulty, origin, family, economy);
    }

    /// <summary>取当前资产档的阶层权重。</summary>
    /// <param name="state">游戏状态。</param>
    /// <returns>权重（平民 → 士人 → 仕族）。</returns>
    private static IReadOnlyList<decimal> WeightsOf(GameState state)
    {
        var assets = TotalAssetsCalculator.Of(state.Economy).Guan;

        if (assets < MarriagePolicy.ClassWealthThresholdLow)
        {
            return MarriagePolicy.ClassWeightsPoor;
        }

        return assets >= MarriagePolicy.ClassWealthThresholdHighGuan
            ? MarriagePolicy.ClassWeightsRich
            : MarriagePolicy.ClassWeightsMiddle;
    }

    /// <summary>取某阶层权重段的中点骰值（保证落在该阶层段内）。</summary>
    /// <param name="marriageClass">目标阶层。</param>
    /// <param name="weights">当前资产档的权重。</param>
    /// <returns>骰值。</returns>
    private static double RollIn(MarriageClass marriageClass, IReadOnlyList<decimal> weights)
    {
        var lower = 0d;
        for (var index = 0; index < (int)marriageClass; index++)
        {
            lower += (double)weights[index];
        }

        return lower + ((double)weights[(int)marriageClass] / 2d);
    }

    /// <summary>某类别的账本条目合计。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="category">账本类别。</param>
    /// <returns>合计金额。</returns>
    private static Money TotalOf(GameState state, LedgerCategory category)
    {
        var total = Money.Zero;
        foreach (var entry in state.Economy.Ledger.Entries)
        {
            if (entry.Category == category)
            {
                total += entry.Amount;
            }
        }

        return total;
    }

    /// <summary>某类别的账本条目条数。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="category">账本类别。</param>
    /// <returns>条数。</returns>
    private static int CountOf(GameState state, LedgerCategory category) =>
        state.Economy.Ledger.Entries.Count(entry => entry.Category == category);

    /// <summary>可计数的固定序列随机源（与 <see cref="FixedRandomService"/> 同款 + 消费计数）。</summary>
    private sealed class ScriptedRandom : IRandomService
    {
        /// <summary>用尽给定序列后返回的默认中值。</summary>
        private const double DefaultFallback = 0.5d;  // arch-guard:allow 随机替身的取值回退（非规则数值副本）

        private readonly Queue<double> _values;

        /// <summary>按给定次序构造。</summary>
        /// <param name="values">依次返回的取值。</param>
        public ScriptedRandom(params double[] values) => _values = new Queue<double>(values);

        /// <summary><see cref="NextDouble"/> 的调用次数。</summary>
        public int DoublesRequested { get; private set; }

        /// <summary><see cref="Next"/> 的调用次数。</summary>
        public int IntsRequested { get; private set; }

        /// <summary>用尽给定序列后返回的值（默认中值）。</summary>
        public double Fallback { get; init; } = DefaultFallback;

        /// <inheritdoc />
        public double NextDouble()
        {
            DoublesRequested++;
            return _values.Count > 0 ? _values.Dequeue() : Fallback;
        }

        /// <inheritdoc />
        public int Next(int minInclusive, int maxExclusive)
        {
            IntsRequested++;
            return minInclusive;
        }

        /// <inheritdoc />
        public void NextBytes(Span<byte> destination) => destination.Clear();
    }
}
