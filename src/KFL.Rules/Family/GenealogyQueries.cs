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
}
