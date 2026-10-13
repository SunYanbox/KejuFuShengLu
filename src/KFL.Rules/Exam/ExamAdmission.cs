using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;

namespace KFL.Rules.Exam;

/// <summary>
/// 录取率、进甲率、甲第归属与名次 roll（规格书 §6、§7.1；契约十三 §4）。
/// </summary>
/// <remarks>
/// <para>
/// 全部为**纯函数**：MUST NOT 读全局态、MUST NOT 改状态、MUST NOT 消耗随机。
/// 「概率计算与掷骰分离」是硬约束（契约十三 §4.3 条款 7）——本类只算概率，
/// 掷骰由 <see cref="ExamSeasonRunner"/> 用注入的 <see cref="IRandomService"/> 执行。
/// </para>
/// <para>
/// 数值一律取自 <see cref="ExamPolicy"/>，本类 MUST NOT 自带任何数值字面量（SC-008）。
/// </para>
/// </remarks>
public static class ExamAdmission
{
    /// <summary>
    /// 解试 / 省试录取率（契约十三 §4.1）：
    /// <c>基础率 × 学业系数 × (1 + 仕天赋 / 400) × 贿赂乘数 × 仕族加成</c>，clamp 到 <c>[0, 89.9999%]</c>。
    /// </summary>
    /// <param name="level">考试级别；只接受解试 / 省试。</param>
    /// <param name="person">考生（取**当月** <see cref="Person.Study"/> 与 <see cref="Person.Talents"/>）。</param>
    /// <param name="hasShiStatus">家族是否已是仕族（<c>Family.HasShiStatus</c>）。</param>
    /// <param name="bribeMultiplier">贿赂乘数；本轮恒为 <see cref="ExamPolicy.NeutralBribeMultiplier"/>。</param>
    /// <returns>已封顶的录取率。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 为 <c>null</c>。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> 不是解试或省试。</exception>
    /// <remarks>
    /// 殿试不走本入口：进殿试者 100% 成为进士（不掷骰、不受封顶影响，契约十三 §4.2 ①）。
    /// </remarks>
    public static decimal AdmissionRate(
        ExamLevel level, Person person, bool hasShiStatus, decimal bribeMultiplier)
    {
        ArgumentNullException.ThrowIfNull(person);

        var shiBonus = hasShiStatus ? ExamPolicy.ShiStatusAdmissionBonus : 1m;

        return Clamp(
            ExamPolicy.BaseRateOf(level)
            * ExamPolicy.StudyFactor(person.Study)
            * (1m + (person.Talents.Officialdom / (decimal)ExamPolicy.OfficialdomDivisor))
            * bribeMultiplier
            * shiBonus);
    }

    /// <summary>
    /// 殿试进甲率（契约十三 §4.2 ②）：<c>5% × 学业系数 × (1 + 仕天赋 / 400) × 贿赂乘数</c>，
    /// clamp 到 <c>[0, 89.9999%]</c>。
    /// </summary>
    /// <param name="person">考生（取**当月**值）。</param>
    /// <param name="bribeMultiplier">贿赂乘数；本轮恒为 <see cref="ExamPolicy.NeutralBribeMultiplier"/>。</param>
    /// <returns>已封顶的进甲率。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 为 <c>null</c>。</exception>
    /// <remarks>
    /// **MUST NOT 乘仕族加成**：<see cref="Family.HasShiStatus"/> 真 / 假时本方法的结果完全相同
    /// （契约十三 §4.3 条款 2）——本方法因此不接收该参数。
    /// </remarks>
    public static decimal ImperialPromotionRate(Person person, decimal bribeMultiplier)
    {
        ArgumentNullException.ThrowIfNull(person);

        return Clamp(
            ExamPolicy.ImperialPromotionBaseRate
            * ExamPolicy.StudyFactor(person.Study)
            * (1m + (person.Talents.Officialdom / (decimal)ExamPolicy.OfficialdomDivisor))
            * bribeMultiplier);
    }

    /// <summary>
    /// 殿试甲第（契约十三 §4.2 ③、④）：进甲 ⇒ 一甲；未进甲 ⇒ 学业 <c>&gt;= 50</c> 二甲，否则三甲。
    /// </summary>
    /// <param name="person">考生。</param>
    /// <param name="promoted">是否进甲。</param>
    /// <returns>甲第。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 为 <c>null</c>。</exception>
    public static ImperialClass ImperialClassFor(Person person, bool promoted)
    {
        ArgumentNullException.ThrowIfNull(person);

        if (promoted)
        {
            return ImperialClass.FirstClass;
        }

        return person.Study >= ExamPolicy.SecondClassStudyThreshold
            ? ImperialClass.SecondClass
            : ImperialClass.ThirdClass;
    }

    /// <summary>
    /// 一甲名次 roll（契约十三 §4.4）：恰好 <c>random.Next(1, 4)</c> 一次，
    /// <c>1 → ZhuangYuan</c>、<c>2 → BangYan</c>、<c>3 → TanHua</c>。
    /// </summary>
    /// <param name="random">随机来源（本方法消耗恰好 1 次 <c>Next</c>）。</param>
    /// <returns>名次。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="random"/> 为 <c>null</c>。</exception>
    /// <remarks>名次与学业、天赋、仕族、贿赂**均无关**（契约十三 §4.4 条款 3）。</remarks>
    public static ImperialPlacement RollPlacement(IRandomService random)
    {
        ArgumentNullException.ThrowIfNull(random);

        return random.Next(1, 4) switch
        {
            1 => ImperialPlacement.ZhuangYuan,
            2 => ImperialPlacement.BangYan,
            3 => ImperialPlacement.TanHua,
            var value => throw new ArgumentOutOfRangeException(
                nameof(random), value, "名次 roll 只能取 1~3（契约十三 §4.4）。"),
        };
    }

    private static decimal Clamp(decimal rate) => Math.Clamp(rate, 0m, ExamPolicy.DiceRateCap);
}
