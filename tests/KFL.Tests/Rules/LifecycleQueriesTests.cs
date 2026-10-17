using System.Globalization;
using System.Text;
using KFL.Core.Config;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;
using KFL.Rules.Economy;
using KFL.Rules.FamilyLifecycle;
using KFL.Rules.Settlement;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// US1 查询面的验收测试（契约十九 §2 / FR-044）：
/// ① 纯度——每个查询调用前后 <see cref="GameState"/> 逐字段不变、结果可重复；
/// ② 判据单一来源——<c>ClassOf</c> 以 <see cref="Family.HasShiStatus"/> 优先于 <see cref="Origin"/>；
/// ③ 提议当月有效（<c>PendingProposal</c> 过期返 <c>null</c>）；
/// ④ 生育可行性与疾病状态逐项。
/// </summary>
/// <remarks>
/// 查询面**不接收随机服务**（签名本身就是「0 次随机」的强制），故随机消费以「纯度 + 结果可重复」等价表达：
/// 若查询偷偷消费了随机或写了状态，调用前后的指纹与两次调用的结果都会分叉。
/// </remarks>
public class LifecycleQueriesTests
{
    private static readonly Guid StateId = Guid.Parse("7b3d1e58-2c64-4a90-8f17-5d6e9a0b1c23");

    // ------------------------------------------------------------------ 纯度

    /// <summary>全部查询在调用前后不改动状态，且两次调用结果一致。</summary>
    [Fact]
    public void 查询面不改动状态且结果可重复()
    {
        var (family, head, wife) = MarriedFamily();
        var state = StateOf(
            family,
            Difficulty.Normal,
            Origin.Merchant,
            RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdHighGuan),
            true);
        var couple = new CoupleKey(head.Id, wife.Id);
        var before = Fingerprint(state);
        var classBefore = LifecycleQueries.ClassOf(state, head);
        var pendingBefore = LifecycleQueries.PendingProposal(state.Family, state.CurrentDate);
        var candidatesBefore = LifecycleQueries.MarriageCandidateMembers(state.Family, state.CurrentDate);
        var feasibilityBefore = LifecycleQueries.MatchmakeFeasibility(state, head, MarriageDirection.BrideInto);
        var generationBefore = LifecycleQueries.GenerationOf(head);
        var illnessBefore = LifecycleQueries.IllnessOf(head);
        var childbirthBefore = LifecycleQueries.ChildbirthFeasibility(state, couple, state.CurrentDate);
        var canApplyBefore = LifecycleQueries.CanApplyForChildbirth(state.Family, couple, state.CurrentDate);
        var purchaseBefore = LifecycleQueries.PurchaseQuote(state);
        var successionBefore = LifecycleQueries.SuccessionCandidates(state.Family);
        var lowerBefore = LifecycleQueries.LowerDegreeOf(DegreeLevel.JinShi);
        var directionBefore = LifecycleQueries.DirectionOf(Gender.Female);
        var feeBefore = LifecycleQueries.MatchmakerFee(state);

        // 再调用一遍：结果必须逐一相等（同一状态下无隐藏随机依赖）
        Assert.Equal(classBefore, LifecycleQueries.ClassOf(state, head));
        Assert.Equal(pendingBefore, LifecycleQueries.PendingProposal(state.Family, state.CurrentDate));
        Assert.Equal(candidatesBefore, LifecycleQueries.MarriageCandidateMembers(state.Family, state.CurrentDate));
        Assert.Equal(feasibilityBefore, LifecycleQueries.MatchmakeFeasibility(state, head, MarriageDirection.BrideInto));
        Assert.Equal(generationBefore, LifecycleQueries.GenerationOf(head));
        Assert.Equal(illnessBefore, LifecycleQueries.IllnessOf(head));
        Assert.Equal(childbirthBefore, LifecycleQueries.ChildbirthFeasibility(state, couple, state.CurrentDate));
        Assert.Equal(canApplyBefore, LifecycleQueries.CanApplyForChildbirth(state.Family, couple, state.CurrentDate));
        Assert.Equal(purchaseBefore, LifecycleQueries.PurchaseQuote(state));
        Assert.Equal(successionBefore, LifecycleQueries.SuccessionCandidates(state.Family));
        Assert.Equal(lowerBefore, LifecycleQueries.LowerDegreeOf(DegreeLevel.JinShi));
        Assert.Equal(directionBefore, LifecycleQueries.DirectionOf(Gender.Female));
        Assert.Equal(feeBefore, LifecycleQueries.MatchmakerFee(state));

