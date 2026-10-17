using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;
using KFL.Rules.Economy;
using KFL.Rules.Exam;
using KFL.Rules.Settlement;

namespace KFL.Rules.Bribery;

/// <summary>
/// 行贿规则（规格书 §7.1、§7.2；契约十五 §3、§4）——**全纯函数**，不改状态、不消费随机。
/// </summary>
/// <remarks>
/// <para>
/// 窗口、应付金额、被抓概率、效果乘数 <c>kⁿ</c>、录取率（殿试为进甲率）与触顶判据都从这里取，
/// 别处 MUST NOT 复制这些公式（章程原则 VI）。数值一律经 <see cref="BribePolicy"/> 与
/// <see cref="DifficultyRates"/> 取得，本类不含任何数值字面量。
/// </para>
/// <para>
/// 窗口与乘数都是**纯派生值**，MUST NOT 持久化（契约十五 §1 条款 3、契约十七 §1 口径 5）。
/// </para>
/// </remarks>
public static class BribeRules
{
    /// <summary>
    /// 判断某日期是否落在某级别考试的**行贿窗口**内
    /// （窗口 = 该级别考试当月及其前 <see cref="BribePolicy.WindowPreviousMonths"/> 个月）。
    /// </summary>
    /// <param name="date">待判定的月份。</param>
    /// <param name="level">考试级别。殿试窗口覆盖省试月本身（两者的窗口相邻且重叠一月）。</param>
    /// <returns>在窗口内为 <see langword="true"/>。</returns>
    /// <remarks>
    /// 判据等价于「本月即该级别考试月」或「其后 <see cref="BribePolicy.WindowPreviousMonths"/> 个月内有该级别的
    /// 考试月」——各方向都取同一处 <see cref="ExamCalendar.LevelOf"/>，因此不存在第二份考期表；
    /// 窗口宽度只由 <see cref="BribePolicy.WindowPreviousMonths"/> 决定（MUST NOT 内联字面量）。
    /// </remarks>
    public static bool IsWithinWindow(GameDate date, ExamLevel level)
    {
        var probe = date;
        for (var offset = 0; offset <= BribePolicy.WindowPreviousMonths; offset++)
        {
            if (ExamCalendar.LevelOf(probe) == level)
            {
                return true;
            }

            probe = NextMonth(probe);
        }

        return false;
    }

    /// <summary>
    /// 取某日期所在（或其后最近）的该级别行贿窗口的**首月**。
    /// </summary>
    /// <param name="date">参考月份。</param>
    /// <param name="level">考试级别。</param>
    /// <returns>
    /// 窗口首月：在窗口内时为该窗口的首月（考试当月的前 <see cref="BribePolicy.WindowPreviousMonths"/> 个月），
    /// 否则为下一个窗口的首月。
    /// </returns>
    /// <exception cref="InvalidOperationException">三个科举年内没有该级别的考试月（不可能发生）。</exception>
    public static GameDate WindowStartOf(GameDate date, ExamLevel level)
    {
        var probe = date;
        for (var offset = 0; offset <= BribePolicy.WindowPreviousMonths; offset++)
        {
            if (ExamCalendar.LevelOf(probe) == level)
            {
                return SubtractMonths(probe, BribePolicy.WindowPreviousMonths);
            }

            probe = NextMonth(probe);
        }

        var cursor = date;
        for (var step = 0; step < 3; step++)
        {
            cursor = ExamCalendar.NextExamMonth(cursor);
            if (ExamCalendar.LevelOf(cursor) == level)
            {
                return SubtractMonths(cursor, BribePolicy.WindowPreviousMonths);
            }
        }

        throw new InvalidOperationException("科举周期内必出现一次该级别的考试月。");
    }

    /// <summary>该级别行贿的**序号** <c>n</c>：现计数 + 1（从 1 起算）。</summary>
    /// <param name="examinee">应考人。</param>
    /// <param name="level">考试级别。</param>
    /// <returns>本次行贿的序号。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="examinee"/> 为 <see langword="null"/>。</exception>
    public static int NthOf(Person examinee, ExamLevel level)
    {
        ArgumentNullException.ThrowIfNull(examinee);
        return CountOf(examinee, level) + 1;
    }

