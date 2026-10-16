using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Core.Entities;

/// <summary>
/// **当月事实缓冲**：承载「已发生但尚未被某次结算报告」的贿赂与惩罚事实（FR-028、FR-030）。
/// </summary>
/// <remarks>
/// <para>
/// 它**不是**任何状态的第二真源——状态的真源仍是 <see cref="Person"/> 的计时 / 状态位与家族账本
/// （data-model §2.3 口径 1）；日志只回答「这个月发生过什么」。
/// </para>
/// <para>
/// **只追加**：除结算第③步的 <see cref="Clear"/> 之外，没有任何改写或删除路径（口径 3）。
/// 集合**按 <c>PersonId</c> 升序**排列（并列时按 <c>Level</c> / <c>Relative</c> 再排序），
/// 与快照的断言面一致（口径 2）。
/// </para>
/// <para>
/// 成员集**含归档成员**的 <c>PersonId</c>——连坐会处死当事人、也会跨族追及已出嫁女儿，
/// 被追及者可能已不在在册集内（口径 4）。
/// </para>
/// <para>
/// 追加不消费随机、也不产生任何副作用；<see cref="Append(BribeFact)"/> 等六个重载与
/// <see cref="MarkFamilyBanDeclared"/> 均可被规则层自由调用。
/// </para>
/// </remarks>
public sealed class PunishmentJournal
{
    private readonly List<BribeFact> _bribes = [];
    private readonly List<SentenceFact> _sentences = [];
    private readonly List<FineFact> _fines = [];
    private readonly List<RelativeBanFact> _relativeBans = [];
    private readonly List<CollectivePunishmentFact> _collectivePunishments = [];
    private readonly List<ReleaseFact> _releases = [];

    /// <summary>六类事实集合是否**全部为空**（与 <see cref="FamilyBanDeclared"/> 无关）。</summary>
    public bool IsEmpty =>
        _bribes.Count == 0 &&
        _sentences.Count == 0 &&
        _fines.Count == 0 &&
        _relativeBans.Count == 0 &&
        _collectivePunishments.Count == 0 &&
        _releases.Count == 0;

    /// <summary>
    /// 本缓冲所在月份是否发生过「宣告**全族**禁考禁升」——地狱殿试 × 被抓的连坐分支
    /// （契约十六 §6）。由 <see cref="MarkFamilyBanDeclared"/> 置位，
    /// 随 <see cref="Clear"/> 复位，因此**只在宣告当月**为真。
    /// </summary>
    public bool FamilyBanDeclared { get; private set; }

    /// <summary>行贿事实行（按 <c>Examinee</c> 升序，并列按 <c>Level</c>）。</summary>
    public IReadOnlyList<BribeFact> Bribes => _bribes;

    /// <summary>服刑建立事实行（按 <c>Subject</c> 升序）。</summary>
    public IReadOnlyList<SentenceFact> Sentences => _sentences;

    /// <summary>罚金事实行（按 <c>Subject</c> 升序）。</summary>
    public IReadOnlyList<FineFact> Fines => _fines;

    /// <summary>亲属禁考 / 禁升事实行（按 <c>Subject</c> 升序，并列按 <c>Relative</c>）。</summary>
    public IReadOnlyList<RelativeBanFact> RelativeBans => _relativeBans;

    /// <summary>连坐后果事实行（按 <c>Subject</c> 升序，并列按 <c>Relative</c>）。</summary>
    public IReadOnlyList<CollectivePunishmentFact> CollectivePunishments => _collectivePunishments;

    /// <summary>服刑期满释放事实行（按 <c>Subject</c> 升序）。</summary>
    public IReadOnlyList<ReleaseFact> Releases => _releases;

    /// <summary>追加一条行贿事实（每次行贿**恰一条**，无论是否被抓；FR-028）。</summary>
    public void Append(BribeFact fact) => Insert(_bribes, fact, static (a, b) => Compare(a.Examinee, a.Level, b.Examinee, b.Level));

    /// <summary>追加一条服刑建立事实（每次判服刑**恰一条**）。</summary>
    public void Append(SentenceFact fact) => Insert(_sentences, fact, static (a, b) => Compare(a.Subject, b.Subject));

    /// <summary>追加一条罚金事实（金额为 0 时不追加；见 <see cref="PunishmentJournal"/> 口径 3）。</summary>
    public void Append(FineFact fact) => Insert(_fines, fact, static (a, b) => Compare(a.Subject, b.Subject));

    /// <summary>追加一条亲属禁考 / 禁升事实（逐人一条）。</summary>
    public void Append(RelativeBanFact fact) => Insert(_relativeBans, fact, static (a, b) => Compare(a.Subject, a.Relative, b.Subject, b.Relative));

