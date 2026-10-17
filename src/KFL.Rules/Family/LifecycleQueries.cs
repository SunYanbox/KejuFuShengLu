using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;
using KFL.Rules.Economy;
using KFL.Rules.Settlement;

namespace KFL.Rules.FamilyLifecycle;

/// <summary>托媒可行性预览（FR-044；金额用区间，因为对方阶层未定且查询面 MUST NOT 消费随机）。</summary>
/// <param name="Available">当前是否可托媒。</param>
/// <param name="RejectReason">不可用时的原因。</param>
/// <param name="DegreeMarriageRejectReason">买功名婚姻选项不可用时的原因。</param>
/// <param name="Direction">本方向。</param>
/// <param name="FamilyClass">本家阶层。</param>
/// <param name="MatchmakerFee">托媒费（含支出系数）。</param>
/// <param name="BetrothalGiftLower">聘礼下限（农/工档，娶入为我方支出、嫁出为我方收入）。</param>
/// <param name="BetrothalGiftUpper">聘礼上限（仕族档）。</param>
/// <param name="DowryLower">嫁妆下限（农/工档）。</param>
/// <param name="DowryUpper">嫁妆上限（仕族档）。</param>
/// <param name="DegreeMarriageAvailable">买功名婚姻选项是否可用。</param>
/// <param name="DegreeMarriageFee">买功名婚姻的功名费。</param>
public sealed record MarriageFeasibility(
    bool Available,
    LifecycleRejectReason RejectReason,
    LifecycleRejectReason DegreeMarriageRejectReason,
    MarriageDirection Direction,
    MarriageClass FamilyClass,
    Money MatchmakerFee,
    Money BetrothalGiftLower,
    Money BetrothalGiftUpper,
    Money DowryLower,
    Money DowryUpper,
    bool DegreeMarriageAvailable,
    Money DegreeMarriageFee);

/// <summary>生育申请可行性（FR-044）。</summary>
/// <param name="Available">当前是否可申请。</param>
/// <param name="RejectReason">不可用时原因。</param>
/// <param name="WifeAge">女方当前年龄。</param>
/// <param name="AlreadyAppliedThisYear">本年度是否已为这对夫妇申请过。</param>
/// <param name="Cost">分娩费用（含支出系数）。</param>
/// <param name="Affordable">现银 + 储蓄是否足额。</param>
public sealed record ChildbirthFeasibility(
    bool Available,
    LifecycleRejectReason RejectReason,
    int WifeAge,
    bool AlreadyAppliedThisYear,
    Money Cost,
    bool Affordable);

/// <summary>买人口报价（FR-044）。</summary>
/// <param name="Available">当前是否可买。</param>
/// <param name="RejectReason">不可用时原因。</param>
/// <param name="Ordinal">本年内第几名（1 起）。</param>
/// <param name="Price">该名次的价格。</param>
/// <param name="Affordable">现银 + 储蓄是否足额。</param>
public sealed record PurchaseQuote(
    bool Available,
    LifecycleRejectReason RejectReason,
    int Ordinal,
    Money Price,
    bool Affordable);

/// <summary>疾病状态快照（FR-044 / FR-048：程度用整数磅值，不用小数）。</summary>
/// <param name="Ill">是否患病。</param>
/// <param name="SeverityBasisPoints">疾病程度磅值（0~10000）；未患病为 <c>null</c>。</param>
/// <param name="TreatmentRemainingMonths">疗程剩余月数（0~3）；未在疗程中为 <c>null</c>。</param>
public sealed record IllnessStatus(
    bool Ill,
    int? SeverityBasisPoints,
    int? TreatmentRemainingMonths);

