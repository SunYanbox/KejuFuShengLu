using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Bribery;
using KFL.Rules.Config;
using KFL.Rules.Punishment;
using KFL.Tests.Fixtures;
using System.Reflection;
using System.Text;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// T044 / US4 / SC-008：惩罚与行贿查询面的**纯度**（调用前后逐字段差异项 = 0）、
/// 囚犯计数的口径，以及「逻辑轨 ⑧ 解锁成就所需事实是否齐备且本特性**不解锁任何成就**」。
/// </summary>
/// <remarks>
/// 查询面八个入口一律是**纯派生值**：它们不接收 <c>IRandomService</c>，也不写回
/// <see cref="GameState"/>，故「随机消费 = 0」由签名即可判定（无随机入口）。
/// 期望值一律经 <c>KFL.Rules/Config</c> 的成员取得（SC-008）；本文件 MUST NOT 另存规则数值。
/// </remarks>
public class BribeQueryTests
{
    private static readonly Guid StateId = Guid.Parse("4a7b2c31-6d58-4e9f-8a0b-3c4d5e6f7a8b");

    /// <summary>参照年月：与 <see cref="RulesHarness.Date"/> 同一月。</summary>
    private static readonly GameDate Month = RulesHarness.Date;

    /// <summary>科举年 79 的解试行贿窗口首月（<c>ExamCalendar.IsKeYear(79)</c> 为真）。</summary>
    private static readonly GameDate JieShiWindowFirstMonth = new(79, 7);

    [Fact]
    public void 查询面八个入口都是纯函数且逐字段不变()
    {
        var examinee = RulesHarness.Member(1, Gender.Male, 20, officialdom: 80);
        var cleanJinShi = RulesHarness.Member(2, Gender.Male, 40, officialdom: 80);
        cleanJinShi.AppendDegree(new DegreeRecord(DegreeLevel.JinShi, null, Month, DegreeChangeCause.ExamPass));

        var family = RulesHarness.FamilyOf(examinee, cleanJinShi);
        var state = NewState(family, Difficulty.Normal, JieShiWindowFirstMonth);
        var before = Fingerprint(state);

        var window = PunishmentQueries.IsWithinBribeWindow(JieShiWindowFirstMonth, ExamLevel.JieShi);
        var feasibility = PunishmentQueries.Of(state, examinee, ExamLevel.JieShi);
        var capped = PunishmentQueries.IsLevelCapReached(state, examinee, ExamLevel.JieShi);
        var cumulative = PunishmentQueries.FamilyCumulativeBribes(state.Family);
        var clean = PunishmentQueries.WasBribeFreeAtImperialPass(cleanJinShi);
        var status = PunishmentQueries.StatusOf(examinee);
        var executed = PunishmentQueries.WasExecutedByState(cleanJinShi);
        var prisoners = PunishmentQueries.PrisonerCount(state.Family, state.CurrentDate);

        // 八个入口各调用一遍后，GameState 逐字段差异项 = 0。
        Assert.Equal(before, Fingerprint(state));

        // 重复调用给出相同结果（纯派生值，不含时间重放）。
        Assert.Equal(window, PunishmentQueries.IsWithinBribeWindow(JieShiWindowFirstMonth, ExamLevel.JieShi));
        Assert.Equal(feasibility, PunishmentQueries.Of(state, examinee, ExamLevel.JieShi));
        Assert.Equal(capped, PunishmentQueries.IsLevelCapReached(state, examinee, ExamLevel.JieShi));
        Assert.Equal(cumulative, PunishmentQueries.FamilyCumulativeBribes(state.Family));
        Assert.Equal(clean, PunishmentQueries.WasBribeFreeAtImperialPass(cleanJinShi));
        Assert.Equal(executed, PunishmentQueries.WasExecutedByState(cleanJinShi));
        Assert.Equal(status, PunishmentQueries.StatusOf(examinee));
        Assert.Equal(prisoners, PunishmentQueries.PrisonerCount(state.Family, state.CurrentDate));

        // 抽查口径：解试行贿窗口内、可受理、清白进士、无人在服刑、无人被处死。
        Assert.True(window);
        Assert.True(feasibility.IsFeasible);
        Assert.True(clean);
        Assert.False(executed);
        Assert.Equal(0, cumulative);
        Assert.False(status.IsServingSentence);
        Assert.Equal(0, prisoners);
    }