    /// <summary>追加一条连坐后果事实（逐人一条）。</summary>
    public void Append(CollectivePunishmentFact fact) => Insert(_collectivePunishments, fact, static (a, b) => Compare(a.Subject, a.Relative, b.Subject, b.Relative));

    /// <summary>追加一条释放事实（每次释放**恰一条**，含无官职者）。</summary>
    public void Append(ReleaseFact fact) => Insert(_releases, fact, static (a, b) => Compare(a.Subject, b.Subject));

    /// <summary>宣告本月发生「全族禁考禁升」；重复调用是幂等的。</summary>
    public void MarkFamilyBanDeclared() => FamilyBanDeclared = true;

    /// <summary>
    /// 清空整个缓冲（六类集合 + <see cref="FamilyBanDeclared"/>）。
    /// **唯一调用点是结算第③步**——投影出快照之后立即清空，使每条事实只被报告一次（FR-028）。
    /// </summary>
    /// <remarks>
    /// 可见性**必须是公开**的：唯一调用方 <c>PunishmentAdvance.Run</c> 在 <c>KFL.Rules</c> 里，
    /// 而 <c>KFL.Core/AssemblyInfo.cs</c> 只对 <c>KFL.Infrastructure</c> 放行 internal
    /// （「MUST NOT 在此放行任何其它程序集」），故本方法不能是 internal。
    /// 它是本缓冲的**唯一**改写通道（口径 3：只追加 + 每月一次清空）。
    /// </remarks>
    public void Clear()
    {
        _bribes.Clear();
        _sentences.Clear();
        _fines.Clear();
        _relativeBans.Clear();
        _collectivePunishments.Clear();
        _releases.Clear();
        FamilyBanDeclared = false;
    }

    /// <summary>
    /// 从存档复原**整份**缓冲。**唯一调用点是 <c>SaveStateMapper</c>**（复原第 ⑦ 步之后、第 ⑧ 步之前），
    /// 与 <see cref="Clear"/> 一样不做语义校验——存档是既成事实（FR-032）。
    /// </summary>
    internal void Restore(
        bool familyBanDeclared,
        IEnumerable<BribeFact>? bribes,
        IEnumerable<SentenceFact>? sentences,
        IEnumerable<FineFact>? fines,
        IEnumerable<RelativeBanFact>? relativeBans,
        IEnumerable<CollectivePunishmentFact>? collectivePunishments,
        IEnumerable<ReleaseFact>? releases)
    {
        ArgumentNullException.ThrowIfNull(bribes);
        ArgumentNullException.ThrowIfNull(sentences);
        ArgumentNullException.ThrowIfNull(fines);
        ArgumentNullException.ThrowIfNull(relativeBans);
        ArgumentNullException.ThrowIfNull(collectivePunishments);
        ArgumentNullException.ThrowIfNull(releases);

        Clear();
        FamilyBanDeclared = familyBanDeclared;

        foreach (BribeFact fact in bribes)
        {
            Append(fact);
        }

        foreach (SentenceFact fact in sentences)
        {
            Append(fact);
        }

        foreach (FineFact fact in fines)
        {
            Append(fact);
        }

        foreach (RelativeBanFact fact in relativeBans)
        {
            Append(fact);
        }

        foreach (CollectivePunishmentFact fact in collectivePunishments)
        {
            Append(fact);
        }

        foreach (ReleaseFact fact in releases)
        {
            Append(fact);
        }
    }

    private static int Compare(PersonId left, PersonId right)
        => left.Value.CompareTo(right.Value);

    private static int Compare(PersonId left, ExamLevel leftTie, PersonId right, ExamLevel rightTie)
    {
        int byId = Compare(left, right);
        return byId != 0 ? byId : leftTie.CompareTo(rightTie);
    }

    private static int Compare(PersonId left, PersonId leftTie, PersonId right, PersonId rightTie)
    {
        int byId = Compare(left, right);
        return byId != 0 ? byId : Compare(leftTie, rightTie);
    }

    /// <summary>
    /// 按 <paramref name="comparer"/> 的升序把 <paramref name="fact"/> 插到首个「大于自己」的行之前，
    /// 从而在纯追加的前提下维持集合有序（口径 2）。
    /// </summary>
    private static void Insert<T>(List<T> rows, T fact, Comparison<T> comparer)
    {
        int index = rows.Count;
        for (int i = 0; i < rows.Count; i++)
        {
            if (comparer(fact, rows[i]) < 0)
            {
                index = i;
                break;
            }
        }

        rows.Insert(index, fact);
    }
}
