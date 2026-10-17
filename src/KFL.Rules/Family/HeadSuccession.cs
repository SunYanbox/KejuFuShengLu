using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Settlement;

namespace KFL.Rules.FamilyLifecycle;

/// <summary>
/// ⑧-h 家主继任（契约二十 §8；规格书 §4.4）：家主亡故或从在册集合消失的**同月**、在 ⑧-g 之后
/// **一次**按四档产生新家主。
/// </summary>
/// <remarks>
/// <para>
/// 四档顺序（逐字照 <c>contracts/illness-lifespan.md</c> §8）：
/// ① 已亡家主的**在册男性子女**；② 已亡儿子的**在册男性后代**（代际就近优先、同代年龄最大）；
/// ③ 已亡家主的**亲兄弟**；④ 在册男性中**年龄最大**者、同岁取**在册子代数更多**者、
/// 仍同按 <see cref="PersonId"/> 升序。
/// </para>
/// <para>
/// 候选 = **在册男性**（<c>CountedMembers.IsRegistered</c>：含服刑、待阙；排除已亡与外嫁），
/// **MUST NOT 设年龄下限**（FR-036，Assumptions 25）——故新生儿也可当选。
/// </para>
/// <para>
/// 无候选 ⇒ 返回空序列，调用方经 <see cref="Select"/> 得 <c>null</c> 后
/// <see cref="Family.SetHead(PersonId?)"/> 置空（**家主空缺，不构成绝嗣**）。
/// </para>
/// <para>
/// 现任家主健在且未外嫁 ⇒ 返回空序列（本子步**不做任何事**，FR-037）。
/// </para>
/// <para>
/// <b>与工件的差异（Phase 9 登记）</b>：<c>data-model.md</c> §3.5 与 <c>tasks.md</c> T062 记的签名是
/// <c>Candidates(Family family)</c>；四档的决胜口径要求 <c>Person.AgeAt</c>，它必须有一个参照月，
/// 故本实现追加可选形参 <see cref="GameDate"/>。传 <c>null</c> 时以「出生日期更早」等价表达年龄降序
/// （查询面 <c>LifecycleQueries.SuccessionCandidates</c> 走这一支，它没有参照月）。
/// </para>
/// </remarks>
public static class HeadSuccession
{
    /// <summary>按四档优先级返回家主继任候选序列（已排序，首位即当选者）。</summary>
    /// <param name="family">家族。</param>
    /// <param name="date">参照月；<c>null</c> 时以出生日期表达年龄降序。</param>
    /// <returns>有序候选；无候选或现任家主健在未外嫁时为空序列。</returns>
    public static IReadOnlyList<Person> Candidates(Family family, GameDate? date = null)
    {
        ArgumentNullException.ThrowIfNull(family);

        var head = family.HeadId is { } headId ? family.TryGet(headId) : null;
        if (head is not null && CountedMembers.IsRegistered(head))
        {
            return [];
        }

        var ordered = new List<Person>();
        var seen = new HashSet<PersonId>();

        void Append(IEnumerable<Person> source, bool byChildCount = false)
        {
            var ranked = byChildCount
                ? RankByChildren(family, source, date)
                : Rank(source, date);

            foreach (var person in ranked)
            {
                if (seen.Add(person.Id))
                {
                    ordered.Add(person);
                }
            }
        }

        // ① 已亡家主的在册男性子女
        if (head is not null)
        {
            Append(family.Members.Where(member => member.FatherId == head.Id || member.MotherId == head.Id));
        }

        // ② 已亡儿子的在册男性后代（代际就近优先：逐代下探，每代内部再按年龄降序）
        if (head is not null)
        {
            var frontier = family.Members
                .Where(member => member.FatherId == head.Id
                    && member.Gender == Gender.Male
                    && !CountedMembers.IsRegistered(member))
                .Select(member => member.Id)
                .ToList();

            while (frontier.Count > 0)
            {
                var next = new List<PersonId>();
                foreach (var parentId in frontier)
                {
                    var children = family.Members.Where(member => member.FatherId == parentId).ToList();
                    Append(children);
                    next.AddRange(children.Select(child => child.Id));
                }

                frontier = next;
            }
        }

        // ③ 已亡家主的亲兄弟（在册男性）
        if (head is not null && head.FatherId is { } fatherId)
        {
            Append(family.Members.Where(member => member.FatherId == fatherId && member.Id != head.Id));
        }

        // ④ 兜底：全部在册男性（年龄降序 → 同岁在册子代数降序 → PersonId 升序）
        Append(family.Members, byChildCount: true);

        return ordered;
    }

    /// <summary>取首位候选的 <see cref="PersonId"/>；空序列返回 <c>null</c>（家主空缺）。</summary>
    /// <param name="candidates">已排序的候选序列。</param>
    /// <returns>新家主；无候选时为 <c>null</c>。</returns>
    public static PersonId? Select(IReadOnlyList<Person> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates.Count == 0 ? null : candidates[0].Id;
    }

    /// <summary>年龄降序 → <see cref="PersonId"/> 升序，且只保留在册男性。</summary>
    private static IEnumerable<Person> Rank(IEnumerable<Person> source, GameDate? date)
    {
        var males = source.Where(CountedMembers.IsRegistered).Where(person => person.Gender == Gender.Male);

        return date is { } reference
            ? males.OrderByDescending(person => person.AgeAt(reference)).ThenBy(person => person.Id.Value)
            : males
                .OrderBy(person => person.BirthDate.Year)
                .ThenBy(person => person.BirthDate.Month)
                .ThenBy(person => person.Id.Value);
    }

    /// <summary>第 ④ 档：年龄降序 → 在册子代数降序 → <see cref="PersonId"/> 升序。</summary>
    private static IEnumerable<Person> RankByChildren(Family family, IEnumerable<Person> source, GameDate? date)
    {
        var males = source.Where(CountedMembers.IsRegistered).Where(person => person.Gender == Gender.Male);
        var byAge = date is { } reference
            ? males.OrderByDescending(person => person.AgeAt(reference))
            : males.OrderBy(person => person.BirthDate.Year).ThenBy(person => person.BirthDate.Month);

        return byAge
            .ThenByDescending(person => RegisteredChildren(family, person))
            .ThenBy(person => person.Id.Value);
    }

    /// <summary>在册子代数（只数男性与女性子嗣中仍在册者，供第 ④ 档同岁决胜）。</summary>
    private static int RegisteredChildren(Family family, Person person)
        => family.Members.Count(child =>
            child.FatherId == person.Id
            && CountedMembers.IsRegistered(child));
}
