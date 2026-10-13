# 契约九：存档服务（`ISaveService`）

**Feature**: `004-save-system` | **Date**: 2026-10-11

本契约定义规格书 §2 列在 `KFL.Infrastructure` 的 `ISaveService`——001 契约二 §5 声明它
「等各自阶段带着真实需求回来定义」，本契约即那次定义（FR-001、FR-002）。它是**界面轨 U1/U5**
将来唯一允许调用的存档入口（章程原则 VI：调试通道与界面 MUST 复用同一入口）。

## 1. 入口形状

```csharp
namespace KFL.Infrastructure.Abstractions;

public readonly record struct SaveSlot(string Name, SaveFormat Format = SaveFormat.Binary);

public enum SaveFormat { Binary = 0, Json = 1 }   // 零值 = 默认格式（FR-005）

public interface ISaveService
{
    string RootDirectory { get; }                        // 生效的存档根（默认 = 可执行文件旁 saves/）
    bool Exists(SaveSlot slot);                          // §5 条款 1：与该名字的**任一格式**文件比
    SaveSlot? FindBySaveId(Guid saveId);                 // §5 条款 3：只读头记录（R-18）
    string FilePathOf(SaveSlot slot);                    // 该槽位的绝对路径（U1/测试需要，MUST NOT 越出根）
    void Save(GameState state, SaveSlot slot, bool overwrite);   // §2、§3
    GameState Load(SaveSlot slot);                       // §2、[契约十 §6](./save-format.md)
    SaveSlot Rename(SaveSlot slot, string newName);      // §6
    void Backup(SaveSlot slot);                          // §7
}

public sealed record SaveOptions
{
    public string? RootDirectory { get; init; }          // null ⇒ 默认可执行文件旁 saves/
    public SaveFormat DefaultFormat { get; init; } = SaveFormat.Binary;
}
```

| 条款 | 要求 |
| --- | --- |
| 1 | `Save`/`Load` MUST 位于**可脱离界面被单元测试**的非 UI 层（FR-001；章程原则 II、IV）：`KFL.Infrastructure` 的两个实现 MUST NOT 引用任何 WPF/Windows 类型 |
| 2 | 注入 MUST 经构造函数；MUST NOT 静态单例、`ServiceLocator` 或属性注入（001 契约二 §4 条款 2） |
| 3 | 存档实现 MUST NOT 读取系统时钟（FR-018；G-07 的 14 个禁用 token 一律零命中）；MUST NOT 落盘写入时间戳（R-23） |
| 4 | 存档实现 MUST NOT 引入第二个内存真源：`SaveStateDto` 只在一次调用的栈内存活（[data-model §1](../data-model.md) 不变量 1） |
| 5 | `FilePathOf` MUST 返回槽位在**当前根**下的路径；MUST NOT 允许槽位逃出根（路径分隔符在名称层即被拒，§4 条款 2） |
| 6 | 每个公开方法 MUST 在**写任何字节之前**完成全部校验（与 002 的 `FamilyEconomy.Apply` 同一「失败原子性」口径） |
| 7 | **当前档槽位**（`Current Slot`，档名 + 格式）是**会话**信息，由装配期注入并由上层持有（`SaveOptions.CurrentSlot` 或回合编排层的构造参数）；MUST NOT 成为 `GameState` 的字段、MUST NOT 随存档落盘（FR-010/FR-012；spec 裁决 **Q4**） |

## 2. 拒绝与失败矩阵

`Save` 的触发条件与结果——**结果列即调用方可分辨的信号**（FR-008/FR-014/FR-015；SC-007）：

| 触发 | 结果 |
| --- | --- |
| 存档名不合法（§4） | 拒绝：`ArgumentException`，MUST NOT 落到文件系统 |
| 同一根下该名字已有存档（任一格式）而 `overwrite: false` | 拒绝：`SaveNameConflictException` |
| 同一根下**另一个文件名**携带同一 `SaveId` | 拒绝：`SaveIdentityConflictException`，MUST NOT 写任何文件 |
| 存档根不可创建 / 不可写 | 拒绝：`SaveAccessException`，MUST NOT 静默吞 |
| `overwrite: true` 且同名存档存在 | 覆盖写入；随后移除同名的**异格式**文件（§5 条款 2） |
| 目标文件缺失 | `Load` 拒绝：`SaveFileException(Kind = Missing)` |
| 文件被截断（剩余字节 < 头声明的 `PayloadLength`） | `Load` 拒绝：`Kind = Truncated` |
| 头记录读不出（字节损坏） | `Load` 拒绝：`Kind = Malformed` |
| `FormatVersion != 2` | `Load` 拒绝：`Kind = UnsupportedVersion`（**读不懂就明说**，FR-015） |
| 主体可解析但缺**本版本必需**成员，或违反 data-model 的十条一致性校验 | `Load` 拒绝：`Kind = InvalidStructure` |
| 其余解析失败 | `Load` 拒绝：`Kind = Malformed` |

