using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;
using KFL.Rules.Economy;
using KFL.Rules.Punishment;
using KFL.Rules.Settlement;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// US2 / SC-004、SC-005、quickstart 场景 3、4：被抓当刻的 12 格矩阵、服刑、罚金与亲属禁考禁升。
/// </summary>
/// <remarks>
/// <para>
/// 期望值一律经 <c>KFL.Rules/Config</c> 的成员现算（<see cref="PunishmentPolicy.CellOf"/>、
/// <see cref="DifficultyRates"/>、<see cref="TotalAssetsCalculator"/>），本文件 MUST NOT
/// 另存规则数值副本（SC-008）。12 格的**字面值**锚点在 <c>SpecAnchorTests</c>，本文件只断言
/// 「该格被如实施加」。
/// </para>
/// <para>
/// 夹具：当事人（无功名无官职）+ 配偶（举人）+ 一名**非亲属**的兄弟成员。
/// 兄弟用于观察「全族禁考禁升」的覆盖范围（连坐 MUST NOT 波及兄弟，但全族禁令覆盖全部在册成员）。
/// </para>
/// </remarks>
public class PunishmentMatrixTests
{
    private static readonly Guid StateId = Guid.Parse("7c1d5e93-2a48-4f61-8b0c-5d6e7f8091a2");

    /// <summary>12 格逐格：施加后当事人与相关亲属的计时 / 状态位 / 罚金 / 快照与 <c>CellOf</c> 逐项一致。</summary>
    /// <param name="difficulty">难度（矩阵的行）。</param>
    /// <param name="level">被抓时的考试级别（矩阵的列）。</param>
    [Theory]
    [InlineData(Difficulty.Easy, ExamLevel.DianShi)]
    [InlineData(Difficulty.Easy, ExamLevel.ShengShi)]
    [InlineData(Difficulty.Easy, ExamLevel.JieShi)]
    [InlineData(Difficulty.Normal, ExamLevel.DianShi)]
    [InlineData(Difficulty.Normal, ExamLevel.ShengShi)]
    [InlineData(Difficulty.Normal, ExamLevel.JieShi)]
    [InlineData(Difficulty.Hard, ExamLevel.DianShi)]
    [InlineData(Difficulty.Hard, ExamLevel.ShengShi)]
    [InlineData(Difficulty.Hard, ExamLevel.JieShi)]
    [InlineData(Difficulty.Hell, ExamLevel.DianShi)]
    [InlineData(Difficulty.Hell, ExamLevel.ShengShi)]
    [InlineData(Difficulty.Hell, ExamLevel.JieShi)]
    public void 十二格逐格施加后果与惩罚格一致(Difficulty difficulty, ExamLevel level)
    {
        // 资金充裕（10000 贯）⇒ 12 格的罚金都能被资金池全额覆盖，条目金额恰等于罚金额。
        var (subject, _, sibling, state) = Scenario(difficulty, cash: RulesHarness.Guan(10_000m));
        var cell = PunishmentPolicy.CellOf(difficulty, level);

        var poolBefore = state.Economy.Treasury.Cash + state.Economy.Treasury.Savings;
        var expectedFine = ExpectedFine(cell, state.Economy, difficulty);

        var snapshot = PunishmentImposer.Apply(state, subject, level, new FixedRandomService());

        Assert.Equal(RulesHarness.Date, snapshot.Month);

        // ② 服刑：只有 SentenceMonths > 0 的格子建立计时与状态位（死刑格为 0）。
        if (cell.SentenceMonths > 0)
        {
            Assert.True(subject.Status.HasFlag(StatusFlag.ServingSentence));
            Assert.Equal(cell.SentenceMonths, subject.Timers.SentenceRemainingMonths);
            Assert.Equal(new SentenceFact(RulesHarness.Date, subject.Id, cell.SentenceMonths), Assert.Single(snapshot.Sentences));
        }
        else
        {
            Assert.False(subject.Status.HasFlag(StatusFlag.ServingSentence));
            Assert.Null(subject.Timers.SentenceRemainingMonths);
            Assert.Empty(snapshot.Sentences);
        }

        // ⑤ 死刑：立即执行（置已亡），MUST NOT 建立服刑计时。
        Assert.Equal(cell.Execution == ExecutionKind.ImmediateSentence, subject.Status.HasFlag(StatusFlag.Deceased));
        Assert.Equal(subject.Status.HasFlag(StatusFlag.Deceased), subject.Timers.SentenceRemainingMonths is null);

        // ③ 罚金：恰 1 条资金类条目，金额 = **池内实际付出额**（规格书 §12.3：资金类条目的金额 =
        // 本次的资金池变动额；缺口转贷款不落条目）；池足额时该值恰等于罚金额。金额 0 ⇒ 不落条目。
        var expectedEntry = expectedFine < poolBefore ? expectedFine : poolBefore;
        var fineEntries = state.Economy.Ledger.Entries
            .Where(entry => entry.Category == LedgerCategory.Fine)
            .ToArray();

        if (expectedEntry.IsPositive)
        {
            var entry = Assert.Single(fineEntries);
            Assert.Equal(-expectedEntry, entry.Amount);
            Assert.Equal(subject.Id, entry.PersonId);
            Assert.Equal(LedgerEntryKind.Treasury, LedgerCategoryMetadata.KindOf(LedgerCategory.Fine));
        }
        else
        {
            Assert.Empty(fineEntries);
        }

        // ④ 相关亲属禁考与 / 或禁升（全族格由 ⑦ 统一施加，故此处按同一份期望断言）。
        foreach (var relative in PunishmentImposer.RelativesOf(state.Family, subject))
        {
            AssertRelativeBan(cell, relative);
        }

        // ⑦ 全族禁考禁升 48 月：逐人计时，覆盖全部在世在册成员（含非亲属的兄弟）。
        Assert.Equal(cell.FamilyWideBan, snapshot.FamilyBanDeclared);
        Assert.Equal(cell.FamilyWideBan, state.Family.PunishmentJournal.FamilyBanDeclared);

        if (cell.FamilyWideBan)
        {
            Assert.Equal(
                PunishmentPolicy.FamilyWideBanMonths, sibling.Timers.ExamBanRemainingMonths);
            Assert.Equal(
                PunishmentPolicy.FamilyWideBanMonths, sibling.Timers.PromotionBanRemainingMonths);
        }
        else
        {
            // 兄弟不是「配偶 / 儿子 / 女儿 / 父亲 / 母亲」，故 MUST NOT 被连坐波及。
            Assert.Null(sibling.Timers.ExamBanRemainingMonths);
            Assert.Null(sibling.Timers.PromotionBanRemainingMonths);
        }

        // 账本里只允许出现这三类：罚金、行贿支出，以及 2026-10-19 起为人口统计图追加的
        // **入狱事件**（服刑格各落一条；事件类不动资金池，故不参与 SC-005 求和）。
        Assert.True(state.Economy.Ledger.Entries.All(
            entry => entry.Category is LedgerCategory.Fine
                or LedgerCategory.BribeExpense
                or LedgerCategory.MemberImprisoned));

        Assert.Equal(PunishmentQueries.PrisonerCount(state.Family, RulesHarness.Date), snapshot.PrisonerCount);
        Assert.Equal(subject.Status.HasFlag(StatusFlag.ServingSentence), snapshot.PrisonerCount == 1);
    }

