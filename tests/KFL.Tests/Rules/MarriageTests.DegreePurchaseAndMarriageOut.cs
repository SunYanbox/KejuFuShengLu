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

    // ------------------------------------------------- 支付能力不足 ⇒ 整单失败（§17 裁决 2026-10-17）

    /// <summary>聘礼「现金 + 储蓄」不足 ⇒ 整单失败：不成婚、无婚嫁条目、托媒费不退、不转贷款。</summary>
    [Fact]
    public void 聘礼付不起时整单失败且托媒费不退()
    {
        var member = RulesHarness.Member(1, Gender.Male, 20);
        var state = State(
            Difficulty.Normal,
            Origin.Artisan,
            RulesHarness.Guan(MarriagePolicy.MatchmakerFeeGuan),
            false,
            member);
        var random = new ScriptedRandom(SuccessRoll, RollIn(MarriageClass.Official, WeightsOf(state)));

        var expectedFee = LifecycleQueries.MatchmakerFee(state);
        var expectedGift = LifecycleQueries.GiftAmount(state, MarriageClass.Official);
        Assert.True(expectedGift.Guan > expectedFee.Guan);

        var outcome = LifecycleEntry.TryMatchmake(
            state,
            member,
            MarriageDirection.BrideInto,
            random,
            new FixedNameGenerator());

        Assert.False(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.InsufficientFunds, outcome.RejectReason);
        Assert.Equal(expectedFee, outcome.Amount);

        // 托媒费照实扣下且不退；托媒成功后才会发生的婚嫁条目一条也不许有
        Assert.Equal(-expectedFee, TotalOf(state, LedgerCategory.MatchmakerFee));
        Assert.Equal(1, CountOf(state, LedgerCategory.MatchmakerFee));
        Assert.Equal(0, CountOf(state, LedgerCategory.BetrothalGiftPaid));
        Assert.Equal(0, CountOf(state, LedgerCategory.DowryReceived));
        Assert.Equal(0, CountOf(state, LedgerCategory.MarriageFormed));

        Assert.Null(state.Family.SpouseOf(member.Id));
        Assert.Single(state.Family.RegisteredMembers);
        Assert.False(state.Economy.Treasury.Loan.Principal.IsPositive);

        // 恰好付得起（现金 == 托媒费 + 聘礼）时照常成婚
        var exact = State(
            Difficulty.Normal,
            Origin.Artisan,
            expectedFee + expectedGift,
            false,
            RulesHarness.Member(1, Gender.Male, 20));
        var exactOutcome = LifecycleEntry.TryMatchmake(
            exact,
            exact.Family.TryGet(exact.Family.HeadId!.Value)!,
            MarriageDirection.BrideInto,
            new ScriptedRandom(SuccessRoll, RollIn(MarriageClass.Official, WeightsOf(exact))),
            new FixedNameGenerator());

        Assert.True(exactOutcome.Accepted);
        Assert.Equal(-expectedGift, TotalOf(exact, LedgerCategory.BetrothalGiftPaid));
    }

    /// <summary>本家嫁妆不足 ⇒ 整单失败：不外嫁归档、无婚嫁条目、托媒费不退。</summary>
    [Fact]
    public void 嫁妆付不起时整单失败且不外嫁归档()
    {
        var member = RulesHarness.Official(1, MarriagePolicy.PressureHighestLevel, gender: Gender.Female, age: 20);
        var state = State(
            Difficulty.Normal,
            Origin.Artisan,
            RulesHarness.Guan(MarriagePolicy.MatchmakerFeeGuan),
            true,
            member);
        var random = new ScriptedRandom(SuccessRoll, RollIn(MarriageClass.Commoner, WeightsOf(state)));

        var expectedFee = LifecycleQueries.MatchmakerFee(state);
        var expectedDowry = LifecycleQueries.OwnDowryAmount(state, member);
        Assert.True(expectedDowry.Guan > expectedFee.Guan);

        var outcome = LifecycleEntry.TryMatchmake(
            state,
            member,
            MarriageDirection.GroomOut,
            random,
            new FixedNameGenerator());

        Assert.False(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.InsufficientFunds, outcome.RejectReason);
        Assert.Equal(expectedFee, outcome.Amount);
        Assert.Equal(0, CountOf(state, LedgerCategory.DowryPaid));
        Assert.Equal(0, CountOf(state, LedgerCategory.BetrothalGiftReceived));
        Assert.Equal(0, CountOf(state, LedgerCategory.DegreeMarriageDowryReceived));
        Assert.Equal(0, CountOf(state, LedgerCategory.MemberMarriedOut));

        Assert.False(member.Status.HasFlag(StatusFlag.MarriedOut));
        Assert.Contains(member, state.Family.RegisteredMembers);
        Assert.Null(state.Family.SpouseOf(member.Id));
        Assert.False(state.Economy.Treasury.Loan.Principal.IsPositive);
    }

    /// <summary>接受提亲时聘礼付不起 ⇒ 整单失败、提议已被消耗（名额不返还）。</summary>
    [Fact]
    public void 接受提亲时付不起聘礼则整单失败且提议已消耗()
    {
        var member = RulesHarness.Member(1, Gender.Male, 20);
        var state = State(
            Difficulty.Normal,
            Origin.Artisan,
            RulesHarness.Guan(1m),
            false,
            member);

        var gift = LifecycleQueries.GiftAmount(state, MarriageClass.Official);
        state.Family.Lifecycle.SetProposal(new MarriageProposal
        {
            Kind = MarriageProposalKind.Matchmaker,
            OurMemberId = member.Id,
            CandidateName = "苏州沈氏",
            CandidateGender = Gender.Female,
            CandidateAge = 18,
            TalentAgriculture = 50,
            TalentCommerce = 50,
            TalentScholar = 50,
            TalentCraft = 50,
            CandidateStudy = 30,
            CandidateHealth = 85,
            CandidateLifespan = 60,
            CandidateDegree = DegreeLevel.BaiShen,
            Class = MarriageClass.Official,
            BetrothalGift = gift,
            Dowry = LifecycleQueries.DowryAmount(state, MarriageClass.Official),
            ProposedAt = RulesHarness.Date,
        });

        Assert.NotNull(LifecycleQueries.PendingProposal(state.Family, RulesHarness.Date));

        var outcome = LifecycleEntry.RespondToProposal(
            state,
            accept: true,
            new ScriptedRandom(),
            new FixedNameGenerator());

        Assert.False(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.InsufficientFunds, outcome.RejectReason);
        Assert.Equal(0, CountOf(state, LedgerCategory.BetrothalGiftPaid));
        Assert.Equal(0, CountOf(state, LedgerCategory.DowryReceived));
        Assert.Equal(0, CountOf(state, LedgerCategory.MarriageFormed));
        Assert.Null(state.Family.SpouseOf(member.Id));
        Assert.Null(LifecycleQueries.PendingProposal(state.Family, RulesHarness.Date));
        Assert.False(state.Economy.Treasury.Loan.Principal.IsPositive);
    }

    // ------------------------------------------------------------------ 基座
}
