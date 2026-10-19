using KFL.Core.Entities;
using KFL.Core.ValueObjects;
using KFL.Presentation.ViewModels;

namespace KFL.Presentation;

/// <summary>家谱里的一个成员方块（玩家反馈 8）。</summary>
/// <param name="row">成员卡片数据。</param>
/// <param name="left">画布横坐标。</param>
/// <param name="top">画布纵坐标。</param>
public sealed class TreeNode(MemberRow row, double left, double top)
{
    /// <summary>方块宽度（与 XAML 里的一致，连线端点靠它算）。</summary>
    public const double NodeWidth = 168d;

    /// <summary>方块高度（与 XAML 里的一致）。</summary>
    public const double NodeHeight = 116d;

    /// <summary>成员卡片。</summary>
    public MemberRow Row { get; } = row;

    /// <summary>画布横坐标（Canvas.Left）。</summary>
    public double Left { get; } = left;

    /// <summary>画布纵坐标（Canvas.Top）。</summary>
    public double Top { get; } = top;

    /// <summary>点击令牌（选中该成员）。</summary>
    public string Token => $"select:{Row.Person.Id.Value}";

    /// <summary>方块中心横坐标。</summary>
    public double CenterX => Left + (NodeWidth / 2);

    /// <summary>方块下沿（向下连线的起点）。</summary>
    public double Bottom => Top + NodeHeight;

    /// <summary>方块上沿（向上连线的终点）。</summary>
    public double TopEdge => Top;
}

/// <summary>家谱里的一代（一行一代人）。</summary>
/// <param name="generation">代际序号。</param>
/// <param name="caption">行标题。</param>
/// <param name="top">该行的纵坐标。</param>
/// <param name="nodes">该行的成员方块（按出生先后排列）。</param>
public sealed class GenerationBand(
    int generation,
    string caption,
    double top,
    IReadOnlyList<TreeNode> nodes)
{
    /// <summary>代际序号（第 0 代是始祖）。</summary>
    public int Generation { get; } = generation;

    /// <summary>行标题（形如「第 2 代（6 人）」）。</summary>
    public string Caption { get; } = caption;

    /// <summary>该行纵坐标。</summary>
    public double Top { get; } = top;

    /// <summary>该行的成员方块。</summary>
    public IReadOnlyList<TreeNode> Nodes { get; } = nodes;
}

/// <summary>家谱里的一条关系连线。</summary>
/// <param name="x1">起点横坐标。</param>
/// <param name="y1">起点纵坐标。</param>
/// <param name="x2">终点横坐标。</param>
/// <param name="y2">终点纵坐标。</param>
/// <param name="brushKey">画刷资源名（ChartBrush2/3/4）。</param>
/// <param name="label">关系名（父母 / 子女 / 兄弟姐妹）。</param>
public sealed record TreeLine(double X1, double Y1, double X2, double Y2, string BrushKey, string Label);

/// <summary>
/// 家谱布局（玩家反馈 8）：把在册 + 归档成员按<b>代</b>分行摆放，并给出当前选中成员与
/// 父母 / 子女 / 兄弟姐妹之间的彩色连线。
/// </summary>
/// <remarks>
/// 纯布局计算，不碰 WPF 类型（<see cref="TreeNode.NodeWidth"/> 等常量与 XAML 手工对齐）。
/// 「一行一代人」的 X 顺序按出生日期（再按 Id）稳定排序，保证同一个存档每次渲染都一致。
/// </remarks>
public static class FamilyTree
{
    /// <summary>行间距。</summary>
    public const double RowStride = 156d;

    /// <summary>行标题占用的高度（方块从行首下方这么多像素处开始）。</summary>
    public const double CaptionHeight = 22d;

    /// <summary>列间距。</summary>
    public const double ColumnStride = TreeNode.NodeWidth + 16d;

    /// <summary>父母连线配色（<c>App.xaml</c> 里的 ChartBrush2）。</summary>
    public const string ParentBrush = "ChartBrush2";

    /// <summary>子女连线配色（ChartBrush3）。</summary>
    public const string ChildBrush = "ChartBrush3";

    /// <summary>兄弟姐妹连线配色（ChartBrush4）。</summary>
    public const string SiblingBrush = "ChartBrush4";

    /// <summary>
    /// 生成代际布局。<paramref name="rows"/> 是当前筛选后的成员卡片；返回的行按代升序，
    /// 行内按出生日期升序。
    /// </summary>
    /// <param name="rows">要摆放的成员卡片（调用方的筛选结果）。</param>
    /// <returns>每代一行；没有成员时为空列表。</returns>
    public static IReadOnlyList<GenerationBand> Layout(IReadOnlyList<MemberRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var bands = new List<GenerationBand>();
        var index = 0;

        foreach (var group in rows
            .GroupBy(row => row.Person.Generation)
            .OrderBy(group => group.Key))
        {
            var ordered = group
                .OrderBy(row => row.Person.BirthDate.Year)
                .ThenBy(row => row.Person.BirthDate.Month)
                .ThenBy(row => row.Person.Id.Value)
                .ToList();

            var nodes = new List<TreeNode>(ordered.Count);
            for (var column = 0; column < ordered.Count; column++)
            {
                nodes.Add(new TreeNode(ordered[column], column * ColumnStride, 0d));
            }

            var top = index * RowStride;
            bands.Add(new GenerationBand(
                group.Key,
                $"第 {group.Key} 代（{ordered.Count} 人）",
                top,
                [.. nodes.Select(node => new TreeNode(node.Row, node.Left, top + CaptionHeight))]));

            index++;
        }

        return bands;
    }

