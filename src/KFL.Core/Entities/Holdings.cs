using KFL.Core.Enums;

namespace KFL.Core.Entities;

/// <summary>
/// 家族资产组合（规格书 §5.3；data-model §3.3）：田、农村宅、城市宅、铺面的**数量**。
/// </summary>
/// <remarks>
/// <para>
/// **不变量**：四个数量皆 <c>&gt;= 0</c>（属性 setter 与 <see cref="Remove"/> 双重守卫）。
/// </para>
/// <para>
/// **明确不含**：单价、市值、农田上限——单价与「每成人 20 亩」都在 <c>KFL.Rules/Config</c>
/// （research R-01；SC-008）。本类型只回答「持有多少」。
/// </para>
/// </remarks>
public sealed class Holdings
{
    private int _farmlandMu;
    private int _ruralHouses;
    private int _urbanHouses;
    private int _shops;

    /// <summary>田（亩），<c>&gt;= 0</c>。</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> 为负。</exception>
    public int FarmlandMu
    {
        get => _farmlandMu;
        set
        {
            EnsureNonNegative(value, nameof(value));
            _farmlandMu = value;
        }
    }

    /// <summary>农村宅（座），<c>&gt;= 0</c>。</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> 为负。</exception>
    public int RuralHouses
    {
        get => _ruralHouses;
        set
        {
            EnsureNonNegative(value, nameof(value));
            _ruralHouses = value;
        }
    }

    /// <summary>城市宅（座），<c>&gt;= 0</c>。做工收入的加成基数（§5.2）。</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> 为负。</exception>
    public int UrbanHouses
    {
        get => _urbanHouses;
        set
        {
            EnsureNonNegative(value, nameof(value));
            _urbanHouses = value;
        }
    }

    /// <summary>铺面（间），<c>&gt;= 0</c>。铺面租收入的基数（§5.3）。</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> 为负。</exception>
    public int Shops
    {
        get => _shops;
        set
        {
            EnsureNonNegative(value, nameof(value));
            _shops = value;
        }
    }

    /// <summary>取指定资产种类的持有量。</summary>
    /// <param name="kind">资产种类。</param>
    /// <returns>持有量（田为亩、宅为座、铺面为间）。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> 不在四种资产之内。</exception>
    public int CountOf(AssetKind kind) => kind switch
    {
        AssetKind.Farmland => _farmlandMu,
        AssetKind.RuralHouse => _ruralHouses,
        AssetKind.UrbanHouse => _urbanHouses,
        AssetKind.Shop => _shops,
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind), kind, "未登记的资产种类（规格书 §5.3 共四种）。"),
    };

    /// <summary>增持指定资产。</summary>
    /// <param name="kind">资产种类。</param>
    /// <param name="count">增持数量，MUST <c>&gt;= 0</c>（减持请用 <see cref="Remove"/>）。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> 为负，或 <paramref name="kind"/> 未登记。</exception>
    public void Add(AssetKind kind, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count), count, "增持数量 MUST >= 0（减持请用 Remove，data-model §3.3）。");
        }

        Set(kind, CountOf(kind) + count);
    }

    /// <summary>减持指定资产。</summary>
    /// <param name="kind">资产种类。</param>
    /// <param name="count">减持数量，MUST <c>&gt;= 0</c> 且不超过当前持有量。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> 为负，或 <paramref name="kind"/> 未登记。</exception>
    /// <exception cref="InvalidOperationException">减持数量大于当前持有量（会减至负数）。</exception>
    public void Remove(AssetKind kind, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count), count, "减持数量 MUST >= 0（data-model §3.3）。");
        }

        var current = CountOf(kind);
        if (count > current)
        {
            throw new InvalidOperationException(
                $"减持 {count} 超过当前的 {current}（data-model §3.3：数量 MUST >= 0）。");
        }

        Set(kind, current - count);
    }

    private void Set(AssetKind kind, int count)
    {
        switch (kind)
        {
            case AssetKind.Farmland:
                FarmlandMu = count;
                break;
            case AssetKind.RuralHouse:
                RuralHouses = count;
                break;
            case AssetKind.UrbanHouse:
                UrbanHouses = count;
                break;
            case AssetKind.Shop:
                Shops = count;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(kind), kind, "未登记的资产种类（规格书 §5.3 共四种）。");
        }
    }

    private static void EnsureNonNegative(int value, string paramName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                paramName, value, "资产数量 MUST >= 0（data-model §3.3）。");
        }
    }
}
