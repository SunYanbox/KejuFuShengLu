# 契约二十三：成就全局持久化与存档形状

**特性**：`008-achievements-extinction`
**上游**：规格书 §14（:528-554）；`spec.md` FR-010~FR-025、FR-036、FR-037；契约二十二
**下游**：U1 徽章墙；`specs/004-save-system/contracts/save-format.md`（回写目标）
**状态**：活工件，MUST 与实现一致。

---

## §1 接口 `IAchievementStore`

**条款 1**：接口住在 `src/KFL.Infrastructure/Abstractions/IAchievementStore.cs`，是 `KFL.Core` ←
`KFL.Infrastructure` ← `KFL.Rules` 链条上的既有接缝风格（与 `ISaveService` 同层同款）。

```csharp
namespace KFL.Infrastructure.Abstractions;

public interface IAchievementStore
{
    /// 全局成就文件的绝对路径（恒为 <RootDirectory>/achievements.json）。
    string FilePath { get; }

    /// 只读当前全局面。文件不存在 ⇒ 空档案（非错误）。
    /// 文件损坏 ⇒ 隔离后返回空档案，Diagnostics 携带诊断（MUST NOT 抛）。
    AchievementArchive Read();

    /// 合并并原子替换。成功 ⇒ IsSuccess；失败 ⇒ 原因与诊断，MUST NOT 抛。
    AchievementFlushResult Flush(IReadOnlyList<PendingUnlock> unlocks, Guid sourceSaveId);
}
```

**条款 2**：`Read()` 与 `Flush(...)` MUST NOT 抛异常。全部可预期失败（目录不可写、文件被占用、
JSON 语法错、结构非法、版本过高）一律以返回值表达。理由：`Read` 在回合末判定之前被调用，
一个抛异常的 `Read` 会让「成就文件坏掉」升级成「游戏无法推进」，违反 FR-024 的精神。

**条款 3**：`AchievementStore` 的实现 MUST 复用 `src/KFL.Infrastructure/Save/AtomicFileWriter.cs`
（`Write(string, ReadOnlySpan<byte>)`，临时文件 → `Flush(flushToDisk: true)` → `File.Move(..., overwrite: true)`）
做原子替换；MUST NOT 直接 `File.WriteAllText`。

---

## §2 文件位置、格式与向后兼容

**条款 4**：文件恒为 `<ISaveService.RootDirectory>/achievements.json`（FR-017）。
`RootDirectory` 的默认值来自 `src/KFL.Infrastructure/Save/SaveCatalog.cs:143-144`，**本契约不另设根目录**。

**条款 5**：格式**恒为 JSON**，**不**提供 MessagePack 侧（`research.md` R-11）。
「跟随当前存档格式」是**禁止**的：同一个玩家的成就面 MUST 在两种存档格式下可互认。

```json
{
  "Version": 1,
  "Achievements": [
    { "Achievement": 44, "Difficulty": 2, "CompletedAt": { "Year": 17, "Month": 8 },
      "SourceSaveId": "5b1f…" }
  ],
  "Origins": [
    { "Origin": 0, "Milestone": 0, "GameDate": { "Year": 17, "Month": 8 },
      "SourceSaveId": "5b1f…" }
  ]
}
```

**条款 6（字段语义）**：

| 位置 | 语义 |
| --- | --- |
| `Version` | 本文件自身的版本，首版 = `1`。**不**与 `SaveFileFormat.Version` 联动 |
| `Achievements[].Achievement` | `AchievementId` 的底层整数 |
| `Achievements[].Difficulty` | `Difficulty` 的底层整数 |
| `Achievements[].CompletedAt` | **最早**达成年月（FR-018），两个 `int` |
| `Achievements[].SourceSaveId` | 最早那条记录的来源存档 UUID（FR-025） |
| `Origins[]` | 出身达成段（FR-020）：`Origin` + `Milestone` + 达成年月 + 来源 UUID |

**条款 7（向后兼容，FR-019）**：解析 MUST 满足
1. 未知属性忽略（**不得**因出现新键而失败）；
2. 缺失属性取默认值（缺失 `Achievements` ⇒ 空数组；缺失 `Version` ⇒ 视作 `1`）；
3. JSON 侧**MUST NOT** 给 `Origins` 或 `Achievements` 的元素标 `[JsonRequired]`
   （否则缺失元素键会被当成致命错误）；
