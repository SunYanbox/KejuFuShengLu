using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Rules.Achievements;

/// <summary>
/// 编年史快照的**唯一**构造点（规格书 §15:632-641；FR-028；契约二十四 §1；SC-011）。
/// </summary>
/// <remarks>
/// <para>
/// 七项在绝嗣成立的**那一次** `Advance` 内一次算定并冻结（契约二十四「条款 1」）；
/// <see cref="ChronicleSnapshot"/> 一经构造即由 <see cref="EndingState"/> 持有，此后 MUST NOT 重算。
/// </para>
/// <para>
/// **③ MUST 读累计量** `AchievementState.PeakTotalAssets`（条款 2）：总资产的算式唯一出处是
/// <c>src/KFL.Rules/Economy/TotalAssetsCalculator.cs</c> 的 `Of`，本类 MUST NOT 重算「月末总资产」。
/// </para>
/// <para>
/// **④⑤ MUST 遍历全量** <c>Family.Members</c>（含已亡与外嫁归档；条款 3），MUST NOT 只数在册。
/// ⑦ 是次序敏感的 `PersonId` 序列、**MUST NOT 复制 `Person`**（条款 4 / R-09）。
/// </para>
/// </remarks>
public static class ChronicleBuilder
{
    /// <summary>算定编年史七项。</summary>
    /// <param name="state">当前游戏状态（读 `Family`、`CurrentDate`、`Achievements`）。</param>
    /// <returns>冻结用的编年史快照。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 <c>null</c>。</exception>
    public static ChronicleSnapshot Build(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var members = state.Family.Members;
        var genealogy = new List<PersonId>(members.Count);

        // 起始值：空族谱 ⇒ Generations = 0、HighestDegree = 白身（零值；不存在 DegreeLevel.None，见 E-13）。
        var maxGeneration = -1;
        var highestDegree = DegreeLevel.BaiShen;
        var totalJinShi = 0;

        foreach (var person in members)
        {
            genealogy.Add(person.Id);

            if (person.Generation > maxGeneration)
            {
                maxGeneration = person.Generation;
            }

            if (person.CurrentDegree > highestDegree)
            {
                highestDegree = person.CurrentDegree;
            }

            foreach (var record in person.DegreeHistory)
            {
                if (record.Level == DegreeLevel.JinShi)
                {
                    totalJinShi++;      // 按**人**计，不按次（同人多次登第只算一人）。
                    break;
                }
            }
        }

        return new ChronicleSnapshot
        {
            SurvivedYears = state.CurrentDate.Year - 1,     // ① 整数年差，不是「推进过年数」（条款 5/7）。
            Generations = maxGeneration + 1,                // ② 辈分 0 基准 ⇒ 最大 Generation + 1。
            PeakTotalAssets = state.Achievements.PeakTotalAssets,   // ③ 累计量，不重算。
            HighestDegree = highestDegree,                 // ④ 全量成员最大功名。
            TotalJinShi = totalJinShi,                     // ⑤ 按人去重。
            AverageLifespan = TukeyTrimean.Of(state.Achievements.Lifespans),   // ⑥ 三均值。
            Genealogy = genealogy,                         // ⑦ 次序敏感，含归档。
        };
    }
}
