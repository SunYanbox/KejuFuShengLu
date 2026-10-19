using KFL.Core.Config;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;
using KFL.Rules.Economy;
using KFL.Rules.Settlement;

namespace KFL.Rules.FamilyLifecycle;

/// <summary>
/// ⑧-c 患病与病程（契约二十 §3~§5）：<b>两段式</b>，第一段推进「进入本子步前已患病」者，
/// 第二段让「进入本子步前健康」者掷感染。
/// </summary>
/// <remarks>
/// <para>
/// <b>第一段的段内次序是本特性最容易被写反的一条</b>（MUST NOT 交换）：
/// ①疗程中先付药费（付不起 ⇒ 疗程中止、本月按未治疗口径）→ ②<b>先</b>扣体质 <c>−(2 + 8s)</c> →
/// ③体质归零 ⇒ 登记 <see cref="DeathCause.Illness"/> 并结束该成员 → ④<b>再</b>掷清除 →
/// ⑤疗程月数递减。
/// </para>
/// <para>
/// <b>宽限月由子步次序隐式表达，MUST NOT 新增字段</b>：确诊发生在第一段之后的第二段，
/// 故确诊当月既无扣减、无药费、也无清除判定；次月起才进入第一段。
/// </para>
/// <para>
/// 遍历集合 = 在册成员（<b>含服刑 / 待阙</b>，<b>不含</b>外嫁与已亡）；两段都用同一份
/// 「进入本子步之前」的快照 ⇒ 当月新愈者 MUST NOT 在第二段被重新掷感染，且清除命中后
/// 该成员当月不再有后续随机消费（Assumption 15）。
/// </para>
/// <para>
/// <b>零年龄档、零封顶</b>：患病概率只按体质线性插值再乘主导天赋 <c>m_p</c>，
/// MUST NOT 乘难度负面事件系数（SC-014），MUST NOT 二次封顶（FR-029）。
/// </para>
/// </remarks>
public static class IllnessAdvance
{
    /// <summary>推进一个月的患病与病程。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="random">随机源。</param>
    /// <param name="deaths">死亡登记表（本子步只登记，MUST NOT 改 <c>Status</c>）。</param>
    /// <returns>当月患病事实（确诊 / 痊愈），按 确诊在前、痊愈在后 的采集次序。</returns>
    public static IReadOnlyList<IllnessFact> Run(GameState state, IRandomService random, DeathRegister deaths)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(deaths);

        var month = state.CurrentDate;
        var registered = CountedMembers.Registered(state.Family)
            .OrderBy(person => person.Id.Value)
            .ToList();

        var ill = registered
            .Where(person => (person.Status & StatusFlag.Ill) == StatusFlag.Ill)
            .ToList();
        var healthy = registered
            .Where(person => (person.Status & StatusFlag.Ill) == StatusFlag.None)
            .ToList();

        var facts = new List<IllnessFact>();

        foreach (var person in ill)
        {
            AdvanceIllness(state, person, random, deaths, facts);
        }

        foreach (var person in healthy)
        {
            DrawInfection(state, person, random, month, facts);
        }

