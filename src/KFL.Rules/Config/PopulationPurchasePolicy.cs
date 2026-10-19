namespace KFL.Rules.Config;

/// <summary>
/// 收养登记（规格书 §9.5；契约二十一 §3.3）——本特性的**唯一**声明处。
/// </summary>
/// <remarks>
/// 递增计价基数（本年第 <c>n</c> 名 = <c>基数 × n</c> 贯）与被收养者的年龄区间。
/// 金额 MUST 经 <c>Money.FromGuan(PopulationPurchasePolicy.BasePriceGuan * n)</c> 构造，
/// MUST NOT 写字面量。
/// </remarks>
public static class PopulationPurchasePolicy
{
    /// <summary>递增计价基数（贯）：本年第 <c>n</c> 名 = 基数 × <c>n</c>。</summary>
    public const int BasePriceGuan = 10;

    /// <summary>被收养者的年龄下界（含）。</summary>
    public const int AgeMin = 8;

    /// <summary>被收养者的年龄上界（含）。</summary>
    public const int AgeMax = 16;
}
