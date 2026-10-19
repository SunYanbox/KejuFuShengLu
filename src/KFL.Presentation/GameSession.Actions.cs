using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Bribery;
using KFL.Rules.Config;
using KFL.Rules.Economy;
using KFL.Rules.Exam;

namespace KFL.Presentation;

public sealed partial class GameSession
{
    // ───────────────────────── 科举与行贿 ─────────────────────────

    public string RegisterExam(Person person)
    {
        var level = ExamRegistration.TargetLevelOf(person);
        if (level is null)
        {
            return $"{person.Name} 暂无可报考科目。";
        }

        if (!ExamRegistration.CanRegister(person, State.CurrentDate))
        {
            return $"{person.Name} 本月不可报名 {Display.ExamLevel(level.Value)}（非报名窗口或资格不符）。";
        }

        ExamRegistration.Register(person, State.CurrentDate);
        var cost = Money.FromGuan(ExamPolicy.ExamTravelCostGuan(State.Difficulty));
        Note($"✎ {person.Name} 报名 {Display.ExamLevel(level.Value)}（路费约 {Display.Money(cost)}）。");
        Changed?.Invoke();
        return string.Empty;
    }

    public string AcceptSpecialTribute(Person person)
    {
        if (!ExamRegistration.SpecialTributeEligible(person, State.CurrentDate))
        {
            return $"{person.Name} 不符合特奏名资格。";
        }

        ExamRegistration.AcceptSpecialTribute(person, State.CurrentDate, _random, State);
        Note($"✎ {person.Name} 接受特奏名，授 L18 待阙。");
        Changed?.Invoke();
        return string.Empty;
    }

    public string Bribe(Person person, ExamLevel level)
    {
        if (!BribeRules.CanBribe(State, person, level, out var feasibility))
        {
            return $"行贿 {Display.ExamLevel(level)} 不可行：{Display.BribeReason(feasibility.RejectReason)}。";
        }

        var outcome = BribeEntry.Bribe(State, person, level, _random);
        if (!outcome.Accepted)
        {
            return $"{person.Name} 行贿被拒：{Display.BribeReason(outcome.RejectReason)}。";
        }

        Note($"💸 {person.Name} 行贿 {Display.ExamLevel(level)} 第 {outcome.Nth} 次，{Display.Money(outcome.Amount)}（现银 {Display.Money(outcome.FromCash)} + 储蓄 {Display.Money(outcome.FromSavings)}），被抓概率 {(double)feasibility.Probability:P1}。");
        Changed?.Invoke();
        return string.Empty;
    }

    public string ResetBribe(Person person)
    {
        BribeEntry.ResetBribeCounts(person, null);
        Note($"（调试）已清零 {person.Name} 的行贿次数与乘数。");
        Changed?.Invoke();
        return string.Empty;
    }

    // ───────────────────────── 家计 ─────────────────────────

    public string BuyAsset(AssetKind kind, int count)
    {
        if (State.Economy.Holdings is null)
        {
            return "无产业账本。";
        }

        try
        {
            var paid = AssetMarket.Buy(State, kind, count);
            Note($"🏠 置办 {Display.Asset(kind)} ×{count}，付出 {Display.Money(paid)}。");
            Changed?.Invoke();
            return string.Empty;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    public string SellAsset(AssetKind kind, int count)
    {
        try
        {
            var gained = AssetMarket.Sell(State, kind, count);
            Note($"💱 变卖 {Display.Asset(kind)} ×{count}，得银 {Display.Money(gained)}。");
            Changed?.Invoke();
            return string.Empty;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    public string MoveMoney(bool toSavings, Money amount)
    {
        var treasury = State.Economy.Treasury;
        try
        {
            if (toSavings)
            {
                treasury.TransferToSavings(amount);
            }
            else
            {
                treasury.TransferToCash(amount);
            }

            Note($"（{Display.Money(amount)} 转入{(toSavings ? "储蓄" : "现银")}）");
            Changed?.Invoke();
            return string.Empty;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    public string MerchantCapital(bool inject, Money amount)
    {
        var treasury = State.Economy.Treasury;
        try
        {
            if (inject)
            {
                treasury.InjectMerchantCapital(amount);
            }
            else
            {
                treasury.WithdrawMerchantCapital(amount);
            }

            Note($"（商本{(inject ? "投入" : "抽回")} {Display.Money(amount)}）");
            Changed?.Invoke();
            return string.Empty;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    public string RepayLoan(Money amount)
    {
        try
        {
            var repayment = State.Economy.RepayLoan(amount, State.CurrentDate);
            Note($"（还贷：本金 {Display.Money(repayment.PrincipalPart)}，利息 {Display.Money(repayment.InterestPart)}）");
            Changed?.Invoke();
            return string.Empty;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    public string SetLivingStandard(LivingStandard standard)
    {
        State.Economy.PendingLivingStandard = standard;
        Note($"（生活费档位切换为「{Display.LivingStandard(standard)}」，次月生效）");
        Changed?.Invoke();
        return string.Empty;
    }

    public string SetDifficulty(Difficulty difficulty)
    {
        State.PendingDifficulty = difficulty;
        Note($"（难度切换为「{Display.Difficulty(difficulty)}」，次月生效）");
        Changed?.Invoke();
        return string.Empty;
    }

    public string SetAutoExamApplication(bool enabled)
    {
        State.Family.AutoExamApplication = enabled;
        Note($"（持续报考：{(enabled ? "开" : "关")}）");
        Changed?.Invoke();
        return string.Empty;
    }
}
