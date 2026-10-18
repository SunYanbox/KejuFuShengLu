using KFL.Core.Config;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Achievements;
using KFL.Rules.Config;
using KFL.Rules.Economy;

namespace KFL.Rules.FamilyLifecycle;

/// <summary>
/// 生命周期规则入口（data-model §3.3 / 契约十九 §3）：**七段式**——
/// ①校验（纯，0 次随机）②算金额 ③<see cref="PaymentPrimitive.CanCover"/> ④支付 ⑤掷随机
/// ⑥写状态与账本 ⑦返回。
/// </summary>
/// <remarks>
/// <para>
/// 成婚一律经 <see cref="Family.Marry"/>（MUST NOT 由本层直接写 <see cref="Person.SpouseId"/>）；
/// 丧偶一律经 <see cref="Family.EndMarriage"/>。
/// </para>
/// <para>
/// **与 data-model §3.3 的签名差异**（Phase 9 回写登记）：<see cref="TryMatchmake"/> 追加
/// <see cref="INameGenerator"/>（候选者姓名来源，照 <c>NewGameSetup.NewGame</c> 的既有模式）与
/// <paramref name="buyDegreeMarriage"/>（FR-017「可选」的唯一传达通道）；
/// <see cref="RespondToProposal"/> 同样追加 <paramref name="buyDegreeMarriage"/>。
/// </para>
/// </remarks>
public static class LifecycleEntry
{
    /// <summary>托媒（规格书 §9.1）：付费 → 50% 判定 → 抽阶层与候选者 → 成婚 → 分方向结算与归档。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="member">我方成员（须在册未婚成年）。</param>
    /// <param name="direction">婚配方向（须与成员性别一致）。</param>
    /// <param name="random">随机源。</param>
    /// <param name="names">姓名来源（候选者名）。</param>
    /// <param name="buyDegreeMarriage">是否勾选买功名婚姻（FR-017；仅娶入可用）。</param>
    /// <returns>入口结果；成功判定失败时 <see cref="LifecycleOutcome.Accepted"/> 为 <c>false</c>、
    /// <see cref="LifecycleOutcome.RejectReason"/> 为 <see cref="LifecycleRejectReason.None"/> 且如实回报已扣费用。</returns>
    /// <exception cref="ArgumentNullException">任一必需参数为 <c>null</c>。</exception>
    public static LifecycleOutcome TryMatchmake(
        GameState state,
        Person member,
        MarriageDirection direction,
        IRandomService random,
        INameGenerator names,
        bool buyDegreeMarriage = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(names);

        var family = state.Family;
        var economy = state.Economy;
        var month = state.CurrentDate;

        // ① 资格校验（纯；0 次随机）
        if (!LifecycleQueries.IsMarriageable(family, member, direction, month))
        {
            return LifecycleOutcome.Rejected(LifecycleRejectReason.NotMarriageable);
        }

        // ①a 月度名额（FR-013：同一成员同一月至多一次，下月可再试）：0 随机、0 付款、零状态变化
        if (family.Lifecycle.HasAppliedForMatchmaking(member.Id, month))
        {
            return LifecycleOutcome.Rejected(LifecycleRejectReason.AlreadyAppliedThisMonth);
        }

        // ② 金额：托媒费 + （可选的）买功名费
        var fee = LifecycleQueries.MatchmakerFee(state);
        var spouseDegree = buyDegreeMarriage
            ? LifecycleQueries.LowerDegreeOf(member.CurrentDegree)
            : null;
        if (buyDegreeMarriage && spouseDegree is null)
        {
            // 举人 / 白身无更低档 ⇒ 选项灰置：0 次随机、0 扣费
            return LifecycleOutcome.Rejected(LifecycleRejectReason.NoLowerDegree);
        }

        var degreeFee = spouseDegree is null ? Money.Zero : LifecycleQueries.DegreeMarriageFee(state, member);
        var upfront = fee + degreeFee;

        // ③ 可付性（现银 + 储蓄；不足即拒，MUST NOT 转贷款）
        if (!PaymentPrimitive.CanCover(economy, upfront))
        {
            return LifecycleOutcome.Rejected(LifecycleRejectReason.InsufficientFunds);
        }

        // ④ 支付（恒不转贷款）
        var entryCount = economy.Ledger.Entries.Count;
        var payment = PaymentPrimitive.Pay(economy, LedgerCategory.MatchmakerFee, month, fee, member.Id);
        var fromCash = payment.FromCash;
        var fromSavings = payment.FromSavings;
        if (degreeFee.IsPositive)
        {
            var degreePayment = PaymentPrimitive.Pay(
                economy,
                LedgerCategory.DegreeMarriageFeePaid,
                month,
                degreeFee,
                member.Id);
            fromCash += degreePayment.FromCash;
            fromSavings += degreePayment.FromSavings;
        }

        var entry = economy.Ledger.Entries.Count > entryCount
            ? economy.Ledger.Entries[entryCount]
            : (LedgerEntry?)null;

        // ④a 登记当月名额（FR-013）：**扣费成功后**才落持久记录——成功判定的失败也消耗名额，
        //     但资金不足 / 无更低功名的拒绝不消耗（上面两处已返回）。
        family.Lifecycle.RecordMatchmakeApplication(member.Id, month);

        // ⑤ 成功判定（恰 1 次 NextDouble；失败不退费，除 ④a 的当月名额记录外 Lifecycle 无变化）
        if (random.NextDouble() >= (double)MarriagePolicy.MatchmakerSuccessChance)
        {
            return new LifecycleOutcome
            {
                Accepted = false,
                RejectReason = LifecycleRejectReason.None,
                Amount = upfront,
                FromCash = fromCash,
                FromSavings = fromSavings,
                Entry = entry,
            };
        }

        // ⑥ 抽对方阶层（恰 1 次 NextDouble 的分段映射）
        var (counterpartClass, _, _) = MarriageMarket.Segment(state, random.NextDouble());

        // ⑦ 初始化候选者（年龄 1 次 Next → 天赋 ×4 → 学业 → 体质 → 寿数 → 姓名）
        var counterpart = MarriageMarket.CreateCandidate(
            state,
            random,
            names,
            direction == MarriageDirection.BrideInto ? Gender.Female : Gender.Male,
            member.AgeAt(month),
            member.Generation,
            spouseDegree ?? DegreeLevel.BaiShen,
            month);

        // ⑧ 婚嫁金额（规格书 §9.2）→ Family.Marry → 账本条目
        var gift = LifecycleQueries.GiftAmount(state, counterpartClass);
        var dowry = direction == MarriageDirection.BrideInto
            ? LifecycleQueries.DowryAmount(state, counterpartClass)
            : LifecycleQueries.OwnDowryAmount(state, member);

        // ⑧a 支付能力校验（规格书 §5.4 / §17 裁决 2026-10-17）：聘礼（娶入）或本家嫁妆（嫁出）
        //     必须在**本次交易前**由「现金 + 储蓄」足额覆盖；否则**整单失败**——不成婚、无婚嫁条目、
        //     状态零变更，且**已付的托媒费与买功名费不退**（媒人已行事，规格书 §9.1「失败不退费」同源）。
        //     MUST NOT 转贷款（与生育费、买人口价、药费、名师学费同一口径）。
        var obligation = direction == MarriageDirection.BrideInto ? gift : dowry;
        if (obligation.IsPositive && !PaymentPrimitive.CanCover(economy, obligation))
        {
            return new LifecycleOutcome
            {
                Accepted = false,
                RejectReason = LifecycleRejectReason.InsufficientFunds,
                Amount = upfront,
                FromCash = fromCash,
                FromSavings = fromSavings,
                Entry = entry,
            };
        }

        SettleAmounts(state, member, direction, gift, dowry);
        SettleMarriage(state, member, counterpart, direction);

        // 嫁出且仕族现任官员 ⇒ 额外收入（规格书 §9.3 第 2 行 / FR-018）
        if (direction == MarriageDirection.GroomOut && family.HasShiStatus && member.Rank is not null)
        {
            economy.Apply(
                LedgerCategory.DegreeMarriageDowryReceived,
                member.Id,
                LifecycleQueries.DegreeMarriageFee(state, member),
                month);
        }

        // 008 接线（T053，trigger ⑥⑦）：**成功之后、返回之前**判定；被拒路径（上面各 return）不调。
        // 走到这里 ⇒ 托媒确已成立，故 `buyDegreeMarriage` 为真即「确实发生了买功名婚姻」。
        AchievementAdjudicator.Report(state, AchievementTrigger.MatchmakingCommenced);
        if (buyDegreeMarriage)
        {
            AchievementAdjudicator.Report(state, AchievementTrigger.DegreeMarriagePurchased);
        }

        return new LifecycleOutcome
        {
            Accepted = true,
            RejectReason = LifecycleRejectReason.None,
            Amount = upfront,
            FromCash = fromCash,
            FromSavings = fromSavings,
            Entry = entry,
        };
    }

