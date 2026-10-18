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
/// T032 / SC-014：查询面必须是**只读投影**——三个入口全部由 ⑦ 投影，调用前后状态逐字段不变、
/// 不消费随机、不落盘、不建视图。
/// </summary>
/// <remarks>
/// 期望值一律经 <c>AchievementCatalog</c> / <c>AchievementPolicy</c> 取得，本文件 MUST NOT 另存
/// 规则数值（SC-008）。
/// </remarks>
public class AchievementQueryTests
{
    private static readonly Guid StateId = Guid.Parse("c4d5e6f7-0a1b-4c2d-9e3f-4a5b6c7d8e9f");

    [Fact]
    public void 查询不消费随机且状态逐字段不变()
    {
        var state = StateOf();
        var random = new CountingRandomService(new SeededRandomService(20261018));
        var engine = new MonthlySettlementEngine(random, new GameStateClock(state), new FixedNameGenerator());

        engine.Settle(state);
        state.Achievements.MarkCompleted(new AchievementMark(
            AchievementId.FoundationLaid, state.Difficulty, state.CurrentDate));

        var requestedBefore = random.DoublesRequested;
        var before = Describe(state);

        Assert.Equal(1, AchievementQuery.CompletedCount(state));
        Assert.True(AchievementQuery.IsCompleted(state, AchievementId.FoundationLaid));
        Assert.Equal(1, AchievementQuery.SeriesProgress(state, AchievementSeries.Emerging).Completed);

        Assert.Equal(requestedBefore, random.DoublesRequested);
        Assert.Equal(before, Describe(state));
    }

    [Fact]
    public void 已达成条数按成就去重与难度无关()
    {
        var state = StateOf();

        // 同一条成就的两个难度各记一次 ⇒ 计数仍为 1（难度是标记的一部分，不是成就的身份）。
        state.Achievements.MarkCompleted(new AchievementMark(
            AchievementId.FoundationLaid, Difficulty.Normal, state.CurrentDate));
        state.Achievements.MarkCompleted(new AchievementMark(
            AchievementId.FoundationLaid, Difficulty.Hell, state.CurrentDate));

        Assert.Equal(2, state.Achievements.Completed.Count);
        Assert.Equal(1, AchievementQuery.CompletedCount(state));
        Assert.True(AchievementQuery.IsCompleted(state, AchievementId.FoundationLaid));
    }

    [Fact]
    public void 系列进度只计本系列且总数为目录口径()
    {
        var state = StateOf();

        state.Achievements.MarkCompleted(new AchievementMark(
            AchievementId.FoundationLaid, Difficulty.Normal, state.CurrentDate));
        state.Achievements.MarkCompleted(new AchievementMark(
            AchievementId.FirstJuRen, Difficulty.Normal, state.CurrentDate));

        var emerging = AchievementQuery.SeriesProgress(state, AchievementSeries.Emerging);
        Assert.Equal(AchievementSeries.Emerging, emerging.Series);
        Assert.Equal(2, emerging.Completed);
        Assert.Equal(AchievementCatalog.CountFor(AchievementSeries.Emerging), emerging.Total);

        // 跨系列不串味：科名累进此刻为零（总数仍为目录口径）。
        var exam = AchievementQuery.SeriesProgress(state, AchievementSeries.ExamProgression);
        Assert.Equal(0, exam.Completed);
        Assert.Equal(AchievementCatalog.CountFor(AchievementSeries.ExamProgression), exam.Total);

        // 七个系列的总数之和恰为 68（目录自身的一致性由 T029 钉死）。
        var total = 0;
        foreach (var definition in AchievementCatalog.All)
        {
            total++;
        }

        Assert.Equal(68, total);
    }

    [Fact]
    public void 未定义的系列抛越界()
    {
        var state = StateOf();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => AchievementQuery.SeriesProgress(state, (AchievementSeries)99));
    }

    [Fact]
    public void 全部由已完成投影而非累计量或队列()
    {
        var state = StateOf();

        state.Achievements.RecordSuccessfulBribes(5);
        state.Achievements.RecordBirths(5);
        state.Achievements.ObserveTotalAssets(Money.FromGuan(500_000m));
        state.Achievements.RecordLoanOriginated();
        state.Achievements.ReportDebugWake(5);

        // 累计量与队列都不构成「已达成」：三个入口必须只读 ⑦。
        Assert.Equal(0, AchievementQuery.CompletedCount(state));
        Assert.False(AchievementQuery.IsCompleted(state, AchievementId.TenThousandGuan));
        Assert.Equal(0, AchievementQuery.SeriesProgress(state, AchievementSeries.Emerging).Completed);
    }

    /// <summary>构造只有一名成年男性成员的状态。</summary>
    private static GameState StateOf()
    {
        var family = RulesHarness.FamilyOf(RulesHarness.Member(1, Gender.Male, 40));
        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(Money.FromGuan(10_000m)),
            new Holdings());

        return new GameState(StateId, RulesHarness.Date, Difficulty.Normal, Origin.Scholar, family, economy);
    }

    /// <summary>把状态面的可观察内容描述成确定性字符串。</summary>
    /// <param name="state">游戏状态。</param>
    private static string Describe(GameState state)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append(state.CurrentDate.Year).Append('-').Append(state.CurrentDate.Month).Append('|')
            .Append(state.Difficulty).Append('|')
            .Append(state.Family.Members.Count).Append('|')
            .Append(state.Economy.Treasury.Cash.Wen).Append('|')
            .Append(state.Achievements.Completed.Count).Append('|')
            .Append(state.Achievements.PendingUnlocks.Count).Append('|')
            .Append(state.Achievements.SuccessfulBribeCount).Append('|')
            .Append(state.Achievements.BirthCount).Append('|')
            .Append(state.Achievements.PeakTotalAssets.Wen).Append('|')
            .Append(state.Achievements.DebugWakeCount);

        return builder.ToString();
    }

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
