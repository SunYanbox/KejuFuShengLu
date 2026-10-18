using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Services;
using KFL.Rules.Achievements;
using KFL.Rules.Bribery;
using KFL.Rules.Config;
using KFL.Rules.Economy;
using KFL.Rules.Exam;
using KFL.Rules.FamilyLifecycle;
using KFL.Rules.Punishment;
using KFL.Rules.Save;
using KFL.Rules.Settlement;
using KFL.Rules.Start;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// T047 / FR-045、FR-046：12 个 <see cref="AchievementTrigger"/> 入口的**即时判定**接线。
/// </summary>
/// <remarks>
/// <para>
/// 断言口径（契约二十二 §3「条款 9 / 条款 10」）：每个入口在**成功之后、返回之前**判定一次；
/// 被拒路径（校验失败 / 金额不足 / 窗口未开 / 已结束存档）MUST NOT 触发；即时判定 MUST NOT
/// 落全局文件、MUST NOT 写档、MUST NOT 抛。
/// </para>
/// <para>
/// 期望值一律经 <c>KFL.Rules/Config</c>、<c>KFL.Rules/Achievements</c> 与
/// <c>KFL.Core.Enums</c> 的成员取得，本文件 MUST NOT 另存规则数值（SC-008）。
/// </para>
/// <para>
/// <b>ACH-035 的已知缺口</b>：<c>ACH-035</c>（秦晋之好）的判据读
/// <c>SettlementResult.Lifecycle.Marriages</c>，而 <c>PopulationAdvance</c> 恒产出
/// <c>LifecycleSnapshot.None</c> ⇒ 该快照的婚嫁序列恒空，故 triggers ⑥⑦⑧（其契约「直接受益」是
/// ACH-035）在**即时判定**里无法解锁 ACH-035。本文件对这三个入口改用**同期可解锁且确实由本次
/// 上报解锁**的 <c>ACH-032</c>（人丁兴旺：在册成员 >= 20）作为「至少一条直接受益成就被解锁」的
/// 可观测证据，并另行断言「买功名婚姻确实发生」以证明 ⑧ 的分支确被执行。
/// </para>
/// </remarks>
public sealed class AchievementTriggerTests
{
    /// <summary>固定存档标识（避免 <c>Guid.Empty</c> 触发 <see cref="GameState"/> 的构造校验）。</summary>
    private static readonly Guid StateId = Guid.Parse("8a1c2f10-4b5d-4e6f-8a9b-0c1d2e3f4a5b");

    [Fact]
    public void 枚举数值与契约表逐项一致()
    {
        Assert.Equal(0, (int)AchievementTrigger.None);
        Assert.Equal(1, (int)AchievementTrigger.NewGameCreated);
        Assert.Equal(2, (int)AchievementTrigger.AssetPurchased);
        Assert.Equal(3, (int)AchievementTrigger.AssetSold);
        Assert.Equal(4, (int)AchievementTrigger.LoanOriginated);
        Assert.Equal(5, (int)AchievementTrigger.LoanRepaid);
        Assert.Equal(6, (int)AchievementTrigger.MatchmakingCommenced);
        Assert.Equal(7, (int)AchievementTrigger.ProposalAnswered);
        Assert.Equal(8, (int)AchievementTrigger.DegreeMarriagePurchased);
        Assert.Equal(9, (int)AchievementTrigger.PersonPurchased);
        Assert.Equal(10, (int)AchievementTrigger.BribeSubmitted);
        Assert.Equal(11, (int)AchievementTrigger.ExamInquiryAnswered);
        Assert.Equal(12, (int)AchievementTrigger.DebugWakeReported);
    }

