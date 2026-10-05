namespace KFL.Core.Config;

/// <summary>
/// 实体**自不变量**的取值域常量（天赋 / 学业 / 体质 0~100）。
/// </summary>
/// <remarks>
/// <para>
/// 归 <c>KFL.Core</c> 的理由：依赖方向是 <c>Core ← Infrastructure ← Rules</c>，
/// <c>KFL.Core</c> **不可能**引用 <c>KFL.Rules</c>；把 0~100 放进 <c>GameConfig</c>
/// 会让 FR-005/FR-006 退化成只在测试里口头成立（research R-06，所有者已确认）。
/// </para>
/// <para>
/// **MUST NOT** 把政绩上限 100 放进来——它属规格书 §8.2 的规则数值，随阶段⑧落地。
/// </para>
/// </remarks>
public static class AttributeLimits
{
    /// <summary>取值下界（含）。</summary>
    public const int Min = 0;

    /// <summary>取值上界（含）。</summary>
    public const int Max = 100;

    /// <summary>校验取值落在 <see cref="Min"/>~<see cref="Max"/>，越界抛异常。</summary>
    /// <param name="value">待校验的值。</param>
    /// <param name="paramName">参数名，用于异常信息。</param>
    /// <exception cref="ArgumentOutOfRangeException">取值越界。</exception>
    public static void EnsureInRange(int value, string paramName)
    {
        if (value < Min || value > Max)
        {
            throw new ArgumentOutOfRangeException(
                paramName, value, $"取值必须在 {Min}~{Max} 之间（规格书 §4.1）。");
        }
    }
}
