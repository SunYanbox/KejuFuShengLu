using KFL.Core.Enums;

namespace KFL.Rules.Config;

/// <summary>
/// 生活费日耗表与乘区系数（规格书 §5.1、§5.4；data-model §4.1；research R-17）。
/// </summary>
/// <remarks>
/// <para>
/// 本类是「三档 × 四年龄档共 12 个日耗」「月天数」「新建存档的初始档位」「农出身独立乘区」
/// 「生活费一般乘区的修正项列表」的**唯一出处**（SC-008）。
/// <c>KFL.Core</c>、计算器与测试 MUST NOT 另存这些数值的第二份副本。
/// </para>
/// <para>
/// **乘区结构**（R-17）：
/// <c>月生活费 = 30 × 米价系数 × 难度支出系数 × Σ各计口成员日耗 × 农出身独立乘区 × 生活费一般乘区</c>。
/// 米价系数与难度支出系数各自独立相乘、**不进**一般乘区；农出身独立乘区是**乘算**、
/// 一般乘区是**加算**（`1 + Σ同区百分比修正`）。
/// </para>
/// <para>
/// **两条派生关系**（由断言守卫，本类只声明比值与定值，不把派生结果再次硬编码）：
/// 老人档 = 成人档 × <see cref="ElderRatioOfAdult"/>；儿童档固定
/// <see cref="ChildDailyCost"/>，**不随档位缩放**。
/// </para>
/// </remarks>
public static class LivingCostTable
{
    /// <summary>一月按 30 日折算（规格书 §5.1 的月公式首项）。</summary>
    public const int DaysPerMonth = 30;

    /// <summary>
    /// 新建存档的**初始**生活费档位（2026-10-06 裁决 E-13，规格书 §5.1 已回写）。
    /// 本类是它的唯一出处；<c>FamilyEconomy</c> 构造仍要求显式传入、不设默认值。
    /// </summary>
    public const LivingStandard InitialStandard = LivingStandard.Normal;

    /// <summary>儿童档日耗（文/日）：三档共用且**不随档位缩放**（E-10 的固定值）。</summary>
    public const decimal ChildDailyCost = 5m;

    /// <summary>老人档 = 成人档的比值（规格书 §5.1 的派生关系）。</summary>
    public const decimal ElderRatioOfAdult = 0.7m;

    /// <summary>农出身独立乘区：已成年（青年 / 成人 / 老人）。</summary>
    public const decimal FarmerMultiplierAdult = 0.90m;

    /// <summary>农出身独立乘区：未成年（儿童）。</summary>
    public const decimal FarmerMultiplierMinor = 0.80m;

    /// <summary>非农出身的独立乘区（恒等）。</summary>
    public const decimal NonFarmerMultiplier = 1.00m;

    /// <summary>生活费一般乘区的修正项：未成年（**所有出身共有**，加算）。</summary>
    public static readonly GeneralZoneModifier MinorModifier = new("未成年", -0.50m);

    /// <summary>
    /// 生活费一般乘区的修正项：救济期全体支出（§5.4，加算）。
    /// **取值单点在 <see cref="FamineTimeline.ReliefExpenseDiscount"/>**——同一个 20% 不出第二份副本（SC-008）。
    /// </summary>
    public static readonly GeneralZoneModifier ReliefModifier =
        new("救济期", -FamineTimeline.ReliefExpenseDiscount);

    /// <summary>
    /// 生活费一般乘区的**修正项列表**：乘区取值 = <c>1 + Σ同区百分比修正</c>（加算）。
    /// </summary>
    public static readonly IReadOnlyList<GeneralZoneModifier> GeneralZoneModifiers = [MinorModifier, ReliefModifier];

    /// <summary>取三档 × 四年龄档的日耗（文/日）。</summary>
    /// <param name="standard">生活费档位。</param>
    /// <param name="bracket">年龄档。</param>
    /// <returns>该格日耗（文/日）。</returns>
    /// <exception cref="ArgumentOutOfRangeException">档位或年龄档不在已登记取值内。</exception>
    public static decimal DailyCost(LivingStandard standard, AgeBracket bracket) => standard switch
    {
        LivingStandard.Frugal => bracket switch
        {
            AgeBracket.Adult => 20m,
            AgeBracket.Youth => 7m,
            AgeBracket.Elder => 14m,
            AgeBracket.Child => ChildDailyCost,
            _ => throw UnknownBracket(bracket),
        },
        LivingStandard.Normal => bracket switch
        {
            AgeBracket.Adult => 25m,
            AgeBracket.Youth => 8.5m,
            AgeBracket.Elder => 17.5m,
            AgeBracket.Child => ChildDailyCost,
            _ => throw UnknownBracket(bracket),
        },
        LivingStandard.Comfortable => bracket switch
        {
            AgeBracket.Adult => 30m,
            AgeBracket.Youth => 10m,
            AgeBracket.Elder => 21m,
            AgeBracket.Child => ChildDailyCost,
            _ => throw UnknownBracket(bracket),
        },
        _ => throw new ArgumentOutOfRangeException(
            nameof(standard), standard, "未登记的生活费档位（规格书 §5.1 共三档）。"),
    };

    /// <summary>取农出身独立乘区（仅 <see cref="Origin.Farmer"/> 非 1；乘算）。</summary>
    /// <param name="origin">出身。</param>
    /// <param name="bracket">年龄档（<see cref="AgeBracket.Child"/> 即未成年）。</param>
    /// <returns>该成员适用的独立乘区取值。</returns>
    public static decimal FarmerMultiplierOf(Origin origin, AgeBracket bracket) =>
        origin != Origin.Farmer
            ? NonFarmerMultiplier
            : bracket == AgeBracket.Child
                ? FarmerMultiplierMinor
                : FarmerMultiplierAdult;

    /// <summary>
    /// 取生活费一般乘区（<c>1 + Σ同区百分比修正</c>，**加算**）。
    /// </summary>
    /// <param name="isMinor">是否未成年（儿童档）。</param>
    /// <param name="inRelief">是否处于救济期（<see cref="FamineStage.Relief"/>）。</param>
    /// <returns>一般乘区取值：未成年 `0.5`、救济期再叠加 `-0.2`。</returns>
    public static decimal GeneralZoneFactor(bool isMinor, bool inRelief)
    {
        var sum = 0m;

        if (isMinor)
        {
            sum += MinorModifier.Percentage;
        }

        if (inRelief)
        {
            sum += ReliefModifier.Percentage;
        }

        return 1m + sum;
    }

    private static ArgumentOutOfRangeException UnknownBracket(AgeBracket bracket) =>
        new(nameof(bracket), bracket, "未登记的年龄档（规格书 §5.1 共四档）。");
}

/// <summary>
/// 生活费一般乘区的一个百分比修正项（规格书 §5.1 的 §17 裁决回写：同区修正**加算**）。
/// </summary>
/// <param name="Name">可读名称。</param>
/// <param name="Percentage">百分比修正（如 `-0.50m` = −50%）。</param>
public readonly record struct GeneralZoneModifier(string Name, decimal Percentage);
