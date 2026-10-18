using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Achievements;
using KFL.Infrastructure.Services;
using KFL.Rules.Achievements;
using KFL.Rules.Config;
using KFL.Rules.Settlement;

namespace KFL.Rules.Save;

/// <summary>
/// 一回合的推进器：结算、按节奏存档、按节奏备份（004-save-system；契约十一 §2/§3）。
/// </summary>
/// <remarks>
/// <para>
/// <b>存档写在结算之外</b>：004 对契约三钉死的结算步骤序列一个字都没动（005 后该序列为八步）——
/// 存档不是序列里的任何一步，而是<b>套在结算外面</b>的一层编排（R-17）。所以本类不是结算引擎的一部分，
/// <c>MonthlySettlementEngine</c> 也不知道自己会被存档。
/// </para>
/// <para>
/// <b>顺序是结构性的</b>：①②③④ 的次序 MUST NOT 靠 <c>try/catch</c> 维持——
/// 「结算抛了就不存档」是「③ 在 ① 正常返回之后」这个形状本身给的；
/// 「备份的是刚写下的内容」是「④ 在 ③ 成功之后」给的。写成 catch 也能过测试，
/// 但那条不变式就从「结构」降级成了「有人记得加 catch」。
/// </para>
/// <para>
/// <b>写在内存里结算之后</b>：③ 失败时结算已经改了 <paramref name="state"/>，此时 MUST
/// 保留这份内存状态并向上抛（Q6）——回滚需要一个「结算前快照」，那就是 FR-020 明文禁止的
/// 第二个内存真源；吞掉失败则让玩家以为存上了。于是「本回合推进了却没落盘」是一个**允许**
/// 的中间态：下一次 <see cref="Advance"/> 的 ③ 会把当前<b>完整</b>状态（含那个未落盘的月）
/// 一并写下，不跳、不补、不串位。
/// </para>
/// <para>
/// <b>当前档不是状态</b>：档名与格式在组合期由 ctor 注入（Q4），本类不持有「上次存的档」，
/// 也不把它记进 <c>GameState</c>——它是会话信息，MUST NOT 落盘。
/// </para>
/// <para>
/// <b>不读钟、不自增计数器</b>：回合数由 <see cref="SaveCadencePolicy.CompletedTurnsAt"/> 从
/// 日期导出（契约十一 §1）。
/// </para>
/// </remarks>
public sealed class TurnRunner
{
    private readonly IRandomService _randomService;
    private readonly ISaveService? _saveService;
    private readonly SaveSlot _slot;
    private readonly INameGenerator _nameGenerator;
    private readonly IAchievementStore? _achievementStore;

    /// <summary>建推进器。</summary>
    /// <param name="randomService">随机源；每回合新建的结算引擎要用它。</param>
    /// <param name="saveService">
    /// 存档服务；<c>null</c> 即「自动存档关」（契约十一 §3 条款 1）——开关就是注入与否，
    /// MUST NOT 另加一个持久化设置。
    /// </param>
    /// <param name="slot">当前档（档名 + 格式）；会话信息，组合期注入。</param>
    /// <param name="nameGenerator">姓名来源（007 新增）：转交给每回合新建的结算引擎，供 ⑧-a/⑧-b 取名。</param>
    /// <param name="achievementStore">
    /// 全局成就档案（008 新增）；<c>null</c> ⇒ 跳过 ⑤ 全局落盘（判定与档内队列仍照跑）。
    /// **本参 MUST 带默认值**，使既有构造点无需改动（契约二十三「条款 20」）。
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="randomService"/> 或 <paramref name="nameGenerator"/> 为 <c>null</c>。
    /// </exception>
    public TurnRunner(
        IRandomService randomService,
        ISaveService? saveService,
        SaveSlot slot,
        INameGenerator nameGenerator,
        IAchievementStore? achievementStore = null)
    {
        ArgumentNullException.ThrowIfNull(randomService);
        ArgumentNullException.ThrowIfNull(nameGenerator);

        _randomService = randomService;
        _saveService = saveService;
        _slot = slot;
        _nameGenerator = nameGenerator;
        _achievementStore = achievementStore;
    }