4. `Version > 当前版本` ⇒ **不**失败，按未知键忽略语义继续（成就面是单调累积的元进度，
   高版本文件里的额外信息对旧实现无害）；这一条与存档本体的「等值闸门」**相反**，MUST 在 XML doc 里写明理由。

**条款 8**：`AchievementArchive` 的形状：

```csharp
public sealed record AchievementArchive
{
    public required int Version { get; init; }
    public required IReadOnlyList<ArchiveAchievementRecord> Achievements { get; init; }
    public required IReadOnlyList<OriginRecord> Origins { get; init; }
    public required AchievementArchiveDiagnostics Diagnostics { get; init; }   // 是否发生过隔离、原因
}

public readonly record struct ArchiveAchievementRecord(
    AchievementId Achievement, Difficulty Difficulty, GameDate CompletedAt, Guid SourceSaveId);

public readonly record struct OriginRecord(
    Origin Origin, OriginMilestone Milestone, GameDate GameDate, Guid SourceSaveId);

public readonly record struct AchievementArchiveDiagnostics(
    bool WasQuarantined, string? QuarantinePath, string? Reason);
```

**条款 9**：`AchievementFlushResult` 的形状：

```csharp
public sealed record AchievementFlushResult
{
    public required bool IsSuccess { get; init; }
    public string? FailureReason { get; init; }        // 目录不可写 / 文件被占用 / 序列化失败 …
    public string? QuarantinePath { get; init; }       // 落盘时发现旧文件损坏而隔离的路径
    public static AchievementFlushResult Success(string? quarantinePath = null);
    public static AchievementFlushResult Failure(string reason, string? quarantinePath = null);
}
```

**条款 10**：`Version` 的读写 MUST 通过 `SaveFileFormat` **之外**的常量
（如 `AchievementArchiveJson.Version = 1`），MUST NOT 复用 `SaveFileFormat.Version`。

---

## §3 合并规则

**条款 11**：合并键为 `(Achievement, Difficulty)`。对每个待落盘的 `PendingUnlock`：

1. 键不存在 ⇒ 追加 `ArchiveAchievementRecord(unlock.Achievement, unlock.Difficulty, unlock.UnlockedAt, unlock.SourceSaveId)`；
2. 键已存在 ⇒ **仅当** `unlock.UnlockedAt < 既有.CompletedAt` 时替换日期与来源；
   相等或更晚 ⇒ **不动**（FR-018 的「最早日期」是幂等操作）。

**条款 12**：`Origins` 的合并键为 `(Origin, Milestone)`，规则同条款 11。
当一次 `Flush` 的待落盘项里含 `FamilyExtinct` / `CenturyHouse` 时，MUST 同时追加
`OriginRecord(state.Origin, Extinction|CenturyFamily, 同年月, 同 SourceSaveId)`。
**注意**：`OriginRecord.Origin` 由调用方（`TurnRunner`）提供 —— `PendingUnlock` 不带 `Origin`，
所以 `Flush` 的签名里有 `Guid sourceSaveId` 但 `Origin` 由 `AchievementStore` 从**当前档案**无从得知；
因此 `Origins` 的追加 MUST 由**调用方**构造并传入，见条款 13。

**条款 13（修正）**：`Flush` 的实际签名 MUST 携带出身达成段的增量：

```csharp
AchievementFlushResult Flush(IReadOnlyList<PendingUnlock> unlocks, IReadOnlyList<OriginRecord> originAdditions);
```

调用方（`TurnRunner` ⑤）用 `state.Origin` 与 `state.CurrentDate`（或 `state.Ending.EndedAt`，若已结束）
构造 `originAdditions`：仅当本次 `PendingUnlocks` 含 `FamilyExtinct` / `CenturyHouse` 时才非空。
`sourceSaveId` 从 `PendingUnlock.SourceSaveId` 取，无需另传。
（条款 1 的签名以此条为准 —— 实现时 MUST NOT 让 `AchievementStore` 自己猜 `Origin`。）

**条款 14**：MUST NOT 在 `Read` 时对档案做「规整」（去重、排序、修正）。合并是 `Flush` 的职责；
`Read` 只解析。理由：`Read` 在判定路径上是只读且可能失败的，让它写文件会破坏「只读面」语义。

---

## §4 损坏隔离

**条款 15**：`Read` 或 `Flush` 遇到不可解析的 `achievements.json` 时：

