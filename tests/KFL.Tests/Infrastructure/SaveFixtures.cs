using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Tests.Fixtures;

namespace KFL.Tests.Infrastructure;

/// <summary>
/// 存档测试的**状态夹具**（004-save-system；tasks.md T020）。
/// </summary>
/// <remarks>
/// <para>
/// 只用 <c>KFL.Core</c> 的**公开** API 构造（测试工程没有 <c>InternalsVisibleTo</c>，这本身就是
/// 一条约束：能用公开 API 造出来的状态，才谈得上是「玩家真的会遇到的状态」）。
/// </para>
/// <para>
/// <b>它为什么必须「厚」</b>：契约十 §5 要求两个格式**信息集等价**，而「往返一致」最容易漏掉的
/// 恰恰是「两个格式都少存了同一个字段」——只有把每一类字段都填上**非默认值**，
/// 漏存才会在比对里暴露。故 <see cref="RichState"/> 覆盖下列 ≥14 类：
/// ① 状态标识与日期；② 难度与待生效难度；③ 出身；④ 家族名与士身份；⑤ 家主；
/// ⑥ 成员集合（含**归档**成员）；⑦ 血亲父/母引用；⑧ 辈分与年龄（生日）；
/// ⑨ 天赋与寿数；⑩ 可变属性（学业/体质/政绩/职业/在职月数）；⑪ 官阶；
/// ⑫ 状态位与四类计时（含 <c>EntryTrack</c>）；⑬ 科举履历；⑭ 配偶与前配偶；
/// ⑮ 三资金池 / 贷款 / 储蓄利率；⑯ 资产四项；⑰ 账本（家族级与成员级、正负号）；
/// ⑱ 饥馑阶段与计时；⑲ 外来者记账（落籍旁系 + 联姻对齐）。
/// </para>
/// <para>
/// <b>标识确定性</b>：状态标识是固定的 <see cref="Guid"/> 字面量，成员标识来自
/// <see cref="FamilyFixtures.Id"/>，夹具里不出现全局随机源（G-07）：标识一律写死。
/// </para>
/// </remarks>
public static class SaveFixtures
{
    /// <summary>厚状态的存档标识（固定值，便于断言头记录与主体一致）。</summary>
    public static readonly Guid RichStateId = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");

    /// <summary>薄状态的存档标识。</summary>
    public static readonly Guid PlainStateId = Guid.Parse("0b6f9e2a-3c4d-4e5f-8a9b-0c1d2e3f4a5b");

    /// <summary>厚状态的参照年月（第七年七月，与成员生日错开）。</summary>
    public static readonly GameDate ReferenceDate = new(12, 7);

    /// <summary>
    /// 厚状态：覆盖上列 ≥14 类字段，且每类都非默认值。
    /// </summary>
    /// <returns>一个新的状态实例（每次调用都重新构造，测试之间不共享可变对象）。</returns>
    public static GameState RichState()
    {
        var multi = FamilyFixtures.MultiGeneration();
        var family = multi.Family;

        // ⑥ 归档成员：一个身故、一个出嫁——「归档也要存」是最容易漏的一类。
        Member(family, multi.GranddaughterId).Status |= StatusFlag.Deceased;
        Member(family, multi.DaughterId).Status |= StatusFlag.MarriedOut;

        // ⑫ 状态位 / 计时 / 入仕途径（**次序即约束**：先 Status，再 Timers，最后 EntryTrack）。
        var son = Member(family, multi.SonId);
        son.Status |= StatusFlag.ServingSentence | StatusFlag.AwaitingPost;
        son.Timers = new StatusTimers(sentenceRemainingMonths: 6, awaitingPostRemainingMonths: 2);
        son.EntryTrack = AppointmentTrack.SecondClass;

        // ⑩ 可变属性；⑪ 官阶；⑨ 寿数（构造期）与天赋（构造期）。
        son.Study = 88;
        son.Health = 77;
        son.Merit = 42;
        son.Occupation = Occupation.Studying;
        son.MonthsInOffice = 14;
        son.Rank = new OfficialRank(5);

        // ⑬ 科举履历：两条，按时间升序（AppendDegree 自己会拒绝逆序）。
        son.AppendDegree(new DegreeRecord(
            DegreeLevel.JuRen,
            placement: null,
            new GameDate(4, 3),
            DegreeChangeCause.ExamPass));
        son.AppendDegree(new DegreeRecord(
            DegreeLevel.JinShi,
            ImperialPlacement.ZhuangYuan,
            new GameDate(5, 4),
            DegreeChangeCause.ExamPass,
            ImperialClass.FirstClass));

        // ⑭ 前配偶：先娶一个外来者，再解除——两条 FormerSpouseIds 与「配偶已被清空」同时出现。
        var wife = family.AddOutsider(FamilyFixtures.NewPerson(
            11,
            "陈孙续弦",
            Gender.Female,
            58,
            2,
            generation: 3));
        family.Marry(multi.GrandsonId, wife.Id);
        family.EndMarriage(multi.GrandsonId);

        // ⑲ 落籍旁系：买来的旁系经 SetOutsiderGeneration 记入「已落籍」集合。
        var collateral = family.AddOutsider(FamilyFixtures.NewPerson(
            10,
            "陈旁系",
            Gender.Male,
            35,
            5,
            generation: family.BoughtCollateralGeneration));
        family.SetOutsiderGeneration(collateral.Id, family.BoughtCollateralGeneration);

        var economy = RichEconomy(son.Id);
        var state = new GameState(
            RichStateId,
            ReferenceDate,
            Difficulty.Hard,
            Origin.Scholar,
            family,
            economy);

        state.PendingDifficulty = Difficulty.Hell;

        return state;
    }

