using System.Collections.ObjectModel;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Core.Entities;

/// <summary>
/// 家族聚合：成员集合、婚姻与血缘主轴，以及辈分与家主。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Family"/> 是**跨实体引用的唯一入口**（章程原则 II：实体只承载数据与自身
/// 不变量，不承担跨实体编排）。<see cref="Person"/> 不提供配偶、父母引用与辈分的公开写入通道。
/// </para>
/// <para>
/// **MUST NOT** 在本阶段实现家主继任判定——它由死亡推进触发，属阶段⑧。本类型只保证
/// <see cref="HeadId"/> 存在、且「为 <c>null</c> 或指向在册成员」这一不变量可被校验。
/// </para>
/// </remarks>
public sealed class Family
{
    private readonly List<Person> _members = [];
    private readonly Dictionary<PersonId, Person> _byId = [];
    private readonly ReadOnlyCollection<Person> _membersView;

    /// <summary>外来者中**辈分已由家族指定（落定）**者（规格书 §4.4）。</summary>
    private readonly HashSet<PersonId> _settledOutsiders = [];

    /// <summary>外来者中**首次家族内成婚的对齐已用掉**者——此后辈分终局（规格书 §4.4）。</summary>
    private readonly HashSet<PersonId> _marriageAlignedOutsiders = [];

    /// <summary>构造一个空家族。</summary>
    /// <param name="name">家族姓氏（规格书 §12.4）。</param>
    public Family(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        _membersView = _members.AsReadOnly();
    }

    /// <summary>家族姓氏。</summary>
    public string Name { get; }

    /// <summary>
    /// 「仕身份」= 进士直系血统（规格书 §10.2）。与任何 <see cref="Origin"/> 独立可并存；
    /// 建在家族级而非成员级是 §17 裁决。
    /// </summary>
    public bool HasShiStatus { get; set; }

    /// <summary>
    /// 家主。<c>null</c> = 家族内无在册男性成员（该情形**不构成绝嗣**，规格书 §15）；
    /// 否则 MUST 指向本家族的**在册**成员。唯一写入入口是 <see cref="SetHead"/>。
    /// </summary>
    public PersonId? HeadId { get; private set; }

    /// <summary>全部成员（含已归档），按加入顺序。</summary>
    public IReadOnlyCollection<Person> Members => _membersView;

    /// <summary>**在册**成员 = 非「已亡」且非「外嫁」（规格书 §12.1、§15）。</summary>
    public IReadOnlyCollection<Person> RegisteredMembers =>
        _members.Where(IsRegistered).ToList().AsReadOnly();

    /// <summary>**已归档**成员 = 已亡 或 外嫁（规格书 §12.1）。归档**不是删除**，档案仍可读。</summary>
    public IReadOnlyCollection<Person> ArchivedMembers =>
        _members.Where(m => !IsRegistered(m)).ToList().AsReadOnly();

    /// <summary>按标识取成员；不存在返回 <c>null</c>。</summary>
    /// <param name="id">成员标识。</param>
    public Person? TryGet(PersonId id) => _byId.TryGetValue(id, out var person) ? person : null;

    /// <summary>
    /// §9.5 买来的旁系的辈分 = **家主辈分 + 1**（规格书 §4.4）。
    /// 这是 <see cref="HeadId"/> 在 001 内的真实消费者；家主为 <c>null</c>（无在册男性成员）时无从推导。
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="HeadId"/> 为 <c>null</c>。</exception>
    public int BoughtCollateralGeneration => HeadId is { } headId
        ? EnsureMember(headId, nameof(HeadId)).Generation + 1
        : throw new InvalidOperationException(
            "家族内无在册男性成员（HeadId 为 null），无法推导买来者的辈分（规格书 §4.4）。");

