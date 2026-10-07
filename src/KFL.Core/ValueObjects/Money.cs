namespace KFL.Core.ValueObjects;

/// <summary>
/// 金额值类型。内部**唯一**存储是以「文」为单位的 <see cref="decimal"/>（章程「技术栈与工程约束」：1 贯 = 1000 文）。
/// </summary>
/// <remarks>
/// <para>
/// **不变量**：<see cref="Wen"/> ∈ <see cref="decimal"/> 全域。这不是一条需要守卫的约束——
/// <see cref="decimal"/> 是 128 位十进制浮点，**没有** <c>NaN</c> / <c>±Infinity</c> 表示，
/// 故「非有限金额」在类型层面即不可表达（<c>decimal</c> 与 <c>double</c> 的这一差异是
/// <c>Money</c> 选 <c>decimal</c> 的理由之一）。唯一的越界风险是算术**溢出**，
/// 由 <c>decimal</c> 运算符抛 <see cref="OverflowException"/>（如
/// <see cref="FromGuan"/> 的 ×1000 换算），不静默截断。
/// </para>
/// <para>
/// **明确不含**：
/// ① **任何舍入**——research R-02 规定结算一律保留 <see cref="decimal"/> 全精度，取整与贯/文展示换算
/// 只发生在展示层（阶段③）；
/// ② **隐式转换**——不提供 <c>double</c> / <c>int</c> / <c>decimal</c> 的隐式转换，把「贯」「文」的
/// 口径混淆挡在编译期（data-model §1.1「明确不含」）；
/// ③ **裸构造入口**——<c>new Money(500)</c> 不可用，具名入口只有 <see cref="FromWen"/> 与 <see cref="FromGuan"/>。
/// </para>
/// </remarks>
public readonly record struct Money
{
    /// <summary>以文为单位构造。**唯一**的底层构造入口，对外不可见。</summary>
    /// <param name="wen">文数。</param>
    private Money(decimal wen) => Wen = wen;

    /// <summary>0 文。</summary>
    public static Money Zero => new(0m);

    /// <summary>以**文**为单位的金额（本类型的唯一存储）。</summary>
    public decimal Wen { get; }

    /// <summary>以**贯**为单位的派生值（= <see cref="Wen"/> ÷ 1000），**仅用于展示与断言**。</summary>
    public decimal Guan => Wen / 1000m;

    /// <summary>是否为正数（结算分支用）。</summary>
    public bool IsPositive => Wen > 0m;

    /// <summary>是否为负数（结算分支用）。</summary>
    public bool IsNegative => Wen < 0m;

    /// <summary>以「文」为单位的具名构造入口。</summary>
    /// <param name="wen">文数。</param>
    /// <returns>对应金额。</returns>
    /// <remarks>
    /// 不做任何校验：**没有**可校验的东西——<paramref name="wen"/> 的静态类型 <see cref="decimal"/>
    /// 已经排除了 <c>NaN</c> / <c>Infinity</c>，「有限性检查」在 <c>decimal</c> 上恒真（死代码）。
    /// 越界只可能来自上游算术的溢出，而溢出在算出的那一刻就已抛出 <see cref="OverflowException"/>，
    /// 不会以非法值的形式传到这里。
    /// </remarks>
    public static Money FromWen(decimal wen) => new(wen);

    /// <summary>以「贯」为单位的具名构造入口（= <see cref="FromWen"/>(贯 × 1000)）。</summary>
    /// <param name="guan">贯数。</param>
    /// <returns>对应金额。</returns>
    /// <exception cref="OverflowException"><paramref name="guan"/> × 1000 超出 <see cref="decimal"/> 值域。</exception>
    public static Money FromGuan(decimal guan) => FromWen(guan * 1000m);

    /// <summary>两笔金额相加。</summary>
    /// <param name="left">左值。</param>
    /// <param name="right">右值。</param>
    /// <returns>和。</returns>
    public static Money operator +(Money left, Money right) => new(left.Wen + right.Wen);

    /// <summary>两笔金额相减。</summary>
    /// <param name="left">左值。</param>
    /// <param name="right">右值。</param>
    /// <returns>差。</returns>
    public static Money operator -(Money left, Money right) => new(left.Wen - right.Wen);

    /// <summary>金额乘以系数（与 <see cref="decimal"/> 的乘法只在「乘系数」处使用，data-model §1.1）。</summary>
    /// <param name="value">被乘金额。</param>
    /// <param name="factor">系数。</param>
    /// <returns>积与比例。**保留全精度**。</returns>
    public static Money operator *(Money value, decimal factor) => new(value.Wen * factor);

    /// <summary>取相反数。</summary>
    /// <param name="value">金额。</param>
    /// <returns>相反数。</returns>
    public static Money operator -(Money value) => new(-value.Wen);

    /// <summary>严格小于。</summary>
    /// <param name="left">左值。</param>
    /// <param name="right">右值。</param>
    /// <returns><paramref name="left"/> 严格小于 <paramref name="right"/> 为 <c>true</c>。</returns>
    public static bool operator <(Money left, Money right) => left.Wen < right.Wen;

    /// <summary>严格大于。</summary>
    /// <param name="left">左值。</param>
    /// <param name="right">右值。</param>
    /// <returns><paramref name="left"/> 严格大于 <paramref name="right"/> 为 <c>true</c>。</returns>
    public static bool operator >(Money left, Money right) => left.Wen > right.Wen;
}