/// <summary>
/// 生命周期查询面（data-model §3.4 / FR-044）：**全部为纯函数**——不写任何状态、不消费随机、不建视图。
/// </summary>
/// <remarks>
/// <para>
/// 这些判据同时是入口（<see cref="LifecycleEntry"/>）与 ⑧-a（<see cref="MarriageMarket"/>）的**单一判据**
/// 来源，避免同一规则出现两处实现。
/// </para>
/// <para>
/// <see cref="ClassOf"/> 的签名与 data-model §3.4 的 <c>(Family, Person)</c> 不同：阶层判据要读
/// <see cref="Origin"/>，而 <see cref="Origin"/> 只挂在 <see cref="GameState"/> 上（<see cref="Family"/> /
/// <see cref="Person"/> 都没有）⇒ 改为 `(GameState, Person)`；Phase 9 回写登记。
/// </para>
/// </remarks>
public static class LifecycleQueries
{
    /// <summary>本方向下当月是否可托媒（契约十九 §2 第 1 条：在册未婚 + 成年 + 方向一致 + 无未应答提议）。</summary>
    /// <param name="family">家族。</param>
    /// <param name="member">我方成员。</param>
    /// <param name="direction">婚配方向。</param>
    /// <param name="date">判定月份。</param>
    /// <returns>可托媒时为 <c>true</c>。</returns>
    public static bool IsMarriageable(Family family, Person member, MarriageDirection direction, GameDate date)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(member);

        if (!CountedMembers.IsRegistered(member) || family.TryGet(member.Id) is null)
        {
            return false;
        }

        if (member.SpouseId is not null)
        {
            return false;
        }

        if (DirectionOf(member.Gender) != direction)
        {
            return false;
        }

        if (!AgeBracketPolicy.IsAdult(member.Gender, member.AgeAt(date)))
        {
            return false;
        }