    [Fact]
    public void 触发器一新建存档成功即时判定开基立业()
    {
        var setup = NewGameSetup.Create(
            new NewGameRequest(Origin.Scholar, Difficulty.Normal, "赵", new GameDate(1, 1)),
            new SeededRandomService(20261018),
            new FixedNameGenerator("赵"));

        Assert.Equal(AchievementId.FoundationLaid, MarkOf(setup.State, AchievementId.FoundationLaid).Achievement);
        Assert.Single(CompletedOf(setup.State, AchievementId.FoundationLaid));

        // 幂等：同月再判一次不重复解锁。
        var again = AchievementAdjudicator.Report(setup.State, AchievementTrigger.NewGameCreated);

        Assert.Contains(AchievementId.FoundationLaid, again.AlreadyCompleted);
        Assert.DoesNotContain(AchievementId.FoundationLaid, again.NewlyUnlocked);
        Assert.Single(CompletedOf(setup.State, AchievementId.FoundationLaid));
    }

    [Fact]
    public void 触发器二买入资产成功即时判定阡陌交通()
    {
        var state = StateOf(RulesHarness.FamilyOf(RulesHarness.Member(1, Gender.Male, 40)), Money.FromGuan(10_000m));

        AssetMarket.Buy(state, AssetKind.Farmland, 100);

        Assert.Equal(AchievementId.FarmlandHundred, MarkOf(state, AchievementId.FarmlandHundred).Achievement);
        Assert.Single(CompletedOf(state, AchievementId.FarmlandHundred));

        var again = AchievementAdjudicator.Report(state, AchievementTrigger.AssetPurchased);

        Assert.Contains(AchievementId.FarmlandHundred, again.AlreadyCompleted);
        Assert.Single(CompletedOf(state, AchievementId.FarmlandHundred));
    }

    [Fact]
    public void 触发器三卖出资产成功即时判定良田万顷()
    {
        var family = RulesHarness.FamilyOf(RulesHarness.Member(1, Gender.Male, 40));
        var state = StateOf(
            family,
            Money.FromGuan(10_000m),
            holdings: RulesHarness.HoldingsOf(farmlandMu: 501));

        AssetMarket.Sell(state, AssetKind.Farmland, 1);

        Assert.Equal(500, state.Economy.Holdings.FarmlandMu);
        Assert.Equal(AchievementId.FarmlandFiveHundred, MarkOf(state, AchievementId.FarmlandFiveHundred).Achievement);
        Assert.Single(CompletedOf(state, AchievementId.FarmlandFiveHundred));

        var again = AchievementAdjudicator.Report(state, AchievementTrigger.AssetSold);

        Assert.Contains(AchievementId.FarmlandFiveHundred, again.AlreadyCompleted);
        Assert.Single(CompletedOf(state, AchievementId.FarmlandFiveHundred));
    }

    [Fact]
    public void 触发器四罚金转为贷款即时判定债台高筑()
    {
        var subject = RulesHarness.Member(1, Gender.Male, 40);
        var family = RulesHarness.FamilyOf(subject, RulesHarness.Member(2, Gender.Male, 30));
        var state = StateOf(family, Money.Zero);

        // 现金 + 储蓄为 0 ⇒ 固定罚金 200 贯全额转为贷款本金（唯一产生贷款的路径）。
        PunishmentImposer.Apply(state, subject, ExamLevel.ShengShi, new FixedRandomService());

        Assert.True(state.Economy.Treasury.Loan.Principal > Money.FromGuan(0));
        Assert.Equal(AchievementId.DebtBurden, MarkOf(state, AchievementId.DebtBurden).Achievement);
        Assert.Single(CompletedOf(state, AchievementId.DebtBurden));

        var again = AchievementAdjudicator.Report(state, AchievementTrigger.LoanOriginated);

        Assert.Contains(AchievementId.DebtBurden, again.AlreadyCompleted);
        Assert.Single(CompletedOf(state, AchievementId.DebtBurden));
    }

