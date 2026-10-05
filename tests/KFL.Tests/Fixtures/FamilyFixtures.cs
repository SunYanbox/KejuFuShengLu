using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Tests.Fixtures;

/// <summary>
/// **五类夹具**（T022）：多代同堂（≥3 代）、有配偶、有子女、娶入配偶、买来的旁系。
/// </summary>
/// <remarks>
/// <para>
/// 夹具**只用 <see cref="Person"/> / <see cref="Family"/> 的公开 API 构造**，
/// MUST NOT 走反射绕过不变量校验——否则测的不是真实约束。
/// </para>
/// <para>
/// 标识一律由 <see cref="Id"/> 确定性生成；本目录在架构守卫 G-07 的扫描范围内
/// （契约一 §2.1），因此不可使用全局随机标识源。
/// </para>
/// </remarks>
// 夹具的成员标识一律确定性生成，MUST NOT 使用 Guid.NewGuid() 一类的全局随机源。 // arch-guard:allow 说明性注释，本行不执行任何环境访问
public static class FamilyFixtures
{
    /// <summary>确定性标识生成器：同一下标永远得到同一个 <see cref="PersonId"/>，且永不为空。</summary>
    /// <param name="index">从 1 起的下标。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> &lt;= 0。</exception>
    public static PersonId Id(int index)
    {
        if (index <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "夹具标识下标从 1 起。");
        }