    /// <summary>应答当月提亲 / 捉婿提议（契约十九 §4.3 / T040）。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="accept">是否接受；<c>false</c> ⇒ 清空提议、0 随机 0 金额 0 状态变更。</param>
    /// <param name="random">随机源（仅在买功名婚姻重掷配偶学业时消费）。</param>
    /// <param name="names">姓名来源（快照未含姓氏时的兜底；正常路径不消费）。</param>
    /// <param name="buyDegreeMarriage">接受时是否加购买功名婚姻（FR-017，仅娶入）。</param>
    /// <returns>入口结果。</returns>
    /// <exception cref="ArgumentNullException">任一必需参数为 <c>null</c>。</exception>
    public static LifecycleOutcome RespondToProposal(
        GameState state,
        bool accept,
        IRandomService random,
        INameGenerator names,
        bool buyDegreeMarriage = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(names);

        var family = state.Family;
        var economy = state.Economy;
        var month = state.CurrentDate;

        var proposal = LifecycleQueries.PendingProposal(family, month);
        if (proposal is null)
        {
            return LifecycleOutcome.Rejected(LifecycleRejectReason.NoPendingProposal);
        }

        if (!accept)
        {
            family.Lifecycle.SetProposal(null);

            // 008 接线（T053，trigger ⑦）：**拒绝也算「应答成功」**——提议确已被应答并消耗。
            AchievementAdjudicator.Report(state, AchievementTrigger.ProposalAnswered);
            return LifecycleOutcome.Rejected(LifecycleRejectReason.None);
        }

        var member = family.TryGet(proposal.OurMemberId);
        if (member is null)
        {
            family.Lifecycle.SetProposal(null);
            return LifecycleOutcome.Rejected(LifecycleRejectReason.NotMarriageable);
        }

        var direction = LifecycleQueries.DirectionOf(member.Gender);
        var degree = proposal.CandidateDegree;
        var degreeFee = Money.Zero;
        if (buyDegreeMarriage)
        {
            if (direction != MarriageDirection.BrideInto || LifecycleQueries.LowerDegreeOf(member.CurrentDegree) is not { } lower)
            {
                return LifecycleOutcome.Rejected(LifecycleRejectReason.NoLowerDegree);
            }

            degree = lower;
            degreeFee = LifecycleQueries.DegreeMarriageFee(state, member);
            if (!PaymentPrimitive.CanCover(economy, degreeFee))
            {
                return LifecycleOutcome.Rejected(LifecycleRejectReason.InsufficientFunds);
            }
        }

        var candidate = new Person(
            LifecycleIds.Next(family, "proposal"),
            proposal.CandidateName,
            proposal.CandidateGender,
            new GameDate(month.Year - proposal.CandidateAge, month.Month),
            new TalentSet(
                proposal.TalentAgriculture,
                proposal.TalentCommerce,
                proposal.TalentScholar,
                proposal.TalentCraft),
            proposal.CandidateLifespan,
            member.Generation);
        candidate.Study = degree == proposal.CandidateDegree
            ? proposal.CandidateStudy
            : RollStudyByDegree(random, degree);
        candidate.Health = proposal.CandidateHealth;
        if (degree != DegreeLevel.BaiShen)
        {
            candidate.AppendDegree(
                new DegreeRecord(degree, proposal.CandidatePlacement, month, DegreeChangeCause.Initial));
        }

        var entry = (LedgerEntry?)null;
        if (degreeFee.IsPositive)
        {
            var entryCount = economy.Ledger.Entries.Count;
            PaymentPrimitive.Pay(economy, LedgerCategory.DegreeMarriageFeePaid, month, degreeFee, member.Id);
            entry = economy.Ledger.Entries.Count > entryCount
                ? economy.Ledger.Entries[entryCount]
                : null;
        }

        // 支付能力校验（规格书 §5.4 / §17 裁决 2026-10-17）：付不起聘礼（娶入）或本家嫁妆（嫁出）
        // ⇒ 整单失败、提议**已消耗**（清空）、不成婚、无婚嫁条目；已付的买功名费不退。
        var obligation = direction == MarriageDirection.BrideInto
            ? proposal.BetrothalGift
            : proposal.Dowry;
        if (obligation.IsPositive && !PaymentPrimitive.CanCover(economy, obligation))
        {
            family.Lifecycle.SetProposal(null);
            return new LifecycleOutcome
            {
                Accepted = false,
                RejectReason = LifecycleRejectReason.InsufficientFunds,
                Amount = degreeFee,
                FromCash = Money.Zero,
                FromSavings = Money.Zero,
                Entry = entry,
            };
        }

        SettleAmounts(state, member, direction, proposal.BetrothalGift, proposal.Dowry);
        SettleMarriage(state, member, candidate, direction);
        family.Lifecycle.SetProposal(null);

        // 008 接线（T053，trigger ⑦⑧）：接受成功之后判定；若本次买了功名婚姻（费额为正）则增报 ⑧。
        AchievementAdjudicator.Report(state, AchievementTrigger.ProposalAnswered);
        if (degreeFee.IsPositive)
        {
            AchievementAdjudicator.Report(state, AchievementTrigger.DegreeMarriagePurchased);
        }

        return new LifecycleOutcome
        {
            Accepted = true,
            RejectReason = LifecycleRejectReason.None,
            Amount = degreeFee,
            FromCash = Money.Zero,
            FromSavings = Money.Zero,
            Entry = entry,
            Proposal = proposal,
        };
    }

