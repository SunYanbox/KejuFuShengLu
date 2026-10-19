using KFL.Core.Config;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;
using KFL.Rules.Economy;
using KFL.Rules.Settlement;

namespace KFL.Rules.FamilyLifecycle;

/// <summary>
/// ⑧-d 成长（契约二十 §6）：学业增减与体质自然恢复，<b>零随机消费</b>。
/// </summary>
/// <remarks>
/// <para>
/// 学业变化（<b>取整口径封印</b>，Assumption 13）：全部加成先用 <see cref="decimal"/> <b>累加</b>
/// （含患病期减半），最后一次 <see cref="MidpointRounding.AwayFromZero"/> 取整，再
/// <see cref="Math.Clamp(int, int, int)"/> 到 <c>0~100</c> 后写 <see cref="Person.Study"/>
/// （setter 是抛异常守卫，不是静默 clamp）。次序：读书 <c>+1.5</c> → 延名师 <c>+2.25</c>
/// （<b>先</b>扣 <c>2 贯 × 支出系数</c>）→ 士出身 <c>×1.10</c> → <c>AgeAt &gt; 16</c> 且未在读书
/// <b>且已指派非读书职业</b> <c>−0.5</c>（16 岁不遗忘、17 岁起遗忘；<b>未指派者不遗忘</b>，
/// §17 2026-10-19 第七轮 Q5）→ 患病期减半。
/// </para>
/// <para>
/// 名师费付不起 ⇒ 按普通速度、<see cref="Person.IsTutoredByMaster"/> <b>保持</b>、
/// MUST NOT 转贷款（Assumption 9）。
/// </para>
/// <para>
/// 体质自然恢复 <c>+1</c> 的条件读作「<c>FamineStage.None</c> 且非患病」：
/// 生活费未足额付清时 ⑥ 的饥馑状态机必然已把阶段迁出 <c>None</c>（E-14），
/// 故「当月生活费足额付清」已被 <c>Stage == None</c> 蕴含，无需另传标志。
/// </para>
/// </remarks>
public static class GrowthAdvance
{
    /// <summary>推进一个月的学业与体质成长。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="date">当月（由 ⑧ 传入，MUST NOT 自取 <c>CurrentDate</c> 之外的时间源）。</param>
    public static void Run(GameState state, GameDate date)
    {
        ArgumentNullException.ThrowIfNull(state);

        foreach (var person in CountedMembers.Registered(state.Family).OrderBy(member => member.Id.Value))
        {
            GrowStudy(state, person, date);
            RecoverHealth(state, person);
        }
    }

    /// <summary>学业变化（十进制累加 + 一次取整）。</summary>
    private static void GrowStudy(GameState state, Person person, GameDate date)
    {
        var age = person.AgeAt(date);
        var studying = person.Occupation == Occupation.Studying;
        var delta = 0m;

        if (studying)
        {
            delta += GrowthPolicy.StudyPerMonth;
        }

        if (person.IsTutoredByMaster)
        {
            var fee = Money.FromGuan(GrowthPolicy.TutorFeeGuanPerMonth * DifficultyRates.ExpenseFactor(state.Difficulty));

            if (PaymentPrimitive.CanCover(state.Economy, fee))
            {
                PaymentPrimitive.Pay(state.Economy, LedgerCategory.TutorFee, date, fee, person.Id);
                delta += GrowthPolicy.TutorStudyBonusPerMonth;
            }
        }

        if (state.Origin == Origin.Scholar)
        {
            delta *= GrowthPolicy.ScholarStudyMultiplier;
        }

        // 遗忘：只施加于**已指派非读书职业**者——未指派（Occupation.None）不构成「未读书」，
        // MUST NOT 因此扣学业（规格书 §4.3 / §17 2026-10-19 第七轮 Q5）。
        var assignedToNonStudyWork = person.Occupation
            is Occupation.Farming or Occupation.Crafting or Occupation.Trading;

        if (age > GrowthPolicy.ForgetAgeThreshold && !studying && assignedToNonStudyWork)
        {
            delta -= GrowthPolicy.ForgetPerMonth;
        }

        if ((person.Status & StatusFlag.Ill) == StatusFlag.Ill)
        {
            delta /= GrowthPolicy.IllnessStudyPenaltyDivisor;
        }

        var rounded = (int)Math.Round(delta, MidpointRounding.AwayFromZero);
        person.Study = Math.Clamp(person.Study + rounded, AttributeLimits.Min, AttributeLimits.Max);
    }

    /// <summary>体质自然恢复（仅非饥馑、非患病；生活费足额付清由饥馑阶段蕴含）。</summary>
    private static void RecoverHealth(GameState state, Person person)
    {
        var famine = state.Economy.Famine.Stage;
        var ill = (person.Status & StatusFlag.Ill) == StatusFlag.Ill;

        if (famine != FamineStage.None || ill)
        {
            return;
        }

        person.Health = Math.Min(AttributeLimits.Max, person.Health + GrowthPolicy.HealthRecoverPerMonth);
    }
}
