using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Core.Entities;

public sealed partial class Person
{
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
    /// <summary>复原「是否已延请名师」标记（**仅供存档层调用**，data-model §7.3）。</summary>
    /// <param name="tutoredByMaster">落盘值。</param>
    /// <remarks>
    /// 本标记本就有公开 setter，故这里只是一条与其它复原通道对称的显式入口，
    /// 不承担任何语义校验。
    /// </remarks>
    internal void RestoreTutoring(bool tutoredByMaster) => _tutoredByMaster = tutoredByMaster;
}
