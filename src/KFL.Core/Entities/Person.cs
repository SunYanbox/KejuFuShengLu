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

    /// <summary>天命寿数（年）。**无公开写入通道**；只约束 <c>&gt;= 0</c>，**无上界**（逻辑轨 ⑦）。</summary>
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

    /// <summary>
    /// 解试**累计失败数**（<c>&gt;= 0</c>，只增不减）。**无公开 setter**；
    /// 唯一写入通道是 <see cref="RecordExamFailure"/>。
    /// </summary>
    /// <remarks>
    /// 它决定**免解**资格（<c>&gt;= 3</c> 后每次解试不再掷录取骰，FR-013）。
    /// 「连坐降级」等降级动作 MUST NOT 清零本计数。
    /// </remarks>
    public int JieShiFailureCount => _jieShiFailureCount;

    /// <summary>
    /// 省试**累计失败数**（<c>&gt;= 0</c>，只增不减）。**无公开 setter**；
    /// 唯一写入通道是 <see cref="RecordExamFailure"/>。
    /// </summary>
    /// <remarks>它决定**特奏名**资格（<c>&gt;= 6</c> 且年满 50，FR-012）。</remarks>
    public int ShengShiFailureCount => _shengShiFailureCount;

    /// <summary>
    /// 解试**行贿累计次数**（<c>&gt;= 0</c>，无上限、无清零）。**无公开 setter**；
    /// 唯一写入通道是 <see cref="RecordBribe"/> 与 <see cref="ResetBribeCounts"/>（FR-031）。
    /// </summary>
    /// <remarks>
    /// 口径（规格书 §7.1；data-model §2.1）：同一考生 × 同一考试级别、**跨科年累计**，
    /// 被抓**不清零**。它是行贿金额、被抓概率与效果乘数 <c>kⁿ</c> 的**唯一输入**；
    /// 乘数本身是派生值，MUST NOT 落成与计数并存的第二真源。
    /// </remarks>
    public int JieShiBribeCount => _jieShiBribeCount;

    /// <summary>省试**行贿累计次数**（<c>&gt;= 0</c>）。**无公开 setter**；写入通道同 <see cref="JieShiBribeCount"/>。</summary>
    public int ShengShiBribeCount => _shengShiBribeCount;

    /// <summary>殿试**行贿累计次数**（<c>&gt;= 0</c>）。**无公开 setter**；写入通道同 <see cref="JieShiBribeCount"/>。</summary>
    public int DianShiBribeCount => _dianShiBribeCount;

    /// <summary>
    /// **本月报名标记**：<c>null</c> = 未报名；非空只允许
    /// <see cref="ExamLevel.JieShi"/> 或 <see cref="ExamLevel.ShengShi"/>（殿试不是报名态）。
    /// **无公开 setter**，唯一写入通道是 <see cref="ApplyForExam"/> / <see cref="ClearExamApplication"/>。
    /// </summary>
    /// <remarks>
    /// 报名窗口 = **考试当月**（幂等，不消耗随机、不读写字账本，FR-004/FR-005）；
    /// 无论中第、落第、免解、弃考还是资格不符，本月结算结束后它 MUST 回到 <c>null</c>。
    /// </remarks>
    public ExamLevel? AppliedExam => _appliedExam;

    /// <summary>
    /// **报名**参加 <paramref name="level"/> 的考试。
    /// </summary>
    /// <param name="level">考试级别；只允许 <see cref="ExamLevel.JieShi"/> 或 <see cref="ExamLevel.ShengShi"/>。</param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="level"/> 为 <see cref="ExamLevel.DianShi"/>；或已有一个**不同**级别的报名标记。
    /// </exception>
    /// <remarks>重复报名**同一**级别是幂等的：不抛异常、不改任何字段。</remarks>
    public void ApplyForExam(ExamLevel level)
    {
        if (level == ExamLevel.DianShi)
        {
            throw new InvalidOperationException(
                "殿试由省试及第自动进入，不存在「报名参加殿试」这一状态（FR-005）。");
        }

        if (level is not (ExamLevel.JieShi or ExamLevel.ShengShi))
        {
            throw new ArgumentOutOfRangeException(
                nameof(level), level, "报名标记只允许解试或省试（EX-13）。");
        }

        if (_appliedExam == level)
        {
            return;
        }

        if (_appliedExam is not null)
        {
            throw new InvalidOperationException(
                $"已有「{_appliedExam}」报名标记，MUST 先清除后才能改报「{level}」（FR-005）。");
        }

        _appliedExam = level;
    }

    /// <summary>清除本月报名标记；标记本为 <c>null</c> 时是 no-op。</summary>
    public void ClearExamApplication() => _appliedExam = null;

    /// <summary>
    /// 记录一次 <paramref name="level"/> 的落第：对应累计失败数 <c>+1</c>（**无上限**）。
    /// </summary>
    /// <param name="level">落第的考试级别；只允许解试或省试。</param>
    /// <exception cref="InvalidOperationException"><paramref name="level"/> 为 <see cref="ExamLevel.DianShi"/>（殿试必中，不存在落第）。</exception>
    public void RecordExamFailure(ExamLevel level)
    {
        switch (level)
        {
            case ExamLevel.JieShi:
                _jieShiFailureCount++;
                break;
            case ExamLevel.ShengShi:
                _shengShiFailureCount++;
                break;
            default:
                throw new InvalidOperationException(
                    "殿试不掷录取骰（100% 中第），MUST NOT 记录殿试落第（FR-009）。");
        }
    }

    /// <summary>
    /// 记录一次 <paramref name="level"/> 的**行贿**：对应累计次数 <c>+1</c>（无上限、无清零）。
    /// </summary>
    /// <param name="level">行贿对应 / 涉及的考试级别；三个级别皆合法。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> 不是已登记的考试级别。</exception>
    /// <remarks>
    /// **被抓不清零、不退贿款**（规格书 §7.1）：是否被抓由调用方决定，本方法只记计数。
    /// 「连坐降级」（<see cref="DegreeChangeCause.PunishmentDemotion"/>）MUST NOT 清零三者。
    /// </remarks>
    public void RecordBribe(ExamLevel level)
    {
        switch (level)
        {
            case ExamLevel.JieShi:
                _jieShiBribeCount++;
                break;
            case ExamLevel.ShengShi:
                _shengShiBribeCount++;
                break;
            case ExamLevel.DianShi:
                _dianShiBribeCount++;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(level), level, "行贿计数只有解试 / 省试 / 殿试三个维度（规格书 §7.1）。");
        }
    }

    /// <summary>
    /// 重置行贿计数（FR-031 的调试入口）。
    /// </summary>
    /// <param name="level">要重置的级别；<c>null</c> ⇒ **三级全清**。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> 不是已登记的考试级别。</exception>
    /// <remarks>
    /// **只**改这三个计数，MUST NOT 触碰乘数以外的任何字段——乘数是计数的派生值，
    /// 重置计数即重置乘数（U4「同行贿叠加」的既有口径）。
    /// </remarks>
    public void ResetBribeCounts(ExamLevel? level)
    {
        switch (level)
        {
            case null:
                _jieShiBribeCount = 0;
                _shengShiBribeCount = 0;
                _dianShiBribeCount = 0;
                break;
            case ExamLevel.JieShi:
                _jieShiBribeCount = 0;
                break;
            case ExamLevel.ShengShi:
                _shengShiBribeCount = 0;
                break;
            case ExamLevel.DianShi:
                _dianShiBribeCount = 0;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(level), level, "行贿计数只有解试 / 省试 / 殿试三个维度（规格书 §7.1）。");
        }
    }

    /// <summary>官阶；<c>null</c> = 无官职（规格书 §8、FR-009）。</summary>
    public OfficialRank? Rank { get; set; }

    /// <summary>
    /// 政绩，<c>&gt;= 0</c>。上限（100）属规格书 §8.2 的规则数值，单点在**逻辑轨 ③** 的
    /// <c>KFL.Rules/Config/OfficialCareerPolicy.MeritMaximum</c>，本类型 MUST NOT 复制它。
    /// </summary>
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
    /// <exception cref="ArgumentException">
    /// 清除某个状态位时，对应的计时字段或（<c>AwaitingPost</c> 的）入仕途径仍非空。
    /// </exception>
    public StatusFlag Status
    {
        get => _status;
        set
        {
            EnsureStatusFlagBacksTimers(value, StatusFlag.ServingSentence, _timers.SentenceRemainingMonths, nameof(value));
            EnsureStatusFlagBacksTimers(value, StatusFlag.ExamBanned, _timers.ExamBanRemainingMonths, nameof(value));
            EnsureStatusFlagBacksTimers(value, StatusFlag.PromotionBanned, _timers.PromotionBanRemainingMonths, nameof(value));
            EnsureStatusFlagBacksTimers(value, StatusFlag.AwaitingPost, _timers.AwaitingPostRemainingMonths, nameof(value));
            EnsureStatusFlagBacksTimers(value, StatusFlag.Ill, _timers.IllnessSeverityBasisPoints, nameof(value));
            EnsureStatusFlagBacksTimers(value, StatusFlag.Ill, _timers.TreatmentRemainingMonths, nameof(value));
            EnsureStatusFlagBacksEntryTrack(value, _entryTrack, nameof(value));
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
            EnsureTimerBackedByStatus(_status, StatusFlag.AwaitingPost, value.AwaitingPostRemainingMonths, nameof(value));
            EnsureTimerBackedByStatus(_status, StatusFlag.Ill, value.IllnessSeverityBasisPoints, nameof(value));
            EnsureTimerBackedByStatus(_status, StatusFlag.Ill, value.TreatmentRemainingMonths, nameof(value));
            _timers = value;
        }
    }

    /// <summary>职业指派。</summary>
    public Occupation Occupation { get; set; }

    /// <summary>
    /// 是否已延请名师（007 新增，规格书 §4.3）。
    /// </summary>
    /// <remarks>
    /// 纯布尔、无范围校验。名师月费**付不起**时本标记**保持**为真，当月只按普通速度成长
    /// （不入贷款、不自动解除）。
    /// </remarks>
    public bool IsTutoredByMaster
    {
        get => _tutoredByMaster;
        set => _tutoredByMaster = value;
    }

    /// <summary>
    /// 在职月数（自授官起算），<c>&gt;= 0</c>；默认 <c>0</c>（FR-013、FR-015、FR-017）。
    /// </summary>
    /// <remarks>
    /// 授官时置 <c>0</c>、逐月 <c>+1</c>、考课判定后重置为 <c>0</c>；
    /// 「满 <c>AppraisalPeriodMonths</c> 个月即考课」的阈值属**逻辑轨 ③** 的
    /// <c>OfficialCareerPolicy</c>，本类型只承载状态与自不变量。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> 为负。</exception>
    public int MonthsInOffice
    {
        get => _monthsInOffice;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), value, "在职月数 MUST >= 0（data-model §2.1 不变量 6）。");
            }

            _monthsInOffice = value;
        }
    }

    /// <summary>
    /// 入仕途径（待阙期间记录）：<c>null</c> = 不在待阙、无可授官的途径。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 它由「及第入仕」入口在置入待阙时**写入一次**，是授官时初始官阶的**唯一来源**
    /// （契约七 §3 条款 6：初始官阶 MUST 按 track 映射）。待阙期满授官时与
    /// <see cref="Timers"/> 的待阙计时一并清空——故它的生命周期与「待阙」严格相同。
    /// </para>
    /// <para>
    /// **为何落成状态而不是每次从 <see cref="DegreeHistory"/> 派生**：途径一旦确定就不应随
    /// 功名记录变化——逻辑轨 ⑥ 的连坐降级会在待阙期内向历史追加一条降级记录（§7.4），
    /// 若在授官时重新派生，一甲进士会被改判成 <c>SpecialTribute</c>。派生只发生在入口
    /// （<c>KFL.Rules/Career/AppointmentEntry.TrackOf</c>）。
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="value"/> 非 <c>null</c>，但「待阙」状态位为假。</exception>
    public AppointmentTrack? EntryTrack
    {
        get => _entryTrack;
        set
        {
            if (value is not null && !_status.HasFlag(StatusFlag.AwaitingPost))
            {
                throw new ArgumentException(
                    "入仕途径仅在「AwaitingPost」状态位为真时可非空（data-model §2.1 不变量 5）。",
                    nameof(value));
            }

            _entryTrack = value;
        }
    }

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

    /// <summary>
    /// 复原三个科举进度成员（**仅供存档层调用**，data-model §3.11）。
    /// </summary>
    /// <param name="jieShiFailureCount">解试累计失败数；<c>&gt;= 0</c>。</param>
    /// <param name="shengShiFailureCount">省试累计失败数；<c>&gt;= 0</c>。</param>
    /// <param name="appliedExam">报名标记；<c>null</c> 或 <see cref="ExamLevel.JieShi"/> / <see cref="ExamLevel.ShengShi"/>。</param>
    /// <exception cref="ArgumentOutOfRangeException">任一失败数为负。</exception>
    /// <exception cref="ArgumentException"><paramref name="appliedExam"/> 不是合法的报名态。</exception>
    /// <remarks>
    /// 这是这三个成员**唯一**的非公开写入通道：它们没有公开 setter，也不经由
    /// <see cref="ApplyForExam"/> / <see cref="RecordExamFailure"/> 的语义校验
    /// （否则无法还原「失败数 &gt; 0 但月内无报名」这类合法状态）。
    /// </remarks>
    internal void RestoreExamProgress(int jieShiFailureCount, int shengShiFailureCount, ExamLevel? appliedExam)
    {
        if (jieShiFailureCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(jieShiFailureCount), jieShiFailureCount, "解试累计失败数 MUST >= 0（FR-013）。");
        }

        if (shengShiFailureCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(shengShiFailureCount), shengShiFailureCount, "省试累计失败数 MUST >= 0（FR-012）。");
        }

        if (appliedExam is not (null or ExamLevel.JieShi or ExamLevel.ShengShi))
        {
            throw new ArgumentException(
                "报名标记只允许 null / 解试 / 省试，殿试不是合法报名态（EX-13）。", nameof(appliedExam));
        }

        _jieShiFailureCount = jieShiFailureCount;
        _shengShiFailureCount = shengShiFailureCount;
        _appliedExam = appliedExam;
    }

    /// <summary>
    /// 复原三个行贿计数（**仅供存档层调用**，data-model §7.3）。
    /// </summary>
    /// <param name="jieShiBribeCount">解试行贿累计次数；<c>&gt;= 0</c>。</param>
    /// <param name="shengShiBribeCount">省试行贿累计次数；<c>&gt;= 0</c>。</param>
    /// <param name="dianShiBribeCount">殿试行贿累计次数；<c>&gt;= 0</c>。</param>
    /// <exception cref="ArgumentOutOfRangeException">任一计数为负。</exception>
    /// <remarks>
    /// 这是这三个成员**唯一**的非公开写入通道：它们没有公开 setter，且「落盘值 &gt; 0」
    /// 不必然等于「刚行贿过」，故不走 <see cref="RecordBribe"/> 的语义校验（FR-032）。
    /// </remarks>
    internal void RestoreBribeCounts(int jieShiBribeCount, int shengShiBribeCount, int dianShiBribeCount)
    {
        if (jieShiBribeCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(jieShiBribeCount), jieShiBribeCount, "解试行贿累计次数 MUST >= 0（FR-032）。");
        }

        if (shengShiBribeCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(shengShiBribeCount), shengShiBribeCount, "省试行贿累计次数 MUST >= 0（FR-032）。");
        }

        if (dianShiBribeCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dianShiBribeCount), dianShiBribeCount, "殿试行贿累计次数 MUST >= 0（FR-032）。");
        }

        _jieShiBribeCount = jieShiBribeCount;
        _shengShiBribeCount = shengShiBribeCount;
        _dianShiBribeCount = dianShiBribeCount;
    }

    /// <summary>复原「是否已延请名师」标记（**仅供存档层调用**，data-model §7.3）。</summary>
    /// <param name="tutoredByMaster">落盘值。</param>
    /// <remarks>
    /// 本标记本就有公开 setter，故这里只是一条与其它复原通道对称的显式入口，
    /// 不承担任何语义校验。
    /// </remarks>
    internal void RestoreTutoring(bool tutoredByMaster) => _tutoredByMaster = tutoredByMaster;

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

    private static void EnsureStatusFlagBacksEntryTrack(
        StatusFlag status, AppointmentTrack? entryTrack, string paramName)
    {
        if (!status.HasFlag(StatusFlag.AwaitingPost) && entryTrack is not null)
        {
            throw new ArgumentException(
                "清除「AwaitingPost」状态位前 MUST 先把入仕途径置空（data-model §2.1 不变量 5）。", paramName);
        }
    }
}
