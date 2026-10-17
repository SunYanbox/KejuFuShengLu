# 契约十三：科举季结算步（科举周期 · 逻辑轨 ⑤）

**Feature**: `005-exam-cycle` | **Date**: 2026-10-12 | **Spec**: [../spec.md](../spec.md) | **Data model**: [../data-model.md](../data-model.md)

> **数值一律不在此声明**；本契约只固定**步骤序列、随机消费次序、判定口径与快照形状**。数值真源与
> 登记见 [契约十四](./config-registry.md)。本步是 [契约三 §1](./../002-monthly-settlement-economy/contracts/monthly-settlement.md)
> 月末结算的**新整步**，位置由本契约 §1 固定。

---

## 1. 步骤位置与内部次序

### 1.1 在月末结算中的位置

| 条款 | 要求 |
| --- | --- |
| 1 | 科举季 MUST 成为月末结算的**固定新增步骤**，位于「⑦ 贷款」**之后**、「⑨ 推进时间（`GameState.AdvanceMonth`）」**之前**（**007 后记**：007 在第 ⑧ 位插入了「人口与健康推进」，本步顺移为 **⑨**） |
| 2 | 顺移后（**007 口径**）：`①` 提升待生效值 `②` 米价游走 `③` 惩罚推进 `④` 官吏推进 `⑤` 收入 `⑥` 生活费 `⑦` 贷款 **`⑧` 人口与健康推进（007）** **`⑨` 科举季** `⑩` 推进时间 |
| 3 | 本步在**推进时间之前**执行 ⇒ 判定用的是**考试当月**的日期与当月值（学业、仕天赋、难度、现金） |
| 4 | 本步 MUST NOT 调用 `AdvanceMonth`；MUST NOT 修改 `CurrentDate` |
| 5 | 逻辑轨 ⑦（成长/衰老/疾病/死亡）已由 007 交付并插在**本步之前**（第 ⑧ 步「人口与健康推进」），符合本条原始约束；逻辑轨 ⑧（绝嗣判定）MUST 在本步**之后**（FR-019）。**推论（007 起生效）**：⑧ 内死亡（含 ⑧-f 寿终与夭折）的成员在 ⑨ 的考生集合中**不存在**（SC-001 的次序断言） |
| 6 | 本步 MUST NOT 与其它任何步合并写；MUST NOT 把报名、路费、判定散进既有步 |

### 1.2 步内固定次序

```text
① 自动报名（仅当 Family.AutoExamApplication 为真；仅解试/省试月）
② 路费扣除与弃考（仅省试月）
③ 免解行使（仅解试月）
④ 录取 / 进甲判定（按 PersonId 升序逐人）
⑤ 及第入仕（按 PersonId 升序）
⑥ 特奏名询问（仅省试月）
```

| 条款 | 要求 |
| --- | --- |
| 1 | 六段 MUST 严格按上述次序执行；同一段内 MUST 按 `PersonId` **升序**逐人处理（确定性；FR-026） |
| 2 | **非考试月**：①②③④⑤⑥ 全部跳过，随机消费 **0** 次，状态变更 **0** 处，返回 `Level == null` 的空快照 |
| 3 | 候选集 = 持有「本月考试级别」报名标记者（含 ① 刚自动报名的），按 `PersonId` 升序 |
| 4 | ③ 免解 MUST 在 ④ 的掷骰**之前**判定（否则会多消耗一次随机，SC-004 要求 0 次） |
| 5 | ④ 全部人员的判定 MUST 先完成，然后才进入 ⑤；MUST NOT 逐人「判定 → 立刻入仕」交错 |
| 6 | ⑥ 仅省试月产生；解试月与殿试月 MUST 不产生任何询问 |

### 1.3 整月随机消费次序（FR-019 钉死，单测逐次断言）

```text
米价系数游走（结算 ②）                       —— 1 次
考课掷骰（结算 ③-d，晋升判定）                —— 0 或 1 次 / 人
储蓄利率（结算 ④，仅 1 月）                   —— 0 或 1 次
贷款计息利率（结算 ⑥）                        —— 0 或 1 次
科举判定（本步 ④，逐人）：
    1 次 NextDouble()   —— 录取（解试/省试）或进甲（殿试）
    进甲者紧接 1 次 Next(1, 4) —— 一甲名次
及第入仕待阙（本步 ⑤，逐人，PersonId 升序）：
    1 次 Next(6, 25)    —— 待阙月数（经 AppointmentEntry 入口）
```

