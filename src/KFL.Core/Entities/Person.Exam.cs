using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Core.Entities;

public sealed partial class Person
{
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
}