    /// <summary>SC-005：付不起罚金时现金与储蓄双清零、缺口转贷款本金，各池 MUST NOT 为负。</summary>
    [Fact]
    public void 罚金超过现金加储蓄时双清零且缺口转为贷款本金()
    {
        var (subject, _, _, state) = Scenario(Difficulty.Normal, cash: RulesHarness.Guan(20m), savings: RulesHarness.Guan(10m));
        var cell = PunishmentPolicy.CellOf(Difficulty.Normal, ExamLevel.JieShi);

        var fine = PunishmentImposer.ComputeFine(cell, state.Economy, Difficulty.Normal);
        var pool = state.Economy.Treasury.Cash + state.Economy.Treasury.Savings;

        Assert.True(fine > pool, "本夹具必须让罚金**超过**可付池，才谈得上「余额转贷款」。");

        PunishmentImposer.Apply(state, subject, ExamLevel.JieShi, new FixedRandomService());

        Assert.Equal(Money.Zero, state.Economy.Treasury.Cash);
        Assert.Equal(Money.Zero, state.Economy.Treasury.Savings);
        Assert.Equal(fine - pool, state.Economy.Treasury.Loan.Principal);

        // 规格书 §12.3：资金类条目的金额 = **本次的资金池变动额**——缺口转贷款不改变资金池，
        // 故条目金额记的是池内付出的 30 贯而非罚金总额；`PaymentResult.Total` 仍恰等于罚金额。
        var entry = Assert.Single(
            state.Economy.Ledger.Entries, e => e.Category == LedgerCategory.Fine);
        Assert.Equal(LedgerCategory.Fine, entry.Category);
        Assert.Equal(-pool, entry.Amount);
        Assert.Equal(fine, (entry.Amount * -1m) + state.Economy.Treasury.Loan.Principal);
        Assert.True(state.Economy.Treasury.Cash.IsNegative is false);
        Assert.True(state.Economy.Treasury.Savings.IsNegative is false);
        Assert.True(state.Economy.Treasury.Loan.Principal.IsNegative is false);
    }

