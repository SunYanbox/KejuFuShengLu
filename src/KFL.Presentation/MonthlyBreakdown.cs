using System.Globalization;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;
using KFL.Rules.FamilyLifecycle;
using KFL.Rules.Settlement;

namespace KFL.Presentation;

/// <summary>一名成员在某一结算月内的收入 / 支出合计（界面投影，不是持久字段）。</summary>
/// <param name="Income">收入合计（正额）。</param>
/// <param name="Expense">支出合计（**正额**，展示时再加负号）。</param>
public sealed record MemberMonthly(Money Income, Money Expense)
{
    /// <summary>净额 = 收入 − 支出。</summary>
    public Money Net => Income - Expense;
}

/// <summary>
/// 「本月收支」投影：把「按当前设置推算的当月收支」与「上一结算月的实际流水」
/// 都整理成可以直接展示的文本（玩家反馈 7：实时显示每月收支 + 悬浮窗按人按来源罗列）。
/// </summary>
/// <remarks>
/// <b>只读</b>：全部金额都来自公开计算器（<see cref="IncomeCalculator"/>、
/// <see cref="LivingCostCalculator"/>）与只读流水账，不写任何状态、不消费随机。
/// </remarks>
public static class MonthlyBreakdown
{
    /// <summary>当前设置下推算的当月收入合计（不含储蓄利息 / 匠户岁入，二者只在 12 月出现）。</summary>
    /// <param name="state">游戏状态（只读）。</param>
    /// <returns>收入分项计算产物。</returns>
    public static IncomeComputation ProjectedIncome(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var economy = state.Economy;
        return IncomeCalculator.Compute(
            state.Family,
            state.CurrentDate,
            economy.Holdings,
            economy.Treasury,
            state.Difficulty,
            state.Origin);
    }

    /// <summary>当前设置下推算的当月生活费（逐成员）。</summary>
    /// <param name="state">游戏状态（只读）。</param>
    /// <returns>逐成员生活费明细。</returns>
    public static IReadOnlyList<LivingCostMemberLine> ProjectedLivingCost(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var economy = state.Economy;
        return LivingCostCalculator.PerMember(
            CountedMembers.Counted(state.Family),
            state.CurrentDate,
            economy.LivingStandard,
            economy.GrainPriceIndex.Value,
            state.Difficulty,
            state.Origin,
            economy.Famine.Stage);
    }

    /// <summary>当前设置下推算的名师束脩合计（每名受业成员一份，未付得起时结算会跳过）。</summary>
    /// <param name="state">游戏状态（只读）。</param>
    /// <returns>束脩合计与受业人数。</returns>
    public static (Money Total, int Count) ProjectedTutorFee(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var fee = Money.FromGuan(
            GrowthPolicy.TutorFeeGuanPerMonth * DifficultyRates.ExpenseFactor(state.Difficulty));
        var count = 0;
        foreach (var person in CountedMembers.Registered(state.Family))
        {
            if (person.IsTutoredByMaster)
            {
                count++;
            }
        }

        return (Money.FromWen(fee.Wen * count), count);
    }

    /// <summary>上一结算月（流水账里最近的一个月）；无流水时返回当前月。</summary>
    /// <param name="state">游戏状态（只读）。</param>
    /// <returns>最近有流水的年月。</returns>
    public static GameDate LastSettledMonth(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var latest = state.CurrentDate;
        var found = false;
        foreach (var entry in state.Economy.Ledger.Entries)
        {
            var later = entry.Date.Year > latest.Year
                || (entry.Date.Year == latest.Year && entry.Date.Month > latest.Month);
            if (!found || later)
            {
                latest = entry.Date;
                found = true;
            }
        }

        return latest;
    }

    /// <summary>上一结算月里每名成员的收入 / 支出合计（按流水账逐条归类）。</summary>
    /// <param name="state">游戏状态（只读）。</param>
    /// <returns>成员标识 → 收支合计；只含当月在流水里出现过的成员。</returns>
    public static IReadOnlyDictionary<Guid, MemberMonthly> ActualByMember(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var month = LastSettledMonth(state);
        var ledger = state.Economy.Ledger;
        var income = new Dictionary<Guid, Money>();
        var expense = new Dictionary<Guid, Money>();

        foreach (var entry in ledger.Entries)
        {
            if (entry.Date.Year != month.Year || entry.Date.Month != month.Month
                || entry.PersonId is not { } personId)
            {
                continue;
            }

            var bucket = entry.Amount.Wen >= 0m ? income : expense;
            var magnitude = Money.FromWen(Math.Abs(entry.Amount.Wen));
            bucket[personId.Value] = bucket.TryGetValue(personId.Value, out var current)
                ? current + magnitude
                : magnitude;
        }

        var result = new Dictionary<Guid, MemberMonthly>();
        foreach (var key in income.Keys.Concat(expense.Keys).Distinct())
        {
            result[key] = new MemberMonthly(
                income.TryGetValue(key, out var earned) ? earned : Money.Zero,
                expense.TryGetValue(key, out var spent) ? spent : Money.Zero);
        }

        return result;
    }

