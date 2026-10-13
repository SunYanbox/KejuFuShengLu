# 快速验证：科举周期

**Feature**: `005-exam-cycle` | **Date**: 2026-10-12 | **Spec**: [spec.md](./spec.md)

本文件是**可运行的人工验收指南**：每条场景给出「怎么跑、看什么、期望什么」。
它不重复契约内容——字段与签名看 [data-model.md](./data-model.md)，形状与次序看
[contracts/](./contracts/)（[契约十二：考试日历与报名](./contracts/exam-calendar.md) /
[契约十三：科举季结算步](./contracts/exam-session.md) /
[契约十四：科举数值登记](./contracts/config-registry.md)）。

## 0. 前置

- .NET 10 SDK（`global.json` 只锁 `10.0` 版本带，允许任意已安装的 `10.0.x`）。
- 本特性的全部测试**只经可设种子的 `IRandomService`**，**无界面、无文件系统、无网络**，
  MUST NOT 访问系统时钟或全局随机（章程原则 IV、G-07）。唯一例外是 US4 的存档往返
  （沿用 004 的测试沙箱与临时目录）。
- **受限宿主（Agent 沙箱）** 下必须带上 `AGENTS.md` 记录的两个开关。

```powershell
# 构建（期望：0 警告 0 错误）
dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false

# 测试（期望：全绿、0 skipped）
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false

# 只跑本特性的测试
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false `
  --filter "FullyQualifiedName~Rules.Exam"
