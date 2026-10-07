namespace KFL.Core.ValueObjects;

/// <summary>
/// 米价系数（规格书 §5.1；research R-10 的 E-01）。
/// </summary>
/// <remarks>
/// <para>
/// **不变量**：<see cref="Value"/> MUST <c>&gt; 0</c>。
/// </para>
/// <para>
/// **明确不含**：clamp 区间 <c>0.7~3.0</c>、月游走幅度 <c>±10%</c>、米价派生系数 <c>0.4</c> / <c>0.6</c>
/// ——三者同属 <c>KFL.Rules/Config/GrainPricePolicy</c>（research R-01/R-10）；
/// **初始值 <c>1.0</c> 亦属该配置类**（contracts/config-registry.md §1），本类型 MUST NOT 另存副本
/// （SC-008：同一个规则数值 MUST NOT 出现第二处出处）。
/// </para>
/// <para>
/// 本类型只回答「米价系数是多少」，不回答「如何游走」「游走到哪止」——后者是规则层的职责。
/// </para>
/// </remarks>
public readonly record struct GrainPriceIndex
{
    /// <summary>构造并校验。</summary>
    /// <param name="value">米价系数，MUST <c>&gt; 0</c>。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> 不大于 0。</exception>
    public GrainPriceIndex(decimal value)
    {
        if (value <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value, "米价系数 MUST > 0（data-model §1.2）。");
        }

        Value = value;
    }

    /// <summary>米价系数。</summary>
    public decimal Value { get; }
}