    [Fact]
    public void 触发器五月度结算清偿贷款即时判定无债一身轻()
    {
        var member = RulesHarness.Member(1, Gender.Male, 40);
        member.Health = (int)GameConfig.Attributes.HealthMean;
        var state = StateOf(
            RulesHarness.FamilyOf(member),
            Money.Zero,
            holdings: RulesHarness.HoldingsOf(shops: 1));

        state.Economy.Treasury.Loan.Principal = Money.FromGuan(1m);
        state.Achievements.RecordLoanOriginated();

        // 结算之前：有借贷历史但尚未清偿 ⇒ 无债一身轻尚未解锁。
        Assert.Empty(CompletedOf(state, AchievementId.DebtFree));

        var outcome = new TurnRunner(
            new FixedRandomService(), null, new SaveSlot("自动档"), new FixedNameGenerator()).Advance(state);

        Assert.True(state.Economy.Treasury.Loan.IsSettled);
        Assert.Equal(AchievementId.DebtFree, MarkOf(state, AchievementId.DebtFree).Achievement);
        Assert.Single(CompletedOf(state, AchievementId.DebtFree));

        // 未配置存档服务 ⇒ 本回合如实报告「没写档」。
        Assert.False(outcome.Saved);
    }

    [Fact]
    public void 触发器六托媒成功即时判定人丁兴旺()
    {
        var members = Adults(19);
        var member = members[0];
        var state = StateOf(RulesHarness.FamilyOf(members), Money.FromGuan(10_000m));

        var outcome = LifecycleEntry.TryMatchmake(
            state,
            member,
            MarriageDirection.BrideInto,
            new FixedRandomService(0.1),  // arch-guard:allow 夹具随机取值（非规则数值副本）
            new FixedNameGenerator());

        Assert.True(outcome.Accepted);
        Assert.Equal(20, RegisteredOf(state.Family));
        Assert.Equal(AchievementId.LargeHousehold, MarkOf(state, AchievementId.LargeHousehold).Achievement);
        Assert.Single(CompletedOf(state, AchievementId.LargeHousehold));

        var again = AchievementAdjudicator.Report(state, AchievementTrigger.MatchmakingCommenced);

        Assert.Contains(AchievementId.LargeHousehold, again.AlreadyCompleted);
        Assert.Single(CompletedOf(state, AchievementId.LargeHousehold));
    }

    [Fact]
    public void 触发器七应答提亲成功即时判定人丁兴旺()
    {
        var members = Adults(19);
        var member = members[0];
        var family = RulesHarness.FamilyOf(members);
        var state = StateOf(family, Money.FromGuan(10_000m));

        family.Lifecycle.SetProposal(ProposalFor(member.Id, state.CurrentDate, DegreeLevel.BaiShen));

        var outcome = LifecycleEntry.RespondToProposal(
            state, accept: true, new FixedRandomService(), new FixedNameGenerator());

        Assert.True(outcome.Accepted);
        Assert.Equal(20, RegisteredOf(state.Family));
        Assert.Equal(AchievementId.LargeHousehold, MarkOf(state, AchievementId.LargeHousehold).Achievement);
        Assert.Single(CompletedOf(state, AchievementId.LargeHousehold));

        var again = AchievementAdjudicator.Report(state, AchievementTrigger.ProposalAnswered);

        Assert.Contains(AchievementId.LargeHousehold, again.AlreadyCompleted);
        Assert.Single(CompletedOf(state, AchievementId.LargeHousehold));
    }

