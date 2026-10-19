using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Rules.FamilyLifecycle;

/// <summary>
/// 家谱视图的派生查询（2026-10-19 第五轮玩家反馈）：收养成员身份与「家主支脉」范围。
/// </summary>
/// <remarks>
/// <para>
/// 两者都是**纯查询**：只读账本与家族关系，不写状态、不消费随机、不改存档形状。
/// </para>
/// <para>
/// 「收养成员」的唯一判据是账本里 <see cref="LedgerCategory.PersonBought"/> 事件条目的
/// <see cref="LedgerEntry.PersonId"/>；该事件只在 <c>LifecycleEntry.BuyPerson</c> 成功路径落一条，
/// 因此被收养者组内通婚所生的子女**天然没有**该条目，不会被误标。
/// </para>
/// </remarks>
public static class GenealogyQueries
{
    /// <summary>全部「收养成员」标识（按账本事件条目派生，顺序不保证）。</summary>
    /// <param name="state">存档状态（只读）。</param>
    /// <returns>被收养者的标识集合；没有收养记录时为空集。</returns>
    public static IReadOnlySet<PersonId> PurchasedPersonIds(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var ids = new HashSet<PersonId>();
        foreach (var entry in state.Economy.Ledger.Entries)
        {
            if (entry.Category == LedgerCategory.PersonBought && entry.PersonId is { } id)
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    /// <summary>
    /// 「家主支脉」的成员标识范围：家主本人、其直系血亲祖先、其全部直系血亲后代，
    /// 以及这些人的**当前配偶**。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 不含家主的兄弟姐妹及其后裔（旁系），也不含旁系的配偶；配偶只作为「附属于某个支脉成员」
    /// 的节点纳入，不据此继续展开其血亲或前段婚姻的子女。
    /// </para>
    /// <para>家主空缺（<see cref="Family.HeadId"/> 为 <c>null</c>）时返回空集，调用方应视为「不过滤」。</para>
    /// </remarks>
    /// <param name="family">家族（只读）。</param>
    /// <returns>家主支脉的标识集合；家主空缺时为空集。</returns>
    public static IReadOnlySet<PersonId> HeadBranchPersonIds(Family family)
    {
        ArgumentNullException.ThrowIfNull(family);

        var ids = new HashSet<PersonId>();
        if (family.HeadId is not { } headId || family.TryGet(headId) is not { } head)
        {
            return ids;
        }

        // ① 直系血亲祖先（沿父母上溯）。
        var ancestors = new Stack<Person>();
        ancestors.Push(head);
        while (ancestors.Count > 0)
        {
            var person = ancestors.Pop();
            if (!ids.Add(person.Id))
            {
                continue;
            }

            if (person.FatherId is { } fatherId && family.TryGet(fatherId) is { } father)
            {
                ancestors.Push(father);
            }

            if (person.MotherId is { } motherId && family.TryGet(motherId) is { } mother)
            {
                ancestors.Push(mother);
            }
        }

        // ② 直系血亲后代（从家主向下逐层展开）。
        var descendants = new Queue<Person>();
        var visited = new HashSet<PersonId> { head.Id };
        descendants.Enqueue(head);
        while (descendants.Count > 0)
        {
            var person = descendants.Dequeue();
            foreach (var child in family.Members)
            {
                if (child.FatherId != person.Id && child.MotherId != person.Id)
                {
                    continue;
                }

                if (visited.Add(child.Id))
                {
                    ids.Add(child.Id);
                    descendants.Enqueue(child);
                }
            }
        }

        // ③ 当前配偶随节点显示（不再据其继续展开）。
        foreach (var id in ids.ToList())
        {
            if (family.TryGet(id)?.SpouseId is { } spouseId)
            {
                ids.Add(spouseId);
            }
        }

        return ids;
    }

    /// <summary>
    /// 「家主一脉嫡系男性」（2026-10-19 第九轮玩家反馈）：家主本人（男性时）＋ 沿**嫡出儿子**链条
    /// 从家主向下的全部男性后代。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 「嫡出」= 生母是父（母）亲的**当前或前任配偶**（<see cref="IsLegitimateChild"/>）；
    /// 「一脉」= 只沿这条嫡出子链走，旁支、庶出与女儿**不继承**该标记，故她们的子孙也不在集合内。
    /// </para>
    /// <para>家主空缺时为空集；该集合只用于**卡片样式**，MUST NOT 影响任何数值或筛选。</para>
    /// </remarks>
    /// <param name="family">家族（只读）。</param>
    /// <returns>家主一脉嫡系男性的标识集合；家主空缺时为空集。</returns>
    public static IReadOnlySet<PersonId> HeadDirectLineMaleIds(Family family)
    {
        ArgumentNullException.ThrowIfNull(family);

        var ids = new HashSet<PersonId>();
        if (family.HeadId is not { } headId || family.TryGet(headId) is not { } head)
        {
            return ids;
        }

        if (head.Gender == Gender.Male)
        {
            ids.Add(head.Id);
        }

        var queue = new Queue<Person>();
        queue.Enqueue(head);
        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();
            foreach (var son in OrderedByBirth(family.Members.Where(candidate =>
                candidate.Gender == Gender.Male
                && (candidate.FatherId == parent.Id || candidate.MotherId == parent.Id)
                && IsLegitimateChild(parent, candidate))))
            {
                if (ids.Add(son.Id))
                {
                    queue.Enqueue(son);
                }
            }
        }

        return ids;
    }

    /// <summary>「嫡出」判定：生母是父（母）亲的当前或前任配偶（§12.2 的「嫡长子」同口径）。</summary>
    /// <param name="parent">父或母。</param>
    /// <param name="child">子女。</param>
    /// <returns>子女是否嫡出。</returns>
    public static bool IsLegitimateChild(Person parent, Person child)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(child);

        if (child.MotherId is not { } mother)
        {
            return false;
        }

        return mother == parent.SpouseId || parent.FormerSpouseIds.Contains(mother);
    }