    [Fact]
    public void 同月三人服刑时囚犯计数为三()
    {
        var first = RulesHarness.Member(1, Gender.Male, 30);
        var second = RulesHarness.Member(2, Gender.Male, 32);
        var third = RulesHarness.Member(3, Gender.Male, 34);
        var family = RulesHarness.FamilyOf(first, second, third);

        RulesHarness.ServingSentence(first, 1);
        RulesHarness.ServingSentence(second, 12);
        RulesHarness.ServingSentence(third, 60);

        var state = NewState(family, Difficulty.Normal);

        Assert.Equal(3, PunishmentQueries.PrisonerCount(family, state.CurrentDate));
        Assert.True(PunishmentQueries.StatusOf(first).IsServingSentence);
        Assert.Equal(1, PunishmentQueries.StatusOf(first).SentenceRemainingMonths);
    }

    [Fact]
    public void 逻辑轨八解锁成就所需事实齐备且本特性不解锁任何成就()
    {
        var family = new Family("事实");

        // 父母为开局成员、当事人是其血亲之子 ⇒ 父母在五类亲属集合里。
        var father = family.AddFoundingMember(Founding(2, Gender.Male, 60));
        var mother = family.AddFoundingMember(Founding(3, Gender.Female, 58));
        family.Marry(father.Id, mother.Id);

        var subject = family.AddChild(Child(1, Gender.Male, 30, father.Id, mother.Id, generation: 1));
        var son = family.AddChild(Child(4, Gender.Male, 25, subject.Id, generation: 2));
        var daughter = family.AddChild(Child(5, Gender.Female, 20, subject.Id, generation: 2));
        var cleanJinShi = family.AddChild(Child(6, Gender.Male, 45, subject.Id, generation: 2));

        var prisonerA = family.AddFoundingMember(Founding(7, Gender.Male, 30));
        var prisonerB = family.AddFoundingMember(Founding(8, Gender.Male, 30));
        var prisonerC = family.AddFoundingMember(Founding(9, Gender.Male, 30));

        father.Rank = new OfficialRank(15);
        subject.AppendDegree(new DegreeRecord(DegreeLevel.GongShi, null, Month, DegreeChangeCause.ExamPass));
        son.AppendDegree(new DegreeRecord(DegreeLevel.JuRen, null, Month, DegreeChangeCause.ExamPass));
        cleanJinShi.AppendDegree(new DegreeRecord(DegreeLevel.JinShi, null, Month, DegreeChangeCause.ExamPass));
        daughter.Status |= StatusFlag.MarriedOut;

        RulesHarness.ServingSentence(prisonerA, 1);
        RulesHarness.ServingSentence(prisonerB, 1);
        RulesHarness.ServingSentence(prisonerC, PunishmentPolicy.FamilyWideBanMonths);

        var state = NewState(family, Difficulty.Hell);

        // 事实一：行贿并被抓（日志恰一行 BribeFact；被抓当次照样计入计数）。
        family.PunishmentJournal.Append(new BribeFact(
            Date: Month,
            Examinee: subject.Id,
            Level: ExamLevel.DianShi,
            Nth: 1,
            Amount: Money.FromGuan(100m),
            Caught: true,
            CapReached: false));
        subject.RecordBribe(ExamLevel.DianShi);

        // 事实二：家族累计行贿次数（成就阈值 3/10/100/1000 的输入）。
        Assert.Equal(1, PunishmentQueries.FamilyCumulativeBribes(family));

        // 事实三：某级别触顶（查询面可判；此处尚未触顶）。
        Assert.False(PunishmentQueries.IsLevelCapReached(state, subject, ExamLevel.DianShi));

        // 事实四：清白传家（进士且三项计数皆为 0）。
        Assert.True(PunishmentQueries.WasBribeFreeAtImperialPass(cleanJinShi));

        // 事实九：某月服刑 ≥ 3。
        Assert.Equal(3, PunishmentQueries.PrisonerCount(family, state.CurrentDate));

        // 事实五~八：死刑、连坐致亲属死亡或降级、外嫁女儿被追及、全族禁考禁升触发。
        PunishmentImposer.Apply(state, subject, ExamLevel.DianShi, new FixedRandomService());

        // 事实五「身首异处」= **当事人本人**的死刑：状态派生（契约十六 §8），不经六类事实行。
        Assert.True(PunishmentQueries.WasExecutedByState(subject));
        Assert.Contains(
            family.PunishmentJournal.CollectivePunishments,
            fact => fact.Outcome == CollectiveOutcome.Executed);
        Assert.Contains(
            family.PunishmentJournal.CollectivePunishments,
            fact => fact.Outcome == CollectiveOutcome.DegreeDemoted);
        Assert.Contains(
            family.PunishmentJournal.CollectivePunishments,
            fact => fact.Relative == daughter.Id);
        Assert.True(family.PunishmentJournal.FamilyBanDeclared);

        // 事实十：服刑期满释放（本月恰两人期满）。
        var snapshot = PunishmentAdvance.Run(state);

        Assert.Equal(2, snapshot.Releases.Count);
        Assert.True(snapshot.FamilyBanDeclared);
        Assert.Equal(1, snapshot.PrisonerCount);
        Assert.Equal(1, PunishmentQueries.PrisonerCount(family, state.CurrentDate));
        Assert.Contains(snapshot.Bribes, fact => fact.Caught);
        Assert.Contains(snapshot.CollectivePunishments, fact => fact.Outcome == CollectiveOutcome.DegreeDemoted);

        // 本特性 MUST NOT 自行解锁任何成就：规则层没有任何成就类型（FR-030）。
        var achievements = typeof(PunishmentQueries).Assembly
            .GetTypes()
            .Where(type => type.Name.Contains("Achievement", StringComparison.OrdinalIgnoreCase)
                || type.Name.Contains("成就", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(achievements);

        // 成就所需的**事实**（而非成就本身）都必须能从只读面取到。
        Assert.NotEmpty(typeof(PunishmentSnapshot).GetProperties(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void 行贿计数触顶后查询面报已封顶()
    {
        var examinee = RulesHarness.Member(1, Gender.Male, 20, officialdom: 100);
        examinee.Study = 100;
        var family = RulesHarness.FamilyOf(examinee);
        var state = NewState(family, Difficulty.Hell, JieShiWindowFirstMonth);

        for (var index = 0; index < 64 && !PunishmentQueries.IsLevelCapReached(state, examinee, ExamLevel.JieShi); index++)
        {
            examinee.RecordBribe(ExamLevel.JieShi);
        }

        // 触顶 = **最终录取率**达到 DiceRateCap 的派生判定（乘数本身无上限）。
        Assert.True(PunishmentQueries.IsLevelCapReached(state, examinee, ExamLevel.JieShi));

        var feasibility = PunishmentQueries.Of(state, examinee, ExamLevel.JieShi);
        Assert.True(feasibility.IsCapReached);
        Assert.False(feasibility.IsFeasible);
        Assert.Equal(BribeRejectReason.CapReached, feasibility.RejectReason);
    }

    private static GameState NewState(Family family, Difficulty difficulty, GameDate? date = null) =>
        new(
            StateId,
            date ?? Month,
            difficulty,
            Origin.Scholar,
            family,
            new FamilyEconomy(
                LivingStandard.Normal,
                new GrainPriceIndex(GrainPricePolicy.Initial),
                new Treasury(Money.FromGuan(10_000m)),
                new Holdings()));

    /// <summary>辈分 0、无父母的开局成员。</summary>
    private static Person Founding(int index, Gender gender, int age) =>
        FamilyFixtures.NewPerson(index, $"P{index}", gender, Month.Year - age, Month.Month, generation: 0);

    /// <summary>辈分 = 父母辈分 + 1 的血亲成员。</summary>
    private static Person Child(
        int index,
        Gender gender,
        int age,
        PersonId fatherId,
        PersonId? motherId = null,
        int generation = 1) =>
        FamilyFixtures.NewPerson(
            index, $"P{index}", gender, Month.Year - age, Month.Month, generation, fatherId, motherId);

    /// <summary>整族的可观测字段快照（用于「逐字段差异项 = 0」断言）。</summary>
    private static string Fingerprint(GameState state)
    {
        var builder = new StringBuilder();
        builder.Append(state.CurrentDate).Append('|')
            .Append(state.Difficulty).Append('|')
            .Append(state.Economy.Treasury.Cash.Guan).Append('|')
            .Append(state.Economy.Treasury.Savings.Guan).Append('|')
            .Append(state.Economy.Treasury.Loan.Principal.Guan).Append('|')
            .Append(state.Economy.Ledger.Entries.Count).Append('|')
            .Append(state.Family.PunishmentJournal.IsEmpty).Append('|')
            .Append(state.Family.PunishmentJournal.FamilyBanDeclared).AppendLine();

        foreach (var member in state.Family.Members)
        {
            builder.Append(member.Id).Append('|')
                .Append(member.CurrentDegree).Append('|')
                .Append(member.Rank?.Level).Append('|')
                .Append(member.Status).Append('|')
                .Append(member.Timers).Append('|')
                .Append(member.Merit).Append('|')
                .Append(member.MonthsInOffice).AppendLine();
        }

        foreach (var entry in state.Economy.Ledger.Entries)
        {
            builder.Append(entry.Date).Append('|')
                .Append(entry.Category).Append('|')
                .Append(entry.Amount).Append('|')
                .Append(entry.PersonId).AppendLine();
        }

        return builder.ToString();
    }
}
