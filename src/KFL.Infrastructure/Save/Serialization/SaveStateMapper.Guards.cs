// SaveStateMapper 的枚举守卫：拒绝本版本未定义的枚举整数。

using KFL.Core.Enums;

namespace KFL.Infrastructure.Save.Serialization;

public static partial class SaveStateMapper
{
    /// <summary>取必需子对象；为 <c>null</c> 即结构错误（缺成员，或显式写了 <c>null</c>）。</summary>
    /// <summary>
    /// 契约十 §3 条款 1 / data-model §3.10：枚举 MUST 是本版本定义的具名成员，越界整数 ⇒ 结构不符（第 ⑦ 步）。
    /// </summary>
    /// <remarks>
    /// 二进制把枚举写成**原值整数**，越界值读得出来（JSON 侧不认得的**名字**在第 ⑤ 步就已失败，属
    /// <see cref="SaveFileErrorKind.Malformed"/>）——若不复核，<c>(Difficulty)999</c> 这类值会静默变成
    /// 领域里的越界枚举，等于为序列化放宽不变量（FR-020）。
    /// <c>StatusFlag</c> 是位掩码，另有自己的位集判据（见 <see cref="Build"/> 第 ③ 步）。
    /// </remarks>
    /// <param name="dto">已通过格式层读出的 DTO。</param>
    private static void EnsureEnumsDefined(SaveStateDto dto)
    {
        EnsureDefined(dto.Difficulty, "difficulty");
        EnsureDefined(dto.PendingDifficulty, "pendingDifficulty");
        EnsureDefined(dto.Origin, "origin");

        foreach (var member in Required(Required(dto.Family, nameof(dto.Family)).Members, "family.members"))
        {
            EnsureDefined(member.Gender, "gender");
            EnsureDefined(member.Occupation, "occupation");
            EnsureDefined(member.EntryTrack, "entryTrack");

            foreach (var degree in Required(member.DegreeHistory, "degreeHistory"))
            {
                EnsureDefined(degree.Level, "level");
                EnsureDefined(degree.Placement, "placement");
                EnsureDefined(degree.Cause, "cause");
                EnsureDefined(degree.ImperialClass, "imperialClass");
            }
        }

        var economy = Required(dto.Economy, nameof(dto.Economy));
        EnsureDefined(economy.LivingStandard, "livingStandard");
        EnsureDefined(economy.PendingLivingStandard, "pendingLivingStandard");

        foreach (var entry in Required(economy.Ledger, "ledger"))
        {
            EnsureDefined(entry.Category, "category");
        }

        EnsureDefined(Required(economy.Famine, "famine").Stage, "stage");

        // 008-achievements-extinction：两个新 DTO 的枚举字段同样是**底层整数**，
        // 越界值（二进制侧读得出来）MUST 在进入 Core 之前拒掉。
        var achievements = Required(dto.Achievements, nameof(dto.Achievements));
        foreach (var mark in Required(achievements.Completed, "completed"))
        {
            EnsureDefined((AchievementId)mark.Achievement, "achievement");
            EnsureDefined((Difficulty)mark.Difficulty, "difficulty");
        }

        foreach (var unlock in Required(achievements.PendingUnlocks, "pendingUnlocks"))
        {
            EnsureDefined((AchievementId)unlock.Achievement, "achievement");
            EnsureDefined((Difficulty)unlock.Difficulty, "difficulty");
        }

        if (dto.Ending is { } ending)
        {
            EnsureDefined((DegreeLevel)Required(ending.Chronicle, "ending.chronicle").HighestDegree, "highestDegree");
        }
    }

    /// <summary>值 MUST 是具名枚举成员。</summary>
    /// <param name="value">待检值。</param>
    /// <param name="field">字段名（用线上键名，便于与盘上内容对照）。</param>
    /// <typeparam name="TEnum">枚举类型。</typeparam>
    private static void EnsureDefined<TEnum>(TEnum value, string field)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw Invalid($"字段 {field} 的值 {value} 不是本版本定义的枚举成员（契约十 §3 条款 1）。");
        }
    }

    /// <summary>可空枚举：非空时 MUST 是具名成员。</summary>
    /// <param name="value">待检值。</param>
    /// <param name="field">字段名。</param>
    /// <typeparam name="TEnum">枚举类型。</typeparam>
    private static void EnsureDefined<TEnum>(TEnum? value, string field)
        where TEnum : struct, Enum
    {
        if (value is { } present)
        {
            EnsureDefined(present, field);
        }
    }
}