    /// <summary>按方向结算婚嫁金额（正额先入账、负额后出账）。</summary>
    /// <remarks>
    /// 调用方 MUST 先用 <see cref="PaymentPrimitive.CanCover"/> 校验**本方应付额**（娶入 = 聘礼、
    /// 嫁出 = 本家嫁妆）——不足一律整单失败（规格书 §5.4 / §17 裁决 2026-10-17），
    /// 故本方法内的 <see cref="PaymentPrimitive.Pay"/> 恒不会把缺口转成贷款。
    /// </remarks>
    private static void SettleAmounts(
        GameState state,
        Person member,
        MarriageDirection direction,
        Money gift,
        Money dowry)
    {
        var economy = state.Economy;
        var month = state.CurrentDate;
        if (direction == MarriageDirection.BrideInto)
        {
            // 娶入：收对方阶层嫁妆、付对方阶层聘礼
            if (dowry.IsPositive)
            {
                economy.Apply(LedgerCategory.DowryReceived, member.Id, dowry, month);
            }

            if (gift.IsPositive)
            {
                PaymentPrimitive.Pay(economy, LedgerCategory.BetrothalGiftPaid, month, gift, member.Id);
            }
        }
        else
        {
            // 嫁出：收对方阶层聘礼、付本家阶层嫁妆
            if (gift.IsPositive)
            {
                economy.Apply(LedgerCategory.BetrothalGiftReceived, member.Id, gift, month);
            }

            if (dowry.IsPositive)
            {
                PaymentPrimitive.Pay(economy, LedgerCategory.DowryPaid, month, dowry, member.Id);
            }
        }
    }

