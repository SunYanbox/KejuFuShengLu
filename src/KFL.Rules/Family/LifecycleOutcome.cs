using KFL.Core.ValueObjects;

namespace KFL.Rules.FamilyLifecycle;

/// <summary>婚配方向（data-model §3.3 只引用、未定义，007 就地落定）。</summary>
/// <remarks>
/// <para><see cref="BrideInto"/> = 娶入（对方进门，我方成员为夫）；<see cref="GroomOut"/> = 嫁出（我方成员出门，对方为夫）。</para>
/// <para>
/// 方向与成员性别一一对应：男成员只能 <see cref="BrideInto"/>、女成员只能 <see cref="GroomOut"/>；
/// 传入不一致的方向即 <see cref="LifecycleRejectReason.NotMarriageable"/>。婚嫁金额表（规格书 §9.2）
/// 两行正是按方向分列，故方向不做成字段、不落存档。
/// </para>
/// </remarks>
public enum MarriageDirection
{
    /// <summary>娶入：我方支出对方阶层聘礼、收入对方阶层嫁妆。</summary>
    BrideInto = 0,

    /// <summary>嫁出：我方支出本家阶层嫁妆、收入对方阶层聘礼。</summary>
    GroomOut = 1,
}

/// <summary>生命周期入口的拒绝原因（data-model §3.4）。</summary>
/// <remarks>
/// <para>
/// <see cref="None"/> 有两种含义：① 调用成功；② **托媒的成功判定失败**——契约十九 §3 的 ⑤ 只在
/// 「判定失败、不退费」时返回，此时没有任何「拒绝原因」可表达（费用已扣、状态无变化），
/// 故以 <c>Accepted = false</c> + <see cref="None"/> 表达，见 <see cref="LifecycleOutcome.Amount"/>。
/// </para>
/// </remarks>
public enum LifecycleRejectReason
{
    /// <summary>无拒绝：已成功，或托媒的成功判定失败（费用已扣、不退）。</summary>
    None = 0,

    /// <summary>资格不符：非在册未婚成年成员、方向与性别不一致、或当月已有未应答提议。</summary>
    NotMarriageable = 1,

    /// <summary>现银 + 储蓄不足（MUST NOT 转贷款；MUST NOT 记 0 金额条目）。</summary>
    InsufficientFunds = 2,

    /// <summary>买功名婚姻仅限**娶入**方向（嫁出侧配偶不入本家籍，为其买功名只利他人）。</summary>
    DegreeMarriageBrideIntoOnly = 3,

    /// <summary>女方不在生育窗口（16~45 岁）。</summary>
    FertilityWindowClosed = 4,

    /// <summary>本年度已为这对夫妇申请过分娩（每自然年至多一次）。</summary>
    AlreadyAppliedThisYear = 5,

    /// <summary>HeadId 为 null（收养 MUST 有家主）。</summary>
    HeadVacant = 6,

    /// <summary>当月无未失效的提亲 / 捉婿提议。</summary>
    NoPendingProposal = 7,

    /// <summary>调用者不是家主。</summary>
    NotHead = 8,

    /// <summary>家族内无在册男性成员。</summary>
    NoRegisteredMale = 9,

    /// <summary>该成员本月已托过一次媒（FR-013：同一成员同一月至多一次，下月可再试）。</summary>
    AlreadyAppliedThisMonth = 10,

    /// <summary>族内通婚要求双方**同代**（玩家反馈 1：不考虑遗传病，但只允许同代通婚）。</summary>
    NotSameGeneration = 11,

    /// <summary>该成员当前没有配偶，无需离异（玩家反馈 1）。</summary>
    NotSpouse = 12,
}

/// <summary>生命周期入口的统一返回（data-model §3.4）。</summary>
/// <remarks>
/// <para>
/// <see cref="Amount"/> / <see cref="FromCash"/> / <see cref="FromSavings"/> / <see cref="Entry"/>
/// 描述**已经实际扣付的前置费用**（托媒费，以及买功名婚姻的功名费）；纯拒绝（资格 / 可付性 / 灰置）
/// 时四者分别为 0 / 0 / 0 / <c>null</c>。
/// </para>
/// <para><see cref="Proposal"/> 只在入口真的产出或消费了一条提议时非空。</para>
/// </remarks>
public sealed record LifecycleOutcome
{
    /// <summary>是否已接受（托媒成功、生育申请受理、收养成交、应答接受）。</summary>
    public required bool Accepted { get; init; }

    /// <summary>拒绝原因；<see cref="LifecycleRejectReason.None"/> 表示无拒绝。</summary>
    public required LifecycleRejectReason RejectReason { get; init; }

    /// <summary>已实际支付的前置费用合计（贯）。</summary>
    public required Money Amount { get; init; }

    /// <summary>其中来自现银的部分。</summary>
    public required Money FromCash { get; init; }

    /// <summary>其中来自储蓄的部分。</summary>
    public required Money FromSavings { get; init; }

    /// <summary>前置费用对应的账本条目（无前置费用时为 <c>null</c>）。</summary>
    public required LedgerEntry? Entry { get; init; }

    /// <summary>本次入口产出或消费的提议（无则为 <c>null</c>）。</summary>
    public MarriageProposal? Proposal { get; init; }

    /// <summary>构造一条「纯拒绝」：无扣费、无条目、无提议。</summary>
    /// <param name="reason">拒绝原因。</param>
    /// <returns>四项金额为 0、<see cref="Entry"/> 为 <c>null</c> 的结果。</returns>
    public static LifecycleOutcome Rejected(LifecycleRejectReason reason) => new()
    {
        Accepted = false,
        RejectReason = reason,
        Amount = Money.Zero,
        FromCash = Money.Zero,
        FromSavings = Money.Zero,
        Entry = null,
    };
}
