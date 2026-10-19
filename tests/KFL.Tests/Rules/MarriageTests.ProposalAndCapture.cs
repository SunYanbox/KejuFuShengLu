using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;
using KFL.Rules.FamilyLifecycle;
using Xunit;

namespace KFL.Tests.Rules;

public partial class MarriageTests
{
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

    /// <summary>FR-020：候选集合只保留「本家族付得起该方向应付额」者——付不起时不出提议且不抽人。</summary>
    [Fact]
    public void 提亲剔除付不起聘礼的候选成员()
    {
        var member = RulesHarness.Member(1, Gender.Male, 20);
        var state = State(Difficulty.Normal, Origin.Artisan, Money.Zero, false, member);
        var random = new ScriptedRandom((double)MarriagePolicy.ProposalBaseChance / 2d);

        var market = MarriageMarket.Run(state, random, new FixedNameGenerator());

        Assert.Null(market.MatchmakerProposal);
        Assert.Null(state.Family.Lifecycle.Proposal);

        // 只有概率骰：候选集合为空 ⇒ 既不分段也不抽人。
        Assert.Equal(1, random.DoublesRequested);
        Assert.Equal(0, random.IntsRequested);
        Assert.Empty(state.Economy.Ledger.Entries);
    }

    /// <summary>FR-020：命中后在可支付候选集合内均匀抽 1 名（恰 1 次 <c>Next(0, 候选数)</c>）。</summary>
    [Fact]
    public void 提亲命中后在候选集合内均匀抽一名()
    {
        var male = RulesHarness.Member(1, Gender.Male, 20);
        var female = RulesHarness.Member(2, Gender.Female, 22);
        var state = State(Difficulty.Normal, Origin.Artisan, RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdLow * 2m), false, male, female);

        // 候选取人的随机消费量：用独立探针量出候选属性 roll 的次数（与取谁无关）。
        var probe = new ScriptedRandom();
        MarriageMarket.CreateCandidate(state, probe, new FixedNameGenerator(), Gender.Female, 20, 1, DegreeLevel.BaiShen, state.CurrentDate);

        var random = new ScriptedRandom((double)MarriagePolicy.ProposalBaseChance / 2d);
        var market = MarriageMarket.Run(state, random, new FixedNameGenerator());

        var proposal = Assert.IsType<MarriageProposal>(market.MatchmakerProposal);
        Assert.Equal(MarriageProposalKind.Matchmaker, proposal.Kind);

        // 取人 1 次 Next + 候选年龄 1 次；概率骰 1 次 NextDouble + 候选属性 16 次。
        Assert.Equal(1 + probe.IntsRequested, random.IntsRequested);
        Assert.Equal(1 + probe.DoublesRequested, random.DoublesRequested);
        Assert.Equal(male.Id, proposal.OurMemberId);
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
}