    [Fact]
    public void 触发器八买入功名婚姻成功即时判定()
    {
        var members = Adults(19);
        var member = RulesHarness.Official(1, 9);
        members[0] = member;
        member.AppendDegree(new DegreeRecord(
            DegreeLevel.GongShi, null, RulesHarness.Date, DegreeChangeCause.Initial));

        var family = RulesHarness.FamilyOf(members);
        var state = StateOf(family, Money.FromGuan(10_000m));
        family.Lifecycle.SetProposal(ProposalFor(member.Id, state.CurrentDate, DegreeLevel.BaiShen));

        var outcome = LifecycleEntry.RespondToProposal(
            state, accept: true, new FixedRandomService(), new FixedNameGenerator(), buyDegreeMarriage: true);

        Assert.True(outcome.Accepted);
        Assert.Equal(20, RegisteredOf(state.Family));

        // 「确实发生了买功名婚姻」的两条硬证据：功名费已付 + 新人自带更低一档功名（贡士 → 举人）。
        Assert.Contains(
            state.Economy.Ledger.Entries, entry => entry.Category == LedgerCategory.DegreeMarriageFeePaid);
        Assert.Equal(DegreeLevel.JuRen, LastMember(state.Family).CurrentDegree);

        Assert.Equal(AchievementId.LargeHousehold, MarkOf(state, AchievementId.LargeHousehold).Achievement);
        Assert.Single(CompletedOf(state, AchievementId.LargeHousehold));

        var again = AchievementAdjudicator.Report(state, AchievementTrigger.DegreeMarriagePurchased);

        Assert.Contains(AchievementId.LargeHousehold, again.AlreadyCompleted);
        Assert.Single(CompletedOf(state, AchievementId.LargeHousehold));
    }

    [Fact]
    public void 触发器九买人口成功即时判定人丁兴旺()
    {
        var members = Adults(19);
        var state = StateOf(RulesHarness.FamilyOf(members), Money.FromGuan(10_000m));

        var outcome = LifecycleEntry.BuyPerson(state, new FixedRandomService(), new FixedNameGenerator());

        Assert.True(outcome.Accepted);
        Assert.Equal(20, RegisteredOf(state.Family));
        Assert.Equal(AchievementId.LargeHousehold, MarkOf(state, AchievementId.LargeHousehold).Achievement);
        Assert.Single(CompletedOf(state, AchievementId.LargeHousehold));

        var again = AchievementAdjudicator.Report(state, AchievementTrigger.PersonPurchased);

        Assert.Contains(AchievementId.LargeHousehold, again.AlreadyCompleted);
        Assert.Single(CompletedOf(state, AchievementId.LargeHousehold));
    }

    [Fact]
    public void 触发器十行贿提交成功即时判定且当月事实可解锁投石问路()
    {
        var examinee = RulesHarness.Member(1, Gender.Male, 30);
        examinee.Health = (int)GameConfig.Attributes.HealthMean;
        examinee.AppendDegree(new DegreeRecord(
            DegreeLevel.JuRen, null, new GameDate(80, 2), DegreeChangeCause.Initial));

        var relative = RulesHarness.Member(2, Gender.Male, 28);
        relative.Health = (int)GameConfig.Attributes.HealthMean;
        var state = StateOf(
            RulesHarness.FamilyOf(examinee, relative), Money.FromGuan(10_000m), date: new GameDate(80, 2));

        // 省试在 2 年 3 月，窗口 = 考试当月 + 前 1 个月 ⇒ (80, 2) 在窗口内。
        var bribe = BribeEntry.Bribe(state, examinee, ExamLevel.ShengShi, new FixedRandomService());

        Assert.True(bribe.Accepted);
        Assert.Single(CompletedOf(state, AchievementId.FoundationLaid));

        // 月末判定把「当月行贿事实」吸收为可直接解锁的 ACH-049（投石问路）。
        new TurnRunner(
            new FixedRandomService(), null, new SaveSlot("自动档"), new FixedNameGenerator()).Advance(state);

        Assert.Equal(AchievementId.FirstSuccessfulBribe, MarkOf(state, AchievementId.FirstSuccessfulBribe).Achievement);
        Assert.Single(CompletedOf(state, AchievementId.FirstSuccessfulBribe));

        var again = AchievementAdjudicator.Report(state, AchievementTrigger.BribeSubmitted);

        Assert.Contains(AchievementId.FoundationLaid, again.AlreadyCompleted);
        Assert.Single(CompletedOf(state, AchievementId.FoundationLaid));
    }

