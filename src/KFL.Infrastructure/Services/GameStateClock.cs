using KFL.Core.Entities;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;

namespace KFL.Infrastructure.Services;

/// <summary>
/// 以 <see cref="GameState.CurrentDate"/> 为后端的游戏时间来源（契约二 §2）。
/// </summary>
/// <remarks>
/// MUST NOT 读取系统墙钟时间或任何系统时钟——时间只由推演推进。
/// </remarks>
public sealed class GameStateClock : IGameClock
{
    private readonly GameState _state;

    /// <summary>构造注入存档级状态。</summary>
    /// <param name="state">存档级状态。</param>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 <c>null</c>。</exception>
    public GameStateClock(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
    }

    /// <inheritdoc />
    public GameDate Current => _state.CurrentDate;
}
