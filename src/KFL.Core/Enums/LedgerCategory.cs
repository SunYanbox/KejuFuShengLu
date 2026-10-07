namespace KFL.Core.Enums;

/// <summary>
/// 家族流水账的类别全集（19 个取值；data-model §2.1，逐行取自规格书 §12.3 与 §5.1~§5.4）。
/// </summary>
/// <remarks>
/// <para>
/// 类别 → 条目种类与正额入账目标的**唯一真源**是 <see cref="LedgerCategoryMetadata"/>；
/// 本枚举 MUST NOT 另存一份分类副本（E-08：条目是「结构事实」，不是平衡数值）。
/// </para>
/// <para>
/// **明确不含**（§17 裁决）：
/// ① **不**为 <see cref="Occupation"/> 新增「自耕」「务农」等取值——收入来源是**账本类别**，
/// 不是职业（E-02）；
/// ② **不**出现「买人口」「聘礼 / 嫁妆」「贿赂」等阶段⑤⑥⑧ 的类别（FR-028：本阶段 MUST NOT 存在该类型，
/// 以「无该类别」断言）。
/// </para>
/// </remarks>
public enum LedgerCategory
{
    /// <summary>生活费（资金类，恒为负；规格书 §5.1）。</summary>
    LivingCost,

    /// <summary>自耕收入（资金类，正额入现金；§5.2）。</summary>
    SelfFarmingIncome,

    /// <summary>田租收入（资金类，正额入现金；§5.2）。</summary>
    LandRentIncome,

    /// <summary>务农收入（资金类，正额入现金；§5.2）。</summary>
    FarmingWageIncome,

    /// <summary>做工收入（资金类，正额入现金；§5.2）。</summary>
    CraftingIncome,

    /// <summary>经商收益（资金类，正额入现金；§5.2）。</summary>
    TradeIncome,

    /// <summary>官俸（资金类，正额入现金；§5.2、§8.1）。</summary>
    OfficialSalary,

    /// <summary>铺面租（资金类，正额入现金；§5.3）。</summary>
    ShopRentIncome,

    /// <summary>工出身 bonus（资金类，正额入现金；§5.2；仅 12 月）。</summary>
    ArtisanBonus,

    /// <summary>储蓄利息（资金类，正额入**储蓄**；§5.4；仅 12 月）。</summary>
    SavingsInterest,

    /// <summary>偿还本金（资金类，恒为负；§5.4）。</summary>
    LoanPrincipalRepaid,

    /// <summary>偿还欠息（资金类，恒为负；§5.4）。</summary>
    LoanInterestRepaid,

    /// <summary>资产购入（资金类，恒为负；§5.3）。</summary>
    AssetPurchase,

    /// <summary>资产售出（资金类，正额入现金；§5.3）。</summary>
    AssetSale,

    /// <summary>贷款计息入欠息（**事件类**，不引起资金池变动；§5.4）。</summary>
    LoanInterestAccrued,

    /// <summary>进入饥馑（**事件类**；§5.4；spec US4 AS1）。</summary>
    FamineEntered,

    /// <summary>转入救济模式（**事件类**；§5.4）。</summary>
    FamineReliefEntered,

    /// <summary>进入第三阶段（**事件类**；§5.4）。</summary>
    FamineSevereEntered,

    /// <summary>饥馑全部解除（**事件类**；§5.4）。</summary>
    FamineResolved,
}
