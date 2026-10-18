using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Core.Entities;

/// <summary>
/// 档内成就累计量与解锁记录（FR-013；SC-014；data-model §2.3）。
/// </summary>
/// <remarks>
/// <para>
/// **持久字段恰 8 项**，逐项等于 FR-013 的列项（SC-014 禁止第九项）。这 8 项的共同特征是
/// 「**不可重算**」：出生与行贿事实只在当月快照里、三均值非线性、历史峰值与「曾有过贷款」
/// 无法由当前状态推出、调试唤醒不产生任何落盘事实、待落盘队列是「已判定但尚未落全局」的
/// 中间态。逐项的证明表见 data-model §2.4。
/// </para>
/// <para>
/// **列表只增不改**：<see cref="Completed"/> 与 <see cref="PendingUnlocks"/> 用
/// `private readonly List&lt;T&gt;` + 只读视图，与 <see cref="PunishmentJournal"/> /
/// <see cref="FamilyLifecycleState"/> 的既有模式一致。
/// </para>
/// <para>
/// **复原通道是 `internal`**：`KFL.Core` 经 `AssemblyInfo.cs` 的
/// `[assembly: InternalsVisibleTo("KFL.Infrastructure")]` 对存档层开放，故复原走
/// <see cref="Restore"/> / <see cref="RestorePendingUnlocks"/>，**MUST NOT** 新增 `public` 面
/// （`plan.md:64-66`）。`KFL.Rules` 只能经写通道改变本类型。
/// </para>
/// </remarks>
public sealed class AchievementState
{
    private readonly List<int> _lifespans = [];
    private readonly List<AchievementMark> _completed = [];
    private readonly List<PendingUnlock> _pendingUnlocks = [];

    /// <summary>
    /// 已完成键的加速集（`(Achievement, Difficulty)`）。它**不是**第九项持久字段：
    /// 内容完全由 <see cref="_completed"/> 派生，复原时一并重建。
    /// </summary>
    private readonly HashSet<(AchievementId Achievement, Difficulty Difficulty)> _completedKeys = [];

    /// <summary>① 家族累计成功行贿次数（增量累加，含被反复判定的当月）。</summary>
    public int SuccessfulBribeCount { get; private set; }

    /// <summary>② 家族累计出生人数（**含夭折**；不含买来的人口与开局成员）。</summary>
    public int BirthCount { get; private set; }

    /// <summary>③ 已故成员的**完整**寿命序列（不是「和 + 个数」：三均值需要原始序列）。</summary>
    public IReadOnlyList<int> Lifespans => _lifespans;

    /// <summary>④ 家族历史总资产峰值（取极大值，不可重算）。初值为 <see cref="Money.Zero"/>。</summary>
    public Money PeakTotalAssets { get; private set; }

    /// <summary>⑤ 是否曾经产生过贷款（清偿后仍为 `true`；「曾经有过」不可由当前贷款推出）。</summary>
    public bool EverHadLoan { get; private set; }

    /// <summary>⑥ 调试唤醒累计次数（FR-041 ⑥ 的可被外部上报的累计接缝）。</summary>
    public int DebugWakeCount { get; private set; }

    /// <summary>⑦ 本档已完成集合；键 `(Achievement, Difficulty)` 内唯一，`CompletedAt` 保留首次。</summary>
    public IReadOnlyList<AchievementMark> Completed => _completed;

    /// <summary>⑧ 待落盘解锁队列；每个键 MUST 也在 <see cref="Completed"/> 中。</summary>
    public IReadOnlyList<PendingUnlock> PendingUnlocks => _pendingUnlocks;

