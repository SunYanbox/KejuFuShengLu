using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Presentation.ViewModels;

/// <summary>成员卡片行（一次刷新重建一次的只读快照视图）。</summary>
public sealed class MemberRow
{
    public MemberRow(
        Person person,
        Family family,
        GameDate date,
        bool archived = false,
        bool childbirthPending = false,
        bool purchased = false)
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
        FatherText = person.FatherId is { } fatherId ? family.TryGet(fatherId)?.Name ?? "（不可考）" : "（不可考）";
        MotherText = person.MotherId is { } motherId ? family.TryGet(motherId)?.Name ?? "（不可考）" : "（不可考）";
        IsArchived = archived;
        IsHead = family.HeadId == person.Id;
        IsPurchased = purchased;
        ChildbirthPending = childbirthPending;

        // 状态标志（玩家反馈 3：卡片上直接表明「有没有病 / 有没有在治疗 / 已报名哪一级 / 名师 / 求子 / 入狱」）
        HasIllness = person.Status.HasFlag(StatusFlag.Ill);
        IsTreated = person.Timers.TreatmentRemainingMonths is not null;
        IsImprisoned = person.Status.HasFlag(StatusFlag.ServingSentence);
        IsTutored = person.IsTutoredByMaster;
        AppliedExamText = person.AppliedExam is { } level ? $"已报{Display.ExamLevel(level)}" : string.Empty;

        var badges = new List<string>();
        if (IsPurchased)
        {
            badges.Add("收养");
        }

        if (HasIllness)
        {
            badges.Add("患病");
        }

        if (IsTreated)
        {
            badges.Add("治疗中");
        }

        if (IsImprisoned)
        {
            badges.Add("入狱");
        }

        if (AppliedExamText.Length > 0)
        {
            badges.Add(AppliedExamText);
        }

        if (IsTutored)
        {
            badges.Add("名师");
        }

        if (ChildbirthPending)
        {
            badges.Add("求子");
        }

        if (person.Status.HasFlag(StatusFlag.AwaitingPost))
        {
            badges.Add("待阙");
        }

        if (person.Status.HasFlag(StatusFlag.MarriedOut))
        {
            // 方向取词（§12.2 / §17 第七轮 Q3）：本人出嫁 ⇒「外嫁」；外来夫婿 ⇒「女婿」。
            badges.Add(person.Gender == Gender.Male ? "女婿" : "外嫁");
        }

        BadgeText = badges.Count > 0 ? string.Join(" ｜ ", badges) : string.Empty;
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

    /// <summary>父亲姓名（无记录时为「（不可考）」）。</summary>
    public string FatherText { get; }

    /// <summary>母亲姓名（无记录时为「（不可考）」）。</summary>
    public string MotherText { get; }

    public bool IsArchived { get; }

    public bool IsHead { get; }

    /// <summary>是否由「收养」引入家族（或其是否显示收养标记，由视图模型开关决定）。</summary>
    public bool IsPurchased { get; }

    /// <summary>是否已「申请生育」（本会话内标记，见 MainViewModel）。</summary>
    public bool ChildbirthPending { get; }

    /// <summary>是否患病。</summary>
    public bool HasIllness { get; }

    /// <summary>是否正在治疗（医疗计时 > 0）。</summary>
    public bool IsTreated { get; }

    /// <summary>是否在服刑。</summary>
    public bool IsImprisoned { get; }

    /// <summary>是否受名师之教。</summary>
    public bool IsTutored { get; }

    /// <summary>「已报解试 / 已报省试」，未报名时为空串。</summary>
    public string AppliedExamText { get; }

    /// <summary>卡片状态徽记串（空串表示无状态标记）。</summary>
    public string BadgeText { get; }

    public string HeadMark => IsHead ? "家主" : string.Empty;

    public string Caption => $"{Name}（{GenderText}{Age}）";

    public string PlacementBrushKey => Display.PlacementBrushKey(Person.CurrentPlacement);

    /// <summary>一甲名次配色（规格书 §12.2）。资源缺失时退回默认前景色。</summary>
    public System.Windows.Media.Brush PlacementBrush
        => System.Windows.Application.Current?.TryFindResource(PlacementBrushKey) as System.Windows.Media.Brush
            ?? System.Windows.Media.Brushes.Black;
}
