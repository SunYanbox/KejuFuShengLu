namespace KFL.Core.Enums;

/// <summary>
/// 家族流水账的类别全集（38 个取值；data-model §2.1、§2.4，逐行取自规格书 §12.3 与 §5.1~§5.4、
/// 006 的 §7.1 / §7.5、007 的 §4.3 / §9.1~§9.5）。
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
/// ② 005 新增 <see cref="ExamTravelCost"/>，006 新增 <see cref="BribeExpense"/> 与
/// <see cref="Fine"/>，007 新增聘礼 / 嫁妆与买人口的 16 项（FR-043）——001 时期
/// 「无贿赂类别」「无买人口 / 聘礼 / 嫁妆类别」的**负向**断言自此由 006 / 007 的类别名单
/// 等价替代（不变量由正向断言守卫，MUST NOT 再保留「该类类别不存在」的断言）。
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

    /// <summary>省试路费（资金类，恒为负；规格书 §12.3 / 契约十三 §3.1）。</summary>
    ExamTravelCost,

    /// <summary>行贿支出（资金类，恒为负；规格书 §7.1 / 契约十五 §2）。</summary>
    BribeExpense,

    /// <summary>罚金（资金类，恒为负；规格书 §7.5 / 契约十六 §4）。</summary>
    Fine,

    /// <summary>托媒费（资金类，恒为负；规格书 §9.1 / 契约十九 §3）。</summary>
    MatchmakerFee,

    /// <summary>名师月费（资金类，恒为负；规格书 §4.3）。</summary>
    TutorFee,

    /// <summary>娶入时付给对方的聘礼（资金类，恒为负；规格书 §9.1 的 24 格金额表）。</summary>
    BetrothalGiftPaid,

    /// <summary>嫁出时付给对方的嫁妆（资金类，恒为负；规格书 §9.1）。</summary>
    DowryPaid,

    /// <summary>买功名婚姻的额外支出（资金类，恒为负；规格书 §9.3，年俸 × 150%）。</summary>
    DegreeMarriageFeePaid,

    /// <summary>生育申请费（资金类，恒为负；规格书 §9.4）。</summary>
    ChildbirthExpense,

    /// <summary>疗程药费（资金类，恒为负；规格书 §4.3，<c>(0.05 + 0.45s) 贯</c> × 支出系数）。</summary>
    MedicineExpense,

    /// <summary>买人口支出（资金类，恒为负；规格书 §9.5，本年第 n 名 = 10n 贯）。</summary>
    PersonPurchase,

    /// <summary>嫁出时收受对方的聘礼（资金类，正额入现金；规格书 §9.1）。</summary>
    BetrothalGiftReceived,

    /// <summary>娶入时收受对方的嫁妆（资金类，正额入现金；规格书 §9.1）。</summary>
    DowryReceived,

    /// <summary>外嫁时收受的买功名婚姻额外嫁妆（资金类，正额入现金；规格书 §9.3）。</summary>
    DegreeMarriageDowryReceived,

    /// <summary>新生儿降生（**事件类**，不引起资金池变动；规格书 §9.4）。</summary>
    ChildBorn,

    /// <summary>成员死亡（**事件类**；规格书 §4.1~§4.4，⑧-g 统一归档时落一条）。</summary>
    MemberDeceased,

    /// <summary>婚姻成立（**事件类**；规格书 §9.1 / §9.2）。</summary>
    MarriageFormed,

    /// <summary>成员外嫁归档（**事件类**；规格书 §9.1 的嫁出方向）。</summary>
    MemberMarriedOut,

    /// <summary>买人口成交（**事件类**；规格书 §9.5）。</summary>
    PersonBought,
}