    /// <summary>① 记录本月的成功行贿次数（增量累加）。</summary>
    /// <param name="count">本次新增次数，MUST `>= 0`。</param>
    /// <returns>累加后的 <see cref="SuccessfulBribeCount"/>。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> 为负。</exception>
    public int RecordSuccessfulBribes(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "成功行贿次数 MUST NOT 为负（SC-014 ①）。");
        }

        SuccessfulBribeCount += count;
        return SuccessfulBribeCount;
    }

    /// <summary>② 记录本月的出生人数（**含夭折**，增量累加）。</summary>
    /// <param name="count">本次新增人数，MUST `>= 0`。</param>
    /// <returns>累加后的 <see cref="BirthCount"/>。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> 为负。</exception>
    public int RecordBirths(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "出生人数 MUST NOT 为负（SC-014 ②）。");
        }

        BirthCount += count;
        return BirthCount;
    }

    /// <summary>③ 追加一名已故成员的寿命（逐个追加，MUST NOT 只记合计）。</summary>
    /// <param name="age">死亡年龄（周岁），MUST `>= 0`。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="age"/> 为负。</exception>
    public void AppendLifespan(int age)
    {
        if (age < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(age), age, "寿命 MUST `>= 0`（SC-014 ③）。");
        }

        _lifespans.Add(age);
    }

    /// <summary>④ 观察总资产，取历史峰值。</summary>
    /// <param name="totalAssets">本次结算的总资产。</param>
    /// <remarks>
    /// 负值按 <see cref="Money.Zero"/> 处理，与
    /// <c>TotalAssetsCalculator</c> 的 `total.IsPositive ? total : Money.Zero` 同口径——
    /// 避免 ④ 记下负数而编年史 ③ 给出负数。
    /// </remarks>
    public void ObserveTotalAssets(Money totalAssets)
    {
        var normalized = totalAssets.IsPositive ? totalAssets : Money.Zero;
        if (normalized > PeakTotalAssets)
        {
            PeakTotalAssets = normalized;
        }
    }

    /// <summary>⑤ 记录「产生了贷款」（幂等置真；清偿后不清零）。</summary>
    public void RecordLoanOriginated() => EverHadLoan = true;

    /// <summary>⑥ 上报调试唤醒次数（增量累加，返回累计值）。</summary>
    /// <param name="count">本次新增次数，MUST `>= 0`。</param>
    /// <returns>累加后的 <see cref="DebugWakeCount"/>。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> 为负。</exception>
    public int ReportDebugWake(int count = 1)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "调试唤醒次数 MUST NOT 为负（SC-014 ⑥）。");
        }

        DebugWakeCount += count;
        return DebugWakeCount;
    }

    /// <summary>⑦ 标记成就已完成（幂等）。</summary>
    /// <param name="mark">完成记录。</param>
    /// <returns>本次是新解锁 ⇒ `true`；键已存在 ⇒ `false` 且**不改动任何状态**。</returns>
    /// <remarks>
    /// `CompletedAt` **保留首次**的月份（档内语义）；跨档的最早日期由全局文件的合并规则负责（FR-018）。
    /// </remarks>
    public bool MarkCompleted(AchievementMark mark)
    {
        if (!_completedKeys.Add((mark.Achievement, mark.Difficulty)))
        {
            return false;
        }

        _completed.Add(mark);
        return true;
    }

    /// <summary>⑦ + ⑧ 的**原子组合**，是新解锁的唯一入口。</summary>
    /// <param name="mark">完成记录。</param>
    /// <param name="sourceSaveId">来源存档标识（写入全局记录用）。</param>
    /// <returns>本次是新解锁 ⇒ `true`；键已存在 ⇒ `false` 且**不写队列**。</returns>
    /// <remarks>
    /// 这是**唯一**同时写 <see cref="Completed"/> 与 <see cref="PendingUnlocks"/> 的入口：
    /// 「待落盘 ⊆ 已完成」（data-model §2.3 设计要点 3）因此在类型层就成立 ——
    /// 无法只写队列不写已完成。
    /// </remarks>
    public bool MarkCompletedAndEnqueue(AchievementMark mark, Guid sourceSaveId)
    {
        if (!MarkCompleted(mark))
        {
            return false;
        }

        _pendingUnlocks.Add(new PendingUnlock(mark.Achievement, mark.Difficulty, mark.CompletedAt, sourceSaveId));
        return true;
    }

    /// <summary>存档层复原通道（`internal`；只在 <c>KFL.Infrastructure</c> 内可见）。</summary>
    /// <param name="successfulBribeCount">① 累计成功行贿次数。</param>
    /// <param name="birthCount">② 累计出生人数。</param>
    /// <param name="lifespans">③ 完整寿命序列（**逐元素、保持次序**）。</param>
    /// <param name="peakTotalAssets">④ 历史总资产峰值。</param>
    /// <param name="everHadLoan">⑤ 是否曾有贷款。</param>
    /// <param name="debugWakeCount">⑥ 调试唤醒累计次数。</param>
    /// <param name="completed">⑦ 已完成集合（**逐元素、保持次序**）。</param>
    /// <param name="pendingUnlocks">⑧ 待落盘队列（**逐元素、保持次序**）。</param>
    /// <exception cref="ArgumentNullException">任一列表为 <c>null</c>。</exception>
    internal void Restore(
        int successfulBribeCount,
        int birthCount,
        IReadOnlyList<int> lifespans,
        Money peakTotalAssets,
        bool everHadLoan,
        int debugWakeCount,
        IReadOnlyList<AchievementMark> completed,
        IReadOnlyList<PendingUnlock> pendingUnlocks)
    {
        ArgumentNullException.ThrowIfNull(lifespans);
        ArgumentNullException.ThrowIfNull(completed);
        ArgumentNullException.ThrowIfNull(pendingUnlocks);

        SuccessfulBribeCount = successfulBribeCount;
        BirthCount = birthCount;
        PeakTotalAssets = peakTotalAssets;
        EverHadLoan = everHadLoan;
        DebugWakeCount = debugWakeCount;

        _lifespans.Clear();
        _lifespans.AddRange(lifespans);

        _completed.Clear();
        _completedKeys.Clear();
        foreach (var mark in completed)
        {
            _completed.Add(mark);
            _completedKeys.Add((mark.Achievement, mark.Difficulty));
        }

        _pendingUnlocks.Clear();
        _pendingUnlocks.AddRange(pendingUnlocks);
    }

    /// <summary>⑧ 的单独写通道：全局文件落盘成功后清空队列（或恢复为给定队列）。</summary>
    /// <param name="pendingUnlocks">新的待落盘队列（常规用法是空集合）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="pendingUnlocks"/> 为 <c>null</c>。</exception>
    /// <remarks>
    /// <para>
    /// **为什么是 <c>public</c>（勘误 E-19）**：本通道的**唯一**常规调用方是
    /// <c>KFL.Rules.Save.TurnRunner.Advance</c> 的 ⑤ 全局落盘成功分支
    /// （<c>data-model.md</c> §3.6、契约二十三 §5、`tasks.md` T043），而
    /// <c>src/KFL.Core/AssemblyInfo.cs:13</c> 只对 <c>KFL.Infrastructure</c> 放行 <c>internal</c>。
    /// 若保持 <c>internal</c>，Rules 侧无法清空队列，FR-024 的重试语义会退化成「每次回合重复写同一批解锁」。
    /// 故此处取最小改动的变体：<b><see cref="Restore"/> 仍为 <c>internal</c></b>（复原走存档层），
    /// 本方法改为 <c>public</c> 写通道。
    /// </para>
    /// <para>
    /// **调用方义务**：传入的队列 MUST 是 <see cref="Completed"/> 的子集
    /// （SC-014 ⑧；存档期由 <c>SaveStateValidator</c> 第 17 条强制）。MUST NOT 用它写入
    /// 未完成的键。
    /// </para>
    /// </remarks>
    public void RestorePendingUnlocks(IReadOnlyList<PendingUnlock> pendingUnlocks)
    {
        ArgumentNullException.ThrowIfNull(pendingUnlocks);

        _pendingUnlocks.Clear();
        _pendingUnlocks.AddRange(pendingUnlocks);
    }
}