    /// <summary>经 <see cref="Family.Marry"/> 落定婚姻；嫁出时把双方都归档为「不在本户」。</summary>
    /// <remarks>
    /// 嫁出的对方（夫）在本游戏中属**另一个家族**，本家族只保留婚姻联系：以
    /// <see cref="StatusFlag.MarriedOut"/> 归档使其不计口、不参与在册成员的成长 / 疾病 / 饥馑子步，
    /// 但仍承载 ⑧-f 寿数判定——与契约十九 §6 的外嫁成员口径一致。
    /// </remarks>
    private static void SettleMarriage(
        GameState state,
        Person member,
        Person counterpart,
        MarriageDirection direction)
    {
        var family = state.Family;
        var month = state.CurrentDate;
        family.AddOutsider(counterpart);

        if (direction == MarriageDirection.BrideInto)
        {
            family.Marry(member.Id, counterpart.Id);
        }
        else
        {
            family.Marry(counterpart.Id, member.Id);
            counterpart.Status |= StatusFlag.MarriedOut;
            member.Status |= StatusFlag.MarriedOut;
            state.Economy.Apply(LedgerCategory.MemberMarriedOut, member.Id, Money.Zero, month);
        }

        state.Economy.Apply(LedgerCategory.MarriageFormed, member.Id, Money.Zero, month);
    }

