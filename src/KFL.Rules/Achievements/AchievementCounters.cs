using KFL.Core.Entities;
using KFL.Core.ValueObjects;
using KFL.Rules.Economy;
using KFL.Rules.Settlement;

namespace KFL.Rules.Achievements;

/// <summary>
/// ①~⑥ 累计量的**同月同步**记账（<c>data-model.md</c> §3.1 第 1 步；FR-013）。
/// </summary>
/// <remarks>
/// <para>
/// <b>只做「事实 → 累计量」</b>：本类 MUST NOT 判定任何成就、MUST NOT 建视图、MUST NOT 落盘。
/// 「哪些事实可累计」与「判定谁成立」是两层职责（契约二十二 §2 条款 7）。
/// </para>
/// <para>
/// <b>为什么必须同月同步</b>：出生 / 连坐 / 释放等瞬时事实只在当月的
/// <see cref="SettlementResult"/> 快照里存在，月末之后无法从
/// <see cref="GameState"/> 复原（<c>data-model.md</c> §2.4 的「不可重算」证明表）。
/// 事后回填或「判定之外更新」都会产生第二真源。
/// </para>
/// <para>
/// <b>纯读 + 写累计量</b>：不消费随机、不读时钟 / 文件 / 网络（G-07 扫描 <c>src\KFL.Rules\</c>）。
/// </para>
/// </remarks>
public static class AchievementCounters
{
    /// <summary>把本月快照里的瞬时事实累计进 ①~⑤（⑥ 只由 <see cref="ReportsDebugWake"/> 推进）。</summary>
    /// <param name="state">游戏状态（累计量的宿主）。</param>
    /// <param name="settlement">当月结算报告（事实的唯一来源）。</param>
    /// <exception cref="ArgumentNullException">任一参数为 <c>null</c>。</exception>
    /// <remarks>
    /// <b>行贿口径</b>：① 计的是**成功**行贿（<c>!Caught</c>）条数，而 006 的
    /// <c>PunishmentQueries.FamilyCumulativeBribes</c> 计的是**尝试**次数——两者语义不同，
    /// MUST NOT 复用（<c>tasks.md</c> T048）。
    /// </remarks>
    public static void AbsorbMonthFacts(GameState state, SettlementResult settlement)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(settlement);

        // ① 成功行贿（未被当场拿获）次数。
        var successfulBribes = 0;
        foreach (var bribe in settlement.Punishment.Bribes)
        {
            if (!bribe.Caught)
            {
                successfulBribes++;
            }
        }

        state.Achievements.RecordSuccessfulBribes(successfulBribes);

        // ② 出生人数（含夭折）。
        state.Achievements.RecordBirths(settlement.Lifecycle.Births.Count);

        // ③ 已故成员的寿命序列：按**事实月份**计算死亡年龄（与 ACH-037 同口径）。
        foreach (var death in settlement.Lifecycle.Deaths)
        {
            var member = Find(state.Family.Members, death.PersonId);
            if (member is not null)
            {
                state.Achievements.AppendLifespan(member.AgeAt(settlement.Month));
            }
        }

        // ④ 总资产历史峰值（算式唯一出处 = TotalAssetsCalculator.Of）。
        state.Achievements.ObserveTotalAssets(TotalAssetsCalculator.Of(state.Economy));

        // ⑤ 「产生过贷款」：未清偿即视为曾发生；清偿后由已置真的标志维持。
        if (!state.Economy.Treasury.Loan.IsSettled)
        {
            state.Achievements.RecordLoanOriginated();
        }
    }

    /// <summary>推进 ⑥ 调试唤醒累计次数，并在**同一次调用里**触发 <c>ACH-064</c>~<c>ACH-068</c> 的即时判定。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="count">本次新增次数，MUST `>= 1`。</param>
    /// <returns>累加后的累计次数。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 <c>null</c>。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> 小于 1。</exception>
    /// <remarks>
    /// <b>唯一调用点</b>：契约二十二 §4「条款 13」要求「⑥ 的累加」与「即时判定」放在同一次调用里，
    /// 使 U4 调试控制台只需一个入口。故 U4 MUST 调本方法而**不是**直接调
    /// <see cref="AchievementState.ReportDebugWake"/> + <see cref="AchievementAdjudicator.Report"/>。
    /// <para>
    /// <b>副作用边界</b>：MUST NOT 消费随机、MUST NOT 落账本条目、MUST NOT 改家族经济与成员状态
    /// （<c>spec.md</c> US5 AS3）；`count` 只累加，不清零、不因超过 10000 而截断（更高档位无成就）。
    /// </para>
    /// </remarks>
    public static int ReportsDebugWake(GameState state, int count)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "调试唤醒次数 MUST `>= 1`（契约二十二 条款 13）。");
        }

        var total = state.Achievements.ReportDebugWake(count);
        AchievementAdjudicator.Report(state, AchievementTrigger.DebugWakeReported);
        return total;
    }

    private static Person? Find(IReadOnlyCollection<Person> members, PersonId id)
    {
        foreach (var member in members)
        {
            if (member.Id == id)
            {
                return member;
            }
        }

        return null;
    }
}