    /// <summary>
    /// 该级别本次行贿的**应付金额**：
    /// <c>BaseAmountGuan × (1 + 0.2 × (n − 1)) × ExpenseFactor</c>。
    /// </summary>
    /// <param name="level">考试级别。</param>
    /// <param name="nth">序号 <c>n</c>（从 1 起算）。</param>
    /// <param name="difficulty">难度（取支出系数）。</param>
    /// <returns>应付金额。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nth"/> 小于 1。</exception>
    public static Money PayableAmount(ExamLevel level, int nth, Difficulty difficulty)
    {
        if (nth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(nth), nth, "行贿序号 n 从 1 起算（nth = 现计数 + 1）。");
        }

        var guan = BribePolicy.BaseAmountGuan(level)
            * (1m + (BribePolicy.ProgressionStep * (nth - 1)))
            * DifficultyRates.ExpenseFactor(difficulty);

        return Money.FromGuan(guan);
    }

    /// <summary>
    /// 该级别本次行贿的**被抓概率**：
    /// <c>min(BaseCaughtRate × (1 + 0.2 × (n − 1)) × BriberyRiskFactor, 1.0)</c>。
    /// </summary>
    /// <param name="level">考试级别。</param>
    /// <param name="nth">序号 <c>n</c>（从 1 起算）。</param>
    /// <param name="difficulty">难度（取贿赂风险系数）。</param>
    /// <returns>被抓概率，落在 <c>[0, 1]</c>。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nth"/> 小于 1。</exception>
    public static decimal CaughtProbability(ExamLevel level, int nth, Difficulty difficulty)
    {
        if (nth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(nth), nth, "行贿序号 n 从 1 起算（nth = 现计数 + 1）。");
        }

        var probability = BribePolicy.BaseCaughtRate(level)
            * (1m + (BribePolicy.ProgressionStep * (nth - 1)))
            * DifficultyRates.BriberyRiskFactor(difficulty);

        return Math.Min(probability, BribePolicy.CaughtProbabilityCap);
    }

    /// <summary>
    /// 该级别的**效果乘数** <c>kⁿ</c>（<c>n</c> 为该级别累计行贿次数，**含被抓的那些**）。
    /// </summary>
    /// <param name="level">考试级别。</param>
    /// <param name="examinee">应考人。</param>
    /// <returns>乘数；<c>n = 0</c> 时为恒等元 <see cref="BribePolicy.UnitMultiplier"/>（<c>k⁰</c>）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="examinee"/> 为 <see langword="null"/>。</exception>
    /// <remarks>乘数**无上限**；触顶判据取最终概率是否达到 <see cref="ExamPolicy.DiceRateCap"/>。</remarks>
    public static decimal MultiplierOf(ExamLevel level, Person examinee)
    {
        ArgumentNullException.ThrowIfNull(examinee);

        var factor = BribePolicy.EffectMultiplierK(level);
        var count = CountOf(examinee, level);
        var multiplier = BribePolicy.UnitMultiplier;

        for (var index = 0; index < count; index++)
        {
            multiplier *= factor;
        }

        return multiplier;
    }

    /// <summary>
    /// 把本级别 <c>kⁿ</c> 乘数**作为形参**交给 005 的录取率 / 进甲率入口，取最终概率。
    /// </summary>
    /// <param name="state">游戏状态（取家族士籍与难度无关项）。</param>
    /// <param name="examinee">应考人。</param>
    /// <param name="level">考试级别；殿试取进甲率。</param>
    /// <returns>最终录取率（殿试为进甲率）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 或 <paramref name="examinee"/> 为 <see langword="null"/>。</exception>
    /// <remarks>
    /// 本方法**只转调** <see cref="ExamAdmission"/>，MUST NOT 复制录取率公式（契约十五 §5 条款 3）。
    /// </remarks>
    public static decimal AdmissionRateWith(GameState state, Person examinee, ExamLevel level)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(examinee);

        var multiplier = MultiplierOf(level, examinee);

        return level == ExamLevel.DianShi
            ? ExamAdmission.ImperialPromotionRate(examinee, multiplier)
            : ExamAdmission.AdmissionRate(level, examinee, state.Family.HasShiStatus, multiplier);
    }

    /// <summary>
    /// 该级别的最终录取率 / 进甲率是否已**触顶**（达到 <see cref="ExamPolicy.DiceRateCap"/>）。
    /// </summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="examinee">应考人。</param>
    /// <param name="level">考试级别。</param>
    /// <returns>已触顶为 <see langword="true"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 或 <paramref name="examinee"/> 为 <see langword="null"/>。</exception>
    public static bool IsLevelCapReached(GameState state, Person examinee, ExamLevel level) =>
        AdmissionRateWith(state, examinee, level) >= ExamPolicy.DiceRateCap;

    /// <summary>
    /// 判断此刻能否行贿，并输出可行性快照（**纯查询**：不改状态、不消费随机）。
    /// </summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="examinee">应考人。</param>
    /// <param name="level">考试级别。</param>
    /// <param name="feasibility">可行性快照（金额 / 概率 / 是否触顶 / 拒绝原因）。</param>
    /// <returns>可行时为 <see langword="true"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 或 <paramref name="examinee"/> 为 <see langword="null"/>。</exception>
    /// <remarks>
    /// 拒绝原因按「窗口 → 在册 → 级别与功名匹配 → 触顶 → 可付性」的固定次序判定（契约十五 §2 步骤①），
    /// 因此同时违反多项时返回**最先命中的那一条**。可付性一项来自规格书 §7.1 的
    /// `§17 裁决回写（2026-10-17）`：行贿是玩家主动的可选支出，MUST 全额可付、MUST NOT 转贷款。
    /// </remarks>
    public static bool CanBribe(GameState state, Person examinee, ExamLevel level, out BribeFeasibility feasibility)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(examinee);

        var nth = NthOf(examinee, level);
        var amount = PayableAmount(level, nth, state.Difficulty);
        var probability = CaughtProbability(level, nth, state.Difficulty);
        var capReached = IsLevelCapReached(state, examinee, level);
        var affordable = PaymentPrimitive.CanCover(state.Economy, amount);

        var reason = BribeRejectReason.None;
        if (!IsWithinWindow(state.CurrentDate, level))
        {
            reason = BribeRejectReason.OutsideWindow;
        }
        else if (!CountedMembers.IsRegistered(examinee))
        {
            reason = BribeRejectReason.NotRegistered;
        }
        else if (ExamRegistration.TargetLevelOf(examinee) != level)
        {
            reason = BribeRejectReason.LevelMismatch;
        }
        else if (capReached)
        {
            reason = BribeRejectReason.CapReached;
        }
        else if (!affordable)
        {
            reason = BribeRejectReason.InsufficientFunds;
        }

        feasibility = new BribeFeasibility
        {
            Amount = amount,
            Probability = probability,
            IsCapReached = capReached,
            IsAffordable = affordable,
            RejectReason = reason,
        };

        return reason == BribeRejectReason.None;
    }

    private static int CountOf(Person examinee, ExamLevel level) => level switch
    {
        ExamLevel.JieShi => examinee.JieShiBribeCount,
        ExamLevel.ShengShi => examinee.ShengShiBribeCount,
        ExamLevel.DianShi => examinee.DianShiBribeCount,
        _ => throw new ArgumentOutOfRangeException(
            nameof(level), level, "未登记的考试级别（规格书 §6 共三级）。"),
    };

    private static GameDate NextMonth(GameDate date) =>
        date.Month == 12 ? new GameDate(date.Year + 1, 1) : new GameDate(date.Year, date.Month + 1);

    private static GameDate PreviousMonth(GameDate date) =>
        date.Month == 1 ? new GameDate(date.Year - 1, 12) : new GameDate(date.Year, date.Month - 1);

    private static GameDate SubtractMonths(GameDate date, int months)
    {
        var result = date;
        for (var step = 0; step < months; step++)
        {
            result = PreviousMonth(result);
        }

        return result;
    }
}
