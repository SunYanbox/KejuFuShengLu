using KFL.Core.Config;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;
using KFL.Rules.Economy;
using KFL.Rules.FamilyLifecycle;
using KFL.Rules.Settlement;
using Xunit;

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
public class MarriageTests
{
    private static readonly Guid StateId = Guid.Parse("2f5c8a10-9d47-4b6e-8f31-7a2b6c4d5e70");

    // ------------------------------------------------------------------ 婚嫁金额表 24 格

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

    // ------------------------------------------------------------------ 提亲（⑧-a 后半）

    /// <summary>提亲概率 = 基础概率 × 负面事件系数（四难度 2.5% / 5% / 6.5% / 7%）。</summary>
    /// <param name="difficulty">难度。</param>
    [Theory]
    [InlineData(Difficulty.Easy)]
    [InlineData(Difficulty.Normal)]
    [InlineData(Difficulty.Hard)]
    [InlineData(Difficulty.Hell)]
    public void 提亲概率为基础概率乘负面事件系数(Difficulty difficulty)
    {
        Assert.Equal(
            MarriagePolicy.ProposalBaseChance * DifficultyRates.NegativeEventFactor(difficulty),
            MarriageMarket.ProposalChance(difficulty));
    }

    /// <summary>每月恰掷一次提亲骰：未命中时不出提议、也不产生任何成员或账本变化。</summary>
    [Fact]
    public void 提亲每月恰掷一次概率骰()
    {
        var member = RulesHarness.Member(1, Gender.Male, 20);
        var state = State(Difficulty.Normal, Origin.Artisan, RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdLow * 2m), false, member);
        var random = new ScriptedRandom();

        var market = MarriageMarket.Run(state, random, new FixedNameGenerator());

