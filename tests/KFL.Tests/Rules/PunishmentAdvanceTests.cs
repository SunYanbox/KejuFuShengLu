using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;
using KFL.Rules.Punishment;
using KFL.Rules.Settlement;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// T036（US3）：惩罚推进（结算第③步，契约十六 §1.2）——服刑 / 禁考 / 禁升三个计时逐月递减、
/// 归零处理（释放与解除）、释放降级、服刑者的计口口径与「不计口」语义。
/// </summary>
/// <remarks>
/// 全部断言都直接调用 <see cref="PunishmentAdvance.Run"/>（本步不接随机源，故随机消费结构性为 0）。
/// </remarks>
public class PunishmentAdvanceTests
{
    private static readonly Guid StateId = Guid.Parse("9d4c7b16-5e28-4a37-8c0f-1b2d3e4f5061");

    [Fact]
    public void 服刑六十月逐月递减后释放并降一级官阶()
    {
        var prisoner = RulesHarness.Official(1, 13, monthsInOffice: 10, merit: 5);
        RulesHarness.ServingSentence(prisoner, 60);
        var state = NewState(RulesHarness.FamilyOf(prisoner));

        for (var elapsed = 1; elapsed < 60; elapsed++)
        {
            var snapshot = PunishmentAdvance.Run(state);

            Assert.Equal(60 - elapsed, prisoner.Timers.SentenceRemainingMonths.GetValueOrDefault(-1));
            Assert.True(prisoner.Status.HasFlag(StatusFlag.ServingSentence));
            Assert.Equal(1, snapshot.PrisonerCount);
            Assert.Empty(snapshot.Releases);
        }

        var release = PunishmentAdvance.Run(state);

        // 释放：清计时与状态位、降一级官阶（L13 → L14）。
        Assert.False(prisoner.Status.HasFlag(StatusFlag.ServingSentence));
        Assert.Null(prisoner.Timers.SentenceRemainingMonths);
        Assert.Equal(14, prisoner.Rank?.Level);
        Assert.Equal(0, release.PrisonerCount);

        var fact = Assert.Single(release.Releases);
        Assert.Equal(prisoner.Id, fact.Subject);
        Assert.Equal(14, fact.NewRankLevel);
        Assert.Equal(RulesHarness.Date, fact.Date);

        // 服刑期、政绩与在职月数不受推进影响：待阙与政绩由其它步骤单独推进。
        Assert.Equal(10, prisoner.MonthsInOffice);
        Assert.Equal(5, prisoner.Merit);
    }

    [Fact]
    public void 已是最低官阶L18者释放后维持L18()
    {
        var prisoner = RulesHarness.Official(1, SalaryTable.LowestLevel);
        RulesHarness.ServingSentence(prisoner, 1);
        var state = NewState(RulesHarness.FamilyOf(prisoner));

        var snapshot = PunishmentAdvance.Run(state);

        Assert.Equal(SalaryTable.LowestLevel, prisoner.Rank?.Level);
        Assert.Equal(SalaryTable.LowestLevel, Assert.Single(snapshot.Releases).NewRankLevel);
    }

    [Fact]
    public void 无官职者释放后不产生官阶变动()
    {
        var prisoner = RulesHarness.Member(1, Gender.Male, 30);
        RulesHarness.ServingSentence(prisoner, 1);
        var state = NewState(RulesHarness.FamilyOf(prisoner));

        var snapshot = PunishmentAdvance.Run(state);

        Assert.Null(prisoner.Rank);
        Assert.Null(Assert.Single(snapshot.Releases).NewRankLevel);
    }

    [Fact]
    public void 服刑期间待阙计时与政绩与在职月数逐月不变()
    {
        var prisoner = RulesHarness.Member(1, Gender.Male, 30);
        prisoner.Status |= StatusFlag.AwaitingPost;
        prisoner.Timers = new StatusTimers(awaitingPostRemainingMonths: 20);
        RulesHarness.ServingSentence(prisoner, 12);
        prisoner.Merit = 7;
        var state = NewState(RulesHarness.FamilyOf(prisoner));

        for (var elapsed = 0; elapsed < 9; elapsed++)
        {
            PunishmentAdvance.Run(state);

            // 本步只作用于服刑 / 禁考 / 禁升三个字段，MUST NOT 触碰待阙计时。
            Assert.Equal(20, prisoner.Timers.AwaitingPostRemainingMonths.GetValueOrDefault(-1));
            Assert.True(prisoner.Status.HasFlag(StatusFlag.AwaitingPost));
            Assert.Equal(7, prisoner.Merit);
        }

        Assert.Equal(3, prisoner.Timers.SentenceRemainingMonths.GetValueOrDefault(-1));
    }

