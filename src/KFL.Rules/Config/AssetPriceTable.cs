using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Rules.Config;

/// <summary>
/// 资产价格表与市值口径（规格书 §5.3；data-model §4.1；research R-15）。
/// </summary>
/// <remarks>
/// <para>
/// 田宅铺**购售同价**；本类是四个单价与铺面年租率的唯一出处（SC-008）。
/// </para>
/// <para>
/// **市值口径** = 田 × 1 + 农村宅 × 10 + 城市宅 × 100 + 铺面 × 300（贯），**不含商本池**
/// （§5.2 的 2026-10-06 裁决；与 §7.5 罚金基数同口径）。工出身 bonus 与将来的罚金共用它。
/// </para>
/// </remarks>
public static class AssetPriceTable
{
    /// <summary>田单价（贯/亩）。</summary>
    public const decimal FarmlandGuanPerMu = 1m;

    /// <summary>农村宅单价（贯/座）。</summary>
    public const decimal RuralHouseGuan = 10m;

    /// <summary>城市宅单价（贯/座）。</summary>
    public const decimal UrbanHouseGuan = 100m;

    /// <summary>铺面单价（贯/间）。</summary>
    public const decimal ShopGuan = 300m;

    /// <summary>铺面年租率（月摊 = 单价 × 年租率 ÷ 12）。</summary>
    public const decimal ShopRentRate = 0.20m;

    /// <summary>取资产单价（购售同价）。</summary>
    /// <param name="kind">资产种类。</param>
    /// <returns>单价。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> 不在四种资产之内。</exception>
    public static Money UnitPrice(AssetKind kind) => Money.FromGuan(kind switch
    {
        AssetKind.Farmland => FarmlandGuanPerMu,
        AssetKind.RuralHouse => RuralHouseGuan,
        AssetKind.UrbanHouse => UrbanHouseGuan,
        AssetKind.Shop => ShopGuan,
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind), kind, "未登记的资产种类（规格书 §5.3 共四种）。"),
    });

    /// <summary>田宅铺市值（贯；**不含商本池**）。</summary>
    /// <param name="holdings">资产组合。</param>
    /// <returns>市值。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="holdings"/> 为 <c>null</c>。</exception>
    public static Money MarketValue(Holdings holdings)
    {
        ArgumentNullException.ThrowIfNull(holdings);

        var guan = (holdings.FarmlandMu * FarmlandGuanPerMu)
            + (holdings.RuralHouses * RuralHouseGuan)
            + (holdings.UrbanHouses * UrbanHouseGuan)
            + (holdings.Shops * ShopGuan);

        return Money.FromGuan(guan);
    }

    /// <summary>铺面月租总额 = 间数 × 单价 × 年租率 ÷ 12。</summary>
    /// <param name="shops">铺面间数。</param>
    /// <returns>月租；间数为 0 时为 0。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="shops"/> 为负。</exception>
    public static Money ShopMonthlyRent(int shops)
    {
        if (shops < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shops), shops, "铺面间数 MUST >= 0。");
        }

        return Money.FromGuan(shops * ShopGuan * ShopRentRate / IncomeRateTable.MonthsPerYear);
    }
}
