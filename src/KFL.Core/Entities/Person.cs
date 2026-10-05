using System.Collections.ObjectModel;
using KFL.Core.Config;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Core.Entities;

/// <summary>
/// 家族成员档案（规格书 §4.1 的字段全集载体）。
/// </summary>
/// <remarks>
/// <para>
/// **写入通道只有两类**（data-model §2.1 不变量 2）：<see cref="Person"/> 自持的可写属性只有
/// <see cref="Name"/>、<see cref="Study"/>、<see cref="Health"/>、<see cref="Rank"/>、
/// <see cref="Merit"/>、<see cref="Status"/>、<see cref="Timers"/>、<see cref="Occupation"/>；
/// 跨实体引用（配偶、父母、辈分）的唯一入口是 <see cref="Family"/>——因此
/// <see cref="SpouseId"/>、<see cref="FormerSpouseIds"/>、<see cref="Generation"/> 在这里
/// 只有只读属性，没有公开 setter。
/// </para>
/// <para>
/// **MUST NOT** 含 <c>ChildIds</c>（由 <see cref="Family"/> 派生）、<c>IsShiIdentity</c>
/// （家族级）、<c>Origin</c>（存档级）、出生时辰、「已亡」时间字段，或任何逐月收支字段
/// （后者归 <c>GameState</c> 账本，research R-16）。
/// </para>
/// </remarks>
public sealed class Person
{
    private readonly List<DegreeRecord> _degreeHistory = [];
    private readonly List<PersonId> _formerSpouseIds = [];
    private readonly ReadOnlyCollection<DegreeRecord> _degreeHistoryView;
    private readonly ReadOnlyCollection<PersonId> _formerSpouseIdsView;

    private int _generation;
    private string _name = string.Empty;
    private int _study;
    private int _health;
    private int _merit;
    private PersonId? _spouseId;
    private StatusFlag _status;
    private StatusTimers _timers;

