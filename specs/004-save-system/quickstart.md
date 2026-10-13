# 快速验证：存档体系

**Feature**: `004-save-system` | **Date**: 2026-10-11 | **Spec**: [spec.md](./spec.md)

本文件是**可运行的人工验收指南**：每条场景都给出「怎么跑、看什么、期望什么」。
它不重复契约内容——字段语义看 [data-model.md](./data-model.md)，形状看
[contracts/](./contracts/)（[契约九](./contracts/save-service.md) /
[契约十](./contracts/save-format.md) / [契约十一](./contracts/save-cadence.md)）。

## 0. 前置

- .NET 10 SDK（`global.json` 只锁 `10.0` 版本带，允许任意已安装的 `10.0.x`）。
- 本机 NuGet 缓存需有 `messagepack 2.5.305`（离线可还原；已实测 `net10.0` 构建 0 警告 0 错误）。
- **受限宿主（Agent 沙箱）** 下必须带上 `AGENTS.md` 记录的两个开关：`-m:1 -nodeReuse:false`
  与 `-p:_MSTestEnableParentProcessQuery=false`。

```powershell
# 构建（期望：0 警告 0 错误）
dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false

# 测试（期望：全绿、0 skipped）
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false

# 只跑本特性的测试
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false `
  --filter "FullyQualifiedName~Infrastructure.Save|FullyQualifiedName~Rules.SaveCadence"
```

**门禁四条**（与 `AGENTS.md` 一致，第三条不能省）：

```powershell
dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false     # 0 警告 0 错误
dotnet test  KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false
dotnet sln   KejuFuShengLu.slnx list                       # 恰好六个工程
Get-ChildItem -Recurse -Filter *.sln | Where-Object { $_.FullName -notmatch '\\\.git\\' }   # 期望无输出
git status --porcelain                                     # 期望：无存档相关变更（SC-009）
```

**全程 MUST NOT 启动界面、MUST NOT 启动游戏循环**（FR-001：本特性可完全脱离界面被验收）。
全部测试都使用**临时目录**，MUST NOT 在仓库工作区留下任何存档产物（FR-022；SC-009）。

---

## 场景 1：关掉再打开，家族还在原地（US1 / FR-002 / FR-003 / SC-001）

**前提**：一份覆盖「在册 / 已亡 / 外嫁 / 待阙 / 致仕 / 患病 / 饥馑 / 有贷款本息 / 功名变迁史非空 /
非默认难度与非默认出身」的完整状态夹具（测试内构造，不落盘）。

**步骤**：分别以**二进制**与 **JSON** 各存读一轮（临时目录），再对读回的状态继续结算若干个月。

**期望**：
1. 读回状态的**每一个字段**等于写入前——逐类断言至少覆盖：存档标识 / 年月 / 难度与待生效难度 /
   出身 / 家族（姓氏、仕身份、家主、成员数含归档）/ 成员全部字段 / 功名变迁史 / 账本 / 三池金额 /
   资产 / 贷款本金与欠息 / 米价系数 / 生活费档位与待生效档位 / 饥馑阶段与计时（**≥14 类**，SC-001）；
2. 两种格式的**差异项数 = 0**，且两格式承载**同一信息集**（US1 AS2）；
3. **金额按原值**（含 scale）：`decimal.GetBits` 逐位相同（US1 AS4；FR-016）；
4. 归档成员仍在家族中且可按标识取回（US1 AS6）；
5. 继续结算一个月的结果与「同一状态从未落盘」的基线**逐字段相同**（US1 AS5；FR-017）。

**落点**：`tests/KFL.Tests/Infrastructure/SaveServiceTests.cs`。

---

## 场景 2：每回合自动保存、每 3 回合备份（US2 / FR-010 / FR-011 / SC-003）

**前提**：`TurnRunner` + 一个 `RecordingSaveService`（内存替身，**不碰文件系统**）+ 固定种子的随机源。

**步骤**：从同一初始状态连续 `Advance` 12 回合。

**期望**：
- `Save` 被调用 **12** 次、`Backup` 被调用 **4** 次，且分别是第 3、6、9、12 回合（契约十一 §5）；
- `CompletedTurnsAt` 依次为 1…12（契约十一 §1）；
- **结算抛出异常时**：该回合既未 `Save` 也未 `Backup`（FR-010 的第二句）；
- **保存抛出异常时**：未 `Backup`（FR-011 的「自动保存完成之后」）；
- **不注入 `ISaveService`** 时零次保存，且推进 N 回合后的存档内容与「开启自动保存」**逐字段相同**
  （US2 AS4）。

**真实文件版节奏**（临时目录）：12 回合后当前档内容 == 第 12 回合结束时的状态；`backups/` 下该存档
恒 **1** 个文件（覆盖式，覆盖率 100%）。

**落点**：`tests/KFL.Tests/Rules/SaveCadenceTests.cs` + `SaveServiceTests.cs`。

---

## 场景 3：写入被中断不会留下坏档（US2 AS5 / FR-013 / SC-006）

**前提**：`AtomicFileWriter` 的**测试用写入钩子**（契约九 §3 条款 6）——在写入第 N 块之后注入一次失败。

**步骤**：对一份已存在的当前档，注入一次「写入中途失败」，再读该存档；另对**备份**注入一次失败。

**期望**：
- 目标路径上存在的文件**要么**是写入前的完整存档、**要么**是写入后的完整存档（「两边都不是」出现
  **0** 次），且**始终可被成功读回**（SC-006）；
- 备份被中断时，**当前档不受任何影响**（Edge Case）；
- 失败后无残留临时文件（或残留的临时文件不参与任何读取路径）。

**落点**：`tests/KFL.Tests/Infrastructure/AtomicWriteTests.cs`。

---

## 场景 4：缺失 / 截断 / 结构不符 / 版本不符各自可识别（US3 AS7 / FR-014 / FR-015 / SC-007）

**步骤**：对同一份好档派生四种输入，逐条 `Load`：

| 输入 | 构造方式 | 期望 `Kind` |
| --- | --- | --- |
| 缺失 | 不存在的档名 | `Missing` |
| 截断 | 只保留文件的前 60% 字节 | `Truncated` |
| 结构不符 | 合法 JSON 但删掉一个必需成员 | `InvalidStructure` |
| 版本不符 | 把头记录的 `FormatVersion` 改成 `3` | `UnsupportedVersion` |
| （附加）字节损坏 | 随机改掉头记录之后的若干字节 | `Malformed` |

**期望（四类共同）**：MUST NOT 返回「看起来能玩」的默认状态或部分状态；失败后**原文件内容**与
**内存中已有的状态**均未被修改（差异 = 0，SC-007）。

**落点**：`tests/KFL.Tests/Infrastructure/SaveFailureTests.cs`。

---

## 场景 5：档名、目录与格式（US3 / FR-005~FR-009 / SC-004 / SC-005）

**步骤与期望**（全部在临时目录）：

| 用例 | 期望 |
| --- | --- |
| 中文档名「宋家三十年」 | 新建成功，磁盘上出现对应扩展名文件 |
| 9 个 Windows 非法字符（`\ / : * ? " < > \|`）逐条 | **拒绝率 100%**，文件系统上无非法文件名 |
| `-`（连字符） | **放行**（规格书 §14 的 Q3 裁决） |
| 空 / 纯空白 / 超长（> 255）/ 保留设备名（`CON`、`NUL`、`COM1`…）/ 首尾空白 / 以 `.` 结尾 | 逐条拒绝，MUST NOT 落到文件系统再报错 |
| 同名存档已存在而未确认 | `Save` 拒绝（`SaveNameConflictException`）；`Exists` 回答 `true` |
| 同名但**另一种格式**已存在 | `Exists` 同样回答 `true`（存在性与格式无关，契约九 §5 条款 1） |
| 改档名 | `SaveId` 与游戏内容**逐字节不变**、家族姓氏**未变**（SC-004） |
| 未指定格式 | 落 `.dat`（默认二进制）；指定 JSON ⇒ `.json` |
| 自定义根目录 | 文件落在该目录下；未配置 ⇒ 可执行文件旁 `saves/` |
| 同 UUID 的另一个文件名 | `Save` 拒绝（`SaveIdentityConflictException`） |
| 格式切换（`.dat` → `.json`） | 先成功写出 `.json`，随后 `.dat` 被移除；写出失败时 `.dat` 必须仍在 |
| 备份回退 | 把 `backups/<名>.dat` 复制回主文件后 `Load` 成功且内容一致 |

