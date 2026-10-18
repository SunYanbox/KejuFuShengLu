# 快速验证：成就与绝嗣结局（008 · 逻辑轨 ⑧）

**Feature** | **Date**: 2026-10-18 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

> 每个场景都是「一条命令 + 一个可证断言」。全部命令在本仓库根目录执行；
> 受限宿主（DSH 等）必须带 `-m:1 -nodeReuse:false` 与 `-p:_MSTestEnableParentProcessQuery=false`
> （见 `AGENTS.md` 的「沙箱命令执行有误解决方法」）。
>
> 本特性是**纯逻辑轨**：全部断言 MUST 可脱离 `KFL.Presentation` / `KFL.App` 运行，
> MUST NOT 依赖墙钟、区域设置或真实用户目录（`spec.md` FR-046）。

## 0. 前置

```powershell
dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false
dotnet test  KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false
dotnet test  KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~Achievement|FullyQualifiedName~Extinction|FullyQualifiedName~Chronicle|FullyQualifiedName~TukeyTrimean|FullyQualifiedName~EndedSave"
```

三条全绿才算本特性可交付；第三条是**本特性专属**的快速回路。

## 场景 1：68 条标识三元组齐备

`US1 / FR-001~FR-005 / SC-001`

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~AchievementCatalogTests"
```

**期望**：`AchievementId` 恰 **68** 个成员；`AchievementCatalog` 恰 **68** 行；
`Code` 集合逐字等于 `ACH-001`~`ACH-068`（无重号、无缺号）；七个系列的成员数
= 初露锋芒 **12** / 科名累进 **8** / 宦海沉浮 **11** / 宗族血脉 **17** / 覆巢之下 **11** /
饥馑求生 **4** / 调试秘辛 **5**（合计 68）；每条 `AchievementSource.Pending` 的恰 **6** 条
（ACH-041、ACH-064~068）。断言表逐行对应 `契约二十二 §1` 的三元组表。

## 场景 2：68 条判据逐条触发与不触发

`US1 / US2 / FR-006~FR-009 / SC-001 / SC-015`

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~AchievementPredicateTests"
```

**期望**：每一条 ACH-001~068 有**两个**用例 —— ① 构造满足判据的状态面 + 当月事实 ⇒
`IsSatisfied` 为真；② 构造**恰好差一格**的状态 ⇒ 为假。参数化后用例总数 = **136**。
边界逐条按 `契约二十二 §2` 的「阈值」列取值（下表是最容易被写错的几处）：

| 成就 | 边界断言 |
| --- | --- |
| `五世其昌` | `Generation >= 4`（**非** `>= 5`；辈分 0 基准，见 `Assumptions 6`） |
| `百年世家` | `SurvivedYears > 100` ⇒ 首次成立时 `CurrentDate.Year == 102` |
| `千秋万代` | `SurvivedYears > 300` ⇒ 首次成立时 `CurrentDate.Year == 302` |
| `绝处逢生` | `>= 100`（**含** 100，§14.1:570 无「超过」字样）⇒ 首次成立时 `CurrentDate.Year == 101` |
| `寿比南山` | `AgeAt >= 80`（分界月份两侧各一例） |
| `囹圄之灾` | 同一月份 `PrisonerCount >= 3` |
| `枯木逢春` | 上月 `Relief` ∧ 本月脱离救济 ∧ 实付 == 应付 |
| `沉疴难起` | 本月 实付 **<** 应付 |
| `名落孙山` | `Level == ShengShi` ∧ `Passed == false` ∧ 判定时 `Study >= 80` |
| `皓首穷经` | `AgeAt >= 50` ∧ `ShengShiFailureCount >= 6` ∧ 特奏名授官 |
| `榜下捉婿` | 曾出现 `Kind == ImperialProposal`（**含被拒**，只读 `Kind`） |

## 场景 3：三均值金色算例逐行

