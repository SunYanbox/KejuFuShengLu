using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Presentation.ViewModels;

/// <summary>
/// 成员详情里的一条关系超链接（玩家反馈 7）：点一下跳到对应成员的详情卡片。
/// </summary>
/// <param name="label">显示文字（形如「父 狄立言」）。</param>
/// <param name="token">命令令牌（形如 <c>select:&lt;guid&gt;</c>）。</param>
public sealed record RelationLink(string Label, string Token);

/// <summary>
/// 亲属关系投影（玩家反馈 3 / 7 / 8）：父母、配偶、子女（嫡长子额外标注）与兄弟姐妹。
/// </summary>
/// <remarks>
/// 「嫡长子」的口径：<b>嫡出</b> = 生母是本成员的配偶或前任配偶（<see cref="Person.SpouseId"/> /
/// <see cref="Person.FormerSpouseIds"/>，非婚生的收养子女不计入）；<b>长子</b> = 这些儿子里出生最早
/// 的一位。没有嫡出儿子时，标注落在最年长的儿子上，避免整行无标记（推断见 <c>推断.md</c> §9）。
/// </remarks>
public static class FamilyRelations
{
    /// <summary>按「父 / 母 / 配偶 / 子女 / 兄弟姐妹」顺序给出可直接点击的关系链接。</summary>
    /// <param name="family">家族（只读）。</param>
    /// <param name="person">中心成员。</param>
    /// <returns>关系链接列表；没有亲属时为空。</returns>
    public static IReadOnlyList<RelationLink> LinksOf(Family family, Person person)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(person);

        var links = new List<RelationLink>();

        Add(links, "父", family, person.FatherId);
        Add(links, "母", family, person.MotherId);
        Add(links, "配偶", family, person.SpouseId);

        foreach (var child in Children(family, person))
        {
            Add(links, IsEldestSon(family, person, child) ? "嫡长子" : "子女", family, child.Id);
        }

        foreach (var sibling in Siblings(family, person))
        {
            Add(links, "兄弟姐妹", family, sibling.Id);
        }

        return links;
    }

    /// <summary>本成员的子女（按出生日期、再按 Id 排序；含已故与已外嫁）。</summary>
    public static IReadOnlyList<Person> Children(Family family, Person person)
        => [.. family.Members
            .Where(other => other.FatherId == person.Id || other.MotherId == person.Id)
            .OrderBy(other => other.BirthDate.Year)
            .ThenBy(other => other.BirthDate.Month)
            .ThenBy(other => other.Id.Value)];

    /// <summary>
    /// 本成员的兄弟姐妹（同父或同母，排除本人；按出生日期排序）。
    /// </summary>
    public static IReadOnlyList<Person> Siblings(Family family, Person person)
        => [.. family.Members
            .Where(other => other.Id != person.Id
                && ((person.FatherId is { } father && other.FatherId == father)
                    || (person.MotherId is { } mother && other.MotherId == mother)))
            .OrderBy(other => other.BirthDate.Year)
            .ThenBy(other => other.BirthDate.Month)
            .ThenBy(other => other.Id.Value)];

    /// <summary>「嫡长子」判定：嫡出儿子里出生最早者（无嫡出时退化为最年长的儿子）。</summary>
    /// <param name="family">家族（只读）。</param>
    /// <param name="parent">父或母。</param>
    /// <param name="child">待判定者。</param>
    /// <returns>该子女是否为嫡长子。</returns>
    public static bool IsEldestSon(Family family, Person parent, Person child)
    {
        if (child.Gender != Gender.Male)
        {
            return false;
        }

        var sons = Children(family, parent).Where(son => son.Gender == Gender.Male).ToList();
        if (sons.Count == 0)
        {
            return false;
        }

        var legitimate = sons
            .Where(son => IsLegitimate(parent, son))
            .ToList();
        var pool = legitimate.Count > 0 ? legitimate : sons;

        return pool[0].Id == child.Id;
    }

    /// <summary>子女摘要文字（供成员详情一行显示）。</summary>
    /// <param name="family">家族（只读）。</param>
    /// <param name="person">中心成员。</param>
    /// <returns>形如「狄立言（嫡长子）、狄蓉娘、狄清照」；无子女时为「—」。</returns>
    public static string ChildrenText(Family family, Person person)
    {
        var children = Children(family, person);
        if (children.Count == 0)
        {
            return "—";
        }

        var parts = children.Select(child =>
            IsEldestSon(family, person, child) ? $"{child.Name}（嫡长子）" : child.Name);

        return string.Join("、", parts);
    }

    /// <summary>兄弟姐妹摘要文字。</summary>
    /// <param name="family">家族（只读）。</param>
    /// <param name="person">中心成员。</param>
    /// <returns>形如「狄立言、狄清照」；无兄弟姐妹时为「—」。</returns>
    public static string SiblingsText(Family family, Person person)
    {
        var siblings = Siblings(family, person);
        return siblings.Count == 0 ? "—" : string.Join("、", siblings.Select(sibling => sibling.Name));
    }

    /// <summary>生母是否本成员的配偶或前任配偶（即嫡出）。</summary>
    private static bool IsLegitimate(Person parent, Person child)
    {
        if (child.MotherId is not { } mother)
        {
            return false;
        }

        return mother == parent.SpouseId || parent.FormerSpouseIds.Contains(mother);
    }

    private static void Add(List<RelationLink> links, string label, Family family, PersonId? id)
    {
        if (id is not { } personId || family.TryGet(personId) is not { } relative)
        {
            return;
        }

        links.Add(new RelationLink($"{label} {relative.Name}", $"select:{relative.Id.Value}"));
    }
}
