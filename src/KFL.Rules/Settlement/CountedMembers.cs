using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Rules.Settlement;

/// <summary>
/// 成员计口口径的**纯函数**（规格书 §5.1、§12.1；FR-029/FR-030；契约三 §3）。
/// </summary>
/// <remarks>
/// <para>
/// **两个口径 MUST 各自独立给出、MUST NOT 互相顶替**：
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>在册</b> = 未亡且未外嫁（含服刑、待阙）。它是绝嗣判定与家族列表的口径。
/// </description></item>
/// <item><description>
/// <b>计口</b> = 在册**且未服刑**。它是生活费与收入的口径；**待阙照常计入**（§8.2 无俸、靠积蓄）。
/// </description></item>
/// <item><description>
/// <b>可指派 / 收入人力</b> = 计口 **∧ 已成年**（男满 12 / 女满 14，生日当月生效，**无年龄上限**，
/// 故青年与老人均可被指派、未成年不可，E-16）。
/// </description></item>
/// </list>
/// <para>
/// 本类只读家族与年月，MUST NOT 触碰资金池，也 MUST NOT 依赖任何规则数值之外的输入。
/// </para>
/// </remarks>
public static class CountedMembers
{
    /// <summary>是否**在册**（未亡且未外嫁，含服刑与待阙）。</summary>
    /// <param name="person">成员。</param>
    /// <returns>在册为 <c>true</c>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 为 <c>null</c>。</exception>
    public static bool IsRegistered(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        return (person.Status & (StatusFlag.Deceased | StatusFlag.MarriedOut)) == StatusFlag.None;
    }

    /// <summary>是否**计口**（在册且未服刑）。</summary>
    /// <param name="person">成员。</param>
    /// <returns>计口为 <c>true</c>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 为 <c>null</c>。</exception>
    public static bool IsCounted(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        return IsRegistered(person) && !person.Status.HasFlag(StatusFlag.ServingSentence);
    }

    /// <summary>取**在册**成员（含服刑与待阙，不含已亡与外嫁）。</summary>
    /// <param name="family">家族。</param>
    /// <returns>在册成员。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="family"/> 为 <c>null</c>。</exception>
    public static IReadOnlyList<Person> Registered(Family family)
    {
        ArgumentNullException.ThrowIfNull(family);

        return family.RegisteredMembers.ToList().AsReadOnly();
    }

    /// <summary>取**计口**成员（在册且未服刑）——生活费与收入的成员集合。</summary>
    /// <param name="family">家族。</param>
    /// <returns>计口成员，按家族加入顺序。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="family"/> 为 <c>null</c>。</exception>
    public static IReadOnlyList<Person> Counted(Family family)
    {
        ArgumentNullException.ThrowIfNull(family);

        return family.Members.Where(IsCounted).ToList().AsReadOnly();
    }

    /// <summary>
    /// 取**可指派 / 收入人力**集合 = 计口 ∧ 已成年（E-16；含青年与老人，无年龄上限）。
    /// </summary>
    /// <param name="family">家族。</param>
    /// <param name="date">判定年月（年龄档边界以它为准）。</param>
    /// <returns>可指派成员，按家族加入顺序。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="family"/> 为 <c>null</c>。</exception>
    public static IReadOnlyList<Person> Assignable(Family family, GameDate date)
    {
        ArgumentNullException.ThrowIfNull(family);

        return family.Members
            .Where(person => IsCounted(person)
                && Config.AgeBracketPolicy.IsAdult(person.Gender, person.AgeAt(date)))
            .ToList()
            .AsReadOnly();
    }
}
