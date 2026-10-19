using KFL.Core.Config;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;
using KFL.Rules.FamilyLifecycle;
using KFL.Rules.Settlement;
using Xunit;

namespace KFL.Tests.Rules;

public partial class MarriageTests
{
    /// <summary>娶入：按对方阶层与支出系数结算聘礼（支出）与嫁妆（收入），并留下恰一条托媒费与婚姻事件。</summary>
    /// <param name="counterpartClass">对方阶层。</param>
    /// <param name="difficulty">难度（决定支出系数）。</param>
    [Theory]
    [InlineData(MarriageClass.Commoner, Difficulty.Easy)]
    [InlineData(MarriageClass.Commoner, Difficulty.Normal)]
    [InlineData(MarriageClass.Commoner, Difficulty.Hard)]
    [InlineData(MarriageClass.Commoner, Difficulty.Hell)]
    [InlineData(MarriageClass.Scholar, Difficulty.Easy)]
    [InlineData(MarriageClass.Scholar, Difficulty.Normal)]
    [InlineData(MarriageClass.Scholar, Difficulty.Hard)]
    [InlineData(MarriageClass.Scholar, Difficulty.Hell)]
    [InlineData(MarriageClass.Official, Difficulty.Easy)]
    [InlineData(MarriageClass.Official, Difficulty.Normal)]
    [InlineData(MarriageClass.Official, Difficulty.Hard)]
    [InlineData(MarriageClass.Official, Difficulty.Hell)]
    public void 托媒成婚按对方阶层与支出系数结算聘礼与嫁妆(
        MarriageClass counterpartClass,
        Difficulty difficulty)
    {
        var member = RulesHarness.Member(1, Gender.Male, 20);
        var state = State(difficulty, Origin.Artisan, RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdLow * 2m), false, member);
        var random = new ScriptedRandom(
            SuccessRoll,
            RollIn(counterpartClass, WeightsOf(state)));

        var expectedGift = LifecycleQueries.GiftAmount(state, counterpartClass);
        var expectedDowry = LifecycleQueries.DowryAmount(state, counterpartClass);
        var expectedFee = LifecycleQueries.MatchmakerFee(state);

        var outcome = LifecycleEntry.TryMatchmake(
            state,
            member,
            MarriageDirection.BrideInto,
            random,
            new FixedNameGenerator());

        Assert.True(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.None, outcome.RejectReason);

        Assert.Equal(-expectedFee, TotalOf(state, LedgerCategory.MatchmakerFee));
        Assert.Equal(-expectedGift, TotalOf(state, LedgerCategory.BetrothalGiftPaid));
        Assert.Equal(expectedDowry, TotalOf(state, LedgerCategory.DowryReceived));
        Assert.Equal(1, CountOf(state, LedgerCategory.MatchmakerFee));
        Assert.Equal(1, CountOf(state, LedgerCategory.BetrothalGiftPaid));
        Assert.Equal(1, CountOf(state, LedgerCategory.DowryReceived));
        Assert.Equal(1, CountOf(state, LedgerCategory.MarriageFormed));

