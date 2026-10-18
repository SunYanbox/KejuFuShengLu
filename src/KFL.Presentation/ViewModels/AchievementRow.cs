using KFL.Core.Enums;

namespace KFL.Presentation.ViewModels;

/// <summary>成就页的一行。</summary>
public sealed class AchievementRow(string code, string name, string series, bool completed, string note)
{
    public string Code { get; } = code;

    public string Name { get; } = name;

    public string Series { get; } = series;

    public bool Completed { get; } = completed;

    public string StateText { get; } = completed ? "已达成" : "未达成";

    public string Note { get; } = note;
}