| 条款 | 要求 |
| --- | --- |
| 1 | **报名阶段（①）MUST NOT 消耗随机** |
| 2 | **被跳过的成员**（② 判定前资格不符、② 现金不足、③ 免解者）MUST NOT 消耗随机 |
| 3 | **殿试录取不掷骰**：进殿试者 100% 成为进士，故 ④ 对殿试只掷**进甲**一次（Q1 裁决） |
| 4 | 殿试进甲者**紧接** 1 次名次掷骰；未进甲者 MUST NOT 掷名次 |
| 5 | 本步的全部随机 MUST 经注入的 `IRandomService`；MUST NOT 使用 `System.Random` 或任何时钟 |
| 6 | 相同种子 + 相同操作序列 ⇒ 逐字段完全可复现（FR-026） |

---

## 2. ② 判定前复核（FR-006）

进入 ② / ④ 之前，对每位候选成员**重新**复核：

| 条款 | 要求 |
| --- | --- |
| 1 | 复核内容 = 契约十二 §3.2 的七项资格 ∧ `TargetLevelOf(person) == 本月级别` |
| 2 | 不再合格者：**跳过**（不掷骰、不计败、不收路费、不追加功名）并**清除报名标记** |
| 3 | 跳过 MUST 在快照里可识别：`ExamAbsenceLine(personId, level, ExamAbsenceReason.IneligibleBeforeJudgement)` |
| 4 | 取消失败数 MUST 不变（跳过 ≠ 落第） |
| 5 | 复核 MUST 用**当月**的功名（`CurrentDegree` 取史末条），以确保「判定前功名被外力改写」也能被正确分级 |

---

## 3. 省试路费与弃考（FR-008、FR-021）

### 3.1 金额与口径

| 条款 | 要求 |
| --- | --- |
| 1 | 金额 = `ExamPolicy.ExamTravelCostGuan(state.Difficulty)`：简单 / 普通 / 困难 / 地狱 = **10 / 20 / 40 / 80 贯**（规格书 §6） |
| 2 | MUST NOT 乘**难度支出系数**（生活费那一套 0.6/1.0/1.1/1.3）——稳中是难度档**直接**取金额 |
| 3 | 在 ④ **判定前**扣除 |
| 4 | **落第不退**：判定后 MUST NOT 补回、MUST NOT 追加反向条目（由「判定前扣、事后不补」结构性成立） |
| 5 | 只有**省试**月收费；解试与殿试 MUST NOT 收任何路费 |

### 3.2 扣付与弃考

| 条款 | 要求 |
| --- | --- |
| 1 | **门槛 = 现金**：`state.Economy.Treasury.Cash < 路费` ⇒ **弃考** |
| 2 | 弃考动作：清除报名标记、不掷骰、**失败数不变**、**不落账本条目**、**不转贷款**、快照落 `ExamAbsenceReason.InsufficientTravelCost` |
| 3 | 付得起时：`state.Economy.Apply(LedgerCategory.ExamTravelCost, person.Id, -路费, 考试月)` |
| 4 | 因条款 1 保证了 `Cash >= 路费`，`Apply` 的「现金 → 储蓄」扣付**必然只动现金** —— MUST NOT 动储蓄、MUST NOT 转贷款 |
| 5 | MUST NOT 使用 `PaymentPrimitive`（现金 → 储蓄 → 余额转贷款）：FR-008 明令「不转贷款」 |
| 6 | 路费 MUST NOT 按人「部分支付」；MUST NOT 出现「付了一半」的中间态 |
| 7 | 无合格缴费人 ⇒ 账本条目 0 条；`SettlementResult.Entries` 中 `ExamTravelCost` 条目数 MUST 等于实际收费人数 |

### 3.3 账本类别 `LedgerCategory.ExamTravelCost`

| 条款 | 要求 |
| --- | --- |
| 1 | 新增类别 MUST 是**资金类**（`LedgerEntryKind.Treasury`）且**恒为负**（`CreditTargetOf == null`） |
| 2 | MUST NOT 用事件类表达；MUST NOT 新建第二套账 |
| 3 | 类别 MUST 注册进既有类别元数据（规格书 §12.3；[契约四 §1](./../002-monthly-settlement-economy/contracts/ledger.md)） |
| 4 | 归属角色 = 考生本人（`LedgerEntry.PersonId = person.Id`）；条目年月 = 考试月 |
| 5 | 金额单位一律为「文」（`Money` 内部口径），键表不含新增金额字段 |

---

## 4. 录取率与进甲率