    /// <summary>推进一个游戏月，并按节奏存档/备份（008 起在 ④ 之后追加 ⑤ 全局落盘）。</summary>
    /// <param name="state">游戏状态（就地推进）。</param>
    /// <returns>本回合的增量快照，含 ⑤ 的结果。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 <c>null</c>。</exception>
    /// <exception cref="InvalidOperationException">
    /// 家族已绝嗣（<see cref="GameState.Ending"/> 的 <c>IsEnded</c> 为真）——结束态存档不可继续推进。
    /// 抛出发生在任何结算之前，保证**零状态变更**（契约二十四「条款 19」；<c>spec.md</c> Assumptions 20）。
    /// </exception>
    /// <exception cref="SaveFileException">落盘失败（内存状态已保留）。</exception>
    /// <exception cref="SaveAccessException">存档目录不可写。</exception>
    /// <remarks>
    /// <para>
    /// <b>五步次序</b>（契约二十三「条款 19」）：① 结算 → ② 成就与绝嗣判定 → ③ 覆盖当前档 →
    /// ④ 备份 → ⑤ 全局成就落盘。② 在 ① 之后是刚需：判据读的就是本回合的月事实与已推进的
    /// <c>CurrentDate</c>。
    /// </para>
    /// <para>
    /// <b>③ 的短路不影响 ⑤</b>（「条款 21」）：<c>_saveService</c> 为 <c>null</c>（自动存档关）
    /// 时 ③④ 跳过，但 ⑤ **仍执行**——成就是独立于存档的能力。
    /// </para>
    /// <para>
    /// <b>③ 与 ⑤ 的异常策略刻意相反</b>（「条款 22」）：③ 失败上抛且不回滚；⑤ 失败**不抛**、
    /// 队列保留待下回合重试，诊断经 <see cref="TurnOutcome.AchievementFlush"/> 交回调用方。
    /// </para>
    /// </remarks>
    public TurnOutcome Advance(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // 结束闸门：必须排在**任何结算之前**，保证拒绝时零状态变更（契约二十四「条款 19」）。
        // MUST NOT 返回「无变化的 TurnOutcome」——静默无操作会让调用方以为推进成功。
        if (state.Ending.IsEnded)
        {
            throw new InvalidOperationException("家族已绝嗣，存档不可继续推进。");
        }

        // ① 结算。引擎绑定到这个具体状态上（既有调用点的同款形状）；抛出 ⇒ 本回合不存档。
        var settlement = new MonthlySettlementEngine(_randomService, new GameStateClock(state), _nameGenerator).Settle(state);

        // ② 成就与绝嗣判定（008 新增）。先读全局档案的只读面（缺文件 ⇒ 空档案），再判：
        //    判据要读全局档案的「出身达成」段（ACH-047），且本次新解锁项要进档内队列。
        var archive = _achievementStore?.Read() ?? AchievementArchive.Empty;
        var archiveMarks = new List<AchievementMark>(archive.Achievements.Count);
        foreach (var completed in archive.Achievements)
        {
            archiveMarks.Add(new AchievementMark(completed.Achievement, completed.Difficulty, completed.CompletedAt));
        }

        var achievement = AchievementAdjudicator.Judge(state, settlement, archiveMarks, archive.Origins);

        var turns = SaveCadencePolicy.CompletedTurnsAt(state.CurrentDate);
        var saved = false;
        var backedUp = false;

        // ③ 覆盖当前档。MUST 与手动存档走同一个 Save 入口（没有第二条序列化路径）。
        //    没注入存档服务 ⇒ 自动存档关：跳过 ③④（结算照跑：US2 AS4），但 ⑤ 仍执行。
        if (_saveService is not null)
        {
            _saveService.Save(state, _slot, overwrite: true);
            saved = true;

            // ④ 备份严格在 ③ 成功之后：备份的是「刚写下的内容」。
            if (SaveCadencePolicy.ShouldBackup(turns))
            {
                _saveService.Backup(_slot);
                backedUp = true;
            }
        }

        // ⑤ 全局成就落盘（唯一落盘点；MUST NOT 进结算步骤表，FR-035）。
        return new TurnOutcome(settlement, turns, saved, backedUp)
        {
            AchievementFlush = FlushAchievements(state, achievement),
        };
    }

    /// <summary>⑤ 全局落盘：成功则清空档内队列，失败则队列保留（契约二十三「条款 18/23」）。</summary>
    /// <remarks>
    /// 未配置成就存储、或档内队列为空时不写盘并返回 <c>null</c>（「条款 20」）。
    /// 写盘内容由存储层负责「既有记录 ∪ 本次增量」（「条款 23」）——本层只交增量。
    /// </remarks>
    private AchievementFlushResult? FlushAchievements(GameState state, AchievementOutcome achievement)
    {
        if (_achievementStore is null)
        {
            return null;
        }

        var pending = state.Achievements.PendingUnlocks;
        if (pending.Count == 0)
        {
            return null;
        }

        var result = _achievementStore.Flush(pending, BuildOriginAdditions(state, achievement, pending));
        if (result.IsSuccess)
        {
            // 落盘成功才清空队列：失败保留，下一次回合带着**原始**解锁年月重试（FR-024）。
            state.Achievements.RestorePendingUnlocks([]);
        }

        return result;
    }

    /// <summary>构造本次的「出身达成」增量：只认绝嗣与百年世家（契约二十三「条款 38」）。</summary>
    /// <remarks>
    /// 千秋万代**不**入 <c>Origins</c>——规格书 §14 只把「出身达成」定义为绝嗣 / 百年世家两类。
    /// 日期取「已结束则 <c>EndedAt</c>，否则 <c>CurrentDate</c>」：绝嗣触发于本月，而玩家选择
    /// 结束游戏后 <c>CurrentDate</c> 不再推进（结束态冻结），故以结束日为准。
    /// 增量取自**队列本身**（而非本次判定结果）：⑤ 失败后队列原样保留，重试那一回合也要能补齐。
    /// </remarks>
    private static List<OriginRecord> BuildOriginAdditions(
        GameState state,
        AchievementOutcome achievement,
        IReadOnlyList<PendingUnlock> pending)
    {
        _ = achievement;
        List<OriginRecord>? additions = null;
        var at = state.Ending.EndedAt ?? state.CurrentDate;

        foreach (var unlock in pending)
        {
            var milestone = unlock.Achievement switch
            {
                AchievementId.FamilyExtinct => OriginMilestone.Extinction,
                AchievementId.CenturyHouse => OriginMilestone.CenturyFamily,
                _ => (OriginMilestone?)null,
            };

            if (milestone is null)
            {
                continue;
            }

            additions ??= [];
            additions.Add(new OriginRecord(state.Origin, milestone.Value, at, state.Id));
        }

        return additions ?? [];
    }

    /// <summary>退出时存档（UI 轨 U1 的进程退出钩子会调它）。</summary>
    /// <remarks>
    /// 与自动存档的差别**只在谁触发**：同一个 <c>Save(state, slot, overwrite: true)</c> 入口、
    /// 同一个当前档、没有第二套参数、没有第二条序列化路径（FR-012；契约十一 §3 条款 3）。
    /// </remarks>
    /// <param name="state">游戏状态。</param>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 <c>null</c>。</exception>
    public void SaveOnExit(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _saveService?.Save(state, _slot, overwrite: true);
    }
}
