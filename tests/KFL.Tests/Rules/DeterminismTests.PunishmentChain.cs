using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Services;
using KFL.Rules.Bribery;
using KFL.Rules.Config;
using KFL.Rules.Punishment;
using KFL.Rules.Settlement;
using System.Text;
using Xunit;

namespace KFL.Tests.Rules;

public partial class DeterminismTests
{
    [Fact]
    public void 惩罚推进恰不消耗随机数()
    {
        // 两次运行同构，唯一差别是家族里是否有人在服刑、日志里是否有待投影的事实：
        // 第③步（惩罚推进）MUST 消费恰好 0 次，故逐月随机消费次数逐位相同。
        Assert.Equal(PunishmentMonthCounts(withPunishment: false), PunishmentMonthCounts(withPunishment: true));
    }

    /// <summary>逐月 <c>NextDouble()</c> 次数；<paramref name="withPunishment"/> 决定是否预置服刑者与日志事实。</summary>
    private static List<int> PunishmentMonthCounts(bool withPunishment)
    {
        var child = RulesHarness.Member(1, Gender.Male, 5);
        var family = RulesHarness.FamilyOf(child);

        if (withPunishment)
        {
            child.Status |= StatusFlag.ServingSentence;
            child.Timers = new StatusTimers(sentenceRemainingMonths: 24);
            family.PunishmentJournal.Append(new SentenceFact(RulesHarness.Date, child.Id, 24));
            family.PunishmentJournal.Append(new FineFact(
                RulesHarness.Date, child.Id, Money.FromGuan(10m), Money.FromGuan(8m), Money.FromGuan(2m)));
            family.PunishmentJournal.Append(new RelativeBanFact(
                RulesHarness.Date, child.Id, child.Id, 12, 12));
            family.PunishmentJournal.MarkFamilyBanDeclared();
        }

        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(),
            new Holdings());
        var state = new GameState(StateId, RulesHarness.Date, Difficulty.Normal, Origin.Scholar, family, economy);
        var random = new CountingRandomService(new SeededRandomService(Seed));
        var engine = new MonthlySettlementEngine(random, new GameStateClock(state), new FixedNameGenerator());
        var counts = new List<int>();

        for (var index = 0; index < 6; index++)
        {
            var before = random.DoublesRequested;
            engine.Settle(state);
            counts.Add(random.DoublesRequested - before);
        }

