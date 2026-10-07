using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Tests.Fixtures;

/// <summary>
/// T019：**可复现的经济夹具**（research R-16）——多代同堂（儿童 / 青年 / 成人 / 老人四档齐备）
/// + 田 + 农村宅 + 城市宅 + 铺面 + 一名在职位成员 + 可注入的贷款（本金 / 欠息 / 距上次计息月数）
/// + 储蓄（含储蓄利率）+ 商本 + 米价系数 + 饥馑阶段 + 生活费档位。
/// </summary>
/// <remarks>
/// <para>
/// 全部初值都来自 <see cref="EconomyFixtureSettings"/> 的**显式取值**：默认值本身也是固定常量，
/// 不是环境读取。夹具 MUST NOT 依赖环境，且只用 <c>KFL.Core</c> 的公开 API 构造
/// （MUST NOT 走反射绕过不变量校验）。
/// </para>
/// <para>
/// 家族骨架复用 <see cref="FamilyFixtures.MultiGeneration"/>（四代九人），只**追加**一名儿童成员。
/// 年龄档的归属（成年边界 12/14、青年上界 18、老人下界 60）由
/// <c>KFL.Rules/Config/AgeBracketPolicy</c> 判定；本夹具只保证各成员的出生年月在固定参照年月下
/// 分属四档，MUST NOT 把边界数值抄成第二处出处（SC-008）。
/// </para>
/// <para>
/// 生活费档位 <see cref="LivingStandard.Normal"/> 与米价系数 <c>1</c> 是**夹具取值**：
/// 两者的产品初值分别单点在 <c>KFL.Rules/Config/LivingCostTable.InitialStandard</c> 与
/// <c>GrainPricePolicy</c>（data-model §3.6、SC-008）。
/// </para>
/// </remarks>
public static class EconomyFixtures
{
    /// <summary>固定参照年月：各成员的年龄档归属都以它为准，MUST NOT 取自环境时钟。</summary>
    public static GameDate ReferenceDate => new(80, 1);

    /// <summary>取默认夹具（全部初值即 <see cref="EconomyFixtureSettings"/> 的固定默认值）。</summary>
    /// <returns>可复现的经济夹具。</returns>
    public static EconomyFixture Create() => Create(new EconomyFixtureSettings());

    /// <summary>按显式初值构造夹具。</summary>
    /// <param name="settings">全部初值。</param>
    /// <returns>可复现的经济夹具。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> 为 <c>null</c>。</exception>
    public static EconomyFixture Create(EconomyFixtureSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // 家族骨架复用既有夹具；MultiGeneration 每次返回新的家族实例，故此处不共享可变状态。
        var household = FamilyFixtures.MultiGeneration();
        var family = household.Family;

        // 追加儿童（辈分 = 血亲父母辈分 + 1）：出生年月由参照年月回推，与档位边界保持距离。
        var child = family.AddChild(FamilyFixtures.NewPerson(
            501,
            "陈幼",
            Gender.Male,
            settings.Date.Year - 8,
            1,
            generation: 3,
            fatherId: household.SonId,
            motherId: household.DaughterInLawId));

        // 在职位成员：官阶只给一个合法取值，俸禄数值属 KFL.Rules/Config/SalaryTable。
        var servingOfficialId = household.SonId;
        var official = family.TryGet(servingOfficialId)
            ?? throw new InvalidOperationException("夹具的在职位成员 MUST 存在于家族中。");
        official.Rank = new OfficialRank(settings.OfficialRankLevel);

        family.HasShiStatus = settings.HasShiStatus;

        var treasury = new Treasury(settings.Cash, settings.Savings, settings.MerchantCapital)
        {
            Loan = BuildLoan(settings),
        };

        if (settings.SavingsRate is { } rate)
        {
            // 赋值次序固定：先置年份，再置利率（Treasury 的交叉校验，data-model §3.1）。
            treasury.SavingsRateYear = settings.SavingsRateYear ?? settings.Date.Year;
            treasury.SavingsRate = rate;
        }

        var holdings = new Holdings();
        holdings.Add(AssetKind.Farmland, settings.FarmlandMu);
        holdings.Add(AssetKind.RuralHouse, settings.RuralHouses);
        holdings.Add(AssetKind.UrbanHouse, settings.UrbanHouses);
        holdings.Add(AssetKind.Shop, settings.Shops);

        var economy = new FamilyEconomy(
            settings.LivingStandard,
            new GrainPriceIndex(settings.GrainPriceIndex),
            treasury,
            holdings)
        {
            PendingLivingStandard = settings.PendingLivingStandard,
        };

        if (settings.FamineStage != FamineStage.None)
        {
            economy.Famine.TransitionTo(settings.FamineStage);
        }

        return new EconomyFixture
        {
            Settings = settings,
            Date = settings.Date,
            Family = family,
            Economy = economy,
            ChildId = child.Id,
            YouthId = household.GranddaughterId,
            AdultId = household.GrandsonId,
            ElderId = household.FounderId,
            ServingOfficialId = servingOfficialId,
        };
    }