    [Fact]
    public void 禁考与服刑独立并行且释放不提前清禁考()
    {
        var prisoner = RulesHarness.Member(1, Gender.Male, 30);
        Sentenced(prisoner, sentenceMonths: 60, examBanMonths: 48);
        var state = NewState(RulesHarness.FamilyOf(prisoner));

        for (var elapsed = 0; elapsed < 48; elapsed++)
        {
            PunishmentAdvance.Run(state);
        }

        // 禁考先期满：清计时与状态位，服刑仍在（剩 12 月）。
        Assert.False(prisoner.Status.HasFlag(StatusFlag.ExamBanned));
        Assert.Null(prisoner.Timers.ExamBanRemainingMonths);
        Assert.Equal(12, prisoner.Timers.SentenceRemainingMonths.GetValueOrDefault(-1));
        Assert.True(prisoner.Status.HasFlag(StatusFlag.ServingSentence));

        for (var elapsed = 0; elapsed < 12; elapsed++)
        {
            PunishmentAdvance.Run(state);
        }

        // 释放 MUST NOT 使已经期满的禁考「复活」。
        Assert.False(prisoner.Status.HasFlag(StatusFlag.ExamBanned));
        Assert.Null(prisoner.Timers.ExamBanRemainingMonths);
        Assert.False(prisoner.Status.HasFlag(StatusFlag.ServingSentence));
    }

    [Fact]
    public void 禁考期满不因释放而提前清零()
    {
        var prisoner = RulesHarness.Member(1, Gender.Male, 30);
        Sentenced(prisoner, sentenceMonths: 2, examBanMonths: 48);
        var state = NewState(RulesHarness.FamilyOf(prisoner));

        PunishmentAdvance.Run(state);
        PunishmentAdvance.Run(state);

        // 服刑 2 月即满 ⇒ 释放；禁考还剩 46 月，MUST NOT 被顺手清掉。
        Assert.False(prisoner.Status.HasFlag(StatusFlag.ServingSentence));
        Assert.True(prisoner.Status.HasFlag(StatusFlag.ExamBanned));
        Assert.Equal(46, prisoner.Timers.ExamBanRemainingMonths.GetValueOrDefault(-1));
    }

    [Fact]
    public void 禁升与服刑独立并行且各自期满()
    {
        var prisoner = RulesHarness.PromotionBannedOfficial(1, remainingMonths: 36, level: 9, monthsInOffice: 4, merit: 12);
        RulesHarness.ServingSentence(prisoner, 36);
        var state = NewState(RulesHarness.FamilyOf(prisoner));

        for (var elapsed = 0; elapsed < 35; elapsed++)
        {
            PunishmentAdvance.Run(state);
        }

        Assert.Equal(1, prisoner.Timers.PromotionBanRemainingMonths.GetValueOrDefault(-1));
        Assert.Equal(1, prisoner.Timers.SentenceRemainingMonths.GetValueOrDefault(-1));

        var snapshot = PunishmentAdvance.Run(state);

        Assert.False(prisoner.Status.HasFlag(StatusFlag.PromotionBanned));
        Assert.Null(prisoner.Timers.PromotionBanRemainingMonths);
        Assert.False(prisoner.Status.HasFlag(StatusFlag.ServingSentence));

        // 释放时官阶非空 ⇒ 降一级（L9 → L10）。
        Assert.Equal(10, Assert.Single(snapshot.Releases).NewRankLevel);
    }