        Assert.Null(market.ImperialProposal);
        Assert.Null(market.MatchmakerProposal);
        Assert.Equal(1, random.DoublesRequested);
        Assert.Equal(0, random.IntsRequested);
        Assert.Null(state.Family.Lifecycle.Proposal);
        Assert.Single(state.Family.RegisteredMembers);
        Assert.Empty(state.Economy.Ledger.Entries);
    }

    // ------------------------------------------------------------------ 阶层抽样（三段资产档）

    /// <summary>三段资产档的阶层权重与区间：每档逐段落在对应的阶层上。</summary>
    /// <param name="tier">资产档下标（0 贫 / 1 中 / 2 富）。</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void 阶层抽样按资产档逐段落位(int tier)
    {
        var member = RulesHarness.Member(1, Gender.Male, 20);
        var cash = tier switch
        {
            0 => MarriagePolicy.ClassWealthThresholdLow / 2m,
            1 => MarriagePolicy.ClassWealthThresholdLow * 2m,
            _ => MarriagePolicy.ClassWealthThresholdHighGuan * 2m,
        };
        var state = State(Difficulty.Normal, Origin.Artisan, RulesHarness.Guan(cash), false, member);
        var weights = WeightsOf(state);

        Assert.Equal(Enum.GetValues<MarriageClass>().Length, weights.Count);

        var lower = 0d;
        for (var index = 0; index < weights.Count; index++)
        {
            var (marriageClass, segmentLower, segmentUpper) = MarriageMarket.Segment(
                state,
                lower + ((double)weights[index] / 2d));

            Assert.Equal((MarriageClass)index, marriageClass);
            Assert.Equal(lower, segmentLower, 10);
            Assert.Equal(lower + (double)weights[index], segmentUpper, 10);
            lower += (double)weights[index];
        }

        Assert.Equal(1d, lower, 10);
    }

    // ------------------------------------------------------------------ 捉婿（⑧-a 前半）

    /// <summary>仕途压力权重：两端取配置值、随官阶单调下降、且逐档等差（线性）。</summary>
    [Fact]
    public void 仕途压力权重两端单调且线性()
    {
        Assert.Equal(MarriagePolicy.PressureWeightTop, OfficialPressure.WeightOf(MarriagePolicy.PressureHighestLevel));
        Assert.Equal(MarriagePolicy.PressureWeightBottom, OfficialPressure.WeightOf(MarriagePolicy.PressureLowestLevel));

        var step = OfficialPressure.WeightOf(MarriagePolicy.PressureHighestLevel)
            - OfficialPressure.WeightOf(MarriagePolicy.PressureHighestLevel + 1);

        for (var level = MarriagePolicy.PressureHighestLevel; level < MarriagePolicy.PressureLowestLevel; level++)
        {
            Assert.True(OfficialPressure.WeightOf(level) > OfficialPressure.WeightOf(level + 1));

            // 等差：小数除法在同一精度口径下比较（末位舍入差 ≤ 1e-20）
            Assert.Equal(step, OfficialPressure.WeightOf(level) - OfficialPressure.WeightOf(level + 1), 20);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            () => OfficialPressure.WeightOf(MarriagePolicy.PressureHighestLevel - 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => OfficialPressure.WeightOf(MarriagePolicy.PressureLowestLevel + 1));
    }

    /// <summary>压力 = 在册且有官者的权重之和；无官、已亡、外嫁与白身不计。</summary>
    [Fact]
    public void 仕途压力只累计在册有官者()
    {
        var head = RulesHarness.Official(1, MarriagePolicy.PressureLowestLevel);
        var second = RulesHarness.Official(2, MarriagePolicy.PressureLowestLevel);
        var family = RulesHarness.FamilyOf(head, second);

        Assert.Equal(
            OfficialPressure.WeightOf(MarriagePolicy.PressureLowestLevel) * 2m,
            OfficialPressure.Compute(family));

        // 卸任 ⇒ 不再计入
        second.Rank = null;
        Assert.Equal(
            OfficialPressure.WeightOf(MarriagePolicy.PressureLowestLevel),
            OfficialPressure.Compute(family));

        // 外嫁 ⇒ 不再计入（即使仍有官）
        second.Rank = new OfficialRank(MarriagePolicy.PressureLowestLevel);
        second.Status |= StatusFlag.MarriedOut;
        Assert.Equal(
            OfficialPressure.WeightOf(MarriagePolicy.PressureLowestLevel),
            OfficialPressure.Compute(family));

        // 已亡 ⇒ 不再计入
        head.Status |= StatusFlag.Deceased;
        Assert.Equal(0m, OfficialPressure.Compute(family));
    }

    /// <summary>捉婿概率 = 基础概率 × 负面事件系数 ÷ (1 + P)：随压力严格下降，P = 0 时等于基础概率乘系数。</summary>
    /// <param name="difficulty">难度。</param>
    [Theory]
    [InlineData(Difficulty.Easy)]
    [InlineData(Difficulty.Normal)]
    [InlineData(Difficulty.Hard)]
    [InlineData(Difficulty.Hell)]
    public void 捉婿概率随压力下降(Difficulty difficulty)
    {
        Assert.Equal(
            MarriagePolicy.ImperialProposalBaseChance * DifficultyRates.NegativeEventFactor(difficulty) / (1m + 0m),
            MarriageMarket.ImperialChance(difficulty, 0m));
        Assert.True(
            MarriageMarket.ImperialChance(difficulty, 0m)
            > MarriageMarket.ImperialChance(difficulty, 1m));
        Assert.True(
            MarriageMarket.ImperialChance(difficulty, 1m)
            > MarriageMarket.ImperialChance(difficulty, 2m));
    }

    /// <summary>捉婿支付金额 = 500~1000 贯 × (1 + P)，随压力严格上升。</summary>
    [Fact]
    public void 捉婿金额随压力上升()
    {
        var random = new ScriptedRandom();

        var low = MarriageMarket.DrawImperialPayment(random, 0m);
        var middle = MarriageMarket.DrawImperialPayment(random, 1m);
        var high = MarriageMarket.DrawImperialPayment(random, 2m);

        Assert.True(low < middle);
        Assert.True(middle < high);
        Assert.True(!(low < Money.FromGuan(MarriagePolicy.ImperialPaymentMinGuan)));
        Assert.True(!(high < low));
    }

    /// <summary>捉婿配偶的功名分档：P &lt; 1 白身、1 ≤ P &lt; 3 举人、P ≥ 3 贡士，且永不为进士。</summary>
    [Fact]
    public void 捉婿配偶功名分档不含进士()
    {
        var epsilon = MarriagePolicy.PressureWeightBottom;

        Assert.Equal(
            DegreeLevel.BaiShen,
            MarriageMarket.ImperialDegreeFor(MarriagePolicy.SpouseDegreeScholarThreshold - epsilon));
        Assert.Equal(
            DegreeLevel.JuRen,
            MarriageMarket.ImperialDegreeFor(MarriagePolicy.SpouseDegreeScholarThreshold));
        Assert.Equal(
            DegreeLevel.JuRen,
            MarriageMarket.ImperialDegreeFor(MarriagePolicy.SpouseDegreeTributeThreshold - epsilon));
        Assert.Equal(
            DegreeLevel.GongShi,
            MarriageMarket.ImperialDegreeFor(MarriagePolicy.SpouseDegreeTributeThreshold));
        Assert.Equal(
            DegreeLevel.GongShi,
            MarriageMarket.ImperialDegreeFor(MarriagePolicy.SpouseDegreeTributeThreshold * 10m));
    }

    /// <summary>上月新科一甲 ⇒ 次月可能被捉婿；提议由对方支付，且 MUST NOT 叠加基础聘礼/嫁妆。</summary>
    [Fact]
    public void 捉婿命中时由对方支付且应答只净收一条()
    {
        var member = FirstClassGraduate();
        var state = State(Difficulty.Normal, Origin.Artisan, RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdLow * 2m), false, member);

        var market = MarriageMarket.Run(
            state,
            new ScriptedRandom((double)MarriagePolicy.PressureWeightBottom),
            new FixedNameGenerator());

        Assert.NotNull(market.ImperialProposal);
        var proposal = state.Family.Lifecycle.Proposal;
        Assert.NotNull(proposal);
        Assert.Equal(MarriageProposalKind.ImperialProposal, proposal.Kind);
        Assert.Equal(member.Id, proposal.OurMemberId);
        Assert.Equal(Money.Zero, proposal.BetrothalGift);
        Assert.True(proposal.Dowry.IsPositive);
        Assert.Equal(state.CurrentDate, proposal.ProposedAt);

        var outcome = LifecycleEntry.RespondToProposal(state, true, new ScriptedRandom(), new FixedNameGenerator());

        Assert.True(outcome.Accepted);
        Assert.Equal(proposal.Dowry, TotalOf(state, LedgerCategory.DowryReceived));
        Assert.Equal(1, CountOf(state, LedgerCategory.DowryReceived));
        Assert.Equal(0, CountOf(state, LedgerCategory.BetrothalGiftPaid));
        Assert.Equal(0, CountOf(state, LedgerCategory.DowryPaid));
        Assert.Equal(0, CountOf(state, LedgerCategory.BetrothalGiftReceived));
        Assert.Equal(1, CountOf(state, LedgerCategory.MarriageFormed));
        Assert.Null(state.Family.Lifecycle.Proposal);

        var spouse = state.Family.SpouseOf(member.Id);
        Assert.NotNull(spouse);
        Assert.Equal(Money.Zero, outcome.Amount);
        Assert.Equal(proposal.CandidateName, spouse.Name);
        Assert.Equal(proposal.CandidateLifespan, spouse.Lifespan);
    }

    /// <summary>R-25：提亲与捉婿同月都掷，提亲命中 MUST NOT 覆盖捉婿提议。</summary>
    [Fact]
    public void 提亲命中不覆盖同月捉婿提议()
    {
        var member = FirstClassGraduate();
        var state = State(Difficulty.Normal, Origin.Artisan, RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdLow * 2m), false, member);

        var market = MarriageMarket.Run(
            state,
            new ScriptedRandom((double)MarriagePolicy.PressureWeightBottom)
            {
                // 候选者属性骰与提亲骰都用同一个低位值：捉婿命中、提亲也命中（R-25 的前置）
                Fallback = (double)MarriagePolicy.PressureWeightBottom,
            },
            new FixedNameGenerator());

        Assert.NotNull(market.ImperialProposal);
        Assert.NotNull(market.MatchmakerProposal);
        Assert.Equal(MarriageProposalKind.ImperialProposal, market.PendingProposal!.Kind);

        var proposal = state.Family.Lifecycle.Proposal;
        Assert.NotNull(proposal);
        Assert.Equal(MarriageProposalKind.ImperialProposal, proposal.Kind);
        Assert.Equal(market.ImperialProposal, proposal);
    }

    /// <summary>拒绝提议：0 随机、0 状态变更，但该科名额已消耗（次月不再重掷）。</summary>
    [Fact]
    public void 拒绝捉婿提议后名额已消耗()
    {
        var member = FirstClassGraduate();
        var state = State(Difficulty.Normal, Origin.Artisan, RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdLow * 2m), false, member);

        var market = MarriageMarket.Run(
            state,
            new ScriptedRandom((double)MarriagePolicy.PressureWeightBottom),
            new FixedNameGenerator());
        Assert.NotNull(market.ImperialProposal);

        var entriesBefore = state.Economy.Ledger.Entries.Count;
        var random = new ScriptedRandom();
        var decline = LifecycleEntry.RespondToProposal(state, false, random, new FixedNameGenerator());

        Assert.False(decline.Accepted);
        Assert.Equal(LifecycleRejectReason.None, decline.RejectReason);
        Assert.Equal(0, random.DoublesRequested);
        Assert.Equal(0, random.IntsRequested);
        Assert.Null(state.Family.Lifecycle.Proposal);
        Assert.Null(member.SpouseId);
        Assert.Equal(entriesBefore, state.Economy.Ledger.Entries.Count);

        // 次月：窗口已过（一甲为上月前一个月）⇒ 不再捉婿
        state.AdvanceMonth();
        var next = MarriageMarket.Run(
            state,
            new ScriptedRandom((double)MarriagePolicy.PressureWeightBottom),
            new FixedNameGenerator());

        Assert.Null(next.ImperialProposal);
        Assert.Equal(entriesBefore, state.Economy.Ledger.Entries.Count);
        Assert.Null(member.SpouseId);
    }

    /// <summary>无提议可应答时返回 <see cref="LifecycleRejectReason.NoPendingProposal"/> 且不消费随机。</summary>
    [Fact]
    public void 无提议时不可应答()
    {
        var member = RulesHarness.Member(1, Gender.Male, 20);
        var state = State(Difficulty.Normal, Origin.Artisan, RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdLow * 2m), false, member);
        var random = new ScriptedRandom();

        var outcome = LifecycleEntry.RespondToProposal(state, true, random, new FixedNameGenerator());

        Assert.False(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.NoPendingProposal, outcome.RejectReason);
        Assert.Equal(0, random.DoublesRequested);
        Assert.Equal(0, random.IntsRequested);
    }

    // ------------------------------------------------------------------ 买功名婚姻（§9.3）

    /// <summary>买功名婚姻（娶入）：额外支付自己年俸 150%，配偶低一级功名。</summary>
    /// <param name="memberDegree">我方成员当前功名。</param>
    /// <param name="expectedSpouseDegree">配偶应有的功名。</param>
    [Theory]
    [InlineData(DegreeLevel.JinShi, DegreeLevel.GongShi)]
    [InlineData(DegreeLevel.GongShi, DegreeLevel.JuRen)]
    public void 买功名婚姻使配偶低一级功名(DegreeLevel memberDegree, DegreeLevel expectedSpouseDegree)
    {
        var member = RulesHarness.Official(1, MarriagePolicy.PressureHighestLevel, age: 30);
        member.AppendDegree(new DegreeRecord(memberDegree, null, RulesHarness.Date, DegreeChangeCause.Initial));
        var state = State(
            Difficulty.Normal,
            Origin.Artisan,
            RulesHarness.Guan(SalaryTable.AnnualSalaryGuan(MarriagePolicy.PressureHighestLevel) * 4m),
            true,
            member);

        var random = new ScriptedRandom(SuccessRoll, RollIn(MarriageClass.Commoner, WeightsOf(state)));
        var expectedFee = LifecycleQueries.DegreeMarriageFee(state, member);

        var outcome = LifecycleEntry.TryMatchmake(
            state,
            member,
            MarriageDirection.BrideInto,
            random,
            new FixedNameGenerator(),
            buyDegreeMarriage: true);

        Assert.True(outcome.Accepted);
        Assert.True(expectedFee.IsPositive);
        Assert.Equal(-expectedFee, TotalOf(state, LedgerCategory.DegreeMarriageFeePaid));
        Assert.Equal(1, CountOf(state, LedgerCategory.DegreeMarriageFeePaid));

        var spouse = state.Family.SpouseOf(member.Id);
        Assert.NotNull(spouse);
        Assert.Equal(expectedSpouseDegree, spouse.CurrentDegree);
        Assert.Single(spouse.DegreeHistory);
        Assert.Equal(DegreeChangeCause.Initial, spouse.DegreeHistory[0].Cause);
    }

    /// <summary>举人 / 白身无更低档 ⇒ 买功名选项灰置：纯拒绝、不扣费、0 次随机。</summary>
    /// <param name="memberDegree">我方成员当前功名。</param>
    [Theory]
    [InlineData(DegreeLevel.BaiShen)]
    [InlineData(DegreeLevel.JuRen)]
    public void 无更低功名时买功名选项灰置(DegreeLevel memberDegree)
    {
        var member = RulesHarness.Official(1, MarriagePolicy.PressureHighestLevel, age: 30);
        if (memberDegree != DegreeLevel.BaiShen)
        {
            member.AppendDegree(new DegreeRecord(memberDegree, null, RulesHarness.Date, DegreeChangeCause.Initial));
        }

        var state = State(
            Difficulty.Normal,
            Origin.Artisan,
            RulesHarness.Guan(SalaryTable.AnnualSalaryGuan(MarriagePolicy.PressureHighestLevel) * 4m),
            true,
            member);
        var random = new ScriptedRandom();

        var outcome = LifecycleEntry.TryMatchmake(
            state,
            member,
            MarriageDirection.BrideInto,
            random,
            new FixedNameGenerator(),
            buyDegreeMarriage: true);

        Assert.False(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.NoLowerDegree, outcome.RejectReason);
        Assert.Equal(Money.Zero, outcome.Amount);
        Assert.Null(outcome.Entry);
        Assert.Equal(0, random.DoublesRequested);
        Assert.Equal(0, random.IntsRequested);
        Assert.Empty(state.Economy.Ledger.Entries);

        var feasibility = LifecycleQueries.MatchmakeFeasibility(state, member, MarriageDirection.BrideInto);
        Assert.False(feasibility.DegreeMarriageAvailable);
        Assert.Equal(LifecycleRejectReason.NoLowerDegree, feasibility.DegreeMarriageRejectReason);
    }

    // ------------------------------------------------------------------ 嫁出（§9.2 第 2 行 / §9.3 第 2 行）

    /// <summary>嫁出：支出本家阶层嫁妆、收入对方阶层聘礼；现任官员的女儿另得「年俸 150%」。</summary>
    [Fact]
    public void 嫁出结算本家嫁妆与对方聘礼并归档()
    {
        var member = RulesHarness.Official(1, MarriagePolicy.PressureHighestLevel, gender: Gender.Female, age: 20);
        var state = State(
            Difficulty.Normal,
            Origin.Artisan,
            RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdHighGuan * 2m),
            true,
            member);

        var random = new ScriptedRandom(SuccessRoll, RollIn(MarriageClass.Commoner, WeightsOf(state)));
        var expectedDowry = LifecycleQueries.OwnDowryAmount(state, member);
        var expectedGift = LifecycleQueries.GiftAmount(state, MarriageClass.Commoner);

        var outcome = LifecycleEntry.TryMatchmake(
            state,
            member,
            MarriageDirection.GroomOut,
            random,
            new FixedNameGenerator());

        Assert.True(outcome.Accepted);
        Assert.Equal(-expectedDowry, TotalOf(state, LedgerCategory.DowryPaid));
        Assert.Equal(expectedGift, TotalOf(state, LedgerCategory.BetrothalGiftReceived));

        // §9.3 第 2 行：仕族现任官员的女儿外嫁 ⇒ 额外「女儿当前年俸 150%」
        var extra = Money.FromGuan(
            SalaryTable.AnnualSalaryGuan(MarriagePolicy.PressureHighestLevel) * MarriagePolicy.DegreeMarriageFeeRatio);
        Assert.Equal(extra, TotalOf(state, LedgerCategory.DegreeMarriageDowryReceived));

        // 外嫁归档：不计口、不可科举，但数据保留
        Assert.True(member.Status.HasFlag(StatusFlag.MarriedOut));
        Assert.DoesNotContain(member, state.Family.RegisteredMembers);
        Assert.DoesNotContain(member, CountedMembers.Counted(state.Family));
        Assert.Contains(member, state.Family.ArchivedMembers);
        Assert.NotNull(state.Family.TryGet(member.Id));
        Assert.Equal(1, CountOf(state, LedgerCategory.MemberMarriedOut));
    }

    /// <summary>非仕族 / 非官员的女子外嫁 MUST NOT 产生「年俸 150%」收入（FR-018 两条件）。</summary>
    /// <param name="shiStatus">家族是否具仕身份。</param>
    /// <param name="official">外嫁女子是否现任官员。</param>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void 非仕族或非官员的外嫁不产生年俸彩礼(bool shiStatus, bool official)
    {
        var member = RulesHarness.Member(1, Gender.Female, 20);
        if (official)
        {
            member.Rank = new OfficialRank(MarriagePolicy.PressureHighestLevel);
        }

        var state = State(
            Difficulty.Normal,
            Origin.Artisan,
            RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdHighGuan * 2m),
            shiStatus,
            member);

        var random = new ScriptedRandom(SuccessRoll, RollIn(MarriageClass.Commoner, WeightsOf(state)));

        var outcome = LifecycleEntry.TryMatchmake(
            state,
            member,
            MarriageDirection.GroomOut,
            random,
            new FixedNameGenerator());

        Assert.True(outcome.Accepted);
        Assert.Equal(0, CountOf(state, LedgerCategory.DegreeMarriageDowryReceived));
    }

    // ------------------------------------------------------------------ 基座

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
