using KFL.Core.Config;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;
using KFL.Rules.Economy;
using KFL.Rules.FamilyLifecycle;
using KFL.Rules.Settlement;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// 买人口（FR-031~FR-034、SC-006、规格书 §9.5）：
/// 「本年内第 n 名 = 10n 贯」递增计价、跨年 1 月重置、现银 + 储蓄全额支付且恒不转贷款、
/// 买来者作为无父母参照的旁系入册并计入计口。
/// </summary>
public class PurchaseTests
{
    private static readonly Guid StateId = Guid.Parse("c9e1f3a7-2d58-4b60-9f83-4a7c1e6b2d90");
    private static readonly Guid EmptyStateId = Guid.Parse("4a6b8c02-7e19-4d35-b87f-6c0a2d9e1f47");

    /// <summary>第 n 名的价格（负数无关；价格恒由配置成员与名次相乘而得，测试里不复写数值）。</summary>
    private static Money PriceOf(int ordinal) =>
        Money.FromGuan(PopulationPurchasePolicy.BasePriceGuan * ordinal);

    [Fact]
    public void 买人口价格按本年内名次递增且不含支出系数()
    {
        var family = Household(out var head);
        var state = State(family, RulesHarness.Guan(1000));

        var first = LifecycleEntry.BuyPerson(state, new ScriptedRandom(), new FixedNameGenerator());
        var second = LifecycleEntry.BuyPerson(state, new ScriptedRandom(), new FixedNameGenerator());
        var third = LifecycleEntry.BuyPerson(state, new ScriptedRandom(), new FixedNameGenerator());

        Assert.True(first.Accepted);
        Assert.True(second.Accepted);
        Assert.True(third.Accepted);
        Assert.Equal(PriceOf(1), first.Amount);
        Assert.Equal(PriceOf(2), second.Amount);
        Assert.Equal(PriceOf(3), third.Amount);
        Assert.Equal(RulesHarness.Guan(1000) - (PriceOf(1) + PriceOf(2) + PriceOf(3)), state.Economy.Treasury.Cash);
        Assert.Equal(3, family.Lifecycle.PurchaseCount);
        Assert.Equal(RulesHarness.Date.Year, family.Lifecycle.PurchaseYear);
        Assert.Equal(3, family.Members.Count(person => person.Generation == head.Generation + 1));
    }

    [Fact]
    public void 跨年后名次重置回第一名()
    {
        var family = Household(out _);
        var state = State(family, RulesHarness.Guan(1000));

        Assert.True(LifecycleEntry.BuyPerson(state, new ScriptedRandom(), new FixedNameGenerator()).Accepted);
        for (var month = 0; month < 12; month++)
        {
            state.AdvanceMonth();
        }

        Assert.Equal(RulesHarness.Date.Year + 1, state.CurrentDate.Year);

        var afterNewYear = LifecycleEntry.BuyPerson(state, new ScriptedRandom(), new FixedNameGenerator());

        Assert.True(afterNewYear.Accepted);
        Assert.Equal(PriceOf(1), afterNewYear.Amount);
        Assert.Equal(1, family.Lifecycle.PurchaseCount);
        Assert.Equal(state.CurrentDate.Year, family.Lifecycle.PurchaseYear);
    }

    [Fact]
    public void 同月多次购买按本年度已购数递增()
    {
        var family = Household(out _);
        var state = State(family, RulesHarness.Guan(1000));

        var before = state.Economy.Treasury.Cash;
        var first = LifecycleEntry.BuyPerson(state, new ScriptedRandom(), new FixedNameGenerator());
        var second = LifecycleEntry.BuyPerson(state, new ScriptedRandom(), new FixedNameGenerator());

        Assert.Equal(PriceOf(1), first.Amount);
        Assert.Equal(PriceOf(2), second.Amount);
        Assert.Equal(before - (PriceOf(1) + PriceOf(2)), state.Economy.Treasury.Cash);
        Assert.Equal(2, family.Lifecycle.PurchaseCount);
        Assert.Equal(2, state.Economy.Ledger.Entries.Count(entry => entry.Category == LedgerCategory.PersonPurchase));
    }

