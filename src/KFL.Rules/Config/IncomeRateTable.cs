using KFL.Core.ValueObjects;

namespace KFL.Rules.Config;

/// <summary>
/// 收入系数表（规格书 §5.2、§5.3；data-model §4.1；research R-18/R-19）。
/// </summary>
/// <remarks>
/// <para>
/// 本类是「自耕亩产率与每人上限」「田租亩产率」「务农 / 做工月额」「城市宅加成」
/// 「经商收益率与门槛与商出身加成」「天赋除数」「工出身 bonus 率」的唯一出处（SC-008）。
/// </para>
/// <para>
/// **天赋除数的口径**（E-03）：<c>农</c> 与 <c>商</c> 除以 <see cref="AgricultureTalentDivisor"/> /
/// <see cref="CommerceTalentDivisor"/>，<c>工</c> 除以 <see cref="CraftTalentDivisor"/>；
/// 自耕、田租、务农取 <see cref="FarmingEfficiency"/>（农 × 工），做工取
/// <see cref="CraftFactor"/>，经商取 <see cref="CommerceFactor"/> × <see cref="CraftFactor"/>。
/// </para>
/// </remarks>
public static class IncomeRateTable
{
    /// <summary>一年十二个月的月摊除数（自耕 / 田租的「年率 ÷ 月数」）。</summary>
    public const decimal MonthsPerYear = 12m;

    /// <summary>自耕亩产率（贯/亩/年，规格书 §5.2 的公式口径）。</summary>
    public const decimal SelfFarmingGuanPerMuPerYear = 0.5m;

    /// <summary>每人自耕上限（亩，含边界：第 21 亩起转田租）。</summary>
    public const int FarmlandPerCapitaMu = 20;

    /// <summary>田租亩产率（贯/亩/年）。</summary>
    public const decimal LandRentGuanPerMuPerYear = 0.1m;

    /// <summary>务农月额（贯/月；仅家族无田可耕时，按每名计口成年成员一份）。</summary>
    public const decimal FarmingWageGuanPerMonth = 2m;

    /// <summary>做工月额（贯/月；每名被指派「做工」的计口成年成员一份）。</summary>
    public const decimal CraftingGuanPerMonth = 1.5m;

    /// <summary>城市宅加成（贯/月；份数 = `min(做工人数, 城市宅数)`）。</summary>
    public const decimal UrbanHouseCraftingBonusGuanPerMonth = 1m;

    /// <summary>经商月收益率（商本 × 2%）。</summary>
    public const decimal TradeProfitRate = 0.02m;

    /// <summary>经商门槛（贯，**含**边界：`商本 ≥ 100` 才产生收益）。</summary>
    public const decimal TradeCapitalThresholdGuan = 100m;

    /// <summary>商出身经商加成。</summary>
    public const decimal MerchantOriginMultiplier = 1.1m;

    /// <summary>工出身 bonus 率（12 月末，基数为正时发放）。</summary>
    public const decimal ArtisanBonusRate = 0.06m;

    /// <summary>农天赋除数（`1 + 农 / 200`）。</summary>
    public const decimal AgricultureTalentDivisor = 200m;

    /// <summary>工天赋除数（`1 + 工 / 400`）。</summary>
    public const decimal CraftTalentDivisor = 400m;

    /// <summary>商天赋除数（`1 + 商 / 200`）。</summary>
    public const decimal CommerceTalentDivisor = 200m;

    /// <summary>农天赋乘数 `1 + 农 / 200`。</summary>
    /// <param name="talents">成员天赋。</param>
    /// <returns>乘数。</returns>
    public static decimal AgricultureFactor(TalentSet talents) =>
        1m + (talents.Agriculture / AgricultureTalentDivisor);

    /// <summary>工天赋乘数 `1 + 工 / 400`。</summary>
    /// <param name="talents">成员天赋。</param>
    /// <returns>乘数。</returns>
    public static decimal CraftFactor(TalentSet talents) =>
        1m + (talents.Craft / CraftTalentDivisor);

    /// <summary>商天赋乘数 `1 + 商 / 200`。</summary>
    /// <param name="talents">成员天赋。</param>
    /// <returns>乘数。</returns>
    public static decimal CommerceFactor(TalentSet talents) =>
        1m + (talents.Commerce / CommerceTalentDivisor);

    /// <summary>耕作效率 = 农乘数 × 工乘数（自耕 / 田租 / 务农共用）。</summary>
    /// <param name="talents">成员天赋。</param>
    /// <returns>耕作效率。</returns>
    public static decimal FarmingEfficiency(TalentSet talents) =>
        AgricultureFactor(talents) * CraftFactor(talents);
}
