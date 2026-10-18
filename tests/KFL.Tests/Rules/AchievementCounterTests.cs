using System.Reflection;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Achievements;
using KFL.Infrastructure.Services;
using KFL.Rules.Achievements;
using KFL.Rules.Config;
using KFL.Rules.Economy;
using KFL.Rules.Settlement;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// T046 / FR-013、SC-014：`AchievementState` 六个写通道的**更新时点**与**增量语义**，
/// 以及 §2.4「不可重算」证明表的逐项落地。
/// </summary>
/// <remarks>
/// ①~⑤ 的「同月同步」一律以**真实**的 <see cref="MonthlySettlementEngine.Settle"/> 为事实来源
/// （MUST NOT 手搓 <see cref="SettlementResult"/>）；⑥ 只经 <c>ReportsDebugWake</c> 上报推进，
/// 月结算 MUST NOT 触碰它。期望值一律经 <c>KFL.Rules/Config</c> 与
/// <c>KFL.Rules/Achievements</c> 的成员取得，本文件 MUST NOT 另存规则数值（SC-008）。
/// </remarks>
public class AchievementCounterTests
{
    private static readonly Guid StateId = Guid.Parse("3f7c1a52-9d84-4b16-8e27-5a0b6c7d8e9f");

    /// <summary>六项累计量所在的类型：它们是**唯一**的持有者（SC-014 禁止第九项、也禁止别处副本）。</summary>
    private static readonly string[] OwnedByAchievementState =
    [
        "SuccessfulBribeCount",
        "BirthCount",
        "Lifespans",
        "PeakTotalAssets",
        "EverHadLoan",
        "DebugWakeCount",
    ];

    [Fact]
    public void 当月事实在判定的同一次调用里吸收且不过夜()
    {
        var state = StateOf(Money.FromGuan(40_000m));
        var before = state.Achievements.PeakTotalAssets;

        var settlement = Settle(state);

        // 判定之前：本月事实尚未吸收（累计量为「上月末」的口径）。
        Assert.Equal(Money.Zero, before);

        AchievementAdjudicator.Judge(state, settlement, [], []);

        // ① 成功行贿 = 当月**未被抓**的条数（不是 `Person` 上的混计计数、也不是 006 的「尝试次数」）。
        var succeeded = 0;
        foreach (var bribe in settlement.Punishment.Bribes)
        {
            if (!bribe.Caught)
            {
                succeeded++;
            }
        }

        Assert.Equal(succeeded, state.Achievements.SuccessfulBribeCount);
        Assert.Equal(settlement.Lifecycle.Births.Count, state.Achievements.BirthCount);
        Assert.Equal(settlement.Lifecycle.Deaths.Count, state.Achievements.Lifespans.Count);

        // ④ 峰值 = 本月的总资产口径，且与 `TotalAssetsCalculator` 同源。
        Assert.Equal(TotalAssetsCalculator.Of(state.Economy), state.Achievements.PeakTotalAssets);

        // ⑤ 未清偿的贷款在月结算里被观察为「产生过贷款」。
        Assert.Equal(!state.Economy.Treasury.Loan.IsSettled, state.Achievements.EverHadLoan);
    }

    [Fact]
    public void 享年条数与历史死亡人数相等且逐条记录事实月年龄()
    {
        var state = StateOf(Money.FromGuan(40_000m));
        var total = 0;

        for (var month = 0; month < 3; month++)
        {
            var settlement = Settle(state);
            AchievementAdjudicator.Judge(state, settlement, [], []);

            // ③ 的完整序列语义：条数 == 历史（累计）死亡人数，而不是「当月」或「当前已亡人数」。
            total += settlement.Lifecycle.Deaths.Count;
            Assert.Equal(total, state.Achievements.Lifespans.Count);

            // 每条享年都是**事实月**的周岁（死亡年龄不可由 `Person.AgeAt(今天)` 复原）。
            foreach (var death in settlement.Lifecycle.Deaths)
            {
                var deceased = Find(state, death.PersonId);

                Assert.NotNull(deceased);
                Assert.Contains(deceased!.AgeAt(settlement.Month), state.Achievements.Lifespans);
            }
        }
    }