    [Fact]
    public void 付不起时不购买且不转贷款()
    {
        var family = Household(out _);
        var state = State(family, RulesHarness.Guan(1));
        var random = new ScriptedRandom();
        var entriesBefore = state.Economy.Ledger.Entries.Count;

        var outcome = LifecycleEntry.BuyPerson(state, random, new FixedNameGenerator());

        Assert.False(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.InsufficientFunds, outcome.RejectReason);
        Assert.Equal(Money.Zero, outcome.Amount);
        Assert.Null(outcome.Entry);
        Assert.Equal(0, random.DoublesRequested);
        Assert.Equal(0, random.IntsRequested);
        Assert.Equal(0, state.Economy.Treasury.Loan.Principal.Guan);
        Assert.Equal(entriesBefore, state.Economy.Ledger.Entries.Count);
        Assert.Equal(0, family.Lifecycle.PurchaseCount);
    }

    [Fact]
    public void 无家主时拒绝且不消费随机()
    {
        var head = RulesHarness.Member(1, Gender.Male, 40);
        var family = new Family("测");
        family.AddFoundingMember(head);
        var state = new GameState(
            EmptyStateId,
            RulesHarness.Date,
            Difficulty.Normal,
            Origin.Artisan,
            family,
            new FamilyEconomy(
                LivingStandard.Normal,
                new GrainPriceIndex(GrainPricePolicy.Initial),
                new Treasury(RulesHarness.Guan(1000)),
                new Holdings()));
        var random = new ScriptedRandom();

        var outcome = LifecycleEntry.BuyPerson(state, random, new FixedNameGenerator());

        Assert.False(outcome.Accepted);
        Assert.Equal(LifecycleRejectReason.HeadVacant, outcome.RejectReason);
        Assert.Equal(0, random.DoublesRequested);
        Assert.Equal(0, random.IntsRequested);
        Assert.Equal(RulesHarness.Guan(1000), state.Economy.Treasury.Cash);
        Assert.Equal(0, family.Lifecycle.PurchaseCount);
    }

    [Fact]
    public void 买来者作为无父母参照的旁系入册并计入计口()
    {
        var family = Household(out var head);
        var state = State(family, RulesHarness.Guan(1000));

        var outcome = LifecycleEntry.BuyPerson(state, new ScriptedRandom(), new FixedNameGenerator());

        var bought = Assert.Single(state.Family.Members, member => member.Id != head.Id);
        Assert.True(outcome.Accepted);
        Assert.True(bought.IsOutsider);
        Assert.Null(bought.FatherId);
        Assert.Null(bought.MotherId);
        Assert.StartsWith(family.Name, bought.Name, StringComparison.Ordinal);
        Assert.InRange(bought.AgeAt(state.CurrentDate), PopulationPurchasePolicy.AgeMin, PopulationPurchasePolicy.AgeMax);
        Assert.Equal(family.BoughtCollateralGeneration, bought.Generation);
        Assert.InRange(bought.Study, AttributeLimits.Min, AttributeLimits.Max);
        Assert.InRange(bought.Health, AttributeLimits.Min, AttributeLimits.Max);
        Assert.Contains(bought.Id, CountedMembers.Counted(family).Select(person => person.Id));
        Assert.Contains(bought.Id, CountedMembers.Registered(family).Select(person => person.Id));
    }

    [Fact]
    public void 买来者的年龄与性别取自配置区间()
    {
        var family = Household(out _);
        var state = State(family, RulesHarness.Guan(1000));

        var outcome = LifecycleEntry.BuyPerson(state, new ScriptedRandom(largeInts: true), new FixedNameGenerator());

        var bought = Assert.Single(state.Family.Members, member => member.Id != family.HeadId);
        Assert.True(outcome.Accepted);
        Assert.Equal(Gender.Female, bought.Gender);
        Assert.Equal(PopulationPurchasePolicy.AgeMax, bought.AgeAt(state.CurrentDate));
    }

