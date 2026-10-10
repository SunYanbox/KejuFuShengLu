using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;

namespace KFL.Rules.Career;

/// <summary>
/// 「及第入仕」入口（规格书 §8.2；FR-012、FR-014、FR-015；data-model §3.7；契约七 §3/§7）。
/// </summary>
/// <remarks>
/// <para>
/// 它把一名**无官职**的成员置入**待阙**：掷剩余月数（<c>Next(6, 24 + 1)</c>，**恰好 1 次</b> <c>Next</c>）
/// 并置状态位与计时。授官本身**不在本类**发生——待阙计时递减到 0 的当月由
/// <see cref="OfficialCareerAdvance"/> 的 ③-a 授官。
/// </para>
/// <para>
/// **MUST NOT**：写官阶（<see cref="Person.Rank"/>）、写 <see cref="Person.MonthsInOffice"/>、
/// 动账本、消耗除「待阙时长」以外的随机。
/// </para>
/// <para>
/// **失败原子性**（契约七 §7）：全部拒绝路径都在**校验通过之前不产生任何写入**——
/// 校验相在掷骰之前完成，故被拒时既不改状态也不消耗随机。
/// </para>
/// <para>
/// <b>赋值次序</b>：置位时 MUST **先 <see cref="Person.Status"/>、后 <see cref="Person.Timers"/>**——
/// <see cref="Person"/> 的交叉校验规定「计时非空 ⇔ 对应状态位为真」，故位必须先为真；
/// 清位时次序相反（先清计时、后清位，见 <see cref="OfficialCareerAdvance"/> ③-a）。
/// 两处都必须保留其余计时字段（重建 <see cref="StatusTimers"/> 而不是整体覆盖为 <c>default</c>）。
/// </para>
/// <para>
/// **明确不含**：科举细节（解试 / 省试 / 殿试、免解、特奏名的触发与拒绝，逻辑轨 ⑤）。
/// </para>
/// </remarks>
public static class AppointmentEntry
{
    /// <summary>进士入口：按功名记录末条派生入仕途径并置入待阙。</summary>
    /// <param name="person">无官职的成员。</param>
    /// <param name="date">触发年月（本特性只用于校验信息，不写任何年月字段）。</param>
    /// <param name="random">随机来源（待阙时长的唯一来源）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 或 <paramref name="random"/> 为 <c>null</c>。</exception>
    /// <exception cref="InvalidOperationException">
    /// 已有官阶、已在待阙、末条功名不是进士、或进士但甲第缺失（**不猜等级**）。
    /// </exception>
    public static void BeginForImperialGraduate(Person person, GameDate date, IRandomService random)
    {
        ArgumentNullException.ThrowIfNull(person);

        EnsureEligible(person);

        var record = LastDegree(person);

        if (record is not { } last || last.Level != DegreeLevel.JinShi)
        {
            throw new InvalidOperationException(
                "进士入口要求功名记录末条为进士（§8.2；契约七 §7）。");
        }

        if (last.Class is not { } imperialClass)
        {
            throw new InvalidOperationException(
                "进士的甲第缺失：入口 MUST 拒绝，MUST NOT 猜一个等级（FR-014；契约七 §3 条款 7）。");
        }

        Begin(person, TrackOfClass(imperialClass), date, random);
    }

    /// <summary>
    /// 通用入口（供逻辑轨 ⑤ 的特奏名复用）：按**显式**途径置入待阙。
    /// </summary>
    /// <param name="person">无官职的成员。</param>
    /// <param name="track">入仕途径。</param>
    /// <param name="date">触发年月。</param>
    /// <param name="random">随机来源（待阙时长的唯一来源）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 或 <paramref name="random"/> 为 <c>null</c>。</exception>
    /// <exception cref="InvalidOperationException">已有官阶，或已在待阙（MUST NOT 重置剩余月数）。</exception>
    public static void Begin(
        Person person, AppointmentTrack track, GameDate date, IRandomService random)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(random);

        EnsureEligible(person);

        var months = OfficialCareerPolicy.NextAwaitingPostMonths(random);

        // 位先真、计时后落（Person 的交叉校验方向：计时非空 ⇔ 位为真）。
        person.Status |= StatusFlag.AwaitingPost;
        person.Timers = ReplaceAwaitingPost(person.Timers, months);
    }

    /// <summary>
    /// 从功名记录**派生**入仕途径（③-a 授官时复用）：进士按甲第映射；
    /// 无进士记录者（特奏名等）取 <see cref="AppointmentTrack.SpecialTribute"/>。
    /// </summary>
    /// <param name="person">成员。</param>
    /// <returns>入仕途径。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 为 <c>null</c>。</exception>
    /// <exception cref="InvalidOperationException">末条是进士但甲第缺失（不可派生）。</exception>
    public static AppointmentTrack TrackOf(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        var record = LastDegree(person);

        if (record is not { } last || last.Level != DegreeLevel.JinShi)
        {
            return AppointmentTrack.SpecialTribute;
        }

        return last.Class is { } imperialClass
            ? TrackOfClass(imperialClass)
            : throw new InvalidOperationException(
                "进士的甲第缺失：无法派生入仕途径（FR-014；契约七 §3 条款 7）。");
    }

    /// <summary>校验相：已有官阶或已在待阙即拒绝，且**不做任何写入、不消耗随机**。</summary>
    private static void EnsureEligible(Person person)
    {
        if (person.Rank is not null)
        {
            throw new InvalidOperationException(
                "已有官阶者 MUST NOT 再次「及第入仕」（FR-015；契约七 §7）。");
        }

        if (person.Status.HasFlag(StatusFlag.AwaitingPost))
        {
            throw new InvalidOperationException(
                "已在待阙者 MUST NOT 重复触发入口，且剩余月数 MUST NOT 被重置（FR-015；契约七 §7）。");
        }
    }

    private static AppointmentTrack TrackOfClass(ImperialClass imperialClass) => imperialClass switch
    {
        ImperialClass.FirstClass => AppointmentTrack.FirstClass,
        ImperialClass.SecondClass => AppointmentTrack.SecondClass,
        ImperialClass.ThirdClass => AppointmentTrack.ThirdClass,
        _ => throw new ArgumentOutOfRangeException(
            nameof(imperialClass), imperialClass, "未登记的甲第（规格书 §4.1 共三种）。"),
    };

    private static DegreeRecord? LastDegree(Person person) =>
        person.DegreeHistory.Count == 0 ? null : person.DegreeHistory[^1];

    /// <summary>只替换待阙计时，**保留**其余三个计时字段（重建结构体，不整体覆盖）。</summary>
    internal static StatusTimers ReplaceAwaitingPost(StatusTimers timers, int? remainingMonths) =>
        new(
            timers.SentenceRemainingMonths,
            timers.ExamBanRemainingMonths,
            timers.PromotionBanRemainingMonths,
            remainingMonths);
}
