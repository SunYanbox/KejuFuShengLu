using KFL.Core.Enums;

namespace KFL.Core.ValueObjects;

/// <summary>
/// 一次**行贿**（含被抓当次）的当月事实行（FR-028、FR-030；data-model §2.2）。
/// </summary>
/// <remarks>
/// <para>
/// `Nth` 是本次行贿后的**累计次数**（含被抓的那些，Q1 = B），故 <c>Nth == 1</c> 表示首次行贿。
/// </para>
/// <para>
/// `Amount` 是本次实付金额（已乘难度收益系数）；`Caught` 是本次是否被抓（**不影响计数与乘数**，
/// 规格书 §7.1）；`CapReached` 是本次调用**之后**该级别是否已触顶（UF-10 的入口置灰判据）。
/// </para>
/// <para>
/// **明确不含**：退款 / 补偿——被抓既不退贿款也不落补偿条目（FR-009）。
/// </para>
/// </remarks>
/// <param name="Date">事实发生的年月。</param>
/// <param name="Examinee">当事人（行贿者）。</param>
/// <param name="Level">行贿对应 / 涉及的考试级别。</param>
/// <param name="Nth">本次之后该考生 × 该级别的**累计行贿次数**（<c>&gt;= 1</c>）。</param>
/// <param name="Amount">本次实付金额（<c>&gt; 0</c>）。</param>
/// <param name="Caught">本次是否被抓。</param>
/// <param name="CapReached">本次之后该级别是否已达录取率上限。</param>
public readonly record struct BribeFact(
    GameDate Date,
    PersonId Examinee,
    ExamLevel Level,
    int Nth,
    Money Amount,
    bool Caught,
    bool CapReached);

/// <summary>
/// 一次**服刑建立**的当月事实行（FR-028、FR-030；data-model §2.2）。
/// </summary>
/// <remarks>只记「建立」；**释放**由 <see cref="ReleaseFact"/> 承载（契约十六 §7）。</remarks>
/// <param name="Date">事实发生的年月。</param>
/// <param name="Subject">被判服刑的当事人。</param>
/// <param name="Months">本次服刑月数（年数 × 12，<c>&gt; 0</c>）。</param>
public readonly record struct SentenceFact(
    GameDate Date,
    PersonId Subject,
    int Months);

/// <summary>
/// 一次**罚金**的当月事实行（FR-012、FR-028；data-model §2.2）。
/// </summary>
/// <remarks>
/// <para>
/// **支付拆分落成两个成员而非 002 的 `PaymentResult` 三段**：
/// 后者定义在 `KFL.Rules`，而本类型在 `KFL.Core`，引用它会违反 G-05 的依赖方向
/// （data-model §1「`KFL.Core` 不认识 `KFL.Rules`」）。三段拆分的**可断言部分**只有
/// 「从资金池扣除多少」与「有多少转成了贷款本金」，故投影成
/// <see cref="PaidFromTreasury"/>（= 现金 + 储蓄两段之和）与 <see cref="ToLoan"/>。
/// </para>
/// <para>
/// 恒有 <c>PaidFromTreasury + ToLoan == Amount</c>，且 <c>ToLoan &gt; 0</c> ⟺ 当事人付不起。
/// </para>
/// </remarks>
/// <param name="Date">事实发生的年月。</param>
/// <param name="Subject">被罚款的当事人。</param>
/// <param name="Amount">罚金金额（<c>&gt; 0</c>；为 0 时不落事实行）。</param>
/// <param name="PaidFromTreasury">由现金 + 储蓄实际扣除的部分。</param>
/// <param name="ToLoan">付不起而转入 <c>Loan.Principal</c> 的部分。</param>
public readonly record struct FineFact(
    GameDate Date,
    PersonId Subject,
    Money Amount,
    Money PaidFromTreasury,
    Money ToLoan);

/// <summary>
/// 一次**亲属禁考 / 禁升**的当月事实行（FR-015、FR-028；data-model §2.2）。
/// </summary>
/// <remarks>
/// 来源有两类：① 12 格矩阵中「相关亲属禁考 N 月」（困难 / 地狱的解试与省试，
/// 此时 <see cref="PromotionBanMonths"/> 为 <c>null</c>）；② 地狱殿试的
/// 「全族禁考禁升 48 月」逐人落地（此时两者皆为 48）。
/// </remarks>
/// <param name="Date">事实发生的年月。</param>
/// <param name="Subject">被施加禁考 / 禁升的亲属本人。</param>
/// <param name="Relative">触发本次连带的当事人。</param>
/// <param name="ExamBanMonths">本次写入的禁考月数（<c>&gt; 0</c>）。</param>
/// <param name="PromotionBanMonths">本次写入的禁升月数；<c>null</c> = **只**禁考。</param>
public readonly record struct RelativeBanFact(
    GameDate Date,
    PersonId Subject,
    PersonId Relative,
    int ExamBanMonths,
    int? PromotionBanMonths);

/// <summary>
/// 一次**连坐后果**的当月事实行（FR-022~FR-026、FR-028；data-model §2.2）。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Outcome"/> 的三个分支**互斥**（<see cref="CollectiveOutcome"/>）；
/// 「免死例外」的孩子**不产生**本类型的事实行（契约十六 §6.3）。
/// </para>
/// <para>
/// <see cref="DegreeBefore"/> / <see cref="DegreeAfter"/> 只在
/// <see cref="CollectiveOutcome.DegreeDemoted"/> 时非空；处死与全族禁考禁升两分支皆为 <c>null</c>。
/// </para>
/// </remarks>
/// <param name="Date">事实发生的年月。</param>
/// <param name="Subject">被连坐的亲属本人（可能是已归档的外嫁女儿）。</param>
/// <param name="Outcome">后果分支。</param>
/// <param name="Relative">触发本次连坐的当事人。</param>
/// <param name="DegreeBefore">降级前的功名；仅降级分支非空。</param>
/// <param name="DegreeAfter">降级后的功名；仅降级分支非空。</param>
public readonly record struct CollectivePunishmentFact(
    GameDate Date,
    PersonId Subject,
    CollectiveOutcome Outcome,
    PersonId Relative,
    DegreeLevel? DegreeBefore,
    DegreeLevel? DegreeAfter);

/// <summary>
/// 一次**服刑期满释放**的当月事实行（FR-020、FR-030；data-model §2.2）。
/// </summary>
/// <remarks>
/// <see cref="NewRankLevel"/> 只在「释放时 <c>Rank</c> 非 null」时非空，等于降级后的品级
/// （<c>Rank.Level + 1</c>；已是 L18 则维持原级，契约十六 §7）。功名**保留**、不计入本行。
/// </remarks>
/// <param name="Date">事实发生的年月。</param>
/// <param name="Subject">被释放的成员。</param>
/// <param name="NewRankLevel">释放降级后的官阶级数；<c>null</c> = 释放时无官职（无降级）。</param>
public readonly record struct ReleaseFact(
    GameDate Date,
    PersonId Subject,
    int? NewRankLevel);
