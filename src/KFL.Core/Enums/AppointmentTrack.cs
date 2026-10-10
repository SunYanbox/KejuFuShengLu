namespace KFL.Core.Enums;

/// <summary>
/// 入仕途径（规格书 §8.2；FR-014）：一甲 / 二甲 / 三甲 / 特奏名。
/// </summary>
/// <remarks>
/// <para>
/// 它是「及第入仕」入口的**入参**：把「怎么入仕」与「功名记录的形状」解耦，使逻辑轨 ⑤ 的
/// **特奏名**（规格书 §6：50 岁 + 省试 6 败 → 授 L18，可拒绝）无需伪造一条进士记录。
/// </para>
/// <para>
/// 「途径 → 初始官阶级数」的映射是**规则数值**，单点在
/// <c>KFL.Rules/Config/OfficialCareerPolicy.InitialRankOf</c>（SC-009）。
/// </para>
/// </remarks>
public enum AppointmentTrack
{
    /// <summary>一甲（授 L11）。</summary>
    FirstClass,

    /// <summary>二甲（授 L13）。</summary>
    SecondClass,

    /// <summary>三甲（授 L15）。</summary>
    ThirdClass,

    /// <summary>特奏名（授 L18；触发与拒绝属逻辑轨 ⑤）。</summary>
    SpecialTribute,
}