    /// <summary>成员详情里的一行「月收支」及其来源明细（上一结算月口径）。</summary>
    /// <param name="state">游戏状态（只读）。</param>
    /// <param name="person">成员。</param>
    /// <returns>若干行文本。</returns>
    public static IReadOnlyList<string> MemberLines(GameState state, Person person)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(person);

        var month = LastSettledMonth(state);
        var lines = new List<string>();
        var income = Money.Zero;
        var expense = Money.Zero;
        var parts = new Dictionary<LedgerCategory, decimal>();

        foreach (var entry in state.Economy.Ledger.Entries)
        {
            if (entry.Date.Year != month.Year || entry.Date.Month != month.Month
                || entry.PersonId is not { } personId || personId.Value != person.Id.Value)
            {
                continue;
            }

            parts.TryGetValue(entry.Category, out var sum);
            parts[entry.Category] = sum + entry.Amount.Wen;

            if (entry.Amount.Wen >= 0m)
            {
                income += entry.Amount;
            }
            else
            {
                expense += Money.FromWen(-entry.Amount.Wen);
            }
        }

        var projectedLiving = ProjectedLivingCost(state)
            .Where(line => line.PersonId.Value == person.Id.Value)
            .ToList();
        var projectedTutor = person.IsTutoredByMaster
            ? Money.FromGuan(GrowthPolicy.TutorFeeGuanPerMonth * DifficultyRates.ExpenseFactor(state.Difficulty))
            : Money.Zero;

        lines.Add($"月收支（上一结算月 {month.Year} 年 {month.Month} 月）："
            + $"收入 {WithSign(income)} ｜ 支出 {WithSign(-expense)} ｜ 结余 {WithSign(income - expense)}");

        var detail = parts
            .OrderByDescending(pair => Math.Abs(pair.Value))
            .Select(pair => $"{Display.Category(pair.Key)} {WithSign(Money.FromWen(pair.Value))}")
            .ToList();

        if (detail.Count > 0)
        {
            lines.Add($"　实际来源：{string.Join('；', detail)}");
        }

        var projected = new List<string>();
        foreach (var line in projectedLiving)
        {
            projected.Add($"生活费 −{Display.Money(line.Amount)}");
        }

        if (projectedTutor.Wen > 0m)
        {
            projected.Add($"名师束脩 −{Display.Money(projectedTutor)}");
        }

        var projectedIncome = ProjectedIncome(state).Lines
            .Where(line => line.PersonId is { } id && id.Value == person.Id.Value);
        foreach (var line in projectedIncome)
        {
            projected.Add($"{Display.Category(line.Category)} +{Display.Money(line.Amount)}");
        }

        if (projected.Count > 0)
        {
            lines.Add($"　本月推算（当前设置）：{string.Join('；', projected)}");
        }

