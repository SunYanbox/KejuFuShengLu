using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;

namespace KFL.Rules.Settlement;

/// <summary>
/// 一次收入计算的产物：各来源分项（类别 + 归属成员 + 金额）与合计。
/// </summary>
public sealed class IncomeComputation
{
    /// <summary>构造并累加合计。</summary>
    /// <param name="lines">各来源分项。</param>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> 为 <c>null</c>。</exception>
    public IncomeComputation(IReadOnlyList<IncomeLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        Lines = lines;

        var total = Money.Zero;
        foreach (var line in lines)
        {
            total += line.Amount;
        }

        Total = total;
    }

    /// <summary>各来源分项，按计算顺序（自耕 → 田租 → 务农 → 做工 → 经商 → 官俸 → 铺面租）。</summary>
    public IReadOnlyList<IncomeLine> Lines { get; }

    /// <summary>收入合计。</summary>
    public Money Total { get; }
}

/// <summary>
/// 收入计算器（规格书 §5.2、§5.3、§8.1、§11；data-model §4.3；契约三 §6）。
/// </summary>
/// <remarks>
/// <para>
/// **纯函数，不碰资金池**：只读家族、资产与商本，产出分项金额与归属成员。
/// </para>
/// <para>
/// **人群口径（E-16）**：凡公式里的「计口成年成员」一律 = 计口 ∧ 已成年（男满 12 / 女满 14，
/// 生日当月生效，**无年龄上限**，故青年与老人可被指派、未成年不可）。
/// </para>
/// <para>
/// **叠加**：自耕 / 田租 / 务农 / 做工 / 经商互不排斥（一名成员可同时贡献自耕与做工）；
/// 但 <see cref="Occupation"/> 是单一取值，故「做工」与「经商」不可同时指派。
/// 难度收益系数对本类产出的**全部**收入生效（储蓄利息不在此类，见 <c>SavingsSettlement</c>）。
/// </para>
/// <para>
/// <b>为何没有「仕身份」入参</b>：§5.2 的收入公式不含仕族加成（仕族只影响录取率、婚嫁规格、
/// 贷款划扣比例与买功名婚姻），故本方法 MUST NOT 接收它——贷款划扣比例由 <c>LoanPolicy</c> 读族级状态。
/// </para>
/// </remarks>
public static class IncomeCalculator
{
    /// <summary>计算当月全部收入来源分项。</summary>
    /// <param name="family">家族（成员、职业指派与家主）。</param>
    /// <param name="date">当月年月（年龄档归属以它为准）。</param>
    /// <param name="holdings">资产组合（田 / 城市宅 / 铺面）。</param>
    /// <param name="treasury">资金状态（**只读**其商本池，用于经商收益基数）。</param>
    /// <param name="difficulty">当月生效的难度（收益系数）。</param>
    /// <param name="origin">存档出身（商出身经商加成）。</param>
    /// <returns>分项与合计。</returns>
    /// <exception cref="ArgumentNullException">任一引用参数为 <c>null</c>。</exception>
    public static IncomeComputation Compute(
        Family family,
        GameDate date,
        Holdings holdings,
        Treasury treasury,
        Difficulty difficulty,
        Origin origin)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(holdings);
        ArgumentNullException.ThrowIfNull(treasury);

        var counted = CountedMembers.Counted(family);
        var assignable = CountedMembers.Assignable(family, date);
        var revenue = DifficultyRates.RevenueFactor(difficulty);

        var lines = new List<IncomeLine>();

        AddSelfFarming(lines, assignable, holdings, family, counted, date, revenue);
        AddFarmingWage(lines, assignable, holdings, date, revenue);
        AddCrafting(lines, assignable, holdings, date, revenue);
        AddTrade(lines, assignable, treasury.MerchantCapital, origin, date, revenue);
        AddSalaries(lines, counted, origin, revenue);
        AddShopRent(lines, holdings, revenue);

