// SaveStateValidator 第 16~22 条：成就、终局与编年史一致性。

using KFL.Core.Entities;
using KFL.Core.Enums;

namespace KFL.Infrastructure.Save.Serialization;

public static partial class SaveStateValidator
{
    /// <summary>第 16 条：<c>Completed</c> 内的键 <c>(Achievement, Difficulty)</c> MUST 唯一。</summary>
    /// <remarks>
    /// 同一成就的不同难度是**两条**记录（SC-014），故唯一性判据是二元键，不是只比成就编号。
    /// </remarks>
    private static void EnsureAchievementKeysUnique(AchievementState achievements)
    {
        var seen = new HashSet<(AchievementId, Difficulty)>();

        foreach (var mark in achievements.Completed)
        {
            if (!seen.Add((mark.Achievement, mark.Difficulty)))
            {
                throw Invalid(
                    $"完成记录的键（{mark.Achievement} / {mark.Difficulty}）重复（R-04 第 16 条）。");
            }
        }
    }

    /// <summary>第 17 条：<c>PendingUnlocks</c> 内键唯一，且每个键都 MUST 出现在 <c>Completed</c> 中。</summary>
    /// <remarks>
    /// 队列是「已完成但还没写进全局存档」的子集，故 <c>PendingUnlocks ⊆ Completed</c>；
    /// 队列本身同样不许重复（重复会让落盘写两条同样的记录）。
    /// </remarks>
    private static void EnsurePendingUnlocksSubsetOfCompleted(AchievementState achievements)
    {
        var completed = new HashSet<(AchievementId, Difficulty)>();

        foreach (var mark in achievements.Completed)
        {
            completed.Add((mark.Achievement, mark.Difficulty));
        }

        var seen = new HashSet<(AchievementId, Difficulty)>();

        foreach (var unlock in achievements.PendingUnlocks)
        {
            var key = (unlock.Achievement, unlock.Difficulty);

            if (!seen.Add(key))
            {
                throw Invalid(
                    $"待落盘队列的键（{unlock.Achievement} / {unlock.Difficulty}）重复（R-04 第 17 条）。");
            }

            if (!completed.Contains(key))
            {
                throw Invalid(
                    $"待落盘队列的键（{unlock.Achievement} / {unlock.Difficulty}）不在完成记录中（R-04 第 17 条）。");
            }
        }
    }

    /// <summary>第 18 条：寿命序列条数 MUST 等于已亡成员数，且每项 MUST <c>&gt;= 0</c>。</summary>
    /// <remarks>
    /// <para>
    /// data-model §6 不变量 5（**经勘误 E-21 修订**）：<c>Lifespans.Count &lt;= Members.Count(p =&gt; p.Deceased)</c>。
    /// </para>
    /// <para>
    /// 取**上界**而非相等：序列只吸收「已登记死亡」（⑧ 的 <c>LifecycleSnapshot.Deaths</c> ⇒
    /// <c>AchievementCounters.AbsorbMonthFacts</c> ③），而 006 的 <c>PunishmentImposer.Execute</c>
    /// 在 <c>DeathRegister</c> **之外**置 <c>StatusFlag.Deceased</c>（处决与株连家属），
    /// 既不产生 <c>DeathFact</c>（<c>DeathCause</c> 明确不补「处决」取值）也不记享年。
    /// 故相等是产品路径上**不可达**的强断言，只保留「不得多记」这一半。
    /// </para>
    /// <para>
    /// 负值在 <c>AppendLifespan</c> 已被拒，这里拦的是盘上被改坏的整数。
    /// </para>
    /// </remarks>
    /// <param name="achievements">档内成就累计量。</param>
    /// <param name="members">全部成员（含归档者）。</param>
    private static void EnsureLifespansMatchDeceased(
        AchievementState achievements,
        IReadOnlyCollection<Person> members)
    {
        foreach (var age in achievements.Lifespans)
        {
            if (age < 0)
            {
                throw Invalid($"寿命序列含负值 {age}（R-04 第 18 条）。");
            }
        }

        var deceased = 0;

        foreach (var member in members)
        {
            if (member.Status.HasFlag(StatusFlag.Deceased))
            {
                deceased++;
            }
        }

        if (achievements.Lifespans.Count > deceased)
        {
            throw Invalid(
                $"寿命序列有 {achievements.Lifespans.Count} 项，而已亡成员只有 {deceased} 名（R-04 第 18 条）。");
        }
    }

