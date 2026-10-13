# 契约十一：保存节奏与回合编排（自动保存 / 备份 / 退出保存）

**Feature**: `004-save-system` | **Date**: 2026-10-11

本契约定义「**什么时候**存、**存哪一份**」：回合数的定义、`TurnRunner` 的编排次序、自动保存的
开关与退出保存的复用。它是 FR-010、FR-011、FR-012、FR-018 与 SC-003 的可执行形式。

> **本契约不改契约三**：月度结算的八步序列（[契约三 §1](../../002-monthly-settlement-economy/contracts/monthly-settlement.md)）
> **一字不动**——它的「**MUST NOT 存在**的步骤」清单里明列「存档写入」，本契约因此把落盘放在
> 结算**之外**的一层（R-17）。故本特性**没有**对任何上游步骤序列做插入式形状变更。

## 1. 回合数的定义

| 条款 | 要求 |
| --- | --- |
| 1 | 「回合数」= 该存档**已成功结算的回合数**，由 `SaveCadencePolicy.CompletedTurnsAt(GameState.CurrentDate)` **派生**：`(Year − 1) × 12 + (Month − 1)`。**推演进程的当前年月恒从纪元 `1 年 1 月` 起算**（spec 裁决 **Q8**：0/负数年份只出现在角色的**出生年月**等成员字段，不会出现在 `CurrentDate`），故真实推演中回合数恒为正、「3 的倍数」不存在负数区间的歧义 |
| 2 | 派生必须成立的前提：1 回合 = 1 游戏月（§3），且 `Settle` 的第 ⑦ 步已把 `CurrentDate` 推进一个月——故「第 N 次成功结算后」`CompletedTurnsAt == N` |
| 3 | MUST NOT 新增持久化的回合计数器/时间戳字段（那是与时间**同步维护**的第二真源，且属跨特性形状变更；§12.3 的「不另存可复制的汇总值」同款口径） |
| 4 | 时点 MUST 完全由回合数与显式入参决定（FR-018）：MUST NOT 读墙钟、MUST NOT 依赖 `File.GetLastWriteTime` 之类的文件系统时间 |

## 2. `TurnRunner` 的编排次序

`KFL.Rules/Save/TurnRunner.TurnOutcome Advance(GameState state)` MUST 严格按此顺序执行，一次回合
**只能**执行一遍：

| 步 | 动作 | 消耗随机 | 落盘 |
| --- | --- | --- | --- |
| ① | **结算**：`new MonthlySettlementEngine(random, new GameStateClock(state)).Settle(state)`（构造引擎绑定到**本次**的 `state`，与既有全部调用点同形） | 同契约三 §4 | — |
| ② | 未注入 `ISaveService` ⇒ 直接返回（**自动保存关闭**，§3） | — | — |
| ③ | **覆盖写入当前档**：`Save(state, slot, overwrite: true)`（FR-010；`slot` 见条款 6） | 抛出 ⇒ **保留已结算的内存状态**并向上传播（MUST NOT 回滚、MUST NOT 静默吞；spec 裁决 **Q6**，见条款 7） | 当前档 |
| ④ | `turns = CompletedTurnsAt(state.CurrentDate)`；`ShouldBackup(turns)` 为真 ⇒ `Backup(slot)`（FR-011） | — | `backups/<名>.<同扩展名>` |
| — | 返回 `TurnOutcome(Settlement, CompletedTurns, Saved, BackedUp)`（**增量**快照，MUST NOT 被写回 `GameState`） | — | — |

**条款**
1. ③ MUST 在①**正常返回之后**：写入 MUST NOT 发生在结算中途，且结算抛出异常时 MUST NOT 覆盖当前档
   （FR-010 的第二句）——这是**结构性**成立的（两次调用分离），MUST NOT 靠 `try/catch` 约定。
2. ④ MUST 在③**成功之后**：备份的是**刚写入的内容**（§14 的 Q1 裁决）。
3. ③ MUST 是 `ISaveService.Save` 的**同一入口**，MUST NOT 另写一套序列化/写盘（章程原则 VI 的同款口径）。
4. `TurnRunner` MUST NOT 读时钟、MUST NOT 自增任何计数器（回合数一律现算，§1）。
5. 每回合新建一次 `MonthlySettlementEngine`：它的 `IGameClock` 绑定到具体的 `GameState`
   （`GameStateClock(state)`），与 001 契约二 §2 的注入口径一致。
6. **当前槽位（`slot` = 档名 + 格式）由 `TurnRunner` 的构造参数在装配期注入**（或由持有它的
   存档服务提供），`Advance` / `SaveOnExit` **不逐次接收**槽位（spec 裁决 **Q4**）：它是
   **会话**信息，MUST NOT 成为 `GameState` 的字段、MUST NOT 随存档落盘（FR-010/FR-012）。
7. **写盘失败（③ 抛出）的语义**（spec 裁决 **Q6**）：结算已改动 `state`，故 ③ MUST **保留**
   该内存状态并把失败向上传播；MUST NOT 回滚（回滚需要结算前快照 = FR-020 禁止的第二份
   内存真源）、MUST NOT 静默吞。「本回合已推进但未落盘」是**允许的中间态**：下一次 `Advance`
   的 ③ MUST 把该回合一并写入，MUST NOT 因上次失败而跳过、补齐或错位。

