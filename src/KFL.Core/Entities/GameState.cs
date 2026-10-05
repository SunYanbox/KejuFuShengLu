using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Core.Entities;

/// <summary>
/// 存档级状态（规格书 §3、§11、§14；data-model §2.3）。
/// </summary>
/// <remarks>
/// <para>
/// 构造函数**只接受已生成的 <see cref="Guid"/>**：<c>IRandomService</c> 定义在
/// <c>KFL.Infrastructure</c>，而 <c>KFL.Core</c> 反向引用它会违反 G-05；也不能改用
/// <c>Guid</c> 的全局标识工厂（G-07 会拦）。「16 字节 → UUID」的转换落在
/// <c>KFL.Infrastructure/Services/GameStateFactory.cs</c>（research R-03，用户已裁决保留）。
/// </para>
/// <para>
/// **明确不含**：资产池、商本、现金 / 储蓄 / 贷款、统计容器——均属阶段②及以后
/// （spec Out of Scope）。
/// </para>
/// </remarks>
public sealed class GameState
{
    /// <summary>构造并校验。</summary>
    /// <param name="id">唯一标识；MUST NOT 为 <see cref="Guid.Empty"/>。</param>
    /// <param name="currentDate">当前游戏年月；新建存档取 1 年 1 月（本类型不强制，加载存档时须能表达任意年月）。</param>
    /// <param name="difficulty">难度。</param>
    /// <param name="origin">出身（规格书 §10.1）。</param>
    /// <param name="family">当前家族。</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> 为 <see cref="Guid.Empty"/>。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="family"/> 为 <c>null</c>。</exception>
    public GameState(Guid id, GameDate currentDate, Difficulty difficulty, Origin origin, Family family)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("GameState.Id MUST NOT 为 Guid.Empty（FR-012）。", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(family);

        Id = id;
        CurrentDate = currentDate;
        Difficulty = difficulty;
        Origin = origin;
        Family = family;
    }

    /// <summary>唯一标识。**无公开写入通道**（FR-012：标识 MUST NOT 随存档改名而变化）。</summary>
    public Guid Id { get; }

    /// <summary>当前游戏年月。推进属阶段②，本阶段只承载起始值（1 年 1 月）。</summary>
    public GameDate CurrentDate { get; internal set; }

    /// <summary>难度；同一存档内可随时切换（切换逻辑属阶段⑫，规格书 §11）。</summary>
    public Difficulty Difficulty { get; set; }

    /// <summary>出身；创建存档时选择（规格书 §10.1）。</summary>
    public Origin Origin { get; }

    /// <summary>当前家族。</summary>
    public Family Family { get; }
}
