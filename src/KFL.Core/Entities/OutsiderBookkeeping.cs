using KFL.Core.ValueObjects;

namespace KFL.Core.Entities;

/// <summary>
/// <see cref="Family"/> 的两处外来者簿记的**瞬时快照**（004-save-system，R-03）。
/// </summary>
/// <remarks>
/// <para>
/// 两处簿记都是「外来者辈分还能不能再变」的判据（规格书 §4.4）：
/// </para>
/// <list type="bullet">
/// <item><description>
/// <see cref="Settled"/>——辈分**已由家族指定（落定）**者：一经落定，MUST NOT 重复指定
/// （<c>Family.SetOutsiderGeneration</c> 的 <c>_settledOutsiders</c>）。
/// </description></item>
/// <item><description>
/// <see cref="MarriageAligned"/>——**首次家族内成婚的对齐已用掉**者：此后其辈分只还能
/// 落定一次，成婚对齐不再生效（<c>Family._marriageAlignedOutsiders</c>）。
/// </description></item>
/// </list>
/// <para>
/// 本记录只承载**数据**，不承载任何校验：复原路径经
/// <see cref="Family.RestoreOutsiderBookkeeping"/> 直接落值，一致性由存档层的
/// <c>SaveStateValidator</c> 负责。
/// </para>
/// </remarks>
/// <param name="Settled">辈分已落定的外来者。</param>
/// <param name="MarriageAligned">首次家族内成婚对齐已用掉的外来者。</param>
internal sealed record OutsiderBookkeeping(
    IReadOnlyCollection<PersonId> Settled,
    IReadOnlyCollection<PersonId> MarriageAligned);
