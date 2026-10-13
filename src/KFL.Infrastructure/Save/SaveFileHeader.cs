namespace KFL.Infrastructure.Save;

/// <summary>
/// 存档文件**头**记录：恰含三个成员，次序固定（004-save-system；契约十 §2）。
/// </summary>
/// <remarks>
/// <para>
/// 头是**手写**的，不是 DTO：二进制为 <c>fixarray(3)</c>（起手字节 <c>0x93</c>）后跟
/// <c>FormatVersion</c>（int）、<c>PayloadLength</c>（int）、<c>SaveId</c>（string，<c>D</c> 格式恰 36 字符）；
/// JSON 为 <c>{"FormatVersion":…,"PayloadLength":…,"SaveId":"…"}</c>，且 <c>State</c> 之前恰有这三个成员。
/// </para>
/// <para>
/// <b>为什么手写</b>：头必须能被**独立**读出（版本闸门要在解析主体之前生效，截断判据也要
/// 在解析主体之前成立）；若头本身是 DTO，判定就得先完整解析主体，闸门形同虚设。
/// </para>
/// <para>
/// <see cref="PayloadLength"/> **只用于截断检测**，MUST NOT 被当作校验和
/// （它不校验内容是否被篡改，只回答「字节够不够」）。
/// </para>
/// </remarks>
/// <param name="FormatVersion">文件格式版本；本版本恒为 2。</param>
/// <param name="PayloadLength">主体（payload）的字节数。</param>
/// <param name="SaveId">存档标识；MUST 等于主体里 <c>GameState.Id</c>。</param>
public readonly record struct SaveFileHeader(int FormatVersion, int PayloadLength, Guid SaveId);
