using System.Collections.ObjectModel;
using System.Reflection;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Services;
using KFL.Rules.Config;
using KFL.Rules.Settlement;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// US5 / quickstart S6、SC-005：流水账的四个聚合查询与「每一文钱都有出处」
/// （FR-021；contracts/ledger.md §4 不变量 1~6；data-model §3.4）。
/// </summary>
/// <remarks>
/// <para>
/// **期望值一律经 <c>KFL.Rules/Config</c> 的成员取得**（SC-008）；夹具取值只作为**用例输入**
/// （契约三清单条款 ⑤），故本文件 MUST NOT 把规则数值抄成第二处出处。
/// </para>
/// <para>
/// **两个维度是同一批条目的两个查询方向**（US5 AS4）：本文件既断言「角色维度之和 + 家族级之和
/// = 家族维度之和」，也断言逐类别、逐月两个方向的合计与它一致——不存在第二份汇总存储。
/// </para>
/// </remarks>
public class LedgerTests
{
    private static readonly Guid StateId = Guid.Parse("d4b7e5f1-3a9c-4c2e-9f8b-1c2d3e4f5a6b");

    /// <summary>固定随机种子：本文件只关心「条目 ↔ 资金池」的等式，不关心取到的具体数值。</summary>
    private const int Seed = 20261006;

    /// <summary>长跑月数：跨两个年度，覆盖 1 月的利率 roll 与 12 月的年度项（储蓄利息、工 bonus）。</summary>
    private const int RunMonths = 24;

    /// <summary>十二月的参照年月（年度项当月）。</summary>
    private static readonly GameDate December = new(80, 12);

    [Fact]
    public void 任意单月与任意连续区间的资金类条目合计等于资金池变动()
    {
        var run = RunFixture(RunMonths, Origin.Artisan);
        var ledger = run.State.Economy.Ledger;

        foreach (var step in run.Steps)
        {
            // 快照口径与外部观测一致（TreasuryPoolBefore 是引擎在动手之前取的值）。
            Assert.Equal(step.PoolBefore, step.Result.TreasuryPoolBefore);
        }

        // 全部「单月」与全部「连续区间」（含跨年、跨年度项的区间）逐一断言。
        for (var start = 0; start < run.Steps.Count; start++)
        {
            for (var end = start; end < run.Steps.Count; end++)
            {
                var from = run.Steps[start].Month;
                var to = run.Steps[end].Month;
                var expected = run.Steps[end].PoolAfter - run.Steps[start].PoolBefore;

                AssertMoneyIdentity(expected, ledger.TreasuryDeltaIn(from, to));
            }
        }
    }

    [Fact]
    public void 结算结果条目与账本新增条目逐条相同()
    {
        var run = RunFixture(RunMonths, Origin.Artisan);
        var appended = 0;

        foreach (var step in run.Steps)
        {
            AssertSameEntries(step.Appended, step.Result.Entries);

            // 快照里的条目 MUST 是**本次**追加的那一段，不是整册账本的重复切片。
            Assert.Equal(appended, IndexOf(run.State.Economy.Ledger, step.Appended));
            appended += step.Appended.Count;
        }

        Assert.Equal(appended, run.State.Economy.Ledger.Entries.Count);
    }