    [Fact]
    public void 同月三人服刑时囚徒计数为三()
    {
        var first = RulesHarness.Member(1, Gender.Male, 30);
        var second = RulesHarness.Member(2, Gender.Male, 31);
        var third = RulesHarness.Member(3, Gender.Male, 32);
        var free = RulesHarness.Member(4, Gender.Male, 33);

        Sentenced(first, 5);
        Sentenced(second, 7);
        Sentenced(third, 9);
        var state = NewState(RulesHarness.FamilyOf(first, second, third, free));

        var snapshot = PunishmentAdvance.Run(state);

        Assert.Equal(3, snapshot.PrisonerCount);
        Assert.Equal(3, PunishmentQueries.PrisonerCount(state.Family, state.CurrentDate));

        // 服刑者**不计口**（不耗生活费、不产生收益与功名）但仍在家族列表中。
        Assert.Equal(4, state.Family.Members.Count);
        Assert.Same(free, Assert.Single(CountedMembers.Counted(state.Family)));
        Assert.Equal(4, CountedMembers.Registered(state.Family).Count);
    }

    [Fact]
    public void 投影并清空日志恰好一遍且释放事实不回写日志()
    {
        var prisoner = RulesHarness.Member(1, Gender.Male, 30);
        Sentenced(prisoner, 1);
        var family = RulesHarness.FamilyOf(prisoner);
        family.PunishmentJournal.Append(new SentenceFact(
            RulesHarness.Date, prisoner.Id, 1));
        var state = NewState(family);

        var first = PunishmentAdvance.Run(state);

        // 日志被投影进快照并**清空**（恰好一遍）：第二次推进不再重复上报同一行；
        // 服刑 1 月即满 ⇒ 释放事实落在**当次**快照。
        Assert.Single(first.Sentences);
        Assert.Single(first.Releases);
        Assert.True(state.Family.PunishmentJournal.IsEmpty);

        var second = PunishmentAdvance.Run(state);

        Assert.Empty(second.Sentences);
        Assert.Empty(second.Releases);

        // 释放事实只进本次快照，MUST NOT 回写日志（否则下月会重复上报并跨月错位）。
        Assert.True(state.Family.PunishmentJournal.IsEmpty);
    }

    [Fact]
    public void 已亡者不再推进计时也不产生释放事实()
    {
        var prisoner = RulesHarness.Member(1, Gender.Male, 30);
        Sentenced(prisoner, 1);
        var state = NewState(RulesHarness.FamilyOf(prisoner));
        prisoner.Status |= StatusFlag.Deceased;

        var snapshot = PunishmentAdvance.Run(state);

        Assert.Equal(1, prisoner.Timers.SentenceRemainingMonths.GetValueOrDefault(-1));
        Assert.Empty(snapshot.Releases);
        Assert.Equal(0, snapshot.PrisonerCount);
    }

    [Fact]
    public void 日志为空时投影六个集合皆为空集且标志为假()
    {
        var state = NewState(RulesHarness.FamilyOf(RulesHarness.Member(1, Gender.Male, 30)));

        var snapshot = PunishmentAdvance.Run(state);

        Assert.Empty(snapshot.Bribes);
        Assert.Empty(snapshot.Sentences);
        Assert.Empty(snapshot.Fines);
        Assert.Empty(snapshot.RelativeBans);
        Assert.Empty(snapshot.CollectivePunishments);
        Assert.Empty(snapshot.Releases);
        Assert.False(snapshot.FamilyBanDeclared);
        Assert.Equal(state.CurrentDate, snapshot.Month);
    }

    private static Person Sentenced(
        Person person, int sentenceMonths, int? examBanMonths = null, int? promotionBanMonths = null)
    {
        person.Status |= StatusFlag.ServingSentence;

        if (examBanMonths is not null)
        {
            person.Status |= StatusFlag.ExamBanned;
        }

        if (promotionBanMonths is not null)
        {
            person.Status |= StatusFlag.PromotionBanned;
        }

        person.Timers = new StatusTimers(sentenceMonths, examBanMonths, promotionBanMonths);
        return person;
    }

    private static GameState NewState(
        Family family, Difficulty difficulty = Difficulty.Normal, GameDate? date = null)
    {
        var month = date ?? RulesHarness.Date;
        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(),
            new Holdings());

        return new GameState(StateId, month, difficulty, Origin.Scholar, family, economy);
    }
}