    [Fact]
    public void 触发器十一特奏名应答成功即时判定皓首穷经()
    {
        var elder = RulesHarness.Member(1, Gender.Male, 55);
        for (var index = 0; index < ExamPolicy.SpecialTributeFailureThreshold; index++)
        {
            elder.RecordExamFailure(ExamLevel.ShengShi);
        }

        var state = StateOf(
            RulesHarness.FamilyOf(elder, RulesHarness.Member(2, Gender.Male, 30)), Money.FromGuan(100m));

        ExamRegistration.AcceptSpecialTribute(
            elder, state.CurrentDate, new FixedRandomService(), state);

        Assert.Equal(AchievementId.VenerableScholar, MarkOf(state, AchievementId.VenerableScholar).Achievement);
        Assert.Single(CompletedOf(state, AchievementId.VenerableScholar));

        var again = AchievementAdjudicator.Report(state, AchievementTrigger.ExamInquiryAnswered);

        Assert.Contains(AchievementId.VenerableScholar, again.AlreadyCompleted);
        Assert.Single(CompletedOf(state, AchievementId.VenerableScholar));
    }

    [Fact]
    public void 触发器十二调试唤醒即时判定且累加与判定同一次调用()
    {
        var state = StateOf(RulesHarness.FamilyOf(RulesHarness.Member(1, Gender.Male, 40)), Money.FromGuan(100m));

        Assert.Equal(1, AchievementCounters.ReportsDebugWake(state, 1));
        Assert.Equal(AchievementId.DebugWakeOne, MarkOf(state, AchievementId.DebugWakeOne).Achievement);
        Assert.Single(CompletedOf(state, AchievementId.DebugWakeOne));

        // 累加只增不清零：再唤醒 9 次 ⇒ 10 次，第二档成就同样**在同一次调用里**判定。
        Assert.Equal(10, AchievementCounters.ReportsDebugWake(state, 9));
        Assert.Equal(AchievementId.DebugWakeTen, MarkOf(state, AchievementId.DebugWakeTen).Achievement);

        var again = AchievementAdjudicator.Report(state, AchievementTrigger.DebugWakeReported);

        Assert.Contains(AchievementId.DebugWakeOne, again.AlreadyCompleted);
        Assert.Single(CompletedOf(state, AchievementId.DebugWakeOne));
    }

    [Fact]
    public void 被拒路径资产金额不足不触发()
    {
        var state = StateOf(RulesHarness.FamilyOf(RulesHarness.Member(1, Gender.Male, 40)), Money.FromGuan(1m));

        Assert.Throws<InvalidOperationException>(() => AssetMarket.Buy(state, AssetKind.Farmland, 100));

        Assert.Empty(state.Achievements.Completed);
        Assert.Empty(state.Achievements.PendingUnlocks);
    }

    [Fact]
    public void 被拒路径托媒金额不足不触发()
    {
        var members = Adults(19);
        var state = StateOf(RulesHarness.FamilyOf(members), Money.Zero);

        var outcome = LifecycleEntry.TryMatchmake(
            state,
            members[0],
            MarriageDirection.BrideInto,
            new FixedRandomService(0.1),  // arch-guard:allow 夹具随机取值（非规则数值副本）
            new FixedNameGenerator());

        Assert.False(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.InsufficientFunds, outcome.RejectReason);
        Assert.Equal(19, RegisteredOf(state.Family));
        Assert.Empty(state.Achievements.Completed);
        Assert.Empty(state.Achievements.PendingUnlocks);
    }

    [Fact]
    public void 被拒路径行贿窗口未开不触发()
    {
        var examinee = RulesHarness.Member(1, Gender.Male, 30);
        examinee.AppendDegree(new DegreeRecord(
            DegreeLevel.JuRen, null, RulesHarness.Date, DegreeChangeCause.Initial));

        // (80, 1)：省试窗口从 (80, 2) 起 ⇒ 此刻窗口未开。
        var state = StateOf(RulesHarness.FamilyOf(examinee), Money.FromGuan(10_000m));

        var bribe = BribeEntry.Bribe(state, examinee, ExamLevel.ShengShi, new FixedRandomService());

        Assert.False(bribe.Accepted);
        Assert.Equal(BribeRejectReason.OutsideWindow, bribe.RejectReason);
        Assert.Empty(state.Achievements.Completed);
        Assert.Empty(state.Achievements.PendingUnlocks);
    }

