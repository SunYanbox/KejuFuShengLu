namespace KFL.Core.Enums;

/// <summary>
/// 一次「连坐」对某名亲属的**结果分支**（规格书 §7.4；契约十六 §6.2）。
/// </summary>
/// <remarks>
/// <para>
/// 三个分支**互斥**：无功名且无官职 ⇒ 处死；有功名且无官职 ⇒ 降一级功名；
/// 有官职 ⇒ 受「全族禁考禁升 48 月」覆盖（Q3 = A：无家族级独立载体）。
/// </para>
/// <para>
/// **无 <c>None</c> 零值**：连坐事实只在**真的发生了后果**时追加一行——
/// 「免死例外」的孩子不产生任何 <see cref="CollectiveOutcome"/> 记录（契约十六 §6.3），
/// 故「无后果」由**记录不存在**表达。取值从 <c>1</c> 起，使
/// <c>default(CollectiveOutcome)</c> 不会被误读成某个具名分支。
/// </para>
/// </remarks>
public enum CollectiveOutcome
{
    /// <summary>处死（无功名 ∧ 无官职：置已亡、归档、进族谱，§7.4）。</summary>
    Executed = 1,

    /// <summary>降一级功名（有功名 ∧ 无官职：进士 → 贡士 → 举人 → 白身）。</summary>
    DegreeDemoted = 2,

    /// <summary>受「全族禁考禁升 48 月」覆盖（有官职者）。</summary>
    FamilyWideBan = 3,
}
