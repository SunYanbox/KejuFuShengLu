namespace KFL.Infrastructure.Abstractions;

/// <summary>存档文件的物理格式（004-save-system；契约九 §1）。</summary>
/// <remarks>
/// <para>
/// 两个枚举值的**信息集等价**（契约十 §5）：同一状态经任一格式落盘，逐字段结果一致。
/// 格式只决定字节形态，不决定存了什么。
/// </para>
/// <para>
/// <see cref="Binary"/> 的值为 <c>0</c>：即 <see cref="SaveFormat"/> 的零值就是默认格式
/// （FR-005），所以 <c>default(SaveFormat)</c> 与「玩家未指定格式」同义，
/// 不需要额外的「未指定」哨兵值。
/// </para>
/// </remarks>
public enum SaveFormat
{
    /// <summary>二进制（MessagePack）；扩展名 <c>.dat</c>；**默认格式**。</summary>
    Binary = 0,

    /// <summary>文本 JSON；扩展名 <c>.json</c>。</summary>
    Json = 1,
}