    /// <summary>
    /// 申请生育（规格书 §9.4、契约十九 §7）：①资格与频率（纯，0 次随机）②金额
    /// ③可付性 ④支付 ⑤记当年申请 + 排入<b>次月</b>分娩队列。**申请当月不分娩**。
    /// </summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="couple">申请生育的夫妇（夫在前）。</param>
    /// <returns>入口结果；被拒时 <see cref="LifecycleOutcome.Entry"/> 为 <c>null</c> 且金额与资金来源全为 0。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 <c>null</c>。</exception>
    public static LifecycleOutcome ApplyChildbirth(GameState state, CoupleKey couple)
    {
        ArgumentNullException.ThrowIfNull(state);

        var month = state.CurrentDate;

        // ① ~ ③ 资格、频率与资金（一次性经查询面判定，拒绝原因与其口径一致；0 次随机）。
        var feasibility = LifecycleQueries.ChildbirthFeasibility(state, couple, month);
        if (!feasibility.Available)
        {
            return LifecycleOutcome.Rejected(feasibility.RejectReason);
        }

        // ④ 支付：现银 + 储蓄全额，不足即无法申请（本层已判定可付，故恒不转贷款）。
        var entryCount = state.Economy.Ledger.Entries.Count;
        var payment = PaymentPrimitive.Pay(
            state.Economy,
            LedgerCategory.ChildbirthExpense,
            month,
            feasibility.Cost,
            couple.WifeId);
        var entry = state.Economy.Ledger.Entries.Count > entryCount
            ? state.Economy.Ledger.Entries[entryCount]
            : (LedgerEntry?)null;

        // ⑤ 记当年申请并排入次月队列（次月的 ⑧-b 消费）。
        state.Family.Lifecycle.RecordBirthApplication(couple, month.Year);
        state.Family.Lifecycle.ScheduleBirth(new PendingBirth(couple, month));

        return new LifecycleOutcome
        {
            Accepted = true,
            RejectReason = LifecycleRejectReason.None,
            Amount = feasibility.Cost,
            FromCash = payment.FromCash,
            FromSavings = payment.FromSavings,
            Entry = entry,
        };
    }