        // 双向配偶关系 + 辈分照家族内配偶 + 计口（娶入不归档）
        var spouse = state.Family.SpouseOf(member.Id);
        Assert.NotNull(spouse);
        Assert.Equal(member.Id, spouse.SpouseId);
        Assert.Equal(member.Generation, spouse.Generation);
        Assert.Contains(spouse, state.Family.RegisteredMembers);
        Assert.Contains(spouse, CountedMembers.Counted(state.Family));
        Assert.Equal(expectedFee, outcome.Amount);
    }

    /// <summary>配偶按「无父母参照者」口径初始化（FR-014），并冠家族姓。</summary>
    [Fact]
    public void 托媒所得配偶落无父母参照者口径并冠家族姓()
    {
        var member = RulesHarness.Member(1, Gender.Male, 20);
        var state = State(Difficulty.Normal, Origin.Artisan, RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdLow * 2m), false, member);
        var random = new ScriptedRandom(SuccessRoll, RollIn(MarriageClass.Commoner, WeightsOf(state)));

        var outcome = LifecycleEntry.TryMatchmake(
            state,
            member,
            MarriageDirection.BrideInto,
            random,
            new FixedNameGenerator());

        Assert.True(outcome.Accepted);

        var spouse = state.Family.SpouseOf(member.Id);
        Assert.NotNull(spouse);

        Assert.Null(spouse.FatherId);
        Assert.Null(spouse.MotherId);
        Assert.StartsWith(state.Family.Name, spouse.Name, StringComparison.Ordinal);
        Assert.Equal(Gender.Female, spouse.Gender);
        Assert.True(spouse.Lifespan > 0);
        Assert.InRange(spouse.Study, AttributeLimits.Min, AttributeLimits.Max);
        Assert.InRange(spouse.Health, AttributeLimits.Min, AttributeLimits.Max);
        Assert.InRange(spouse.Talents.Agriculture, AttributeLimits.Min, AttributeLimits.Max);
        Assert.InRange(spouse.Talents.Commerce, AttributeLimits.Min, AttributeLimits.Max);
        Assert.InRange(spouse.Talents.Officialdom, AttributeLimits.Min, AttributeLimits.Max);
        Assert.InRange(spouse.Talents.Craft, AttributeLimits.Min, AttributeLimits.Max);
        Assert.Equal(DegreeLevel.BaiShen, spouse.CurrentDegree);

        var ageAtMarriage = spouse.AgeAt(state.CurrentDate);
        Assert.True(AgeBracketPolicy.IsAdult(spouse.Gender, ageAtMarriage));
        Assert.True(
            Math.Abs(ageAtMarriage - member.AgeAt(state.CurrentDate)) <= MarriagePolicy.CandidateAgeSpread);

        // 成功路径的随机消费：恰 1 次成功骰 + 1 次阶层骰 + 1 次年龄 + 4×2 + 2×3 次属性
        Assert.Equal(1, random.IntsRequested);
        Assert.Equal(2 + (4 * 2) + (2 * 3), random.DoublesRequested);
    }

    // ------------------------------------------------------------------ 托媒的拒绝与不退费

    /// <summary>成功判定失败：托媒费照扣、其余状态一律不变（契约十九 §3 ⑤）。</summary>
    [Fact]
    public void 托媒成功判定失败时不退费且不改动状态()
    {
        var member = RulesHarness.Member(1, Gender.Male, 20);
        var state = State(Difficulty.Normal, Origin.Artisan, RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdLow * 2m), false, member);
        var random = new ScriptedRandom((double)MarriagePolicy.MatchmakerSuccessChance);
        var fee = LifecycleQueries.MatchmakerFee(state);
        var cashBefore = state.Economy.Treasury.Cash;
        var entriesBefore = state.Economy.Ledger.Entries.Count;

        var outcome = LifecycleEntry.TryMatchmake(
            state,
            member,
            MarriageDirection.BrideInto,
            random,
            new FixedNameGenerator());

        Assert.False(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.None, outcome.RejectReason);
        Assert.Equal(fee, outcome.Amount);
        Assert.Equal(1, random.DoublesRequested);
        Assert.Equal(0, random.IntsRequested);

        Assert.Equal(cashBefore - fee, state.Economy.Treasury.Cash);
        Assert.Equal(entriesBefore + 1, state.Economy.Ledger.Entries.Count);
        Assert.Equal(-fee, TotalOf(state, LedgerCategory.MatchmakerFee));
        Assert.Equal(0, CountOf(state, LedgerCategory.BetrothalGiftPaid));
        Assert.Equal(0, CountOf(state, LedgerCategory.DowryReceived));
        Assert.Equal(0, CountOf(state, LedgerCategory.MarriageFormed));

        Assert.Null(member.SpouseId);
        Assert.Null(state.Family.Lifecycle.Proposal);
        Assert.Single(state.Family.RegisteredMembers);
    }

    /// <summary>现银 + 储蓄不足：拒绝、0 次随机、不转贷款、不落 0 金额条目。</summary>
    [Fact]
    public void 托媒资金不足时拒绝且不消费随机()
    {
        var member = RulesHarness.Member(1, Gender.Male, 20);
        var state = State(Difficulty.Normal, Origin.Artisan, Money.Zero, false, member);
        var random = new ScriptedRandom();
        var entriesBefore = state.Economy.Ledger.Entries.Count;

        var outcome = LifecycleEntry.TryMatchmake(
            state,
            member,
            MarriageDirection.BrideInto,
            random,
            new FixedNameGenerator());

        Assert.False(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.InsufficientFunds, outcome.RejectReason);
        Assert.Equal(0, random.DoublesRequested);
        Assert.Equal(0, random.IntsRequested);

        Assert.Equal(Money.Zero, state.Economy.Treasury.Loan.Total);
        Assert.Equal(entriesBefore, state.Economy.Ledger.Entries.Count);
        Assert.Equal(Money.Zero, outcome.Amount);
        Assert.Equal(Money.Zero, outcome.FromCash);
        Assert.Equal(Money.Zero, outcome.FromSavings);
        Assert.Null(outcome.Entry);
        Assert.Null(member.SpouseId);
        Assert.Single(state.Family.RegisteredMembers);
    }

    /// <summary>未成年、方向不符、已婚后均不可托媒（且 0 次随机）。</summary>
    [Theory]
    [InlineData(MarriageDirection.BrideInto)]
    [InlineData(MarriageDirection.GroomOut)]
    public void 未成年成员不可托媒(MarriageDirection direction)
    {
        var boy = RulesHarness.Member(1, Gender.Male, AgeBracketPolicy.MaleAdulthoodAge - 1);
        var state = State(Difficulty.Normal, Origin.Artisan, RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdLow * 2m), false, boy);
        var random = new ScriptedRandom();

        var outcome = LifecycleEntry.TryMatchmake(state, boy, direction, random, new FixedNameGenerator());

        Assert.False(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.NotMarriageable, outcome.RejectReason);
        Assert.Equal(0, random.DoublesRequested);
        Assert.Equal(0, random.IntsRequested);
    }

    /// <summary>方向与性别不符时拒绝（男 ⇒ 娶入、女 ⇒ 嫁出）。</summary>
    [Fact]
    public void 方向与性别不符时拒绝()
    {
        var woman = RulesHarness.Member(1, Gender.Female, 20);
        var state = State(Difficulty.Normal, Origin.Artisan, RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdLow * 2m), false, woman);
        var random = new ScriptedRandom();

        var outcome = LifecycleEntry.TryMatchmake(state, woman, MarriageDirection.BrideInto, random, new FixedNameGenerator());

        Assert.False(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.NotMarriageable, outcome.RejectReason);
        Assert.Equal(0, random.DoublesRequested);
    }

    /// <summary>已有未应答提议时同一成员不可再托媒（契约十九 §2 第 1 条末项）。</summary>
    [Fact]
    public void 已有未应答提议时不可再托媒()
    {
        var member = RulesHarness.Member(1, Gender.Male, 20);
        var state = State(Difficulty.Normal, Origin.Artisan, RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdLow * 2m), false, member);

        var market = MarriageMarket.Run(
            state,
            new ScriptedRandom((double)MarriagePolicy.ProposalBaseChance / 2d),
            new FixedNameGenerator());
        Assert.NotNull(market.PendingProposal);
        Assert.NotNull(state.Family.Lifecycle.Proposal);

        var random = new ScriptedRandom();
        var outcome = LifecycleEntry.TryMatchmake(
            state,
            member,
            MarriageDirection.BrideInto,
            random,
            new FixedNameGenerator());

        Assert.False(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.NotMarriageable, outcome.RejectReason);
        Assert.Equal(0, random.DoublesRequested);
        Assert.Equal(0, random.IntsRequested);
    }

    /// <summary>FR-013：同一成员同一月至多一次——首次失败也占用名额，再次托媒被拒且 0 次随机、0 次支付。</summary>
    [Fact]
    public void 同一成员同月再次托媒被拒且不消费随机()
    {
        var member = RulesHarness.Member(1, Gender.Male, 20);
        var state = State(Difficulty.Normal, Origin.Artisan, RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdLow * 2m), false, member);

        // 首次：成功判定失败（托媒费照扣 ⇒ 当月名额已用）。
        var first = new ScriptedRandom((double)MarriagePolicy.MatchmakerSuccessChance);
        var firstOutcome = LifecycleEntry.TryMatchmake(
            state, member, MarriageDirection.BrideInto, first, new FixedNameGenerator());

        Assert.False(firstOutcome.Accepted);
        Assert.Equal(LifecycleRejectReason.None, firstOutcome.RejectReason);
        Assert.True(state.Family.Lifecycle.HasAppliedForMatchmaking(member.Id, state.CurrentDate));
        Assert.Single(state.Family.Lifecycle.MatchmakeApplications);

        var cashBefore = state.Economy.Treasury.Cash;
        var entriesBefore = state.Economy.Ledger.Entries.Count;

        var second = new ScriptedRandom();
        var secondOutcome = LifecycleEntry.TryMatchmake(
            state, member, MarriageDirection.BrideInto, second, new FixedNameGenerator());

        Assert.False(secondOutcome.Accepted);
        Assert.Equal(LifecycleRejectReason.AlreadyAppliedThisMonth, secondOutcome.RejectReason);
        Assert.Equal(0, second.DoublesRequested);
        Assert.Equal(0, second.IntsRequested);
        Assert.Equal(Money.Zero, secondOutcome.Amount);
        Assert.Equal(cashBefore, state.Economy.Treasury.Cash);
        Assert.Equal(entriesBefore, state.Economy.Ledger.Entries.Count);
        Assert.Null(member.SpouseId);

        // 查询面同口径：灰置按钮复用同一个拒绝原因（FR-045）。
        var feasibility = LifecycleQueries.MatchmakeFeasibility(state, member, MarriageDirection.BrideInto);
        Assert.False(feasibility.Available);
        Assert.Equal(LifecycleRejectReason.AlreadyAppliedThisMonth, feasibility.RejectReason);
    }

    /// <summary>FR-013：失败后下月可再试——名额按月判定，旧月记录跨月失效。</summary>
    [Fact]
    public void 托媒名额跨月释放()
    {
        var member = RulesHarness.Member(1, Gender.Male, 20);
        var state = State(Difficulty.Normal, Origin.Artisan, RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdLow * 2m), false, member);

        var first = new ScriptedRandom((double)MarriagePolicy.MatchmakerSuccessChance);
        Assert.False(LifecycleEntry.TryMatchmake(
            state, member, MarriageDirection.BrideInto, first, new FixedNameGenerator()).Accepted);
        var appliedAt = state.CurrentDate;

        state.AdvanceMonth();

        // 次月 ⑧-a 先跑：清掉旧月名额记录（本用例走提亲未命中路径，无副作用）。
        MarriageMarket.Run(state, new ScriptedRandom(), new FixedNameGenerator());
        Assert.DoesNotContain(
            state.Family.Lifecycle.MatchmakeApplications,
            application => application.AppliedAt == appliedAt);

        var feasibility = LifecycleQueries.MatchmakeFeasibility(state, member, MarriageDirection.BrideInto);
        Assert.True(feasibility.Available);
        Assert.Equal(LifecycleRejectReason.None, feasibility.RejectReason);

        var second = new ScriptedRandom(SuccessRoll, RollIn(MarriageClass.Commoner, WeightsOf(state)));
        var outcome = LifecycleEntry.TryMatchmake(
            state, member, MarriageDirection.BrideInto, second, new FixedNameGenerator());

        Assert.True(outcome.Accepted);
        Assert.True(state.Family.Lifecycle.HasAppliedForMatchmaking(member.Id, state.CurrentDate));
    }

    /// <summary>FR-013：付款失败（拒绝）时不占名额——记录只在扣款成功后写入。</summary>
    [Fact]
    public void 托媒付款失败时不占当月名额()
    {
        var member = RulesHarness.Member(1, Gender.Male, 20);
        var state = State(Difficulty.Normal, Origin.Artisan, Money.Zero, false, member);

        var outcome = LifecycleEntry.TryMatchmake(
            state, member, MarriageDirection.BrideInto, new ScriptedRandom(), new FixedNameGenerator());

        Assert.Equal(LifecycleRejectReason.InsufficientFunds, outcome.RejectReason);
        Assert.Empty(state.Family.Lifecycle.MatchmakeApplications);
    }

    // ------------------------------------------------------------------ 提亲（⑧-a 后半）
}
