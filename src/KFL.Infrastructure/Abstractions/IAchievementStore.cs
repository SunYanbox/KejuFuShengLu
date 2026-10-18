using KFL.Core.ValueObjects;
using KFL.Infrastructure.Achievements;

namespace KFL.Infrastructure.Abstractions;

/// <summary>
/// 成就全局档案的读写接缝：一份**跨存档**的元进度面（008-achievements-extinction；契约二十三 §1）。
/// </summary>
/// <remarks>
/// <para>
/// <b>归属</b>：定义与实现都在 <c>KFL.Infrastructure</c>，与 <see cref="ISaveService"/> 同层同款
/// （契约二十三「条款 1」）。<c>KFL.Rules</c> 只依赖本接口，MUST NOT 触碰文件系统。
/// </para>
/// <para>
/// <b>就地失败，MUST NOT 抛</b>（契约二十三「条款 2」）：<see cref="Read"/> 在回合末判定**之前**
/// 被调用，一个抛异常的 <see cref="Read"/> 会把「成就文件坏掉」升级成「游戏无法推进」。
/// 因此目录不可写、文件被占用、JSON 语法错、结构非法、版本过高**一律以返回值表达**——
/// 这是本接口与 <see cref="ISaveService"/>（失败即抛）最刻意的差别。
/// </para>
/// <para>
/// <b>版本语义与存档本体相反</b>（契约二十三「条款 7」第 4 点）：存档本体是**等值闸门**
/// （版本不同即拒绝），成就面则对「<c>Version &gt; 当前版本</c>」**不失败**——它把未知键忽略、
/// 继续按已知字段使用。理由是成就面是**单调累积的元进度**，高版本文件里的额外信息对旧实现无害，
/// 为它拒绝启动是拿不到任何好处的破坏性行为。
/// </para>
/// </remarks>
public interface IAchievementStore
{
    /// <summary>全局成就文件的路径（恒为 <c>&lt;RootDirectory&gt;/achievements.json</c>）。</summary>
    /// <remarks>
    /// 该文件与当前存档的**格式无关**：同一玩家的成就面在 JSON 档与二进制档下 MUST 互认
    /// （契约二十三「条款 4」「条款 5」）。
    /// </remarks>
    string FilePath { get; }

    /// <summary>只读当前全局面。</summary>
    /// <remarks>
    /// <para>
    /// 文件**不存在** ⇒ 空档案且**无诊断**（缺失不是损坏，契约二十三「条款 17」）；
    /// 文件**损坏** ⇒ 先隔离（改名 <c>achievements.corrupt-&lt;n&gt;.json</c>）再以空档案继续，
    /// 诊断经 <see cref="AchievementArchive.Diagnostics"/> 返回（「条款 15」「条款 16」）。
    /// </para>
    /// <para>
    /// MUST NOT 在读取时对档案做任何「规整」（去重、排序、修正）——合并是
    /// <see cref="Flush"/> 的职责（「条款 14」）。
    /// </para>
    /// </remarks>
    /// <returns>当前全局面（永不抛）。</returns>
    AchievementArchive Read();

    /// <summary>把本次增量**合并进全量档案**并原子替换文件。</summary>
    /// <remarks>
    /// <para>
    /// 写盘内容恒为**全量合并后**的档案（既有记录 ∪ 本次增量），MUST NOT 只写增量
    /// （契约二十三「条款 23」）。合并键为 <c>(Achievement, Difficulty)</c>，仅当新日期的
    /// <c>UnlockedAt</c> **更早**时才替换日期与来源（「条款 11」）。
    /// </para>
    /// <para>
    /// <b>出身段由调用方构造</b>（「条款 13」）：<see cref="PendingUnlock"/> 不携带
    /// <see cref="KFL.Core.Enums.Origin"/>，实现无从得知，故增量经
    /// <paramref name="originAdditions"/> 显式传入——仅在本次含
    /// <c>FamilyExtinct</c> / <c>CenturyHouse</c> 时才非空（「条款 38」：千秋万代**不**入段）。
    /// </para>
    /// <para>
    /// 落点由 <c>AtomicFileWriter</c> 完成原子替换（临时文件 → 落盘刷盘 → 同卷改名，
    /// 「条款 3」）；任何失败 ⇒ <see cref="AchievementFlushResult.IsSuccess"/> 为 <c>false</c>
    /// 且**不抛**，由调用方保留待落盘队列择机重试。
    /// </para>
    /// </remarks>
    /// <param name="unlocks">本次待落盘的成就增量。</param>
    /// <param name="originAdditions">本次出身达成段增量（无则传空集合）。</param>
    /// <returns>落盘结果（永不抛）。</returns>
    AchievementFlushResult Flush(
        IReadOnlyList<PendingUnlock> unlocks,
        IReadOnlyList<OriginRecord> originAdditions);
}