    /// <summary>按显式初值构造贷款：本金 / 欠息 / 距上次计息月数三件都可注入。</summary>
    /// <param name="settings">全部初值。</param>
    /// <returns>已注入贷款的实例。</returns>
    private static Loan BuildLoan(EconomyFixtureSettings settings)
    {
        var loan = new Loan
        {
            Principal = settings.LoanPrincipal,
            MonthsSinceInterest = settings.LoanMonthsSinceInterest,
        };

        // 欠息的唯一写入通道是 AccrueInterest（AccruedInterest 只读，data-model §3.2）。
        loan.AccrueInterest(settings.LoanAccruedInterest);

        return loan;
    }
}

/// <summary>
/// 经济夹具的全部初值。每个成员的默认值都是**固定常量**，与运行环境无关。
/// </summary>
public sealed record EconomyFixtureSettings
{
    /// <summary>参照年月；各成员的年龄档归属以它为准。</summary>
    public GameDate Date { get; init; } = EconomyFixtures.ReferenceDate;

    /// <summary>当前生效的生活费档位（夹具取值，产品初值单点在 Rules）。</summary>
    public LivingStandard LivingStandard { get; init; } = LivingStandard.Normal;

    /// <summary>待生效的生活费档位；<c>null</c> = 无待生效切换。</summary>
    public LivingStandard? PendingLivingStandard { get; init; }

    /// <summary>米价系数（夹具取值，产品初值单点在 Rules；MUST <c>&gt; 0</c>）。</summary>
    public decimal GrainPriceIndex { get; init; } = 1m;

    /// <summary>现金。</summary>
    public Money Cash { get; init; } = Money.FromGuan(200m);

    /// <summary>储蓄。</summary>
    public Money Savings { get; init; } = Money.FromGuan(50m);

    /// <summary>当年储蓄利率；<c>null</c> = 尚未 roll 过。</summary>
    public decimal? SavingsRate { get; init; } = 0.01m;

    /// <summary>储蓄利率所属年份；<c>null</c> = 取 <see cref="Date"/> 的年份。</summary>
    public int? SavingsRateYear { get; init; }

    /// <summary>商本池。</summary>
    public Money MerchantCapital { get; init; } = Money.FromGuan(120m);

    /// <summary>贷款本金。</summary>
    public Money LoanPrincipal { get; init; } = Money.FromGuan(30m);

    /// <summary>贷款欠息。</summary>
    public Money LoanAccruedInterest { get; init; } = Money.FromGuan(2m);

    /// <summary>距上次计息的**自然月**数。</summary>
    public int LoanMonthsSinceInterest { get; init; } = 7;

    /// <summary>饥馑阶段；非 <see cref="FamineStage.None"/> 时按「转入当月记 1」置入。</summary>
    public FamineStage FamineStage { get; init; } = FamineStage.None;

    /// <summary>田（亩）。</summary>
    public int FarmlandMu { get; init; } = 40;

    /// <summary>农村宅（座）。</summary>
    public int RuralHouses { get; init; } = 1;

    /// <summary>城市宅（座）。</summary>
    public int UrbanHouses { get; init; } = 1;

    /// <summary>铺面（间）。</summary>
    public int Shops { get; init; } = 1;

    /// <summary>仕身份（进士直系血统，家族级）。</summary>
    public bool HasShiStatus { get; init; } = true;

    /// <summary>在职位成员的官阶（合法取值 1~18）。</summary>
    public int OfficialRankLevel { get; init; } = 5;
}

/// <summary>
/// 一个可复现的经济夹具：家族、经济聚合与四年龄档成员的确定性标识。
/// </summary>
public sealed record EconomyFixture
{
    /// <summary>本夹具的全部初值。</summary>
    public required EconomyFixtureSettings Settings { get; init; }

    /// <summary>参照年月（各成员在此年月下的年龄决定其所属档位）。</summary>
    public required GameDate Date { get; init; }

    /// <summary>多代同堂的家族。</summary>
    public required Family Family { get; init; }

    /// <summary>经济聚合（资金 / 资产 / 账本 / 米价 / 档位 / 饥馑的唯一落点）。</summary>
    public required FamilyEconomy Economy { get; init; }

    /// <summary>儿童档成员（追加的第五代）。</summary>
    public required PersonId ChildId { get; init; }

    /// <summary>青年档成员（第三代女儿）。</summary>
    public required PersonId YouthId { get; init; }

    /// <summary>成人档成员（第四代孙）。</summary>
    public required PersonId AdultId { get; init; }

    /// <summary>老人档成员（第一代创始人）。</summary>
    public required PersonId ElderId { get; init; }

    /// <summary>在职位成员（第三代子，成人档，已授官阶）。</summary>
    public required PersonId ServingOfficialId { get; init; }
}