    /// <summary>构造并校验出生即定与构造期可校验的字段。</summary>
    /// <param name="id">成员标识。</param>
    /// <param name="name">姓名。</param>
    /// <param name="gender">性别，出生即定。</param>
    /// <param name="birthDate">出生年月，出生即定；年龄由它派生。</param>
    /// <param name="talents">四项天赋，出生即定。</param>
    /// <param name="lifespan">天命寿数（年）。**只约束 <c>&gt;= 0</c>，无上界**——上界来自规格书 §4.2 的天命寿数分布，属阶段⑧。</param>
    /// <param name="generation">辈分；血亲 = 父母辈分 + 1，外来者由 <see cref="Family"/> 指定。</param>
    /// <param name="fatherId">父亲引用；<c>null</c> 表示家族内无父母参照。</param>
    /// <param name="motherId">母亲引用；<c>null</c> 表示家族内无父母参照。</param>
    /// <exception cref="ArgumentOutOfRangeException">天命寿数 &lt; 0 或辈分 &lt; 0。</exception>
    /// <exception cref="ArgumentException">父母引用指向自身。</exception>
    public Person(
        PersonId id,
        string name,
        Gender gender,
        GameDate birthDate,
        TalentSet talents,
        int lifespan,
        int generation,
        PersonId? fatherId = null,
        PersonId? motherId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (lifespan < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifespan), lifespan, "天命寿数 MUST >= 0（规格书 §4.1）；上界属阶段⑧，本阶段不设。");
        }

        if (generation < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(generation), generation, "辈分 MUST >= 0（规格书 §4.4）。");
        }

        if (fatherId == id)
        {
            throw new ArgumentException("FatherId MUST NOT 指向自身。", nameof(fatherId));
        }

        if (motherId == id)
        {
            throw new ArgumentException("MotherId MUST NOT 指向自身。", nameof(motherId));
        }

        Id = id;
        Name = name;
        Gender = gender;
        BirthDate = birthDate;
        Talents = talents;
        Lifespan = lifespan;
        FatherId = fatherId;
        MotherId = motherId;
        _generation = generation;

        _degreeHistoryView = _degreeHistory.AsReadOnly();
        _formerSpouseIdsView = _formerSpouseIds.AsReadOnly();
    }

    /// <summary>成员标识。**无公开写入通道**。</summary>
    public PersonId Id { get; }

    /// <summary>姓名。</summary>
    public string Name
    {
        get => _name;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _name = value;
        }
    }

    /// <summary>性别。**无公开写入通道**（出生即定）。</summary>
    public Gender Gender { get; }

    /// <summary>
    /// 辈分。**无公开写入通道**：血亲成员出生即定、终身不可变更；外来者的辈分只能由
    /// <see cref="Family"/> 指定，且 MUST 在其尚无子女时落定（规格书 §4.4）。
    /// </summary>
    public int Generation => _generation;

    /// <summary>出生年月。**无公开写入通道**。</summary>
    public GameDate BirthDate { get; }

    /// <summary>四项天赋。**无公开写入通道**（出生即定，终生不可升降，规格书 §4.1）。</summary>
    public TalentSet Talents { get; }

    /// <summary>学业，0~100。</summary>
    public int Study
    {
        get => _study;
        set
        {
            AttributeLimits.EnsureInRange(value, nameof(value));
            _study = value;
        }
    }

    /// <summary>体质，0~100。</summary>
    public int Health
    {
        get => _health;
        set
        {
            AttributeLimits.EnsureInRange(value, nameof(value));
            _health = value;
        }
    }

    /// <summary>天命寿数（年）。**无公开写入通道**；只约束 <c>&gt;= 0</c>，**无上界**（阶段⑧）。</summary>
    public int Lifespan { get; }

    /// <summary>
    /// 功名变迁历史，追加式、按 <see cref="DegreeRecord.ChangedAt"/> 非降序。
    /// **无公开写入通道**；追加唯一入口是 <see cref="AppendDegree"/>，既有记录 MUST NOT 被改写或删除。
    /// </summary>
    public IReadOnlyList<DegreeRecord> DegreeHistory => _degreeHistoryView;

    /// <summary>当前功名：由 <see cref="DegreeHistory"/> 末条**派生**；空历史时为白身。**无公开写入通道**。</summary>
    public DegreeLevel CurrentDegree =>
        _degreeHistory.Count == 0 ? DegreeLevel.BaiShen : _degreeHistory[^1].Level;

    /// <summary>当前一甲名次：由 <see cref="DegreeHistory"/> 末条派生；空历史时为 <c>null</c>。**无公开写入通道**。</summary>
    public ImperialPlacement? CurrentPlacement =>
        _degreeHistory.Count == 0 ? null : _degreeHistory[^1].Placement;

    /// <summary>官阶；<c>null</c> = 无官职（规格书 §8、FR-009）。</summary>
    public OfficialRank? Rank { get; set; }

    /// <summary>政绩，<c>&gt;= 0</c>（上限 100 属规格书 §8.2 规则，阶段⑧）。</summary>
    public int Merit
    {
        get => _merit;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "政绩 MUST >= 0（规格书 §4.1）。");
            }

            _merit = value;
        }
    }

    /// <summary>状态标记，九位可并存（规格书 §4.1、§7.5）。</summary>
    /// <exception cref="ArgumentException">清除某个状态位时，对应的计时字段仍非空。</exception>
    public StatusFlag Status
    {
        get => _status;
        set
        {
            EnsureStatusFlagBacksTimers(value, StatusFlag.ServingSentence, _timers.SentenceRemainingMonths, nameof(value));
            EnsureStatusFlagBacksTimers(value, StatusFlag.ExamBanned, _timers.ExamBanRemainingMonths, nameof(value));
            EnsureStatusFlagBacksTimers(value, StatusFlag.PromotionBanned, _timers.PromotionBanRemainingMonths, nameof(value));
            _status = value;
        }
    }

    /// <summary>状态计时；**仅在对应状态位为真时可非空**（data-model §2.1 不变量 4）。</summary>
    /// <exception cref="ArgumentException">某个计时字段非空，但对应状态位为假。</exception>
    public StatusTimers Timers
    {
        get => _timers;
        set
        {
            EnsureTimerBackedByStatus(_status, StatusFlag.ServingSentence, value.SentenceRemainingMonths, nameof(value));
            EnsureTimerBackedByStatus(_status, StatusFlag.ExamBanned, value.ExamBanRemainingMonths, nameof(value));
            EnsureTimerBackedByStatus(_status, StatusFlag.PromotionBanned, value.PromotionBanRemainingMonths, nameof(value));
            _timers = value;
        }
    }

    /// <summary>职业指派。</summary>
    public Occupation Occupation { get; set; }

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

    /// <summary>
    /// 向功名变迁历史**追加**一条记录。既有记录 MUST NOT 被改写或删除；新记录的
    /// <see cref="DegreeRecord.ChangedAt"/> MUST NOT 早于末条（非降序，research R-14）。
    /// </summary>
    /// <param name="record">一条功名变迁记录。</param>
    /// <exception cref="ArgumentException"><paramref name="record"/> 的年月早于末条。</exception>
    public void AppendDegree(DegreeRecord record)
    {
        if (_degreeHistory.Count > 0 && record.ChangedAt < _degreeHistory[^1].ChangedAt)
        {
            throw new ArgumentException(
                "功名变迁历史 MUST 按 ChangedAt 非降序追加（data-model §2.1 不变量 7）。", nameof(record));
        }

        _degreeHistory.Add(record);
    }

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

    private static void EnsureStatusFlagBacksTimers(
        StatusFlag status, StatusFlag flag, int? timer, string paramName)
    {
        if (!status.HasFlag(flag) && timer is not null)
        {
            throw new ArgumentException(
                $"清除「{flag}」状态位前 MUST 先把对应计时字段置空（规格书 §7.5）。", paramName);
        }
    }

    private static void EnsureTimerBackedByStatus(
        StatusFlag status, StatusFlag flag, int? timer, string paramName)
    {
        if (timer is not null && !status.HasFlag(flag))
        {
            throw new ArgumentException(
                $"计时字段仅在「{flag}」状态位为真时可非空（data-model §2.1 不变量 4）。", paramName);
        }
    }
}
