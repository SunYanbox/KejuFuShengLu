using KFL.Core.Enums;
using KFL.Infrastructure.Abstractions;

namespace KFL.Rules.Config;

/// <summary>
/// 四出身开局表（规格书 §10.1、§5.3；FR-002~FR-004、FR-008；data-model §3.1）——§10.1 的**唯一**声明处。
/// </summary>
/// <remarks>
/// <para>
/// 本类是本特性全部**开局数值**的唯一出处（SC-009）：初始现金 / 田 / 宅 / 商本、成员构成、
/// 年龄区间、学业与体质口径、士出身的带入功名。调用方（<c>Start/NewGameSetup</c>）MUST NOT
/// 另抄一份初值。
/// </para>
/// <para>
/// **所有区间一律「整数均匀、含两端点」**：实现统一走
/// <c>Next(min, max + 1)</c>（<see cref="IRandomService.Next"/> 的上界是开区间）。
/// </para>
/// <para>
/// **明确不含**：任何界面文案、官名、资产**单价**（单价在 <c>AssetPriceTable</c>，002 已交付）、
/// 孩子的天赋遗传与新生儿口径（逻辑轨 ⑦）。
/// </para>
/// </remarks>
public static class OriginStartTable
{
    /// <summary>配偶人数（§10.1「夫妇」，四个出身一致）。</summary>
    public const int SpouseCount = 1;

    /// <summary>年龄区间的半宽（<c>±5</c>）。</summary>
    public const int AgeSpread = 5;

    /// <summary>家主年龄均值：农 / 工 / 商（§10.1）。</summary>
    public const int CommonerHeadAgeMean = 28;

    /// <summary>家主年龄均值：士（§10.1）。</summary>
    public const int ScholarHeadAgeMean = 30;

    /// <summary>配偶年龄均值（§10.1）。</summary>
    public const int SpouseAgeMean = 25;

    /// <summary>孩子年龄下界（含）。</summary>
    public const int ChildAgeMin = 0;

    /// <summary>孩子年龄上界（含）。</summary>
    public const int ChildAgeMax = 8;

    /// <summary>士出身家主的学业（**常量，不掷骰**；§10.1）。</summary>
    public const int ScholarHeadStudy = 60;

    /// <summary>其余出身家主学业的下界（含）。</summary>
    public const int CommonerHeadStudyMin = 10;

    /// <summary>其余出身家主学业的上界（含）。</summary>
    public const int CommonerHeadStudyMax = 30;

    /// <summary>家主体质下界（含）。</summary>
    public const int HeadHealthMin = 80;

    /// <summary>家主体质上界（含）。</summary>
    public const int HeadHealthMax = 100;

    /// <summary>孩子学业（**常量 0，不掷骰**；§10.1）。</summary>
    public const int ChildStudyValue = 0;

    /// <summary>孩子体质下界（含）。</summary>
    public const int ChildHealthMin = 90;

    /// <summary>孩子体质上界（含）。</summary>
    public const int ChildHealthMax = 100;

    /// <summary>初始现金（贯，§10.1）。</summary>
    /// <param name="origin">出身。</param>
    /// <returns>初始现金（贯）。</returns>
    public static decimal InitialCashGuan(Origin origin) => origin switch
    {
        Origin.Farmer => 80m,
        Origin.Artisan => 80m,
        Origin.Merchant => 500m,
        Origin.Scholar => 200m,
        _ => throw Unknown(origin),
    };

    /// <summary>初始田（亩）：仅农出身（§10.1）。</summary>
    /// <param name="origin">出身。</param>
    /// <returns>初始田亩数。</returns>
    public static int InitialFarmlandMu(Origin origin) => origin == Origin.Farmer ? 40 : 0;

    /// <summary>初始农村宅（座）：农 / 工 / 士各 1（「农舍」与「农村宅」同价，§5.3、§10.1）。</summary>
    /// <param name="origin">出身。</param>
    /// <returns>初始农村宅座数。</returns>
    public static int InitialRuralHouses(Origin origin) =>
        origin is Origin.Farmer or Origin.Artisan or Origin.Scholar ? 1 : 0;

    /// <summary>初始城市宅（座）：仅商出身 1 座（§10.1）。</summary>
    /// <param name="origin">出身。</param>
    /// <returns>初始城市宅座数。</returns>
    public static int InitialUrbanHouses(Origin origin) => origin == Origin.Merchant ? 1 : 0;