        var bytes = new byte[16];
        bytes[0] = (byte)(index & 0xFF);
        bytes[1] = (byte)((index >> 8) & 0xFF);
        bytes[2] = (byte)((index >> 16) & 0xFF);
        bytes[3] = (byte)((index >> 24) & 0xFF);
        bytes[15] = 0xA5;
        return new PersonId(new Guid(bytes));
    }

    /// <summary>四项天赋全为给定值的 <see cref="TalentSet"/>。</summary>
    /// <param name="value">四项的取值。</param>
    public static TalentSet Talents(int value) => new(value, value, value, value);

    /// <summary>构造一名尚未加入任何家族的成员。</summary>
    /// <param name="idIndex">标识下标（见 <see cref="Id"/>）。</param>
    /// <param name="name">姓名。</param>
    /// <param name="gender">性别。</param>
    /// <param name="birthYear">出生年。</param>
    /// <param name="birthMonth">出生月。</param>
    /// <param name="generation">辈分。</param>
    /// <param name="fatherId">父亲引用。</param>
    /// <param name="motherId">母亲引用。</param>
    /// <param name="lifespan">天命寿数。</param>
    public static Person NewPerson(
        int idIndex,
        string name,
        Gender gender,
        int birthYear,
        int birthMonth,
        int generation,
        PersonId? fatherId = null,
        PersonId? motherId = null,
        int lifespan = 60) =>
        new(
            Id(idIndex),
            name,
            gender,
            new GameDate(birthYear, birthMonth),
            Talents(50),
            lifespan,
            generation,
            fatherId,
            motherId);

    /// <summary>夹具一：**多代同堂**（四代，含娶入配偶与子女），可向上向下遍历。</summary>
    public static MultiGenerationFixture MultiGeneration()
    {
        var family = new Family("陈");

        var founder = family.AddFoundingMember(
            NewPerson(1, "陈祖", Gender.Male, 1, 1, generation: 0, lifespan: 70));
        var foundress = family.AddFoundingMember(
            NewPerson(2, "陈祖妻", Gender.Female, 2, 3, generation: 0, lifespan: 68));
        family.Marry(founder.Id, foundress.Id);

        var father = family.AddChild(
            NewPerson(3, "陈父", Gender.Male, 20, 5, generation: 1, fatherId: founder.Id, motherId: foundress.Id));
        var mother = family.AddOutsider(
            NewPerson(4, "陈母", Gender.Female, 21, 7, generation: 1));
        family.Marry(father.Id, mother.Id);

        var son = family.AddChild(
            NewPerson(5, "陈子", Gender.Male, 40, 2, generation: 2, fatherId: father.Id, motherId: mother.Id));
        var daughter = family.AddChild(
            NewPerson(6, "陈女", Gender.Female, 42, 9, generation: 2, fatherId: father.Id, motherId: mother.Id));
        var daughterInLaw = family.AddOutsider(
            NewPerson(7, "陈媳", Gender.Female, 41, 4, generation: 2));
        family.Marry(son.Id, daughterInLaw.Id);

        var grandson = family.AddChild(
            NewPerson(8, "陈孙", Gender.Male, 60, 6, generation: 3, fatherId: son.Id, motherId: daughterInLaw.Id));
        var granddaughter = family.AddChild(
            NewPerson(9, "陈孙女", Gender.Female, 62, 8, generation: 3, fatherId: son.Id, motherId: daughterInLaw.Id));

        family.SetHead(founder.Id);

        return new MultiGenerationFixture
        {
            Family = family,
            FounderId = founder.Id,
            FoundressId = foundress.Id,
            FatherId = father.Id,
            MotherId = mother.Id,
            SonId = son.Id,
            DaughterId = daughter.Id,
            DaughterInLawId = daughterInLaw.Id,
            GrandsonId = grandson.Id,
            GranddaughterId = granddaughter.Id,
        };
    }

    /// <summary>夹具二：**有配偶**（一对双向配偶）。</summary>
    public static CoupleFixture Couple()
    {
        var family = new Family("林");

        var husband = family.AddFoundingMember(
            NewPerson(101, "林郎", Gender.Male, 1, 1, generation: 0));
        var wife = family.AddOutsider(
            NewPerson(102, "林娘", Gender.Female, 3, 2, generation: 0));
        family.Marry(husband.Id, wife.Id);
        family.SetHead(husband.Id);

        return new CoupleFixture { Family = family, HusbandId = husband.Id, WifeId = wife.Id };
    }

    /// <summary>夹具三：**有子女**（父母的 <c>FatherId</c> / <c>MotherId</c> 指向正确）。</summary>
    public static ParentChildFixture ParentChild()
    {
        var family = new Family("苏");

        var father = family.AddFoundingMember(
            NewPerson(201, "苏父", Gender.Male, 1, 1, generation: 0));
        var mother = family.AddOutsider(
            NewPerson(202, "苏母", Gender.Female, 2, 2, generation: 0));
        family.Marry(father.Id, mother.Id);

        var elderSon = family.AddChild(
            NewPerson(203, "苏长子", Gender.Male, 20, 3, generation: 1, fatherId: father.Id, motherId: mother.Id));
        var youngerDaughter = family.AddChild(
            NewPerson(204, "苏幼女", Gender.Female, 22, 7, generation: 1, fatherId: father.Id, motherId: mother.Id));
        family.SetHead(father.Id);

        return new ParentChildFixture
        {
            Family = family,
            FatherId = father.Id,
            MotherId = mother.Id,
            ElderSonId = elderSon.Id,
            YoungerDaughterId = youngerDaughter.Id,
        };
    }

    /// <summary>夹具四：**娶入配偶**——家族内无父母，辈分等于其家族内配偶的辈分（规格书 §4.4）。</summary>
    public static MarriedInFixture MarriedIn()
    {
        var family = new Family("赵");

        var founder = family.AddFoundingMember(
            NewPerson(301, "赵祖", Gender.Male, 1, 1, generation: 0));
        var son = family.AddChild(
            NewPerson(302, "赵父", Gender.Male, 20, 1, generation: 1, fatherId: founder.Id));

        // 娶入配偶的辈分 = 其家族内配偶的辈分（此处为 1，而非常见的 0）。
        var spouse = family.AddOutsider(
            NewPerson(303, "赵母", Gender.Female, 21, 6, generation: son.Generation));
        family.Marry(son.Id, spouse.Id);
        family.SetHead(founder.Id);

        return new MarriedInFixture
        {
            Family = family,
            FounderId = founder.Id,
            SpouseHostId = son.Id,
            MarriedInId = spouse.Id,
        };
    }

    /// <summary>
    /// 夹具五：**买来的旁系**——家族内无父母、辈分 = 家主辈分 + 1（开局家主治下为 1），
    /// 且终身未婚仍可按辈分在家族树中定位（规格书 §4.4、§9.5；spec Edge Case）。
    /// </summary>
    public static BoughtCollateralFixture BoughtCollateral()
    {
        var family = new Family("周");

        var head = family.AddFoundingMember(
            NewPerson(401, "周家主", Gender.Male, 1, 1, generation: 0));
        family.SetHead(head.Id);

        var collateral = family.AddOutsider(
            NewPerson(402, "周旁系", Gender.Male, 30, 4, generation: family.BoughtCollateralGeneration));

        return new BoughtCollateralFixture
        {
            Family = family,
            HeadId = head.Id,
            CollateralId = collateral.Id,
        };
    }
}