    [Fact]
    public void 家族维度与角色维度聚合的是同一批条目()
    {
        var run = RunFixture(RunMonths, Origin.Artisan);
        var ledger = run.State.Economy.Ledger;
        var from = run.Steps[0].Month;
        var to = run.Steps[^1].Month;
        var delta = ledger.TreasuryDeltaIn(from, to);

        // ① 逐类别合计 = 家族维度合计（收益来源明细与支出明细同一批条目）。
        var byCategory = Money.Zero;

        foreach (var (_, total) in ledger.TotalsByCategory(from, to))
        {
            byCategory += total;
        }

        AssertMoneyIdentity(delta, byCategory);

        // ② 角色维度之和 + 家族级之和 = 家族维度合计（既不重复计、也不丢失）。
        var familyLevel = Money.Zero;

        foreach (var entry in ledger.EntriesIn(from, to))
        {
            if (IsTreasury(entry) && entry.PersonId is null)
            {
                familyLevel += entry.Amount;
            }
        }

        var byPerson = Money.Zero;

        foreach (var member in run.State.Family.Members)
        {
            byPerson += ledger.TotalsByPerson(member.Id, from, to);
        }

        AssertMoneyIdentity(delta, byPerson + familyLevel);

        // ③ 每名成员的逐月合计之和 = 其区间累计；逐月按月升序且不重复。
        foreach (var member in run.State.Family.Members)
        {
            var months = ledger.MonthlyByPerson(member.Id, from, to);
            var monthly = Money.Zero;

            for (var index = 0; index < months.Count; index++)
            {
                monthly += months[index].Total;

                if (index > 0)
                {
                    Assert.True(
                        months[index - 1].Month < months[index].Month,
                        "逐月合计 MUST 按年月升序且同月至多出现一次。");
                }
            }

            AssertMoneyIdentity(ledger.TotalsByPerson(member.Id, from, to), monthly);
        }
    }

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
        var engine = new MonthlySettlementEngine(new SeededRandomService(Seed), new GameStateClock(state));
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

    [Fact]
    public void 事件类条目存在且可读但不参与求和()
    {
        // ① 饥馑四类迁移：付不起时不落生活费条目，只落**事件类**条目。
        var famine = NewFamineState();
        var engine = new MonthlySettlementEngine(new SeededRandomService(Seed), new GameStateClock(famine));
        var steps = new List<MonthStep>();

        var monthsToSevere = FamineTimeline.MonthsUntilRelief + FamineTimeline.ReliefMonthsUntilSevere - 1;

        for (var index = 0; index < monthsToSevere; index++)
        {
            steps.Add(Settle(engine, famine));
        }

        Assert.Equal(FamineStage.Severe, famine.Economy.Famine.Stage);

        var ledger = famine.Economy.Ledger;
        var from = steps[0].Month;
        var to = steps[^1].Month;

        // 恰好三条迁移条目（进入、转救济、转第三阶段），且全部为事件类。
        Assert.Equal(3, ledger.Entries.Count);
        Assert.All(ledger.Entries, e => Assert.Equal(LedgerEntryKind.Event, LedgerCategoryMetadata.KindOf(e.Category)));
        Assert.Single(ledger.Entries, e => e.Category == LedgerCategory.FamineEntered);
        Assert.Single(ledger.Entries, e => e.Category == LedgerCategory.FamineReliefEntered);
        Assert.Single(ledger.Entries, e => e.Category == LedgerCategory.FamineSevereEntered);

        // 金额与归属可读（迁移一律 0、家族级），但不参与求和：区间合计仍为 0。
        Assert.All(ledger.Entries, e => Assert.Equal(Money.Zero, e.Amount));
        Assert.All(ledger.Entries, e => Assert.Null(e.PersonId));
        Assert.Equal(Money.Zero, ledger.TreasuryDeltaIn(from, to));
        Assert.Empty(ledger.TotalsByCategory(from, to));

        // 第四类迁移：补足资金 → 全部解除，同样只落一条事件类条目。
        famine.Economy.Apply(LedgerCategory.AssetSale, null, Guan(1000m), famine.CurrentDate);

        var resolved = Settle(engine, famine);

        Assert.Equal(FamineStage.None, famine.Economy.Famine.Stage);
        Assert.True(resolved.Result.LivingCostPaid.IsPositive);
        Assert.Equal(Money.Zero, Assert.Single(ledger.Entries, e => e.Category == LedgerCategory.FamineResolved).Amount);

        // ② 贷款计息入欠息：同为事件类条目，可读但不参与求和。
        var loanRun = RunLoanInterestMonth();
        var loanStep = Assert.Single(loanRun.Steps);
        var accrual = Assert.Single(loanRun.State.Economy.Ledger.Entries, e => e.Category == LedgerCategory.LoanInterestAccrued);

        Assert.True(loanStep.Result.LoanInterestAccrued.IsPositive);
        Assert.Equal(loanStep.Result.LoanInterestAccrued, accrual.Amount);
        Assert.Equal(LedgerEntryKind.Event, LedgerCategoryMetadata.KindOf(accrual.Category));
        Assert.Null(accrual.PersonId);

        // 计息只增加负债：事件类条目不参与求和，故区间合计仍等于资金池变动。
        AssertMoneyIdentity(
            loanStep.PoolAfter - loanStep.PoolBefore,
            loanRun.State.Economy.Ledger.TreasuryDeltaIn(loanStep.Month, loanStep.Month));
        Assert.DoesNotContain(
            loanRun.State.Economy.Ledger.TotalsByCategory(loanStep.Month, loanStep.Month),
            t => t.Category == LedgerCategory.LoanInterestAccrued);
    }