        return lines;
    }

    /// <summary>收支悬浮窗全文：当月推算（按来源、按人）+ 上一结算月实际流水。</summary>
    /// <param name="state">游戏状态（只读）。</param>
    /// <returns>多行文本。</returns>
    public static string BuildPopupText(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var date = state.CurrentDate;
        var family = state.Family;
        var lines = new List<string>
        {
            $"— 本月推算（{date.Year} 年 {date.Month} 月，按当前设置）—",
        };

        var income = ProjectedIncome(state);
        var living = ProjectedLivingCost(state);
        var (tutorTotal, tutorCount) = ProjectedTutorFee(state);

        var incomeByCategory = new Dictionary<LedgerCategory, List<IncomeLine>>();
        foreach (var line in income.Lines)
        {
            if (!incomeByCategory.TryGetValue(line.Category, out var bucket))
            {
                bucket = [];
                incomeByCategory.Add(line.Category, bucket);
            }

            bucket.Add(line);
        }

        lines.Add("── 收入 ──");
        var incomeTotal = Money.Zero;
        foreach (var pair in incomeByCategory.OrderByDescending(p => p.Value.Sum(line => line.Amount.Wen)))
        {
            var subtotal = Money.FromWen(pair.Value.Sum(line => line.Amount.Wen));
            incomeTotal += subtotal;
            lines.Add($"　{Display.Category(pair.Key)} +{Display.Money(subtotal)}");
            foreach (var line in pair.Value.OrderByDescending(l => l.Amount.Wen))
            {
                lines.Add($"　　{NameOf(family, line.PersonId)}　+{Display.Money(line.Amount)}");
            }
        }

        if (incomeTotal.Wen <= 0m)
        {
            lines.Add("　（无）");
        }

        lines.Add("── 支出 ──");
        var livingByBracket = new Dictionary<AgeBracket, List<LivingCostMemberLine>>();
        foreach (var line in living)
        {
            if (!livingByBracket.TryGetValue(line.AgeBracket, out var bucket))
            {
                bucket = [];
                livingByBracket.Add(line.AgeBracket, bucket);
            }

            bucket.Add(line);
        }

        var livingTotal = Money.FromWen(living.Sum(line => line.Amount.Wen));
        lines.Add($"　生活支出 −{Display.Money(livingTotal)}");
        foreach (var pair in livingByBracket)
        {
            lines.Add($"　　{BracketName(pair.Key)} {pair.Value.Count} 人");
            foreach (var line in pair.Value)
            {
                lines.Add($"　　　{NameOf(family, line.PersonId)}　−{Display.Money(line.Amount)}");
            }
        }

        if (tutorCount > 0)
        {
            lines.Add($"　名师束脩 −{Display.Money(tutorTotal)}（{tutorCount} 人）");
            foreach (var person in CountedMembers.Registered(family).Where(p => p.IsTutoredByMaster))
            {
                lines.Add($"　　{person.Name}　−{Display.Money(Money.FromWen(tutorTotal.Wen / tutorCount))}");
            }
        }

        var expenseTotal = livingTotal + tutorTotal;
        var net = incomeTotal - expenseTotal;

        lines.Add($"══ 收入 +{Display.Money(incomeTotal)} ｜ 支出 −{Display.Money(expenseTotal)} ｜ 预计结余 {WithSign(net)} ══");

        var month = LastSettledMonth(state);
        lines.Add(string.Empty);
        lines.Add($"— 上一结算月实际流水（{month.Year} 年 {month.Month} 月）—");

        var actual = new Dictionary<LedgerCategory, List<LedgerEntry>>();
        foreach (var entry in state.Economy.Ledger.Entries)
        {
            if (entry.Date.Year != month.Year || entry.Date.Month != month.Month)
            {
                continue;
            }

            if (!actual.TryGetValue(entry.Category, out var bucket))
            {
                bucket = [];
                actual.Add(entry.Category, bucket);
            }

            bucket.Add(entry);
        }

        if (actual.Count == 0)
        {
            lines.Add("　（尚无流水）");
            return string.Join(Environment.NewLine, lines);
        }

        var actualIncome = Money.Zero;
        var actualExpense = Money.Zero;
        foreach (var pair in actual.OrderByDescending(p => p.Value.Sum(e => Math.Abs(e.Amount.Wen))))
        {
            var subtotal = Money.FromWen(pair.Value.Sum(e => e.Amount.Wen));
            if (subtotal.Wen >= 0m)
            {
                actualIncome += subtotal;
            }
            else
            {
                actualExpense += Money.FromWen(-subtotal.Wen);
            }

            lines.Add($"　{Display.Category(pair.Key)} {WithSign(subtotal)}");
            foreach (var entry in pair.Value.OrderByDescending(e => Math.Abs(e.Amount.Wen)))
            {
                lines.Add($"　　{NameOf(family, entry.PersonId)}　{WithSign(entry.Amount)}");
            }
        }

        lines.Add($"══ 实际收入 +{Display.Money(actualIncome)} ｜ 实际支出 −{Display.Money(actualExpense)} ｜ 实际结余 {WithSign(actualIncome - actualExpense)} ══");

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>带正负号的金额文案（负数用全角减号，正数补 +）。</summary>
    /// <param name="amount">金额。</param>
    /// <returns>带符号文本。</returns>
    public static string WithSign(Money amount)
        => amount.Wen >= 0m ? $"+{Display.Money(amount)}" : $"−{Display.Money(Money.FromWen(-amount.Wen))}";

    private static string NameOf(Family family, PersonId? personId)
        => personId is { } id
            ? family.TryGet(id)?.Name ?? "（外人）"
            : "（家族）";

    private static string BracketName(AgeBracket bracket) => bracket switch
    {
        AgeBracket.Adult => "成人",
        AgeBracket.Youth => "青年",
        AgeBracket.Elder => "老人",
        AgeBracket.Child => "儿童",
        _ => bracket.ToString(),
    };

    /// <summary>把数字格式化成「0.##」（仅调试 / 诊断用）。</summary>
    /// <param name="value">数值。</param>
    /// <returns>文本。</returns>
    internal static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