```

**门禁四条**（与 `AGENTS.md` 一致，第三条不能省）：

```powershell
dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false     # 0 警告 0 错误
dotnet test  KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false
dotnet sln   KejuFuShengLu.slnx list                       # 恰好六个工程
Get-ChildItem -Recurse -Filter *.sln | Where-Object { $_.FullName -notmatch '\\\.git\\' }   # 期望无输出
git status --porcelain                                     # 期望：跑完后变更数 = 0（SC-010）
```

**全程 MUST NOT 启动界面、MUST NOT 启动游戏循环、MUST NOT 实现快进**（FR-029）：本特性可完全
脱离界面被验收；U1 的快进暂停只消费查询面（场景 5）。

---

## 场景 1：一届科年走完（US1 / FR-001~FR-011 / SC-001、SC-003）

**前提**：一名 16 岁白身成员，学业 60、仕天赋 60、家族尚无进士；难度 `普通`、现金充足；
随机源为固定种子（`RulesTestHarness.FixedRandomService`）。

**步骤**：科年 8 月报名解试 → 结算 8 月；强制掷骰命中 → 次年 3 月报名省试 → 结算 3 月；
强制命中 → 同年 4 月结算（无需报名）。

**期望**：
1. 8 月的录取率 = `0.01 × (0.4 + 0.012 × 60) × (1 + 60/400) × 1.0 × 1.0 = 1.288%`（US1 AS1）；
2. 中第后功名链**恰好**追加 3 条 `ExamPass` 记录：举人（8 月）→ 贡士（次年 3 月）→ 进士（4 月），
   当前功名 = 史末条（FR-010；SC-003）；
3. **殿试不掷录取骰**：无论进甲与否，参加者 100% 成为进士（Q1 裁决；FR-009）；
4. 4 月的记录按进甲结果落甲第：进甲 ⇒ `FirstClass` 且**名次非空**（状元/榜眼/探花之一）；
   未进甲 ⇒ 学业 ≥ 50 取 `SecondClass`、否则 `ThirdClass`，且**名次为空**（I-4）；
5. 及第当月置 `StatusFlag.AwaitingPost` 为真、`EntryTrack` = 一甲 `FirstClass` / 二甲 `SecondClass` /
   三甲 `ThirdClass`，待阙月数落在 `[6, 24]`；**本特性 MUST NOT 写 `Person.Rank`**（FR-011）；
6. 首个进士的家族当月 `HasShiStatus` 变真且**幂等**（FR-020）；
7. 整个科年**无需界面**即可完成，所有随机调用次数与 [契约十三 §1.3](./contracts/exam-session.md#13-整月随机消费次序fr-019-钉死单测逐次断言)
   的次序逐次一致。

**落点**：`tests/KFL.Tests/Rules/ExamSeasonTests.cs`、`ExamAdmissionTests.cs`、`ExamRegistrationTests.cs`。

---

## 场景 2：报名窗口、路费与弃考（US2 / FR-004、FR-005、FR-008、FR-017、FR-021 / SC-006）

**前提**：举人成员、科年次年 2 月（**省试前一个月**，非窗口）与 3 月（窗口）各一份状态；
现金分别置于「恰好够 20 贯」「19 贯」两侧。

**步骤**：
1. 在 2 月尝试报名 → 在 3 月报名 → 重复报名一次；
2. 结算 3 月，观察路费与账本；
3. 用现金 19 贯重跑，观察弃考分支；
4. 打开 `Family.AutoExamApplication`，从 7 月连续结算到次年 4 月。

**期望**：
1. 2 月报名被拒且**不改任何字段**；3 月报名成功；重复报名**幂等**、不抛异常、不消耗随机
   （FR-004；I-7）；
2. 报名与自动报名**都不消耗随机、都不读写账本**；`AppliedExam` 在当月结算结束后必为 `null`
   （FR-005；I-10）；
3. `普通` 难度扣 **20 贯**，落**一条** `LedgerCategory.ExamTravelCost` 资金类条目、`PersonId` = 本人、
   金额为负；**落第不退**（中第与落第时条目数各为 1）；
4. 四种难度的金额分别为 **10 / 20 / 40 / 80 贯**，与**难度支出系数无关**（FR-008；I-15/SC-006）——
   测试的期望金额 MUST 经 `ExamPolicy.ExamTravelCostGuan(difficulty)` 取得（契约十四 §3.4）；
5. 现金不足 ⇒ **弃考**：账本条目 **0** 条、解试/省试失败数**不变**、**不转贷款**、
   快照落 `ExamAbsenceReason.InsufficientTravelCost`（FR-008；I-9）；
6. 持续报考开启后，8 月与次年 3 月**自动报名并在同一结算内完成判定**；关闭后 MUST NOT 自动报名
   （FR-017）。

**落点**：`tests/KFL.Tests/Rules/ExamRegistrationTests.cs`、`ExamSeasonTests.cs`、`Core/LedgerTests.cs`。

---

## 场景 3：免解（US3 前半 / FR-013 / SC-004）

**前提**：某成员解试累计失败数恰为 **2** 与恰为 **3** 的两份状态（含跨科年）。

**步骤**：在科年 8 月报名解试并结算，逐次记录 `IRandomService` 的调用次数。

**期望**：
1. 失败数为 2 时**仍掷骰**；失败数为 3 时**随机消费 0 次**、直接按中第处理（I-8）；
2. 末条功名低于举人 ⇒ 追加一条「举人 / `ExamPass`」；**已达举人或更高** ⇒ MUST NOT 追加重复记录
   （仍算中第、仍清除标记）；
3. 取得资格后解试累计失败数**不再增长**（此后再跑一个科年，差异项 = 0）；
4. 免解**不跨级**：该成员的省试仍须在 3 月报名并付路费。

**落点**：`tests/KFL.Tests/Rules/ExamAdmissionTests.cs`、`ExamSeasonTests.cs`。

---

## 场景 4：特奏名（US3 后半 / FR-012 / SC-005）

**前提**：一名满 50 岁、省试累计失败数 = 6、无官阶、非待阙的举人；3 月结算必落第。

**步骤**：结算 3 月 → 观察询问 → **拒绝** → 再跑一个科年（再次落第）→ 观察第二次询问 → 在
第二次询问上**接受**。

**期望**：
1. 落第当月**恰好 1 次询问**，且询问本身**不改任何状态**（功名史、状态位、账本条目全部不变）
   （FR-012；I-14）；
2. **拒绝**后状态与功名史**逐字段不变**，下次省试落第后**再次**询问（次数 = 1）（Q3 裁决）；
3. **接受**后：`EntryTrack == AppointmentTrack.SpecialTribute`、置入待阙（6~24 月）、最终授官
   **L18**（由 `OfficialCareerPolicy.InitialRankOf` 单点决定）；
4. 接受后**功名记录条数不变**——MUST NOT 追加功名记录、MUST NOT 伪造进士记录；
5. 接受后该成员**退出科举**：不再被自动报名、不再进入判定。

**落点**：`tests/KFL.Tests/Rules/ExamSpecialTributeTests.cs`。

---

## 场景 5：查询面是纯函数（US4 / FR-018 / SC-007）

**前提**：一份状态快照（可用于调用前后比对）。

**步骤**：
1. 对连续 **3 个科年**的每一个月调用「该月考试级别」与「下一考试月」，与手算期望值逐月比对；
2. 在报名前与报名后各调用一次「该考试月合格且未报名者名单」。

**期望**：
1. 跨年边界逐月命中：`(1,8) 解试 → (1,9)~(2,2) 无 → (2,3) 省试 → (2,4) 殿试 → (2,5)~(4,7) 无 →
   (4,8) 解试`（差异项 = 0）；
2. `NextExamMonth` 返回**严格晚于**入参的第一个考试月，含跨年与跨科年边界；
3. 三个查询**调用前后状态逐字段相同**（纯函数）、**不消耗随机**；
4. 「合格未报名者名单」在报名后把该成员**移出**；非解试/省试月恒为空；
5. 科年与考试月**不出现在任何存档键表中**（派生值，MUST NOT 持久化）。

**落点**：`tests/KFL.Tests/Rules/ExamCalendarTests.cs`、`ExamRegistrationTests.cs`。

---

## 场景 6：逐乘区封印（SC-002）

**前提**：同一成员的两份状态，仅**一个**乘区不同（其余固定）。

**步骤**：分别改「学业」「仕天赋」「`HasShiStatus`」「贿赂乘数」，观察解试/省试录取率与殿试进甲率。

**期望**：
1. 基础率 1% / 10% / 5% 与学业系数 `0.4 + 0.012 × 学业`、仕天赋项 `1 + 仕/400` 逐项独立可断言；
2. 仕族加成 **×1.1 只作用于录取率**：进甲率在 `HasShiStatus` 真/假时**结果完全相同**（差异项 = 0；I-13）；
3. 封顶 **89.9999%** 在解试录取率、省试录取率、殿试进甲率**三处**生效，且**殿试录取率不受封顶影响**
   （100% 必中；I-12）；
4. 贿赂乘数本轮恒 `1.0`（k⁰）：公式经**同一入口的形参**接收，规则层**无按调用方分支的绕过路径**
   （FR-015、FR-016、章程原则 VI）。

**落点**：`tests/KFL.Tests/Rules/ExamAdmissionTests.cs`。

---

## 场景 7：存档往返与版本闸门（FR-024 / SC-009）

**前提**：一份把三个新字段（`jieShiFailureCount` / `shengShiFailureCount` / `appliedExam`）与
`autoExamApplication` 都设成**非默认值**的完整状态夹具（否则往返测试会静默通过）。

**步骤**：以**二进制**与 **JSON** 各存读一轮（临时目录，沿用 004 的测试沙箱），再对读回状态继续结算。

**期望**：
1. 新增字段在两种格式下**往返逐字段相等**（差异项 = 0），两格式承载**同一信息集**；
2. `SaveFileFormat.Version` = **2**；版本闸门对 `1`（旧）与 `3`（更新）**都拒绝**且早于主体解析
   （FR-024：MUST NOT 实现旧→新迁移）；
3. 键表与成员数常量同步：`PersonDto` 22 → **25**、`FamilyDto` 6 → **7**，且
   `二进制walker的成员数常量与DTO键数逐一相等` 全绿；
4. 读回后继续结算与「从未落盘」的基线逐字段相同。

**落点**：`tests/KFL.Tests/Infrastructure/{SaveFixtures,SaveStateComparer,SaveFormatContractTests,SaveFailureTests}.cs`。

---

## 场景 8：确定性与门禁（FR-026 / SC-001、SC-008、SC-010、SC-011）

**步骤**：用**同一固定种子**跑两遍完整科年；然后跑门禁四条与架构守卫。

**期望**：
1. 两次独立运行的**逐字段差异项 = 0**（SC-001）；随机消费次序逐次可断言（FR-019）；
2. 配置字面量违规数 = **0**：新增的 **4** 项小数（`0.01` / `0.05` / `0.012` / `0.899999`）已登记，
   整数规则值未登记，`GameConfig` 无自带数值字面量，路费期望值经配置成员取得（SC-008；
   [契约十四 §3](./contracts/config-registry.md#3-与-configliteraltestsregisteredvalues-的对照)）；
3. 门禁四条全过，且跑完后 `git status --porcelain` **无残留**（SC-010）；
4. 回写核对：`specs/002-*/contracts/monthly-settlement.md` 的「MUST NOT 存在」清单中
   **不再包含**「科举季触发」，七项回写目标同一提交内全部更新并已在 `tasks.md` 登记（SC-011；
   [plan.md](./plan.md) 的「回写义务清单」）。

**落点**：`tests/KFL.Tests/Rules/DeterminismTests.cs`、`Architecture/ConfigLiteralTests.cs`、
`Rules/SpecAnchorTests.cs`。
