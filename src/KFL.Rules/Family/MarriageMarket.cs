using KFL.Core.Config;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;
using KFL.Rules.Economy;
using KFL.Rules.Settlement;

namespace KFL.Rules.FamilyLifecycle;

/// <summary>⑧-a 婚配事件的当月产出（契约十九 §4）。</summary>
/// <param name="ImperialProposal">当月命中的榜下捉婿提议；未命中为 <c>null</c>。</param>
/// <param name="MatchmakerProposal">当月命中的提亲提议；未命中为 <c>null</c>。</param>
/// <remarks>
/// 两者同月可同时命中（R-25：各自掷一次），但**捉婿优先**——<see cref="PendingProposal"/> 取捉婿，
/// 提亲命中 MUST NOT 覆盖捉婿。
/// </remarks>
public sealed record MatchmakerResult(MarriageProposal? ImperialProposal, MarriageProposal? MatchmakerProposal)
{
    /// <summary>当月新产出的提议（捉婿优先）；两者都未命中为 <c>null</c>。</summary>
    public MarriageProposal? PendingProposal => ImperialProposal ?? MatchmakerProposal;
}

/// <summary>
/// ⑧-a 婚配事件（契约十九 §4 / T039）：**先捉婿、后提亲**，只写 <see cref="FamilyLifecycleState.Proposal"/>，
/// MUST NOT 改动任何 <see cref="Person"/> 状态、MUST NOT 触碰资金。
/// </summary>
/// <remarks>
/// <para>
/// **随机消费**（契约十八 §3 的次序表）：捉婿对每名合格成员掷 1 次概率、命中再掷 1 次金额；
/// 提亲每月恰掷 1 次概率，命中后复用该值分段映射阶层（0 次额外随机），再以
/// <c>Next(0, 候选数)</c> 在**可支付候选集合**内均匀取人（1 次额外随机，FR-020）。
/// 两处命中后的候选者初始化按契约十九 §3 ⑦ 的口径消费（年龄 1 次 Next、属性 16 次 NextDouble、
/// 姓名 0 次随机）；未命中 MUST NOT 消费这些值。
/// </para>
/// <para>本类型不建视图、不改存档字段（提议经 <see cref="FamilyLifecycleState.SetProposal"/> 落定）。</para>
/// </remarks>
public static class MarriageMarket
{
    /// <summary>执行 ⑧-a：捉婿（先）与提亲（后）。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="random">随机源。</param>
    /// <param name="names">姓名来源（候选者名）。</param>
    /// <returns>当月提议产出。</returns>
    /// <exception cref="ArgumentNullException">任一参数为 <c>null</c>。</exception>
    public static MatchmakerResult Run(GameState state, IRandomService random, INameGenerator names)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(names);

        var family = state.Family;
        var month = state.CurrentDate;
        var negativeEventFactor = DifficultyRates.NegativeEventFactor(state.Difficulty);
        var pressure = OfficialPressure.Compute(family);

        // 上月及更早的托媒名额记录过期（当月仍有效，故用严格早于当月）。
        family.Lifecycle.ConsumeMatchmakeApplications(month);

        var imperial = DrawImperialProposal(state, random, names, month, negativeEventFactor, pressure);
        if (imperial is not null)
        {
            family.Lifecycle.SetProposal(imperial);
        }

        var matchmaker = DrawMatchmakerProposal(state, random, names, month, negativeEventFactor);
        if (matchmaker is not null && imperial is null)
        {
            family.Lifecycle.SetProposal(matchmaker);
        }

