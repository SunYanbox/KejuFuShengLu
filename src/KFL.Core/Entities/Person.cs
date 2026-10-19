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
/// <see cref="Merit"/>、<see cref="Status"/>、<see cref="Timers"/>、<see cref="Occupation"/>、
/// <see cref="MonthsInOffice"/>、<see cref="EntryTrack"/>；
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
public sealed partial class Person
{
    private readonly List<DegreeRecord> _degreeHistory = [];
    private readonly List<PersonId> _formerSpouseIds = [];
    private readonly ReadOnlyCollection<DegreeRecord> _degreeHistoryView;
    private readonly ReadOnlyCollection<PersonId> _formerSpouseIdsView;

    private int _generation;
private TalentSet _talents;
private int _lifespan;
    private string _name = string.Empty;
    private int _study;
    private int _health;
    private int _merit;
    private int _monthsInOffice;
    private int _jieShiFailureCount;
    private int _shengShiFailureCount;
    private int _jieShiBribeCount;
    private int _shengShiBribeCount;
    private int _dianShiBribeCount;
    private AppointmentTrack? _entryTrack;
    private ExamLevel? _appliedExam;
    private PersonId? _spouseId;
    private StatusFlag _status;
    private StatusTimers _timers;
    private bool _tutoredByMaster;

    /// <summary>构造并校验出生即定与构造期可校验的字段。</summary>
    /// <param name="id">成员标识。</param>
    /// <param name="name">姓名。</param>
    /// <param name="gender">性别，出生即定。</param>
    /// <param name="birthDate">出生年月，出生即定；年龄由它派生。</param>
    /// <param name="talents">四项天赋，出生即定。</param>
    /// <param name="lifespan">天命寿数（年）。**只约束 <c>&gt;= 0</c>，无上界**——上界来自规格书 §4.2 的天命寿数分布，属逻辑轨 ⑦。</param>
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
                nameof(lifespan), lifespan, "天命寿数 MUST >= 0（规格书 §4.1）；上界属逻辑轨 ⑦，本阶段不设。");
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
        _talents = talents;
        _lifespan = lifespan;
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
    /// <remarks>
    /// 唯一的例外是 <see cref="DebugSetTalents"/>：调试控制台按玩家要求改写四项天赋点；
    /// 正常运行路径 MUST NOT 调用它（规格书 §4.1 的「终生不可升降」约束的是游戏内机制，不是调试入口）。
    /// </remarks>
    public TalentSet Talents => _talents;

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

    /// <summary>天命寿数（年）。**无公开写入通道**；只约束 <c>&gt;= 0</c>，**无上界**（逻辑轨 ⑦）。</summary>
    /// <remarks>唯一例外是 <see cref="DebugSetLifespan"/>（调试控制台）。</remarks>
    public int Lifespan => _lifespan;

    /// <summary>调试写入：改写四项天赋（仅调试控制台调用；正常路径 MUST NOT 使用）。</summary>
    /// <param name="talents">新的四项天赋。</param>
    public void DebugSetTalents(TalentSet talents) => _talents = talents;

    /// <summary>调试写入：改写天命寿数（仅调试控制台调用；正常路径 MUST NOT 使用）。</summary>
    /// <param name="lifespan">新的寿数（年，<c>&gt;= 0</c>）。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lifespan"/> 为负。</exception>
    public void DebugSetLifespan(int lifespan)
    {
        if (lifespan < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifespan), lifespan, "天命寿数 MUST >= 0（规格书 §4.1）。");
        }

        _lifespan = lifespan;
    }
}
