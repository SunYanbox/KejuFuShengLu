using System.Collections.ObjectModel;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Presentation.Mvvm;
using KFL.Rules.FamilyLifecycle;

namespace KFL.Presentation.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    public MemberRow? Selected
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value))
            {
                RefreshDetail();
                RefreshTreeLines();
            }
        }
    }

    /// <summary>家谱视图：按代分行的成员布局（玩家反馈 8）。</summary>
    public ObservableCollection<GenerationBand> Generations { get; } = [];

    /// <summary>家谱视图：扁平化的成员方块（画布绝对坐标，便于与行标题共用一个 Canvas）。</summary>
    public ObservableCollection<TreeNode> TreeNodes { get; } = [];

    /// <summary>家谱视图：选中成员与亲属之间的彩色连线（玩家反馈 8）。</summary>
    public ObservableCollection<TreeLine> TreeLines { get; } = [];

    /// <summary>家谱显示「父母」连线（玩家反馈 8 的三个可选项之一）。</summary>
    public bool ShowParentLinks
    {
        get => _showParentLinks;
        set
        {
            if (Set(ref _showParentLinks, value))
            {
                RefreshTreeLines();
            }
        }
    }

    /// <summary>家谱显示「子女」连线。</summary>
    public bool ShowChildLinks
    {
        get => _showChildLinks;
        set
        {
            if (Set(ref _showChildLinks, value))
            {
                RefreshTreeLines();
            }
        }
    }

    /// <summary>家谱显示「兄弟姐妹」连线。</summary>
    public bool ShowSiblingLinks
    {
        get => _showSiblingLinks;
        set
        {
            if (Set(ref _showSiblingLinks, value))
            {
                RefreshTreeLines();
            }
        }
    }

    /// <summary>家谱显示「买入」标记（第五轮玩家反馈：买入成员须一眼可辨）。</summary>
    public bool ShowPurchaseMarks
    {
        get => _showPurchaseMarks;
        set
        {
            if (Set(ref _showPurchaseMarks, value))
            {
                RefreshMembers();
            }
        }
    }

    /// <summary>家谱只显示家主支脉（第五轮玩家反馈：旁系可折叠）。</summary>
    public bool ShowHeadBranchOnly
    {
        get => _showHeadBranchOnly;
        set
        {
            if (Set(ref _showHeadBranchOnly, value))
            {
                RefreshMembers();
            }
        }
    }

    /// <summary>家谱画布宽高（绑定到 Canvas.Width/Height）。</summary>
    public double TreeWidth => _treeWidth;
    /// <summary>家谱画布高度。</summary>
    public double TreeHeight => _treeHeight;

    public string DetailText
    {
        get => _detailText;
        private set => Set(ref _detailText, value);
    }

    /// <summary>按「筛选器」重建成员卡片列表（玩家反馈：需要按在册 / 归档 / 亡故 / 外嫁 / 服刑筛选）。</summary>
    private void RefreshMembers()
    {
        if (_session is null)
        {
            return;
        }

        var state = _session.State;
        var family = state.Family;
        var date = state.CurrentDate;

        // 「买入」标记由账本派生（第五轮玩家反馈）；关掉开关即不写徽章。
        var purchasedIds = _showPurchaseMarks ? GenealogyQueries.PurchasedPersonIds(state) : null;

        Members.Clear();
        foreach (var person in family.RegisteredMembers.OrderByDescending(p => family.HeadId == p.Id).ThenBy(p => p.Id.Value))
        {
            Members.Add(new MemberRow(person, family, date, childbirthPending: _childbirthPending.Contains(person.Id.Value), purchased: purchasedIds?.Contains(person.Id) == true));
        }

        Archived.Clear();
        foreach (var person in family.ArchivedMembers.OrderByDescending(p => p.Id.Value))
        {
            Archived.Add(new MemberRow(person, family, date, archived: true, childbirthPending: _childbirthPending.Contains(person.Id.Value), purchased: purchasedIds?.Contains(person.Id) == true));
        }

        var all = Members.Concat(Archived).ToList();

        // 「仅显示家主支脉」（第五轮玩家反馈）：联络外的旁系与旁系的配偶一并隐去。
        // 家主空缺时查询返回空集，此时视为不过滤（否则家谱会整片空白）。
        if (_showHeadBranchOnly)
        {
            var branch = GenealogyQueries.HeadBranchPersonIds(family);
            if (branch.Count > 0)
            {
                all = all.Where(row => branch.Contains(row.Person.Id)).ToList();
            }
        }

        var filter = _selectedMemberFilter?.Value ?? "registered";
        var shown = filter switch
        {
            "all" => all,
            "archived" => all.Where(row => row.IsArchived).ToList(),
            "deceased" => all.Where(row => row.Person.Status.HasFlag(StatusFlag.Deceased)).ToList(),
            "marriedout" => all.Where(row => row.Person.Status.HasFlag(StatusFlag.MarriedOut)).ToList(),
            "serving" => all.Where(row => row.Person.Status.HasFlag(StatusFlag.ServingSentence)).ToList(),
            "awaiting" => all.Where(row => row.Person.Status.HasFlag(StatusFlag.AwaitingPost)).ToList(),
            "banned" => all.Where(row => row.Person.Status.HasFlag(StatusFlag.ExamBanned) || row.Person.Status.HasFlag(StatusFlag.PromotionBanned)).ToList(),
            _ => all.Where(row => !row.IsArchived).ToList(),
        };

        Members.Clear();
        foreach (var row in shown)
        {
            Members.Add(row);
        }

        MemberCountText = $"筛选「{_selectedMemberFilter?.Text ?? "在册成员"}」：{shown.Count} 人 ｜ 在册 {family.RegisteredMembers.Count} 人 ｜ 归档 {family.ArchivedMembers.Count} 人";

        RefreshTree(shown);
    }

    /// <summary>
    /// 重建家谱视图（玩家反馈 8）：一行一代人，节点按出生先后排列；画布尺寸随人数增长。
    /// </summary>
    /// <param name="rows">当前筛选后的成员卡片（与左侧列表同一批）。</param>
    private void RefreshTree(IReadOnlyList<MemberRow> rows)
    {
        Generations.Clear();
        TreeNodes.Clear();
        foreach (var band in FamilyTree.Layout(rows))
        {
            Generations.Add(band);
            foreach (var node in band.Nodes)
            {
                TreeNodes.Add(node);
            }
        }

        var width = FamilyTree.CanvasWidth(Generations);
        var height = FamilyTree.CanvasHeight(Generations);
        if (Math.Abs(width - _treeWidth) > 0.01d)
        {
            _treeWidth = width;
            Raise(nameof(TreeWidth));
        }

        if (Math.Abs(height - _treeHeight) > 0.01d)
        {
            _treeHeight = height;
            Raise(nameof(TreeHeight));
        }

        RefreshTreeLines();
    }

    /// <summary>重算家谱里当前选中成员的亲属连线（玩家反馈 8 的三个可选项）。</summary>
    private void RefreshTreeLines()
    {
        TreeLines.Clear();

        if (_session is null || Selected is null)
        {
            return;
        }

        foreach (var line in FamilyTree.Lines(
            _session.State.Family,
            Generations,
            Selected.Person,
            _showParentLinks,
            _showChildLinks,
            _showSiblingLinks))
        {
            TreeLines.Add(line);
        }
    }

    /// <summary>
    /// 按 Id 选中成员（需求 7 / 8：详情里的关系超链接与家谱节点跳转都走这里）。
    /// 已归档的成员也能选中——否则点父母的超链接会「没反应」。
    /// </summary>
    /// <param name="text">成员 Id 的字符串形式。</param>
    public void SelectMember(string text)
    {
        if (_session is null || !Guid.TryParse(text, out var id))
        {
            return;
        }

        var target = Members.FirstOrDefault(row => row.Person.Id.Value == id)
            ?? Archived.FirstOrDefault(row => row.Person.Id.Value == id);

        if (target is null)
        {
            // 目标不在当前筛选结果里：放宽到「全部」，再找一次。
            SelectedMemberFilter = MemberFilters.First(option => option.Value == "all");
            target = Members.FirstOrDefault(row => row.Person.Id.Value == id)
                ?? Archived.FirstOrDefault(row => row.Person.Id.Value == id);
        }

        if (target is not null)
        {
            Selected = target;
        }
    }
}
