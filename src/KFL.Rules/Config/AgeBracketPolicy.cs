using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Rules.Config;

/// <summary>
/// 年龄档归属（规格书 §5.1、§4.3；data-model §4.1；research R-06）。
/// </summary>
/// <remarks>
/// <para>
/// 口径**逐字**取自规格书 §5.1：成年 = 男满 <see cref="MaleAdulthoodAge"/>、
/// 女满 <see cref="FemaleAdulthoodAge"/>（生日当月生效）；未成年一律
/// <see cref="AgeBracket.Child"/>；已成年且 ≤ <see cref="YouthUpperAge"/> 为
/// <see cref="AgeBracket.Youth"/>；≥ <see cref="ElderLowerAge"/> 为
/// <see cref="AgeBracket.Elder"/>；其余 <see cref="AgeBracket.Adult"/>。
/// </para>
/// <para>
/// 边界数值只在本类声明一次（SC-008）；判定输入是性别与
/// <see cref="GameDate.AgeInYearsAt"/>（其语义已是「生日当月即计入」），故**生日当月即转档**。
/// </para>
/// </remarks>
public static class AgeBracketPolicy
{
    /// <summary>男性成年年龄（周岁，含当月）。</summary>
    public const int MaleAdulthoodAge = 12;

    /// <summary>女性成年年龄（周岁，含当月）。</summary>
    public const int FemaleAdulthoodAge = 14;

    /// <summary>青年档年龄上界（含）。</summary>
    public const int YouthUpperAge = 18;

    /// <summary>老人档年龄下界（含）。</summary>
    public const int ElderLowerAge = 60;

    /// <summary>按性别判定是否已成年（规格书 §4.3，生日当月生效）。</summary>
    /// <param name="gender">性别。</param>
    /// <param name="age">已满周岁数。</param>
    /// <returns>已成年为 <c>true</c>。</returns>
    /// <exception cref="ArgumentOutOfRangeException">年龄为负。</exception>
    public static bool IsAdult(Gender gender, int age)
    {
        if (age < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(age), age, "年龄 MUST >= 0。");
        }

        return age >= (gender == Gender.Male ? MaleAdulthoodAge : FemaleAdulthoodAge);
    }

    /// <summary>按性别与周岁数取年龄档。</summary>
    /// <param name="gender">性别。</param>
    /// <param name="age">已满周岁数。</param>
    /// <returns>年龄档。</returns>
    /// <exception cref="ArgumentOutOfRangeException">年龄为负，或性别不在已登记取值内。</exception>
    public static AgeBracket Of(Gender gender, int age)
    {
        if (!Enum.IsDefined(gender))
        {
            throw new ArgumentOutOfRangeException(
                nameof(gender), gender, "未登记的性别（规格书 §4.1 共两种）。");
        }

        if (!IsAdult(gender, age))
        {
            return AgeBracket.Child;
        }

        return age switch
        {
            _ when age >= ElderLowerAge => AgeBracket.Elder,
            _ when age <= YouthUpperAge => AgeBracket.Youth,
            _ => AgeBracket.Adult,
        };
    }

    /// <summary>取成员在指定年月的年龄档（生日当月即转档）。</summary>
    /// <param name="person">成员。</param>
    /// <param name="date">查询年月。</param>
    /// <returns>年龄档。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 为 <c>null</c>。</exception>
    public static AgeBracket Of(Person person, GameDate date)
    {
        ArgumentNullException.ThrowIfNull(person);

        return Of(person.Gender, person.AgeAt(date));
    }
}
