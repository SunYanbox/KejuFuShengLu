using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Achievements;
using KFL.Rules.Settlement;

namespace KFL.Rules.Achievements;

/// <summary>
/// 成就判定器：68 条成就的月判与即时判定，外加绝嗣补判（FR-004~FR-009、FR-032）。
/// </summary>
/// <remarks>
/// <para>
/// **唯一次序**（契约二十四 §6「条款 25」；<c>spec.md</c> Assumptions 19）：一次编排内
/// 依次为 —— ① 推进累计量（<see cref="AchievementCounters.AbsorbMonthFacts"/>）→
/// ② 判 68 条（<see cref="AchievementPredicates.IsSatisfied"/>）→
/// ③ 判绝嗣并补判（<see cref="ExtinctionJudge.TryTrigger"/>）。**先成就、后绝嗣**：
/// 触发绝嗣的当月事实仍能正常解锁（例如同月的 <c>ACH-063</c> 路有冻骨）。
/// 累计量 MUST 先跑：编年史 ③ 读 <c>AchievementState.PeakTotalAssets</c>（data-model §3.6）。
/// </para>
/// <para>
/// **副作用边界**：本类 MUST NOT 访问时钟 / 文件 / 网络、MUST NOT 消费随机、MUST NOT 造 GUID
/// （来源 UUID 一律取 <c>GameState.Id</c>）。全局档案落盘 ONLY 发生在回合末 ⑤
/// （<c>TurnRunner</c>；契约二十三 §5），本类只把待落盘项排入队列。
/// </para>
/// <para>
/// **结束态冻结**：<c>Ending.IsEnded</c> 为真时两入口均**提前返回空结果**且零状态变更。
/// </para>
/// <para>
/// **难度口径**：<c>CompletedAt = state.CurrentDate</c>、<c>Difficulty = state.Difficulty</c>
/// ——**不**取 <c>PendingDifficulty</c>（未生效的难度切换不改变成就归属）。
/// </para>
/// </remarks>
public static class AchievementAdjudicator
{
    /// <summary>回合末判定：推进累计量、判 68 条、判绝嗣（FR-004、FR-032）。</summary>
    /// <param name="state">当前游戏状态。</param>
    /// <param name="settlement">当月结算报告（回合末判定的月事实来源）。</param>
    /// <param name="archiveMarks">全局档案的已完成项（跨存档读面，判据当前不读该面）。</param>
    /// <param name="archiveOrigins">全局档案的「出身达成」段（<c>ACH-047</c> 判据的唯一输入）。</param>
    /// <returns>本次判定结果；结束态或无可判定项时列表为空。</returns>
    /// <exception cref="ArgumentNullException">任一参数为 <c>null</c>。</exception>
    public static AchievementOutcome Judge(
        GameState state,
        SettlementResult settlement,
        IReadOnlyList<AchievementMark> archiveMarks,
        IReadOnlyList<OriginRecord> archiveOrigins)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(archiveMarks);
        ArgumentNullException.ThrowIfNull(archiveOrigins);

        if (state.Ending.IsEnded)
        {
            return AchievementOutcome.Empty;
        }

        // ① 推进累计量（必须在判据之前：ACH-033/050~053 与编年史 ③ 都读它们）。
        AchievementCounters.AbsorbMonthFacts(state, settlement);

        // ② 判 68 条（目录升序，保证 NewlyUnlocked 的次序确定）。
        var newlyUnlocked = new List<AchievementId>();
        var alreadyCompleted = new List<AchievementId>();
        JudgeCatalog(
            new AchievementContext(state, settlement, archiveMarks, archiveOrigins, state.CurrentDate),
            state,
            newlyUnlocked,
            alreadyCompleted);

        // ③ 判绝嗣并补判 ACH-044（地狱加 ACH-048）；先成就后绝嗣是本类的硬次序。
        var extinctionJustTriggered = ExtinctionJudge.TryTrigger(state);
        if (extinctionJustTriggered)
        {
            SupplementExtinction(state, newlyUnlocked, alreadyCompleted);
        }

        return new AchievementOutcome
        {
            NewlyUnlocked = newlyUnlocked,
            AlreadyCompleted = alreadyCompleted,
            ExtinctionJustTriggered = extinctionJustTriggered,
        };
    }

    /// <summary>动作型即时判定：动作成功后判定 68 条（FR-009；契约二十二 §3）。</summary>
    /// <param name="state">当前游戏状态。</param>
    /// <param name="trigger">触发本判定的动作入口。</param>
    /// <returns>本次判定结果；结束态或 <see cref="AchievementTrigger.None"/> 时列表为空。</returns>
    /// <remarks>
    /// <b>月事实缺席</b>：即时判定没有当月结算报告，故 <see cref="AchievementContext.Settlement"/>
    /// 传 <c>null</c>，仅「状态面」判据可能成立（实现上仍全表判，成本可忽略）。
    /// 本方法 MUST NOT 落全局文件、MUST NOT 写档、MUST NOT 抛（「条款 10」）；
    /// 调用位置 MUST 是「动作执行成功之后、入口返回之前」（「条款 9」）。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 <c>null</c>。</exception>
    public static AchievementOutcome Report(GameState state, AchievementTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Ending.IsEnded || trigger == AchievementTrigger.None)
        {
            return AchievementOutcome.Empty;
        }

        var newlyUnlocked = new List<AchievementId>();
        var alreadyCompleted = new List<AchievementId>();
        JudgeCatalog(
            new AchievementContext(state, null, [], [], state.CurrentDate),
            state,
            newlyUnlocked,
            alreadyCompleted);

        return new AchievementOutcome
        {
            NewlyUnlocked = newlyUnlocked,
            AlreadyCompleted = alreadyCompleted,
            ExtinctionJustTriggered = false,
        };
    }

    private static void JudgeCatalog(
        AchievementContext context,
        GameState state,
        List<AchievementId> newlyUnlocked,
        List<AchievementId> alreadyCompleted)
    {
        foreach (var definition in AchievementCatalog.All)
        {
            if (!AchievementPredicates.IsSatisfied(definition.Id, context))
            {
                continue;
            }

            var mark = new AchievementMark(definition.Id, state.Difficulty, state.CurrentDate);
            if (state.Achievements.MarkCompletedAndEnqueue(mark, state.Id))
            {
                newlyUnlocked.Add(definition.Id);
            }
            else
            {
                alreadyCompleted.Add(definition.Id);
            }
        }
    }

    private static void SupplementExtinction(
        GameState state,
        List<AchievementId> newlyUnlocked,
        List<AchievementId> alreadyCompleted)
    {
        MarkSupplemented(state, AchievementId.FamilyExtinct, newlyUnlocked, alreadyCompleted);

        if (state.Difficulty == Difficulty.Hell)
        {
            MarkSupplemented(state, AchievementId.HellEnding, newlyUnlocked, alreadyCompleted);
        }
    }

    private static void MarkSupplemented(
        GameState state,
        AchievementId id,
        List<AchievementId> newlyUnlocked,
        List<AchievementId> alreadyCompleted)
    {
        var mark = new AchievementMark(id, state.Difficulty, state.CurrentDate);
        if (state.Achievements.MarkCompletedAndEnqueue(mark, state.Id))
        {
            newlyUnlocked.Add(id);
        }
        else
        {
            alreadyCompleted.Add(id);
        }
    }
}
