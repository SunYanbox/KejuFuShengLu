using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.FamilyLifecycle;

namespace KFL.Presentation.ViewModels;

/// <summary>
/// 成员详情里的一条关系超链接（玩家反馈 7）：点一下跳到对应成员的详情卡片。
/// </summary>
/// <param name="label">显示文字（形如「父 狄立言」「大哥 狄立本」）。</param>
/// <param name="token">命令令牌（形如 <c>select:&lt;guid&gt;</c>）。</param>
/// <param name="state">该亲属当前状态（形如「在册」「患病」「服刑(3月)」「身故」），随悬浮提示显示。</param>
/// <param name="brushKey">按钮前景色键：按状态区分（患病 / 服刑 / 身故 / 处罚期 / 默认）。</param>
public sealed record RelationLink(string Label, string Token, string State = "", string BrushKey = "RelationBrush")
{
    /// <summary>悬浮提示文字（含该亲属当前状态，便于一眼判断能否派事）。</summary>
    public string ToolTip => State.Length > 0
        ? $"点击跳到该成员详情（当前状态：{State}）"
        : "点击跳到该成员详情";
}

/// <summary>
/// 成员详情里的一**行**关系按钮（2026-10-19 第九轮玩家反馈 2：父母与配偶一行、兄弟姐妹一行、子女一行）。
/// </summary>
/// <param name="Title">该行的行首标题（形如「父母与配偶」）。</param>
/// <param name="Links">该行的关系按钮；为空时整行显示占位文字。</param>
public sealed record RelationRow(string Title, IReadOnlyList<RelationLink> Links)
{
    /// <summary>该行是否没有任何关系（视图据此显示「—」）。</summary>
    public bool IsEmpty => Links.Count == 0;
}

/// <summary>
/// 亲属关系投影（玩家反馈 3 / 7 / 8）：父母、配偶、子女（嫡长子额外标注）与兄弟姐妹。
/// </summary>
/// <remarks>
/// <para>
/// 「嫡长子」的口径：<b>嫡出</b> = 生母是本成员的配偶或前任配偶（<see cref="Person.SpouseId"/> /
/// <see cref="Person.FormerSpouseIds"/>，非婚生的收养子女不计入）；<b>长子</b> = 这些儿子里出生最早
/// 的一位。没有嫡出儿子时，标注落在最年长的儿子上，避免整行无标记（推断见 <c>推断.md</c> §9）。
/// </para>
/// <para>
/// 长幼称谓（大哥 / 三弟 / 二女儿 / 嫡长子）统一由
/// <see cref="KFL.Rules.FamilyLifecycle.GenealogyQueries"/> 计算，序号一律按**混合性别、含本人在内**的
/// 出生全序排定，故兄弟姐妹与子女的序号都不断档（第九轮玩家反馈 2）。
/// </para>
/// </remarks>
public static class FamilyRelations
{
    /// <summary>按「父 / 母 / 配偶 / 子女 / 兄弟姐妹」顺序给出可直接点击的关系链接（平铺版）。</summary>
    /// <param name="family">家族（只读）。</param>
    /// <param name="person">中心成员。</param>
    /// <returns>关系链接列表；没有亲属时为空。</returns>
    public static IReadOnlyList<RelationLink> LinksOf(Family family, Person person)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(person);

        var links = new List<RelationLink>();
        AddParentsAndSpouse(links, family, person);

        foreach (var entry in GenealogyQueries.ChildTitles(family, person))
        {
            Add(links, entry.Title, family, entry.Person.Id);
        }

        foreach (var entry in GenealogyQueries.SiblingTitles(family, person))
        {
            Add(links, entry.Title, family, entry.Person.Id);
        }

