using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.FamilyLifecycle;
using KFL.Presentation.Mvvm;

namespace KFL.Presentation.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>把选中成员与指定候选者结为族内夫妻（玩家反馈 1）。</summary>
    private string MarryWithinFamily(PersonId candidateId)
    {
        if (_session is null || Selected is null)
        {
            return "请先选择一名成员。";
        }

        return _session.State.Family.TryGet(candidateId) is { } candidate
            ? _session.MarryWithinFamily(Selected.Person, candidate)
            : "候选者不存在。";
    }

    /// <summary>
    /// 自由金额输入（需求 13）：把输入框解析成贯；空 / 非法 / 非正数一律回落成 10 贯
    /// （与旧按钮「存 10 贯」的手感一致，不会因为输入框没填就报错）。
    /// </summary>
    private static Money ParseMoneyInput(string text)
        => decimal.TryParse(
                text,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var guan) && guan > 0m
            ? Money.FromGuan(guan)
            : Money.FromGuan(10m);

    /// <summary>资金操作（需求 13）：储蓄 / 取现 / 还贷 / 商本投入与抽回，金额来自输入框。</summary>
    private string ExecuteBank(string action)
    {
        if (_session is null)
        {
            return "尚未开局。";
        }

        var amount = ParseMoneyInput(MoneyAmountText);

        return action switch
        {
            "savings" => _session.MoveMoney(toSavings: true, amount),
            "cash" => _session.MoveMoney(toSavings: false, amount),
            "repay" => _session.RepayLoan(amount),
            "merchantin" => _session.MerchantCapital(inject: true, amount),
            "merchantout" => _session.MerchantCapital(inject: false, amount),
            _ => "未知的资金操作。",
        };
    }

    /// <summary>删除选中存档（需求 9）。确认由界面层（MessageBox）负责，本方法只执行。</summary>
    private string DeleteSelectedSave()
    {
        if (SelectedSave is not { } chosen)
        {
            return "没有选中的存档。";
        }

        var deletingCurrent = _session is not null && _session.Slot.Equals(chosen.Value);
        var error = GameSession.DeleteSave(chosen.Value);
        if (error.Length == 0)
        {
            if (deletingCurrent)
            {
                Detach();
            }

            RefreshSaves();
            _session?.Note($"◆ 已删除存档「{chosen.Value.Name}」。");
        }

        return error;
    }

    /// <summary>删除选中备份（需求 9）。</summary>
    private string DeleteSelectedBackup()
    {
        if (SelectedBackup is not { } chosen)
        {
            return "没有选中的备份。";
        }

        var error = GameSession.DeleteBackup(chosen.Value);
        if (error.Length == 0)
        {
            RefreshBackups();
        }

        return error;
    }

    private string WithCouple()
    {
        if (Selected is null || _session is null)
        {
            return "请先选择一名成员。";
        }

        var family = _session.State.Family;
        var person = Selected.Person;
        if (person.SpouseId is not { } spouseId || family.TryGet(spouseId) is not { } spouse)
        {
            return "所选成员无配偶，无法申请生育。";
        }

        var couple = person.Gender == Gender.Male
            ? new CoupleKey(person.Id, spouse.Id)
            : new CoupleKey(spouse.Id, person.Id);

        var feasibility = LifecycleQueries.ChildbirthFeasibility(_session.State, couple, _session.State.CurrentDate);
        if (!feasibility.Available)
        {
            return $"生育不可行：{Display.LifecycleReason(feasibility.RejectReason)}。";
        }

        var outcome = _session.ApplyChildbirth(couple);
        if (outcome.Length == 0)
        {
            _childbirthPending.Add(person.Id.Value);
            _childbirthPending.Add(spouse.Id.Value);
        }

        return outcome;
    }

    private static ExamLevel ParseExamLevel(string text) => text switch
    {
        "JieShi" => ExamLevel.JieShi,
        "ShengShi" => ExamLevel.ShengShi,
        "DianShi" => ExamLevel.DianShi,
        _ => ExamLevel.JieShi,
    };
}
