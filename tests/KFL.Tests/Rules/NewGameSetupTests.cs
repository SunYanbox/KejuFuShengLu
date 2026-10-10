using KFL.Core.Config;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Services;
using KFL.Rules.Config;
using KFL.Rules.Settlement;
using KFL.Rules.Start;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// quickstart S1/S2、SC-001、SC-002、SC-008：四出身开局逐项（每种出身 8 类事实）与开局确定性。
/// </summary>
/// <remarks>
/// <para>
/// 期望值一律经 <c>KFL.Rules/Config</c> 的成员取得（SC-009），本文件 MUST NOT 另存规则数值副本；
/// 随机一律来自 <see cref="SeededRandomService"/>（固定种子），姓名一律来自固定姓名替身。
/// </para>
/// </remarks>
public class NewGameSetupTests
{
    private static readonly int Seed = 20261008;

    private static readonly GameDate StartDate = new(1, 1);

    /// <summary>四出身。</summary>
    public static TheoryData<Origin> Origins =>
        new() { Origin.Farmer, Origin.Artisan, Origin.Merchant, Origin.Scholar };

    [Theory]
    [MemberData(nameof(Origins))]
    public void 成员数与性别(Origin origin)
    {
        var setup = Create(origin);

        Assert.Equal(
            1 + OriginStartTable.SpouseCount + OriginStartTable.ChildCount(origin),
            setup.Members.Count);
        Assert.Equal(Gender.Male, Head(setup).Gender);
        Assert.Equal(Gender.Female, Spouse(setup).Gender);
        Assert.Equal(OriginStartTable.ChildCount(origin), Children(setup).Count);
    }

    [Theory]
    [MemberData(nameof(Origins))]
    public void 年龄区间(Origin origin)
    {
        var setup = Create(origin);

        Assert.InRange(
            Head(setup).AgeAt(setup.StartDate),
            OriginStartTable.HeadAgeMin(origin),
            OriginStartTable.HeadAgeMax(origin));
        Assert.InRange(
            Spouse(setup).AgeAt(setup.StartDate),
            OriginStartTable.SpouseAgeMean - OriginStartTable.AgeSpread,
            OriginStartTable.SpouseAgeMean + OriginStartTable.AgeSpread);

        foreach (var child in Children(setup))
        {
            Assert.InRange(
                child.AgeAt(setup.StartDate), OriginStartTable.ChildAgeMin, OriginStartTable.ChildAgeMax);
        }
    }

    [Theory]
    [MemberData(nameof(Origins))]
    public void 辈分家主与婚姻与父母引用(Origin origin)
    {
        var setup = Create(origin);
        var head = Head(setup);
        var spouse = Spouse(setup);

        Assert.Equal(0, head.Generation);
        Assert.Equal(0, spouse.Generation);
        Assert.All(Children(setup), child => Assert.Equal(1, child.Generation));

        Assert.Equal(head.Id, setup.HeadId.GetValueOrDefault());
        Assert.Equal(spouse.Id, head.SpouseId.GetValueOrDefault());
        Assert.Equal(head.Id, spouse.SpouseId.GetValueOrDefault());

        foreach (var child in Children(setup))
        {
            Assert.Equal(head.Id, child.FatherId.GetValueOrDefault());
            Assert.Equal(spouse.Id, child.MotherId.GetValueOrDefault());
        }
    }

    [Theory]
    [MemberData(nameof(Origins))]
    public void 四项天赋与学业体质寿数口径(Origin origin)
    {
        var setup = Create(origin);
        var head = Head(setup);
        var spouse = Spouse(setup);

        Assert.All(
            setup.Members,
            person =>
            {
                Assert.InRange(person.Talents.Agriculture, AttributeLimits.Min, AttributeLimits.Max);
                Assert.InRange(person.Talents.Commerce, AttributeLimits.Min, AttributeLimits.Max);
                Assert.InRange(person.Talents.Officialdom, AttributeLimits.Min, AttributeLimits.Max);
                Assert.InRange(person.Talents.Craft, AttributeLimits.Min, AttributeLimits.Max);
                Assert.InRange(person.Study, AttributeLimits.Min, AttributeLimits.Max);
                Assert.InRange(person.Health, AttributeLimits.Min, AttributeLimits.Max);
                Assert.True(person.Lifespan >= AttributeLimits.Min);
            });

        // 家主学业：士为常量、其余落 10~30；家主体质恒 80~100。
        if (origin == Origin.Scholar)
        {
            Assert.Equal(OriginStartTable.ScholarHeadStudy, head.Study);
        }
        else
        {
            Assert.InRange(
                head.Study, OriginStartTable.CommonerHeadStudyMin, OriginStartTable.CommonerHeadStudyMax);
        }

        Assert.InRange(head.Health, OriginStartTable.HeadHealthMin, OriginStartTable.HeadHealthMax);

        // 配偶：学业与体质取分布（只断言落在值域内）。
        Assert.InRange(spouse.Study, AttributeLimits.Min, AttributeLimits.Max);
        Assert.InRange(spouse.Health, AttributeLimits.Min, AttributeLimits.Max);

        // 孩子：学业为常量 0（不掷骰）、体质恒 90~100。
        foreach (var child in Children(setup))
        {
            Assert.Equal(OriginStartTable.ChildStudyValue, child.Study);
            Assert.InRange(child.Health, OriginStartTable.ChildHealthMin, OriginStartTable.ChildHealthMax);
        }
    }

