namespace KFL.Infrastructure.Abstractions;

/// <summary>
/// 一个存档槽位：**档名 + 格式**（004-save-system；契约九 §1，FR-005）。
/// </summary>
/// <remarks>
/// <para>
/// 默认参数 <c>Format = <see cref="SaveFormat.Binary"/></c> 就是「未指定格式 ⇒ 二进制」的表达，
/// 故本类型无需「格式未指定」这一第三态。
/// </para>
/// <para>
/// <see cref="Name"/> **不在本类型校验**：口径的唯一真源是 <c>SaveName.Validate</c>
/// （<c>KFL.Infrastructure/Save/SaveName.cs</c>），由 <c>SaveService</c> 在写任何字节之前调用。
/// 构造函数保持零副作用，才能让「非法档名不得触碰文件系统」成为一条可测的结构性事实
/// （契约九 §2）。
/// </para>
/// <para>
/// 本结构是**会话**信息（FR-010/Q4）：由装配期注入，MUST NOT 成为 <c>GameState</c> 的字段，
/// MUST NOT 随存档落盘。
/// </para>
/// </remarks>
/// <param name="Name">玩家可见的档名（不含扩展名）。</param>
/// <param name="Format">物理格式；默认二进制。</param>
public readonly record struct SaveSlot(string Name, SaveFormat Format = SaveFormat.Binary);