        return new MatchmakerResult(imperial, matchmaker);
    }

    /// <summary>按本家总资产档抽取对方阶层，并给出该阶层在本档权重里的区间。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="roll">已抽取的 <c>[0,1)</c> 值。</param>
    /// <returns>阶层与区间。</returns>
    public static (MarriageClass Class, double Lower, double Upper) Segment(GameState state, double roll)
    {
        ArgumentNullException.ThrowIfNull(state);

        var weights = WeightsOf(state);
        var lower = 0d;
        for (var index = 0; index < weights.Count; index++)
        {
            var upper = lower + (double)weights[index];
            if (roll < upper || index == weights.Count - 1)
            {
                return ((MarriageClass)index, lower, upper);
            }

            lower = upper;
        }

        return (MarriageClass.Official, lower, 1d);
    }

    /// <summary>抽取候选者年龄：本家成员年龄 ± <c>CandidateAgeSpread</c>，并按性别成年线抬高下界。</summary>
    /// <param name="random">随机源。</param>
    /// <param name="anchorAge">本家成员年龄。</param>
    /// <param name="gender">候选者性别。</param>
    /// <returns>候选者年龄。</returns>
    public static int DrawCandidateAge(IRandomService random, int anchorAge, Gender gender)
    {
        ArgumentNullException.ThrowIfNull(random);

        var lower = anchorAge - MarriagePolicy.CandidateAgeSpread;
        var adulthood = gender == Gender.Male
            ? AgeBracketPolicy.MaleAdulthoodAge
            : AgeBracketPolicy.FemaleAdulthoodAge;
        if (lower < adulthood)
        {
            lower = adulthood;
        }

        var upper = anchorAge + MarriagePolicy.CandidateAgeSpread;
        if (upper < lower)
        {
            upper = lower;
        }

        return random.Next(lower, upper + 1);
    }

    /// <summary>
    /// 初始化一名候选者（契约十九 §3 ⑦ / FR-014）：年龄 → 天赋 ×4（农、商、仕、工）→ 学业 → 体质 → 寿数 → 姓名。
    /// </summary>
    /// <param name="state">游戏状态（提供家族姓氏）。</param>
    /// <param name="random">随机源。</param>
    /// <param name="names">姓名来源。</param>
    /// <param name="gender">候选者性别。</param>
    /// <param name="anchorAge">我方成员的年龄（年龄差 ≤ <c>CandidateAgeSpread</c>）。</param>
    /// <param name="generation">入族后的辈分（= 家族内配偶辈分）。</param>
    /// <param name="degree">候选者功名（普通人白身，或买功名 / 捉婿分档的结果）。</param>
    /// <param name="month">判定月份。</param>
    /// <returns>尚未入族的候选者。</returns>
    public static Person CreateCandidate(
        GameState state,
        IRandomService random,
        INameGenerator names,
        Gender gender,
        int anchorAge,
        int generation,
        DegreeLevel degree,
        GameDate month)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(names);

        var age = DrawCandidateAge(random, anchorAge, gender);
        var talents = new TalentSet(
            AttributePolicy.NextTalent(random),
            AttributePolicy.NextTalent(random),
            AttributePolicy.NextTalent(random),
            AttributePolicy.NextTalent(random));
        var study = RollStudy(random, degree);
        var health = AttributePolicy.NextHealth(random);
        var lifespan = AttributePolicy.NextLifespan(gender, random);
        var name = state.Family.Name + names.NextGivenName(gender);
        var birthDate = new GameDate(month.Year - age, month.Month);

        var candidate = new Person(
            LifecycleIds.Next(state.Family, "proposal"),
            name,
            gender,
            birthDate,
            talents,
            lifespan,
            generation);

        candidate.Study = study;
        candidate.Health = health;
        if (degree != DegreeLevel.BaiShen)
        {
            candidate.AppendDegree(new DegreeRecord(degree, null, month, DegreeChangeCause.Initial));
        }

        return candidate;
    }

    /// <summary>捉婿的配偶功名分档（契约十九 §4.2）：<c>P &lt; 1</c> 白身、<c>1 ≤ P &lt; 3</c> 举人、<c>P ≥ 3</c> 贡士（MUST NOT 进士）。</summary>
    /// <param name="pressure">仕途压力 <c>P</c>。</param>
    /// <returns>功名分档。</returns>
    public static DegreeLevel ImperialDegreeFor(decimal pressure) => pressure switch
    {
        var value when value < MarriagePolicy.SpouseDegreeScholarThreshold => DegreeLevel.BaiShen,
        var value when value < MarriagePolicy.SpouseDegreeTributeThreshold => DegreeLevel.JuRen,
        _ => DegreeLevel.GongShi,
    };

    /// <summary>捉婿的支付金额：<c>Next(500, 1001) × (1 + P)</c> 贯四舍五入到整贯。</summary>
    /// <param name="random">随机源。</param>
    /// <param name="pressure">仕途压力 <c>P</c>。</param>
    /// <returns>对方支付的金额。</returns>
    public static Money DrawImperialPayment(IRandomService random, decimal pressure)
    {
        ArgumentNullException.ThrowIfNull(random);

        var guan = random.Next(
            MarriagePolicy.ImperialPaymentMinGuan,
            MarriagePolicy.ImperialPaymentMaxGuan + 1);
        return Money.FromGuan(
            Math.Round(guan * (1m + pressure), MidpointRounding.AwayFromZero));
    }

    /// <summary>捉婿命中概率：<c>30% × 负面事件系数 ÷ (1 + P)</c>。</summary>
    /// <param name="difficulty">难度。</param>
    /// <param name="pressure">仕途压力 <c>P</c>。</param>
    /// <returns>命中概率。</returns>
    public static decimal ImperialChance(Difficulty difficulty, decimal pressure)
    {
        var factor = DifficultyRates.NegativeEventFactor(difficulty);
        return MarriagePolicy.ImperialProposalBaseChance * factor / (1m + pressure);
    }

    /// <summary>提亲命中概率：<c>5% × 负面事件系数</c>（四难度 2.5% / 5% / 6.5% / 7%）。</summary>
    /// <param name="difficulty">难度。</param>
    /// <returns>命中概率。</returns>
    public static decimal ProposalChance(Difficulty difficulty) =>
        MarriagePolicy.ProposalBaseChance * DifficultyRates.NegativeEventFactor(difficulty);

    private static MarriageProposal? DrawImperialProposal(
        GameState state,
        IRandomService random,
        INameGenerator names,
        GameDate month,
        decimal negativeEventFactor,
        decimal pressure)
    {
        var chance = MarriagePolicy.ImperialProposalBaseChance * negativeEventFactor / (1m + pressure);
        foreach (var member in ImperialCandidates(state.Family, month))
        {
            if (random.NextDouble() >= (double)chance)
            {
                continue;
            }

            var payment = DrawImperialPayment(random, pressure);
            var degree = ImperialDegreeFor(pressure);
            var candidate = CreateCandidate(
                state,
                random,
                names,
                LifecycleQueries.DirectionOf(member.Gender) == MarriageDirection.BrideInto
                    ? Gender.Female
                    : Gender.Male,
                member.AgeAt(month),
                member.Generation,
                degree,
                month);

            return new MarriageProposal
            {
                Kind = MarriageProposalKind.ImperialProposal,
                OurMemberId = member.Id,
                CandidateName = candidate.Name,
                CandidateGender = candidate.Gender,
                CandidateAge = candidate.AgeAt(month),
                TalentAgriculture = candidate.Talents.Agriculture,
                TalentCommerce = candidate.Talents.Commerce,
                TalentScholar = candidate.Talents.Officialdom,
                TalentCraft = candidate.Talents.Craft,
                CandidateStudy = candidate.Study,
                CandidateHealth = candidate.Health,
                CandidateLifespan = candidate.Lifespan,
                CandidateDegree = degree,
                Class = MarriageClass.Scholar,
                BetrothalGift = Money.Zero,
                Dowry = payment,
                ProposedAt = month,
            };
        }

        return null;
    }

    private static MarriageProposal? DrawMatchmakerProposal(
        GameState state,
        IRandomService random,
        INameGenerator names,
        GameDate month,
        decimal negativeEventFactor)
    {
        var roll = random.NextDouble();
        if (roll >= (double)(MarriagePolicy.ProposalBaseChance * negativeEventFactor))
        {
            return null;
        }

        var members = LifecycleQueries.MarriageCandidateMembers(state.Family, month);
        if (members.Count == 0)
        {
            return null;
        }

        // 阶层分段仍复用同一次概率抽样值（0 次额外随机）：只决定对方阶层与金额。
        var (counterpartClass, _, _) = Segment(state, roll);

        // FR-020 / FR-052：候选集合只保留「本家族付得起该方向应付额」者——
        // 娶入应付对方阶层聘礼、嫁出应付本家阶层嫁妆。付不起的成员 MUST NOT 被抽中。
        var eligible = new List<Person>(members.Count);
        foreach (var member in members)
        {
            var obligation = LifecycleQueries.DirectionOf(member.Gender) == MarriageDirection.BrideInto
                ? LifecycleQueries.GiftAmount(state, counterpartClass)
                : LifecycleQueries.OwnDowryAmount(state, member);
            if (!obligation.IsPositive || PaymentPrimitive.CanCover(state.Economy, obligation))
            {
                eligible.Add(member);
            }
        }

        if (eligible.Count == 0)
        {
            return null;
        }

        // FR-020：在可支付候选集合内均匀抽 1 名（恰 1 次 Next(0, 候选数)）。
        var picked = eligible[random.Next(0, eligible.Count)];
        var direction = LifecycleQueries.DirectionOf(picked.Gender);
        var degree = picked.CurrentDegree == DegreeLevel.BaiShen
            ? DegreeLevel.BaiShen
            : LifecycleQueries.LowerDegreeOf(picked.CurrentDegree) ?? DegreeLevel.BaiShen;
        var candidate = CreateCandidate(
            state,
            random,
            names,
            direction == MarriageDirection.BrideInto ? Gender.Female : Gender.Male,
            picked.AgeAt(month),
            picked.Generation,
            degree,
            month);

        var gift = LifecycleQueries.GiftAmount(state, counterpartClass);
        var dowry = direction == MarriageDirection.BrideInto
            ? LifecycleQueries.DowryAmount(state, counterpartClass)
            : LifecycleQueries.OwnDowryAmount(state, picked);

        return new MarriageProposal
        {
            Kind = MarriageProposalKind.Matchmaker,
            OurMemberId = picked.Id,
            CandidateName = candidate.Name,
            CandidateGender = candidate.Gender,
            CandidateAge = candidate.AgeAt(month),
            TalentAgriculture = candidate.Talents.Agriculture,
            TalentCommerce = candidate.Talents.Commerce,
            TalentScholar = candidate.Talents.Officialdom,
            TalentCraft = candidate.Talents.Craft,
            CandidateStudy = candidate.Study,
            CandidateHealth = candidate.Health,
            CandidateLifespan = candidate.Lifespan,
            CandidateDegree = degree,
            Class = counterpartClass,
            BetrothalGift = gift,
            Dowry = dowry,
            ProposedAt = month,
        };
    }

    private static IEnumerable<Person> ImperialCandidates(Family family, GameDate month) =>
        LifecycleQueries.MarriageCandidateMembers(family, month)
            .Where(member => LastFirstClassMonth(member) is { } changedAt
                && changedAt.ElapsedMonths(month) == 1);

    private static GameDate? LastFirstClassMonth(Person member)
    {
        for (var index = member.DegreeHistory.Count - 1; index >= 0; index--)
        {
            var record = member.DegreeHistory[index];
            if (record.Class == ImperialClass.FirstClass)
            {
                return record.ChangedAt;
            }
        }

        return null;
    }

    private static IReadOnlyList<decimal> WeightsOf(GameState state)
    {
        var total = TotalAssetsCalculator.Of(state.Economy);
        if (total < Money.FromGuan(MarriagePolicy.ClassWealthThresholdLow))
        {
            return MarriagePolicy.ClassWeightsPoor;
        }

        return total < Money.FromGuan(MarriagePolicy.ClassWealthThresholdHighGuan)
            ? MarriagePolicy.ClassWeightsMiddle
            : MarriagePolicy.ClassWeightsRich;
    }

    private static int RollStudy(IRandomService random, DegreeLevel degree)
    {
        var (mean, sigma) = degree switch
        {
            DegreeLevel.GongShi => (MarriagePolicy.TributeSpouseStudyMean, MarriagePolicy.TributeSpouseStudySigma),
            DegreeLevel.JuRen => (MarriagePolicy.JuRenSpouseStudyMean, MarriagePolicy.JuRenSpouseStudySigma),
            _ => (0, 0),
        };

        if (mean == 0 && sigma == 0)
        {
            return AttributePolicy.NextStudy(random);
        }

        var value = (int)Math.Round(
            AttributePolicy.NextNormal(mean, sigma, random),
            MidpointRounding.AwayFromZero);
        return Math.Clamp(value, AttributeLimits.Min, AttributeLimits.Max);
    }
}
