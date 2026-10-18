using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Achievements;
using KFL.Rules.Config;
using KFL.Rules.Economy;

namespace KFL.Rules.Punishment;

/// <summary>
/// 惩罚施加（规格书 §7.3~§7.5；契约十六 §2~§7）——「被抓当刻」按 **12 格矩阵**命中一格，
/// 并施加该格的全部后果。
/// </summary>
/// <remarks>
/// <para>
/// 施加次序（MUST NOT 调换）：① 取该格 → ② 服刑 → ③ 罚金 → ④ 相关亲属禁考与 / 或禁升
/// → ⑤ 死刑 → ⑥ 连坐 → ⑦ 全族禁考禁升 <see cref="PunishmentPolicy.FamilyWideBanMonths"/> 月。
/// 全部后果**同刻**记入 <see cref="Family.PunishmentJournal"/> 并投影为**同一份**
/// <see cref="PunishmentSnapshot"/>。
/// </para>
/// <para>
/// **事实行字段归属口径**（data-model §2.2 只给出字段名，未逐字段定义，此处定稿并登记回写范围）：
/// <c>Subject</c> = **当事人**（触发惩罚的行贿者），<c>Relative</c> = **被施加 / 被波及的成员**。
/// 该口径与 <see cref="PunishmentJournal"/> 的排序键一致（主键 <c>Subject</c>、并列按 <c>Relative</c>）：
/// 「全族逐人」与「多亲属」两种情形下多条事实行共享同一 <c>Subject</c>，正需 <c>Relative</c> 破除并列。
/// </para>
/// <para>
/// **不叠加、取替换**（契约十六 §3 条款 2）：同一成员在**同一次**施加里只被写一次计时
/// （多条路径命中同一人时按已覆盖去重），且本次施加值**替换**既有值（MUST NOT 相加）。
/// </para>
/// <para>
/// **本类 MUST NOT 消费随机**：被抓判定发生在 <c>BribeEntry</c>（契约十六 §1.2 条款 5）；
/// <paramref name="random"/> 只作接口完整性保留，MUST NOT 调用其任何成员（仅用于空值守卫）。
/// </para>
/// </remarks>
public static class PunishmentImposer
{
    /// <summary>
    /// 对当事人施加该难度 × 该级别对应的全部惩罚后果。
    /// </summary>
    /// <param name="state">游戏状态（提供当刻难度、年月与家族 / 经济）。</param>
    /// <param name="subject">当事人（被抓的行贿者）。</param>
    /// <param name="level">被抓对应的考试级别。</param>
    /// <param name="random">随机来源；本方法 MUST NOT 消费随机。</param>
    /// <returns>本次施加产生的五类事实（行贿事实由 <c>BribeEntry</c> 自身报告）。</returns>
    /// <exception cref="ArgumentNullException">任一引用参数为 <see langword="null"/>。</exception>
    public static PunishmentSnapshot Apply(
        GameState state, Person subject, ExamLevel level, IRandomService random)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(random);

        var month = state.CurrentDate;
        var cell = PunishmentPolicy.CellOf(state.Difficulty, level);
        var journal = state.Family.PunishmentJournal;

        var sentences = new List<SentenceFact>();
        var fines = new List<FineFact>();
        var relativeBans = new List<RelativeBanFact>();
        var collective = new List<CollectivePunishmentFact>();
        var banned = new HashSet<PersonId>();

        // ② 服刑：仅含服刑的格子建立计时与状态位（死刑格 SentenceMonths == 0）。
        if (cell.SentenceMonths > 0)
        {
            EstablishSentence(subject, cell.SentenceMonths);
            var fact = new SentenceFact(month, subject.Id, cell.SentenceMonths);
            journal.Append(fact);
            sentences.Add(fact);
        }

        // ③ 罚金：金额为 0 时不落条目（资金类条目不变量）。
        var fine = ComputeFine(cell, state.Economy, state.Difficulty);
        if (fine.IsPositive)
        {
            var payment = PaymentPrimitive.Pay(
                state.Economy, LedgerCategory.Fine, month, fine, subject.Id);
            var fact = new FineFact(
                month, subject.Id, fine, payment.FromCash + payment.FromSavings, payment.ToLoan);
            journal.Append(fact);
            fines.Add(fact);

            // 008 接线（T052，trigger ④）：罚金付不起而转为贷款本金时，即时判定 ACH-042（债台高筑）。
            // 本入口由 BribeEntry 在**动作路径**上调用（CurrentDate 即当月），故落点恰当；
            // 这里是当前代码里唯一真能产生贷款本金的路径（其余 Pay 调用点都先过 CanCover）。
            if (payment.ToLoan.IsPositive)
            {
                AchievementAdjudicator.Report(state, AchievementTrigger.LoanOriginated);
            }
        }

