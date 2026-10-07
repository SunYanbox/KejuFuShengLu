using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;

namespace KFL.Infrastructure.Services;

/// <summary>
/// 从注入的随机来源生成存档级状态（research R-03；契约二 §1 的消费者）。
/// </summary>
/// <remarks>
/// 这是 001 内**唯一**把「随机来源」与「存档级状态」接起来的**产品代码路径**：
/// <see cref="GameState"/> 在 <c>KFL.Core</c> 里看不到 <see cref="IRandomService"/>，
/// 故「16 字节 → UUID」的转换必须落在这里。MUST NOT 使用全局标识工厂。
/// </remarks>
public sealed class GameStateFactory
{
    private const int GuidSizeInBytes = 16;

    private readonly IRandomService _randomService;

    /// <summary>构造注入随机来源。</summary>
    /// <param name="randomService">随机来源。</param>
    /// <exception cref="ArgumentNullException"><paramref name="randomService"/> 为 <c>null</c>。</exception>
    public GameStateFactory(IRandomService randomService)
    {
        ArgumentNullException.ThrowIfNull(randomService);
        _randomService = randomService;
    }

    /// <summary>创建一个存档级状态；唯一标识由注入的随机来源生成（同种子 → 同标识）。</summary>
    /// <param name="date">起始游戏年月；新建存档用 1 年 1 月。</param>
    /// <param name="difficulty">难度。</param>
    /// <param name="origin">出身。</param>
    /// <param name="family">当前家族。</param>
    /// <param name="economy">
    /// 家族经济聚合，**由调用方构造并传入**（R-13）。本方法 MUST NOT 决定新建存档的生活费初始档位：
    /// 该值单点在 <c>KFL.Rules/Config/LivingCostTable.InitialStandard</c>，而
    /// <c>KFL.Infrastructure</c> 看不到 <c>KFL.Rules</c>（G-05）——在此代填会引入第二个出处（SC-008）。
    /// </param>
    /// <returns>新建的存档级状态。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="family"/> 或 <paramref name="economy"/> 为 <c>null</c>。</exception>
    public GameState Create(
        GameDate date,
        Difficulty difficulty,
        Origin origin,
        Family family,
        FamilyEconomy economy)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(economy);

        Span<byte> buffer = stackalloc byte[GuidSizeInBytes];
        _randomService.NextBytes(buffer);

        return new GameState(new Guid(buffer), date, difficulty, origin, family, economy);
    }
}
