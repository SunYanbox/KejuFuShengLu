using KFL.Core.ValueObjects;

namespace KFL.Core.Entities;

/// <summary>
/// 家族生命周期状态的唯一载体：买人口计数 / 待分娩 / 生育申请 / 待应答提议（007 的 data-model §2.5）。
/// </summary>
/// <remarks>
/// <para>
/// 经 <see cref="Family.Lifecycle"/> 暴露。所有写入通道都在本类型上，
/// <see cref="Family"/> MUST NOT 为此新增任何写入方法。
/// </para>
/// <para>
/// **两个 Year 字段不可省**：跨年重置的唯一载体，MUST NOT 靠 <c>GameState.CurrentDate</c> 反推
/// （否则存档复原后重置时点会漂移）。开局均为 <c>1</c>。
/// </para>
/// <para>
/// **明确不含**：绝嗣判定、成就、族谱绘制、怀孕率。
/// </para>
/// </remarks>
public sealed class FamilyLifecycleState
{
    private readonly List<PendingBirth> _pendingBirths = [];
    private readonly List<CoupleKey> _appliedBirthCouples = [];
    private readonly IReadOnlyList<PendingBirth> _pendingBirthsView;
    private readonly IReadOnlyList<CoupleKey> _appliedBirthCouplesView;

    /// <summary>构造空状态。</summary>
    public FamilyLifecycleState()
    {
        _pendingBirthsView = _pendingBirths.AsReadOnly();
        _appliedBirthCouplesView = _appliedBirthCouples.AsReadOnly();
    }

    /// <summary>该计数所属自然年内已买人口数（跨年由 <see cref="NextPurchaseOrdinal"/> 重置）。</summary>
    public int PurchaseCount { get; private set; }

    /// <summary>买人口计数所属的自然年（单调不减，开局 <c>1</c>）。</summary>
    public int PurchaseYear { get; private set; } = 1;

    /// <summary>待分娩队列（次月 ⑧-b 消费）。</summary>
    public IReadOnlyList<PendingBirth> PendingBirths => _pendingBirthsView;

    /// <summary>本年度已申请生育的夫妇集合。</summary>
    public IReadOnlyList<CoupleKey> AppliedBirthCouples => _appliedBirthCouplesView;

    /// <summary>生育申请所属的自然年（单调不减，开局 <c>1</c>）。</summary>
    public int BirthApplicationYear { get; private set; } = 1;

    /// <summary>待应答提议；<c>null</c> = 无提议（已应答或已失效并清空）。</summary>
    public MarriageProposal? Proposal { get; private set; }

    /// <summary>纯读取「本年第几名」的价格名次（**零副作用**）。</summary>
    /// <param name="year">当前自然年。</param>
    /// <returns>本年第 <c>n</c> 名的 <c>n</c>（1 起）。</returns>
    public int PeekPurchaseOrdinal(int year) => PurchaseYear == year ? PurchaseCount + 1 : 1;

    /// <summary>提交本年度名次并返回它（**带副作用，MUST 只在扣款成功后调用**）。</summary>
    /// <param name="year">当前自然年。</param>
    /// <returns>本年第 <c>n</c> 名的 <c>n</c>（1 起）。</returns>
    public int NextPurchaseOrdinal(int year)
    {
        if (PurchaseYear != year)
        {
            PurchaseYear = year;
            PurchaseCount = 0;
        }

        PurchaseCount++;
        return PurchaseCount;
    }

    /// <summary>安排一次次月分娩。</summary>
    /// <param name="birth">待分娩记录。</param>
    public void ScheduleBirth(PendingBirth birth) => _pendingBirths.Add(birth);

    /// <summary>清空待分娩队列（⑧-b 处理完毕后调用一次）。</summary>
    public void ConsumeBirths() => _pendingBirths.Clear();

    /// <summary>
    /// 清空**已到期**的待分娩记录（<see cref="PendingBirth.AppliedAt"/> 早于当月的），
    /// 保留当月新提交、须待次月才分娩的申请（契约十九 §7「申请当月不分娩」）。
    /// </summary>
    /// <param name="month">当月。</param>
    public void ConsumeBirths(GameDate month) =>
        _pendingBirths.RemoveAll(birth => birth.AppliedAt < month);

    /// <summary>该夫妇在本自然年是否已申请过生育。</summary>
    /// <param name="couple">夫妇键。</param>
    /// <param name="year">当前自然年。</param>
    /// <returns>已申请 ⇒ <c>true</c>。</returns>
    public bool HasAppliedForBirth(CoupleKey couple, int year) =>
        BirthApplicationYear == year && _appliedBirthCouples.Contains(couple);

    /// <summary>登记一次生育申请（跨年自动清空上一年度的集合）。</summary>
    /// <param name="couple">夫妇键。</param>
    /// <param name="year">当前自然年。</param>
    public void RecordBirthApplication(CoupleKey couple, int year)
    {
        if (BirthApplicationYear != year)
        {
            BirthApplicationYear = year;
            _appliedBirthCouples.Clear();
        }

        if (!_appliedBirthCouples.Contains(couple))
        {
            _appliedBirthCouples.Add(couple);
        }
    }

    /// <summary>设置待应答提议；<c>null</c> = 应答或失效后清空。</summary>
    /// <param name="proposal">新提议。</param>
    public void SetProposal(MarriageProposal? proposal) => Proposal = proposal;

    /// <summary>存档复原用的瞬时快照（只承载数据，不承载校验）。</summary>
    /// <returns>六项状态。</returns>
    internal (
        int PurchaseCount,
        int PurchaseYear,
        IReadOnlyList<PendingBirth> PendingBirths,
        IReadOnlyList<CoupleKey> AppliedBirthCouples,
        int BirthApplicationYear,
        MarriageProposal? Proposal) CaptureBookkeeping() =>
        (PurchaseCount, PurchaseYear, [.. _pendingBirths], [.. _appliedBirthCouples], BirthApplicationYear, Proposal);

    /// <summary>存档复原通道（一致性由存档层的 <c>SaveStateValidator</c> 负责）。</summary>
    /// <param name="purchaseCount">已买人口数。</param>
    /// <param name="purchaseYear">计数所属自然年。</param>
    /// <param name="pendingBirths">待分娩队列。</param>
    /// <param name="appliedBirthCouples">本年度已申请生育的夫妇。</param>
    /// <param name="birthApplicationYear">生育申请所属自然年。</param>
    /// <param name="proposal">待应答提议。</param>
    internal void RestoreBookkeeping(
        int purchaseCount,
        int purchaseYear,
        IEnumerable<PendingBirth> pendingBirths,
        IEnumerable<CoupleKey> appliedBirthCouples,
        int birthApplicationYear,
        MarriageProposal? proposal)
    {
        PurchaseCount = purchaseCount;
        PurchaseYear = purchaseYear;
        _pendingBirths.Clear();
        _pendingBirths.AddRange(pendingBirths);
        _appliedBirthCouples.Clear();
        _appliedBirthCouples.AddRange(appliedBirthCouples);
        BirthApplicationYear = birthApplicationYear;
        Proposal = proposal;
    }
}
