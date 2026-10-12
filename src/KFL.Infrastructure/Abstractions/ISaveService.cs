using KFL.Core.Entities;

namespace KFL.Infrastructure.Abstractions;

/// <summary>
/// 存档服务：把一份 <see cref="GameState"/> 落盘、再从盘上复原（004-save-system；契约九 §1）。
/// </summary>
/// <remarks>
/// <para>
/// <b>归属</b>：定义与实现都在 <c>KFL.Infrastructure</c>（规格书 §2），MUST NOT 引用任何
/// WPF/Windows 类型。001 契约二 §5 登记为「待定义」的接缝即此处。
/// </para>
/// <para>
/// <b>注入</b>：MUST 经构造函数注入（如 <c>SaveOptions</c>），MUST NOT 静态单例、
/// MUST NOT <c>ServiceLocator</c>、MUST NOT 属性注入；「当前档槽位」（档名 + 格式）是
/// **会话**信息，由装配期决定并注入（Q4），MUST NOT 成为 <c>GameState</c> 的字段、
/// MUST NOT 落盘。
/// </para>
/// <para>
/// <b>失败即明说</b>：所有失败都以异常表达，MUST NOT 静默吞掉（契约九 §2 的失败矩阵）。
/// 且每个公开方法 MUST 在写任何字节**之前**完成全部校验（契约九 §4 第 6 条）——
/// 拒绝路径不得留下半成品文件。
/// </para>
/// <para>
/// <b>不变量</b>：<see cref="Load"/> 只**新建**一个 <see cref="GameState"/>；失败路径
/// MUST NOT 改动原文件、MUST NOT 返回默认/部分状态、MUST NOT 污染调用方已有的内存状态
/// （契约九 §6、契约十 §6）。
/// </para>
/// </remarks>
public interface ISaveService
{
    /// <summary>当前存档根目录（绝对路径）。</summary>
    /// <remarks>
    /// 未配置时是 <c>AppContext.BaseDirectory</c> 下的 <c>saves</c>（FR-006）；
    /// 已配置时是 <c>SaveOptions.RootDirectory</c> 原样（契约九 §4 第 3 条）。
    /// </remarks>
    string RootDirectory { get; }

    /// <summary>该档名是否**已有存档**（不分格式）。</summary>
    /// <remarks>
    /// 档名比较**大小写不敏感**（Q7：Windows 文件系统口径，<c>SongJia</c> ≡ <c>songjia</c>）。
    /// 备份目录里的文件**不参与**本判定（契约九 §5）。
    /// </remarks>
    /// <param name="slot">档名 + 格式；格式不影响本判定。</param>
    /// <returns>存在任一格式的同名档 ⇒ <c>true</c>。</returns>
    bool Exists(SaveSlot slot);

    /// <summary>按存档标识反查档名。</summary>
    /// <remarks>
    /// 只读文件**头**（MUST NOT 解析主体）。找到 ⇒ 返回该槽位；未找到 ⇒ <c>null</c>
    /// （契约九 §5）。
    /// </remarks>
    /// <param name="saveId">存档标识。</param>
    /// <returns>携带该标识的槽位，或 <c>null</c>。</returns>
    SaveSlot? FindBySaveId(Guid saveId);

    /// <summary>该槽位在当前根目录下的绝对路径。</summary>
    /// <remarks>
    /// 档名在名称层就已拒绝路径分隔符，故返回值不会逃出 <see cref="RootDirectory"/>
    /// （契约九 §5 第 5 条）。
    /// </remarks>
    /// <param name="slot">档名 + 格式。</param>
    /// <returns>绝对路径（文件不必存在）。</returns>
    string FilePathOf(SaveSlot slot);

    /// <summary>把状态落盘到该槽位。</summary>
    /// <remarks>
    /// <para>
    /// 写入经原子替换完成（临时文件 → 落盘刷盘 → 同卷改名），故目标文件**任何时刻**
    /// 要么是完整的旧档、要么是完整的新档，绝不会是半个（FR-013；契约九 §3）。
    /// </para>
    /// <para>
    /// <b>失败矩阵</b>（契约九 §2）：档名非法 ⇒ <see cref="ArgumentException"/>（不触碰文件系统）；
    /// 同名档已存在且 <paramref name="overwrite"/> 为 <c>false</c> ⇒
    /// <see cref="SaveNameConflictException"/>；同根下另一**文件名**已携带同一存档标识 ⇒
    /// <see cref="SaveIdentityConflictException"/>（一个字节都不写）；根目录建不出/写不进 ⇒
    /// <see cref="SaveAccessException"/>（绝不静默吞掉）。
    /// </para>
    /// <para>
    /// <paramref name="overwrite"/> 为 <c>true</c> 时：写完新档再移除**同名的另一格式**文件
    /// （契约十 §5）。
    /// </para>
    /// </remarks>
    /// <param name="state">要落盘的状态。</param>
    /// <param name="slot">档名 + 格式。</param>
    /// <param name="overwrite">是否确认覆盖同名档。</param>
    void Save(GameState state, SaveSlot slot, bool overwrite);

    /// <summary>从该槽位读档，返回一个**新建**的状态。</summary>
    /// <remarks>
    /// 判定次序固定为八步（存在 → 头可读 → 剩余字节足量 → 版本相符 → 主体可解析 →
    /// 必需成员齐备 → 逐字段复原 → 九条一致性校验），失败按类抛
    /// <see cref="SaveFileException"/>（契约十 §6）。版本闸门 MUST 在任何主体解析**之前**生效。
    /// </remarks>
    /// <param name="slot">档名 + 格式。</param>
    /// <returns>复原出的新状态。</returns>
    GameState Load(SaveSlot slot);

    /// <summary>改名：主文件与备份一起改为新档名。</summary>
    /// <remarks>
    /// 主文件是**纯改名**（字节不变 ⇒ 存档标识与游戏内容全不变，含 <c>Family.Name</c>）；
    /// 备份副本一并改为「新档名 + 同扩展名」。备份改名失败不得损坏主文件，
    /// 且必须以 <see cref="SaveAccessException"/> 家族浮出、MUST NOT 静默（契约九 §6）。
    /// </remarks>
    /// <param name="slot">原槽位。</param>
    /// <param name="newName">新档名。</param>
    /// <returns>新槽位（格式不变）。</returns>
    SaveSlot Rename(SaveSlot slot, string newName);

    /// <summary>为当前档生成一份备份（覆盖式，永远只留一份）。</summary>
    /// <remarks>
    /// 落点为 <c>&lt;根&gt;/backups/&lt;档名&gt;.&lt;与主文件相同的扩展名&gt;</c>，
    /// 经原子写完成；MUST NOT 按时间戳追加、MUST NOT 保留多份。备份是**一份可读的完整存档**
    /// （回滚 = 把备份拷回主文件）。备份失败不得影响已写好的主文件，且 MUST NOT 静默
    /// （契约九 §7）。备份时机由契约十一决定。
    /// </remarks>
    /// <param name="slot">档名 + 格式。</param>
    void Backup(SaveSlot slot);
}
