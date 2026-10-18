using KFL.Core.Entities;
using KFL.Core.ValueObjects;

namespace KFL.Presentation.ViewModels;

/// <summary>成员卡片行（一次刷新重建一次的只读快照视图）。</summary>
public sealed class MemberRow
{
    public MemberRow(Person person, Family family, GameDate date, bool archived = false)
    {
        Person = person;
        Name = person.Name;
        GenderText = Display.Gender(person.Gender);
        Age = person.AgeAt(date);
        Generation = person.Generation;
        DegreeText = Display.Degree(person.CurrentDegree);
        PlacementText = Display.Placement(person.CurrentPlacement);
        RankText = person.Rank is { } rank ? rank.ToString() : "—";
        Study = person.Study;
        Health = person.Health;
        Lifespan = person.Lifespan;
        Merit = person.Merit;
        StatusText = Display.StatusOf(person, date);
        SpouseText = person.SpouseId is { } spouseId ? family.TryGet(spouseId)?.Name ?? "—" : "—";
        IsArchived = archived;
        IsHead = family.HeadId == person.Id;
    }

    public Person Person { get; }

    public string Name { get; }

    public string GenderText { get; }

    public int Age { get; }

    public int Generation { get; }

    public string DegreeText { get; }

    public string PlacementText { get; }

    public string RankText { get; }

    public int Study { get; }

    public int Health { get; }

    public int Lifespan { get; }

    public int Merit { get; }

    public string StatusText { get; }

    public string SpouseText { get; }

    public bool IsArchived { get; }

    public bool IsHead { get; }

    public string HeadMark => IsHead ? "家主" : string.Empty;

    public string Caption => $"{Name}（{GenderText}{Age}）";

    public string PlacementBrushKey => Display.PlacementBrushKey(Person.CurrentPlacement);

    /// <summary>一甲名次配色（规格书 §12.2）。资源缺失时退回默认前景色。</summary>
    public System.Windows.Media.Brush PlacementBrush
        => System.Windows.Application.Current?.TryFindResource(PlacementBrushKey) as System.Windows.Media.Brush
            ?? System.Windows.Media.Brushes.Black;
}
