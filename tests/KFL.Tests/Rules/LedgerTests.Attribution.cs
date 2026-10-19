using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Services;
using KFL.Rules.Settlement;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Rules;

public partial class LedgerTests
{
    [Fact]
    public void 已归档成员的历史条目仍可按角色读出()
    {
        var run = RunFixture(RunMonths, Origin.Artisan);
        var family = run.State.Family;
        var ledger = run.State.Economy.Ledger;
        var from = run.Steps[0].Month;
        var to = run.Steps[^1].Month;

        var totalsBefore = new Dictionary<PersonId, Money>();
        var monthsBefore = new Dictionary<PersonId, int>();

        foreach (var member in family.Members)
        {
            totalsBefore[member.Id] = ledger.TotalsByPerson(member.Id, from, to);
            monthsBefore[member.Id] = ledger.MonthlyByPerson(member.Id, from, to).Count;
        }

        // 在职位成员按角色有俸禄进账，故这组断言不是空转。
        var official = MemberOf(family, run.OfficialId);

        Assert.True(totalsBefore[official.Id].IsPositive, "在职位成员 MUST 有按角色归属的收入条目。");
        Assert.True(monthsBefore[official.Id] > 0);

        // 归档两名成员：一名已亡、一名外嫁（US5 AS3、SC-009）。
        MemberOf(family, run.OfficialId).Status = StatusFlag.Deceased;
        MemberOf(family, run.YouthId).Status = StatusFlag.MarriedOut;

        Assert.DoesNotContain(family.RegisteredMembers, m => m.Id == run.OfficialId);
        Assert.DoesNotContain(family.RegisteredMembers, m => m.Id == run.YouthId);
        Assert.Contains(family.ArchivedMembers, m => m.Id == run.OfficialId);
        Assert.Contains(family.ArchivedMembers, m => m.Id == run.YouthId);

        // 归档不是删除：既往条目的角色维度查询结果逐位不变。
        foreach (var member in family.Members)
        {
            Assert.Equal(totalsBefore[member.Id], ledger.TotalsByPerson(member.Id, from, to));
            Assert.Equal(monthsBefore[member.Id], ledger.MonthlyByPerson(member.Id, from, to).Count);
        }
    }

    [Fact]
    public void 家族级条目不被塞给成员也不丢失且经商归被采用的成员()
    {
        var fixture = EconomyFixtures.Create(new EconomyFixtureSettings { Date = December });
        var trader = fixture.Family.TryGet(fixture.ServingOfficialId)
            ?? throw new InvalidOperationException("夹具的在职位成员 MUST 存在于家族中。");

        // 指派经商：商本池（夹具取值）已达门槛，故本月必产生一份经商收益（E-18）。
        trader.Occupation = Occupation.Trading;

        var state = new GameState(
            StateId, fixture.Date, Difficulty.Normal, Origin.Artisan, fixture.Family, fixture.Economy);
        var engine = new MonthlySettlementEngine(new SeededRandomService(Seed), new GameStateClock(state), new FixedNameGenerator());
        var step = Settle(engine, state);
        var ledger = state.Economy.Ledger;

        // ① 铺面租、储蓄利息、工 bonus 归为**家族级**条目：PersonId 一律为 null（MUST NOT 塞给某个成员）。
        Assert.All(
            ledger.Entries.Where(e => e.Category is
                LedgerCategory.ShopRentIncome or LedgerCategory.SavingsInterest or LedgerCategory.ArtisanBonus),
            e => Assert.Null(e.PersonId));

        Assert.Single(ledger.Entries, e => e.Category == LedgerCategory.ShopRentIncome);
        Assert.Single(ledger.Entries, e => e.Category == LedgerCategory.SavingsInterest);
        Assert.Single(ledger.Entries, e => e.Category == LedgerCategory.ArtisanBonus);

        // ② 经商**不在**家族级之列：TradeIncome 归属被采用的那名成员（E-18）。
        var trade = Assert.Single(ledger.Entries, e => e.Category == LedgerCategory.TradeIncome);

        Assert.True(trade.Amount.IsPositive);
        Assert.Equal(fixture.ServingOfficialId, trade.PersonId);

        // ③ 家族级与角色级互补：角色维度之和 + 家族级之和 = 区间合计（既不重复计、也不丢失）。
        var familyLevel = Money.Zero;
        var expectedTrader = Money.Zero;

        foreach (var entry in step.Result.Entries)
        {
            if (!IsTreasury(entry))
            {
                continue;
            }

            if (entry.PersonId is null)
            {
                familyLevel += entry.Amount;
            }
            else if (entry.PersonId == fixture.ServingOfficialId)
            {
                expectedTrader += entry.Amount;
            }
        }

        var byPerson = Money.Zero;

        foreach (var member in fixture.Family.Members)
        {
            byPerson += ledger.TotalsByPerson(member.Id, step.Month, step.Month);
        }

        AssertMoneyIdentity(familyLevel + byPerson, ledger.TreasuryDeltaIn(step.Month, step.Month));

        // 经商收益（以及同月的官俸）都记在这名成员头上。
        AssertMoneyIdentity(expectedTrader, ledger.TotalsByPerson(fixture.ServingOfficialId, step.Month, step.Month));

        // 家族级条目 MUST NOT 出现在任何成员的逐月合计里：逐月之和恰为该成员的私有条目之和。
        var traderMonths = Money.Zero;

        foreach (var month in ledger.MonthlyByPerson(fixture.ServingOfficialId, step.Month, step.Month))
        {
            traderMonths += month.Total;
        }

        AssertMoneyIdentity(expectedTrader, traderMonths);
    }
}
