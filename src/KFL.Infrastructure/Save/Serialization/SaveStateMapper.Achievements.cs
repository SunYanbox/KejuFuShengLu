// SaveStateMapper 的成就部分：成就累计量、终局与编年史映射。

using KFL.Core.Entities;
using KFL.Core.ValueObjects;

namespace KFL.Infrastructure.Save.Serialization;

public static partial class SaveStateMapper
{
    /// <summary>
    /// 把档内成就累计量拍成瞬态 DTO（008-achievements-extinction；契约二十三 §6、data-model §7.1）。
    /// </summary>
    /// <remarks>
    /// 逐项搬运**持久面**（恰 8 项），**不含**任何派生量：已解锁数、七系进度、加速集都不入档，
    /// 由复原方从 ⑦ 重建（SC-014）。
    /// </remarks>
    /// <param name="achievements">档内成就累计量。</param>
    private static AchievementStateDto ToDto(AchievementState achievements) => new()
    {
        SuccessfulBribeCount = achievements.SuccessfulBribeCount,
        BirthCount = achievements.BirthCount,
        Lifespans = [.. achievements.Lifespans],
        PeakTotalAssets = achievements.PeakTotalAssets.Wen,
        EverHadLoan = achievements.EverHadLoan,
        DebugWakeCount = achievements.DebugWakeCount,
        Completed = [.. achievements.Completed.Select(ToDto)],
        PendingUnlocks = [.. achievements.PendingUnlocks.Select(ToDto)],
    };

    /// <summary>一条完成记录 ↔ 瞬态 DTO（008；编号按底层整数写，年月拆两个键）。</summary>
    /// <param name="mark">完成记录。</param>
    private static AchievementMarkDto ToDto(AchievementMark mark) => new()
    {
        Achievement = (int)mark.Achievement,
        Difficulty = (int)mark.Difficulty,
        CompletedYear = mark.CompletedAt.Year,
        CompletedMonth = mark.CompletedAt.Month,
    };

    /// <summary>一条待落盘解锁 ↔ 瞬态 DTO（008；与完成记录的差别只有末键）。</summary>
    /// <param name="unlock">待落盘解锁。</param>
    private static PendingUnlockDto ToDto(PendingUnlock unlock) => new()
    {
        Achievement = (int)unlock.Achievement,
        Difficulty = (int)unlock.Difficulty,
        UnlockedYear = unlock.UnlockedAt.Year,
        UnlockedMonth = unlock.UnlockedAt.Month,
        SourceSaveId = unlock.SourceSaveId,
    };

    /// <summary>
    /// 把终局状态拍成瞬态 DTO（008；契约二十三「条款 26」）。
    /// </summary>
    /// <remarks>
    /// 未结束 ⇒ <c>null</c>。不变量 `IsEnded ⇔ EndedAt.HasValue ∧ Chronicle is not null` 由
    /// <c>EndingState</c> 与第 ⑳ 条校验共同保证，这里只需把它翻译成 DTO 侧的两态表示。
    /// </remarks>
    /// <param name="ending">终局状态。</param>
    private static EndingStateDto? ToDto(EndingState ending) =>
        ending.IsEnded && ending.EndedAt is { } endedAt && ending.Chronicle is { } chronicle
            ? new EndingStateDto
            {
                EndedYear = endedAt.Year,
                EndedMonth = endedAt.Month,
                Chronicle = ToDto(chronicle),
            }
            : null;

    /// <summary>编年史七项 ↔ 瞬态 DTO（008；合约二十四 §1，次序与规格书 §15 一致）。</summary>
    /// <param name="chronicle">编年史快照。</param>
    private static ChronicleDto ToDto(ChronicleSnapshot chronicle) => new()
    {
        SurvivedYears = chronicle.SurvivedYears,
        Generations = chronicle.Generations,
        PeakTotalAssets = chronicle.PeakTotalAssets.Wen,
        HighestDegree = (int)chronicle.HighestDegree,
        TotalJinShi = chronicle.TotalJinShi,
        AverageLifespan = chronicle.AverageLifespan,
        Genealogy = [.. chronicle.Genealogy.Select(id => id.Value)],
    };
}
