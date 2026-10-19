using KFL.Core.Entities;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save;

namespace KFL.Tests.Rules;

/// <summary>一次存档/备份调用的记录。</summary>
/// <param name="Kind">调用种类（<c>Save</c> / <c>Backup</c> / <c>Rename</c>）。</param>
/// <param name="Slot">调用时的当前档。</param>
/// <param name="Overwrite">是否覆盖。</param>
public readonly record struct SaveCall(string Kind, SaveSlot Slot, bool Overwrite);

/// <summary>
/// 存档服务的**内存替身**（004-save-system；契约十一 §5 的 RecordingSaveService）。
/// </summary>
/// <remarks>
/// <para>
/// 节奏断言的对象是「第几回合调了存档、第几回合调了备份」——那是**调用序列**，不是盘上字节。
/// 用替身可以让这类断言与文件系统完全解耦（真文件的对应断言另有一组用例）。
/// </para>
/// <para>
/// <see cref="SaveFailures"/> 是一个队列：入队即成「下一次存档抛这个错」，用于验证
/// Q6 的失败语义（不备份、不回滚、下次写下完整状态）。
/// </para>
/// </remarks>
public sealed class RecordingSaveService : ISaveService
{
    private readonly Dictionary<string, GameState> _states = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>按发生次序记录的调用。</summary>
    public List<SaveCall> Calls { get; } = [];

    /// <summary>下一次存档要抛的异常队列。</summary>
    public Queue<Exception> SaveFailures { get; } = new();

    /// <summary>最后一次成功存档的状态。</summary>
    public GameState? LastSavedState => _lastSavedState;

    /// <summary>存档调用次数。</summary>
    public int SaveCount => Calls.Count(call => call.Kind == "Save");

    /// <summary>备份调用次数。</summary>
    public int BackupCount => Calls.Count(call => call.Kind == "Backup");

    /// <inheritdoc />
    public string RootDirectory => "(内存替身)";

    private GameState? _lastSavedState;

    /// <inheritdoc />
    public bool Exists(SaveSlot slot) => _states.ContainsKey(slot.Name);

    /// <inheritdoc />
    public SaveSlot? FindBySaveId(Guid saveId) => null;

    /// <inheritdoc />
    public string FilePathOf(SaveSlot slot) => slot.Name;

    /// <inheritdoc />
    public void Save(GameState state, SaveSlot slot, bool overwrite)
    {
        Calls.Add(new SaveCall("Save", slot, overwrite));

        if (SaveFailures.Count > 0)
        {
            throw SaveFailures.Dequeue();
        }

        _states[slot.Name] = state;
        _lastSavedState = state;
    }

    /// <inheritdoc />
    public GameState Load(SaveSlot slot) =>
        _states.TryGetValue(slot.Name, out var state)
            ? state
            : throw new SaveFileException(SaveFileErrorKind.Missing, $"替身里没有这个档：{slot.Name}");

    /// <inheritdoc />
    public SaveSlot Rename(SaveSlot slot, string newName)
    {
        Calls.Add(new SaveCall("Rename", slot, false));
        return slot with { Name = newName };
    }

    /// <inheritdoc />
    public void Backup(SaveSlot slot) => Calls.Add(new SaveCall("Backup", slot, false));

    /// <inheritdoc />
    public IReadOnlyList<SaveSlot> ListBackups() => [];

    /// <inheritdoc />
    public bool Delete(SaveSlot slot) => _states.Remove(slot.Name);

    /// <inheritdoc />
    public bool DeleteBackup(SaveSlot slot) => true;

    /// <inheritdoc />
    public GameState LoadBackup(SaveSlot slot) => Load(slot);

    /// <inheritdoc />
    public GameState SaveAsFork(GameState state, SaveSlot slot)
    {
        if (_states.ContainsKey(slot.Name))
        {
            throw new SaveNameConflictException($"存档名已存在：{slot.Name}");
        }

        _states[slot.Name] = state;
        _lastSavedState = state;
        Calls.Add(new SaveCall("Save", slot, false));
        return state;
    }
}