    /// <summary>派生子女引用（父母任一指向该成员即算子女）。</summary>
    /// <param name="id">父或母的标识。</param>
    /// <returns>子女列表，按加入顺序；含已归档成员。</returns>
    /// <exception cref="ArgumentException"><paramref name="id"/> 不是本家族成员。</exception>
    public IReadOnlyList<Person> ChildrenOf(PersonId id)
    {
        EnsureMember(id, nameof(id));
        return _members.Where(m => m.FatherId == id || m.MotherId == id).ToList().AsReadOnly();
    }

    /// <summary>取配偶；无配偶返回 <c>null</c>。</summary>
    /// <param name="id">成员标识。</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> 不是本家族成员。</exception>
    public Person? SpouseOf(PersonId id)
    {
        var person = EnsureMember(id, nameof(id));
        return person.SpouseId is { } spouseId ? EnsureMember(spouseId, nameof(id)) : null;
    }

    /// <summary>加入一名**开局成员**：无父母参照，且辈分 MUST 为 0（规格书 §4.4「辈分自创始者为 0」）。</summary>
    /// <param name="person">待加入的成员。</param>
    /// <returns>已加入的成员。</returns>
    /// <exception cref="ArgumentException">该成员有父母参照，或辈分不为 0。</exception>
    public Person AddFoundingMember(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        if (person.FatherId is not null || person.MotherId is not null)
        {
            throw new ArgumentException("开局成员 MUST NOT 有父母参照。", nameof(person));
        }

        if (person.Generation != 0)
        {
            throw new ArgumentException(
                $"开局成员的辈分 MUST 为 0（规格书 §4.4「辈分自创始者为 0」），实际为 {person.Generation}。",
                nameof(person));
        }

        return Register(person);
    }

    /// <summary>
    /// 加入一名**血亲成员**：<see cref="Person.FatherId"/> / <see cref="Person.MotherId"/> 至少一方
    /// 指向本家族在册成员，且辈分 MUST = 主轴父母辈分 + 1（规格书 §4.4；父母皆有则以父系为主轴）。
    /// </summary>
    /// <param name="child">待加入的成员。</param>
    /// <returns>已加入的成员。</returns>
    /// <exception cref="ArgumentException">无父母参照、父母引用不在本家族，或辈分不等于父母辈分 + 1。</exception>
    public Person AddChild(Person child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (child.FatherId is null && child.MotherId is null)
        {
            throw new ArgumentException(
                "血亲成员 MUST 至少有一位父母引用指向本家族成员。", nameof(child));
        }

        var father = child.FatherId is { } fatherId ? EnsureMember(fatherId, nameof(child)) : null;
        var mother = child.MotherId is { } motherId ? EnsureMember(motherId, nameof(child)) : null;
        var focal = father ?? mother!;

        var expected = focal.Generation + 1;
        if (child.Generation != expected)
        {
            throw new ArgumentException(
                $"血亲成员的辈分 MUST = 父母辈分 + 1（应为 {expected}），实际为 {child.Generation}（规格书 §4.4）。",
                nameof(child));
        }

        return Register(child);
    }

    /// <summary>
    /// 加入一名**外来者**：<see cref="Person.FatherId"/> 与 <see cref="Person.MotherId"/> 皆 <c>null</c>
    /// （开局成员、娶入配偶、§9.5 买来的旁系）。辈分由家族指定——娶入配偶等于其配偶的辈分，
    /// 买来的旁系等于家主辈分 + 1（规格书 §4.4）。
    /// </summary>
    /// <param name="outsider">待加入的成员。</param>
    /// <returns>已加入的成员。</returns>
    /// <exception cref="ArgumentException">该成员有父母参照（那是血亲，应走 <see cref="AddChild"/>）。</exception>
    public Person AddOutsider(Person outsider)
    {
        ArgumentNullException.ThrowIfNull(outsider);

        if (!outsider.IsOutsider)
        {
            throw new ArgumentException(
                "外来者 MUST 无父母参照；有父母参照的成员应走 AddChild。", nameof(outsider));
        }

        return Register(outsider);
    }