    /// <summary>第 19 条：四个累计量 MUST 非负。</summary>
    /// <remarks>
    /// 覆盖二进制侧的越界整数。四项都是**只增不减**的累计量，负值不可能由产品路径产生。
    /// </remarks>
    private static void EnsureAchievementCountersNonNegative(AchievementState achievements)
    {
        if (achievements.SuccessfulBribeCount < 0)
        {
            throw Invalid($"累计成功行贿次数为负（{achievements.SuccessfulBribeCount}，R-04 第 19 条）。");
        }

        if (achievements.BirthCount < 0)
        {
            throw Invalid($"累计出生人数为负（{achievements.BirthCount}，R-04 第 19 条）。");
        }

        if (achievements.DebugWakeCount < 0)
        {
            throw Invalid($"调试唤醒累计次数为负（{achievements.DebugWakeCount}，R-04 第 19 条）。");
        }

        if (achievements.PeakTotalAssets.IsNegative)
        {
            throw Invalid($"历史总资产峰值为负（{achievements.PeakTotalAssets.Wen}，R-04 第 19 条）。");
        }
    }

    /// <summary>第 20 条：<c>IsEnded ⇔ (EndedAt.HasValue ∧ Chronicle is not null)</c>。</summary>
    /// <remarks>
    /// data-model §6 不变量 9。<c>EndingState</c> 的 <c>End</c> 与复原通道都会整体赋值，
    /// 故「三元不齐备」只可能来自被改坏的档。
    /// </remarks>
    private static void EnsureEndingTripleConsistent(EndingState ending)
    {
        var hasDate = ending.EndedAt.HasValue;
        var hasChronicle = ending.Chronicle is not null;

        if (hasDate != hasChronicle)
        {
            throw Invalid(
                $"终局三元不齐备：EndedAt 非空={hasDate}、Chronicle 非空={hasChronicle}（R-04 第 20 条）。");
        }

        if (ending.IsEnded != (hasDate && hasChronicle))
        {
            throw Invalid(
                $"IsEnded={ending.IsEnded} 与「EndedAt 非空 ∧ Chronicle 非空」={hasDate && hasChronicle} 不一致（R-04 第 20 条）。");
        }
    }

    /// <summary>第 21 条：<c>IsEnded</c> ⇒ 家族再无任何**在册**成员。</summary>
    /// <remarks>
    /// 终局的定义就是「绝嗣」（<c>ExtinctionJudge.IsExtinct</c>：<c>RegisteredMembers.Count == 0</c>），
    /// 故已结束却还有在册成员是不可能状态。反向**不成立**：在册为零时未必已置位终局
    /// （判定发生在月份推进的第 ② 步），所以这里 MUST NOT 写成双向。
    /// </remarks>
    /// <param name="state">已复原的状态。</param>
    private static void EnsureEndedImpliesNoRegisteredMembers(GameState state)
    {
        if (!state.Ending.IsEnded)
        {
            return;
        }

        var registered = state.Family.RegisteredMembers.Count;

        if (registered != 0)
        {
            throw Invalid($"家族已绝嗣却仍有 {registered} 名在册成员（R-04 第 21 条）。");
        }
    }

    /// <summary>
    /// 第 22 条：编年史宗谱**次序敏感**逐元素等于成员 Id 序列，且存续年数等于结束年 − 1。
    /// </summary>
    /// <remarks>
    /// data-model §6 不变量 8/9。比较 MUST 按位置逐项进行，**MUST NOT** 用集合相等——
    /// 宗谱的次序本身是产品信息（契约二十四「条款 34」）。
    /// </remarks>
    /// <param name="state">已复原的状态。</param>
    private static void EnsureChronicleMatchesFamily(GameState state)
    {
        if (state.Ending.Chronicle is not { } chronicle || state.Ending.EndedAt is not { } endedAt)
        {
            return;
        }

        var members = state.Family.Members;

        if (chronicle.Genealogy.Count != members.Count)
        {
            throw Invalid(
                $"编年史宗谱有 {chronicle.Genealogy.Count} 项，而成员（含归档）有 {members.Count} 名（R-04 第 22 条）。");
        }

        var index = 0;

        foreach (var member in members)
        {
            if (chronicle.Genealogy[index] != member.Id)
            {
                throw Invalid(
                    $"编年史宗谱第 {index} 项 {chronicle.Genealogy[index]} 与同序成员 {member.Id} 不一致（R-04 第 22 条：MUST NOT 用集合相等）。");
            }

            index++;
        }

        if (chronicle.SurvivedYears != endedAt.Year - 1)
        {
            throw Invalid(
                $"编年史存续年数 {chronicle.SurvivedYears} ≠ 结束年 {endedAt.Year} − 1（R-04 第 22 条）。");
        }
    }
}
