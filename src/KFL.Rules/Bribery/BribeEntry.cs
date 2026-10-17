using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Economy;
using KFL.Rules.Punishment;

namespace KFL.Rules.Bribery;

/// <summary>
/// 行贿行动（规格书 §7；契约十五 §2）——玩家侧的**唯一**行贿入口。
/// </summary>
/// <remarks>
/// <para>
/// 固定次序（MUST NOT 调换）：① 校验（窗口 ∧ 在册 ∧ 级别匹配 ∧ 未触顶 ∧ **全额可付**）→ ② 自算
/// <c>n</c> / 金额 / 概率 → ③ 支付（<see cref="LedgerCategory.BribeExpense"/>，恰 1 条恒为负的资金类条目）
/// → ④ 消费随机**恰 1 次** 判定被抓 → ⑤ <c>RecordBribe</c>（**无论是否被抓都执行**，Q1 = B）
/// → ⑥ 落 1 行 <see cref="BribeFact"/> → ⑦ 被抓时交 <c>PunishmentImposer</c> 裁决。
/// </para>
/// <para>
/// 拒绝路径（①未通过）MUST NOT 改动任何字段、MUST NOT 消费随机、MUST NOT 落条目（不变量 I-3）。
/// 被抓 MUST NOT 退贿款、MUST NOT 落任何补偿条目（FR-009）。无次数上限、无拒收机制。
/// **行贿 MUST NOT 转贷款**：它是玩家主动的可选支出，应付金额须由现银 + 储蓄全额覆盖，付不起即拒绝
/// （规格书 §7.1 的 `§17 裁决回写（2026-10-17）`）。故 ③ 的池内付出恒等于应付额、`ToLoan` 恒为 0。
/// </para>
/// <para>
/// 本类是本特性**唯一**新增的随机消费点，且发生在结算之外（契约十六 §1.3 的整月随机次序不变）。
/// </para>
/// </remarks>
public static class BribeEntry
{
    /// <summary>
    /// 执行一次行贿。
    /// </summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="examinee">行贿的应考人。</param>
    /// <param name="level">行贿对应的考试级别。</param>
    /// <param name="random">随机来源（判定被抓恰消费 1 次）。</param>
    /// <returns>行贿结果；被拒绝时 <see cref="BribeOutcome.Accepted"/> 为 <see langword="false"/>。</returns>
    /// <exception cref="ArgumentNullException">任一引用参数为 <see langword="null"/>。</exception>
    public static BribeOutcome Bribe(GameState state, Person examinee, ExamLevel level, IRandomService random)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(examinee);
        ArgumentNullException.ThrowIfNull(random);

        var month = state.CurrentDate;

        // ① 校验：窗口 ∧ 在册 ∧ 级别与当前功名匹配 ∧ 未触顶。拒绝 ⇒ 逐字段不变、随机 0。
        if (!BribeRules.CanBribe(state, examinee, level, out var feasibility))
        {
            return new BribeOutcome
            {
                Accepted = false,
                RejectReason = feasibility.RejectReason,
                Amount = feasibility.Amount,
                FromCash = Money.Zero,
                FromSavings = Money.Zero,
                Caught = false,
                Nth = BribeRules.NthOf(examinee, level),
                MultiplierAfter = BribeRules.MultiplierOf(level, examinee),
                CapReached = feasibility.IsCapReached,
                Punishment = null,
            };
        }

        // ② 自算：n 由 CanBribe 内部同源得出，此处只取结果值，避免第二份公式。
        var nth = BribeRules.NthOf(examinee, level);
        var amount = feasibility.Amount;
        var probability = feasibility.Probability;

        // ③ 支付：恰 1 条 BribeExpense（现金 → 储蓄；① 已保证全额可付，故不走第三步转贷款）。
        var payment = PaymentPrimitive.Pay(state.Economy, LedgerCategory.BribeExpense, month, amount, examinee.Id);

        if (payment.ToLoan != Money.Zero)
        {
            throw new InvalidOperationException(
                "行贿 MUST NOT 转贷款（规格书 §7.1 的 §17 裁决回写（2026-10-17））。");
        }

        // ④ 判定被抓：恰消费 1 次随机。
        var caught = random.NextDouble() < (double)probability;

        // ⑤ 计数递增：无论是否被抓都执行（Q1 = B）。
        examinee.RecordBribe(level);

        // ⑥ 落 1 行事实（含被抓当次）。
        var capReached = BribeRules.IsLevelCapReached(state, examinee, level);
        state.Family.PunishmentJournal.Append(
            new BribeFact(month, examinee.Id, level, nth, amount, caught, capReached));

        // ⑦ 被抓时的裁决：整条链路交给 PunishmentImposer（本类 MUST NOT 按调用方分支）。
        var punishment = caught
            ? PunishmentImposer.Apply(state, examinee, level, random)
            : null;

        return new BribeOutcome
        {
            Accepted = true,
            RejectReason = BribeRejectReason.None,
            Amount = amount,
            FromCash = payment.FromCash,
            FromSavings = payment.FromSavings,
            Caught = caught,
            Nth = nth,
            MultiplierAfter = BribeRules.MultiplierOf(level, examinee),
            CapReached = capReached,
            Punishment = punishment,
        };
    }

    /// <summary>
    /// 清零行贿计数（FR-031 调试入口；<paramref name="level"/> 为 <see langword="null"/> 表示三级全清）。
    /// </summary>
    /// <param name="examinee">当事人。</param>
    /// <param name="level">要清零的级别；<see langword="null"/> = 三级全清。</param>
    /// <exception cref="ArgumentNullException"><paramref name="examinee"/> 为 <see langword="null"/>。</exception>
    public static void ResetBribeCounts(Person examinee, ExamLevel? level)
    {
        ArgumentNullException.ThrowIfNull(examinee);
        examinee.ResetBribeCounts(level);
    }
}