    /// <summary>「嫡长子」判定：嫡出儿子里出生最早者（无嫡出时退化为最年长的儿子）。</summary>
    /// <param name="family">家族（只读）。</param>
    /// <param name="parent">父或母。</param>
    /// <param name="child">待判定者。</param>
    /// <returns>该子女是否为嫡长子。</returns>
    public static bool IsEldestSon(Family family, Person parent, Person child)
    {
        ArgumentNullException.ThrowIfNull(family);

        if (child.Gender != Gender.Male)
        {
            return false;
        }

        var sons = OrderedByBirth(family.Members.Where(candidate =>
            candidate.Gender == Gender.Male
            && (candidate.FatherId == parent.Id || candidate.MotherId == parent.Id))).ToList();
        if (sons.Count == 0)
        {
            return false;
        }

        var legitimate = sons.Where(son => IsLegitimateChild(parent, son)).ToList();
        var pool = legitimate.Count > 0 ? legitimate : sons;

        return pool[0].Id == child.Id;
    }

    /// <summary>
    /// 兄弟姐妹的**长幼称谓**（2026-10-19 第九轮玩家反馈）：按全序（含本人在内，混合性别、按出生先后）
    /// 取序号，序号做成「大 / 二 / 三 …」前缀，再按「相对本人年长/年幼 + 性别」接「兄 / 弟 / 姐 / 妹」。
    /// </summary>
    /// <remarks>
    /// 例：本人是次子（二哥）时，其兄弟姐妹的称谓依次可能为「大哥、三弟、四妹」——**本人的序号占位保留**，
    /// 故弟妹的序号不因本人被剔除而前移。
    /// </remarks>
    /// <param name="family">家族（只读）。</param>
    /// <param name="person">中心成员。</param>
    /// <returns>按出生先后的兄弟姐妹及其称谓；无兄弟姐妹时为空列表。</returns>
    public static IReadOnlyList<KinshipEntry> SiblingTitles(Family family, Person person)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(person);

        var all = OrderedByBirth(family.Members.Where(other =>
            other.Id == person.Id || SharesParent(person, other))).ToList();
        var selfIndex = all.FindIndex(other => other.Id == person.Id);

        var entries = new List<KinshipEntry>();
        for (var index = 0; index < all.Count; index++)
        {
            var sibling = all[index];
            if (sibling.Id == person.Id)
            {
                continue;
            }

            entries.Add(new KinshipEntry(sibling, SiblingTitle(index + 1, sibling.Gender, index < selfIndex)));
        }

