using KFL.Core.Enums;

namespace KFL.Presentation.ViewModels;

/// <summary>成就页的一行。</summary>
public sealed class AchievementRow(
    string code,
    string name,
    string series,
    bool completed,
    string note,
    string description,
    string localTime)
{
    public string Code { get; } = code;

    public string Name { get; } = name;

    public string Series { get; } = series;

    public bool Completed { get; } = completed;

    public string StateText { get; } = completed ? "已达成" : "未达成";

    /// <summary>游戏内达成年月（含难度）。</summary>
    public string Note { get; } = note;

    /// <summary>达成条件说明（唯一来源：规格书 §14.1）。</summary>
    public string Description { get; } = description;

    /// <summary>本机观测到解锁的本地时间（内部按 UTC 存，展示转本地时区）。</summary>
    public string LocalTime { get; } = localTime;

    /// <summary>详情列：达成时同时给出游戏内年月与本机本地时间（单行紧凑显示）。</summary>
    public string Detail { get; } = completed && localTime.Length > 0
        ? $"{note} · {localTime}"
        : note;
}
