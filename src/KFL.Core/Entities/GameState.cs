using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Core.Entities;

/// <summary>
/// 存档级状态（规格书 §3、§11、§14；data-model §2.3、§3.7）。
/// </summary>
/// <remarks>
/// <para>
/// 构造函数**只接受已生成的 <see cref="Guid"/>**：<c>IRandomService</c> 定义在
/// <c>KFL.Infrastructure</c>，而 <c>KFL.Core</c> 反向引用它会违反 G-05；也不能改用
/// <c>Guid</c> 的全局标识工厂（G-07 会拦）。「16 字节 → UUID」的转换落在
/// <c>KFL.Infrastructure/Services/GameStateFactory.cs</c>（research R-03，用户已裁决保留）。
/// </para>
/// <para>
/// **经济状态自阶段② 起统一归 <see cref="Economy"/> 聚合**（<see cref="FamilyEconomy"/>，
/// data-model §3.6/§3.7）：现金 / 储蓄 / 商本 / 贷款 / 流水账 / 资产 / 米价系数 / 生活费档位 /
/// 饥馑状态**一律不在本类型上直接暴露**——本类型只持有聚合本身。这不是「阶段① 的边界被放弃」，
/// 而是**有依据地放宽**：资金流动的唯一写入通道必须是聚合（FR-021、SC-005），
/// 让 `GameState` 直接挂 `Cash` / `Ledger` 之类的成员会让旁路重新变得可表达。
/// </para>
/// <para>
/// **时间只能经 <see cref="AdvanceMonth"/> 推进**：<see cref="CurrentDate"/> 是**只读**属性
/// （无任何 setter），因此「1 回合 = 1 游戏月、跨年进位」的语义不可绕过（§3；R-03）。
/// </para>
/// </remarks>
public sealed class GameState
{
    private GameDate _currentDate;

    /// <summary>构造并校验。</summary>
    /// <param name="id">唯一标识；MUST NOT 为 <see cref="Guid.Empty"/>。</param>
    /// <param name="currentDate">当前游戏年月；新建存档取 1 年 1 月（本类型不强制，加载存档时须能表达任意年月）。</param>
    /// <param name="difficulty">难度。</param>
    /// <param name="origin">出身（规格书 §10.1）。</param>
    /// <param name="family">当前家族。</param>
    /// <param name="economy">
    /// 家族经济聚合（data-model §3.6）。**由调用方给出**：新建存档的生活费初始档位单点在
    /// <c>KFL.Rules/Config/LivingCostTable.InitialStandard</c>，而 <c>KFL.Infrastructure</c>
    /// 看不到 <c>KFL.Rules</c>（G-05），故本类型 MUST NOT 代它决定初值（R-13）。
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="id"/> 为 <see cref="Guid.Empty"/>。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="family"/> 或 <paramref name="economy"/> 为 <c>null</c>。</exception>
    public GameState(
        Guid id,
        GameDate currentDate,
        Difficulty difficulty,
        Origin origin,
        Family family,
        FamilyEconomy economy)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("GameState.Id MUST NOT 为 Guid.Empty（FR-012）。", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(economy);

        Id = id;
        _currentDate = currentDate;
        Difficulty = difficulty;
        Origin = origin;
        Family = family;
        Economy = economy;
    }

    /// <summary>唯一标识。**无公开写入通道**（FR-012：标识 MUST NOT 随存档改名而变化）。</summary>
    public Guid Id { get; }

    /// <summary>
    /// 当前游戏年月。**只读**：推进只能经 <see cref="AdvanceMonth"/>（一次一个月、跨年进位），
    /// 结算语义为「结算本年月当月的账目，随后 <see cref="AdvanceMonth"/>」（R-03；契约三 §2）。
    /// </summary>
    public GameDate CurrentDate => _currentDate;

    /// <summary>难度；同一存档内可随时切换（切换逻辑属阶段⑫，规格书 §11）。</summary>
    public Difficulty Difficulty { get; set; }

    /// <summary>
    /// 待生效的难度；<c>null</c> = 无待生效切换。
    /// </summary>
    /// <remarks>
    /// 难度「次月生效」的落点（§11；R-09）：结算第①步提升为 <see cref="Difficulty"/> 并置回
    /// <c>null</c>，当月结算全程使用提升后的值。切换难度的入口只写本属性。
    /// </remarks>
    public Difficulty? PendingDifficulty { get; set; }

    /// <summary>出身；创建存档时选择（规格书 §10.1）。</summary>
    public Origin Origin { get; }

    /// <summary>当前家族。</summary>
    public Family Family { get; }

    /// <summary>
    /// 家族经济聚合（资金流动的唯一入口，data-model §3.6）。**只读引用**：
    /// 内部状态的变化必须经聚合自己的方法，而不是替换整个聚合。
    /// </summary>
    public FamilyEconomy Economy { get; }

    /// <summary>
    /// 推进**一个月**（跨年进位：12 月 → 次年 1 月）。
    /// </summary>
    /// <remarks>
    /// 这是 <see cref="CurrentDate"/> 唯一的写入通道：不提供 setter，故调用方无法跳到任意年月，
    /// §3 的「1 回合 = 1 游戏月」得以保持（R-03；契约三 §2）。
    /// </remarks>
    public void AdvanceMonth()
    {
        _currentDate = _currentDate.Month == 12
            ? new GameDate(_currentDate.Year + 1, 1)
            : new GameDate(_currentDate.Year, _currentDate.Month + 1);
    }
}
