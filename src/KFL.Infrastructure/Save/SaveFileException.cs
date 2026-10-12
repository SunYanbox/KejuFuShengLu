namespace KFL.Infrastructure.Save;

/// <summary>
/// 存档读取失败的**分类**（004-save-system；契约九 §2，SC-007 的可识别信号）。
/// </summary>
/// <remarks>
/// 五个值两两互不相同，且 MUST NOT 依赖异常消息文本来区分——判据是「先分类、后解析」
/// （契约十 §6 的八步次序）。
/// </remarks>
public enum SaveFileErrorKind
{
    /// <summary>目标文件不存在。</summary>
    Missing,

    /// <summary>文件被截断：剩余字节少于头声明的 <c>PayloadLength</c>。</summary>
    Truncated,

    /// <summary>字节损坏：头读不出，或主体以任何其它方式解析失败（规格书未列举的兜底类）。</summary>
    Malformed,

    /// <summary>版本不符（<c>FormatVersion != 1</c>）：读不懂就明说，MUST NOT 猜读（FR-015）。</summary>
    UnsupportedVersion,

    /// <summary>结构不符：主体能解析，但缺本版本必需成员，或违反一致性校验。</summary>
    InvalidStructure,
}

/// <summary>
/// 存档文件读取失败（004-save-system；契约九 §2，R-07）。
/// </summary>
/// <remarks>
/// 不带 <see cref="SaveFileErrorKind"/> 的构造重载一律归为
/// <see cref="SaveFileErrorKind.Malformed"/>——它是「字节损坏」这一类兜底语义，
/// 也正是无分类信息时唯一诚实的归类。
/// </remarks>
public sealed class SaveFileException : Exception
{
    /// <summary>构造一个归类为 <see cref="SaveFileErrorKind.Malformed"/> 的失败。</summary>
    public SaveFileException()
        : this(SaveFileErrorKind.Malformed, "存档文件读取失败。")
    {
    }

    /// <summary>构造一个归类为 <see cref="SaveFileErrorKind.Malformed"/> 的失败。</summary>
    /// <param name="message">说明。</param>
    public SaveFileException(string message)
        : this(SaveFileErrorKind.Malformed, message)
    {
    }

    /// <summary>构造一个归类为 <see cref="SaveFileErrorKind.Malformed"/> 的失败。</summary>
    /// <param name="message">说明。</param>
    /// <param name="innerException">内因。</param>
    public SaveFileException(string message, Exception innerException)
        : this(SaveFileErrorKind.Malformed, message, innerException)
    {
    }

    /// <summary>按给定分类构造失败。</summary>
    /// <param name="kind">失败分类。</param>
    /// <param name="message">说明。</param>
    public SaveFileException(SaveFileErrorKind kind, string message)
        : base(message) => Kind = kind;

    /// <summary>按给定分类构造失败。</summary>
    /// <param name="kind">失败分类。</param>
    /// <param name="message">说明。</param>
    /// <param name="innerException">内因。</param>
    public SaveFileException(SaveFileErrorKind kind, string message, Exception innerException)
        : base(message, innerException) => Kind = kind;

    /// <summary>失败分类。</summary>
    public SaveFileErrorKind Kind { get; }
}

/// <summary>档名冲突：同名档已存在，而调用方**未确认覆盖**（契约九 §2）。</summary>
public sealed class SaveNameConflictException : Exception
{
    /// <summary>构造失败。</summary>
    public SaveNameConflictException()
        : this("同名存档已存在，且未确认覆盖。")
    {
    }

    /// <summary>构造失败。</summary>
    /// <param name="message">说明。</param>
    public SaveNameConflictException(string message)
        : base(message)
    {
    }

    /// <summary>构造失败。</summary>
    /// <param name="message">说明。</param>
    /// <param name="innerException">内因。</param>
    public SaveNameConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>存档标识冲突：同一根目录下，另一个**文件名**已携带同一存档标识（契约九 §2/§5）。</summary>
public sealed class SaveIdentityConflictException : Exception
{
    /// <summary>构造失败。</summary>
    public SaveIdentityConflictException()
        : this("同一存档标识已存在于另一个文件名下。")
    {
    }

    /// <summary>构造失败。</summary>
    /// <param name="message">说明。</param>
    public SaveIdentityConflictException(string message)
        : base(message)
    {
    }

    /// <summary>构造失败。</summary>
    /// <param name="message">说明。</param>
    /// <param name="innerException">内因。</param>
    public SaveIdentityConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>存档目录不可用：建不出来或写不进去（契约九 §2；绝不静默吞掉）。</summary>
public sealed class SaveAccessException : Exception
{
    /// <summary>构造失败。</summary>
    public SaveAccessException()
        : this("存档目录不可用。")
    {
    }

    /// <summary>构造失败。</summary>
    /// <param name="message">说明。</param>
    public SaveAccessException(string message)
        : base(message)
    {
    }

    /// <summary>构造失败。</summary>
    /// <param name="message">说明。</param>
    /// <param name="innerException">内因。</param>
    public SaveAccessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