    /// <summary>
    /// 薄状态：单人家族 + 默认经济，用于「只关心落盘/读档机制」的用例。
    /// </summary>
    /// <returns>一个新的状态实例。</returns>
    public static GameState PlainState()
    {
        var family = new Family("宋家三十年");
        var head = family.AddFoundingMember(FamilyFixtures.NewPerson(1, "宋家主", Gender.Male, 1, 1, generation: 0));
        family.SetHead(head.Id);

        var economy = new FamilyEconomy(LivingStandard.Normal, new GrainPriceIndex(1.000m));

        return new GameState(PlainStateId, new GameDate(1, 1), Difficulty.Normal, Origin.Farmer, family, economy);
    }

    /// <summary>取成员；不存在即抛（夹具自身的完整性检查）。</summary>
    /// <param name="family">家族。</param>
    /// <param name="id">成员标识。</param>
    /// <returns>成员。</returns>
    private static Person Member(Family family, PersonId id) =>
        family.TryGet(id) ?? throw new InvalidOperationException($"夹具成员不存在：{id}");

    /// <summary>构造厚经济聚合（⑮⑯⑰⑱）。</summary>
    /// <param name="officialId">官俸记账所属的成员。</param>
    /// <returns>经济聚合。</returns>
    private static FamilyEconomy RichEconomy(PersonId officialId)
    {
        // ⑮ 三资金池 + 贷款 + 储蓄利率（**利率先于所属年份**：Core 的 setter 就是这么互锁的）。
        var treasury = new Treasury(Money.FromGuan(120m), Money.FromGuan(45m), Money.FromGuan(30m));
        treasury.SavingsRateYear = ReferenceDate.Year;
        treasury.SavingsRate = 0.01m;

        var loan = new Loan
        {
            Principal = Money.FromGuan(20m),
            MonthsSinceInterest = 4,
        };
        loan.AccrueInterest(Money.FromGuan(1.25m));
        treasury.Loan = loan;

        // ⑯ 资产四项。
        var holdings = new Holdings
        {
            FarmlandMu = 33,
            RuralHouses = 2,
            UrbanHouses = 1,
            Shops = 3,
        };

        // ⑰ 账本：家族级与成员级各若干条、正负号都有、类别跨「资金」与「事件」两种。
        var ledger = new Ledger();
        ledger.Append(new LedgerEntry(
            new GameDate(12, 1), personId: null, LedgerCategory.LivingCost, Money.FromGuan(-3.75m)));
        ledger.Append(new LedgerEntry(
            new GameDate(12, 1), officialId, LedgerCategory.OfficialSalary, Money.FromGuan(12.5m)));
        ledger.Append(new LedgerEntry(
            new GameDate(12, 2), personId: null, LedgerCategory.ShopRentIncome, Money.FromGuan(7.5m)));
        ledger.Append(new LedgerEntry(
            new GameDate(12, 3), officialId, LedgerCategory.SavingsInterest, Money.FromGuan(2.5m)));
        ledger.Append(new LedgerEntry(
            new GameDate(12, 4), personId: null, LedgerCategory.AssetPurchase, Money.FromGuan(-1000m)));
        ledger.Append(new LedgerEntry(
            new GameDate(12, 4), personId: null, LedgerCategory.FamineEntered, Money.Zero));

        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(1.25m),
            treasury,
            holdings,
            ledger)
        {
            PendingLivingStandard = LivingStandard.Comfortable,
        };

        // ⑱ 饥馑：转入当月记 1，再走一个月 ⇒ 计时 2（「阶段非 None ⇒ 计时 ≥ 1」的一致性由校验器管）。
        economy.Famine.TransitionTo(FamineStage.Famine);
        economy.Famine.Tick();

        return economy;
    }
}