        var relatives = RelativesOf(state.Family, subject);

        // ④ 相关亲属禁考与 / 或禁升：全族格由 ⑦ 统一施加，此处 MUST NOT 重复（否则同一人两条事实行）。
        if (!cell.FamilyWideBan && cell.RelativeBanMonths > 0)
        {
            foreach (var relative in relatives)
            {
                var fact = Ban(
                    journal, month, relative, subject.Id, cell.RelativeBanMonths, cell.RelativePromotionBan, banned);
                if (fact is { } ban)
                {
                    relativeBans.Add(ban);
                }
            }
        }

        // ⑤ 死刑（立即执行：置已亡、归档、进族谱；MUST NOT 建立服刑计时）。
        if (cell.Execution == ExecutionKind.ImmediateSentence)
        {
            Execute(subject);
        }

        // ⑥ 连坐：仅地狱殿试 × 被抓（cell.CollectivePunishment 为真的唯一一格）。
        if (cell.CollectivePunishment)
        {
            foreach (var relative in relatives)
            {
                var fact = Collective(
                    journal, month, state.Family, subject, relative, banned, relativeBans);
                if (fact is { } punishment)
                {
                    collective.Add(punishment);
                }
            }
        }

        // ⑦ 全族禁考禁升 48 月：逐人计时（MUST NOT 新增家族级字段），只有地狱殿试格为真。
        if (cell.FamilyWideBan)
        {
            foreach (var member in LivingRegisteredMembers(state.Family))
            {
                if (member.Status.HasFlag(StatusFlag.Deceased))
                {
                    continue;
                }

                var fact = Ban(
                    journal, month, member, subject.Id, PunishmentPolicy.FamilyWideBanMonths, true, banned);
                if (fact is { } ban)
                {
                    relativeBans.Add(ban);
                }
            }

            journal.MarkFamilyBanDeclared();
        }

