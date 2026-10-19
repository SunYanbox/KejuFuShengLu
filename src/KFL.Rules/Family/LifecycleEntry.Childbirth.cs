using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Achievements;
using KFL.Rules.Config;
using KFL.Rules.Economy;

namespace KFL.Rules.FamilyLifecycle;

public static partial class LifecycleEntry
{
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
}
