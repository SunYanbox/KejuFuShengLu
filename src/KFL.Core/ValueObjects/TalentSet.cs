using KFL.Core.Config;

namespace KFL.Core.ValueObjects;

/// <summary>
/// 四项天赋（农、商、仕、工）。出生即定，终生不可通过游戏行为升降（规格书 §4.1）。
/// </summary>
/// <remarks>
/// 「仅调试控制台可修改」的落地方式属阶段⑦；本类型只保证**不存在**常规写入通道——
/// 整组不可变，四个成员都是只读属性。
/// </remarks>
public readonly record struct TalentSet
{
    /// <summary>构造并逐项校验。</summary>
    /// <param name="agriculture">农，0~100。</param>
    /// <param name="commerce">商，0~100。</param>
    /// <param name="officialdom">仕，0~100。</param>
    /// <param name="craft">工，0~100。</param>
    /// <exception cref="ArgumentOutOfRangeException">任一项不在 0~100。</exception>
    public TalentSet(int agriculture, int commerce, int officialdom, int craft)
    {
        AttributeLimits.EnsureInRange(agriculture, nameof(agriculture));
        AttributeLimits.EnsureInRange(commerce, nameof(commerce));
        AttributeLimits.EnsureInRange(officialdom, nameof(officialdom));
        AttributeLimits.EnsureInRange(craft, nameof(craft));

        Agriculture = agriculture;
        Commerce = commerce;
        Officialdom = officialdom;
        Craft = craft;
    }

    /// <summary>农。</summary>
    public int Agriculture { get; }

    /// <summary>商。</summary>
    public int Commerce { get; }

    /// <summary>仕。</summary>
    public int Officialdom { get; }

    /// <summary>工。</summary>
    public int Craft { get; }
}