1. 改名为 `achievements.corrupt-<n>.json`，`n` 为**从 1 起的第一个未占用序号**（
   `achievements.corrupt-1.json`、`-2.json`、…）；MUST NOT 使用时间戳
   （`DateTime.Now`/`UtcNow`/`Today`/`DateTimeOffset.Now` 在 G-07 的 14 项禁用 token 清单里，
   扫描范围含 `src\KFL.Infrastructure\`）。
2. 以**空档案**继续（`Read`）或以**空档案为基底**合并（`Flush`）。
3. 诊断经 `AchievementArchiveDiagnostics` / `AchievementFlushResult` 返回；MUST NOT 抛。
4. 隔离失败（改名也失败，例如目录只读）⇒ 不隔离、不删除，按空档案继续并把原因写进诊断。

**条款 16**：MUST NOT 删除损坏文件（销毁证据），MUST NOT 边隔离边覆盖同名文件。

**条款 17**：`achievements.json` 的缺失**不是**损坏：文件不存在 ⇒ 空档案、无诊断。

---

## §5 落盘点与原子性

**条款 18**：全局成就文件的**唯一写点**是 `TurnRunner.Advance` 的第 ⑤ 步。
动作型即时判定（契约二十二 §3）MUST NOT 落全局文件；`SaveOnExit` MUST NOT 落全局文件。

**条款 19**：`TurnRunner.Advance` 编排（FR-034），次序 MUST 由结构保证、MUST NOT 靠 try/catch 维持：

```
① MonthlySettlementEngine.Settle(state)                       // 既有；⑩ 已推进 CurrentDate
② AchievementAdjudicator.Judge(state, settlement, archiveMarks)   // 本次新增
③ _saveService.Save(state, _slot, overwrite: true)             // 既有；失败上抛且不回滚
④ 备份（SaveCadencePolicy.ShouldBackup）                        // 既有
⑤ _achievementStore.Flush(state.Achievements.PendingUnlocks, originAdditions)   // 本次新增
     成功 ⇒ state.Achievements.RestorePendingUnlocks([]) 并清空
     失败 ⇒ 队列保留、TurnOutcome 携带诊断、**不抛**
