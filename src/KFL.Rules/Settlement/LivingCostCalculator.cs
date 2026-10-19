using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;

namespace KFL.Rules.Settlement;

/// <summary>一次生活费计算的产物：逐年龄档明细 + 当月**应付**额（纯函数）。</summary>
/// <param name="Lines">逐年龄档明细，按「成人 / 青年 / 老人 / 儿童」固定顺序、四档恒在。</param>
/// <param name="Payable">当月应付生活费 = 各档小计之和。</param>
public sealed record LivingCostComputation(IReadOnlyList<LivingCostLine> Lines, Money Payable);

/// <summary>
/// 逐成员生活费明细（玩家反馈：收支悬浮窗要「按人罗列」，故需要一个成员维度的投影）。
/// </summary>
/// <param name="PersonId">成员标识。</param>
/// <param name="AgeBracket">年龄档（决定日耗与乘区）。</param>
/// <param name="Amount">该成员当月生活费。</param>
public sealed record LivingCostMemberLine(PersonId PersonId, AgeBracket AgeBracket, Money Amount);

/// <summary>
/// 生活费计算器（规格书 §5.1、§5.4；data-model §4.3；research R-17）。
/// </summary>
/// <remarks>
/// <para>
/// **纯函数，不碰资金池**：只按四个乘区算出「应付多少」，付得起与否由结算编排判定（E-06）。
/// </para>
/// <para>
/// 公式：`30 × 米价系数 × 难度支出系数 × Σ各计口成员日耗 × 农出身独立乘区 × 生活费一般乘区`。
/// 米价系数与难度支出系数各自独立相乘、**不进**一般乘区；农出身独立乘区是**乘算**、
/// 一般乘区是**加算**（`1 + Σ`）。按年龄档分组即可等价求和，因为农出身乘区与一般乘区
/// 都只由「是否儿童」决定，而四个年龄档与「是否儿童」一一对应。
/// </para>
/// <para>
/// 救济期的判定用**传入的阶段**（US4 传当前阶段，US1 传 <see cref="FamineStage.None"/>）；
/// <see cref="FamineStage.Severe"/> 不再减免，应付额 = 正常档。
/// </para>
/// </remarks>
public static class LivingCostCalculator
{
    /// <summary>明细的固定档位顺序（与规格书 §5.1 的表头一致）。</summary>
    public static readonly IReadOnlyList<AgeBracket> BracketOrder =
        [AgeBracket.Adult, AgeBracket.Youth, AgeBracket.Elder, AgeBracket.Child];

    /// <summary>计算当月应付生活费与逐年龄档明细。</summary>
    /// <param name="countedMembers">计口成员（在册且未服刑）。</param>
    /// <param name="date">当月年月（年龄档归属以它为准，生日当月即转档）。</param>
    /// <param name="livingStandard">当月**生效**的生活费档位。</param>
    /// <param name="grainPriceIndex">当月**生效**的米价系数（游走后）。</param>
    /// <param name="difficulty">当月生效的难度。</param>
    /// <param name="origin">存档出身（决定农出身独立乘区）。</param>
    /// <param name="famineStage">判定前的饥馑阶段（决定救济期的一般乘区）。</param>
    /// <returns>逐年龄档明细与应付额。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="countedMembers"/> 为 <c>null</c>。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="grainPriceIndex"/> 不大于 0。</exception>
    public static LivingCostComputation Compute(
        IReadOnlyList<Person> countedMembers,
        GameDate date,
        LivingStandard livingStandard,
        decimal grainPriceIndex,
        Difficulty difficulty,
        Origin origin,
        FamineStage famineStage)
    {
        ArgumentNullException.ThrowIfNull(countedMembers);

        if (grainPriceIndex <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(grainPriceIndex), grainPriceIndex, "米价系数 MUST > 0（data-model §1.2）。");
        }

        var inRelief = famineStage == FamineStage.Relief;
        var expenseFactor = DifficultyRates.ExpenseFactor(difficulty);

        var buckets = new Dictionary<AgeBracket, int>();
        foreach (var person in countedMembers)
        {
            var bracket = AgeBracketPolicy.Of(person, date);
            buckets[bracket] = buckets.TryGetValue(bracket, out var count) ? count + 1 : 1;
        }

        var lines = new List<LivingCostLine>(BracketOrder.Count);
        var payable = Money.Zero;

        foreach (var bracket in BracketOrder)
        {
            var dailyCost = LivingCostTable.DailyCost(livingStandard, bracket);
            var memberCount = buckets.TryGetValue(bracket, out var count) ? count : 0;

            // 量纲：月(30) × 无量纲系数们 × 日耗(文/日) = **文/人/月**，故变量名以 Wen 结尾
            // （曾误名 perMemberGuan——那是贯，与实参喂给 FromWen 的口径不符）。
            var perMemberWen = LivingCostTable.DaysPerMonth * grainPriceIndex * expenseFactor * dailyCost
                * LivingCostTable.FarmerMultiplierOf(origin, bracket)
                * LivingCostTable.GeneralZoneFactor(bracket == AgeBracket.Child, inRelief);

            var subtotal = Money.FromWen(perMemberWen * memberCount);
            lines.Add(new LivingCostLine(bracket, dailyCost, memberCount, subtotal));
            payable += subtotal;
        }

        return new LivingCostComputation(lines, payable);
    }

    /// <summary>
    /// 逐成员生活费明细（玩家反馈：收支悬浮窗要「按人罗列」）。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Compute"/> 用的是同一组乘区，区别只在**舍入点**：这里按人各自
    /// <see cref="Money.FromWen"/>，故合计可能与档位级舍入相差数文——本方法**只供界面展示**，
    /// 结算口径仍以 <see cref="Compute"/> 为准。
    /// </remarks>
    /// <param name="countedMembers">计口成员（在册且未服刑）。</param>
    /// <param name="date">当月年月。</param>
    /// <param name="livingStandard">当月生效的生活费档位。</param>
    /// <param name="grainPriceIndex">当月生效的米价系数。</param>
    /// <param name="difficulty">当月生效的难度。</param>
    /// <param name="origin">存档出身。</param>
    /// <param name="famineStage">判定前的饥馑阶段。</param>
    /// <returns>逐成员明细，顺序与 <paramref name="countedMembers"/> 一致。</returns>
    public static IReadOnlyList<LivingCostMemberLine> PerMember(
        IReadOnlyList<Person> countedMembers,
        GameDate date,
        LivingStandard livingStandard,
        decimal grainPriceIndex,
        Difficulty difficulty,
        Origin origin,
        FamineStage famineStage)
    {
        ArgumentNullException.ThrowIfNull(countedMembers);

        if (grainPriceIndex <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(grainPriceIndex), grainPriceIndex, "米价系数 MUST > 0（data-model §1.2）。");
        }

        var inRelief = famineStage == FamineStage.Relief;
        var expenseFactor = DifficultyRates.ExpenseFactor(difficulty);
        var lines = new List<LivingCostMemberLine>(countedMembers.Count);

        foreach (var person in countedMembers)
        {
            var bracket = AgeBracketPolicy.Of(person, date);
            var perMemberWen = LivingCostTable.DaysPerMonth * grainPriceIndex * expenseFactor
                * LivingCostTable.DailyCost(livingStandard, bracket)
                * LivingCostTable.FarmerMultiplierOf(origin, bracket)
                * LivingCostTable.GeneralZoneFactor(bracket == AgeBracket.Child, inRelief);

            lines.Add(new LivingCostMemberLine(person.Id, bracket, Money.FromWen(perMemberWen)));
        }

        return lines;
    }
}