`US3 / FR-013 ③ / FR-028 ⑥ / SC-009`

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~TukeyTrimeanTests"
```

**期望**：`[50]`→`50.000`；`[40,60]`→`50.000`；`[10,50,90]`→`50.000`；`[10,40,60,70]`→`47.500`；
`[10,20,50,60,70]`→`45.000`；`[10,20,30,40,100]`→**`30.000`**（同时断言 **≠ `36.250`**，
即「奇数样本弃中位」的错误变体）；`[]`→`0.000` 且**不抛异常**；`[1..9]`→`5.000`。
口径逐条对应 `契约二十四` 条款 15；`decimal` 全程、只在最后 `Math.Round(…, 3, AwayFromZero)`。

## 场景 4：回合末判定次序与编年史七项

`US3 / FR-007 / FR-028 / FR-032 / FR-034 / SC-009`

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~ChronicleTests|FullyQualifiedName~AchievementAdjudicatorTests"
```

**期望**：`TurnRunner.Advance` 的编排为 ① `Settle` → ② 成就与绝嗣判定 → ③ 覆盖写档 →
④ 备份 → ⑤ 全局成就落盘；**同月**既满足某成就判据又绝嗣时，成就在绝嗣**之前**判定
（先成就后绝嗣，`契约二十四` §6）。七项取值逐项断言：存续年数 = `EndedAt.Year − 1`；
传承代数 = `Family.Members.Max(Generation) + 1`；峰值资产 = `AchievementState.PeakTotalAssets`；
最高功名与进士总数**含已亡与外嫁归档**；族谱 = 全量 `Members.Select(Id)`（次序敏感）；
平均寿命走后 5 的算例。`AchievementState` 的持久字段数 **最多 8**（SC-014）。

## 场景 5：绝嗣正负例（外嫁不计）

`US3 / FR-026 / 规格书 §16.1 必测清单第 9 条 / SC-008`

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~ExtinctionTests"
```

**期望**：① 在册成员归零 ⇒ `Ending.IsEnded == true` 且 `EndedAt == CurrentDate`；
② **仅剩外嫁归档者** ⇒ **绝嗣**（外嫁不计）；③ 仅剩服刑者 ⇒ **不绝嗣**；
④ 仅剩待阙者 ⇒ **不绝嗣**；⑤ 仅剩患病者 ⇒ **不绝嗣**；⑥ `HeadBecameVacant`（家主空缺）
而仍有在册成员 ⇒ **不绝嗣**。另有断言：`EndedAt` 一旦成立即**冻结**，再次判定不改写。

## 场景 6：全局成就文件——去重、最早日期、兼容、隔离

`US1 / US4 / FR-017~FR-025 / SC-004~SC-006`

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~AchievementStoreTests"
```

**期望**：文件恒为 `<RootDirectory>/achievements.json`（JSON，**无** MessagePack 侧）；
同 `(Achievement, Difficulty)` 重复 `Flush` 只留**一条**且 `CompletedAt` 取**最早**那次
（后一次更晚的日期 MUST NOT 覆盖）；来源存档 UUID 保留最早那条的 `GameState.Id`（**非**档名）；
未知属性忽略、缺 `Version` 视作 `1`、`Version` 更高不失败；文件损坏 ⇒ 改名为
`achievements.corrupt-1.json`（序号递增，**不用时间戳**）、空档案继续、诊断可读、原文件不被删；
文件不存在 ⇒ 空档案且无诊断；`Origins` 段只写「绝嗣 / 百年世家」两类（**千秋万代不写**）。

## 场景 7：写失败不阻断回合，恢复后仍用原解锁年月

`US4 / FR-013 ⑧ / FR-024 / SC-007`

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~AchievementStoreFailureTests"
```

**期望**：目录不可写（只读目录或注入必然失败的替身）时 —— ① 月结算**照常完成**；
② 存档与备份**照常**（`ISaveService` 不受影响）；③ `Flush` 返回 `IsSuccess == false` 且
**不抛**；④ `state.Achievements.PendingUnlocks` **保留**；⑤ 恢复可写后下一次 `Advance` 的 ⑤
**一次性**写入，且 `CompletedAt` 是**原解锁年月**（不是重试当月的年月）。

## 场景 8：存档形状与版本 4 → 5

`US4 / FR-036 / FR-037 / SC-010~SC-013`

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~AchievementSaveFormatTests|FullyQualifiedName~EndedSaveTests"
```

