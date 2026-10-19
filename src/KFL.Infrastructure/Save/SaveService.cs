using System.IO.Compression;
using KFL.Core.Entities;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save.Serialization;

namespace KFL.Infrastructure.Save;

/// <summary>
/// 存档服务的唯一实现（004-save-system；契约九）。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么在 Infrastructure 而不是 Rules</b>：它只做「把一份 <see cref="GameState"/> 落成文件」
/// 与「把文件读回一份 <see cref="GameState"/>」，不含任何**何时存**的决策——那是
/// <c>KFL.Rules.Save.TurnRunner</c> 与 <c>SaveCadencePolicy</c> 的事（契约十一）。放在
/// Infrastructure 的另一个理由是 UI 轨 U1/U2（退出存档、读档界面）应当在没有规则层的
/// 前提下也能拿它读档。
/// </para>
/// <para>
/// <b>校验次序是契约的一部分</b>（契约九 §1 条款 6）：每个公开方法都先把**全部**校验做完，
/// 再动文件系统的第一个字节。于是「非法档名」「同名冲突」「同标识冲突」这三类失败
/// **结构上**不可能留下半成品，不依赖 try/catch 回滚（FR-013）。
/// </para>
/// <para>
/// <b>不读钟</b>：本类型不落盘任何写入时间戳，也不读系统墙钟（契约九 §1 条款 3，
/// 结清 001 R-04）。备份文件名固定为「存档名 + 同扩展名」，绝不追加时间戳（契约九 §7）。
/// </para>
/// <para>
/// <b>当前档（档名 + 格式）不是这里的状态</b>：它由调用方按次传入（Q4）。本类型不记得
/// 「上次存的是哪个档」，也不把它写进 <see cref="GameState"/>（契约九 §1 条款 7）。
/// </para>
/// </remarks>
public sealed class SaveService : ISaveService
{
    private readonly ISaveFormat _binaryFormat = new MessagePackSaveFormat();
    private readonly ISaveFormat _jsonFormat = new JsonSaveFormat();
    private readonly AtomicFileWriter _writer;

    /// <summary>
    /// 只读头探针的读取上界（字节）。头记录（三个成员 + 主体之前的固定外壳）远小于此；
    /// 上界的作用是让探针**不随档的大小增长**（契约九 §5 条款 3：MUST NOT 读主体）。
    /// </summary>
    private const int HeaderProbeBytes = 4096;

    /// <summary>用默认选项建服务：存档根 = 可执行文件旁的 <c>saves/</c>，默认格式 = 二进制。</summary>
    public SaveService()
        : this(new SaveOptions())
    {
    }

    /// <summary>用给定选项建服务。</summary>
    /// <param name="options">存档选项。</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> 为 <c>null</c>。</exception>
    public SaveService(SaveOptions options)
        : this(options, new AtomicFileWriter())
    {
    }

    /// <summary>用给定选项与原子写入器建服务（写入器参数存在的唯一目的是注入测试钩子）。</summary>
    /// <param name="options">存档选项。</param>
    /// <param name="writer">原子写入器。</param>
    /// <exception cref="ArgumentNullException">任一参数为 <c>null</c>。</exception>
    public SaveService(SaveOptions options, AtomicFileWriter writer)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(writer);