        return new IncomeComputation(lines);
    }

    /// <summary>自耕 + 田租。亩数按耕作效率降序填满每人 20 亩，第 21 亩起转田租。</summary>
    private static void AddSelfFarming(
        List<IncomeLine> lines,
        IReadOnlyList<Person> assignable,
        Holdings holdings,
        Family family,
        IReadOnlyList<Person> counted,
        GameDate date,
        decimal revenue)
    {
        var remaining = holdings.FarmlandMu;

        if (remaining > 0 && assignable.Count > 0)
        {
            foreach (var person in OrderByEfficiency(assignable, date, p => IncomeRateTable.FarmingEfficiency(p.Talents)))
            {
                if (remaining <= 0)
                {
                    break;
                }

                var mu = Math.Min(IncomeRateTable.FarmlandPerCapitaMu, remaining);
                remaining -= mu;

                var guan = mu * IncomeRateTable.SelfFarmingGuanPerMuPerYear / IncomeRateTable.MonthsPerYear
                    * IncomeRateTable.FarmingEfficiency(person.Talents) * revenue;

                Add(lines, LedgerCategory.SelfFarmingIncome, person.Id, guan);
            }
        }

        if (remaining > 0)
        {
            var headFactor = HeadFarmingFactor(family, counted);
            var guan = remaining * IncomeRateTable.LandRentGuanPerMuPerYear / IncomeRateTable.MonthsPerYear
                * headFactor * revenue;

            Add(lines, LedgerCategory.LandRentIncome, null, guan);
        }
    }

    /// <summary>务农：仅在家族**无田可耕**（<c>FarmlandMu == 0</c>，E-17）时，每名计口成年成员一份。</summary>
    private static void AddFarmingWage(
        List<IncomeLine> lines,
        IReadOnlyList<Person> assignable,
        Holdings holdings,
        GameDate date,
        decimal revenue)
    {
        if (holdings.FarmlandMu != 0)
        {
            return;
        }

        foreach (var person in OrderByEfficiency(assignable, date, p => IncomeRateTable.FarmingEfficiency(p.Talents)))
        {
            var guan = IncomeRateTable.FarmingWageGuanPerMonth
                * IncomeRateTable.FarmingEfficiency(person.Talents) * revenue;

            Add(lines, LedgerCategory.FarmingWageIncome, person.Id, guan);
        }
    }

    /// <summary>做工（每名被指派者一份）+ 城市宅加成（`min(做工人数, 城市宅数)` 份、按本人工乘数降序逐人归属）。</summary>
    private static void AddCrafting(
        List<IncomeLine> lines,
        IReadOnlyList<Person> assignable,
        Holdings holdings,
        GameDate date,
        decimal revenue)
    {
        var workers = assignable.Where(p => p.Occupation == Occupation.Crafting).ToList();

        if (workers.Count == 0)
        {
            return;
        }

        foreach (var worker in OrderByEfficiency(workers, date, p => IncomeRateTable.CraftFactor(p.Talents)))
        {
            var guan = IncomeRateTable.CraftingGuanPerMonth * IncomeRateTable.CraftFactor(worker.Talents) * revenue;
            Add(lines, LedgerCategory.CraftingIncome, worker.Id, guan);
        }

        var shares = Math.Min(workers.Count, holdings.UrbanHouses);

        foreach (var worker in OrderByEfficiency(workers, date, p => IncomeRateTable.CraftFactor(p.Talents)).Take(shares))
        {
            var guan = IncomeRateTable.UrbanHouseCraftingBonusGuanPerMonth
                * IncomeRateTable.CraftFactor(worker.Talents) * revenue;

            Add(lines, LedgerCategory.CraftingIncome, worker.Id, guan);
        }
    }

    /// <summary>经商：商本 ≥ 门槛且 ≥1 名计口成年成员指派经商；单一份，归属乘数最大者（E-18）。</summary>
    private static void AddTrade(
        List<IncomeLine> lines,
        IReadOnlyList<Person> assignable,
        Money merchantCapital,
        Origin origin,
        GameDate date,
        decimal revenue)
    {
        if (merchantCapital.Guan < IncomeRateTable.TradeCapitalThresholdGuan)
        {
            return;
        }

        var traders = assignable.Where(p => p.Occupation == Occupation.Trading).ToList();

        if (traders.Count == 0)
        {
            return;
        }

        var chosen = OrderByEfficiency(traders, date, TradeEfficiency).First();

        var guan = merchantCapital.Guan
            * IncomeRateTable.TradeProfitRate
            * TradeEfficiency(chosen)
            * (origin == Origin.Merchant ? IncomeRateTable.MerchantOriginMultiplier : 1m)
            * revenue;

        Add(lines, LedgerCategory.TradeIncome, chosen.Id, guan);
    }

    /// <summary>官俸：`年俸 ÷ 12 × 收益系数`，士出身当官再 ×1.05。</summary>
    private static void AddSalaries(
        List<IncomeLine> lines,
        IReadOnlyList<Person> counted,
        Origin origin,
        decimal revenue)
    {
        var originMultiplier = origin == Origin.Scholar ? SalaryTable.ScholarOriginMultiplier : 1m;

        foreach (var person in counted)
        {
            if (person.Rank is not { } rank)
            {
                continue;
            }

            var guan = SalaryTable.MonthlySalaryGuan(rank.Level) * originMultiplier * revenue;
            Add(lines, LedgerCategory.OfficialSalary, person.Id, guan);
        }
    }

    /// <summary>铺面租：间数 × 单价 × 年租率 ÷ 12。</summary>
    private static void AddShopRent(List<IncomeLine> lines, Holdings holdings, decimal revenue)
    {
        if (holdings.Shops == 0)
        {
            return;
        }

        // 铺面月租已由配置表以 Money（文）给出，故直接乘系数——不再经 .Guan（÷1000）往返
        // 到「贯」再在 Add 里 ×1000 回「文」（Money 的唯一存储口径是文）。
        Add(lines, LedgerCategory.ShopRentIncome, null, AssetPriceTable.ShopMonthlyRent(holdings.Shops) * revenue);
    }

    /// <summary>经商效率 = 商乘数 × 工乘数。</summary>
    private static decimal TradeEfficiency(Person person) =>
        IncomeRateTable.CommerceFactor(person.Talents) * IncomeRateTable.CraftFactor(person.Talents);

    /// <summary>
    /// 家族级来源（田租）的天赋乘数：取**家主**的耕作效率；家主为 <c>null</c> 或非计口成员时取 `1.0`（E-03）。
    /// </summary>
    private static decimal HeadFarmingFactor(Family family, IReadOnlyList<Person> counted)
    {
        if (family.HeadId is not { } headId)
        {
            return 1m;
        }

        foreach (var person in counted)
        {
            if (person.Id == headId)
            {
                return IncomeRateTable.FarmingEfficiency(person.Talents);
            }
        }

        return 1m;
    }

    /// <summary>按效率降序、并列时年龄降序、再按 <see cref="PersonId"/> 升序（E-11 / E-15 / E-18 的确定性排序）。</summary>
    private static IEnumerable<Person> OrderByEfficiency(
        IEnumerable<Person> people, GameDate date, Func<Person, decimal> efficiency) =>
        people.OrderByDescending(efficiency)
            .ThenByDescending(p => p.AgeAt(date))
            .ThenBy(p => p.Id.Value);

    /// <summary>只落**正额**分项：0 金额既非收入也无条目（资金类条目金额 MUST 非 0）。</summary>
    private static void Add(List<IncomeLine> lines, LedgerCategory category, PersonId? personId, decimal guan)
    {
        if (guan <= 0m)
        {
            return;
        }

        lines.Add(new IncomeLine(category, personId, Money.FromGuan(guan)));
    }

    /// <summary>
    /// 只落**正额**分项（已是 <see cref="Money"/> 的金额用这一重载）：免去 `.Guan`（÷1000）→
    /// `Add` 内 `FromGuan`（×1000）的口径往返（data-model §1.1：唯一存储以文为单位）。
    /// </summary>
    private static void Add(List<IncomeLine> lines, LedgerCategory category, PersonId? personId, Money amount)
    {
        if (!amount.IsPositive)
        {
            return;
        }

        lines.Add(new IncomeLine(category, personId, amount));
    }
}