    [Fact]
    public void 资金类条目金额非零而事件类可为零()
    {
        var run = RunFixture(RunMonths, Origin.Artisan);

        foreach (var step in run.Steps)
        {
            foreach (var entry in step.Result.Entries)
            {
                if (IsTreasury(entry))
                {
                    Assert.NotEqual(Money.Zero, entry.Amount);
                }
                else
                {
                    Assert.Equal(LedgerEntryKind.Event, LedgerCategoryMetadata.KindOf(entry.Category));
                }
            }
        }

        // 事件类条目允许 0 元（饥馑迁移一律 0），且仍入账可读——与资金类的不变量形成对照。
        var famine = NewFamineState();
        Settle(new MonthlySettlementEngine(new SeededRandomService(Seed), new GameStateClock(famine)), famine);

        var entered = Assert.Single(
            famine.Economy.Ledger.Entries, e => e.Category == LedgerCategory.FamineEntered);

        Assert.Equal(Money.Zero, entered.Amount);
    }

    [Fact]
    public void 条目数与资金事件一一对应不存在只动资金池而不落条目的路径()
    {
        var run = RunFixture(RunMonths, Origin.Artisan);

        foreach (var step in run.Steps)
        {
            var treasury = new List<LedgerEntry>();
            var events = new List<LedgerEntry>();

            foreach (var entry in step.Result.Entries)
            {
                (IsTreasury(entry) ? treasury : events).Add(entry);
            }

            // 资金类条目逐条对应一个已发生的资金事件：收入分项（含年度项）+ 实付生活费 + 先本后息两段。
            var expected = step.Result.Incomes.Count
                + (step.Result.LivingCostPaid.IsPositive ? 1 : 0)
                + (step.Result.LoanRepayment.PrincipalPart.IsPositive ? 1 : 0)
                + (step.Result.LoanRepayment.InterestPart.IsPositive ? 1 : 0);

            Assert.Equal(expected, treasury.Count);

            // 资金池动了就必然有资金类条目——「只动池而不落条目」在本阶段不可表达（FR-021）。
            if (step.PoolAfter != step.PoolBefore)
            {
                Assert.NotEmpty(treasury);
            }

            // 事件类条目只可能是五类迁移之一（种类由 LedgerCategoryMetadata 唯一决定）。
            Assert.All(events, e => Assert.Equal(LedgerEntryKind.Event, LedgerCategoryMetadata.KindOf(e.Category)));

            // 每条条目都落在本月的年月下（可读、可定位）。
            Assert.All(step.Result.Entries, e => Assert.Equal(step.Month, e.Date));
        }

        // 账本本身不含任何「未落条目」的池变动：全区间恒等。
        var from = run.Steps[0].Month;
        var to = run.Steps[^1].Month;

        AssertMoneyIdentity(
            run.Steps[^1].PoolAfter - run.Steps[0].PoolBefore,
            run.State.Economy.Ledger.TreasuryDeltaIn(from, to));
    }