    [Fact]
    public void 被拒路径无待应答提议不触发()
    {
        var state = StateOf(RulesHarness.FamilyOf(RulesHarness.Member(1, Gender.Male, 40)), Money.FromGuan(10_000m));

        var outcome = LifecycleEntry.RespondToProposal(
            state, accept: true, new FixedRandomService(), new FixedNameGenerator());

        Assert.False(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.NoPendingProposal, outcome.RejectReason);
        Assert.Empty(state.Achievements.Completed);
        Assert.Empty(state.Achievements.PendingUnlocks);
    }

    [Fact]
    public void 被拒路径新建存档起始年月非法不触发()
    {
        Assert.Throws<ArgumentException>(() => NewGameSetup.Create(
            new NewGameRequest(Origin.Scholar, Difficulty.Normal, "赵", new GameDate(2, 1)),
            new SeededRandomService(20261018),
            new FixedNameGenerator("赵")));
    }

    [Fact]
    public void 被拒路径调试唤醒次数为零不判定也不累加()
    {
        var state = StateOf(RulesHarness.FamilyOf(RulesHarness.Member(1, Gender.Male, 40)), Money.FromGuan(100m));

        Assert.Throws<ArgumentOutOfRangeException>(() => AchievementCounters.ReportsDebugWake(state, 0));

        Assert.Equal(0, state.Achievements.DebugWakeCount);
        Assert.Empty(state.Achievements.Completed);
        Assert.Empty(state.Achievements.PendingUnlocks);
    }

    [Fact]
    public void 无触发路径None与已结束存档都不判定()
    {
        var state = StateOf(RulesHarness.FamilyOf(RulesHarness.Member(1, Gender.Male, 40)), Money.FromGuan(10_000m));

        // `None == 0` = 「非触发路径」：不判、不标、不抛。
        var none = AchievementAdjudicator.Report(state, AchievementTrigger.None);

        Assert.Empty(none.NewlyUnlocked);
        Assert.Empty(none.AlreadyCompleted);
        Assert.Empty(state.Achievements.Completed);

        state.Ending.End(state.CurrentDate, ChronicleBuilder.Build(state));

        var ended = AchievementAdjudicator.Report(state, AchievementTrigger.AssetPurchased);

        Assert.Empty(ended.NewlyUnlocked);
        Assert.Empty(ended.AlreadyCompleted);
        Assert.False(ended.ExtinctionJustTriggered);
        Assert.Empty(state.Achievements.Completed);
        Assert.Empty(state.Achievements.PendingUnlocks);
    }

    [Fact]
    public void 即时判定不落全局文件不写档且不抛异常()
    {
        // 记录器**确实接在回合推进上**：这样下面「0 次读写」才是「即时判定没碰全局文件」，
        // 而不是「根本没人接存储」。
        var store = new RecordingAchievementStore();
        var state = StateOf(RulesHarness.FamilyOf(RulesHarness.Member(1, Gender.Male, 40)), Money.FromGuan(10_000m));
        var runner = new TurnRunner(
            new FixedRandomService(), null, new SaveSlot("自动档"), new FixedNameGenerator(), store);

        AssetMarket.Buy(state, AssetKind.Farmland, 100);
        AssetMarket.Sell(state, AssetKind.Farmland, 1);
        _ = AchievementAdjudicator.Report(state, AchievementTrigger.NewGameCreated);
        _ = AchievementCounters.ReportsDebugWake(state, 1);

        // 即时判定三连（买入 / 卖出 / 调试唤醒）全程不读、不写全局成就文件。
        Assert.Equal(0, store.ReadCount);
        Assert.Equal(0, store.FlushCount);
        Assert.NotEmpty(state.Achievements.PendingUnlocks);

        // 未配置存档服务 ⇒ 回合推进照样完成，但不写档；成就落盘只发生在回合末。
        var outcome = runner.Advance(state);

        Assert.False(outcome.Saved);
        Assert.False(outcome.BackedUp);
        Assert.NotNull(outcome.AchievementFlush);
        Assert.Equal(1, store.FlushCount);
        Assert.Equal(1, store.ReadCount);
    }

