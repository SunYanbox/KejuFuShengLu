namespace KFL.Infrastructure.Achievements;

/// <summary>
/// 全局面落盘的结果（008-achievements-extinction；契约二十三「条款 9」）。
/// </summary>
/// <remarks>
/// <para>
/// 与存档本体的失败语义**刻意不同**（契约二十三「条款 22」）：<c>ISaveService.Save</c> 失败
/// **上抛**且不回滚内存状态（落的是用户数据，调用方必须知道）；本结果表达的失败**不上抛**，
/// 由 <c>TurnRunner</c> 把待落盘队列留在档内、下次回合重试（落的是元进度，FR-024 明令
/// 「MUST NOT 阻断回合推进与存档」）。
/// </para>
/// <para>
/// <see cref="QuarantinePath"/> 只在「落盘时发现旧文件已损坏并被隔离」时非空——
/// 那是**成功路径上的附带诊断**，与 <see cref="FailureReason"/> 不互斥。
/// </para>
/// </remarks>
public sealed record AchievementFlushResult
{
    /// <summary>本次落盘是否成功（全量合并后的档案已原子替换到位）。</summary>
    public required bool IsSuccess { get; init; }

    /// <summary>失败原因（目录不可写 / 文件被占用 / 序列化失败……）；成功为 <c>null</c>。</summary>
    public string? FailureReason { get; init; }

    /// <summary>落盘时发现旧文件损坏而被隔离到的路径；未发生隔离为 <c>null</c>。</summary>
    public string? QuarantinePath { get; init; }

    /// <summary>成功。</summary>
    /// <param name="quarantinePath">若旧文件损坏且已隔离，隔离后的路径。</param>
    /// <returns>成功结果。</returns>
    public static AchievementFlushResult Success(string? quarantinePath = null) => new()
    {
        IsSuccess = true,
        QuarantinePath = quarantinePath,
    };

    /// <summary>失败（**不抛**；调用方保留队列待重试）。</summary>
    /// <param name="reason">原因。</param>
    /// <param name="quarantinePath">若失败前已隔离旧文件，隔离后的路径。</param>
    /// <returns>失败结果。</returns>
    public static AchievementFlushResult Failure(string reason, string? quarantinePath = null) => new()
    {
        IsSuccess = false,
        FailureReason = reason,
        QuarantinePath = quarantinePath,
    };
}
