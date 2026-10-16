using KFL.Core.ValueObjects;
using KFL.Rules.Punishment;

namespace KFL.Rules.Bribery;

/// <summary>
/// 行贿被拒绝的原因（契约十五 §2 步骤①；FR-029）。
/// </summary>
/// <remarks>
/// 拒绝路径 MUST NOT 改动任何字段、MUST NOT 消费随机（不变量 I-3）。
/// </remarks>
public enum BribeRejectReason
{
    /// <summary>未被拒绝。</summary>
    None = 0,

    /// <summary>不在行贿窗口内（窗口 = 该级别考试当月及其前一月）。</summary>
    OutsideWindow,

    /// <summary>当事人不在家族册上（已亡 / 已出嫁外姓）。</summary>
    NotRegistered,

    /// <summary>级别与当事人当前功名不匹配（沿用 005 的合格目标级别）。</summary>
    LevelMismatch,

    /// <summary>该级别的最终录取率 / 进甲率已触顶（再行贿不再提高）。</summary>
    CapReached,
}

/// <summary>
/// 行贿可行性快照（纯查询结果）：当前应付金额、当前被抓概率、是否已触顶、拒绝原因。
/// </summary>
/// <remarks>
/// 由 <see cref="BribeRules.CanBribe"/> 产出；**只读派生值**，MUST NOT 持久化。
/// </remarks>
public sealed record BribeFeasibility
{
    /// <summary>当前应付行贿金额（含累进因子与难度支出系数）。</summary>
    public required Money Amount { get; init; }

    /// <summary>当前被抓概率（含累进因子与难度贿赂风险系数，已按 <c>1.0</c> 封顶）。</summary>
    public required decimal Probability { get; init; }

    /// <summary>该级别的最终录取率 / 进甲率是否已触顶（入口应置灰）。</summary>
    public required bool IsCapReached { get; init; }

    /// <summary>拒绝原因；<see cref="BribeRejectReason.None"/> 表示可行。</summary>
    public required BribeRejectReason RejectReason { get; init; }

    /// <summary>是否可行（<see cref="RejectReason"/> 为 <see cref="BribeRejectReason.None"/>）。</summary>
    public bool IsFeasible => RejectReason == BribeRejectReason.None;
}

/// <summary>
/// 一次行贿行动的结果（契约十五 §2；<see cref="BribeEntry.Bribe"/> 的返回值）。
/// </summary>
/// <remarks>
/// 被拒绝时 <see cref="Accepted"/> 为 <see langword="false"/>，其余字段表示**若可行会是怎样**的纯计算值，
/// 但当刻状态逐字段未变、随机消费为 0。
/// </remarks>
public sealed record BribeOutcome
{
    /// <summary>是否被受理。</summary>
    public required bool Accepted { get; init; }

    /// <summary>拒绝原因；受理时为 <see cref="BribeRejectReason.None"/>。</summary>
    public required BribeRejectReason RejectReason { get; init; }

    /// <summary>应付行贿金额。</summary>
    public required Money Amount { get; init; }

    /// <summary>现金支付段。</summary>
    public required Money FromCash { get; init; }

    /// <summary>储蓄支付段。</summary>
    public required Money FromSavings { get; init; }

    /// <summary>不足以支付而转入贷款的余额段。</summary>
    public required Money ToLoan { get; init; }

    /// <summary>本次行贿是否被抓。</summary>
    public required bool Caught { get; init; }

    /// <summary>这是该级别的第几次行贿（<c>n</c>，从 1 起算）。</summary>
    public required int Nth { get; init; }

    /// <summary>本次行贿**之后**该级别的效果乘数（<c>kⁿ</c>，<c>n</c> 含被抓的那些）。</summary>
    public required decimal MultiplierAfter { get; init; }

    /// <summary>行动之后该级别是否已触顶。</summary>
    public required bool CapReached { get; init; }

    /// <summary>被抓时由惩罚裁决产生的当月惩罚快照；未被抓时为 <see langword="null"/>。</summary>
    public PunishmentSnapshot? Punishment { get; init; }
}
