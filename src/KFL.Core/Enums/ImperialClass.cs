namespace KFL.Core.Enums;

/// <summary>
/// 甲第（规格书 §6；§17 裁决回写 2026-10-08）：一甲 / 二甲 / 三甲。
/// </summary>
/// <remarks>
/// <para>
/// **取值顺序即甲第高低**（一甲 = 进甲者，其余按学业分二甲 / 三甲），但本类型
/// **不承载任何级数**——「一甲 → L11」之类的授官映射是**规则数值**，单点在
/// <c>KFL.Rules/Config/OfficialCareerPolicy</c>（SC-009）。
/// </para>
/// <para>
/// 甲第只属于进士，且与 <see cref="ValueObjects.DegreeRecord.Placement"/> 互相约束：
/// 名次非空 ⟺ 甲第 = 一甲（规格书 §4.1；FR-014）。
/// </para>
/// </remarks>
public enum ImperialClass
{
    /// <summary>一甲（进甲者，有状元 / 榜眼 / 探花名次）。</summary>
    FirstClass,

    /// <summary>二甲。</summary>
    SecondClass,

    /// <summary>三甲。</summary>
    ThirdClass,
}
