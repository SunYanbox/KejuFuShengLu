using KFL.Core.ValueObjects;

namespace KFL.Core.Entities;

public sealed partial class Person
{
    /// <summary>父亲引用。**无公开写入通道**。</summary>
    public PersonId? FatherId { get; }

    /// <summary>母亲引用。**无公开写入通道**。</summary>
    public PersonId? MotherId { get; }

    /// <summary>当前配偶。**无公开写入通道**；指定与清空的唯一入口是 <see cref="Family"/>（规格书 §9.1 一夫一妻）。</summary>
    public PersonId? SpouseId => _spouseId;

    /// <summary>既往配偶。**无公开写入通道**；追加的唯一入口是 <see cref="Family"/> 的婚姻终止流程。</summary>
    public IReadOnlyList<PersonId> FormerSpouseIds => _formerSpouseIdsView;

    /// <summary>是否家族内的**外来者**（<see cref="FatherId"/> 与 <see cref="MotherId"/> 皆 <c>null</c>）。</summary>
    public bool IsOutsider => FatherId is null && MotherId is null;

    /// <summary>在本年月出生者于 <paramref name="at"/> 时已满几周岁——年龄**不落裸字段**（research R-07）。</summary>
    /// <param name="at">查询时点。</param>
    /// <returns>已满周岁数。</returns>
    public int AgeAt(GameDate at) => BirthDate.AgeInYearsAt(at);
    /// <summary>设置辈分。**仅供 <see cref="Family"/> 调用**：血亲不可变更，外来者须尚无子女。</summary>
    internal void SetGeneration(int generation)
    {
        if (generation < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(generation), generation, "辈分 MUST >= 0（规格书 §4.4）。");
        }

        _generation = generation;
    }

    /// <summary>设置当前配偶。**仅供 <see cref="Family"/> 调用**（保证双向一致与一夫一妻）。</summary>
    internal void SetSpouse(PersonId? spouseId)
    {
        if (spouseId == Id)
        {
            throw new ArgumentException("SpouseId MUST NOT 指向自身。", nameof(spouseId));
        }

        _spouseId = spouseId;
    }

    /// <summary>把一名前任配偶追加进 <see cref="FormerSpouseIds"/>。**仅供 <see cref="Family"/> 调用**。</summary>
    internal void AddFormerSpouse(PersonId formerSpouseId)
    {
        if (formerSpouseId == Id)
        {
            throw new ArgumentException("FormerSpouseIds MUST NOT 包含自身。", nameof(formerSpouseId));
        }

        if (formerSpouseId == _spouseId)
        {
            throw new ArgumentException("FormerSpouseIds MUST NOT 包含当前配偶。", nameof(formerSpouseId));
        }

        if (_formerSpouseIds.Contains(formerSpouseId))
        {
            throw new ArgumentException("FormerSpouseIds MUST NOT 有重复项。", nameof(formerSpouseId));
        }

        _formerSpouseIds.Add(formerSpouseId);
    }
}