        return entries;
    }

    /// <summary>
    /// 子女的**长幼称谓**（2026-10-19 第九轮玩家反馈）：嫡长子标「嫡长子」，其余按全序（混合性别、
    /// 按出生先后）取序号，做成「大 / 二 / 三 …」前缀再接「儿子 / 女儿」。
    /// </summary>
    /// <param name="family">家族（只读）。</param>
    /// <param name="person">中心成员。</param>
    /// <returns>按出生先后的子女及其称谓；无子女时为空列表。</returns>
    public static IReadOnlyList<KinshipEntry> ChildTitles(Family family, Person person)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(person);

        var children = OrderedByBirth(family.Members.Where(other =>
            other.FatherId == person.Id || other.MotherId == person.Id)).ToList();

        var entries = new List<KinshipEntry>();
        for (var index = 0; index < children.Count; index++)
        {
            var child = children[index];
            entries.Add(new KinshipEntry(
                child,
                ChildTitle(index + 1, child.Gender, IsEldestSon(family, person, child))));
        }

        return entries;
    }

    /// <summary>兄弟姐妹称谓：序号前缀 + 哥 / 弟 / 姐 / 妹（<paramref name="senior"/> = 比本人年长）。</summary>
    /// <param name="rank">在全序中的序号（从 1 起，含本人在内的排位）。</param>
    /// <param name="gender">该兄弟姐妹的性别。</param>
    /// <param name="senior">是否比本人年长。</param>
    /// <returns>形如「大哥」「三弟」「四妹」（年长的男性用「哥」，与玩家反馈给出的示例一致）。</returns>
    public static string SiblingTitle(int rank, Gender gender, bool senior)
    {
        var suffix = (gender, senior) switch
        {
            (Gender.Male, true) => "哥",
            (Gender.Male, false) => "弟",
            (Gender.Female, true) => "姐",
            _ => "妹",
        };

        return OrdinalOf(rank) + suffix;
    }

    /// <summary>子女称谓：嫡长子特例，其余为序号前缀 + 儿子 / 女儿。</summary>
    /// <param name="rank">在全序中的序号（从 1 起）。</param>
    /// <param name="gender">该子女的性别。</param>
    /// <param name="eldestSon">是否为嫡长子。</param>
    /// <returns>形如「嫡长子」「大儿子」「二女儿」。</returns>
    public static string ChildTitle(int rank, Gender gender, bool eldestSon)
        => eldestSon ? "嫡长子" : OrdinalOf(rank) + (gender == Gender.Male ? "儿子" : "女儿");

    /// <summary>长幼序号的中文前缀：1 → 大，2 → 二，10 → 十，11 → 十一，21 → 二十一。</summary>
    /// <param name="rank">序号（从 1 起）。</param>
    /// <returns>中文序号前缀。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rank"/> 小于 1。</exception>
    public static string OrdinalOf(int rank)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rank, 1);

        string[] digits = ["一", "二", "三", "四", "五", "六", "七", "八", "九"];
        if (rank == 1)
        {
            return "大";
        }

        if (rank <= 10)
        {
            return rank == 10 ? "十" : digits[rank - 1];
        }

        var tens = rank / 10;
        var ones = rank % 10;
        var head = tens == 1 ? "十" : digits[tens - 1] + "十";
        return ones == 0 ? head : head + digits[ones - 1];
    }

    /// <summary>按出生先后排序（年 → 月 → Id），家谱与关系称谓共用的全序。</summary>
    /// <param name="people">待排序成员。</param>
    /// <returns>排好序的成员序列。</returns>
    public static IEnumerable<Person> OrderedByBirth(IEnumerable<Person> people)
        => people
            .OrderBy(person => person.BirthDate.Year)
            .ThenBy(person => person.BirthDate.Month)
            .ThenBy(person => person.Id.Value);

    /// <summary>是否同父或同母（不含本人；本人对本人此处视为真，由调用方剔除）。</summary>
    private static bool SharesParent(Person person, Person other)
        => (person.FatherId is { } father && other.FatherId == father)
            || (person.MotherId is { } mother && other.MotherId == mother);
}

/// <summary>一条亲属称谓（家谱与成员详情共用）。</summary>
/// <param name="Person">亲属本人。</param>
/// <param name="Title">长幼称谓，形如「大哥」「嫡长子」「二女儿」。</param>
public sealed record KinshipEntry(Person Person, string Title);
