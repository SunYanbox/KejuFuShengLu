using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;
using KFL.Presentation.Mvvm;

namespace KFL.Presentation.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    // ───────────────────────── 动作派发 ─────────────────────────

    private void Execute(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        var parts = token.Split(':');
        var head = parts[0];

        // 开局菜单专用的动作：此时**还没有会话**，MUST 在下面的空会话守卫之前处理
        // （否则「读取选中存档」会被静默吞掉，玩家反馈：点了没有任何效果）。
        if (head == "menuload")
        {
            if (SelectedMenuSave is { } menuSlot)
            {
                LoadGame(menuSlot.Value);
            }
            else
            {
                MenuSaveOverviewText = "没有可读取的存档。";
            }

            return;
        }

        if (_session is null)
        {
            return;
        }

        var error = string.Empty;

        switch (head)
        {
            case "advance":
                if (_session.CanAdvance)
                {
                    _session.AdvanceMonth();
                    AfterAdvance();
                }

                break;
            case "ffexam":
                FastForwardToExam();
                return;
            case "balance":
                IsBreakdownVisible = !IsBreakdownVisible;
                break;
            case "bank":
                error = ExecuteBank(parts.Length > 1 ? parts[1] : string.Empty);
                break;
            case "examrates":
                ConsoleOutput = BuildExamRatesText();
                ConsoleVisible = true;
                return;
            case "clearlog":
                _session.ClearLog();
                break;
            case "delete":
                error = DeleteSelectedSave();
                break;
            case "deletebackup":
                error = DeleteSelectedBackup();
                break;
            case "autotreat":
                AutoTreat = !AutoTreat;
                _session.Note(AutoTreat ? "自动治疗：开。" : "自动治疗：关。");
                break;
            case "loadbackup":
                if (SelectedBackup is { } backup)
                {
                    LoadBackup(backup.Value);
                }
                else
                {
                    error = "没有可读取的备份。";
                }

                break;
            case "ff":
                FastForward(parts.Length > 1 ? parts[1] : "12");
                return;
            case "stop":
                StopFastForward();
                break;
            case "exam":
                error = WithSelected(session => session.RegisterExam(Selected!.Person));
                break;
            case "special":
                error = WithSelected(session => session.AcceptSpecialTribute(Selected!.Person));
                break;
            case "bribe":
                error = WithSelected(session => session.Bribe(Selected!.Person, ParseExamLevel(parts[1])));
                break;
            case "assetbuy":
                error = _session.BuyAsset(SelectedAssetKind.Value, parts.Length > 1 ? int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 1);
                break;
            case "assetsell":
                error = _session.SellAsset(SelectedAssetKind.Value, parts.Length > 1 ? int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 1);
                break;
            case "money":
                error = _session.MoveMoney(parts[1] == "savings", Money.FromGuan(decimal.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture)));
                break;
            case "merchant":
                error = _session.MerchantCapital(parts[1] == "in", Money.FromGuan(decimal.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture)));
                break;
            case "repay":
                error = _session.RepayLoan(Money.FromGuan(decimal.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)));
                break;
            case "living":
                error = _session.SetLivingStandard(Enum.Parse<LivingStandard>(parts[1]));
                break;
            case "difficulty":
                error = _session.SetDifficulty(Enum.Parse<Difficulty>(parts[1]));
                break;
            case "autoexam":
                error = _session.SetAutoExamApplication(!_session.State.Family.AutoExamApplication);
                break;
            case "matchmake":
                error = WithSelected(session => session.Matchmake(Selected!.Person, parts.Length > 1 && parts[1] == "degree"));
                break;
            case "endogamy":
                error = WithSelected(_ => MarryWithinFamily(SelectedEndogamy?.Value ?? Selected!.Person.Id));
                break;
            case "divorce":
                error = WithSelected(session => session.Divorce(Selected!.Person));
                break;
            case "proposal":
                error = _session.RespondToProposal(parts[1] != "reject", parts.Length > 2 && parts[2] == "degree");
                break;
            case "childbirth":
                error = WithCouple();
                break;
            case "buyperson":
                error = _session.BuyPerson();
                break;
            case "treat":
                error = WithSelected(session => session.StartTreatment(Selected!.Person));
                break;
            case "tutor":
                error = WithSelected(session => session.SetTutor(Selected!.Person, !Selected!.Person.IsTutoredByMaster));
                break;
            case "head":
                error = WithSelected(session => session.SetHead(Selected!.Person));
                break;
            case "release":
                error = WithSelected(session => session.Release(Selected!.Person));
                break;
            case "pardon":
                error = WithSelected(session => session.Pardon(Selected!.Person));
                break;
            case "clearbans":
                error = WithSelected(session => session.ClearBans(Selected!.Person));
                break;
            case "clearfamilybans":
                error = _session.ClearFamilyBans();
                break;
            case "save":
                error = _session.Save();
                break;
            case "saveas":
                error = _session.SaveAs(SaveName, SelectedSaveFormat.Value);
                break;
            case "rename":
                error = _session.Rename(SaveName);
                break;
            case "export":
                error = _session.ExportLedgerCsv();
                break;
            case "backup":
                error = _session.Backup();
                break;
            case "load":
                LoadGame(parts.Length > 1 && parts[1] == "selected" && SelectedSave is { } chosen
                    ? chosen.Value
                    : new SaveSlot(SaveName, SaveFormat.Binary));
                return;
            case "menu":
                Detach();
                // 玩家反馈 12：回主菜单后必须**重新列一遍存档**——否则菜单里的
                // 「读取已有存档」还停在上一局的列表上（旧实现只在构造与读档后刷新）。
                RefreshSaves();
                RefreshBackups();
                RefreshMenuSaveOverview();
                return;
            case "select":
                SelectMember(parts.Length > 1 ? parts[1] : string.Empty);
                return;
            case "console":
                ConsoleVisible = !ConsoleVisible;
                return;
            case "quit":
                ConsoleVisible = false;
                return;
            default:
                error = $"未知命令：{token}";
                break;
        }

        if (error.Length > 0)
        {
            _session?.Note($"✗ {error}");
        }

        RefreshLog();
        RefreshAll();
    }

    private string WithSelected(Func<GameSession, string> action)
        => Selected is null ? "请先选择一名成员。" : action(_session!);
}
