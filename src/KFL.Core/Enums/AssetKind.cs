namespace KFL.Core.Enums;

/// <summary>
/// 资产种类（规格书 §5.3）。
/// </summary>
/// <remarks>
/// **明确不含**：单价（田 1 / 农村宅 10 / 城市宅 100 / 铺面 300 贯）、铺面年租 20%、
/// 每成人 20 亩的农田上限——全部属 <c>KFL.Rules/Config</c>（<c>AssetPriceTable</c> /
/// <c>IncomeRateTable</c>，research R-01）。
/// </remarks>
public enum AssetKind
{
    /// <summary>田（单位：亩）。</summary>
    Farmland,

    /// <summary>农村宅（单位：座）。</summary>
    RuralHouse,

    /// <summary>城市宅（单位：座）。</summary>
    UrbanHouse,

    /// <summary>铺面（单位：间）。</summary>
    Shop,
}