        return counts;
    }

    [Fact]
    public void 同种子两遍行贿被抓惩罚服刑释放链逐字段差异项为零()
    {
        var first = PunishmentChain(Seed);
        var second = PunishmentChain(Seed);

        // SC-001：固定种子跑两遍完整链路（含同月多人与亲属禁考），逐字段差异项 = 0。
        Assert.Equal(DescribeChain(first), DescribeChain(second));
    }

    [Fact]
    public void 行贿被抓惩罚服刑释放链确实走到每个环节()
    {
        var run = PunishmentChain(Seed);

        // 「行贿 → 被抓 → 惩罚 → 服刑 → 释放」四段与亲属禁考都必须真的发生过，
        // 否则上面的零差异断言是空转。
        Assert.Contains(run.State.Economy.Ledger.Entries, entry => entry.Category == LedgerCategory.BribeExpense);
        Assert.Contains(run.State.Economy.Ledger.Entries, entry => entry.Category == LedgerCategory.Fine);
        Assert.Contains(run.Results, result => result.Punishment.Bribes.Count > 0);
        Assert.Contains(run.Results, result => result.Punishment.Sentences.Count > 0);
        Assert.Contains(run.Results, result => result.Punishment.RelativeBans.Count > 0);
        Assert.Contains(run.Results, result => result.Punishment.Releases.Count > 0);
        Assert.Contains(run.Results, result => result.Punishment.PrisonerCount > 0);
    }

    /// <summary>跑一遍完整「行贿 → 被抓 → 惩罚 → 服刑 → 释放」链路（固定种子、固定月份序列）。</summary>
    private static ChainRun PunishmentChain(int seed)
    {
        var examinee = RulesHarness.Member(1, Gender.Male, 20);
        var spouse = RulesHarness.Member(2, Gender.Female, 20);
        var second = RulesHarness.Member(3, Gender.Male, 22);

        // 夹具成员 MUST 有正常体质：RulesHarness.Member 默认不设 Health（为 0），而 007 起 ⑧-c 会让
        // 0 体质者在患病当月就归零死亡 ⇒ 服刑者被划出在册成员 ⇒ PrisonerCount 恒为 0、链路断在「服刑」段。
        examinee.Health = (int)AttributePolicy.HealthMean;
        spouse.Health = (int)AttributePolicy.HealthMean;
        second.Health = (int)AttributePolicy.HealthMean;

        var family = RulesHarness.FamilyOf(examinee, spouse, second);
        family.Marry(examinee.Id, spouse.Id);

        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(Money.FromGuan(50_000m)),
            new Holdings());
        var state = new GameState(
            StateId, new GameDate(79, 6), Difficulty.Normal, Origin.Scholar, family, economy);
        var random = new CountingRandomService(new SeededRandomService(seed));
        var engine = new MonthlySettlementEngine(random, new GameStateClock(state), new FixedNameGenerator());
        var results = new List<SettlementResult>();

        for (var index = 0; index < 70; index++)
        {
            var month = state.CurrentDate;

            // 同月多人：窗口内两人各试一次（拒绝路径不改状态、不消费随机）。
            foreach (var person in new[] { examinee, second })
            {
                if (BribeRules.IsWithinWindow(month, ExamLevel.JieShi))
                {
                    BribeEntry.Bribe(state, person, ExamLevel.JieShi, random);
                }
            }

            if (index == 1)
            {
                // 确定性的「被抓」后果：普通难度省试格（服刑 60 月 + 相关亲属禁考 24 月），
                // 保证「惩罚 → 服刑 → 释放」三段一定被走到。
                PunishmentImposer.Apply(state, examinee, ExamLevel.ShengShi, random);
            }

            results.Add(engine.Settle(state));
        }

        return new ChainRun(state, results);
    }

    /// <summary>把一次链路的全部可观测字段摊平成字符串（逐字段比较用）。</summary>
    private static string DescribeChain(ChainRun run)
    {
        var builder = new StringBuilder();

        foreach (var member in run.State.Family.Members)
        {
            builder.Append(member.Id).Append('|')
                .Append(member.CurrentDegree).Append('|')
                .Append(member.Rank?.Level).Append('|')
                .Append(member.Status).Append('|')
                .Append(member.Timers).Append('|')
                .Append(member.Merit).Append('|')
                .Append(member.MonthsInOffice).Append('|')
                .Append(member.EntryTrack).AppendLine();
        }

        foreach (var entry in run.State.Economy.Ledger.Entries)
        {
            builder.Append(entry.Date).Append('|')
                .Append(entry.Category).Append('|')
                .Append(entry.Amount).Append('|')
                .Append(entry.PersonId).AppendLine();
        }

        foreach (var result in run.Results)
        {
            builder.Append(result.Month).Append('|')
                .Append(result.Punishment.Bribes.Count).Append('|')
                .Append(result.Punishment.Sentences.Count).Append('|')
                .Append(result.Punishment.Fines.Count).Append('|')
                .Append(result.Punishment.RelativeBans.Count).Append('|')
                .Append(result.Punishment.CollectivePunishments.Count).Append('|')
                .Append(result.Punishment.Releases.Count).Append('|')
                .Append(result.Punishment.FamilyBanDeclared).Append('|')
                .Append(result.Punishment.PrisonerCount).AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>一次链路的产物：收尾状态与逐月快照。</summary>
    private sealed record ChainRun(GameState State, List<SettlementResult> Results);
}