    /// <summary>整个画布的高度（行数 × 行距，至少一行高）。</summary>
    /// <param name="bands">代际布局。</param>
    /// <returns>画布高度（像素）。</returns>
    public static double CanvasHeight(IReadOnlyList<GenerationBand> bands)
        => Math.Max(RowStride, bands.Count * RowStride);

    /// <summary>整个画布的宽度（最宽一行 × 列距）。</summary>
    /// <param name="bands">代际布局。</param>
    /// <returns>画布宽度（像素）。</returns>
    public static double CanvasWidth(IReadOnlyList<GenerationBand> bands)
    {
        ArgumentNullException.ThrowIfNull(bands);

        var widest = bands.Count == 0
            ? 1
            : bands.Max(band => band.Nodes.Count);

        return Math.Max(ColumnStride, widest * ColumnStride);
    }

    /// <summary>
    /// 选中成员的彩色连线（玩家反馈 8）：父母（蓝）/ 子女（绿）/ 兄弟姐妹（琥珀），
    /// 三组可由 <paramref name="parents"/> / <paramref name="children"/> / <paramref name="siblings"/> 分别开关。
    /// </summary>
    /// <param name="family">家族（只读）。</param>
    /// <param name="bands">代际布局。</param>
    /// <param name="selected">当前选中的成员（无选中时返回空）。</param>
    /// <param name="parents">是否显示父母连线。</param>
    /// <param name="children">是否显示子女连线。</param>
    /// <param name="siblings">是否显示兄弟姐妹连线。</param>
    /// <returns>连线列表。</returns>
    public static IReadOnlyList<TreeLine> Lines(
        Family family,
        IReadOnlyList<GenerationBand> bands,
        Person? selected,
        bool parents = true,
        bool children = true,
        bool siblings = true)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(bands);

        if (selected is null)
        {
            return [];
        }

        var nodes = bands
            .SelectMany(band => band.Nodes)
            .ToDictionary(node => node.Row.Person.Id.Value);

        if (!nodes.TryGetValue(selected.Id.Value, out var center))
        {
            return [];
        }

        var lines = new List<TreeLine>();

        if (parents)
        {
            AddUp(lines, nodes, center, selected.FatherId, ParentBrush, "父");
            AddUp(lines, nodes, center, selected.MotherId, ParentBrush, "母");
        }

        if (children)
        {
            foreach (var child in FamilyRelations.Children(family, selected))
            {
                AddDown(lines, nodes, center, child.Id, ChildBrush, "子女");
            }
        }

        if (siblings)
        {
            foreach (var sibling in FamilyRelations.Siblings(family, selected))
            {
                AddSide(lines, nodes, center, sibling.Id, SiblingBrush, "兄弟姐妹");
            }
        }

        return lines;
    }

    /// <summary>连到上方的亲属（父母）：从本方块上沿到对方块下沿。</summary>
    private static void AddUp(
        List<TreeLine> lines,
        Dictionary<Guid, TreeNode> nodes,
        TreeNode center,
        PersonId? id,
        string brushKey,
        string label)
    {
        if (id is not { } personId || !nodes.TryGetValue(personId.Value, out var other) || other.Top >= center.Top)
        {
            return;
        }

        lines.Add(new TreeLine(
            center.CenterX, center.TopEdge, other.CenterX, other.Bottom, brushKey, label));
    }

    /// <summary>连到下方的亲属（子女）：从本方块下沿到对方块上沿。</summary>
    private static void AddDown(
        List<TreeLine> lines,
        Dictionary<Guid, TreeNode> nodes,
        TreeNode center,
        PersonId id,
        string brushKey,
        string label)
    {
        if (!nodes.TryGetValue(id.Value, out var other) || other.Top <= center.Top)
        {
            return;
        }

        lines.Add(new TreeLine(
            center.CenterX, center.Bottom, other.CenterX, other.TopEdge, brushKey, label));
    }

    /// <summary>连到同代的亲属（兄弟姐妹）：从本方块左/右沿到对方块最近的边沿。</summary>
    private static void AddSide(
        List<TreeLine> lines,
        Dictionary<Guid, TreeNode> nodes,
        TreeNode center,
        PersonId id,
        string brushKey,
        string label)
    {
        if (!nodes.TryGetValue(id.Value, out var other) || Math.Abs(other.Top - center.Top) > 0.01d)
        {
            return;
        }

        var middle = center.Top + (TreeNode.NodeHeight / 2);
        var rightwards = other.CenterX >= center.CenterX;
        lines.Add(new TreeLine(
            rightwards ? center.Left + TreeNode.NodeWidth : center.Left,
            middle,
            rightwards ? other.Left : other.Left + TreeNode.NodeWidth,
            middle,
            brushKey,
            label));
    }
}
