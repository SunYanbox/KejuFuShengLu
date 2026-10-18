using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace KFL.Presentation;

/// <summary>
/// 成就「解锁时刻」的墙钟记录。
/// </summary>
/// <remarks>
/// 规格书 §14.1 只要求展示「游戏内达成年月」，而本文件额外记录**本机解锁时刻**，用于成就页
/// 「解锁时的具体日期时间」。
/// <para>
/// 关键设计约束：存档（MessagePack 格式 5）里没有任何墙钟时间戳，无法从存档回填历史时刻；
/// 因此这里采用「首次观测即记下现在」的近似——凡是首次见到某成就已完成，就把当时的本机时间
/// （UTC）写入 <c>achievement-times.json</c>，之后长期保留。若成就是在本次追加本文件之前就已
/// 达成，则回退为「本次进入游戏的时间」，并在 UI 上标注「（观测时间）」。
/// </para>
/// <para>
/// 存储用 UTC（零点区），展示时转本机时区，与玩家「内部存储 0 时区」的要求一致。
/// </para>
/// </remarks>
public sealed class AchievementTimeLog
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private readonly string _path;
    private readonly Dictionary<int, DateTimeOffset> _observed = [];
    private readonly Dictionary<int, bool> _synthesized = [];

    private AchievementTimeLog(string path)
    {
        _path = path;
        Load();
    }

    /// <summary>按存档目录建立（与该存档同级，便于随存档一起搬走）。</summary>
    public static AchievementTimeLog For(SaveSlot slot, string saveDirectory)
        => new(Path.Combine(saveDirectory, "achievement-times." + Sanitize(slot.Name) + ".json"));

    /// <summary>扫描已达成成就，补齐缺失的首次观测时刻并落盘。</summary>
    public void Observe(AchievementState achievements)
    {
        var changed = false;
        foreach (var mark in achievements.Completed)
        {
            var id = (int)mark.Achievement;
            if (_observed.ContainsKey(id))
            {
                continue;
            }

            _observed[id] = DateTimeOffset.UtcNow;
            _synthesized[id] = true;
            changed = true;
        }

        if (changed)
        {
            Save();
        }
    }

    /// <summary>展示文本：本机时区「yyyy-MM-dd HH:mm」；合成时刻附「观测」标记。</summary>
    public string LocalText(AchievementMark mark)
    {
        var id = (int)mark.Achievement;
        if (!_observed.TryGetValue(id, out var utc))
        {
            return string.Empty;
        }

        var local = utc.ToLocalTime();
        var text = local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        return _synthesized.TryGetValue(id, out var synthesized) && synthesized ? text + " 观测" : text;
    }

    private static string Sanitize(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            builder.Append(Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch);
        }

        return builder.ToString();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return;
            }

            using var stream = File.OpenRead(_path);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
            if (data is null)
            {
                return;
            }

            foreach (var (key, value) in data)
            {
                if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                    && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when))
                {
                    _observed[id] = when;
                }
            }
        }
        catch (IOException)
        {
            // 记录文件不可读时退化为「无本机时刻」，不影响游戏。
        }
        catch (JsonException)
        {
        }
    }

    private void Save()
    {
        try
        {
            var data = _observed.ToDictionary(
                pair => pair.Key.ToString(CultureInfo.InvariantCulture),
                pair => pair.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            using var stream = File.Create(_path);
            JsonSerializer.Serialize(stream, data, Indented);
        }
        catch (IOException)
        {
        }
    }
}