**失败原子性**：`Save` 的全部拒绝路径 MUST 在校验通过之前不产生任何文件写入；`Load` 的全部拒绝路径
MUST NOT 修改原文件、MUST NOT 返回默认状态或部分状态、MUST NOT 污染调用方已有的内存状态
（FR-014；SC-007 的四类输入各自可识别）。

**为什么 `Kind` 要细分**：SC-007 要求「缺失 / 截断 / 结构不符 / 版本不符」**各自**产生可识别的失败
信号——上表给出四个互不相同的 `Kind`；`Malformed` 是规格书未列举的第五类（字节损坏）的兜底，
不削弱前四类的可分辨性（R-07）。

## 3. 原子性条款

| 条款 | 要求 |
| --- | --- |
| 1 | 一切写盘 MUST 经 `AtomicFileWriter`：① 同目录临时文件 → ② `Flush(flushToDisk: true)` → ③ `File.Move(tmp, target, overwrite: true)` |
| 2 | 临时文件 MUST 落在**目标同目录**（同卷 ⇒ 替换原子），MUST NOT 落在系统临时目录 |
| 3 | 任意时刻（含进程被杀、磁盘写满、无写权限）目标路径上的文件 MUST 要么是**写入前**的完整存档、要么是**写入后**的完整存档（FR-013；SC-006） |
| 4 | 失败路径 MUST 尽力清理临时文件，且 MUST NOT 触碰目标文件 |
| 5 | **备份走同一条路径**（R-14）：半截的备份与半截的当前档同样不可接受（Edge Case） |
| 6 | 可注入的中断点：`AtomicFileWriter` MUST 暴露一个**仅供测试**的写入钩子（`Action<int>? OnChunkWritten` 形态），使「写入中途失败」不必靠真的杀进程来注入（SC-006 的可测性） |

## 4. 命名与路径条款

| 条款 | 要求 |
| --- | --- |
| 1 | 名称口径的**唯一真源**是 `SaveName`（R-13）：接受中文与 `-`；拒绝 `\ / : * ? " < > \|`（9 个，FR-007 的 Q3 裁决）、空/纯空白、长度 > 255、Windows 保留设备名（`CON`/`PRN`/`AUX`/`NUL`/`COM1~9`/`LPT1~9`，大小写不敏感且**带扩展名与否都算**）、首尾空白、以 `.` 结尾、ASCII 控制字符 `0x00~0x1F` |
| 1.1 | **同名判定（含 `Exists`、覆盖冲突、改名目标）MUST 大小写不敏感**（spec 裁决 **Q7**）：`SongJia` 与 `songjia` 视为同名——Windows 文件系统本就不区分，若判重敏感就会出现「系统回答不存在、随后的保存静默覆盖另一写法」的空洞；保存时仍**原样保留**玩家输入的大小写写法；不做 Unicode NFC 规范化（Windows 也不按规范化比较） |
| 2 | 一律**拒绝**、MUST NOT 静默规范化（FR-007/Edge Case 允许「拒绝或规范化」，取拒绝以避免「同名却指向不同文件」的歧义）；因此名称里不可能出现路径分隔符，槽位逃不出根 |
| 3 | 存档根：未配置 ⇒ `AppContext.BaseDirectory` + `saves`（FR-006）；已配置 ⇒ `SaveOptions.RootDirectory` 原样使用 |
| 4 | 备份落点 = **存档根**下的 `backups`（§7；§14 的 Q1 裁决） |
| 5 | 目录不存在 MUST 自动创建；创建失败/无写权限 ⇒ `SaveAccessException`，MUST NOT 静默吞（Edge Case） |
| 6 | 文件系统访问 MUST 收敛在**少数明确行**上，逐行 `// arch-guard:allow <理由>`（FR-019；SC-008） |

### 4.1 文件系统访问点与行级豁免清单（SC-008 的可列举对象）

| 类型 | 允许的文件系统操作 | 数量级 |
| --- | --- | --- |
| `AtomicFileWriter` | 建/写临时文件、`Flush`、`Move` 替换、失败清理、删除旧格式文件与旧格式备份、备份改名 | ≤ 10 行 |
| `SaveCatalog` | 枚举根目录与 `backups/`、存在性判定（**大小写不敏感**）、读头记录、目录创建 | ≤ 10 行 |
| `SaveName` | 无（纯字符串校验） | 0 |
| `tests/KFL.Tests/Infrastructure/SaveTestSandbox.cs` | 建临时根、读字节、列文件、复制备份回主文件、清理 | ≤ 15 行 |

**MUST NOT** 出现第四处文件系统访问点（含 `KFL.Core` / `KFL.Rules`：三者的 G-07 违规数 = 0）。

## 5. 唯一性与格式切换条款

