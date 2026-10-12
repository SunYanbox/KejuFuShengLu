using KFL.Infrastructure.Abstractions;

namespace KFL.Infrastructure.Save.Serialization;

/// <summary>
/// 一种存档**物理格式**的编解码接缝（004-save-system；data-model §4，契约十 §2/§3）。
/// </summary>
/// <remarks>
/// <para>
/// 抽出这个接缝的理由不是「将来可能有第三种格式」，而是<b>可断言</b>：契约十 §5 要求
/// 「两个格式信息集等价」，若只有一个实现加一堆 <c>if (format == …)</c>，这条等价性就只能靠
/// 人工比对；有接缝才能对同一份状态同时跑两个实现逐字段比。
/// </para>
/// <para>
/// <b>头由实现自己算</b>：<see cref="Write"/> 收的是存档标识而不是完整的
/// <c>SaveFileHeader</c>——<c>PayloadLength</c> 恰是「主体有多少字节」，让调用方先构造头
/// 就会引入「声明长度与实际字节不符」的可能，而那正是截断判据赖以成立的东西。
/// </para>
/// <para>
/// <b>读只管到 DTO</b>：<see cref="Read"/> 不做字段级复原、不做九条一致性校验——那是
/// <see cref="SaveStateMapper"/> 与 <see cref="SaveStateValidator"/> 的职责，由
/// <c>SaveService.Load</c> 按契约十 §6 的次序调用。
/// </para>
/// </remarks>
public interface ISaveFormat
{
    /// <summary>本实现对应的格式。</summary>
    SaveFormat Format { get; }

    /// <summary>把主体编成一份**完整**的存档文件字节（头 + 主体）。</summary>
    /// <param name="saveId">存档标识；写入头并与主体标识相等。</param>
    /// <param name="payload">主体。</param>
    /// <returns>完整文件字节。</returns>
    byte[] Write(Guid saveId, SaveStateDto payload);

    /// <summary>
    /// 只读头记录，MUST NOT 解析主体（契约九 §5：<c>FindBySaveId</c> 的 O(1) 探针）。
    /// </summary>
    /// <param name="fileBytes">整个文件的字节。</param>
    /// <returns>头记录。</returns>
    /// <exception cref="SaveFileException">头读不出（分类恒为 <see cref="SaveFileErrorKind.Malformed"/>）。</exception>
    SaveFileHeader ReadHeader(ReadOnlySpan<byte> fileBytes);

    /// <summary>读出整份文件（头 + 主体）。</summary>
    /// <param name="fileBytes">整个文件的字节。</param>
    /// <returns>头记录与主体 DTO。</returns>
    /// <exception cref="SaveFileException">
    /// 按契约十 §6 的次序分类：头读不出 ⇒ <see cref="SaveFileErrorKind.Malformed"/>；
    /// 剩余字节不足 ⇒ <see cref="SaveFileErrorKind.Truncated"/>；
    /// 版本不符 ⇒ <see cref="SaveFileErrorKind.UnsupportedVersion"/>；
    /// 主体解析失败 ⇒ <see cref="SaveFileErrorKind.Malformed"/>；
    /// 必需成员缺失 ⇒ <see cref="SaveFileErrorKind.InvalidStructure"/>。
    /// </exception>
    (SaveFileHeader Header, SaveStateDto Payload) Read(ReadOnlySpan<byte> fileBytes);
}
