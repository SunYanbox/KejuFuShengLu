namespace KFL.Infrastructure.Abstractions;

// 本文件按 plan.md 的文件树落在 Save/ 下；命名空间按**契约九 §1 的签名块**归到
// Abstractions —— 契约与 plan 的文件树在此各说一半，二者兼顾即本文件的形态：
// 位置跟随 plan.md（Save/SaveOptions.cs），命名空间跟随契约九（KFL.Infrastructure.Abstractions）。
// SaveOptions 与 ISaveService/SaveSlot/SaveFormat 同属「存储侧跨层值类型」，放同一命名空间
// 也让装配方只需一条 using。

/// <summary>
/// 存档服务的装配选项（004-save-system；契约九 §1，FR-006）。
/// </summary>
/// <remarks>
/// <para>
/// 章程「存档、日志等运行时路径 MUST 可配置」：<see cref="RootDirectory"/> 为 <c>null</c> 时
/// 取 <c>AppContext.BaseDirectory</c> + <c>saves</c>；已配置时**原样使用**
/// （本类型不展开、不规整、不校验存在性；建目录失败由 <see cref="SaveAccessException"/> 表达）。
/// </para>
/// <para>
/// 这里**没有**「当前档槽位」：按 FR-010/Q4，槽位是会话信息，由回合编排层的构造参数携带
/// （<c>TurnRunner(IRandomService, ISaveService?, SaveSlot)</c>），MUST NOT 落盘。
/// </para>
/// </remarks>
public sealed record SaveOptions
{
    /// <summary>存档根目录；<c>null</c> ⇒ 可执行文件旁的 <c>saves/</c>。</summary>
    public string? RootDirectory { get; init; }

    /// <summary>未显式指定格式时使用的格式；默认二进制。</summary>
    public SaveFormat DefaultFormat { get; init; } = SaveFormat.Binary;
}