```

**条款 20**：`_achievementStore` 在 `TurnRunner` 的 ctor 里是**可选**参数
（`IAchievementStore? achievementStore = null`）：
`null` ⇒ 跳过 ⑤（判定与档内队列仍照跑）。理由与 `ISaveService?` 同款（`research.md` R-20）。
该参数 MUST 带默认值，使既有构造点无需改动。

**条款 21**：③ 的短路（`_saveService is null`）时 ⑤ **仍执行**：成就是独立于存档的能力。
短路返回的 `TurnOutcome` MUST 携带 ⑤ 的结果。

**条款 22**：⑤ 失败与 ③ 失败的语义**刻意不同**（③ 上抛、⑤ 不上抛）。两者对比 MUST 写进
`TurnOutcome` 与该文件的 XML doc：③ 落的是用户数据（失败必须让调用方知道且内存状态保留），
⑤ 落的是元进度（已有档内队列承担重试，FR-024 明令「MUST NOT 阻断回合推进与存档」）。

**条款 23**：⑤ 的写盘内容 MUST 是**全量合并后**的档案（既有记录 ∪ 本次增量），
MUST NOT 只写增量（否则会丢历史）。

---

## §6 存档本体的形状变更（FR-036）

**条款 24**：`src/KFL.Infrastructure/Save/SaveFileFormat.cs:32` 的 `Version` MUST 由 `4` 自增为 `5`。
理由：004 的闸门是**等值**比较（`SaveFailureTests.cs:156-157`、`:183-184` 用 `Version + 1` 造拒绝用例），
新增持久字段而不自增会让旧档静默缺省。

**条款 25**：键表（活工件，回写 `specs/004-save-system/contracts/save-format.md`）：

| DTO | 键数 | 键表 |
| --- | --- | --- |
| `SaveStateDto` | **10**（原 8） | `[0..7]` 原样；`[8]` `AchievementStateDto achievements`；`[9]` `EndingStateDto? ending` |
| `AchievementStateDto` | **8** | `[0] int successfulBribeCount`、`[1] int birthCount`、`[2] IReadOnlyList<int> lifespans`、`[3] Money peakTotalAssets`、`[4] bool everHadLoan`、`[5] int debugWakeCount`、`[6] IReadOnlyList<AchievementMarkDto> completed`、`[7] IReadOnlyList<PendingUnlockDto> pendingUnlocks` |
| `AchievementMarkDto` | **4** | `[0] int achievement`、`[1] int difficulty`、`[2] int completedYear`、`[3] int completedMonth` |
| `PendingUnlockDto` | **5** | `[0] int achievement`、`[1] int difficulty`、`[2] int unlockedYear`、`[3] int unlockedMonth`、`[4] Guid sourceSaveId` |
| `EndingStateDto` | **3** | `[0] int endedYear`、`[1] int endedMonth`、`[2] ChronicleDto chronicle` |
| `ChronicleDto` | **7** | `[0] int survivedYears`、`[1] int generations`、`[2] Money peakTotalAssets`、`[3] int highestDegree`、`[4] int totalJinShi`、`[5] decimal averageLifespan`、`[6] IReadOnlyList<Guid> genealogy` |

**条款 26**：`[9] ending` 用 `null` 表达「未结束」。JSON 侧 MUST NOT 标 `[JsonRequired]`；
二进制侧 MUST 用 `TryReadNil()` 分支（照 007 对 `proposal` 的既有处理）。
**MUST NOT** 改成「`bool` + 两个可空字段」的三键表示。

**条款 27**：`MessagePackSaveFormat` 同步：
- `SaveStateMemberCount` 8 → 10；新增 `AchievementStateMemberCount = 8`、`AchievementMarkMemberCount = 4`、
  `PendingUnlockMemberCount = 5`、`EndingStateMemberCount = 3`、`ChronicleMemberCount = 7`；
- `EnsureSaveStateShape` 的跳过序列 `Skip(ref reader, 6)` → `Skip(ref reader, 8)`，
  并按既有「多余成员 / 缺失成员」两个分支补齐新项的判定；
- `EnsureVersion` 的比较值 MUST 引 `SaveFileFormat.Version`（改常量即生效）。

**条款 28**：序号一律写底层整数（004 既有约定）；年月用两个 `int` 键（照 `SaveStateDto[1]` / `[2]` 的风格），
MUST NOT 引入新的日期表示。

**条款 29**：`SaveStateMapper` 的 `MapToDomain`（`src/KFL.Infrastructure/Save/Serialization/SaveStateMapper.cs:77` 附近）
MUST 增加两段复原调用，并 MUST 保持 `Completed` / `PendingUnlocks` 的**元素次序**
（SC-011 的逐字段比较依赖次序，`SaveStateComparer` 亦依赖）。

**条款 30（版本同步清单，SC-013）**：

| 位置 | 动作 |
| --- | --- |
| `src/KFL.Infrastructure/Save/SaveFileFormat.cs:32` | `4` → `5` |
| `src/KFL.Infrastructure/Save/SaveFileHeader.cs:21` | 陈旧注释「恒为 3」→ `5` |
| `tests/KFL.Tests/Infrastructure/SaveFailureTests.cs:23` | 陈旧注释「恒为 3」→ `5` |
| `tests/KFL.Tests/Infrastructure/SaveFormatContractTests.cs:37,56,270` | **无改动**（均写 `SaveFileFormat.Version` 常量） |
| `tests/KFL.Tests/Infrastructure/SaveStateValidatorTests.cs:356` | **无改动**（常量） |
| `tests/KFL.Tests/Infrastructure/SaveFailureTests.cs:156-157,183-184,477,503,585-586` | **无改动**（常量） |

**条款 31**：`ISaveService` 的签名与语义**不变**；`SaveFormat` / `SaveSlot` / `SaveCadencePolicy` **不变**。
本特性的⑤是 `TurnRunner` 的编排，不是 `ISaveService` 的成员。

---

## §7 一致性校验新增条目

**条款 32**：`SaveStateValidator` MUST 新增**恰 7** 条（既有 15 条不动）。
违反一律 `SaveFileException(SaveFileErrorKind.InvalidStructure)`（与既有表达一致）。

| # | 断言 | 依据 |
| --- | --- | --- |
| 1 | `Completed` 内 `(Achievement, Difficulty)` 唯一 | FR-011 |
| 2 | `PendingUnlocks` 内 `(Achievement, Difficulty)` 唯一，且每个键都出现在 `Completed` 里 | FR-013 ⑦⑧ |
| 3 | `Lifespans.Count <= Members.Count(p => p.Status.HasFlag(StatusFlag.Deceased))` 且每项 `>= 0`（**E-21 修订**：处决/株连在 `DeathRegister` 之外置 `Deceased`、不记享年 ⇒ 只能取上界） | FR-013 ③ |
| 4 | `SuccessfulBribeCount >= 0`、`BirthCount >= 0`、`DebugWakeCount >= 0`、`PeakTotalAssets >= 0` | 域下界 |
| 5 | `IsEnded ⇔ (EndedAt.HasValue ∧ Chronicle is not null)` | `research.md` R-08 |
| 6 | `IsEnded ⇒ Family.RegisteredMembers.Count == 0` | FR-026 |
| 7 | `Chronicle.Genealogy` 逐元素 == `Family.Members.Select(p => p.Id)`；且 `Chronicle.SurvivedYears == EndedAt.Year − 1` | R-09 / R-06 |

**条款 33（明确**不**校验的清单）**：`PeakTotalAssets` / `HighestDegree` / `TotalJinShi` /
`AverageLifespan` 与任何「重算值」的一致性 MUST **NOT** 校验。它们是**冻结的历史**：
绝嗣后成员已归档、功名记录的「首达时点」不可复原，重算只会在**合法档**上误报。
这条清单 MUST 以注释形式写在新增校验代码旁，防止后续实现「顺手补校验」。

**条款 34**：条目 7 的逐元素比较 MUST 用**次序敏感**的逐项比对（MUST NOT 用集合相等），
因为 `Genealogy` 是次序证物。

---

## §8 与既有测试的冲突面

**条款 35**：本次改动触碰的既有测试面**只有四处**，MUST 逐条核对：

| 冲突面 | 处理 |
| --- | --- |
| `SaveFileFormat.Version` 4 → 5 | 既有断言全用常量，**无需改**；改 2 处陈旧注释（条款 30） |
| `SaveStateDto` 8 → 10 键 | 既有测试**无**键数断言；同步 `MessagePackSaveFormat` 常量即可 |
| `TurnRunner` ctor +1 可选参 | 既有构造点**可不改**（默认 `null`） |
| `TurnRunner.Advance` 顶部新增结束拒绝 | 既有回合测试的 `GameState` 均未结束（`Ending.IsEnded == false`），行为不变 |

**条款 36**：`SaveStateComparer`（`tests/KFL.Tests/Infrastructure/SaveStateComparer.cs:21`）
MUST 扩展为比较 `Achievements` 的 8 个字段与 `Ending` 的三个状态字段（含次序），
使既有的往返测试自动覆盖新增形状。

**条款 37**：`tests/KFL.Tests/Rules/RulesTestHarness.cs` 与 `RecordingSaveService` MUST NOT 因本特性
被迫改动；新增的存档替身若需要，MUST 新建（如 `RecordingAchievementStore`），
MUST NOT 把「记录调用」混进 `SaveTestSandbox`（它已被定位为「存档测试唯一临时目录助手」）。

---

## §9 出身达成段

**条款 38**：`Origins` 段只在两种情况追加：`FamilyExtinct`（绝嗣）与 `CenturyHouse`（百年世家）。
`MillenniumHouse`（千秋万代）**不**入 `Origins` —— 规格书 §14 只把「出身达成」定义为
「绝嗣 / 百年世家」两类（FR-020）。这条是**排除项**，MUST 有测试断言「千年不写 Origins」。

**条款 39**：ACH-047 的判据读 `Origins` 的 `Origin` 去重集合是否为
{`Farmer`, `Artisan`, `Merchant`, `Scholar`}（四个 `Origin` 成员）。
它 MUST NOT 读当前存档的 `GameState.Origin`（那是单一值，永远不可能齐备）。

**条款 40**：`Origins` 的 `SourceSaveId` 语义与 `Achievements` 一致：保留**最早**那条的来源 UUID；
界面（U1）MUST NOT 另行推算来源（FR-025）。

---

## 后记

- **版本自增的连锁**：本契约一次性把 004 契约的「键表 + 版本闸门 + 常量登记 + 校验条数」四处全部回写，
  并登记了唯一需要人工核对的陈旧注释（`SaveFileHeader.cs:21`、`SaveFailureTests.cs:23`）。
  这两处在 004/005/006/007 的每一次版本自增里都容易被漏，故本契约把它们写成**显式清单项**。
- **与 004 的关系**：004 的格式契约是**活工件**，本契约对它做增量修订（键表 8 → 10、版本 4 → 5、
  校验 15 → 22 条）。修订 MUST 与实现**同一次提交**落地。
- **与 007 的关系**：007 的 `SaveStateMemberCount = 9`（家族成员段）与本契约新增的
  `AchievementStateMemberCount = 8` 是**不同**的常量，MUST NOT 混用。
- **不实现的部分**：`achievements.json` 的 MessagePack 侧、多进程并发写、旧档迁移器、
  成就展示口径 —— 见 `data-model.md` §9。