    /// <summary>SC-005：付得起时罚金从现金与储蓄按序划扣，且总额恰为罚金。</summary>
    [Fact]
    public void 可付罚金按现金再储蓄划扣且不留贷款()
    {
        var (subject, _, _, state) = Scenario(Difficulty.Normal, cash: RulesHarness.Guan(1000m), savings: RulesHarness.Guan(500m));
        var cell = PunishmentPolicy.CellOf(Difficulty.Normal, ExamLevel.JieShi);

        var fine = PunishmentImposer.ComputeFine(cell, state.Economy, Difficulty.Normal);

        PunishmentImposer.Apply(state, subject, ExamLevel.JieShi, new FixedRandomService());

        Assert.Equal(RulesHarness.Guan(1000m) - fine, state.Economy.Treasury.Cash);
        Assert.Equal(RulesHarness.Guan(500m), state.Economy.Treasury.Savings);
        Assert.Equal(Money.Zero, state.Economy.Treasury.Loan.Principal);
        Assert.Equal(
            -fine,
            Assert.Single(state.Economy.Ledger.Entries, e => e.Category == LedgerCategory.Fine).Amount);
    }

    /// <summary>SC-005：比例罚金对**最终金额**封顶（先乘支出系数、再封顶 50000 贯）。</summary>
    [Fact]
    public void 超大总资产的比例罚金恰为封顶值()
    {
        var (subject, _, _, state) = Scenario(Difficulty.Normal, cash: RulesHarness.Guan(1_000_000m));
        var cell = PunishmentPolicy.CellOf(Difficulty.Normal, ExamLevel.DianShi);

        Assert.True(cell.Fine == FineKind.Ratio, "本夹具要求比例罚金格（普通殿试 15%）。");

        var uncapped = cell.FineRatio * TotalAssetsCalculator.Of(state.Economy).Guan
            * DifficultyRates.ExpenseFactor(Difficulty.Normal);

        Assert.True(uncapped > PunishmentPolicy.RatioFineCapGuan, "本夹具的总资产必须让比例罚金超过封顶。");

        var fine = PunishmentImposer.ComputeFine(cell, state.Economy, Difficulty.Normal);

        Assert.Equal(Money.FromGuan(PunishmentPolicy.RatioFineCapGuan), fine);
        Assert.Equal(PunishmentPolicy.RatioFineCapGuan, fine.Guan);

        PunishmentImposer.Apply(state, subject, ExamLevel.DianShi, new FixedRandomService());

        Assert.Equal(
            -fine,
            Assert.Single(state.Economy.Ledger.Entries, e => e.Category == LedgerCategory.Fine).Amount);
    }

    /// <summary>SC-005：总资产为负（罚金基数按 0）⇒ 罚金 0 贯、**不落条目**、资金池零变动。</summary>
    [Fact]
    public void 总资产为负时罚金为零且不落条目()
    {
        var (subject, _, _, state) = Scenario(Difficulty.Normal);
        state.Economy.Treasury.Loan.Principal = RulesHarness.Guan(500m);

        Assert.Equal(Money.Zero, PunishmentImposer.FineBase(state.Economy));
        Assert.Equal(Money.Zero, TotalAssetsCalculator.Of(state.Economy));

        var cell = PunishmentPolicy.CellOf(Difficulty.Normal, ExamLevel.DianShi);

        Assert.True(cell.Fine is FineKind.Ratio);
        Assert.Equal(Money.Zero, PunishmentImposer.ComputeFine(cell, state.Economy, Difficulty.Normal));

        PunishmentImposer.Apply(state, subject, ExamLevel.DianShi, new FixedRandomService());

        // 罚金 0 ⇒ **不落罚金条目**（资金类条目不变量）；入狱事件条目与资金池无关，另计。
        Assert.DoesNotContain(
            state.Economy.Ledger.Entries, entry => entry.Category == LedgerCategory.Fine);
        Assert.Equal(Money.Zero, state.Economy.Treasury.Cash);
        Assert.Equal(Money.Zero, state.Economy.Treasury.Savings);
        Assert.Equal(RulesHarness.Guan(500m), state.Economy.Treasury.Loan.Principal);

        // 服刑事件照样落在快照里（罚金 0 只影响账本，不影响服刑）。
        Assert.Equal(cell.SentenceMonths, Assert.Single(
            PunishmentImposer.Apply(state, subject, ExamLevel.DianShi, new FixedRandomService()).Sentences).Months);
    }

