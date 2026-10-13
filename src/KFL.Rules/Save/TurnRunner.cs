using KFL.Core.Entities;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Services;
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

    /// <summary>建推进器。</summary>
    /// <param name="randomService">随机源；每回合新建的结算引擎要用它。</param>
    /// <param name="saveService">
    /// 存档服务；<c>null</c> 即「自动存档关」（契约十一 §3 条款 1）——开关就是注入与否，
    /// MUST NOT 另加一个持久化设置。
    /// </param>
    /// <param name="slot">当前档（档名 + 格式）；会话信息，组合期注入。</param>
    /// <exception cref="ArgumentNullException"><paramref name="randomService"/> 为 <c>null</c>。</exception>
    public TurnRunner(IRandomService randomService, ISaveService? saveService, SaveSlot slot)
    {
        ArgumentNullException.ThrowIfNull(randomService);

        _randomService = randomService;
        _saveService = saveService;
        _slot = slot;
    }

    /// <summary>推进一个游戏月，并按节奏存档/备份。</summary>
    /// <param name="state">游戏状态（就地推进）。</param>
    /// <returns>本回合的增量快照。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 <c>null</c>。</exception>
    /// <exception cref="SaveFileException">落盘失败（内存状态已保留）。</exception>
    /// <exception cref="SaveAccessException">存档目录不可写。</exception>
    public TurnOutcome Advance(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // ① 结算。引擎绑定到这个具体状态上（既有调用点的同款形状）；抛出 ⇒ 本回合不存档。
        var settlement = new MonthlySettlementEngine(_randomService, new GameStateClock(state)).Settle(state);

        // ② 没注入存档服务 ⇒ 自动存档关，直接返回（结算照跑，结果不受影响：US2 AS4）。
        if (_saveService is null)
        {
            return new TurnOutcome(
                settlement,
                SaveCadencePolicy.CompletedTurnsAt(state.CurrentDate),
                Saved: false,
                BackedUp: false);
        }

        // ③ 覆盖当前档。MUST 与手动存档走同一个 Save 入口（没有第二条序列化路径）。
        _saveService.Save(state, _slot, overwrite: true);

        // ④ 备份严格在 ③ 成功之后：备份的是「刚写下的内容」。
        var turns = SaveCadencePolicy.CompletedTurnsAt(state.CurrentDate);
        var backedUp = false;
        if (SaveCadencePolicy.ShouldBackup(turns))
        {
            _saveService.Backup(_slot);
            backedUp = true;
        }

        return new TurnOutcome(settlement, turns, Saved: true, BackedUp: backedUp);
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