        return new PunishmentSnapshot
        {
            Month = month,
            Bribes = Array.Empty<BribeFact>(),
            Sentences = sentences,
            Fines = fines,
            RelativeBans = relativeBans,
            CollectivePunishments = collective,
            Releases = Array.Empty<ReleaseFact>(),
            FamilyBanDeclared = cell.FamilyWideBan,
            PrisonerCount = PunishmentQueries.PrisonerCount(state.Family, month),
        };
    }

    /// <summary>罚金基数 = <see cref="TotalAssetsCalculator.Of(FamilyEconomy)"/>（FR-011 的单一访问器）。</summary>
    /// <param name="economy">家族经济。</param>
    /// <returns>总资产；为负或 0 时返回 <see cref="Money.Zero"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="economy"/> 为 <see langword="null"/>。</exception>
    public static Money FineBase(FamilyEconomy economy) => TotalAssetsCalculator.Of(economy);

    /// <summary>
    /// 计算该格的罚金（契约十六 §4.2）。
    /// </summary>
    /// <param name="cell">惩罚格。</param>
    /// <param name="economy">家族经济（提供总资产基数）。</param>
    /// <param name="difficulty">难度（决定支出系数）。</param>
    /// <returns>
    /// 比例格 = <c>min(比例 × 总资产 × 支出系数, 50000 贯)</c>；固定格 = <c>固定额 × 支出系数</c>；
    /// 无罚金格 = <see cref="Money.Zero"/>。封顶对**最终金额**生效（先乘系数、再封顶）。
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="economy"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cell"/> 的罚金计法未登记。</exception>
    public static Money ComputeFine(PunishmentCell cell, FamilyEconomy economy, Difficulty difficulty)
    {
        ArgumentNullException.ThrowIfNull(economy);

        var expenseFactor = DifficultyRates.ExpenseFactor(difficulty);

        return cell.Fine switch
        {
            FineKind.None => Money.Zero,
            FineKind.Fixed => cell.FineFixedGuan * expenseFactor,
            FineKind.Ratio => CapRatioFine(
                Money.FromGuan(cell.FineRatio * TotalAssetsCalculator.Of(economy).Guan) * expenseFactor),
            _ => throw new ArgumentOutOfRangeException(
                nameof(cell), cell.Fine, "未登记的罚金计法（规格书 §7.5 只有无罚金 / 比例 / 固定三种）。"),
        };
    }

    /// <summary>把目标的功名降一级（进士 → 贡士 → 举人 → 白身；白身无降）。</summary>
    /// <param name="person">被降级的成员。</param>
    /// <param name="month">降级发生的年月（= 连坐当刻）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 为 <see langword="null"/>。</exception>
    /// <remarks>
    /// **恰 1 条** <see cref="DegreeRecord"/>，原因复用既有的
    /// <see cref="DegreeChangeCause.PunishmentDemotion"/>（MUST NOT 自创原因值）。
    /// 行贿三项计数与失败数**MUST NOT** 被本方法清零。
    /// </remarks>
    public static void DemoteDegree(Person person, GameDate month)
    {
        ArgumentNullException.ThrowIfNull(person);

        var current = person.CurrentDegree;
        if (current == DegreeLevel.BaiShen)
        {
            return;
        }

        person.AppendDegree(new DegreeRecord(
            (DegreeLevel)((int)current - 1), placement: null, month, DegreeChangeCause.PunishmentDemotion));
    }

    /// <summary>
    /// 当事人的**相关亲属**（契约十六 §6.4、§2）：配偶、儿子、女儿、父亲、母亲中的**在世者**。
    /// </summary>
    /// <param name="family">家族。</param>
    /// <param name="subject">当事人。</param>
    /// <returns>按 <c>PersonId</c> 升序的相关亲属；不含当事人本人、不含已亡者。</returns>
    /// <exception cref="ArgumentNullException">任一参数为 <see langword="null"/>。</exception>
    /// <remarks>
    /// 取自 <see cref="Family.Members"/>（**含归档成员**），故**已出嫁归档的女儿被娘家跨族追及**；
    /// 兄弟、孙辈、姻亲 MUST NOT 被波及。
    /// </remarks>
    public static IReadOnlyList<Person> RelativesOf(Family family, Person subject)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(subject);

        var result = new List<Person>();
        var seen = new HashSet<PersonId>();

        void Consider(Person? candidate)
        {
            if (candidate is null || candidate.Id == subject.Id)
            {
                return;
            }

            if (candidate.Status.HasFlag(StatusFlag.Deceased))
            {
                return;
            }

            if (seen.Add(candidate.Id))
            {
                result.Add(candidate);
            }
        }

        Consider(family.SpouseOf(subject.Id));

        foreach (var child in family.ChildrenOf(subject.Id))
        {
            Consider(child);
        }

        if (subject.FatherId is { } fatherId)
        {
            Consider(family.TryGet(fatherId));
        }

        if (subject.MotherId is { } motherId)
        {
            Consider(family.TryGet(motherId));
        }

        result.Sort(static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
        return result;
    }

    /// <summary>把比例罚金封顶到 <see cref="PunishmentPolicy.RatioFineCapGuan"/> 贯。</summary>
    private static Money CapRatioFine(Money amount)
    {
        var cap = Money.FromGuan(PunishmentPolicy.RatioFineCapGuan);
        return amount < cap ? amount : cap;
    }

    /// <summary>建立服刑：**先置状态位、后写计时**（<see cref="Person.Status"/> / <see cref="Person.Timers"/> 的交叉校验强制）。</summary>
    private static void EstablishSentence(Person person, int months)
    {
        person.Status |= StatusFlag.ServingSentence;

        var timers = person.Timers;
        person.Timers = new StatusTimers(
            sentenceRemainingMonths: months,
            examBanRemainingMonths: timers.ExamBanRemainingMonths,
            promotionBanRemainingMonths: timers.PromotionBanRemainingMonths,
            awaitingPostRemainingMonths: timers.AwaitingPostRemainingMonths);
    }

    /// <summary>
    /// 对被波及成员写禁考（与可选的禁升）计时，并追加**恰 1 条** <see cref="RelativeBanFact"/>。
    /// </summary>
    /// <returns>追加的事实行；该成员在**本次施加**中已被写过计时时返回 <see langword="null"/>（取替换、不叠加）。</returns>
    private static RelativeBanFact? Ban(
        PunishmentJournal journal,
        GameDate month,
        Person target,
        PersonId subjectId,
        int months,
        bool promotionBan,
        HashSet<PersonId> banned)
    {
        if (!banned.Add(target.Id))
        {
            return null;
        }

        if (months > 0)
        {
            target.Status |= StatusFlag.ExamBanned;
            var timers = target.Timers;
            target.Timers = new StatusTimers(
                sentenceRemainingMonths: timers.SentenceRemainingMonths,
                examBanRemainingMonths: months,
                promotionBanRemainingMonths: timers.PromotionBanRemainingMonths,
                awaitingPostRemainingMonths: timers.AwaitingPostRemainingMonths);
        }

        if (promotionBan)
        {
            target.Status |= StatusFlag.PromotionBanned;
            var timers = target.Timers;
            target.Timers = new StatusTimers(
                sentenceRemainingMonths: timers.SentenceRemainingMonths,
                examBanRemainingMonths: timers.ExamBanRemainingMonths,
                promotionBanRemainingMonths: months,
                awaitingPostRemainingMonths: timers.AwaitingPostRemainingMonths);
        }

        var fact = new RelativeBanFact(
            month, subjectId, target.Id, months, promotionBan ? months : null);
        journal.Append(fact);
        return fact;
    }

    /// <summary>
    /// 对一名相关亲属施加连坐的**互斥三分支**（契约十六 §6.2）；免死例外时返回 <see langword="null"/>。
    /// </summary>
    private static CollectivePunishmentFact? Collective(
        PunishmentJournal journal,
        GameDate month,
        Family family,
        Person subject,
        Person relative,
        HashSet<PersonId> banned,
        List<RelativeBanFact> relativeBans)
    {
        // ③ 有官职 ⇒ 受全族禁考禁升 48 月覆盖（MUST NOT 另立「禁升 60 月」）。
        if (relative.Rank is not null)
        {
            // 本分支的 `RelativeBanFact(48, 48)` 与 `CollectivePunishmentFact(FamilyWideBan)` 同属
            // 本次施加的后果（契约十六 §6.2），MUST 一并进入快照——故回收 Ban 的返回行，
            // 否则 Apply 的返回快照会漏掉该亲属的禁令行（日志仍已写入，只瞒了立即报告面）。
            var ban = Ban(
                journal, month, relative, subject.Id, PunishmentPolicy.FamilyWideBanMonths, true, banned);
            if (ban is { } relativeBan)
            {
                relativeBans.Add(relativeBan);
            }

            var familyWide = new CollectivePunishmentFact(
                month, subject.Id, CollectiveOutcome.FamilyWideBan, relative.Id, null, null);
            journal.Append(familyWide);
            return familyWide;
        }

        // ② 有功名 ∧ 无官职 ⇒ 降一级功名（恰 1 条 PunishmentDemotion 记录）。
        if (relative.CurrentDegree != DegreeLevel.BaiShen)
        {
            var before = relative.CurrentDegree;
            DemoteDegree(relative, month);
            var after = relative.CurrentDegree;

            var demoted = new CollectivePunishmentFact(
                month, subject.Id, CollectiveOutcome.DegreeDemoted, relative.Id, before, after);
            journal.Append(demoted);
            return demoted;
        }

        // ① 无功名 ∧ 无官职 ⇒ 处死；免死例外（配偶有官职 ∧ 该孩子未成年）时状态逐字段不变、不落事实行。
        if (IsExemptFromExecution(family, subject, relative, month))
        {
            return null;
        }

        Execute(relative);
        var executed = new CollectivePunishmentFact(
            month, subject.Id, CollectiveOutcome.Executed, relative.Id, null, null);
        journal.Append(executed);
        return executed;
    }

    /// <summary>免死例外（契约十六 §6.3）：当事人的配偶有官职 ∧ 该孩子未成年。</summary>
    /// <remarks>
    /// 判据**只适用于孩子**（规格书 §7.4 与 §17 ③ 裁决回写：不适用于当事人本人、不扩展到配偶以外的亲属），
    /// 故先核对 <paramref name="relative"/> 确为当事人的子女，再看其是否成年。
    /// </remarks>
    private static bool IsExemptFromExecution(Family family, Person subject, Person relative, GameDate month)
    {
        var spouse = family.SpouseOf(subject.Id);
        if (spouse is null || spouse.Rank is null)
        {
            return false;
        }

        if (!IsChildOf(family, subject, relative))
        {
            return false;
        }

        return !AgeBracketPolicy.IsAdult(relative.Gender, relative.AgeAt(month));
    }

    /// <summary>该成员是否为当事人的子女（与 <see cref="RelativesOf"/> 同口径：含已出嫁归档的女儿）。</summary>
    private static bool IsChildOf(Family family, Person subject, Person relative)
    {
        foreach (var child in family.ChildrenOf(subject.Id))
        {
            if (child.Id == relative.Id)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>死刑立即执行：置已亡（归档与族谱归类由 <see cref="Family"/> 的状态位派生）。</summary>
    private static void Execute(Person person) => person.Status |= StatusFlag.Deceased;

    /// <summary>全族禁令的施加范围：**在世在册**成员（按 <c>PersonId</c> 升序）。</summary>
    private static List<Person> LivingRegisteredMembers(Family family)
    {
        var members = new List<Person>(family.RegisteredMembers);
        members.Sort(static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
        return members;
    }
}