        return links;
    }

    /// <summary>
    /// 按三行给出关系按钮（第九轮玩家反馈 2）：①父母与配偶；②兄弟姐妹；③子女。
    /// </summary>
    /// <param name="family">家族（只读）。</param>
    /// <param name="person">中心成员。</param>
    /// <returns>固定三行（行内可能为空列表）。</returns>
    public static IReadOnlyList<RelationRow> RowsOf(Family family, Person person)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(person);

        var parentSpouse = new List<RelationLink>();
        AddParentsAndSpouse(parentSpouse, family, person);

        var siblings = new List<RelationLink>();
        foreach (var entry in GenealogyQueries.SiblingTitles(family, person))
        {
            Add(siblings, entry.Title, family, entry.Person.Id);
        }

        var children = new List<RelationLink>();
        foreach (var entry in GenealogyQueries.ChildTitles(family, person))
        {
            Add(children, entry.Title, family, entry.Person.Id);
        }

        return
        [
            new RelationRow("父母与配偶", parentSpouse),
            new RelationRow("兄弟姐妹", siblings),
            new RelationRow("子女", children),
        ];
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
        => GenealogyQueries.IsEldestSon(family, parent, child);

    /// <summary>子女摘要文字（供成员详情一行显示）。</summary>
    /// <param name="family">家族（只读）。</param>
    /// <param name="person">中心成员。</param>
    /// <returns>形如「狄立言（嫡长子）、狄蓉娘、狄清照」；无子女时为「—」。</returns>
    public static string ChildrenText(Family family, Person person)
    {
        var children = Children(family, person);
        return children.Count == 0
            ? "—"
            : string.Join("、", children.Select(child =>
                IsEldestSon(family, person, child) ? $"{child.Name}（嫡长子）" : child.Name));
    }

    /// <summary>兄弟姐妹摘要文字。</summary>
    /// <param name="family">家族（只读）。</param>
    /// <param name="person">中心成员。</param>
    /// <returns>形如「狄立言、狄清照」；无兄弟姐妹时为「—」。</returns>
    public static string SiblingsText(Family family, Person person)
        => GenealogyQueries.SiblingTitles(family, person) is { Count: > 0 } titles
            ? string.Join("、", titles.Select(entry => $"{entry.Title} {entry.Person.Name}"))
            : "—";

    /// <summary>按状态给关系按钮选前景色键（第九轮：生病 / 服刑 / 死亡等状态换色以便区分）。</summary>
    /// <param name="person">该关系指向的成员。</param>
    /// <param name="normalBrushKey">无异常状态时返回的资源键（家谱关系按钮用蓝色，成员详情抬头用墨色）。</param>
    /// <returns>主题资源键：<c>RelationDeceasedBrush</c> / <c>RelationIllBrush</c> /
    /// <c>RelationSentenceBrush</c> / <c>RelationWarnBrush</c> / <paramref name="normalBrushKey"/>。</returns>
    public static string BrushKeyOf(Person person, string normalBrushKey = "RelationBrush")
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalBrushKey);

        if (person.Status.HasFlag(StatusFlag.Deceased))
        {
            return "RelationDeceasedBrush";
        }

        if (person.Status.HasFlag(StatusFlag.Ill) || person.Timers.TreatmentRemainingMonths is not null)
        {
            return "RelationIllBrush";
        }

        if (person.Status.HasFlag(StatusFlag.ServingSentence))
        {
            return "RelationSentenceBrush";
        }

        if (person.Status.HasFlag(StatusFlag.AwaitingPost)
            || person.Status.HasFlag(StatusFlag.ExamBanned)
            || person.Status.HasFlag(StatusFlag.PromotionBanned)
            || person.Status.HasFlag(StatusFlag.MarriedOut))
        {
            return "RelationWarnBrush";
        }

        return normalBrushKey;
    }

    /// <summary>父 / 母 / 配偶三条（第一行）。</summary>
    private static void AddParentsAndSpouse(List<RelationLink> links, Family family, Person person)
    {
        Add(links, "父", family, person.FatherId);
        Add(links, "母", family, person.MotherId);
        Add(links, "配偶", family, person.SpouseId);
    }

    private static void Add(List<RelationLink> links, string label, Family family, PersonId? id)
    {
        if (id is not { } personId || family.TryGet(personId) is not { } relative)
        {
            return;
        }

        links.Add(new RelationLink(
            $"{label} {relative.Name}",
            $"select:{relative.Id.Value}",
            Display.StatusOf(relative, relative.BirthDate),
            BrushKeyOf(relative)));
    }
}