    /// <summary>
    /// 买人口（FR-031~FR-034，玩家入口）：以「本年内第 n 名」递增计价（<b>MUST NOT 乘支出系数</b>），
    /// 从现银与储蓄全额支付，买来的成员作为家族旁系入册。
    /// </summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="random">随机源（年龄 1 次 <c>Next</c>、性别 1 次 <c>Next</c>，之后属性与寿数抽样）。</param>
    /// <param name="names">姓名生成器（只取名，姓取家族姓）。</param>
    /// <returns>入口结果；被拒时金额与资金来源全为 0 且 <see cref="LifecycleOutcome.Entry"/> 为 <c>null</c>。</returns>
    /// <exception cref="ArgumentNullException">任一参数为 <c>null</c>。</exception>
    /// <remarks>
    /// <para>
    /// 固定次序：①校验（<see cref="Family.HeadId"/> 为 <c>null</c> ⇒ <see cref="LifecycleRejectReason.HeadVacant"/>，
    /// <b>0 次随机</b>；报价经 <see cref="FamilyLifecycleState.PeekPurchaseOrdinal"/> <b>纯读不推进</b>）
    /// ②可付性判定 ③支付 ③b <b>扣款成功之后</b>才 <see cref="FamilyLifecycleState.NextPurchaseOrdinal"/> 记名次
    /// ④初始化新成员（年龄 → 性别 → 天赋 ×4 → 学业 → 体质 → 寿数 → 姓名，照 003 开局口径）
    /// ⑤入册为旁系并落定辈分（家主辈分 + 1）⑥事件条目。
    /// </para>
    /// <para>
    /// 失败路径 MUST NOT 改动 <see cref="FamilyLifecycleState.PurchaseCount"/> 与
    /// <see cref="FamilyLifecycleState.PurchaseYear"/>；支付不足即无法购买，<b>MUST NOT 转贷款</b>。
    /// </para>
    /// </remarks>
    public static LifecycleOutcome BuyPerson(GameState state, IRandomService random, INameGenerator names)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(names);

        var family = state.Family;
        var month = state.CurrentDate;

        // ① 校验：无家主即无从推导辈分（0 次随机），报价纯读不推进名次。
        if (family.HeadId is null)
        {
            return LifecycleOutcome.Rejected(LifecycleRejectReason.HeadVacant);
        }

        var price = Money.FromGuan(
            PopulationPurchasePolicy.BasePriceGuan * family.Lifecycle.PeekPurchaseOrdinal(month.Year));

        // ② 可付性：现银 + 储蓄全额，不足即无法购买（恒不转贷款）。
        if (!PaymentPrimitive.CanCover(state.Economy, price))
        {
            return LifecycleOutcome.Rejected(LifecycleRejectReason.InsufficientFunds);
        }

        // ③ 支付，并从账本尾部取出本条资金类条目。
        var entryCount = state.Economy.Ledger.Entries.Count;
        var payment = PaymentPrimitive.Pay(state.Economy, LedgerCategory.PersonPurchase, month, price);
        var entry = state.Economy.Ledger.Entries.Count > entryCount
            ? state.Economy.Ledger.Entries[entryCount]
            : (LedgerEntry?)null;

        // ③b 扣款成功后名次才推进（失败路径 MUST NOT 改计数与年份）。
        family.Lifecycle.NextPurchaseOrdinal(month.Year);

        // ④ 新成员初始化（顺序与 003 开局口径对称）。
        var age = random.Next(PopulationPurchasePolicy.AgeMin, PopulationPurchasePolicy.AgeMax + 1);
        var gender = random.Next(0, 2) == 0 ? Gender.Male : Gender.Female;
        var talents = new TalentSet(
            AttributePolicy.NextTalent(random),
            AttributePolicy.NextTalent(random),
            AttributePolicy.NextTalent(random),
            AttributePolicy.NextTalent(random));
        var study = AttributePolicy.NextStudy(random);
        var health = AttributePolicy.NextHealth(random);
        var lifespan = AttributePolicy.NextLifespan(gender, random);
        var name = family.Name + names.NextGivenName(gender);