    [Theory]
    [MemberData(nameof(Origins))]
    public void 初始资产与资金池逐格(Origin origin)
    {
        var setup = Create(origin);
        var economy = setup.State.Economy;

        Assert.Equal(Money.FromGuan(OriginStartTable.InitialCashGuan(origin)), economy.Treasury.Cash);
        Assert.Equal(Money.Zero, economy.Treasury.Savings);
        Assert.Equal(
            Money.FromGuan(OriginStartTable.InitialMerchantCapitalGuan(origin)),
            economy.Treasury.MerchantCapital);

        Assert.Equal(OriginStartTable.InitialFarmlandMu(origin), economy.Holdings.FarmlandMu);
        Assert.Equal(OriginStartTable.InitialRuralHouses(origin), economy.Holdings.RuralHouses);
        Assert.Equal(OriginStartTable.InitialUrbanHouses(origin), economy.Holdings.UrbanHouses);
        Assert.Equal(0, economy.Holdings.Shops);

        // 资金池 = 现金 + 储蓄 + 商本（三池不错池）。
        Assert.Equal(economy.TreasuryPool, economy.Treasury.Cash + economy.Treasury.Savings + economy.Treasury.MerchantCapital);
    }

    [Theory]
    [MemberData(nameof(Origins))]
    public void 士家主的功名记录与仕身份为假(Origin origin)
    {
        var setup = Create(origin);
        var head = Head(setup);

        // 任何出身下「仕」身份都为 false（§10.2、FR-008）。
        Assert.False(setup.State.Family.HasShiStatus);

        if (origin == Origin.Scholar)
        {
            var record = Assert.Single(head.DegreeHistory);
            Assert.Equal(DegreeLevel.JuRen, record.Level);
            Assert.Equal(DegreeChangeCause.Initial, record.Cause);
            Assert.Null(record.Placement);
            Assert.Null(record.Class);
            Assert.Equal(setup.StartDate, record.ChangedAt);
        }
        else
        {
            Assert.Empty(head.DegreeHistory);
            Assert.Equal(DegreeLevel.BaiShen, head.CurrentDegree);
        }

        Assert.All(setup.Members, person => Assert.Null(person.Rank));
    }

    [Theory]
    [MemberData(nameof(Origins))]
    public void 存档级状态与账本为空(Origin origin)
    {
        var setup = Create(origin);

        Assert.Equal(origin, setup.State.Origin);
        Assert.Equal(Difficulty.Normal, setup.State.Difficulty);
        Assert.Equal(StartDate, setup.State.CurrentDate);
        Assert.Equal(StartDate, setup.StartDate);
        Assert.NotEqual(Guid.Empty, setup.State.Id);

        // 生活费档位与米价系数取既有单点；开局**不落任何账本条目**（初始余额口径，FR-010）。
        Assert.Equal(LivingCostTable.InitialStandard, setup.State.Economy.LivingStandard);
        Assert.Equal(GrainPricePolicy.Initial, setup.State.Economy.GrainPriceIndex.Value);
        Assert.Empty(setup.State.Economy.Ledger.Entries);
    }

    [Fact]
    public void 商出身的三池逐格与商本不错池()
    {
        var setup = Create(Origin.Merchant);
        var economy = setup.State.Economy;

        Assert.Equal(Money.FromGuan(OriginStartTable.InitialCashGuan(Origin.Merchant)), economy.Treasury.Cash);
        Assert.Equal(
            Money.FromGuan(OriginStartTable.InitialMerchantCapitalGuan(Origin.Merchant)),
            economy.Treasury.MerchantCapital);
        Assert.Equal(1, economy.Holdings.UrbanHouses);

        // 反向验证一：可付额 = 现金 + 储蓄，商本 MUST NOT 进入。
        var payable = economy.Treasury.Cash + economy.Treasury.Savings;

        Assert.True(economy.TreasuryPool > payable);
        Assert.Throws<InvalidOperationException>(() => economy.Apply(
            LedgerCategory.LivingCost, personId: null, -(payable + Money.FromWen(1m)), StartDate));
    }