**入口**：`KFL.Rules/Exam/ExamAdmission.cs`（全为纯函数，MUST NOT 读全局态）。

### 4.1 解试 / 省试录取率

```text
录取率 = 基础率 × 学业系数 × (1 + 仕天赋 / 仕天赋除数) × 贿赂乘数 × 仕族加成

基础率   = 解试 ExamPolicy.JieShiBaseRate（1%）
           省试 ExamPolicy.ShengShiBaseRate（10%）
学业系数 = ExamPolicy.StudyFactorBase + ExamPolicy.StudyFactorPerPoint × person.Study
           （0.4 + 0.012 × 学业）
仕天赋除数 = ExamPolicy.OfficialdomDivisor（400）
仕族加成 = HasShiStatus ? ExamPolicy.ShiStatusAdmissionBonus（1.1） : 1.0
结果 clamp 到 [0, ExamPolicy.DiceRateCap]（89.9999%）
```

### 4.2 殿试进甲率（**不掷录取骰**）

```text
① 进殿试者 100% 成为进士（**不掷骰**，MUST NOT 受封顶影响）
② 进甲率 = ExamPolicy.ImperialPromotionBaseRate（5%）× 学业系数 × (1 + 仕天赋 / 除数) × 贿赂乘数
    —— **MUST NOT 乘仕族加成**
    —— clamp 到 [0, ExamPolicy.DiceRateCap]
③ 进甲 ⇒ ImperialClass = FirstClass，且**同一记录**带 1~3 均匀 roll 的名次
④ 未进甲 ⇒ 学业 >= ExamPolicy.SecondClassStudyThreshold（50）⇒ SecondClass，否则 ThirdClass；名次为空
```

### 4.3 逐乘区条款（SC-002 的「封印」）

| 条款 | 要求 |
| --- | --- |
| 1 | 学业与仕天赋取**考试月当月值**（`Person.Study` / `Person.Talents.Officialdom`） |
| 2 | 仕族加成（×1.1）**只作用于录取率**（解试/省试）；进甲率在 `HasShiStatus` 真/假时结果 MUST 完全相同（差异项 = 0） |
| 3 | 封顶只作用于**需掷骰的概率**：解试录取率、省试录取率、殿试进甲率三处；MUST NOT 作用于「殿试必中」 |
| 4 | 贿赂乘数经**同一入口**的形参 `bribeMultiplier` 接收实际值（FR-016）：005 的 `ExamPolicy.NeutralBribeMultiplier` **占位已作废（006 落地后删除）**——006 已落地派生 `BribeRules.MultiplierOf(ExamLevel, Person)`（`kⁿ`，`k = BribePolicy.EffectMultiplierK(level)`：解试 1.6 / 省试 1.3 / 殿试 1.1；`k⁰` 由 `BribePolicy.UnitMultiplier = 1m` 唯一给出）。`AdmissionRate` / `ImperialPromotionRate` 的**签名、公式与 clamp 一字未动** |
| 5 | 规则层 MUST NOT 提供按调用方分支的绕过路径；「无限制」能力（U4）MUST 经装配期显式注入的配置或策略提供（FR-015、章程原则 VI） |
| 6 | 各乘区 MUST 可独立断言：基础率、学业系数、仕天赋项、仕族加成、贿赂乘数、封顶逐项各有单测锚点 |
| 7 | 概率计算与掷骰 MUST 分离：`AdmissionRate` / `ImperialPromotionRate` 只算概率；掷骰由本步用 `IRandomService` 执行 |

### 4.4 名次 roll

| 条款 | 要求 |
| --- | --- |
| 1 | `RollPlacement(random)` = `random.Next(1, 4)` **恰好 1 次**（整数均匀、含两端点、等概率） |
| 2 | 映射 `1 → ZhuangYuan`、`2 → BangYan`、`3 → TanHua` |
| 3 | 名次与学业、天赋、仕族、贿赂**均无关**（封顶命中时名次仍均匀） |
| 4 | 只有一甲记录可带名次（`DegreeRecord` 构造期保证） |

---

## 5. 免解（FR-013）