    [Fact]
    public void 不存在任何月度汇总类型或字段且聚合每次从条目重算()
    {
        // 契约四 §4 不变量 6 / FR-021：源码级断言「不存在月度汇总类型/字段」。
        var core = typeof(Ledger).Assembly;

        var summaryTypes = core.GetTypes()
            .Where(t => t.Name.Contains("Summary", StringComparison.Ordinal)
                || t.Name.Contains("Monthly", StringComparison.Ordinal))
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        Assert.Empty(summaryTypes);

        // 字段层面：任何实体都 MUST NOT 出现形似月度汇总的字段（含编译期生成的支持字段）。
        foreach (var type in core.GetTypes())
        {
            foreach (var field in type.GetFields(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Assert.False(
                    field.Name.Contains("Summary", StringComparison.Ordinal)
                        || field.Name.Contains("Monthly", StringComparison.Ordinal),
                    $"{type.FullName}.{field.Name} 形似月度汇总字段（契约四 §4 不变量 6）。");
            }
        }

        // Ledger 自身的存储只有「条目列表 + 其只读视图」——没有可存放汇总的第二处。
        var storage = typeof(Ledger).GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.Equal(2, storage.Length);

        foreach (var field in storage)
        {
            Assert.True(
                field.FieldType == typeof(List<LedgerEntry>)
                    || field.FieldType == typeof(ReadOnlyCollection<LedgerEntry>),
                $"Ledger 只能持有条目本身，实际出现 {field.Name}（{field.FieldType}）。");
        }

        // 「每次从条目算」：追加一条同月条目后，同一区间的合计随之改变（不是预先存好的值）。
        var run = RunFixture(1);
        var ledger = run.State.Economy.Ledger;
        var month = run.Steps[0].Month;
        var before = ledger.TreasuryDeltaIn(month, month);

        run.State.Economy.Apply(LedgerCategory.LandRentIncome, null, Guan(1000m), month);

        AssertMoneyIdentity(before + Guan(1000m), ledger.TreasuryDeltaIn(month, month));
        Assert.Contains(ledger.TotalsByCategory(month, month), t => t.Category == LedgerCategory.LandRentIncome);
    }

    /// <summary>
    /// 金额恒等断言：差额 MUST 为 0（SC-005 与「两个维度同一批条目」共用）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 唯一被容忍的是 <see cref="decimal"/> **尾数饱和**带来的 1 ulp：「资金池 = 期初 + 逐条累加」与
    /// 「条目合计 = 从 0 逐条累加」是两次**数量级不同**的定点累加，尾数为 28~29 位有效数字，
    /// 当累计金额超出尾数上限时两侧会相差 1 ulp（本夹具 300 个区间中恰有一个落在该边界上，
    /// 差额 = 1e-23 文）。这是 decimal 的表示精度下限，**不是**「只动资金池而不落条目」——
    /// 后者表现为条目量级的差额，会被下面这条断言直接判红。
    /// </para>
    /// </remarks>
    private static void AssertMoneyIdentity(Money expected, Money actual)
    {
        var difference = expected - actual;

        if (difference.Wen == 0m)
        {
            return;
        }

        var magnitude = Absolute(expected.Wen);
        var actualMagnitude = Absolute(actual.Wen);

        if (actualMagnitude > magnitude)
        {
            magnitude = actualMagnitude;
        }

        Assert.True(
            Absolute(difference.Wen) <= Ulp(magnitude),
            FormattableString.Invariant(
                $"差额 {difference.Wen} 文（期望 {expected.Wen}、实际 {actual.Wen}）超出 decimal 尾数饱和的 1 ulp。"));
    }

    /// <summary>给定数量级下 <see cref="decimal"/> 的 1 ulp（28 位有效数字）。</summary>
    private static decimal Ulp(decimal magnitude)
    {
        var scaled = magnitude;
        var ulp = 1E-28m;

        while (scaled >= 1m)
        {
            scaled /= 10m;
            ulp *= 10m;
        }

        return ulp;
    }

    private static decimal Absolute(decimal value) => value < 0m ? -value : value;
    private static bool IsTreasury(LedgerEntry entry) =>
        LedgerCategoryMetadata.KindOf(entry.Category) == LedgerEntryKind.Treasury;

    private static void AssertSameEntries(List<LedgerEntry> expected, IReadOnlyList<LedgerEntry> actual)
    {
        Assert.Equal(expected.Count, actual.Count);

        for (var index = 0; index < expected.Count; index++)
        {
            Assert.Equal(expected[index], actual[index]);
        }
    }

    private static int IndexOf(Ledger ledger, List<LedgerEntry> slice)
    {
        if (slice.Count == 0)
        {
            return ledger.Entries.Count;
        }

        for (var index = 0; index + slice.Count <= ledger.Entries.Count; index++)
        {
            var same = true;

            for (var offset = 0; offset < slice.Count; offset++)
            {
                if (ledger.Entries[index + offset] != slice[offset])
                {
                    same = false;
                    break;
                }
            }

            if (same)
            {
                return index;
            }
        }

        return -1;
    }

    private static Person MemberOf(Family family, PersonId id) =>
        family.TryGet(id) ?? throw new InvalidOperationException($"夹具成员 {id} MUST 存在于家族中。");

    /// <summary>按夹具与固定种子跑满 <paramref name="months"/> 个结算月，并逐月留痕。</summary>
    private static FixtureRun RunFixture(
        int months, Origin origin = Origin.Scholar, EconomyFixtureSettings? settings = null)
    {
        var fixture = EconomyFixtures.Create(settings ?? new EconomyFixtureSettings());
        var state = new GameState(
            StateId, fixture.Date, Difficulty.Normal, origin, fixture.Family, fixture.Economy);
        var engine = new MonthlySettlementEngine(new SeededRandomService(Seed), new GameStateClock(state));
        var steps = new List<MonthStep>();

        for (var index = 0; index < months; index++)
        {
            steps.Add(Settle(engine, state));
        }

        return new FixtureRun(state, steps, fixture.ServingOfficialId, fixture.YouthId);
    }

    /// <summary>大额本金 + 计息计时恰好差 1 个月 → 首个结算月必触发一次计息（阈值取配置成员）。</summary>
    private static FixtureRun RunLoanInterestMonth() => RunFixture(
        1,
        settings: new EconomyFixtureSettings
        {
            LoanPrincipal = Money.FromGuan(2000m),
            LoanMonthsSinceInterest = InterestPolicy.InterestPeriodMonths - 1,
        });

    /// <summary>只含一名儿童的家族：无任何收入来源、无资产、零资金 → 必进入饥馑且不落生活费条目。</summary>
    private static GameState NewFamineState()
    {
        var family = RulesHarness.FamilyOf(RulesHarness.Member(91, Gender.Male, 8));
        var economy = new FamilyEconomy(
            LivingCostTable.InitialStandard,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury());

        return new GameState(StateId, RulesHarness.Date, Difficulty.Normal, Origin.Scholar, family, economy);
    }

    private static MonthStep Settle(MonthlySettlementEngine engine, GameState state)
    {
        var first = state.Economy.Ledger.Entries.Count;
        var result = engine.Settle(state);
        var entries = state.Economy.Ledger.Entries;
        var appended = new List<LedgerEntry>(entries.Count - first);

        for (var index = first; index < entries.Count; index++)
        {
            appended.Add(entries[index]);
        }

        return new MonthStep(
            result.Month, result.TreasuryPoolBefore, result.TreasuryPoolAfter, result, appended);
    }

    private static Money Guan(decimal value) => Money.FromGuan(value);

    /// <summary>一次结算的留痕：年月、资金池前后、快照，以及本次追加的那一段条目。</summary>
    private sealed record MonthStep(
        GameDate Month,
        Money PoolBefore,
        Money PoolAfter,
        SettlementResult Result,
        List<LedgerEntry> Appended);

    /// <summary>一个夹具长跑：存档（就地推进后）、逐月留痕与两名归档用例成员的标识。</summary>
    private sealed record FixtureRun(
        GameState State,
        List<MonthStep> Steps,
        PersonId OfficialId,
        PersonId YouthId);
}