| 条款 | 要求 |
| --- | --- |
| 1 | `Exists(slot)` MUST 回答「该**名字**在存档根下是否已有存档」，**与格式无关**（FR-008） |
| 2 | 同名的异格式文件处置：`overwrite: true` ⇒ 写出新文件后移除同名异格式文件（「该名字归调用方」）；`overwrite: false` ⇒ §2 的冲突拒绝 |
| 3 | `FindBySaveId(saveId)` MUST 只读**头记录**（R-05/R-18）：命中即返回该槽位，未命中返回 `null`；MUST NOT 读主体（性能与最小读取面） |
| 4 | 同一根下同 `SaveId` 至多一个文件（Assumptions；001 R-03 的收口）：`Save` 前 MUST 探测，命中**另一个文件名**即 `SaveIdentityConflictException` |
| 5 | **格式切换**：MUST 先成功写出新格式文件，**再**删除 UUID 相同的旧格式文件；新文件写出失败 ⇒ MUST NOT 删除旧文件（Assumptions）。**同一时点 MUST 一并移除该存档的旧格式备份**（spec 裁决 **Q5**），且只在新的当前档写成功之后移除 |
| 6 | 备份文件 MUST NOT 参与唯一性判定与 `Exists`（它是副本，不是候选档） |
| 7 | 改名与格式切换 MUST NOT 让同一存档标识在 `backups/` 下留下**两个**候选（旧名副本 + 新名副本，或两种扩展名各一份）：见 §6 条款 5、§7 条款 7（spec 裁决 **Q5**） |

## 6. 改名不变性条款

| 条款 | 要求 |
| --- | --- |
| 1 | `Rename` 的**主档** MUST 只做**文件移动**（`File.Move`）：字节逐字节不变 ⇒ `SaveId` 与全部游戏内容不变（FR-009；001 FR-012 的「改档名不变性」在本契约首次获得可验证对象）；同存档的**备份**按条款 5 同步改名 |
| 2 | 新名 MUST 先过 `SaveName`（§4 条款 1）；新名已存在 ⇒ 拒绝（§2 的冲突口径） |
| 3 | `Rename` MUST NOT 触碰任何主体字节，因此 MUST NOT 改动 `Family.Name`（§12.4 与 §14 是两个独立概念；SC-004） |
| 4 | 返回值 = 新槽位；旧槽位文件 MUST 已不存在 |
| 5 | `Rename` MUST **同时**把该存档在 `backups/` 下的备份（若有）**改名**为同名的同扩展名（spec 裁决 **Q5**）——使「主档名 = 备份名」在改名后仍成立；备份改名失败 MUST NOT 损坏主档，且 MUST 以 `SaveAccessException` 家族的可识别方式暴露，MUST NOT 静默吞 |
| 6 | 备份改名 MUST NOT 触碰其字节（与条款 3 同口径）：改名后备份仍可被同一套 `Load` 读回 |

## 7. 备份条款

| 条款 | 要求 |
| --- | --- |
| 1 | `Backup(slot)` = 把当前档的字节复制为 `<根>/backups/<存档名>.<与当前档相同的扩展名>`（§14 的 Q1 裁决；FR-011） |
| 2 | **覆盖式**：每个存档恒 1 份（`backups/` 下该名字至多一个文件），MUST NOT 按时间戳追加；**改名与格式切换之后该不变式 MUST 仍成立**（spec 裁决 **Q5**、SC-003） |
| 3 | 经 `AtomicFileWriter`（§3 条款 5）；`backups/` 不存在即创建 |
| 4 | 备份是**可读回**的完整存档（同格式 ⇒ 同一套 `Load` 路径）；「回退」= 把备份复制回主文件 |
| 5 | 备份失败 MUST NOT 影响已写好的当前档；MUST NOT 静默吞（Edge Case 的「降级 MUST 以可识别的方式暴露」） |
| 6 | 备份的**时点**由 [契约十一](./save-cadence.md) 决定，本契约只提供原语 |
| 7 | `backups/` 下同一存档标识 MUST 恒至多 1 份，且其文件名 MUST 与当前档**同名同扩展名**：改名走 §6 条款 5、格式切换走 §5 条款 5；发现同标识的**其他**备份文件（如上一版本遗留的旧名副本）时 MUST 在本次备份/切换中清理（spec 裁决 **Q5**） |

## 8. 本契约明确不包含

- **节奏与编排**（每回合保存、每 3 回合备份、退出保存的调用次序）：[契约十一](./save-cadence.md)。
- **线上字节形状**（头记录、DTO 键表、编解码选项、版本闸门与失败分类的判据）：[契约十](./save-format.md)。
- **界面**（新建/读档/回主菜单界面、存档列表、覆盖确认弹窗、设置面板的「存档格式」「存档目录」项）：
  界面轨 U1/U5；本契约只交付被它们调用的能力（FR-008、FR-026 之外）。
- **进程退出钩子与主菜单事件**：界面轨 U1（本契约交付入口，不实现钩子；FR-012）。
- **成就持久化与按 UUID 归属**（`achievements.json`）：逻辑轨 ⑧。
- **并发/多进程协调、文件锁**：规格书未要求（Assumptions）。
- **旧格式迁移与向下兼容**：Out of Scope（[契约十 §6](./save-format.md) 只拒绝，不转换）。
- **传入 `GameState` 以外的规则**：本契约 MUST NOT 含任何游戏规则数值（§14 的 Q2 裁决与 §12.3 的
  统计口径属 `KFL.Rules`）。
