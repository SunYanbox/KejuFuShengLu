using KFL.Core.Enums;

namespace KFL.Core.ValueObjects;

/// <summary>
/// 绝嗣结局的编年史快照（规格书 §15:632-641；FR-017；data-model §2.2）。
/// </summary>
/// <remarks>
/// <para>
/// 七项与规格书 §15:632-641 —— 顺序也按规格书。快照在结局触发当刻**冻结**，
/// 此后 MUST NOT 重算（SC-011）：读档后再推进不改写编年史。
/// </para>
/// <para>
/// <see cref="Genealogy"/> 只冻结**次序与成员集合**（`Family.Members` 的 `Id` 序列，含归档），
/// **不复制 `Person`**（R-09）。次序敏感：校验与复原都按逐元素比较，
/// MUST NOT 用集合相等（契约二十三「条款 34」）。
/// </para>
/// <para>
/// <see cref="HighestDegree"/> 取不到任何功名时用 <see cref="DegreeLevel.BaiShen"/>——它是既有
/// `DegreeLevel` 枚举的零值（**不存在** `DegreeLevel.None`；见 `plan.md` E-13）；
/// <see cref="Generations"/> 在零成员时为 `0`（产品路径上不可达，绝嗣前至少有过开局成员）。
/// </para>
/// </remarks>
public sealed record ChronicleSnapshot
{
    /// <summary>① 存续年数 = `CurrentDate.Year − 1`（严格按规格书口径，非「推进过年数」）。</summary>
    public required int SurvivedYears { get; init; }

    /// <summary>② 世代数 = 全量成员的最大 `Generation` + 1。</summary>
    public required int Generations { get; init; }

    /// <summary>③ 历史资产峰值 = `AchievementState.PeakTotalAssets`（累计量，不可重算）。</summary>
    public required Money PeakTotalAssets { get; init; }

    /// <summary>④ 全量成员 `DegreeHistory` 的最高功名；取不到用 <see cref="DegreeLevel.BaiShen"/>。</summary>
    public required DegreeLevel HighestDegree { get; init; }

    /// <summary>⑤ 进士人数（按人去重）。</summary>
    public required int TotalJinShi { get; init; }

    /// <summary>⑥ 平均寿命（三均值 Tukey trimean，保留 3 位小数）。</summary>
    public required decimal AverageLifespan { get; init; }

    /// <summary>⑦ 宗谱 = 全量成员 `Id` 的**次序敏感**序列（含归档），不复制 `Person`。</summary>
    public required IReadOnlyList<PersonId> Genealogy { get; init; }
}
