using System.Globalization;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Services;
using KFL.Rules.Achievements;
using KFL.Rules.Config;
using KFL.Rules.Settlement;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// T033 / SC-003：成就判定的确定性——同种子 + 同初始状态 + 同月份序列 ⇒ 8 个持久字段与解锁序列
/// **逐位相同**；判定路径**不消费随机**；结果**不依赖区域设置**（也无时钟 / 文件面）。
/// </summary>
/// <remarks>
/// 断言一律是**相对**形式（两遍运行的互比），不复制规则数值（SC-008）。
/// </remarks>
public class AchievementDeterminismTests
{
    private static readonly Guid StateId = Guid.Parse("f0e1d2c3-b4a5-4968-8778-99aabbccddee");

    /// <summary>固定种子：本文件断言的是「同种子 → 同结果」，取值本身不重要。</summary>
    private const int Seed = 20261018;

    /// <summary>复跑月数：跨两个年度，覆盖 1 月利率 roll 与年末项。</summary>
    private const int RunMonths = 24;

    [Fact]
    public void 同种子两遍运行的成就状态与解锁序列逐位相同()
    {
        var first = Run(Seed, adjudicate: true);
        var second = Run(Seed, adjudicate: true);

        Assert.Equal(first.Unlocked, second.Unlocked);
        Assert.Equal(Describe(first.State.Achievements), Describe(second.State.Achievements));

        // 非空转：判定确实解锁过（ACH-001 开基立业恒成立）。
        Assert.NotEmpty(first.Unlocked);
    }

    [Fact]
    public void 判定路径不消费随机()
    {
        var withoutJudging = Run(Seed, adjudicate: false);
        var withJudging = Run(Seed, adjudicate: true);

        // 相对断言：判定路径的随机消费数 MUST 恰为 0（否则两遍的逐月结算序列会错位）。
        Assert.Equal(withoutJudging.DoublesRequested, withJudging.DoublesRequested);
        Assert.Equal(withoutJudging.SettlementCount, withJudging.SettlementCount);
    }

    [Fact]
    public void 判定结果不依赖区域设置()
    {
        var original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var invariant = Run(Seed, adjudicate: true);

            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var german = Run(Seed, adjudicate: true);

            Assert.Equal(Describe(invariant.State.Achievements), Describe(german.State.Achievements));
            Assert.Equal(invariant.Unlocked, german.Unlocked);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>跑一遍完整序列；<paramref name="adjudicate"/> 决定每月是否追加回合末判定。</summary>
    /// <param name="seed">随机种子。</param>
    /// <param name="adjudicate">是否在每次结算后调用判定器。</param>
    private static RunRecord Run(int seed, bool adjudicate)
    {
        var head = RulesHarness.Member(1, Gender.Male, 30, agriculture: 60);
        var spouse = RulesHarness.Member(2, Gender.Female, 28);
        var child = RulesHarness.Member(3, Gender.Male, 6);

        // 夹具成员的体质默认 0：007 起 ⑧-c 会让 0 体质者当月归零身故，链路会断在第一个月。
        head.Health = (int)AttributePolicy.HealthMean;
        spouse.Health = (int)AttributePolicy.HealthMean;
        child.Health = (int)AttributePolicy.HealthMean;

        var family = RulesHarness.FamilyOf(head, spouse, child);
        family.Marry(head.Id, spouse.Id);

        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(Money.FromGuan(50_000m)),
            new Holdings { FarmlandMu = 20 });

        var state = new GameState(
            StateId, new GameDate(79, 1), Difficulty.Normal, Origin.Farmer, family, economy);
        var random = new CountingRandomService(new SeededRandomService(seed));
        var engine = new MonthlySettlementEngine(random, new GameStateClock(state), new FixedNameGenerator());
        var unlocked = new List<AchievementId>();
        var settlements = 0;

        for (var index = 0; index < RunMonths; index++)
        {
            var settlement = engine.Settle(state);
            settlements++;

            if (adjudicate)
            {
                foreach (var id in AchievementAdjudicator.Judge(state, settlement, [], []).NewlyUnlocked)
                {
                    unlocked.Add(id);
                }
            }
        }

        return new RunRecord(state, unlocked, random.DoublesRequested, settlements);
    }

    /// <summary>把 8 个持久字段描述成确定性字符串。</summary>
    /// <param name="achievements">成就状态。</param>
    private static string Describe(AchievementState achievements)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append(achievements.SuccessfulBribeCount).Append('|')
            .Append(achievements.BirthCount).Append('|')
            .Append(achievements.PeakTotalAssets.Wen).Append('|')
            .Append(achievements.EverHadLoan).Append('|')
            .Append(achievements.DebugWakeCount).Append('|');

        foreach (var age in achievements.Lifespans)
        {
            builder.Append(age).Append(',');
        }

        builder.Append('|');

        foreach (var mark in achievements.Completed)
        {
            builder.Append(mark.Achievement).Append('@')
                .Append(mark.Difficulty).Append('@')
                .Append(mark.CompletedAt.Year).Append('-').Append(mark.CompletedAt.Month).Append(';');
        }

        builder.Append('|');

        foreach (var pending in achievements.PendingUnlocks)
        {
            builder.Append(pending.Achievement).Append('@')
                .Append(pending.Difficulty).Append('@')
                .Append(pending.UnlockedAt.Year).Append('-').Append(pending.UnlockedAt.Month).Append('@')
                .Append(pending.SourceSaveId).Append(';');
        }

        return builder.ToString();
    }

    /// <summary>一次运行的产物。</summary>
    /// <param name="State">收尾状态。</param>
    /// <param name="Unlocked">按次序累积的解锁项。</param>
    /// <param name="DoublesRequested">结算路径的 <c>NextDouble()</c> 请求总数。</param>
    /// <param name="SettlementCount">结算次数。</param>
    private sealed record RunRecord(
        GameState State,
        List<AchievementId> Unlocked,
        int DoublesRequested,
        int SettlementCount);

    /// <summary>包一层计数：只统计 <see cref="NextDouble"/> 的调用次数。</summary>
    private sealed class CountingRandomService : IRandomService
    {
        private readonly IRandomService _inner;

        /// <summary>用内层替身构造。</summary>
        /// <param name="inner">取值来源。</param>
        public CountingRandomService(IRandomService inner) => _inner = inner;

        /// <summary>至今被请求的 <see cref="NextDouble"/> 次数。</summary>
        public int DoublesRequested { get; private set; }

        /// <inheritdoc />
        public double NextDouble()
        {
            DoublesRequested++;
            return _inner.NextDouble();
        }

        /// <inheritdoc />
        public int Next(int minInclusive, int maxExclusive) => _inner.Next(minInclusive, maxExclusive);

        /// <inheritdoc />
        public void NextBytes(Span<byte> destination) => _inner.NextBytes(destination);
    }
}
