using KFL.Core.Config;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;

namespace KFL.Rules.FamilyLifecycle;

/// <summary>
/// 遗传取值：新生儿属性由父母属性经正态分布派生（契约二十 §6、FR-011）。
/// </summary>
/// <remarks>
/// <para>
/// 公式（规格书 §4.2）：<c>mean = (父 + 母) / 2</c>、
/// <c>sigma = |父 − 母| / 2 + <see cref="ChildbirthPolicy.InheritanceSigmaBase"/></c>、
/// <c>value = clamp(round(N(mean, sigma)), 0, 100)</c>；学业均值再乘
/// <see cref="ChildbirthPolicy.StudyMeanHalving"/><b>但共用同一个 sigma</b>；体质另加
/// <c>max(value, <see cref="ChildbirthPolicy.InheritedHealthFloor"/>)</c>。
/// </para>
/// <para>
/// <b>任一方缺失（含单亲）</b>MUST 走「无父母参照者」分布（<see cref="AttributePolicy.NextTalent"/> /
/// <see cref="NextStudyFromNoParents"/> / <see cref="NextHealthFromNoParents"/>），
/// <b>MUST NOT 用单亲的值外推</b>（T042）。
/// </para>
/// <para>
/// 取整一律 <see cref="MidpointRounding.AwayFromZero"/>；每次取值恰好消耗
/// <b>2 次</b> <see cref="IRandomService.NextDouble"/>（复用
/// <see cref="AttributePolicy.NextNormal"/>）。本类为纯函数：只读父母值、只消费随机，
/// MUST NOT 写任何状态。
/// </para>
/// </remarks>
public static class Inheritance
{
    /// <summary>天赋遗传：<c>N((父 + 母) / 2, |父 − 母| / 2 + 5)</c> → 取整 → clamp 0~100。</summary>
    /// <param name="father">父方天赋。</param>
    /// <param name="mother">母方天赋。</param>
    /// <param name="random">随机来源。</param>
    /// <returns>新生儿该天赋取值。</returns>
    public static int Attribute(int father, int mother, IRandomService random) =>
        Draw(father, mother, meanScale: 1d, random);

    /// <summary>
    /// 天赋遗传（容许缺失）：任一方缺失（含单亲）⇒ 无父母参照者分布
    /// <c>N(60, 20)</c>，**MUST NOT** 用单亲的值外推（T042 / FR-011）。
    /// </summary>
    /// <param name="father">父方天赋；缺失为 <c>null</c>。</param>
    /// <param name="mother">母方天赋；缺失为 <c>null</c>。</param>
    /// <param name="random">随机来源。</param>
    /// <returns>新生儿该天赋取值。</returns>
    public static int Attribute(int? father, int? mother, IRandomService random) =>
        father is null || mother is null
            ? NextTalentFromNoParents(random)
            : Attribute(father.Value, mother.Value, random);

    /// <summary>
    /// 学业遗传：均值 = 父母学业均值 × <see cref="ChildbirthPolicy.StudyMeanHalving"/>，
    /// 标准差与天赋<b>完全相同</b>（<c>|父 − 母| / 2 + 5</c>）。
    /// </summary>
    /// <param name="father">父方学业。</param>
    /// <param name="mother">母方学业。</param>
    /// <param name="random">随机来源。</param>
    /// <returns>新生儿学业取值。</returns>
    public static int Study(int father, int mother, IRandomService random) =>
        Draw(father, mother, (double)ChildbirthPolicy.StudyMeanHalving, random);

    /// <summary>学业遗传（容许缺失）：任一方缺失 ⇒ 无父母参照者分布 <c>N(30, 15)</c>。</summary>
    /// <param name="father">父方学业；缺失为 <c>null</c>。</param>
    /// <param name="mother">母方学业；缺失为 <c>null</c>。</param>
    /// <param name="random">随机来源。</param>
    /// <returns>新生儿学业取值。</returns>
    public static int Study(int? father, int? mother, IRandomService random) =>
        father is null || mother is null
            ? NextStudyFromNoParents(random)
            : Study(father.Value, mother.Value, random);

    /// <summary>
    /// 体质遗传：同 <see cref="Attribute"/>，另加下限
    /// <see cref="ChildbirthPolicy.InheritedHealthFloor"/>（先 clamp 再取下限）。
    /// </summary>
    /// <param name="father">父方体质。</param>
    /// <param name="mother">母方体质。</param>
    /// <param name="random">随机来源。</param>
    /// <returns>新生儿体质取值。</returns>
    public static int Health(int father, int mother, IRandomService random) =>
        Math.Max(
            Draw(father, mother, meanScale: 1d, random),
            ChildbirthPolicy.InheritedHealthFloor);

    /// <summary>体质遗传（容许缺失）：任一方缺失 ⇒ 无父母参照者分布 <c>N(85, 10)</c>。</summary>
    /// <param name="father">父方体质；缺失为 <c>null</c>。</param>
    /// <param name="mother">母方体质；缺失为 <c>null</c>。</param>
    /// <param name="random">随机来源。</param>
    /// <returns>新生儿体质取值。</returns>
    public static int Health(int? father, int? mother, IRandomService random) =>
        father is null || mother is null
            ? NextHealthFromNoParents(random)
            : Health(father.Value, mother.Value, random);

    /// <summary>无父母参照者天赋：<c>N(60, 20)</c>（FR-011、T042）。</summary>
    /// <param name="random">随机来源。</param>
    /// <returns>天赋取值。</returns>
    public static int NextTalentFromNoParents(IRandomService random) => AttributePolicy.NextTalent(random);

    /// <summary>无父母参照者学业：<c>N(30, 15)</c>。</summary>
    /// <param name="random">随机来源。</param>
    /// <returns>学业取值。</returns>
    public static int NextStudyFromNoParents(IRandomService random) => AttributePolicy.NextStudy(random);

    /// <summary>无父母参照者体质：<c>N(85, 10)</c>（此处不叠 <c>max(20)</c>，该分布本身高于下限）。</summary>
    /// <param name="random">随机来源。</param>
    /// <returns>体质取值。</returns>
    public static int NextHealthFromNoParents(IRandomService random) => AttributePolicy.NextHealth(random);

    /// <summary>遗传单值抽取（sigma 恒同、均值可缩放）。</summary>
    private static int Draw(int father, int mother, double meanScale, IRandomService random)
    {
        var mean = ((father + mother) / 2d) * meanScale;
        var sigma = (Math.Abs(father - mother) / 2d) + ChildbirthPolicy.InheritanceSigmaBase;

        var value = (int)Math.Round(
            AttributePolicy.NextNormal(mean, sigma, random),
            MidpointRounding.AwayFromZero);

        return Math.Clamp(value, AttributeLimits.Min, AttributeLimits.Max);
    }
}