| 条款 | 要求 |
| --- | --- |
| 1 | 判定：仅**解试月**，`person.JieShiFailureCount >= ExamPolicy.JieShiExemptionFailureThreshold`（3） |
| 2 | 该类成员 MUST NOT 掷录取骰，直接按**中第**处理（**随机消费 0 次**） |
| 3 | 「中第」若当前功名**低于举人** ⇒ 追加一条 `JuRen / ExamPass` 记录；**已达举人或更高** ⇒ MUST NOT 追加重复记录（仍算中第、仍清除标记） |
| 4 | 取得资格后解试累计失败数 MUST NOT 继续增长（此后每次解试都免掷骰，不再走落第分支） |
| 5 | 免解**不跨级**：省试仍须在报名月报名（契约十二 §4）并付路费（§3） |
| 6 | 免解行使 MUST 记入快照：`Exemptions`（`PersonId` 列表）与该人的 `ExamOutcomeLine.Exempted = true` |
| 7 | 门槛判定用**进入本步之前**的失败数（免解当月不因免解而 +1） |
| 8 | 若免解者后续因**连坐降级**回到举人以下，且失败数仍 >= 3 ⇒ 再次解试时**仍然**免掷骰（计数不因降级清零，也 MUST NOT 因调试改写清零）。连坐降级的施加者是**逻辑轨 ⑥**（[006 契约四](../../006-bribery-punishment/contracts/punishment.md)），本特性只被动消费其降级结果 |

---

## 6. 功名记录追加（FR-010）

| 条款 | 要求 |
| --- | --- |
| 1 | 每一次**中第** MUST 向功名变迁史追加**恰好一条** `DegreeRecord`（经既有 `Person.AppendDegree`） |
| 2 | 记录四要素：`ChangedAt` = 考试月；`Cause` = `DegreeChangeCause.ExamPass`；`Level` = 举人/贡士/进士；`ImperialClass` 仅进士且仅二/三甲为非空（一甲为 `FirstClass` 且带名次） |
| 3 | MUST NOT 改写或删除既有记录；「当前功名」= 史末条；史为空 ⇒ 白身 |
| 4 | 同一年 **3 月省试 → 贡士** 与 **4 月殿试 → 进士** MUST 各追加一条，MUST NOT 合并或覆盖 |
| 5 | 一次完整链条（白身 → 举人 → 贡士 → 进士）恰好追加 **3** 条 `ExamPass` 记录 |
| 6 | 落第 MUST NOT 追加任何功名记录 |
| 7 | 一甲名次与甲第的兼容性由 `DegreeRecord` 既有四不变量保证：**名次非空 ⟺ 甲第 = 一甲** |
| 8 | 本特性 MUST NOT 使用 `DegreeChangeCause` 之外的既有取值（一律 `ExamPass`）；MUST NOT 引入未登记的功名变化原因 |

---

## 7. 及第入仕与 `HasShiStatus`（FR-011、FR-020）

### 7.1 及第入仕

| 条款 | 要求 |
| --- | --- |
| 1 | 仅**殿试月**（(N+1, 4)）产生入仕 |
| 2 | 入口 = 003 的 `AppointmentEntry.BeginForImperialGraduate(person, 考试月, random)`，在 ④ 判定**之后**、按 `PersonId` 升序调用 |
| 3 | 入仕途径由**已追加的进士记录的 `ImperialClass`** 决定：一甲 → `FirstClass`、二甲 → `SecondClass`、三甲 → `ThirdClass` |
| 4 | 本特性 MUST NOT 自行写 `Person.Rank` / `Person.MonthsInOffice` / 账本；MUST NOT 参与待阙递减与授官 |
| 5 | 待阙月数 = 6~24（`OfficialCareerPolicy.NextAwaitingPostMonths`，经入口掷骰）；初始官阶 = 一甲 **L11** / 二甲 **L13** / 三甲 **L15**（由 `OfficialCareerPolicy.InitialRankOf` 单点决定） |
| 6 | 当月调用：及第当月 MUST 置入待阙（`StatusFlag.AwaitingPost` 为真、`EntryTrack` 对应）；**当月不递减**，次月起走 |
| 7 | 快照记录 `ExamAppointmentLine(personId, track, awaitingPostMonths)` |
| 8 | 已在待阙或已有官阶者 MUST NOT 被重复入仕（入口自身校验） |

### 7.2 `Family.HasShiStatus`

| 条款 | 要求 |
| --- | --- |
| 1 | 家族产生**首个进士**的当月 MUST 置 `Family.HasShiStatus = true` |
| 2 | MUST **幂等**：已为 `true` 时 no-op |
| 3 | 置位当月快照 `HasShiStatusBecomesTrue = true`（仅首次） |
| 4 | 此后解试/省试录取率 × 1.1（§4.1）；殿试进甲率**不受影响**（§4.3 条款 2） |
| 5 | 本特性是该字段在仓库中的**首个生产者**；对贷款月划扣比例（仕 20%）的影响**自次月**生效（当月结算的 ⑥ 贷款步已在本步之前执行） |