        // ⑤ 入册为旁系（无父母参照），辈分 = 家主辈分 + 1（首次家族内成婚前可再对齐一次）。
        var newcomer = new Person(
            LifecycleIds.Next(family, "purchase"),
            name,
            gender,
            new GameDate(month.Year - age, month.Month),
            talents,
            lifespan,
            generation: 0)
        {
            Study = study,
            Health = health,
        };
        family.AddOutsider(newcomer);
        family.SetOutsiderGeneration(newcomer.Id, family.BoughtCollateralGeneration);

        // ⑥ 事件条目（金额 0）。
        state.Economy.Apply(LedgerCategory.PersonBought, newcomer.Id, Money.Zero, month);

        // 008 接线（T053，trigger ⑨）：买人口成功之后判定（被拒路径在上面已返回）。
        AchievementAdjudicator.Report(state, AchievementTrigger.PersonPurchased);

        return new LifecycleOutcome
        {
            Accepted = true,
            RejectReason = LifecycleRejectReason.None,
            Amount = price,
            FromCash = payment.FromCash,
            FromSavings = payment.FromSavings,
            Entry = entry,
        };
    }

    /// <summary>
    /// 发起一次疗程（FR-030）：仅对 <see cref="StatusFlag.Ill"/> 置位者可用，
    /// 置 <see cref="StatusTimers.TreatmentRemainingMonths"/> = <c>3</c>（<b>零随机、零支付</b>）。
    /// </summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="patient">患者。</param>
    /// <returns>入口结果；<c>Accepted = false</c> 时 <see cref="LifecycleOutcome.RejectReason"/> 为 <c>NotMarriageable</c>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 或 <paramref name="patient"/> 为 <c>null</c>。</exception>
    /// <remarks>
    /// <para>
    /// <b>宽限月内 MUST NOT 支付药费</b>：药费只在 ⑧-c 第一段的疗程月产生，而 ⑧-c 在确诊当月
    /// 已先于感染判定执行完，故确诊当月调用本入口不会产生任何支付——疗程自患病<b>次月</b>起算。
    /// </para>
    /// <para>
    /// 疗程结束后若仍未愈可<b>再次发起</b>（本入口只重置月数，不区分是否曾治疗过）。
    /// MUST NOT 自创新枚举值：非患病者一律用 <see cref="LifecycleRejectReason.NotMarriageable"/>。
    /// </para>
    /// </remarks>
    public static LifecycleOutcome StartTreatment(GameState state, Person patient)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(patient);

        var registered = (patient.Status & StatusFlag.Deceased) == StatusFlag.None
                         && (patient.Status & StatusFlag.MarriedOut) == StatusFlag.None
                         && state.Family.TryGet(patient.Id) is not null;

        if (!registered || (patient.Status & StatusFlag.Ill) != StatusFlag.Ill)
        {
            return LifecycleOutcome.Rejected(LifecycleRejectReason.NotMarriageable);
        }

        patient.Timers = new StatusTimers(
            sentenceRemainingMonths: patient.Timers.SentenceRemainingMonths,
            examBanRemainingMonths: patient.Timers.ExamBanRemainingMonths,
            promotionBanRemainingMonths: patient.Timers.PromotionBanRemainingMonths,
            awaitingPostRemainingMonths: patient.Timers.AwaitingPostRemainingMonths,
            illnessSeverityBasisPoints: patient.Timers.IllnessSeverityBasisPoints,
            treatmentRemainingMonths: IllnessPolicy.TreatmentMonths);

        return new LifecycleOutcome
        {
            Accepted = true,
            RejectReason = LifecycleRejectReason.None,
            Amount = Money.Zero,
            FromCash = Money.Zero,
            FromSavings = Money.Zero,
            Entry = null,
        };
    }

    /// <summary>买功名配偶的学业重掷（规格书 §9.3：贡士 N(60,10) / 举人 N(50,10)）。</summary>
    private static int RollStudyByDegree(IRandomService random, DegreeLevel degree)
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