        Assert.Equal(before, Fingerprint(state));
    }

    // ------------------------------------------------------------------ 阶层判据（单一来源）

    /// <summary><c>ClassOf</c>：<c>HasShiStatus</c> 优先于出身；商 / 士出身落士人档，农 / 工落平民档。</summary>
    /// <param name="shiStatus">家族是否具仕身份。</param>
    /// <param name="origin">出身。</param>
    /// <param name="expected">期望阶层。</param>
    [Theory]
    [InlineData(false, Origin.Farmer, MarriageClass.Commoner)]
    [InlineData(false, Origin.Artisan, MarriageClass.Commoner)]
    [InlineData(false, Origin.Merchant, MarriageClass.Scholar)]
    [InlineData(false, Origin.Scholar, MarriageClass.Scholar)]
    [InlineData(true, Origin.Farmer, MarriageClass.Official)]
    [InlineData(true, Origin.Artisan, MarriageClass.Official)]
    [InlineData(true, Origin.Merchant, MarriageClass.Official)]
    [InlineData(true, Origin.Scholar, MarriageClass.Official)]
    public void 阶层判据以仕身份优先于出身(bool shiStatus, Origin origin, MarriageClass expected)
    {
        var member = RulesHarness.Member(1, Gender.Male, 20);
        var state = State(Difficulty.Normal, origin, RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdLow), shiStatus, member);

        Assert.Equal(expected, LifecycleQueries.ClassOf(state, member));
    }

    // ------------------------------------------------------------------ 提议的有效期

    /// <summary>提议只在产出当月有效：次月查询返 <c>null</c>（无状态位、无清理动作）。</summary>
    [Fact]
    public void 提议当月有效次月即失效()
    {
        var member = RulesHarness.Member(1, Gender.Male, 20);
        var state = State(
            Difficulty.Normal,
            Origin.Artisan,
            RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdLow * 2m),
            false,
            member);

        MarriageMarket.Run(
            state,
            new FixedRandomService((double)MarriagePolicy.ProposalBaseChance / 2d),
            new FixedNameGenerator());

        var proposal = state.Family.Lifecycle.Proposal;
        Assert.NotNull(proposal);
        Assert.Equal(proposal, LifecycleQueries.PendingProposal(state.Family, state.CurrentDate));
        Assert.Null(LifecycleQueries.PendingProposal(state.Family, NextMonth(state.CurrentDate)));

        // 过期后成员恢复可托媒
        Assert.True(LifecycleQueries.IsMarriageable(
            state.Family,
            member,
            MarriageDirection.BrideInto,
            NextMonth(state.CurrentDate)));
    }

    // ------------------------------------------------------------------ 婚配候选与可行性

    /// <summary>可婚配成员：在册、未婚、成年；已亡 / 外嫁 / 已婚 / 未成年一律排除。</summary>
    [Fact]
    public void 可婚配成员只在册未婚成年()
    {
        var adultMale = RulesHarness.Member(1, Gender.Male, 20);
        var adultFemale = RulesHarness.Member(2, Gender.Female, 20);
        var minor = RulesHarness.Member(3, Gender.Male, AgeBracketPolicy.MaleAdulthoodAge - 1);
        var marriedMan = RulesHarness.Member(4, Gender.Male, 30);
        var deadMan = RulesHarness.Member(5, Gender.Male, 30);
        var outMarried = RulesHarness.Member(6, Gender.Female, 30);
        var family = RulesHarness.FamilyOf(adultMale, adultFemale, minor, marriedMan, deadMan, outMarried);

        var wife = RulesHarness.Member(7, Gender.Female, 30);
        family.AddFoundingMember(wife);
        family.Marry(marriedMan.Id, wife.Id);
        deadMan.Status |= StatusFlag.Deceased;
        outMarried.Status |= StatusFlag.MarriedOut;

        var state = new GameState(
            StateId,
            RulesHarness.Date,
            Difficulty.Normal,
            Origin.Artisan,
            family,
            NewEconomy(RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdLow)));

        var candidates = LifecycleQueries.MarriageCandidateMembers(state.Family, state.CurrentDate);

        Assert.Contains(adultMale, candidates);
        Assert.Contains(adultFemale, candidates);
        Assert.DoesNotContain(minor, candidates);
        Assert.DoesNotContain(marriedMan, candidates);
        Assert.DoesNotContain(deadMan, candidates);
        Assert.DoesNotContain(outMarried, candidates);
        Assert.Equal(candidates.OrderBy(member => member.Id.Value), candidates);
    }

    /// <summary>可行性预览：金额区间按支出系数、买功名选项按「仕 + 现任官员 + 有更低档」三条件。</summary>
    [Fact]
    public void 婚配可行性预览给出金额区间与买功名选项()
    {
        var member = RulesHarness.Official(1, MarriagePolicy.PressureHighestLevel, age: 30);
        member.AppendDegree(new DegreeRecord(DegreeLevel.JinShi, null, RulesHarness.Date, DegreeChangeCause.Initial));
        var state = State(
            Difficulty.Normal,
            Origin.Artisan,
            RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdHighGuan),
            true,
            member);

        var factor = DifficultyRates.ExpenseFactor(Difficulty.Normal);
        var feasibility = LifecycleQueries.MatchmakeFeasibility(state, member, MarriageDirection.BrideInto);

        Assert.True(feasibility.Available);
        Assert.Equal(LifecycleRejectReason.None, feasibility.RejectReason);
        Assert.Equal(MarriageDirection.BrideInto, feasibility.Direction);
        Assert.Equal(LifecycleQueries.ClassOf(state, member), feasibility.FamilyClass);
        Assert.Equal(LifecycleQueries.MatchmakerFee(state), feasibility.MatchmakerFee);
        Assert.Equal(
            Money.FromGuan(MarriagePolicy.CommonerGiftGuan * factor),
            feasibility.BetrothalGiftLower);
        Assert.Equal(
            Money.FromGuan(MarriagePolicy.OfficialGiftGuan * factor),
            feasibility.BetrothalGiftUpper);
        Assert.Equal(
            Money.FromGuan(MarriagePolicy.CommonerDowryGuan * factor),
            feasibility.DowryLower);
        Assert.Equal(
            Money.FromGuan(MarriagePolicy.OfficialDowryGuan * factor),
            feasibility.DowryUpper);
        Assert.True(feasibility.DegreeMarriageAvailable);
        Assert.Equal(LifecycleQueries.DegreeMarriageFee(state, member), feasibility.DegreeMarriageFee);
    }

    /// <summary>非仕族或非官员时买功名选项不可用（FR-017 前置）。</summary>
    /// <param name="shiStatus">家族是否具仕身份。</param>
    /// <param name="official">成员是否现任官员。</param>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void 非仕族或非官员时买功名选项不可用(bool shiStatus, bool official)
    {
        var member = RulesHarness.Member(1, Gender.Male, 30);
        if (official)
        {
            member.Rank = new OfficialRank(MarriagePolicy.PressureHighestLevel);
        }

        member.AppendDegree(new DegreeRecord(DegreeLevel.JinShi, null, RulesHarness.Date, DegreeChangeCause.Initial));
        var state = State(
            Difficulty.Normal,
            Origin.Artisan,
            RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdHighGuan),
            shiStatus,
            member);

        var feasibility = LifecycleQueries.MatchmakeFeasibility(state, member, MarriageDirection.BrideInto);

        Assert.False(feasibility.DegreeMarriageAvailable);
        Assert.Equal(LifecycleRejectReason.NotMarriageable, feasibility.DegreeMarriageRejectReason);
        Assert.Equal(Money.Zero, feasibility.DegreeMarriageFee);
    }

    // ------------------------------------------------------------------ 生育

    /// <summary>生育可行性：年龄窗口、婚姻关系、本年是否已申请、金额可及。</summary>
    [Fact]
    public void 生育可行性覆盖年龄婚姻年度与资金()
    {
        var (family, head, wife) = MarriedFamily();
        var state = StateOf(
            family,
            Difficulty.Normal,
            Origin.Artisan,
            RulesHarness.Guan(ChildbirthPolicy.ChildbirthExpenseGuan),
            false);
        var couple = new CoupleKey(head.Id, wife.Id);
        var cost = LifecycleQueries.ChildbirthCost(state);

        var available = LifecycleQueries.ChildbirthFeasibility(state, couple, state.CurrentDate);
        Assert.True(available.Available);
        Assert.Equal(LifecycleRejectReason.None, available.RejectReason);
        Assert.Equal(wife.AgeAt(state.CurrentDate), available.WifeAge);
        Assert.False(available.AlreadyAppliedThisYear);
        Assert.Equal(cost, available.Cost);
        Assert.True(available.Affordable);
        Assert.True(LifecycleQueries.CanApplyForChildbirth(state.Family, couple, state.CurrentDate));

        // 本年已申请 ⇒ 灰置
        state.Family.Lifecycle.RecordBirthApplication(couple, state.CurrentDate.Year);
        var applied = LifecycleQueries.ChildbirthFeasibility(state, couple, state.CurrentDate);
        Assert.False(applied.Available);
        Assert.Equal(LifecycleRejectReason.AlreadyAppliedThisYear, applied.RejectReason);
        Assert.True(applied.AlreadyAppliedThisYear);
        Assert.False(LifecycleQueries.CanApplyForChildbirth(state.Family, couple, state.CurrentDate));

        // 次年起恢复可申请
        Assert.True(LifecycleQueries.CanApplyForChildbirth(
            state.Family,
            couple,
            new GameDate(state.CurrentDate.Year + 1, state.CurrentDate.Month)));
    }

    /// <summary>女方年龄在窗口之外时返 <c>FertilityWindowClosed</c>。</summary>
    /// <param name="age">女方年龄。</param>
    [Theory]
    [InlineData(ChildbirthPolicy.FertilityAgeMin - 1)]
    [InlineData(ChildbirthPolicy.FertilityAgeMax + 1)]
    public void 女方超出生育窗口时不可申请(int age)
    {
        var head = RulesHarness.Member(1, Gender.Male, 40);
        var wife = RulesHarness.Member(2, Gender.Female, age);
        var family = RulesHarness.FamilyOf(head, wife);
        family.Marry(head.Id, wife.Id);
        var state = new GameState(
            StateId,
            RulesHarness.Date,
            Difficulty.Normal,
            Origin.Artisan,
            family,
            NewEconomy(RulesHarness.Guan(ChildbirthPolicy.ChildbirthExpenseGuan)));

        var feasibility = LifecycleQueries.ChildbirthFeasibility(
            state,
            new CoupleKey(head.Id, wife.Id),
            state.CurrentDate);

        Assert.False(feasibility.Available);
        Assert.Equal(LifecycleRejectReason.FertilityWindowClosed, feasibility.RejectReason);
        Assert.Equal(age, feasibility.WifeAge);
    }

    /// <summary>资金不足时返 <c>InsufficientFunds</c>（资格仍成立）。</summary>
    [Fact]
    public void 生育费用不足时返资金不足()
    {
        var (family, head, wife) = MarriedFamily();
        var state = StateOf(family, Difficulty.Normal, Origin.Artisan, Money.Zero, false);

        var feasibility = LifecycleQueries.ChildbirthFeasibility(
            state,
            new CoupleKey(head.Id, wife.Id),
            state.CurrentDate);

        Assert.False(feasibility.Available);
        Assert.Equal(LifecycleRejectReason.InsufficientFunds, feasibility.RejectReason);
        Assert.True(LifecycleQueries.CanApplyForChildbirth(
            state.Family,
            new CoupleKey(head.Id, wife.Id),
            state.CurrentDate));
        Assert.False(feasibility.Affordable);
    }

    /// <summary>非配偶或一方不在册时返 <c>NotMarriageable</c>。</summary>
    [Fact]
    public void 非配偶时不可申请生育()
    {
        var head = RulesHarness.Member(1, Gender.Male, 40);
        var wife = RulesHarness.Member(2, Gender.Female, 30);
        var state = State(Difficulty.Normal, Origin.Artisan, RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdLow), false, head, wife);

        var feasibility = LifecycleQueries.ChildbirthFeasibility(
            state,
            new CoupleKey(head.Id, wife.Id),
            state.CurrentDate);

        Assert.False(feasibility.Available);
        Assert.Equal(LifecycleRejectReason.NotMarriageable, feasibility.RejectReason);
    }

    // ------------------------------------------------------------------ 疾病与买人口

    /// <summary>疾病状态：未患病时两项计时为 <c>null</c>；患病时如实回报程度与疗程。</summary>
    [Fact]
    public void 疾病状态如实回报程度与疗程()
    {
        var member = RulesHarness.Member(1, Gender.Male, 30);

        var healthy = LifecycleQueries.IllnessOf(member);
        Assert.False(healthy.Ill);
        Assert.Null(healthy.SeverityBasisPoints);
        Assert.Null(healthy.TreatmentRemainingMonths);

        member.Status |= StatusFlag.Ill;
        member.Timers = new StatusTimers(
            illnessSeverityBasisPoints: IllnessPolicy.SeverityBasisPointsScale,
            treatmentRemainingMonths: IllnessPolicy.TreatmentMonths);

        var ill = LifecycleQueries.IllnessOf(member);
        Assert.True(ill.Ill);
        Assert.Equal(IllnessPolicy.SeverityBasisPointsScale, ill.SeverityBasisPoints);
        Assert.Equal(IllnessPolicy.TreatmentMonths, ill.TreatmentRemainingMonths);
    }

    /// <summary>买人口报价：第 n 名 = 基础价 × n，跨年重置为第 1 名。</summary>
    [Fact]
    public void 买人口报价按年内名次递增且跨年重置()
    {
        var member = RulesHarness.Member(1, Gender.Male, 30);
        var state = State(
            Difficulty.Normal,
            Origin.Artisan,
            RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdLow),
            false,
            member);

        var first = LifecycleQueries.PurchaseQuote(state);
        Assert.True(first.Available);
        Assert.Equal(LifecycleRejectReason.None, first.RejectReason);
        Assert.Equal(1, first.Ordinal);
        Assert.Equal(Money.FromGuan(PopulationPurchasePolicy.BasePriceGuan), first.Price);
        Assert.True(first.Affordable);

        Assert.Equal(1, state.Family.Lifecycle.NextPurchaseOrdinal(state.CurrentDate.Year));

        var second = LifecycleQueries.PurchaseQuote(state);
        Assert.Equal(2, second.Ordinal);
        Assert.Equal(Money.FromGuan(PopulationPurchasePolicy.BasePriceGuan * 2), second.Price);

        // 推进到下一个自然年 ⇒ 名次重置为第 1 名
        for (var month = 0; month < 12; month++)
        {
            state.AdvanceMonth();
        }

        Assert.Equal(RulesHarness.Date.Year + 1, state.CurrentDate.Year);
        var nextYear = LifecycleQueries.PurchaseQuote(state);
        Assert.Equal(1, nextYear.Ordinal);
        Assert.Equal(Money.FromGuan(PopulationPurchasePolicy.BasePriceGuan), nextYear.Price);
    }

    /// <summary>无家主 / 资金不足时的买人口报价灰置原因。</summary>
    [Fact]
    public void 买人口报价在无家主或资金不足时灰置()
    {
        var member = RulesHarness.Member(1, Gender.Male, 30);
        var poor = State(Difficulty.Normal, Origin.Artisan, Money.Zero, false, member);

        var noFunds = LifecycleQueries.PurchaseQuote(poor);
        Assert.False(noFunds.Available);
        Assert.Equal(LifecycleRejectReason.InsufficientFunds, noFunds.RejectReason);

        var vacantEconomy = NewEconomy(RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdLow));
        var vacantFamily = new Family("测");
        vacantFamily.AddFoundingMember(member);
        var vacant = new GameState(StateId, RulesHarness.Date, Difficulty.Normal, Origin.Artisan, vacantFamily, vacantEconomy);

        var noHead = LifecycleQueries.PurchaseQuote(vacant);
        Assert.False(noHead.Available);
        Assert.Equal(LifecycleRejectReason.HeadVacant, noHead.RejectReason);
    }

    // ------------------------------------------------------------------ 辈分与继任候选

    /// <summary><c>GenerationOf</c> 如实返回成员辈分；继任候选在家主健在时为空。</summary>
    [Fact]
    public void 辈分如实返回且家主健在时无继任候选()
    {
        var head = RulesHarness.Member(1, Gender.Male, 40);
        var child = FamilyFixtures.NewPerson(
            2,
            "P2",
            Gender.Male,
            RulesHarness.Date.Year - 10,
            RulesHarness.Date.Month,
            head.Generation + 1,
            fatherId: head.Id);
        var family = RulesHarness.FamilyOf(head);
        family.AddChild(child);
        var state = new GameState(
            StateId,
            RulesHarness.Date,
            Difficulty.Normal,
            Origin.Artisan,
            family,
            NewEconomy(RulesHarness.Guan(MarriagePolicy.ClassWealthThresholdLow)));

        Assert.Equal(head.Generation, LifecycleQueries.GenerationOf(head));
        Assert.Equal(child.Generation, LifecycleQueries.GenerationOf(child));
        Assert.Empty(LifecycleQueries.SuccessionCandidates(state.Family));

        // 家主亡故 ⇒ 首选在册男性子女
        head.Status |= StatusFlag.Deceased;
        var candidates = LifecycleQueries.SuccessionCandidates(state.Family);
        Assert.NotEmpty(candidates);
        Assert.Equal(child.Id, candidates[0].Id);
    }

    // ------------------------------------------------------------------ 基座

    /// <summary>一对已婚夫妇（男 40 / 女 30，均在生育窗口且满足婚龄）及其所在家族。</summary>
    /// <returns>家族、丈夫与妻子。</returns>
    private static (Family Family, Person Husband, Person Wife) MarriedFamily()
    {
        var husband = RulesHarness.Member(1, Gender.Male, 40);
        var wife = RulesHarness.Member(2, Gender.Female, 30);
        var family = RulesHarness.FamilyOf(husband, wife);
        family.Marry(husband.Id, wife.Id);

        return (family, husband, wife);
    }

    /// <summary>装配一个含既有家族的游戏状态（家族 MUST 已建好，避免成员被重复注册）。</summary>
    /// <param name="family">已建好的家族。</param>
    /// <param name="difficulty">难度。</param>
    /// <param name="origin">出身。</param>
    /// <param name="cash">现银。</param>
    /// <param name="shiStatus">家族是否具仕身份。</param>
    /// <returns>游戏状态。</returns>
    private static GameState StateOf(
        Family family,
        Difficulty difficulty,
        Origin origin,
        Money cash,
        bool shiStatus)
    {
        family.HasShiStatus = shiStatus;

        return new GameState(StateId, RulesHarness.Date, difficulty, origin, family, NewEconomy(cash));
    }

    /// <summary>下一个月。</summary>
    /// <param name="date">基准月份。</param>
    /// <returns>下一个月。</returns>
    private static GameDate NextMonth(GameDate date) =>
        date.Month < 12 ? new GameDate(date.Year, date.Month + 1) : new GameDate(date.Year + 1, 1);

    /// <summary>装配一个含给定成员的游戏状态（首个为家主）。</summary>
    /// <param name="difficulty">难度。</param>
    /// <param name="origin">出身。</param>
    /// <param name="cash">现银。</param>
    /// <param name="shiStatus">家族是否具仕身份。</param>
    /// <param name="members">成员。</param>
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

        return new GameState(StateId, RulesHarness.Date, difficulty, origin, family, NewEconomy(cash));
    }

    /// <summary>新建一个空持有的家族经济。</summary>
    /// <param name="cash">现银。</param>
    /// <returns>家族经济。</returns>
    private static FamilyEconomy NewEconomy(Money cash) => new(
        LivingStandard.Normal,
        new GrainPriceIndex(GrainPricePolicy.Initial),
        new Treasury(cash),
        new Holdings());

    /// <summary>
    /// 状态指纹：把查询面可能波及的字段全部串起来，用于「调用前后逐字段不变」的断言。
    /// </summary>
    /// <param name="state">游戏状态。</param>
    /// <returns>指纹字符串。</returns>
    private static string Fingerprint(GameState state)
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"date={state.CurrentDate.Year}-{state.CurrentDate.Month};");
        builder.Append(CultureInfo.InvariantCulture, $"difficulty={state.Difficulty};pending={state.PendingDifficulty};origin={state.Origin};");
        builder.Append(CultureInfo.InvariantCulture, $"id={state.Id};");

        var family = state.Family;
        builder.Append(CultureInfo.InvariantCulture, $"family={family.Name};shi={family.HasShiStatus};head={family.HeadId};");
        builder.Append(CultureInfo.InvariantCulture, $"lifecycle={family.Lifecycle.PurchaseCount}/{family.Lifecycle.PurchaseYear}/{family.Lifecycle.BirthApplicationYear};");
        builder.Append(CultureInfo.InvariantCulture, $"proposal={family.Lifecycle.Proposal?.Kind};");
        builder.Append(CultureInfo.InvariantCulture, $"members={family.Members.Count};registered={family.RegisteredMembers.Count};archived={family.ArchivedMembers.Count};");

        var economy = state.Economy;
        builder.Append(CultureInfo.InvariantCulture, $"cash={economy.Treasury.Cash.Wen};savings={economy.Treasury.Savings.Wen};capital={economy.Treasury.MerchantCapital.Wen};loan={economy.Treasury.Loan.Total.Wen};");
        builder.Append(CultureInfo.InvariantCulture, $"grain={economy.GrainPriceIndex.Value};living={economy.LivingStandard};pendingLiving={economy.PendingLivingStandard};");
        builder.Append(CultureInfo.InvariantCulture, $"famine={economy.Famine.Stage}/{economy.Famine.ElapsedMonths};");
        builder.Append(CultureInfo.InvariantCulture, $"holdings={economy.Holdings.FarmlandMu}/{economy.Holdings.RuralHouses}/{economy.Holdings.UrbanHouses}/{economy.Holdings.Shops};");
        builder.Append(CultureInfo.InvariantCulture, $"entries={economy.Ledger.Entries.Count};");

        foreach (var person in family.Members.OrderBy(member => member.Id.Value))
        {
            var timers = person.Timers;
            builder.Append(CultureInfo.InvariantCulture, $"person={person.Id.Value}:{person.Name}:{person.Status}:{person.SpouseId}:{person.Study}:{person.Health}:{person.Rank}:{person.Merit}:{person.MonthsInOffice}:{person.IsTutoredByMaster}:{person.Occupation}:{person.EntryTrack}:{timers.SentenceRemainingMonths}/{timers.ExamBanRemainingMonths}/{timers.PromotionBanRemainingMonths}/{timers.AwaitingPostRemainingMonths}/{timers.IllnessSeverityBasisPoints}/{timers.TreatmentRemainingMonths}:{person.Generation};");
        }

        return builder.ToString();
    }
}