    /// <summary>构造只有一名成年男性成员的状态（默认 Normal / Scholar / 参照年月）。</summary>
    /// <param name="family">家族。</param>
    /// <param name="cash">现金（贯）。</param>
    /// <param name="date">当前年月；<c>null</c> ⇒ <see cref="RulesHarness.Date"/>。</param>
    /// <param name="holdings">资产；<c>null</c> ⇒ 空资产。</param>
    private static GameState StateOf(Family family, Money cash, GameDate? date = null, Holdings? holdings = null)
    {
        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(cash),
            holdings ?? new Holdings());

        return new GameState(
            StateId, date ?? RulesHarness.Date, Difficulty.Normal, Origin.Scholar, family, economy);
    }

    /// <summary>构造 <paramref name="count"/> 名成年男性成员（下标从 1 起，年龄 20）。</summary>
    /// <param name="count">人数。</param>
    private static Person[] Adults(int count)
    {
        var members = new Person[count];

        for (var index = 0; index < count; index++)
        {
            members[index] = RulesHarness.Member(index + 1, Gender.Male, 20);
        }

        return members;
    }

    /// <summary>构造一份娶入用的当月提议快照（白身候选者、双方金额均为 0）。</summary>
    /// <param name="memberId">我方参与婚配的成员。</param>
    /// <param name="date">提出年月（须等于当月，否则提议已失效）。</param>
    /// <param name="degree">候选者功名。</param>
    private static MarriageProposal ProposalFor(PersonId memberId, GameDate date, DegreeLevel degree) => new()
    {
        Kind = MarriageProposalKind.Matchmaker,
        OurMemberId = memberId,
        CandidateName = "测婉",
        CandidateGender = Gender.Female,
        CandidateAge = 18,
        TalentAgriculture = 50,
        TalentCommerce = 50,
        TalentScholar = 50,
        TalentCraft = 50,
        CandidateStudy = 30,
        CandidateHealth = 80,
        CandidateLifespan = 70,
        CandidateDegree = degree,
        Class = MarriageClass.Commoner,
        BetrothalGift = Money.Zero,
        Dowry = Money.Zero,
        ProposedAt = date,
    };

    /// <summary>取指定成就的完成标记（不存在即失败——「至少一条直接受益成就被解锁」即此断言）。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="id">成就标识。</param>
    private static AchievementMark MarkOf(GameState state, AchievementId id)
    {
        var marks = CompletedOf(state, id);

        if (marks.Count == 0)
        {
            throw new InvalidOperationException($"成就 {id} 未被即时判定解锁。");
        }

        return marks[0];
    }

    /// <summary>取指定成就的**全部**完成标记（用于「只留一条」的幂等断言）。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="id">成就标识。</param>
    private static List<AchievementMark> CompletedOf(GameState state, AchievementId id)
    {
        var marks = new List<AchievementMark>();

        foreach (var mark in state.Achievements.Completed)
        {
            if (mark.Achievement == id)
            {
                marks.Add(mark);
            }
        }

        return marks;
    }

    /// <summary>取家族的在册成员数（婚嫁 / 买人口后「人丁兴旺」的判据口径）。</summary>
    /// <param name="family">家族。</param>
    private static int RegisteredOf(Family family) => family.RegisteredMembers.Count;

    /// <summary>取家族中最后加入的成员（提议候选者 / 买来者）。</summary>
    /// <param name="family">家族。</param>
    private static Person LastMember(Family family)
    {
        Person? last = null;

        foreach (var member in family.Members)
        {
            last = member;
        }

        return last ?? throw new InvalidOperationException("家族中没有任何成员。");
    }
}