## 3. 自动保存的开关与退出保存

| 条款 | 要求 |
| --- | --- |
| 1 | 「自动保存开启/关闭」= 装配期是否注入 `ISaveService`（`null` ⇒ 关闭）；MUST NOT 新增持久化设置项（设置面板属界面轨 U5；章程「复杂度 MUST 被论证」） |
| 2 | 开启自动保存 MUST NOT 改变推演结果：推进 N 个月后的存档内容 MUST 与「关闭后手工保存一次」的**逐字段相同**（US2 AS4） |
| 3 | `TurnRunner.SaveOnExit(GameState)` MUST 复用③的**同一个** `Save` 入口与同一个槽位（FR-012）；本契约只交付该入口，进程退出钩子属**界面轨 U1** |
| 4 | 退出保存与自动保存的差异只有「由谁触发」，MUST NOT 有第二套参数、第二份序列化或第二个路径 |

## 4. 数值登记

| 数值 | 配置成员 | 断言锚点 |
| --- | --- | --- |
| 备份间隔 **3 回合** | `GameConfig.Save.BackupIntervalTurns`（单点在 `SaveCadencePolicy.BackupIntervalTurns`） | `SaveCadenceTests.推进十二回合时当前档写十二次备份写四次`（第 3、6、9、12 回合各一次；SC-003） |
| 推演纪元 **1 年 1 月** | `GameConfig.Save.EpochYear` / `EpochMonth`（单点在 `SaveCadencePolicy`） | `SaveCadenceTests.回合数自纪元逐月递增`（§3「从 1 年 1 月开始推演」） |

**条款**
1. `BackupIntervalTurns` 与纪元数值 MUST 只声明在 `KFL.Rules/Config/`（章程「技术栈与工程约束」；
   SC-009）；`KFL.Infrastructure` 的存档实现 MUST NOT 出现这两者的副本（`Infrastructure` 看不到
   `KFL.Rules`，G-05 从依赖边就堵住了这条错路）。
2. 两者**逐条出自**规格书（§14 的「每 3 回合」、§3 的「从 1 年 1 月开始」），**不新增、不改动任何
   影响平衡的数值**；`3` 是整数，按 [契约五 §5](../../002-monthly-settlement-economy/contracts/config-registry.md)
   的既有口径**不进** `ConfigLiteralTests.RegisteredValues`（该清单只收小数与 4 位及以上官俸）。
3. `ShouldBackup(turns)` MUST 含 `turns > 0` 条件，使「尚未结算任何回合」不被算成 3 的倍数。
4. `ConfigLiteralTests` 的登记清单**无需增补**；本特性**没有**新增小数规则值
   （`SaveFileFormat.Version` 等文件格式常量不属规则数值，见 [契约十 §7](./save-format.md)）。

## 5. 断言锚点（SC-003 的物理落点）

| 断言 | 期望 |
| --- | --- |
| 从同一初始状态推进 12 回合（`RecordingSaveService` 内存替身） | `Save` 被调用 **12** 次、`Backup` 被调用 **4** 次；调用序号为第 3、6、9、12 次 |
| 备份文件个数 | 该存档的备份恒为 **1** 个（覆盖式，覆盖率 100%） |
| 结算抛出时 | 该回合 MUST NOT 调 `Save`、MUST NOT 调 `Backup`（FR-010 的第二句） |
| 保存抛出时 | MUST NOT 调 `Backup`（FR-011 的「完成之后」）；**内存状态保留**（`state` 仍处于已结算的当前年月），失败向上传播（spec 裁决 **Q6**） |
| 保存失败后继续推进 | 下一次 `Advance` 的 ③ 写入的是**当前**完整状态（含上次未落盘的那一个月），账本条目全量与推进次数一致，**无缺口**（Q6） |
| 关闭自动保存（不注入服务）推进 N 回合 | 零次 `Save`；与「开启后推进 N 回合」的**存档内容**逐字段相同 |
| `SaveOnExit` 与自动保存 | 命中**同一个** `Save(state, slot, overwrite: true)` 调用（用替身记录实参比对） |
| 回合数 | 初始状态 ⇒ `CompletedTurnsAt == 0`；第 1~12 次结算后依次为 1~12 |

## 6. 本契约明确不包含

- **存档服务入口、命名、原子性、唯一性、改名、备份原语**：[契约九](./save-service.md)。
- **线上字节形状**：[契约十](./save-format.md)。
- **月度结算的步骤序列**：[契约三 §1](../../002-monthly-settlement-economy/contracts/monthly-settlement.md)
  （本契约**不改**它；落盘在结算之外的一层）。
- **界面**（下月/快进按钮、退出钩子、主菜单返回、自动保存的设置开关）：界面轨 U1/U5。
- **快进暂停规则与决策事件**（§3）：逻辑轨 ⑦ 与界面轨 U1。
- **多档并行保存、定时/后台保存**：规格书未要求；逻辑推进仅由用户输入驱动（§3）。