/// <summary>多代同堂夹具的成员索引。</summary>
public sealed record MultiGenerationFixture
{
    /// <summary>家族。</summary>
    public required Family Family { get; init; }

    /// <summary>第一代（开局成员，辈分 0）。</summary>
    public required PersonId FounderId { get; init; }

    /// <summary>第一代配偶（开局成员，辈分 0）。</summary>
    public required PersonId FoundressId { get; init; }

    /// <summary>第二代（辈分 1）。</summary>
    public required PersonId FatherId { get; init; }

    /// <summary>第二代娶入配偶（辈分 1）。</summary>
    public required PersonId MotherId { get; init; }

    /// <summary>第三代（辈分 2）。</summary>
    public required PersonId SonId { get; init; }

    /// <summary>第三代女儿（辈分 2）。</summary>
    public required PersonId DaughterId { get; init; }

    /// <summary>第三代娶入配偶（辈分 2）。</summary>
    public required PersonId DaughterInLawId { get; init; }

    /// <summary>第四代（辈分 3）。</summary>
    public required PersonId GrandsonId { get; init; }

    /// <summary>第四代女儿（辈分 3）。</summary>
    public required PersonId GranddaughterId { get; init; }
}

/// <summary>有配偶夹具的成员索引。</summary>
public sealed record CoupleFixture
{
    /// <summary>家族。</summary>
    public required Family Family { get; init; }

    /// <summary>丈夫。</summary>
    public required PersonId HusbandId { get; init; }

    /// <summary>妻子。</summary>
    public required PersonId WifeId { get; init; }
}

/// <summary>有子女夹具的成员索引。</summary>
public sealed record ParentChildFixture
{
    /// <summary>家族。</summary>
    public required Family Family { get; init; }

    /// <summary>父亲。</summary>
    public required PersonId FatherId { get; init; }

    /// <summary>母亲。</summary>
    public required PersonId MotherId { get; init; }

    /// <summary>长子。</summary>
    public required PersonId ElderSonId { get; init; }

    /// <summary>幼女。</summary>
    public required PersonId YoungerDaughterId { get; init; }
}

/// <summary>娶入配偶夹具的成员索引。</summary>
public sealed record MarriedInFixture
{
    /// <summary>家族。</summary>
    public required Family Family { get; init; }

    /// <summary>开局家主。</summary>
    public required PersonId FounderId { get; init; }

    /// <summary>家族内的一方。</summary>
    public required PersonId SpouseHostId { get; init; }

    /// <summary>娶入的一方（外来者）。</summary>
    public required PersonId MarriedInId { get; init; }
}

/// <summary>买来的旁系夹具的成员索引。</summary>
public sealed record BoughtCollateralFixture
{
    /// <summary>家族。</summary>
    public required Family Family { get; init; }

    /// <summary>家主。</summary>
    public required PersonId HeadId { get; init; }

    /// <summary>买来的旁系。</summary>
    public required PersonId CollateralId { get; init; }
}
