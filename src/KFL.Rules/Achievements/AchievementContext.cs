using KFL.Core.Entities;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Achievements;
using KFL.Rules.Settlement;

namespace KFL.Rules.Achievements;

/// <summary>
/// 一次判定的**只读输入**：状态面 + 当月事实 + 跨档档案 + 判定时点（契约二十二 §2 条款 3）。
/// </summary>
/// <remarks>
/// <para>
/// <b>谓词只读</b>：<see cref="AchievementPredicates.IsSatisfied"/> 拿到本结构后 MUST NOT 改动其中
/// 任何对象，也 MUST NOT 消费随机、读时钟、碰文件或网络（G-07 扫描 <c>src\KFL.Rules\</c>）。
/// </para>
/// <para>
/// <b>两条路径共用一个形状</b>：回合末判定（<see cref="AchievementAdjudicator.Judge"/>）带当月
/// <see cref="Settlement"/> 进来；动作型即时判定（<see cref="AchievementAdjudicator.Report"/>）
/// 没有「当月报告」，<see cref="Settlement"/> 为 <c>null</c>——此时「当月报告非空」型判据一律
/// 不成立（它们只可能在回合末成立，契约二十二 §2 条款 3）。
/// </para>
/// <para>
/// <b>时点恒取状态面</b>：<see cref="Now"/> 一律来自 <c>GameState.CurrentDate</c>，
/// MUST NOT 由调用方另传一个「现在」（R-06）。唯一例外是 ACH-037 襁褓之殇，它用**事实月份**
/// 的年龄（契约二十二 §2 条款 6）。
/// </para>
/// </remarks>
/// <param name="State">游戏状态（只读使用）。</param>
/// <param name="Settlement">当月结算报告；动作型即时判定为 <c>null</c>。</param>
/// <param name="ArchiveMarks">跨档全局档案里的成就记录（可能为空集合）。</param>
/// <param name="ArchiveOrigins">跨档全局档案里的出身达成段（可能为空集合）。</param>
/// <param name="Now">判定时点，恒等于 <c>GameState.CurrentDate</c>。</param>
public readonly record struct AchievementContext(
    GameState State,
    SettlementResult? Settlement,
    IReadOnlyList<AchievementMark> ArchiveMarks,
    IReadOnlyList<OriginRecord> ArchiveOrigins,
    GameDate Now);