    /// <summary>
    /// **落定**一名外来者的辈分。血亲成员的辈分经此入口 MUST 被拒（终身不可变更）；
    /// 外来者 MUST 在其**尚无子女**时落定，且该成员**至多落定一次**——此后其辈分只还能在
    /// **首次家族内成婚**时被对齐一次，之后终局（规格书 §4.4，2026-10-05 裁决）。
    /// </summary>
    /// <param name="id">成员标识。</param>
    /// <param name="generation">新的辈分。</param>
    /// <exception cref="InvalidOperationException">
    /// 该成员是血亲、或已有子女、或辈分已落定、或其首次家族内成婚已发生（辈分终局）。
    /// </exception>
    public void SetOutsiderGeneration(PersonId id, int generation)
    {
        var person = EnsureMember(id, nameof(id));

        if (!person.IsOutsider)
        {
            throw new InvalidOperationException(
                "只有外来者的辈分可由家族指定；血亲辈分出生即定、终身不可变更（规格书 §4.4）。");
        }

        if (generation < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(generation), generation, "辈分 MUST >= 0（规格书 §4.4）。");
        }

        if (ChildrenOf(id).Count > 0)
        {
            throw new InvalidOperationException(
                "外来者的辈分 MUST 在其尚无子女时落定（规格书 §4.4）。");
        }

        if (_marriageAlignedOutsiders.Contains(id))
        {
            throw new InvalidOperationException(
                "该外来者首次在家族内成婚已发生，其辈分已终局，MUST NOT 再变更（规格书 §4.4）。");
        }

        if (!_settledOutsiders.Add(id))
        {
            throw new InvalidOperationException(
                "外来者的辈分一经家族指定即落定，MUST NOT 重复指定；此后仅其首次在家族内成婚时可额外变动一次（规格书 §4.4）。");
        }