    [Fact]
    public void 商本不进入工出身bonus的资产基数()
    {
        // 反向验证二：同一份现金 / 储蓄 / 田宅（工出身存档）下，把商本从 0 加到 300 贯，12 月 bonus 不变。
        var setup = Create(Origin.Artisan);
        var withoutMerchant = DecemberArtisanBonus(setup, Money.Zero);
        var withMerchant = DecemberArtisanBonus(
            setup, Money.FromGuan(OriginStartTable.InitialMerchantCapitalGuan(Origin.Merchant)));

        Assert.True(withoutMerchant.IsPositive, "工出身的 12 月 bonus 应为正，否则对照恒真。");
        Assert.Equal(withoutMerchant, withMerchant);
    }

    [Fact]
    public void 姓氏为空时经姓名来源取随机姓氏()
    {
        var request = new NewGameRequest(Origin.Farmer, Difficulty.Normal, Surname: null, StartDate);
        var setup = NewGameSetup.Create(request, new SeededRandomService(Seed), new FixedNameGenerator("赵"));

        Assert.Equal("赵", setup.State.Family.Name);
        Assert.All(setup.Members, person => Assert.StartsWith("赵", person.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void 姓氏非空时原样使用()
    {
        var setup = Create(Origin.Scholar, surname: "李");

        Assert.Equal("李", setup.State.Family.Name);
        Assert.All(setup.Members, person => Assert.StartsWith("李", person.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void 起始年月非元年正月被拒()
    {
        var request = new NewGameRequest(Origin.Farmer, Difficulty.Normal, "李", new GameDate(2, 1));

        Assert.Throws<ArgumentException>(() => NewGameSetup.Create(
            request, new SeededRandomService(Seed), new FixedNameGenerator()));
    }

    [Fact]
    public void 姓氏为空白串被拒()
    {
        foreach (var surname in new[] { string.Empty, "   " })
        {
            var request = new NewGameRequest(Origin.Farmer, Difficulty.Normal, surname, StartDate);

            Assert.Throws<ArgumentException>(() => NewGameSetup.Create(
                request, new SeededRandomService(Seed), new FixedNameGenerator()));
        }
    }

    [Fact]
    public void 天命寿数只clamp下界不设上限()
    {
        // 下界：Box–Muller 产出负值 ⇒ clamp 到 0（AttributeLimits.Min）。
        var negative = AttributePolicy.NextLifespan(
            Gender.Male, new FixedRandomService(0.999_999_999_999_9d, 0.5d));  // arch-guard:allow 夹具随机取值（非规则数值副本）

        Assert.Equal(AttributeLimits.Min, negative);

        // 上界：产出大于 100 的取值 MUST NOT 被截断（FR-006 的无上限口径）。
        var huge = AttributePolicy.NextLifespan(
            Gender.Male, new FixedRandomService(0.999_999d, 0.0d));

        Assert.True(
            huge > AttributeLimits.Max,
            $"天命寿数 MUST NOT 有上限，实际取值为 {huge}（FR-006）。");
    }

    private static NewGameSetupResult Create(Origin origin, string surname = "测")
    {
        var request = new NewGameRequest(origin, Difficulty.Normal, surname, StartDate);

        return NewGameSetup.Create(
            request, new SeededRandomService(Seed), new FixedNameGenerator(surname));
    }

    private static Person Head(NewGameSetupResult setup) =>
        setup.State.Family.TryGet(setup.HeadId!.Value)!;

    private static Person Spouse(NewGameSetupResult setup) =>
        setup.State.Family.SpouseOf(Head(setup).Id)!;

    private static List<Person> Children(NewGameSetupResult setup) =>
        setup.Members.Where(person => person.Generation == 1).ToList();

    /// <summary>用同一家族重搭一份经济聚合（只换商本），跑满 12 个月并返回 12 月的工出身 bonus。</summary>
    private static Money DecemberArtisanBonus(NewGameSetupResult setup, Money merchantCapital)
    {
        var source = setup.State.Economy;
        var holdings = source.Holdings;
        var treasury = new Treasury(source.Treasury.Cash, source.Treasury.Savings, merchantCapital);
        var rebuilt = new Holdings
        {
            FarmlandMu = holdings.FarmlandMu,
            RuralHouses = holdings.RuralHouses,
            UrbanHouses = holdings.UrbanHouses,
            Shops = holdings.Shops,
        };

        var economy = new FamilyEconomy(
            source.LivingStandard, new GrainPriceIndex(GrainPricePolicy.Initial), treasury, rebuilt);

        var state = new GameState(
            setup.State.Id, setup.StartDate, Difficulty.Normal, Origin.Artisan, setup.State.Family, economy);

        var engine = new MonthlySettlementEngine(
            new FixedRandomService(0.5d), new GameStateClock(state));  // arch-guard:allow 夹具随机取值（非规则数值副本）

        SettlementResult? last = null;

        for (var index = 0; index < 12; index++)
        {
            last = engine.Settle(state);
        }

        return last!.ArtisanBonus;
    }
}
