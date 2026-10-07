namespace KFL.Rules.Config;

/// <summary>
/// 十八级年俸表（规格书 §8.1；data-model §4.1）。
/// </summary>
/// <remarks>
/// <para>
/// 逐级年俸（贯/年）L1 → L18 =
/// <c>5100 / 4250 / 3560 / 2980 / 2490 / 2090 / 1750 / 1460 / 1220 / 1020 / 860 / 720 / 600 / 500 / 420 / 235 / 130 / 72</c>。
/// 月摊 = ÷ <see cref="IncomeRateTable.MonthsPerYear"/>。
/// </para>
/// <para>
/// 本类是这 18 个数值与士出身加成的唯一出处（SC-008）；锚点 L1 / L15 / L18 = 5100 / 420 / 72
/// 是 SC-004 的被验证对象，允许在测试中以字面量断言。
/// </para>
/// </remarks>
public static class SalaryTable
{
    /// <summary>最高品级（宰相，L1）。</summary>
    public const int HighestLevel = 1;

    /// <summary>最低品级（选人，L18）。</summary>
    public const int LowestLevel = 18;

    /// <summary>士出身成员当官后的俸禄加成（规格书 §8.1 括注、§10.1）。</summary>
    public const decimal ScholarOriginMultiplier = 1.05m;

    /// <summary>取某品的年俸（贯/年）。</summary>
    /// <param name="level">品级，1~18。</param>
    /// <returns>年俸（贯/年）。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> 不在 1~18。</exception>
    public static decimal AnnualSalaryGuan(int level) => level switch
    {
        1 => 5100m,
        2 => 4250m,
        3 => 3560m,
        4 => 2980m,
        5 => 2490m,
        6 => 2090m,
        7 => 1750m,
        8 => 1460m,
        9 => 1220m,
        10 => 1020m,
        11 => 860m,
        12 => 720m,
        13 => 600m,
        14 => 500m,
        15 => 420m,
        16 => 235m,
        17 => 130m,
        18 => 72m,
        _ => throw new ArgumentOutOfRangeException(
            nameof(level), level, "官阶为 L1~L18（规格书 §8.1 十八级），无 L0 与 L19。"),
    };

    /// <summary>取某品的年俸月摊（贯/月，= 年俸 ÷ 12）。</summary>
    /// <param name="level">品级，1~18。</param>
    /// <returns>月摊（贯/月）。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> 不在 1~18。</exception>
    public static decimal MonthlySalaryGuan(int level) =>
        AnnualSalaryGuan(level) / IncomeRateTable.MonthsPerYear;

    /// <summary>取某品的年俸（以「文」为单位的金额）。</summary>
    /// <param name="level">品级，1~18。</param>
    /// <returns>年俸金额。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> 不在 1~18。</exception>
    public static Core.ValueObjects.Money AnnualSalary(int level) =>
        Core.ValueObjects.Money.FromGuan(AnnualSalaryGuan(level));
}
