using KFL.Core.Entities;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Achievements;
using KFL.Infrastructure.Save;
using KFL.Infrastructure.Services;
using KFL.Rules.Config;
using KFL.Rules.Save;
using System.IO;

namespace KFL.Presentation;

public sealed partial class GameSession
{
    // ───────────────────────── 存档 ─────────────────────────

    public string Save()
    {
        try
        {
            _saves.Save(State, Slot, overwrite: true);
            Note($"◆ 已存档「{Slot.Name}」。");
            return string.Empty;
        }
        catch (SaveFileException ex)
        {
            return $"存档失败：{ex.Message}";
        }
    }

    public string SaveAs(string name, SaveFormat format)
    {
        try
        {
            var slot = new SaveSlot(name, format);

            // 另存为 = 以**新存档标识**复制一份（同一标识并存于两个档名是契约九 §5 禁止的状态，
            // 旧的「直接 Save」正是「存档标识已存在于另一个档名下」崩溃的来源）。
            State = _saves.SaveAsFork(State, slot);
            Slot = slot;
            _runner = new TurnRunner(_random, _saves, Slot, _names, _achievements);
            Note($"◆ 已另存为「{name}」（新存档标识）。");
            Changed?.Invoke();
            return string.Empty;
        }
        catch (SaveFileException ex)
        {
            return $"另存失败：{ex.Message}";
        }
    }

    /// <summary>仅改档名，存档标识不变（契约九 §5：改名 MUST NOT 变更 SaveId）。</summary>
    /// <param name="newName">新档名。</param>
    /// <returns>失败原因；成功为空串。</returns>
    public string Rename(string newName)
    {
        try
        {
            Slot = _saves.Rename(Slot, newName);
            _runner = new TurnRunner(_random, _saves, Slot, _names, _achievements);
            Note($"◆ 已改名为「{Slot.Name}」。");
            Changed?.Invoke();
            return string.Empty;
        }
        catch (SaveFileException ex)
        {
            return $"改名失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 删除一个存档主文件（2026-10-19 需求 9）。删除**当前档**时同时结束本会话。
    /// </summary>
    /// <param name="slot">档名 + 格式。</param>
    /// <returns>成功且确实删了 ⇒ 空串；否则为给玩家看的原因。</returns>
    public static string DeleteSave(SaveSlot slot)
    {
        try
        {
            var saves = new SaveService();
            return saves.Delete(slot) ? string.Empty : $"存档不存在：{slot.Name}";
        }
        catch (SaveFileException ex)
        {
            return $"删除失败：{ex.Message}";
        }
    }

    /// <summary>删除一个压缩备份（2026-10-19 需求 9）。</summary>
    /// <param name="slot">备份槽位。</param>
    /// <returns>成功且确实删了 ⇒ 空串；否则为原因。</returns>
    public static string DeleteBackup(SaveSlot slot)
    {
        try
        {
            var saves = new SaveService();
            return saves.DeleteBackup(slot) ? string.Empty : $"备份不存在：{slot.Name}";
        }
        catch (SaveFileException ex)
        {
            return $"删除备份失败：{ex.Message}";
        }
    }

    /// <summary>列出 <c>saves/backups</c> 下的全部压缩备份（2026-10-19 需求 15）。</summary>
    /// <returns>备份槽位（按档名升序）。</returns>
    public static IReadOnlyList<SaveSlot> ListBackups()
    {
        var saves = new SaveService();

        try
        {
            return saves.ListBackups();
        }
        catch (SaveFileException)
        {
            return [];
        }
    }

    /// <summary>从备份读档（2026-10-19 需求 15）：读回后即以此为当前档继续。</summary>
    /// <param name="slot">备份槽位。</param>
    /// <param name="seed">随机种子。</param>
    /// <returns>会话。</returns>
    public static GameSession LoadBackup(SaveSlot slot, int seed)
    {
        var random = new SeededRandomService(seed);
        var names = new SongStyleNameGenerator(random);
        var saves = new SaveService();
        var achievements = new AchievementStore(saves);
        var state = saves.LoadBackup(slot);
        var session = new GameSession(state, slot, saves, random, names, achievements);
        session.Note($"☆ 从备份读档 {slot.Name}：{state.Family.Name}家，{state.CurrentDate}。");
        return session;
    }

    public string Backup()
    {
        try
        {
            _saves.Backup(Slot);
            Note("◆ 已备份。");
            return string.Empty;
        }
        catch (SaveFileException ex)
        {
            return $"备份失败：{ex.Message}";
        }
    }

    public void SaveOnExit()
    {
        if (State.Ending.IsEnded)
        {
            return;
        }

        try
        {
            _runner.SaveOnExit(State);
        }
        catch (SaveFileException)
        {
            // 退出时静默失败：不阻断关闭流程。
        }
    }

    public AchievementArchive ReadArchive() => _achievements.Read();

    /// <summary>导出族流水账（规格书 §12.3「可导出 CSV」）。</summary>
    public string ExportLedgerCsv()
    {
        try
        {
            Directory.CreateDirectory(_saves.RootDirectory);
            var path = Path.Combine(_saves.RootDirectory, $"{Slot.Name}-流水账.csv");
            var lines = new List<string> { "年,月,类别,成员编号,金额（文）" };
            foreach (var entry in State.Economy.Ledger.Entries)
            {
                lines.Add(
                    $"{entry.Date.Year},{entry.Date.Month},{entry.Category},{entry.PersonId?.Value.ToString("D", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty},{entry.Amount.Wen.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }

            File.WriteAllLines(path, lines, System.Text.Encoding.UTF8);
            Note($"◆ 流水账已导出：{path}");
            return string.Empty;
        }
        catch (IOException ex)
        {
            return $"导出失败：{ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            return $"导出失败：{ex.Message}";
        }
    }
}
