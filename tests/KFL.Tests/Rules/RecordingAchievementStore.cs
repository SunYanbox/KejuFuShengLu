using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Achievements;

namespace KFL.Tests.Rules;

/// <summary>一次全局成就落盘的记录（008-achievements-extinction）。</summary>
/// <param name="Unlocks">本次传入的待落盘增量（**已拷贝**，不受调用方后续清空影响）。</param>
/// <param name="Origins">本次传入的出身达成段增量（已拷贝）。</param>
public readonly record struct AchievementFlushCall(
    IReadOnlyList<PendingUnlock> Unlocks,
    IReadOnlyList<OriginRecord> Origins);

/// <summary>
/// 全局成就档案的**内存替身**（008-achievements-extinction；契约二十三「条款 37」）。
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="RecordingSaveService"/> 同款：断言的对象是**调用序列与入参**，不是盘上字节。
/// 真文件的对应断言另有 <c>Infrastructure/AchievementStoreTests</c>。
/// </para>
/// <para>
/// 刻意**不**把记录调用混进 <c>SaveTestSandbox</c>：那个类型已被定位为「存档测试唯一临时目录
/// 助手」，往里塞成就替身会让它的职责再次膨胀（契约二十三「条款 37」）。
/// </para>
/// <para>
/// <see cref="FlushFailures"/> 是队列：入队即成「下一次落盘返回这个失败原因」，用于验证
/// ⑤ 的「不上抛、队列保留、下次一次性写入」语义（FR-024 / SC-007）。
/// </para>
/// <para>
/// 本替身**不**模拟合并——合并规则的真源在 <c>AchievementStore.Flush</c>，
/// 替身只回放调用与返回值，否则同一个规则会有两份实现。
/// </para>
/// </remarks>
public sealed class RecordingAchievementStore : IAchievementStore
{
    /// <summary>按发生次序记录的调用名（<c>Read</c> / <c>Flush</c>）。</summary>
    public List<string> Calls { get; } = [];

    /// <summary>每次 <c>Flush</c> 的入参快照。</summary>
    public List<AchievementFlushCall> Flushes { get; } = [];

    /// <summary>下一次 <c>Flush</c> 要返回的失败原因队列。</summary>
    public Queue<string> FlushFailures { get; } = new();

    /// <summary><c>Read</c> 要返回的全局面（默认空档案）。</summary>
    public AchievementArchive Archive { get; set; } = AchievementArchive.Empty;

    /// <summary>读取次数。</summary>
    public int ReadCount => Calls.Count(call => call == "Read");

    /// <summary>落盘次数。</summary>
    public int FlushCount => Flushes.Count;

    /// <inheritdoc />
    public string FilePath { get; set; } = "(内存替身)/achievements.json";

    /// <inheritdoc />
    public AchievementArchive Read()
    {
        Calls.Add("Read");
        return Archive;
    }

    /// <inheritdoc />
    public AchievementFlushResult Flush(
        IReadOnlyList<PendingUnlock> unlocks,
        IReadOnlyList<OriginRecord> originAdditions)
    {
        Calls.Add("Flush");

        // 深拷贝入参：TurnRunner 成功后会立刻清空待落盘队列，断言若持的是同一个 List
        // 就只能看到空集合——那会让「⑤ 真的收到了增量」这条断言永远假绿。
        Flushes.Add(new AchievementFlushCall([.. unlocks], [.. originAdditions]));

        return FlushFailures.Count > 0
            ? AchievementFlushResult.Failure(FlushFailures.Dequeue())
            : AchievementFlushResult.Success();
    }
}