**期望**：`SaveFileFormat.Version == 5`；`SaveStateDto` 键 **8 → 10**（`[8] achievements`、`[9] ending`）；
`AchievementStateDto` 恰 **8** 键、`AchievementMarkDto` **4**、`PendingUnlockDto` **5**、
`EndingStateDto` **3**、`ChronicleDto` **7**；`ending` 为 `null` 时 JSON 侧**不**标 `[JsonRequired]`、
二进制侧走 `TryReadNil()`；两格式信息集等价、逐字段往返一致（含 `Lifespans` 与 `Genealogy` 的**次序**）。
结束档：`Advance` 抛 `InvalidOperationException` 且**零状态变更**（随机未消费 / 无新账本条目 /
`CurrentDate` 未推进 / 存档 mtime 不变）；写盘 / 读回 / 改名 / 备份**仍可用**；
读回后 `Ending` 逐字段复原且再推进仍被拒。
既有版本断言**全部**用 `SaveFileFormat.Version` 常量（`SaveFormatContractTests.cs:37,56,270`、
`SaveStateValidatorTests.cs:356`、`SaveFailureTests.cs:156-157,183-184,477,503,585-586`）⇒ 0 处需改；
仅 2 处陈旧注释需同步（`SaveFileHeader.cs:21`、`SaveFailureTests.cs:23` 的「恒为 3」）。

## 场景 9：确定性与配置字面量守卫

`FR-012 / FR-005 / SC-003 / SC-014`

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~DeterminismTests|FullyQualifiedName~ConfigLiteralTests"
```

**期望**：同一种子两次推演（含成就判定与全局落盘）后 `GameState` 与 `achievements.json`
**逐字段 / 逐字节**相同；判定器**不**读墙钟、**不**消费随机、**不**依赖区域设置；
`AchievementPolicy` 的阈值只出现在配置类，`GameConfig.Achievements` 是**纯转发**（零字面量），
`ConfigLiteralTests` 全绿（新增登记项以守卫变红为准确认）。

## 场景 10：待接线登记与门禁四条（交付前必跑）

`US5 / FR-041~FR-044 / SC-002 / SC-012`

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~AchievementPendingWiringTests"
dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false
dotnet test  KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false
dotnet sln   KejuFuShengLu.slnx list
Get-ChildItem -Recurse -Filter *.sln | Where-Object { $_.FullName -notmatch '\\\.git\\' }
```

**期望**：`AchievementSource.Pending` 恰 6 条且逐条出现在 `tasks.md` 的待接线清单里；
grep 全仓**0 处**自造的「经商亏损 / 调试唤醒」规则常量或判定代码（FR-043 / SC-002）。
门禁四条：**0** 警告 **0** 错误；测试**全绿**（含 `G-01`~`G-08` 架构约束）；
恰好**六个**工程；最后一条**无输出**。

## 回写核对

`SC-013 / FR-038 / FR-039`

```powershell
Select-String -Path "specs\004-save-system\contracts\save-format.md" -Pattern "FormatVersion|MemberCount|十五条|Version"
Select-String -Path "specs\004-save-system\data-model.md" -Pattern "版本|键表"
Select-String -Path "科举浮生录规格书.md" -Pattern "裁决块：2026-10-18"
Select-String -Path "specs\007-family-lifecycle\data-model.md" -Pattern "成就|绝嗣"
```

**期望**：004 契约的键表已到 **10** 键、版本闸门已写 `== 5`、常量登记已含五个新 `*MemberCount`、
一致性校验已写 **22** 条（15 + 7）；004 的 `data-model.md` 版本行已同步；规格书已追加
「裁决块：2026-10-18（逻辑轨 ⑧ 的空白填充，Q1~Q6）」；007 的 `data-model.md` 已补
「⑧ 消费本特性事实表」的两行交叉引用。
**且**：`specs/*/tasks.md` 的既有条目、`specs/001-core-skeleton/implementation-notes.md`、
既有 `checklists/` **MUST NOT** 被回改（历史记录类工件）——`git diff` 中不得出现这三类的语义改写。