---

## 8. 特奏名询问（判定面；入口面见契约十二 §6.2）

| 条款 | 要求 |
| --- | --- |
| 1 | 触发：仅**省试月**，落第者满足 `ExamRegistration.SpecialTributeEligible` 时，在**落第当月**产出**一次**询问 |
| 2 | 询问 MUST NOT 改任何状态（不改功名、不改状态位、不落条目、不掷骰） |
| 3 | 询问 MUST 记入快照 `SpecialTributeInquiries`（`PersonId` 列表，按升序） |
| 4 | 接受/拒绝的落地见契约十二 §6.2 条款 3~7；接受后 MUST NOT 再被自动报名或进入判定 |
| 5 | 每个科年**至多一次**询问；同一人跨科年可由多次省试落第各自触发 |
| 6 | 快照的 `SpecialTributeInquiries` MUST 能表达「至少一次」并可被逐次计数 |

---

## 9. 快照与断言面（FR-022、FR-027）

`KFL.Rules/Exam/ExamSeasonSnapshot.cs`（形状见 [data-model §3.5](../data-model.md#35-examseasonsnapshotkflrulesexamexamseasonsnapshotcs)）；
作为 `SettlementResult` 的新增子段 `ExamSeason`。

| 条款 | 要求 |
| --- | --- |
| 1 | 快照 MUST 为**增量**：只含本月发生的事；MUST NOT 回写 `GameState`；MUST NOT 成为第二真源 |
| 2 | 快照 MUST 逐项表达：① 自动报名与弃考 ② 免解行使 ③ 逐人中第/落第与级别 ④ 一甲名次与甲第 ⑤ 累计失败数变化 ⑥ 及第入仕的待阙月数 ⑦ 特奏名询问 ⑧ `HasShiStatus` 置位 |
| 3 | 全部集合按 `PersonId` 升序；MUST 为只读 |
| 4 | 路费金额 **不进快照**（已在 `Entries` 的 `ExamTravelCost` 条目里，避免第二个金额真源）；弃考无条目，故用 `Absences` 表达 |
| 5 | 非考试月 ⇒ `Level == null` 且全部集合为空、`HasShiStatusBecomesTrue == false` |
| 6 | 3 月省试与 4 月殿试 MUST 各自产生独立快照，MUST NOT 合并 |
| 7 | 事件流供逻辑轨 ⑧ 消费（FR-027）：中第/落第/免解/特奏名询问与接受/一甲名次，含成员、级别、年月、甲第、名次 |
| 8 | 可读计数：`ExamOutcomeLine.FailureCountAfter` 给出两个累计失败数的变化后值；「清白传家」的行贿计数**已由逻辑轨 ⑥ 交付**——查询面 `PunishmentQueries.WasBribeFreeAtImperialPass(Person)`（读 `Person` 的三个行贿计数），本特性 MUST NOT 自建。成就本身的**解锁**仍未由 ⑥ 交付（逻辑轨 ⑧） |

---

## 10. 明确不包含

| # | 项 | 归属 |
| --- | --- | --- |
| 1 | 成员成长 / 衰老 / 疾病 / 死亡（将来插在本步**之前**） | 逻辑轨 ⑦ |
| 2 | 成就解锁与绝嗣判定（本步只产出事件流与计数） | 逻辑轨 ⑧ |
| 3 | 快进循环、「下月」按钮、调试控制台、卡片与统计 | 界面轨 U1/U2/U3/U4 |
| 4 | 存档迁移与向下兼容 | 契约十 §8 |
| 5 | 官名表；跨 .NET 版本的随机数稳定性 | 规格书未定义 |

> **006 后记（2026-10-13）**：上表原先的第 1 行「贿赂的累进金额、被抓概率、惩罚矩阵、连坐、服刑」已由
> `006-bribery-punishment`（逻辑轨 ⑥）交付（`BribePolicy` / `PunishmentPolicy` 与月度结算第 ③ 步
> `PunishmentAdvance.Run(state)`），故从本清单移除；本契约只经**同一入口**的 `bribeMultiplier` 形参
> 消费其派生乘数（§4.3 条款 4），并被动消费连坐降级的结果（§5 条款 8）。

本契约**不含**任何数值字面量；`SettlementResult` 的两条既有不变量（`Entries` 逐条相同、
`TreasuryPool` 差 = 资金类条目和）在本步仍然成立。