    /// <summary>同上取替换不叠加：同一人再次被告知时取本次施加值，剩余月数既不增加也不缩短错误方向。</summary>
    [Fact]
    public void 同档再次施加以替换而非叠加()
    {
        var (subject, spouse, _, state) = Scenario(Difficulty.Normal);
        var cell = PunishmentPolicy.CellOf(Difficulty.Normal, ExamLevel.DianShi);

        Assert.True(cell.RelativeBanMonths > 0 && cell.SentenceMonths > 0);

        PunishmentImposer.Apply(state, subject, ExamLevel.DianShi, new FixedRandomService());
        PunishmentImposer.Apply(state, subject, ExamLevel.DianShi, new FixedRandomService());

        Assert.Equal(cell.SentenceMonths, subject.Timers.SentenceRemainingMonths);
        Assert.Equal(cell.RelativeBanMonths, spouse.Timers.ExamBanRemainingMonths);
    }

    /// <summary>构造当事人 + 配偶（举人）+ 非亲属兄弟的标准场景。</summary>
    private static (Person Subject, Person Spouse, Person Sibling, GameState State) Scenario(
        Difficulty difficulty,
        Money cash = default,
        Money savings = default)
    {
        var subject = RulesHarness.Member(1, Gender.Male, 40);
        var spouse = RulesHarness.Member(2, Gender.Female, 38);
        var sibling = RulesHarness.Member(3, Gender.Male, 36);

        spouse.AppendDegree(new DegreeRecord(
            DegreeLevel.JuRen, placement: null, RulesHarness.Date, DegreeChangeCause.ExamPass));

        var family = RulesHarness.FamilyOf(subject, spouse, sibling);
        family.Marry(subject.Id, spouse.Id);

        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(cash, savings),
            new Holdings());

        var state = new GameState(StateId, RulesHarness.Date, difficulty, Origin.Scholar, family, economy);

        return (subject, spouse, sibling, state);
    }

    /// <summary>按契约十六 §4.2 从配置成员**独立**推导该格的罚金。</summary>
    private static Money ExpectedFine(PunishmentCell cell, FamilyEconomy economy, Difficulty difficulty)
    {
        var expenseFactor = DifficultyRates.ExpenseFactor(difficulty);

        return cell.Fine switch
        {
            FineKind.None => Money.Zero,
            FineKind.Fixed => cell.FineFixedGuan * expenseFactor,
            FineKind.Ratio => Cap(
                Money.FromGuan(cell.FineRatio * TotalAssetsCalculator.Of(economy).Guan) * expenseFactor),
            _ => throw new ArgumentOutOfRangeException(nameof(cell), cell.Fine, "未登记的罚金计法。"),
        };

        static Money Cap(Money amount)
        {
            var cap = Money.FromGuan(PunishmentPolicy.RatioFineCapGuan);
            return amount < cap ? amount : cap;
        }
    }

    /// <summary>断言一名相关亲属的禁考 / 禁升计时与所施加的格一致。</summary>
    private static void AssertRelativeBan(PunishmentCell cell, Person relative)
    {
        if (cell.FamilyWideBan)
        {
            // 全族格：连坐分支或 ⑦ 的逐人覆盖都必须给出 48 月双计时。
            Assert.Equal(PunishmentPolicy.FamilyWideBanMonths, relative.Timers.ExamBanRemainingMonths);
            Assert.Equal(PunishmentPolicy.FamilyWideBanMonths, relative.Timers.PromotionBanRemainingMonths);
            return;
        }

        if (cell.RelativeBanMonths == 0)
        {
            // 「无禁考」格：MUST NOT 建立任何禁考 / 禁升计时。
            Assert.Null(relative.Timers.ExamBanRemainingMonths);
            Assert.Null(relative.Timers.PromotionBanRemainingMonths);
            return;
        }

        Assert.Equal(cell.RelativeBanMonths, relative.Timers.ExamBanRemainingMonths);
        Assert.Equal(
            cell.RelativePromotionBan ? cell.RelativeBanMonths : null,
            relative.Timers.PromotionBanRemainingMonths);
    }
}