**落点**：`tests/KFL.Tests/Infrastructure/SaveServiceTests.cs`。

---

## 场景 6：格式契约（FR-004 / FR-021 / SC-010）

**步骤**：① 断言头记录恰有三个成员且次序为 `FormatVersion, PayloadLength, SaveId`；
② 断言 DTO 的键名集合与 [契约十 §4](./contracts/save-format.md) 的键表逐项一致；
③ 断言枚举在 JSON 里落成**名字**、在二进制里落成**整数**，非法值被拒；
④ 用同一状态构造一份 **100 年（1200 回合）** 的存档，写入并读回。

**期望**：
- 键表多一个、少一个都失败；
- 1200 回合的存档**完整可写、可读**，账本条目**丢失数 = 0**，且落盘形态是**单一文件**（不分块、
  不压缩）——FR-021 的裁决在极端体量下仍成立（SC-010）；
- 两格式读回结果逐字段等价（FR-004/FR-005）。

**落点**：`tests/KFL.Tests/Infrastructure/SaveFormatContractTests.cs`。

---

## 场景 7：章程与守卫（FR-018 / FR-019 / FR-020 / SC-008 / SC-009）

```powershell
# G-07 零违规（非 UI 层与领域/接缝测试无系统时钟、无全局随机、无文件系统、无网络）
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false `
  --filter "FullyQualifiedName~Architecture.EnvironmentDependencyTests"

# SC-008 的金额条款与数值唯一性
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false `
  --filter "FullyQualifiedName~Architecture.ConfigLiteralTests"
```

**期望**：
- `KFL.Core` 与 `KFL.Rules` 的禁用 token 违规数 = **0**；存档实现的每一处文件系统访问都在**同一行**
  带 `// arch-guard:allow <理由>`，豁免行可按 [契约九 §4.1](./contracts/save-service.md) 的清单
  逐一列举；
- `KFL.Core` 的类型上**没有**任何序列化框架注解、**没有**新增公开写通道（可 grep `MessagePackObject`
  与 `InternalsVisibleTo` 的落点核对）；
- 一次完整门禁跑完后，`git status --porcelain` 里**无存档相关变更**（SC-009）。

---

## 场景 8：不引入系统时钟（FR-018 / R-23）

**步骤**：`grep -rn "DateTime\.\|Environment\.TickCount" src/KFL.Infrastructure src/KFL.Rules`。

**期望**：无命中；且存档文件里**不含**任何写入时间戳（同一状态的两次存档字节**完全相同**——
这一条同时是「可复现」的最强断言）。001 `research.md` 登记的「系统时钟抽象（存档写入时间戳）」
由本特性**结清为不需要**。