        return PendingProposal(family, date) is null;
    }

    /// <summary>成员性别对应的婚配方向（男 ⇒ 娶入、女 ⇒ 嫁出）。</summary>
    /// <param name="gender">成员性别。</param>
    /// <returns>方向。</returns>
    public static MarriageDirection DirectionOf(Gender gender) =>
        gender == Gender.Male ? MarriageDirection.BrideInto : MarriageDirection.GroomOut;

    /// <summary>本家阶层的单一判据（契约十九 §1 / FR-015）：<c>HasShiStatus</c> 优先于出身。</summary>
    /// <param name="state">游戏状态（提供 <see cref="GameState.Origin"/> 与家族）。</param>
    /// <param name="person">被判定者；当前判据只用家族与出身，参数保留供后续按人细分。</param>
    /// <returns>阶层。</returns>
    public static MarriageClass ClassOf(GameState state, Person person)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(person);

        if (state.Family.HasShiStatus)
        {
            return MarriageClass.Official;
        }

        return state.Origin is Origin.Merchant or Origin.Scholar
            ? MarriageClass.Scholar
            : MarriageClass.Commoner;
    }

    /// <summary>低一级功名（FR-017）：进士→贡士、贡士→举人；举人 / 白身无更低档。</summary>
    /// <param name="degree">当前功名。</param>
    /// <returns>低一级功名；无更低档时为 <c>null</c>。</returns>
    public static DegreeLevel? LowerDegreeOf(DegreeLevel degree) => degree switch
    {
        DegreeLevel.JinShi => DegreeLevel.GongShi,
        DegreeLevel.GongShi => DegreeLevel.JuRen,
        _ => null,
    };

    /// <summary>托媒费：<c>5 贯 × 支出系数</c>（规格书 §9.1）。</summary>
    /// <param name="state">游戏状态（提供难度）。</param>
    /// <returns>托媒费。</returns>
    public static Money MatchmakerFee(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Money.FromGuan(
            MarriagePolicy.MatchmakerFeeGuan * DifficultyRates.ExpenseFactor(state.Difficulty));
    }

    /// <summary>对方阶层的聘礼：<c>金额表 × 支出系数</c>（规格书 §9.2）。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="counterpartClass">对方阶层。</param>
    /// <returns>聘礼金额。</returns>
    public static Money GiftAmount(GameState state, MarriageClass counterpartClass)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Money.FromGuan(
            GiftGuanOf(counterpartClass) * DifficultyRates.ExpenseFactor(state.Difficulty));
    }

    /// <summary>对方阶层的嫁妆：<c>金额表 × 支出系数</c>（规格书 §9.2）。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="counterpartClass">对方阶层。</param>
    /// <returns>嫁妆金额。</returns>
    public static Money DowryAmount(GameState state, MarriageClass counterpartClass)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Money.FromGuan(
            DowryGuanOf(counterpartClass) * DifficultyRates.ExpenseFactor(state.Difficulty));
    }

    /// <summary>嫁出时本家承担的嫁妆（本家阶层，规格书 §9.2 第 2 行）。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="member">外嫁成员。</param>
    /// <returns>嫁妆金额。</returns>
    public static Money OwnDowryAmount(GameState state, Person member)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(member);
        return DowryAmount(state, ClassOf(state, member));
    }

    /// <summary>买功名婚姻的功名费：<c>自己年俸 × 150%</c>（规格书 §9.3；不乘支出系数）。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="member">我方成员。</param>
    /// <returns>功名费；无官职时为 0。</returns>
    public static Money DegreeMarriageFee(GameState state, Person member)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(member);

        return member.Rank is { } rank
            ? Money.FromGuan(SalaryTable.AnnualSalaryGuan(rank.Level) * MarriagePolicy.DegreeMarriageFeeRatio)
            : Money.Zero;
    }

    /// <summary>分娩费用：<c>10 贯 × 支出系数</c>（规格书 §9.4）。</summary>
    /// <param name="state">游戏状态。</param>
    /// <returns>分娩费用。</returns>
    public static Money ChildbirthCost(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Money.FromGuan(
            ChildbirthPolicy.ChildbirthExpenseGuan * DifficultyRates.ExpenseFactor(state.Difficulty));
    }

    /// <summary>托媒可行性预览（含金额区间与买功名选项）。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="member">我方成员。</param>
    /// <param name="direction">婚配方向。</param>
    /// <returns>可行性快照。</returns>
    public static MarriageFeasibility MatchmakeFeasibility(
        GameState state,
        Person member,
        MarriageDirection direction)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(member);

        var available = IsMarriageable(state.Family, member, direction, state.CurrentDate);
        var lowerDegree = LowerDegreeOf(member.CurrentDegree);
        var degreeAvailable = state.Family.HasShiStatus && member.Rank is not null && lowerDegree is not null;
        var degreeReason = degreeAvailable
            ? LifecycleRejectReason.None
            : state.Family.HasShiStatus && member.Rank is not null
                ? LifecycleRejectReason.NoLowerDegree
                : LifecycleRejectReason.NotMarriageable;

        var factor = DifficultyRates.ExpenseFactor(state.Difficulty);
        var classForDowry = direction == MarriageDirection.BrideInto
            ? MarriageClass.Official
            : ClassOf(state, member);
        var ownDowry = DowryAmount(state, classForDowry);

        return new MarriageFeasibility(
            Available: available,
            RejectReason: available ? LifecycleRejectReason.None : LifecycleRejectReason.NotMarriageable,
            DegreeMarriageRejectReason: degreeReason,
            Direction: direction,
            FamilyClass: ClassOf(state, member),
            MatchmakerFee: MatchmakerFee(state),
            BetrothalGiftLower: Money.FromGuan(MarriagePolicy.CommonerGiftGuan * factor),
            BetrothalGiftUpper: Money.FromGuan(MarriagePolicy.OfficialGiftGuan * factor),
            DowryLower: direction == MarriageDirection.BrideInto
                ? Money.FromGuan(MarriagePolicy.CommonerDowryGuan * factor)
                : ownDowry,
            DowryUpper: direction == MarriageDirection.BrideInto
                ? Money.FromGuan(MarriagePolicy.OfficialDowryGuan * factor)
                : ownDowry,
            DegreeMarriageAvailable: degreeAvailable,
            DegreeMarriageFee: degreeAvailable ? DegreeMarriageFee(state, member) : Money.Zero);
    }

    /// <summary>我方全部可婚配成员（在册、未婚、成年），按 <see cref="PersonId"/> 升序。</summary>
    /// <param name="family">家族。</param>
    /// <param name="date">判定月份。</param>
    /// <returns>候选成员列表。</returns>
    public static IReadOnlyList<Person> MarriageCandidateMembers(Family family, GameDate date)
    {
        ArgumentNullException.ThrowIfNull(family);

        return CountedMembers.Registered(family)
            .Where(member => member.SpouseId is null
                && AgeBracketPolicy.IsAdult(member.Gender, member.AgeAt(date)))
            .OrderBy(member => member.Id.Value)
            .ToList();
    }

    /// <summary>当月未失效的提议（当月末即失效，非字段判据）。</summary>
    /// <param name="family">家族。</param>
    /// <param name="date">判定月份。</param>
    /// <returns>提议；无或已过期时为 <c>null</c>。</returns>
    public static MarriageProposal? PendingProposal(Family family, GameDate date)
    {
        ArgumentNullException.ThrowIfNull(family);

        var proposal = family.Lifecycle.Proposal;
        return proposal is not null && proposal.ProposedAt.Year == date.Year && proposal.ProposedAt.Month == date.Month
            ? proposal
            : null;
    }

    /// <summary>成员当前辈分（data-model §3.4；血亲出生即定、外来者由家族指定）。</summary>
    /// <param name="person">成员。</param>
    /// <returns>辈分。</returns>
    public static int GenerationOf(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);
        return person.Generation;
    }

    /// <summary>成员的疾病状态（程度用整数磅值，见 FR-048）。</summary>
    /// <param name="person">成员。</param>
    /// <returns>疾病快照。</returns>
    public static IllnessStatus IllnessOf(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        return new IllnessStatus(
            Ill: (person.Status & StatusFlag.Ill) != StatusFlag.None,
            SeverityBasisPoints: person.Timers.IllnessSeverityBasisPoints,
            TreatmentRemainingMonths: person.Timers.TreatmentRemainingMonths);
    }

    /// <summary>生育资格（不含资金判定）：双方在册、互为配偶、女方在 16~45 岁、本年未申请。</summary>
    /// <param name="family">家族。</param>
    /// <param name="couple">夫妇键。</param>
    /// <param name="date">判定月份。</param>
    /// <returns>可申请时为 <c>true</c>。</returns>
    public static bool CanApplyForChildbirth(Family family, CoupleKey couple, GameDate date)
    {
        ArgumentNullException.ThrowIfNull(family);

        var husband = family.TryGet(couple.HusbandId);
        var wife = family.TryGet(couple.WifeId);
        if (husband is null || wife is null
            || !CountedMembers.IsRegistered(husband)
            || !CountedMembers.IsRegistered(wife)
            || husband.SpouseId != wife.Id
            || wife.SpouseId != husband.Id)
        {
            return false;
        }

        var wifeAge = wife.AgeAt(date);
        if (wifeAge < ChildbirthPolicy.FertilityAgeMin || wifeAge > ChildbirthPolicy.FertilityAgeMax)
        {
            return false;
        }

        return !family.Lifecycle.HasAppliedForBirth(couple, date.Year);
    }

    /// <summary>生育可行性预览（资格 + 资金）。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="couple">夫妇键。</param>
    /// <param name="date">判定月份。</param>
    /// <returns>可行性快照。</returns>
    public static ChildbirthFeasibility ChildbirthFeasibility(
        GameState state,
        CoupleKey couple,
        GameDate date)
    {
        ArgumentNullException.ThrowIfNull(state);

        var family = state.Family;
        var wife = family.TryGet(couple.WifeId);
        var wifeAge = wife?.AgeAt(date) ?? 0;
        var applied = family.Lifecycle.HasAppliedForBirth(couple, date.Year);
        var cost = ChildbirthCost(state);
        var affordable = PaymentPrimitive.CanCover(state.Economy, cost);

        var reason = LifecycleRejectReason.None;
        if (!CanApplyForChildbirth(family, couple, date))
        {
            reason = applied
                ? LifecycleRejectReason.AlreadyAppliedThisYear
                : wifeAge is >= ChildbirthPolicy.FertilityAgeMin and <= ChildbirthPolicy.FertilityAgeMax
                    ? LifecycleRejectReason.NotMarriageable
                    : LifecycleRejectReason.FertilityWindowClosed;
        }
        else if (!affordable)
        {
            reason = LifecycleRejectReason.InsufficientFunds;
        }

        return new ChildbirthFeasibility(
            Available: reason == LifecycleRejectReason.None,
            RejectReason: reason,
            WifeAge: wifeAge,
            AlreadyAppliedThisYear: applied,
            Cost: cost,
            Affordable: affordable);
    }

    /// <summary>买人口报价：第 n 名 = <c>10 × n 贯</c>（规格书 §9.5；不乘支出系数，每年 1 月重置）。</summary>
    /// <param name="state">游戏状态。</param>
    /// <returns>报价快照。</returns>
    public static PurchaseQuote PurchaseQuote(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var family = state.Family;
        var ordinal = family.Lifecycle.PeekPurchaseOrdinal(state.CurrentDate.Year);
        var price = Money.FromGuan(PopulationPurchasePolicy.BasePriceGuan * ordinal);
        var affordable = PaymentPrimitive.CanCover(state.Economy, price);

        var reason = LifecycleRejectReason.None;
        if (family.HeadId is null)
        {
            reason = LifecycleRejectReason.HeadVacant;
        }
        else if (!affordable)
        {
            reason = LifecycleRejectReason.InsufficientFunds;
        }

        return new PurchaseQuote(
            Available: reason == LifecycleRejectReason.None,
            RejectReason: reason,
            Ordinal: ordinal,
            Price: price,
            Affordable: affordable);
    }

    /// <summary>
    /// 家主继任候选（契约二十 §4 的四档，按优先级排序；查询面无 <see cref="GameDate"/> 形参，
    /// 故「同代年龄最大」以 <see cref="Person.BirthDate"/> 更早等价表达）。
    /// </summary>
    /// <param name="family">家族。</param>
    /// <returns>有序候选；家主健在未外嫁时为空。</returns>
    public static IReadOnlyList<Person> SuccessionCandidates(Family family)
        => HeadSuccession.Candidates(family);

    /// <summary>阶层金额表：聘礼（规格书 §9.2 第 1 列）。</summary>
    private static int GiftGuanOf(MarriageClass counterpartClass) => counterpartClass switch
    {
        MarriageClass.Commoner => MarriagePolicy.CommonerGiftGuan,
        MarriageClass.Scholar => MarriagePolicy.ScholarGiftGuan,
        MarriageClass.Official => MarriagePolicy.OfficialGiftGuan,
        _ => throw new ArgumentOutOfRangeException(
            nameof(counterpartClass),
            counterpartClass,
            "未知阶层（data-model §2.2 共三个取值）。"),
    };

    /// <summary>阶层金额表：嫁妆（规格书 §9.2 第 2 列）。</summary>
    private static int DowryGuanOf(MarriageClass counterpartClass) => counterpartClass switch
    {
        MarriageClass.Commoner => MarriagePolicy.CommonerDowryGuan,
        MarriageClass.Scholar => MarriagePolicy.ScholarDowryGuan,
        MarriageClass.Official => MarriagePolicy.OfficialDowryGuan,
        _ => throw new ArgumentOutOfRangeException(
            nameof(counterpartClass),
            counterpartClass,
            "未知阶层（data-model §2.2 共三个取值）。"),
    };
}