    /// <summary>初始商本（贯，**入商本池、不入现金**）：仅商出身 300 贯（§10.1、§5.2）。</summary>
    /// <param name="origin">出身。</param>
    /// <returns>初始商本（贯）。</returns>
    public static decimal InitialMerchantCapitalGuan(Origin origin) => origin == Origin.Merchant ? 300m : 0m;

    /// <summary>孩子人数（§10.1；FR-003）。</summary>
    /// <param name="origin">出身。</param>
    /// <returns>孩子人数。</returns>
    public static int ChildCount(Origin origin) => origin switch
    {
        Origin.Farmer => 2,
        Origin.Artisan => 1,
        Origin.Merchant => 2,
        Origin.Scholar => 1,
        _ => throw Unknown(origin),
    };

    /// <summary>家主年龄下界（含）。</summary>
    /// <param name="origin">出身。</param>
    /// <returns>下界。</returns>
    public static int HeadAgeMin(Origin origin) => HeadAgeMean(origin) - AgeSpread;

    /// <summary>家主年龄上界（含）。</summary>
    /// <param name="origin">出身。</param>
    /// <returns>上界。</returns>
    public static int HeadAgeMax(Origin origin) => HeadAgeMean(origin) + AgeSpread;

    /// <summary>取家主年龄（整数均匀、含两端点；§10.1）。</summary>
    /// <param name="origin">出身。</param>
    /// <param name="random">随机来源。</param>
    /// <returns>区间内的年龄。</returns>
    public static int NextHeadAge(Origin origin, IRandomService random) =>
        NextInclusive(HeadAgeMin(origin), HeadAgeMax(origin), random);

    /// <summary>取配偶年龄（<c>25±5</c>，四个出身一致）。</summary>
    /// <param name="random">随机来源。</param>
    /// <returns>区间内的年龄。</returns>
    public static int NextSpouseAge(IRandomService random) =>
        NextInclusive(SpouseAgeMean - AgeSpread, SpouseAgeMean + AgeSpread, random);

    /// <summary>取孩子年龄（<c>0~8</c>，四个出身一致）。</summary>
    /// <param name="random">随机来源。</param>
    /// <returns>区间内的年龄。</returns>
    public static int NextChildAge(IRandomService random) =>
        NextInclusive(ChildAgeMin, ChildAgeMax, random);

    /// <summary>取家主学业：士为常量 <see cref="ScholarHeadStudy"/>（**不掷骰**），其余 10~30 均匀。</summary>
    /// <param name="origin">出身。</param>
    /// <param name="random">随机来源（士出身不会被消费）。</param>
    /// <returns>家主学业。</returns>
    public static int NextHeadStudy(Origin origin, IRandomService random) =>
        origin == Origin.Scholar
            ? ScholarHeadStudy
            : NextInclusive(CommonerHeadStudyMin, CommonerHeadStudyMax, random);

    /// <summary>取家主体质（<c>80~100</c> 整数均匀、含端点）。</summary>
    /// <param name="random">随机来源。</param>
    /// <returns>家主体质。</returns>
    public static int NextHeadHealth(IRandomService random) =>
        NextInclusive(HeadHealthMin, HeadHealthMax, random);

    /// <summary>取孩子体质（<c>90~100</c> 整数均匀、含端点）。</summary>
    /// <param name="random">随机来源。</param>
    /// <returns>孩子体质。</returns>
    public static int NextChildHealth(IRandomService random) =>
        NextInclusive(ChildHealthMin, ChildHealthMax, random);

    /// <summary>该出身是否带入一条「举人 / Initial」功名记录（§10.1、FR-008）。</summary>
    /// <param name="origin">出身。</param>
    /// <returns>士出身为 <c>true</c>。</returns>
    public static bool ScholarOriginHasJuRenRecord(Origin origin) => origin == Origin.Scholar;

    private static int HeadAgeMean(Origin origin) => origin switch
    {
        Origin.Farmer or Origin.Artisan or Origin.Merchant => CommonerHeadAgeMean,
        Origin.Scholar => ScholarHeadAgeMean,
        _ => throw Unknown(origin),
    };

    /// <summary>闭区间整数均匀取样：<c>Next(min, max + 1)</c>（上界是开区间）。</summary>
    private static int NextInclusive(int min, int max, IRandomService random) => random.Next(min, max + 1);

    private static ArgumentOutOfRangeException Unknown(Origin origin) =>
        new(nameof(origin), origin, "未登记的出身（规格书 §10.1 共四种）。");
}