        _writer = writer;
        RootDirectory = string.IsNullOrWhiteSpace(options.RootDirectory)
            ? SaveCatalog.DefaultRootDirectory()
            : options.RootDirectory;
    }

    /// <inheritdoc />
    public string RootDirectory { get; }

    /// <inheritdoc />
    public bool Exists(SaveSlot slot)
    {
        var name = SaveName.Validate(slot.Name);

        // 「存在」与格式无关（契约九 §5）：同名只要有一个格式的档在，就算存在。
        return FindSameNameFiles(name).Count > 0;
    }

    /// <inheritdoc />
    public SaveSlot? FindBySaveId(Guid saveId)
    {
        var matches = FindAllBySaveId(saveId);

        return matches.Count > 0 ? matches[0] : null;
    }

    /// <summary>列出根下**全部**指向该标识的存档（契约九 §5 条款 3/4 的唯一性判定要看全部命中）。</summary>
    /// <param name="saveId">存档标识。</param>
    /// <returns>命中项（无序；空标识 ⇒ 空表）。</returns>
    private List<SaveSlot> FindAllBySaveId(Guid saveId)
    {
        var matches = new List<SaveSlot>();

        if (saveId == Guid.Empty)
        {
            return matches;
        }

        foreach (var file in SaveCatalog.ListFiles(RootDirectory))
        {
            if (!TryReadHeader(file, out var header, out var format))
            {
                continue;   // 读不出头的档连「自己是谁」都说不清，不参与判定
            }

            if (header.SaveId == saveId)
            {
                matches.Add(new SaveSlot(SaveCatalog.FileNameStemOf(file), format));
            }
        }

        return matches;
    }

    /// <inheritdoc />
    public string FilePathOf(SaveSlot slot)
    {
        var name = SaveName.Validate(slot.Name);

        return SaveCatalog.Combine(RootDirectory, name + SaveFileFormat.ExtensionOf(slot.Format));
    }

    /// <inheritdoc />
    public void Save(GameState state, SaveSlot slot, bool overwrite)
    {
        ArgumentNullException.ThrowIfNull(state);

        // ① 档名口径先于一切：非法档名 MUST NOT 触碰文件系统（契约九 §2、§4 第 2 条）。
        var name = SaveName.Validate(slot.Name);
        var extension = SaveFileFormat.ExtensionOf(slot.Format);

        // ② 同名占用：**跨格式**、大小写不敏感（契约九 §4 第 1.1 条、§5）。
        var sameNameFiles = FindSameNameFiles(name);
        if (!overwrite && sameNameFiles.Count > 0)
        {
            throw new SaveNameConflictException($"存档名已存在：{name}");
        }

        // ③ 同一标识并存于**另一个文件名** ⇒ 一个字节都不写（契约九 §2、§5；结清 001 R-03）。
        //    探测 MUST 看**全部**同标识文件：盘上出现两个同 SaveId 的档是外部可见的坏状态，
        //    只看第一个命中会让结果依赖目录枚举次序（契约九 §5 条款 4）。
        foreach (var existing in FindAllBySaveId(state.Id))
        {
            if (!SaveName.IsSameName(existing.Name, name))
            {
                throw new SaveIdentityConflictException(
                    $"存档标识已存在于另一个档名下：{state.Id}（{existing.Name}）");
            }
        }

        // ④ 序列化先于落盘：这一步失败时旧档仍完好（FR-013）。
        var payload = SaveStateMapper.ToDto(state);
        var fileBytes = FormatOf(slot.Format).Write(state.Id, payload);

        // ⑤ 目录可用性（到这一步才第一次动文件系统）。
        SaveCatalog.EnsureDirectory(RootDirectory);

        // ⑥ 原子替换：目标恒为「旧的完整档」或「新的完整档」，绝无半成品。
        _writer.Write(SaveCatalog.Combine(RootDirectory, name + extension), fileBytes);

        // ⑦ 写完**之后**才清同名异构档与该存档的旧格式备份（契约九 §5、§7 第 7 条）。
        RemoveStaleSiblings(name, extension, state.Id);
    }

    /// <inheritdoc />
    public GameState Load(SaveSlot slot)
    {
        var name = SaveName.Validate(slot.Name);
        var path = SaveCatalog.Combine(RootDirectory, name + SaveFileFormat.ExtensionOf(slot.Format));

        // ① 文件存在吗 ⇒ 否 ⇒ Missing。
        //    判据取「读一下」而不是「先问存不存在」：目录不可读时存在性检查只会答「不存在」，
        //    把访问失败误报成缺档（契约九 §2、§4 条款 5）。
        var fileBytes = ReadFileOrThrow(path, name);

        return ReadState(fileBytes, slot);
    }

    /// <inheritdoc />
    public IReadOnlyList<SaveSlot> ListBackups()
    {
        var slots = new List<SaveSlot>();

        foreach (var file in SaveCatalog.ListFiles(SaveCatalog.BackupsDirectoryOf(RootDirectory)))
        {
            if (!TryDescribeBackup(file, out var stem, out var format))
            {
                continue;
            }

            if (!slots.Exists(slot => slot.Format == format && SaveName.IsSameName(slot.Name, stem)))
            {
                slots.Add(new SaveSlot(stem, format));
            }
        }

        slots.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
        return slots.AsReadOnly();
    }

    /// <inheritdoc />
    public GameState LoadBackup(SaveSlot slot)
    {
        var name = SaveName.Validate(slot.Name);
        var extension = SaveFileFormat.ExtensionOf(slot.Format);
        var path = SaveCatalog.Combine(
            SaveCatalog.BackupsDirectoryOf(RootDirectory), name + extension + BackupSuffix);

        var zipBytes = ReadFileOrThrow(path, name + extension + BackupSuffix);

        return ReadState(Unzip(zipBytes), slot);
    }

    /// <inheritdoc />
    public bool Delete(SaveSlot slot)
    {
        var name = SaveName.Validate(slot.Name);
        var extension = SaveFileFormat.ExtensionOf(slot.Format);

        return TryDeleteFile(SaveCatalog.Combine(RootDirectory, name + extension), name);
    }

    /// <inheritdoc />
    public bool DeleteBackup(SaveSlot slot)
    {
        var name = SaveName.Validate(slot.Name);
        var extension = SaveFileFormat.ExtensionOf(slot.Format);
        var path = SaveCatalog.Combine(
            SaveCatalog.BackupsDirectoryOf(RootDirectory), name + extension + BackupSuffix);

        return TryDeleteFile(path, name + extension + BackupSuffix);
    }

    /// <inheritdoc />
    public GameState SaveAsFork(GameState state, SaveSlot slot)
    {
        ArgumentNullException.ThrowIfNull(state);

        var name = SaveName.Validate(slot.Name);
        var extension = SaveFileFormat.ExtensionOf(slot.Format);

        if (FindSameNameFiles(name).Count > 0)
        {
            throw new SaveNameConflictException($"存档名已存在：{name}");
        }

        var payload = SaveStateMapper.ToDto(state);

        // 另存为 MUST 换一个存档标识：同标识并存于两个档名是契约九 §5 明令禁止的外部坏状态。
        payload.Id = Guid.NewGuid(); // arch-guard:allow 契约九 §5：另存为必须铸造新存档标识（基础设施层的唯一铸造点）

        var fileBytes = FormatOf(slot.Format).Write(payload.Id, payload);
        SaveCatalog.EnsureDirectory(RootDirectory);
        _writer.Write(SaveCatalog.Combine(RootDirectory, name + extension), fileBytes);
        RemoveStaleSiblings(name, extension, payload.Id);

        // 从盘上读回：调用方拿到的是**独立**的新标识状态，与入参不发生共享。
        return Load(new SaveSlot(name, slot.Format));
    }

    /// <summary>删除一个文件；失败按契约九 §2 的分类浮出（绝不静默）。</summary>
    private static bool TryDeleteFile(string path, string displayName)
    {
        try
        {
            return SaveCatalog.DeleteFile(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new SaveAccessException($"删除存档失败：{displayName}（{ex.Message}）", ex);
        }
    }

    /// <summary>读整个文件；缺文件 / 不可读两类失败按契约九 §2 分类抛出。</summary>
    private static byte[] ReadFileOrThrow(string path, string displayName)
    {
        try
        {
            return SaveCatalog.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new SaveFileException(SaveFileErrorKind.Missing, $"存档不存在：{displayName}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new SaveAccessException($"存档不可读：{path}", ex);
        }
    }

    /// <summary>字节 → 状态（Load / LoadBackup 共用的八步校验）。</summary>
    private GameState ReadState(byte[] fileBytes, SaveSlot slot)
    {
        // ②③④⑤⑥ 头 / 截断 / 版本闸门 / 主体解析 / 必需成员，全部由格式实现按契约十 §6 分类。
        var (header, payload) = FormatOf(slot.Format).Read(fileBytes);

        // ⑦ 逐字段复原（Core 自身的交叉校验在这里把不一致顶成 InvalidStructure）。
        var state = SaveStateMapper.ToDomain(payload);

        // ⑦′ 读档自愈（规格书 §17 裁决 2026-10-19）：历史缺陷可能留下两处生命周期簿记——
        //     ①「过期提议」指向的成员此后经其他路径成了婚（第 13 条）；
        //     ②「已申请本年生育的夫妇」此后离异 / 丧偶（第 14 条，整档读不进来）。
        //     两者都是可安全丢弃的当月簿记，故在此清空后再校验；其余不一致一律照旧拒绝。
        SaveStateRepair.ClearStaleProposal(state);
        SaveStateRepair.ClearStaleBirthBookkeeping(state);

        // ⑧ 头标识一致 + 十二条一致性校验。
        SaveStateValidator.ValidateIdentity(header, state);
        SaveStateValidator.Validate(state);

        // INV11：任一步失败都不得改文件、不得返回半成品、不得污染调用方状态——异常即出口。
        return state;
    }

    /// <inheritdoc />
    public SaveSlot Rename(SaveSlot slot, string newName)
    {
        var currentName = SaveName.Validate(slot.Name);
        var targetName = SaveName.Validate(newName);
        var extension = SaveFileFormat.ExtensionOf(slot.Format);
        var sourcePath = SaveCatalog.Combine(RootDirectory, currentName + extension);

        if (!SaveCatalog.FileExists(sourcePath))
        {
            throw new SaveFileException(SaveFileErrorKind.Missing, $"存档不存在：{currentName}");
        }

        if (string.Equals(currentName, targetName, StringComparison.Ordinal))
        {
            return slot;   // 新旧名逐字符相同 ⇒ 无事发生
        }

        // 目标名不得被别的文件占用（跨格式、大小写不敏感）；源文件自己不算冲突。
        foreach (var file in FindSameNameFiles(targetName))
        {
            if (string.Equals(file, sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            throw new SaveNameConflictException($"存档名已存在：{targetName}");
        }

        // 纯移动 ⇒ 字节不变 ⇒ SaveId 与全部内容（含 Family.Name）不变（契约九 §6；FR-012）。
        _writer.Move(sourcePath, SaveCatalog.Combine(RootDirectory, targetName + extension));

        // 备份跟着改名；失败时主档已经就位、未被损坏，且以 SaveAccessException 家族浮出。
        RenameBackup(currentName, targetName, extension);

        return new SaveSlot(targetName, slot.Format);
    }

    /// <inheritdoc />
    public void Backup(SaveSlot slot)
    {
        var name = SaveName.Validate(slot.Name);
        var extension = SaveFileFormat.ExtensionOf(slot.Format);
        var sourcePath = SaveCatalog.Combine(RootDirectory, name + extension);

        if (!SaveCatalog.FileExists(sourcePath))
        {
            throw new SaveFileException(SaveFileErrorKind.Missing, $"存档不存在：{name}");
        }

        var backupsDirectory = SaveCatalog.BackupsDirectoryOf(RootDirectory);
        SaveCatalog.EnsureDirectory(backupsDirectory);

        // 覆盖式、文件名 = 存档名 + 与当前档相同扩展名 + .zip、每档只留一份、绝不追加时间戳
        // （契约九 §7；2026-10-19 需求 14 起备份一律压缩）。
        var bytes = SaveCatalog.ReadAllBytes(sourcePath);
        _writer.Write(
            SaveCatalog.Combine(backupsDirectory, name + extension + BackupSuffix),
            Zip(name + extension, bytes));

        // 清掉 backups/ 下指向**同一标识**的其它备份（契约九 §7 第 7 条）：这条不变式对
        // 「本次保存 / 本次备份 / 本次格式切换」一律成立，故与 Save 复用同一个清理点。
        // 主档读不出头（截断/损坏）时不猜标识，只做与标识无关的那半（清理留待下次成功写入）。
        var saveId = TryReadHeader(sourcePath, out var header, out _) ? header.SaveId : (Guid?)null;
        RemoveStaleBackups(name, extension, saveId);
    }

    private ISaveFormat FormatOf(SaveFormat format) => format switch
    {
        SaveFormat.Binary => _binaryFormat,
        SaveFormat.Json => _jsonFormat,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "未知的存档格式。"),
    };

    private List<string> FindSameNameFiles(string name)
    {
        var matches = new List<string>();

        foreach (var file in SaveCatalog.ListFiles(RootDirectory))
        {
            if (TryDescribe(file, out var stem, out _) && SaveName.IsSameName(stem, name))
            {
                matches.Add(file);
            }
        }

        return matches;
    }

    private static bool TryDescribe(string path, out string stem, out SaveFormat format)
    {
        stem = string.Empty;

        if (!SaveFileFormat.TryFormatOfExtension(SaveCatalog.ExtensionOf(path), out format))
        {
            return false;
        }

        stem = SaveCatalog.FileNameStemOf(path);
        return true;
    }

    private bool TryReadHeader(string path, out SaveFileHeader header, out SaveFormat format)
    {
        header = default;

        if (!TryDescribe(path, out _, out format))
        {
            return false;
        }

        try
        {
            // 只读头（契约九 §5 条款 3）：FindBySaveId 是 O(1) 探针，MUST NOT 解析主体。
            // 封顶前缀读（而非整档）是这条的落地：头记录远小于 HeaderProbeBytes。
            header = FormatOf(format).ReadHeader(SaveCatalog.ReadBytes(path, HeaderProbeBytes));
            return true;
        }
        catch (SaveFileException)
        {
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void RenameBackup(string currentName, string newName, string extension)
    {
        var backupsDirectory = SaveCatalog.BackupsDirectoryOf(RootDirectory);
        var sourcePath = SaveCatalog.Combine(backupsDirectory, currentName + extension + BackupSuffix);

        if (!SaveCatalog.FileExists(sourcePath))
        {
            return;   // 从没备份过 ⇒ 没有要跟着改名的东西
        }

        // 备份改名 MUST NOT 损坏主档，且 MUST NOT 被吞（契约九 §6 第 5 条）。
        _writer.Move(sourcePath, SaveCatalog.Combine(backupsDirectory, newName + extension + BackupSuffix));
    }

    /// <summary>备份文件名 = 存档名 + 原扩展名 + <c>.zip</c>（2026-10-19 需求 14）。</summary>
    private const string BackupSuffix = ".zip";

    /// <summary>把一份存档字节压进单条目 zip（条目名 = 原文件名，解压即得可读存档）。</summary>
    private static byte[] Zip(string entryName, byte[] payload)
    {
        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);

            using var entryStream = entry.Open();
            entryStream.Write(payload, 0, payload.Length);
        }

        return buffer.ToArray();
    }

    /// <summary>解开单条目 zip，取回内部存档字节。</summary>
    private static byte[] Unzip(byte[] zipBytes)
    {
        using var buffer = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(buffer, ZipArchiveMode.Read);

        if (archive.Entries.Count != 1)
        {
            throw new SaveFileException(
                SaveFileErrorKind.Malformed, $"备份压缩包条目数异常：{archive.Entries.Count}（应为 1）。");
        }

        using var entryStream = archive.Entries[0].Open();
        using var output = new MemoryStream();
        entryStream.CopyTo(output);

        return output.ToArray();
    }

    /// <summary>
    /// 备份文件名 → 「档名 + 格式」。备份是 <c>&lt;档名&gt;&lt;扩展名&gt;.zip</c>，
    /// 故先剥掉 <c>.zip</c> 再按普通存档文件名解析。
    /// </summary>
    private static bool TryDescribeBackup(string path, out string stem, out SaveFormat format)
    {
        stem = string.Empty;
        format = default;

        if (!string.Equals(SaveCatalog.ExtensionOf(path), BackupSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var inner = SaveCatalog.FileNameStemOf(path);   // 「狄家.dat.zip」→「狄家.dat」

        return SaveFileFormat.TryFormatOfExtension(SaveCatalog.ExtensionOf(inner), out format)
            && (stem = SaveCatalog.FileNameStemOf(inner)) is { Length: > 0 };
    }

    private void RemoveStaleSiblings(string name, string extension, Guid saveId)
    {
        RemoveStalePrimary(name, extension);
        RemoveStaleBackups(name, extension, saveId);
    }

    private void RemoveStalePrimary(string name, string extension)
    {
        // 主目录下的同名异构档（格式切换后留下的旧格式主档）。
        foreach (var file in SaveCatalog.ListFiles(RootDirectory))
        {
            if (!TryDescribe(file, out var stem, out var format) || !SaveName.IsSameName(stem, name))
            {
                continue;
            }

            if (!string.Equals(SaveFileFormat.ExtensionOf(format), extension, StringComparison.OrdinalIgnoreCase))
            {
                _writer.Delete(file);
            }
        }
    }

    /// <summary>清 backups/ 下的同名异构档与指向同一标识的其它备份（契约九 §7 第 7 条）。</summary>
    /// <param name="name">当前档名。</param>
    /// <param name="extension">当前档扩展名。</param>
    /// <param name="saveId">当前档标识；<c>null</c> 即读不出头，此时跳过「同标识」那半。</param>
    private void RemoveStaleBackups(string name, string extension, Guid? saveId)
    {
        var backupsDirectory = SaveCatalog.BackupsDirectoryOf(RootDirectory);
        foreach (var file in SaveCatalog.ListFiles(backupsDirectory))
        {
            if (!TryDescribeBackup(file, out var stem, out var format))
            {
                continue;
            }

            var sameName = SaveName.IsSameName(stem, name);
            if (sameName)
            {
                if (!string.Equals(SaveFileFormat.ExtensionOf(format), extension, StringComparison.OrdinalIgnoreCase))
                {
                    _writer.Delete(file);   // 旧格式备份
                }

                continue;   // 当前格备份：保留，备份口径是「每档一份，覆盖式」
            }

            if (saveId is { } id
                && TryReadBackupHeader(file, format, out var header)
                && header.SaveId == id)
            {
                _writer.Delete(file);   // 同标识的其它备份
            }
        }
    }

    /// <summary>从压缩备份里读头（备份是 zip，故先解压再走格式实现的只读头探针）。</summary>
    private bool TryReadBackupHeader(string path, SaveFormat format, out SaveFileHeader header)
    {
        header = default;

        try
        {
            header = FormatOf(format).ReadHeader(Unzip(SaveCatalog.ReadAllBytes(path)));
            return true;
        }
        catch (SaveFileException)
        {
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return false;
        }
    }
}
