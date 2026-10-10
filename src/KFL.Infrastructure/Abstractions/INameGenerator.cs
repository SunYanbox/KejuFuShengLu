using KFL.Core.Enums;

namespace KFL.Infrastructure.Abstractions;

/// <summary>
/// 姓名来源接缝（规格书 §12.4；§2 的接口清单；FR-009）。
/// </summary>
/// <remarks>
/// <para>
/// **只取名**：姓氏由家族决定（开局由玩家给定或随机取一个），名由本接缝按性别给出。
/// 规则层（<c>KFL.Rules</c>）只依赖本抽象，MUST NOT 引用任何具体实现或 Bogus 类型。
/// </para>
/// <para>
/// **全部随机 MUST 经注入的 <see cref="IRandomService"/>**（章程原则 IV）：
/// 实现 MUST NOT 触碰任何全局随机源，也 MUST NOT 依赖 Bogus 的全局种子
/// （否则「同种子 → 同姓名」不再成立，FR-011）。
/// </para>
/// </remarks>
public interface INameGenerator
{
    /// <summary>取一个姓氏（规格书 §12.4「随机姓氏」）。</summary>
    /// <returns>姓氏，MUST 非空。</returns>
    string NextSurname();

    /// <summary>按性别取一个名（**只取名**；姓由家族姓氏决定）。</summary>
    /// <param name="gender">成员性别。</param>
    /// <returns>名，MUST 非空。</returns>
    string NextGivenName(Gender gender);
}