    [Fact]
    public void 买来者各落一条资金类与一条事件类条目()
    {
        var family = Household(out _);
        var state = State(family, RulesHarness.Guan(1000));

        var outcome = LifecycleEntry.BuyPerson(state, new ScriptedRandom(), new FixedNameGenerator());

        var purchase = Assert.Single(state.Economy.Ledger.Entries, entry => entry.Category == LedgerCategory.PersonPurchase);
        var bought = Assert.Single(state.Economy.Ledger.Entries, entry => entry.Category == LedgerCategory.PersonBought);
        Assert.True(outcome.Accepted);
        Assert.Equal(LedgerCategory.PersonPurchase, outcome.Entry?.Category);
        Assert.True(purchase.Amount.IsNegative);
        Assert.Equal(PriceOf(1), -purchase.Amount);
        Assert.Equal(Money.Zero, bought.Amount);
        Assert.Equal(LedgerEntryKind.Event, LedgerCategoryMetadata.KindOf(bought.Category));
    }

    [Fact]
    public void 首次家族内成婚使买来者辈分对齐配偶且此后落定()
    {
        var head = RulesHarness.Member(1, Gender.Male, 60);
        var family = RulesHarness.FamilyOf(head);
        var son = FamilyFixtures.NewPerson(
            2, "陈子", Gender.Male, RulesHarness.Date.Year - 40, RulesHarness.Date.Month, generation: 1, fatherId: head.Id);
        family.AddChild(son);
        var grandson = FamilyFixtures.NewPerson(
            3, "陈孙", Gender.Male, RulesHarness.Date.Year - 20, RulesHarness.Date.Month, generation: 2, fatherId: son.Id);
        family.AddChild(grandson);
        var state = State(family, RulesHarness.Guan(1000));

        // 买来者性别由抽签决定；本用例取上界 ⇒ 女性，可与家族内男性成婚。
        Assert.True(LifecycleEntry
            .BuyPerson(state, new ScriptedRandom(largeInts: true), new FixedNameGenerator())
            .Accepted);
        var bought = Assert.Single(family.Members, member => member.Id != head.Id && member.Id != son.Id && member.Id != grandson.Id);
        Assert.Equal(head.Generation + 1, bought.Generation);

        family.Marry(bought.Id, grandson.Id);

        Assert.Equal(grandson.Generation, bought.Generation);

        // 首次家族内成婚已用掉：此后辈分终局，任何再次指定都必须被拒（规格书 §4.4）。
        Assert.Throws<InvalidOperationException>(() =>
        {
            family.SetOutsiderGeneration(bought.Id, son.Generation);
        });

        family.EndMarriage(bought.Id);
        family.Marry(bought.Id, grandson.Id);

        Assert.Equal(grandson.Generation, bought.Generation);
    }

    [Fact]
    public void 买人口报价在名次未推进前是纯读的()
    {
        var family = Household(out _);
        var state = State(family, RulesHarness.Guan(1000));

        var quote = LifecycleQueries.PurchaseQuote(state);

        Assert.True(quote.Available);
        Assert.Equal(1, quote.Ordinal);
        Assert.Equal(PriceOf(1), quote.Price);
        Assert.Equal(0, family.Lifecycle.PurchaseCount);
        Assert.Equal(1, LifecycleQueries.PurchaseQuote(state).Ordinal);
    }

    private static Family Household(out Person head)
    {
        head = RulesHarness.Member(1, Gender.Male, 40);
        return RulesHarness.FamilyOf(head);
    }

    private static GameState State(Family family, Money cash) =>
        new(
            StateId,
            RulesHarness.Date,
            Difficulty.Normal,
            Origin.Artisan,
            family,
            new FamilyEconomy(
                LivingStandard.Normal,
                new GrainPriceIndex(GrainPricePolicy.Initial),
                new Treasury(cash),
                new Holdings()));

    /// <summary>计数替身：<c>Next</c> 返回下界（或上界减一），<c>NextDouble</c> 队列用尽后回退常量。</summary>
    private sealed class ScriptedRandom(bool largeInts = false, params double[] values) : IRandomService
    {
        private readonly Queue<double> _values = new(values);

        public int DoublesRequested { get; private set; }

        public int IntsRequested { get; private set; }

        public double NextDouble()
        {
            DoublesRequested++;
            return _values.Count > 0 ? _values.Dequeue() : 0.5d; // arch-guard:allow 夹具随机取值（非规则数值副本）
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            IntsRequested++;
            return largeInts ? maxExclusive - 1 : minInclusive;
        }

        public void NextBytes(Span<byte> buffer) => buffer.Clear();
    }
}