    [Fact]
    public void 增量通道返回累计值并拒绝负增量()
    {
        var achievements = new AchievementState();

        Assert.Equal(3, achievements.RecordSuccessfulBribes(3));
        Assert.Equal(5, achievements.RecordSuccessfulBribes(2));
        Assert.Equal(5, achievements.SuccessfulBribeCount);

        Assert.Equal(2, achievements.RecordBirths(2));
        Assert.Equal(2, achievements.RecordBirths(0));

        Assert.Equal(4, achievements.ReportDebugWake(4));
        Assert.Equal(5, achievements.ReportDebugWake());

        Assert.Throws<ArgumentOutOfRangeException>(() => achievements.RecordSuccessfulBribes(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => achievements.RecordBirths(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => achievements.AppendLifespan(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => achievements.ReportDebugWake(-1));

        // 被拒的增量 MUST NOT 改变累计量。
        Assert.Equal(5, achievements.SuccessfulBribeCount);
        Assert.Equal(2, achievements.BirthCount);
        Assert.Equal(5, achievements.DebugWakeCount);
        Assert.Empty(achievements.Lifespans);
    }

    [Fact]
    public void 寿命逐个追加而不合并为合计()
    {
        var achievements = new AchievementState();

        achievements.AppendLifespan(70);
        achievements.AppendLifespan(40);
        achievements.AppendLifespan(55);

        // 次序即事实次序（存档往返仍逐位相等，见 SC-011）；三均值口径依赖这三次独立追加。
        Assert.Equal([70, 40, 55], achievements.Lifespans);
    }

    [Fact]
    public void 总资产只记历史峰值且负值夹零()
    {
        var achievements = new AchievementState();

        achievements.ObserveTotalAssets(Money.FromGuan(1_000m));
        achievements.ObserveTotalAssets(Money.FromGuan(50m));

        Assert.Equal(Money.FromGuan(1_000m), achievements.PeakTotalAssets);

        achievements.ObserveTotalAssets(Money.FromGuan(-5m));

        // ④ 的夹零口径：负值按 `Money.Zero` 处理，峰值不因负值而下降或变负。
        Assert.Equal(Money.FromGuan(1_000m), achievements.PeakTotalAssets);

        var empty = new AchievementState();
        empty.ObserveTotalAssets(Money.FromGuan(-5m));

        Assert.Equal(Money.Zero, empty.PeakTotalAssets);
    }

    [Fact]
    public void 贷款标记幂等且清偿后不清零()
    {
        var state = StateOf(Money.FromGuan(1_000m));

        state.Achievements.RecordLoanOriginated();
        state.Achievements.RecordLoanOriginated();

        Assert.True(state.Achievements.EverHadLoan);

        // 真的借一笔、再全额清偿：`IsSettled` 不再能证明「曾经有过」。
        var loan = state.Economy.Treasury.Loan;
        loan.Principal = Money.FromGuan(200m);

        var settlement = Settle(state);
        AchievementAdjudicator.Judge(state, settlement, [], []);
        Assert.True(state.Achievements.EverHadLoan);

        state.Economy.RepayLoan(loan.Total, state.CurrentDate);
        Assert.True(loan.IsSettled);

        var after = Settle(state);
        AchievementAdjudicator.Judge(state, after, [], []);

        // ⑤ 是**曾经**的标记：清偿（`IsSettled == true`）MUST NOT 把它清掉。
        Assert.True(state.Achievements.EverHadLoan);
    }

    [Fact]
    public void 调试唤醒只经上报路径推进且月结算不触碰()
    {
        var state = StateOf(Money.FromGuan(40_000m));
        var settlement = Settle(state);

        AchievementAdjudicator.Judge(state, settlement, [], []);

        // ⑥ 与 ①~⑤ 不同：月结算 MUST NOT 重算或推进它（FR-041 ⑥ 是**外部上报**的接缝）。
        Assert.Equal(0, state.Achievements.DebugWakeCount);

        var cumulative = AchievementCounters.ReportsDebugWake(state, AchievementPolicy.DebugWakeTen);

        Assert.Equal(AchievementPolicy.DebugWakeTen, cumulative);
        Assert.True(state.Achievements.PendingUnlocks.Count > 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => AchievementCounters.ReportsDebugWake(state, 0));
        Assert.Equal(AchievementPolicy.DebugWakeTen, state.Achievements.DebugWakeCount);
    }

    [Fact]
    public void 六项累计量只由成就状态持有()
    {
        // ① ② ⑥ 的「不可重算」：`Person` / `Family` 根本没有同义成员可查
        //（① 的 `JieShi/ShengShi/DianShiBribeCount` 混计成功与被抓，且不含家族合计口径）。
        var personNames = PropertyNames(typeof(Person));
        var familyNames = PropertyNames(typeof(Family));

        foreach (var name in OwnedByAchievementState)
        {
            Assert.DoesNotContain(name, personNames);
            Assert.DoesNotContain(name, familyNames);
        }

        // ② 的另一半：开局成员与买来的人口都进 `Family.Members`，故成员数推不出「出生数」。
        var state = StateOf(Money.FromGuan(40_000m));

        Assert.True(state.Family.Members.Count > 0);
        Assert.Equal(0, state.Achievements.BirthCount);

        // ③ 的另一半：`AgeAt` 是「现在几岁」，死亡年龄不可复原。
        var member = RulesHarness.Member(9, Gender.Male, 40);
        var achievements = new AchievementState();
        achievements.AppendLifespan(30);

        Assert.Equal(40, member.AgeAt(RulesHarness.Date));
        Assert.Equal([30], achievements.Lifespans);
    }

    [Fact]
    public void 完成记录与待落盘队列不可由其它数据推出()
    {
        var state = StateOf(Money.FromGuan(40_000m));
        var mark = new AchievementMark(AchievementId.FoundationLaid, Difficulty.Normal, state.CurrentDate);

        Assert.True(state.Achievements.MarkCompletedAndEnqueue(mark, state.Id));

        // ⑦ 在**档内**记录：全局档案为空的读档态仍能判「本档已解锁」（幂等的根据）。
        Assert.Contains(state.Achievements.Completed, item => item.Achievement == AchievementId.FoundationLaid);
        Assert.Contains(state.Achievements.PendingUnlocks, unlock => unlock.Achievement == AchievementId.FoundationLaid);

        var second = AchievementAdjudicator.Judge(state, Settle(state), [], []);
        Assert.Contains(AchievementId.FoundationLaid, second.AlreadyCompleted);

        // ⑧ 是「已判定但尚未落全局」的独立状态：清空它 MUST NOT 影响 ⑦。
        var completedBefore = state.Achievements.Completed.Count;
        state.Achievements.RestorePendingUnlocks([]);

        Assert.Empty(state.Achievements.PendingUnlocks);
        Assert.Equal(completedBefore, state.Achievements.Completed.Count);

        // 幂等：键已存在 ⇒ `false` 且**不入队**（队列保持为空）。
        Assert.False(state.Achievements.MarkCompletedAndEnqueue(mark, state.Id));
        Assert.Empty(state.Achievements.PendingUnlocks);
    }

    [Fact]
    public void 两条时点特例的口径各不落后一拍()
    {
        // ACH-042（债台高筑）：状态面 `Loan.Principal` 上的**借款当刻**即判，门槛为严格大于。
        var state = StateOf(Money.FromGuan(40_000m));
        var loan = state.Economy.Treasury.Loan;
        var context = new AchievementContext(state, null, [], [], state.CurrentDate);

        loan.Principal = Money.FromGuan(AchievementPolicy.DebtBurdenGuan);
        Assert.False(AchievementPredicates.IsSatisfied(AchievementId.DebtBurden, context));

        loan.Principal = Money.FromGuan(AchievementPolicy.DebtBurdenGuan + 1m);
        Assert.True(AchievementPredicates.IsSatisfied(AchievementId.DebtBurden, context));

        // ACH-034（榜下捉婿）：**待应答**提议（尚未被接受或拒绝）即已算「见过」——
        // 产出月与判定同在一次 `Advance` 内完成，玩家动作在其后，故「含被拒」天然成立。
        var member = state.Family.Members.First();
        state.Family.Lifecycle.SetProposal(ImperialProposal(member.Id));

        Assert.True(AchievementPredicates.IsSatisfied(AchievementId.ImperialGroomPick, context));

        state.Family.Lifecycle.SetProposal(null);

        // 提议被应答后状态面清空，此时「见过」只可能来自当月快照；本断言证明谓词**不是**只读状态面。
        Assert.False(AchievementPredicates.IsSatisfied(AchievementId.ImperialGroomPick, context));
    }

    /// <summary>构造一名成年男性成员的状态。</summary>
    /// <param name="cash">现金（贯）。</param>
    private static GameState StateOf(Money cash)
    {
        var family = RulesHarness.FamilyOf(RulesHarness.Member(1, Gender.Male, 40));
        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(cash),
            new Holdings());

        return new GameState(StateId, RulesHarness.Date, Difficulty.Normal, Origin.Scholar, family, economy);
    }

    /// <summary>构造一条「榜下捉婿」待应答提议。</summary>
    /// <param name="ourMemberId">本方成员。</param>
    private static MarriageProposal ImperialProposal(PersonId ourMemberId) =>
        new()
        {
            Kind = MarriageProposalKind.ImperialProposal,
            OurMemberId = ourMemberId,
            CandidateName = "宗室女",
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
            BetrothalGift = Money.Zero,
            Dowry = Money.Zero,
            ProposedAt = RulesHarness.Date,
        };

    /// <summary>跑一次真实结算，作为累计量的当月事实来源。</summary>
    /// <param name="state">游戏状态。</param>
    private static SettlementResult Settle(GameState state)
    {
        var engine = new MonthlySettlementEngine(
            new FixedRandomService(), new GameStateClock(state), new FixedNameGenerator());

        return engine.Settle(state);
    }

    /// <summary>在全量成员里按标识查找。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="id">成员标识。</param>
    private static Person? Find(GameState state, PersonId id)
    {
        foreach (var member in state.Family.Members)
        {
            if (member.Id == id)
            {
                return member;
            }
        }

        return null;
    }

    /// <summary>取类型的公开属性名（用于「别处没有同义成员」的结构性断言）。</summary>
    /// <param name="type">目标类型。</param>
    private static string[] PropertyNames(Type type) =>
        [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name)];
}
