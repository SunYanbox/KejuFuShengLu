using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Rules.Achievements;

/// <summary>
/// 一个系列的完成进度（纯投影结果，<b>不是</b>持久字段）。
/// </summary>
/// <param name="Series">系列。</param>
/// <param name="Completed">该系列已达成的条数（按 <see cref="AchievementId"/> 去重）。</param>
/// <param name="Total">该系列的总条数（<see cref="AchievementCatalog.CountFor"/>）。</param>
public readonly record struct AchievementSeriesProgress(
    AchievementSeries Series,
    int Completed,
    int Total);

/// <summary>
/// 只读查询面：供 U1 徽章与 U2 结局画面直接取数（<c>data-model.md</c> §3.9）。
/// </summary>
/// <remarks>
/// <para>
/// <b>全部由 ⑦ 投影</b>：三个入口都只读 <see cref="AchievementState.Completed"/>，
/// 不落盘、不缓存、不建视图、不新增第二真源（SC-014）。
/// </para>
/// <para>
/// <b>纯度</b>：没有副作用的纯函数——调用前后 <see cref="GameState"/> 逐字段不变，
/// 不消费随机、不读时钟/文件（G-07）。
/// </para>
/// </remarks>
public static class AchievementQuery
{
    /// <summary>已达成条数（按 <see cref="AchievementId"/> 去重，与难度无关）。</summary>
    /// <param name="state">游戏状态。</param>
    /// <returns>已达成条数。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 <c>null</c>。</exception>
    public static int CompletedCount(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var seen = new HashSet<AchievementId>();
        foreach (var mark in state.Achievements.Completed)
        {
            seen.Add(mark.Achievement);
        }

        return seen.Count;
    }

    /// <summary>某条成就是否已达成（任意难度）。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="id">成就标识。</param>
    /// <returns>已达成或未达成。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 <c>null</c>。</exception>
    public static bool IsCompleted(GameState state, AchievementId id)
    {
        ArgumentNullException.ThrowIfNull(state);

        foreach (var mark in state.Achievements.Completed)
        {
            if (mark.Achievement == id)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>某个系列的完成进度。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="series">系列。</param>
    /// <returns>该系列的进度。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 <c>null</c>。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="series"/> 不是已定义的成员。</exception>
    public static AchievementSeriesProgress SeriesProgress(GameState state, AchievementSeries series)
    {
        ArgumentNullException.ThrowIfNull(state);

        var seen = new HashSet<AchievementId>();
        foreach (var mark in state.Achievements.Completed)
        {
            if (AchievementCatalog.Of(mark.Achievement).Series == series)
            {
                seen.Add(mark.Achievement);
            }
        }

        return new AchievementSeriesProgress(series, seen.Count, AchievementCatalog.CountFor(series));
    }
}