        return facts;
    }

    /// <summary>生病成员的当月推进（付药费 → 扣体质 → 判病亡 → 掷清除 → 递减疗程）。</summary>
    private static void AdvanceIllness(
        GameState state,
        Person person,
        IRandomService random,
        DeathRegister deaths,
        List<IllnessFact> facts)
    {
        var severity = SeverityOf(person);
        var treated = PayMedicineIfNeeded(state, person, severity);

        // ② 先扣体质（MUST NOT 挪到掷清除之后）。
        var loss = SeverityLoss(severity);
        person.Health = Math.Max(AttributeLimits.Min, person.Health - loss);

        // ③ 先扣后判：体质归零 ⇒ 病亡；不再掷清除、不再递减疗程。
        if (person.Health <= AttributeLimits.Min)
        {
            deaths.Add(person, DeathCause.Illness);
            return;
        }

        // ④ 再掷清除；概率钳到 1 ⇒ 必愈且 MUST NOT 消费随机。
        if (Recovers(state, person, treated, out var chance) || random.NextDouble() < chance)
        {
            ClearIllness(person);
            facts.Add(new IllnessFact(person.Id, Contracted: false, Recovered: true));
            return;
        }

        // ⑤ 疗程月数递减（仅本月中真实付过药费者；疗程中止者月数已置空）。
        if (treated && person.Timers.TreatmentRemainingMonths is { } remaining)
        {
            person.Timers = Timers(
                person,
                severityBasisPoints: SeverityBasisPointsOf(person),
                treatmentRemainingMonths: remaining - 1);
        }
    }

    /// <summary>① 疗程月内先付药费；付不起 ⇒ 疗程中止（<c>null</c>）、本月按未治疗口径，MUST NOT 转贷款。</summary>
    private static bool PayMedicineIfNeeded(GameState state, Person person, decimal severity)
    {
        if (person.Timers.TreatmentRemainingMonths is not > 0)
        {
            return false;
        }

        var fee = MedicineFee(state, severity);

        if (!PaymentPrimitive.CanCover(state.Economy, fee))
        {
            person.Timers = Timers(
                person,
                severityBasisPoints: SeverityBasisPointsOf(person),
                treatmentRemainingMonths: null);
            return false;
        }

        PaymentPrimitive.Pay(state.Economy, LedgerCategory.MedicineExpense, state.CurrentDate, fee, person.Id);
        return true;
    }

    /// <summary>第二段：健康成员掷感染；<c>AgeAt &lt; 3</c> 不掷、不消费随机。</summary>
    private static void DrawInfection(
        GameState state,
        Person person,
        IRandomService random,
        GameDate month,
        List<IllnessFact> facts)
    {
        if (person.AgeAt(month) < IllnessPolicy.IllnessExemptAge)
        {
            return;
        }

        var (infectionMultiplier, _, _) = DominantMultipliers(person);
        var health = Math.Clamp(person.Health, IllnessPolicy.HealthFloor, IllnessPolicy.HealthCeiling);
        var chance = (IllnessPolicy.InfectionChanceAtFullHealth
                      + ((IllnessPolicy.InfectionChanceAtFloorHealth - IllnessPolicy.InfectionChanceAtFullHealth)
                         * (IllnessPolicy.HealthCeiling - health) / IllnessPolicy.InterpolationSpan))
                     * infectionMultiplier;

        if (random.NextDouble() >= (double)chance)
        {
            return;
        }

        // 命中：先置 Ill 位（计时仍空，满足交叉校验），再紧接 1 次 NextDouble 定 s。
        // MUST NOT 扣体质、MUST NOT 掷清除（宽限月）。
        person.Status |= StatusFlag.Ill;
        var severity = (decimal)random.NextDouble();
        person.Timers = Timers(
            person,
            severityBasisPoints: IllnessPolicy.ToBasisPoints((double)severity),
            treatmentRemainingMonths: null);
        facts.Add(new IllnessFact(person.Id, Contracted: true, Recovered: false));
    }

    /// <summary>清除判定：返回 <c>true</c> = 概率已钳到 1（必愈且 MUST NOT 消费随机）。</summary>
    private static bool Recovers(GameState state, Person person, bool treated, out double chance)
    {
        var breakdown = BreakdownOf(person, state.CurrentDate, treated);
        chance = (double)breakdown.Rate;
        return breakdown.Rate >= 1m;
    }

    /// <summary>自愈率逐项拆解（与 <see cref="Recovers"/> 同一份算式；供界面展示）。</summary>
    internal static HealRateBreakdown BreakdownOf(Person person, GameDate date, bool treated)
    {
        var (_, recoveryMultiplier, talentName) = DominantMultipliers(person);
        var health = person.Health;

        var basePart = treated
            ? (health * IllnessPolicy.TreatedRecoveryCoefficient / 100m) + IllnessPolicy.TreatedRecoveryOffset
            : health * IllnessPolicy.BaseRecoveryCoefficient / 100m;

        var raw = basePart * recoveryMultiplier;
        var ageBonus = ClearBonus(person.AgeAt(date));
        var debugBonus = DebugOverrides.HealRateBonusOf(person.Id);
        var rate = Math.Min(1m, Math.Max(0m, raw + ageBonus + debugBonus));

        return new HealRateBreakdown(
            health, treated, basePart, recoveryMultiplier, talentName, raw, ageBonus, debugBonus, rate);
    }

    /// <summary>三段年龄增益（<b>加算</b>在「基础 × 天赋乘数」之上）：<c>3~12 ⇒ +0.60</c>、<c>12~24 ⇒ +0.30</c>、其余 0。</summary>
    private static decimal ClearBonus(int age)
    {
        if (age >= IllnessPolicy.ClearBonusBoundaryChild && age < IllnessPolicy.Youth)
        {
            return IllnessPolicy.ClearBonusChild;
        }

        return age >= IllnessPolicy.Youth && age < IllnessPolicy.Adult ? IllnessPolicy.ClearBonusYouth : 0m;
    }

    /// <summary>主导天赋单点：四项天赋<b>最高</b>的一项，并列按 农 → 商 → 仕 → 工 取先者（T053）。</summary>
    private static (decimal Infection, decimal Recovery, string Name) DominantMultipliers(Person person)
    {
        var talents = person.Talents;
        var best = talents.Agriculture;
        var infection = IllnessPolicy.TalentMultiplierFarmer;
        var recovery = IllnessPolicy.FarmerRecoveryMultiplier;
        var name = "农";

        if (talents.Commerce > best)
        {
            best = talents.Commerce;
            infection = IllnessPolicy.TalentMultiplierMerchant;
            recovery = IllnessPolicy.MerchantRecoveryMultiplier;
            name = "商";
        }

        if (talents.Officialdom > best)
        {
            best = talents.Officialdom;
            infection = IllnessPolicy.TalentMultiplierScholar;
            recovery = IllnessPolicy.ScholarRecoveryMultiplier;
            name = "仕";
        }

        if (talents.Craft > best)
        {
            infection = IllnessPolicy.TalentMultiplierArtisan;
            recovery = IllnessPolicy.ArtisanRecoveryMultiplier;
            name = "工";
        }

        return (infection, recovery, name);
    }

    /// <summary>药费 <c>(0.05 + 0.45s) 贯 × 支出系数</c>。</summary>
    private static Money MedicineFee(GameState state, decimal severity) =>
        Money.FromGuan((IllnessPolicy.MedicineBaseGuan + (IllnessPolicy.MedicineSeverityGuan * severity))
                       * DifficultyRates.ExpenseFactor(state.Difficulty));

    /// <summary>患病扣减 <c>2 + 8s</c>（四舍五入取整；MUST NOT 小于 2）。</summary>
    private static int SeverityLoss(decimal severity) =>
        (int)Math.Round(IllnessPolicy.SeverityLossBase + (IllnessPolicy.SeverityLossSpan * severity), MidpointRounding.AwayFromZero);

    /// <summary>疾病程度（0~1）。</summary>
    private static decimal SeverityOf(Person person) =>
        (decimal)IllnessPolicy.ToSeverity(person.Timers.IllnessSeverityBasisPoints ?? 0);

    /// <summary>疾病程度磅值。</summary>
    private static int SeverityBasisPointsOf(Person person) => person.Timers.IllnessSeverityBasisPoints ?? 0;

    /// <summary>清除患病：<b>先清计时（写 null）、后清状态位</b>——Person 的交叉校验 setter 强制该次序。</summary>
    private static void ClearIllness(Person person)
    {
        person.Timers = Timers(person, severityBasisPoints: null, treatmentRemainingMonths: null);
        person.Status &= ~StatusFlag.Ill;
    }

    /// <summary>保留其余四项计时、只改写患病两项。</summary>
    private static StatusTimers Timers(Person person, int? severityBasisPoints, int? treatmentRemainingMonths) =>
        new(
            sentenceRemainingMonths: person.Timers.SentenceRemainingMonths,
            examBanRemainingMonths: person.Timers.ExamBanRemainingMonths,
            promotionBanRemainingMonths: person.Timers.PromotionBanRemainingMonths,
            awaitingPostRemainingMonths: person.Timers.AwaitingPostRemainingMonths,
            illnessSeverityBasisPoints: severityBasisPoints,
            treatmentRemainingMonths: treatmentRemainingMonths);
}