        person.SetGeneration(generation);
    }

    /// <summary>
    /// 结为夫妻。**一夫一妻**：已有配偶者 MUST 被拒（规格书 §9.1）。
    /// 若一方是外来者、另一方是家族内成员，则外来者的辈分随之对齐（规格书 §4.4）——
    /// 这只是其**首次家族内成婚**才有的那一次变动，此后终身不再变动。
    /// </summary>
    /// <param name="first">一方标识。</param>
    /// <param name="second">另一方标识。</param>
    /// <exception cref="InvalidOperationException">任一方已有配偶；或外来者已有子女导致辈分无法对齐。</exception>
    public void Marry(PersonId first, PersonId second)
    {
        if (first == second)
        {
            throw new ArgumentException("成员 MUST NOT 与自己成婚。", nameof(second));
        }

        var a = EnsureMember(first, nameof(first));
        var b = EnsureMember(second, nameof(second));

        if (a.SpouseId is not null || b.SpouseId is not null)
        {
            throw new InvalidOperationException(
                "一夫一妻：已有配偶者 MUST NOT 被指定第二个配偶（规格书 §9.1）。");
        }

        a.SetSpouse(b.Id);
        b.SetSpouse(a.Id);

        AlignOutsiderGeneration(a, b);
        AlignOutsiderGeneration(b, a);
    }

    /// <summary>
    /// 终止婚姻（丧偶或离异），走「先置空 <see cref="Person.SpouseId"/>、再把前任追加进
    /// <see cref="Person.FormerSpouseIds"/>」两步，既往配偶记录 MUST NOT 被覆盖（规格书 §9.1）。
    /// **MUST NOT** 改动任何子女的 <see cref="Person.FatherId"/> / <see cref="Person.MotherId"/>。
    /// </summary>
    /// <param name="id">婚姻终止时存活/留存的一方。</param>
    /// <exception cref="InvalidOperationException">该成员当前没有配偶。</exception>
    public void EndMarriage(PersonId id)
    {
        var widowed = EnsureMember(id, nameof(id));

        if (widowed.SpouseId is not { } spouseId)
        {
            throw new InvalidOperationException("该成员当前没有配偶，无需终止婚姻。");
        }

        var spouse = EnsureMember(spouseId, nameof(id));

        widowed.SetSpouse(null);
        spouse.SetSpouse(null);

        widowed.AddFormerSpouse(spouse.Id);
        spouse.AddFormerSpouse(widowed.Id);
    }

    /// <summary>
    /// 指定家主。<c>null</c> 表示家族内无在册男性成员（**不构成绝嗣**，规格书 §15）；
    /// 非空时 MUST 指向本家族的**在册**成员——已亡或外嫁的已归档成员 MUST 被拒（规格书 §4.4）。
    /// </summary>
    /// <param name="headId">家主标识，或 <c>null</c>。</param>
    /// <exception cref="InvalidOperationException">目标不是本家族成员，或已被归档。</exception>
    /// <remarks>
    /// 001 **不校验家主的性别**：继任判定（含「在册男性成员」的筛选）属阶段⑧，
    /// 本阶段只承载字段与其在册性不变量。
    /// </remarks>
    public void SetHead(PersonId? headId)
    {
        if (headId is null)
        {
            HeadId = null;
            return;
        }

        var head = _byId.TryGetValue(headId.Value, out var found)
            ? found
            : throw new InvalidOperationException(
                $"HeadId MUST 指向本家族成员，实际指向 {headId.Value}。");

        if (!IsRegistered(head))
        {
            throw new InvalidOperationException(
                "HeadId MUST NOT 指向已亡或外嫁的已归档成员（规格书 §4.4）。");
        }

        HeadId = headId;
    }

    private static bool IsRegistered(Person person) =>
        (person.Status & (StatusFlag.Deceased | StatusFlag.MarriedOut)) == StatusFlag.None;

    private Person EnsureMember(PersonId id, string paramName) =>
        _byId.TryGetValue(id, out var person)
            ? person
            : throw new ArgumentException(
                $"PersonId 引用 MUST 指向本家族成员（无悬挂引用），实际为 {id}。", paramName);

    private Person Register(Person person)
    {
        if (_byId.ContainsKey(person.Id))
        {
            throw new ArgumentException($"成员标识 MUST 唯一，{person.Id} 已存在。", nameof(person));
        }

        if (person.SpouseId is { } spouseId)
        {
            var spouse = EnsureMember(spouseId, nameof(person));
            if (spouse.SpouseId != person.Id)
            {
                throw new ArgumentException(
                    "配偶关系 MUST 双向一致（data-model §2.2 不变量 2）。", nameof(person));
            }
        }

        foreach (var former in person.FormerSpouseIds)
        {
            EnsureMember(former, nameof(person));
        }

        _members.Add(person);
        _byId.Add(person.Id, person);
        return person;
    }

    /// <summary>
    /// 结婚后把外来者一方的辈分对齐到家族内配偶的辈分（规格书 §4.4）。
    /// **只在首次家族内成婚时生效**：此后（如丧偶再婚）该外来者的辈分终局，MUST NOT 再变动。
    /// </summary>
    private void AlignOutsiderGeneration(Person outsider, Person member)
    {
        if (!outsider.IsOutsider || member.IsOutsider)
        {
            return;
        }

        if (_marriageAlignedOutsiders.Contains(outsider.Id))
        {
            // 非首次家族内成婚（如丧偶再婚）：辈分已终局，此处不再变动（规格书 §4.4）。
            return;
        }

        if (outsider.Generation != member.Generation)
        {
            if (ChildrenOf(outsider.Id).Count > 0)
            {
                throw new InvalidOperationException(
                    "外来者的辈分 MUST 在其尚无子女时落定，已有子女后 MUST NOT 再变更（规格书 §4.4）。");
            }

            outsider.SetGeneration(member.Generation);
        }

        // 无论数值是否真的变了，「首次家族内成婚」这一次机会都已用掉（规格书 §4.4）。
        _settledOutsiders.Add(outsider.Id);
        _marriageAlignedOutsiders.Add(outsider.Id);
    }
}
