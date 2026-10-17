namespace KFL.Core.Enums;

/// <summary>
/// 当月死亡事实的死因（规格书 §4.1~§4.4、§9.4；007 的 R-04）。
/// </summary>
/// <remarks>
/// <para>
/// **只**出现在当月事实段 <c>LifecycleSnapshot.Deaths</c> 的 <c>DeathFact</c> 里。
/// 它 MUST NOT 落 <see cref="Entities.Person"/> 的任何字段，MUST NOT 落账本条目
/// （<see cref="ValueObjects.LedgerEntry"/> 的形状是
/// <c>(GameDate, PersonId?, LedgerCategory, Money)</c>，没有承载死因的位置）。
/// </para>
/// <para>
/// **明确不含**：006 的「处死」路径（<c>KFL.Rules/Punishment/</c> 只置
/// <c>StatusFlag.Deceased</c>）MUST NOT 为本枚举补取值（FR-033）。
/// </para>
/// </remarks>
public enum DeathCause
{
    /// <summary>寿终：⑧-f 的天命寿数到龄（<c>AgeAt(date) &gt;= Lifespan</c>）。</summary>
    OldAge = 0,

    /// <summary>病亡：⑧-c 的患病扣减把体质扣到 <c>0</c>。</summary>
    Illness = 1,

    /// <summary>饥馑：⑧-e 的严重期体质扣减归零，或该子步的每月死亡判定命中。</summary>
    Famine = 2,

    /// <summary>夭折：⑧-f 的 <c>AgeAt &lt; 3</c> 生日月判定命中。</summary>
    Infancy = 3,

    /// <summary>分娩死亡：⑧-b 的产妇死亡判定命中（新生儿照常出生）。</summary>
    Childbirth = 4,
}
