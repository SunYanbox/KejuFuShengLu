# 快速验证：贿赂全链路

**Feature**: `006-bribery-punishment` | **Date**: 2026-10-13 | **Spec**: [spec.md](./spec.md)

本文件是**可运行的人工验收指南**：每条场景给出「怎么跑、看什么、期望什么」。
它不重复契约内容——字段与签名看 [data-model.md](./data-model.md)，形状与次序看
[contracts/](./contracts/)（[契约十五：行贿](./contracts/bribery.md) /
[契约十六：惩罚推进与连坐](./contracts/punishment.md) /
[契约十七：数值登记](./contracts/config-registry.md)）。

## 0. 前置

- .NET 10 SDK（`global.json` 只锁 `10.0` 版本带，允许任意已安装的 `10.0.x`）。
- 本特性的全部测试**只经可设种子的 `IRandomService`**，**无界面、无文件系统、无网络**，
  MUST NOT 访问系统时钟或全局随机（章程原则 IV、G-07）。唯一例外是场景 7 的存档往返
  （沿用 004 的测试沙箱与临时目录）。
- **受限宿主（Agent 沙箱）** 下必须带上 `AGENTS.md` 记录的两个开关。

```powershell
# 构建（期望：0 警告 0 错误）
dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false

# 测试（期望：全绿、0 skipped）
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false

# 只跑本特性的测试
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false `
  --filter "FullyQualifiedName~Rules.Bribe|FullyQualifiedName~Rules.Punishment|FullyQualifiedName~Rules.CollectivePunishment"
```

**门禁四条**（与 `AGENTS.md` 一致，第三条不能省）：

```powershell
dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false     # 0 警告 0 错误
dotnet test  KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false
dotnet sln   KejuFuShengLu.slnx list                       # 恰好六个工程
Get-ChildItem -Recurse -Filter *.sln | Where-Object { $_.FullName -notmatch '\\\.git\\' }   # 期望无输出
git status --porcelain                                     # 期望：跑完后变更数 = 0（SC-010）
```

**全程 MUST NOT 启动界面、MUST NOT 启动游戏循环、MUST NOT 实现快进**：本特性可完全脱离界面被验收；
U1 的「贿赂入口在快进期间置灰」只消费查询面（场景 6）。

---

## 场景 1：行贿的窗口、金额与被抓概率（US1 / FR-003~FR-007 / SC-002）

**前提**：普通难度、科年 8 月（`GameState.CurrentDate` 尚未结算）、一名白身考生、现金充足；
随机源为固定种子（`RulesTestHarness.FixedRandomService`）。

**步骤**：在 6 月尝试行贿解试 → 在 7 月尝试 → 回到 8 月连续行贿 n = 1/2/3 次，逐次记录
`IRandomService` 的调用次数。

**期望**：
1. 6 月（窗口外，前 2 个月）被拒且**资金、计数、乘数、账本、随机消费次数全部不变**
   （FR-003；I-3）；
2. 7 月与 8 月**照常受理**（窗口 = 考试当月及前 1 个月）；殿试的窗口覆盖**省试月本身**（§7.1）；
3. 第 n 次应付金额 = `100 × (1 + 0.2×(n−1)) × 1.0`（普通支出系数）= **100 / 120 / 140 贯**；
   被抓概率 = `0.05 × (1 + 0.2×(n−1)) × 1.0` = **5% / 6% / 7%**（FR-005、FR-006；SC-002）；
4. 每次行贿**恰好**扣 1 笔款、落**恰好 1 条** `LedgerCategory.BribeExpense` 资金类条目
   （`PersonId` = 本人、金额为负）、消费**恰好 1 次** `NextDouble()`（FR-007）；
5. 四种难度的金额分别为 `×0.6 / ×1.0 / ×1.1 / ×1.3`——期望值 MUST 经
   `BribeRules.PayableAmount` 取得（禁止字面量；[契约十七 §4](./contracts/config-registry.md#4-与-configliteraltestsregisteredvalues-的对照)）；
6. 被抓概率累进到 `≥ 1.0` 时按 **100%（必定被抓）**处理，**不出现「概率 > 100%」**——
   地狱难度殿试第 2 次即 `0.35 × 1.2 × 3.0 = 1.26 → 1.0`（FR-006；spec Edge Cases）；
7. 同一考生同月可行贿多次，每次金额与概率**各自按当次 n 累进**、各消费 1 次随机。

**落点**：`tests/KFL.Tests/Rules/BribeEntryTests.cs`。

---

## 场景 2：乘数 kⁿ、被抓不清零与触顶置灰（US1 / FR-008~FR-010 / SC-003）

**前提**：同一考生在解试行贿 n = 1/2/3 次；另一份状态把乘数推到触顶；第三份状态让第 2 次行贿**被抓**。

**步骤**：连续行贿 3 次并断言乘数 → 把乘数推到触顶后再调用行贿入口 → 第 2 次被抓后继续结算并复查计数。

**期望**：
1. 第 1/2/3 次后解试乘数 = `1.6¹ / 1.6² / 1.6³ = 1.6 / 2.56 / 4.096`（FR-008）；
2. `ExamAdmission.AdmissionRate(…)` 收到的乘数形参**等于该值**（经同一入口、无公式复制）；
3. 触顶（最终录取率/进甲率命中 `0.899999m`）后：查询面报「已封顶（入口置灰）」，
   **直接调用行贿入口被拒**且状态**逐字段不变**、随机消费 = **0**（FR-010；SC-003）；
4. 第 2 次**被抓**后：该级别计数 = **2**、乘数仍 = `1.6²`、账上**无**退款或补偿条目
   （Q1 = B；I-4），且服刑使其在服刑期内无法报名（005 的资格判定已排除 `ServingSentence`）；
5. 落榜时**没有**退款条目、乘数保留、计数不变（下次从第 4 次起）（FR-009、US1 AS5）；
6. `ExamPolicy.NeutralBribeMultiplier` 占位**不存在**（已删除），`k⁰ = 1.0` 只由
   `BribeRules.MultiplierOf` 在计数为 0 时给出（[../research.md](./research.md) R-05）。

**落点**：`tests/KFL.Tests/Rules/BribeEntryTests.cs`、`ExamAdmissionTests.cs`、`SpecAnchorTests.cs`。

---

## 场景 3：12 格矩阵逐格与罚金口径（US2 / FR-011~FR-016 / SC-004、SC-005）

**前提**：以夹具构造「某难度 × 某级别 + 一道被抓事件」，四种难度 × 三个级别共 **12** 份；
另备两份金额夹具（现金 + 储蓄 = 30 贯 ∧ 比例罚金 = 120 贯；总资产为负）。

**步骤**：逐格调用 `PunishmentImposer.Apply`（对**每一格**），逐项断言服刑月数、罚金、禁考与禁升计时；
再跑两份金额夹具。

**期望**：
1. 12 格**逐格**命中：服刑月数 = 年数 × 12（`CASE：简单 60/36/12、普通 180/60/36、
   困难 240/120 + 死刑、地狱 240/120 + 死刑+连坐`）；罚金为比例或固定额（§7.3；SC-004）；
2. **简单档三格**的亲属禁考计时与禁升计时**均为 `null`**（「无禁考」）；**困难与地狱的解试/省试
   三格**的禁升计时为 `null`（「只禁考」）——违例各 **0** 条（FR-015；SC-004）；
3. 普通解试**无**任何亲属禁考计时；简单解试**无罚金**；
4. 罚金基数 = `TotalAssetsCalculator.Of(economy)`（**单一访问器**），且 12 月工出身 bonus 的
   「总资产为正」判定**复用同一位**（FR-011；差异项 = 0）；
5. 比例罚金在超大总资产下**恰为 50000 贯**（全域封顶，对最终金额生效）（FR-012；SC-005）；
6. 30 贯夹具 ⇒ 现金与储蓄清零、**贷款本金 +90 贯**、`Fine` 条目金额 = **池内实际付出额 30 贯**
   （缺口 90 贯转贷款、**不落**资金条目；`PaymentResult.Total` 仍 = 120 贯）、任何池余额 **≥ 0**
   （[plan.md](./plan.md) 勘误 #4：资金类条目金额 = 本次的资金池变动额）；
7. 总资产为负 ⇒ 罚金金额 = **0** 且**不落 0 金额条目**（FR-013；I-9）；
8. 账本条目：每次罚金**恰好 1 条** `LedgerCategory.Fine`（资金类、恒为负、`PersonId` = 当事人）；
   **服刑事件不落账本**——它落在惩罚快照的 `Sentences` 里（[plan.md](./plan.md) 勘误 #1）。

**落点**：`tests/KFL.Tests/Rules/PunishmentMatrixTests.cs`、`Core/LedgerInvariantTests.cs`。

---

## 场景 4：服刑推进、暂停与刑满释放（US3 / FR-017~FR-021 / SC-006）

**前提**：把某成员置为「服刑 60 月」（L13 官员，`Rank` 非 null）并给出 `PersonId` 升序的多人夹具；
另一份夹具同时给他禁考 48 月；第三份是待阙剩余 20 月者被判服刑 12 月。

**步骤**：连续跑 **60** 次结算，逐月记录三种计时、待阙/在职计时、政绩、功名史条数、服刑人数。

**期望**：
1. 每次结算（第③步）三种计时各递减 **1**；第 60 次结算后 `ServingSentence` = 假、
   `SentenceRemainingMonths` = `null`、官阶 **L13 → L14**（降 1 级）、**降级恰 1 次**、
   功名史条数**不变**（FR-020；SC-006）；
2. 服刑期间待阙/在职计时与政绩**逐月不变**（暂停 = 不推进、不重置）；释放**次月**起恢复推进（FR-018）；
3. 服刑期间该成员**不计口**（不耗生活费、不产生收益与功名），但仍在家族列表与卡片上、
   剩余月数可经查询面读出（FR-017）；
4. 禁考与服刑**独立并行**：同时被判禁考 48 月者在服刑期释放后**仍在禁考期**，剩余月数按 48 月
   独立递减；释放当月**不得报名**，禁考期满后可考（FR-019、FR-020；US3 AS2/AS5）；
5. 在任官员处于禁考期时**照常在职、照常领俸**（禁考 MUST NOT 使他丢官或丢俸）（FR-019）；
6. 待阙者（剩余 20 月）服刑 12 月期间待阙剩余月数**逐月不变**，释放后从 20 月继续（FR-018）；
7. **L18 官员**服刑期满后官阶**维持 L18**（不再降）（FR-020；US3 AS1'）；
8. 某月恰有 **3** 名成员处于服刑状态时，查询面 `PrisonerCount` = **3**、
   快照 `PrisonerCount` = 3，且本特性 **MUST NOT** 自行解锁「囹圄之灾」（FR-029④、FR-030）。

**落点**：`tests/KFL.Tests/Rules/PunishmentAdvanceTests.cs`、`Rules/DeterminismTests.cs`。

---

## 场景 5：地狱殿试连坐三分支、免死与外嫁追及（US4 / FR-022~FR-026 / SC-007）

**前提**：地狱难度 + 当事人处于殿试月 + 已被抓获；家族含五类亲属各一人——一名无功名无官职的父亲、
一名举人无官职的儿子、一名有官职的配偶、一名未成年且无功名无官职的孩子、
一名**已出嫁归档**的女儿；另加一名兄弟与一名孙辈。

**步骤**：调用 `PunishmentImposer.Apply`，逐人断言状态、功名史、死亡与归档标志、计时。

**期望**：
1. **三分支各命中 1 条**：父亲被处死（置已亡、归档、进族谱、计入死亡结算）；
   儿子降为白身 + **恰 1 条** `PunishmentDemotion` 记录；有官职者受**全族 48 月**覆盖（FR-023；SC-007）；
2. **免死例外成立**：配偶有官职 ∧ 孩子未成年 ⇒ 该孩子**就连坐分支的后果而言状态逐字段不变**
   （不置已亡、不降功名、无 `CollectivePunishmentFact`；差异项 = 0），
   但全族禁考禁升 48 月**照常适用**（他的两条禁令计时照常写入；期内不得报名、期满可考）（FR-024）；
3. **已出嫁归档的女儿被跨族追及**：她照样被处理，并产出「殃及池鱼」所需的事实（FR-025）；
4. **范围封印（只针对连坐）**：兄弟与孙辈**不被连坐**——不处死、不降功名、不产生
   `CollectivePunishmentFact`；但两者仍是「在世在册成员」，故**照常受全族 48 月禁令覆盖**
   （FR-016；SC-007；连坐范围与全族禁令范围是两次不同的范围）；
5. **当事人本人**：死刑**立即执行**（置已亡并归档），**MUST NOT** 建立服刑计时（FR-026）；
6. **其余 11 格**：连坐触发数 = **0**（SC-007）；死刑格以外的 11 格 MUST NOT 产生
   `CollectivePunishmentFact`；
7. 全族禁考禁升生效后 `PunishmentQueries` 报「全族禁考/禁升生效」，
   `PunishmentCommands.ClearFamilyBans` 一次清除**全部在册成员**的相应计时（FR-021、FR-031）；
8. 惩罚快照 `FamilyBanDeclared` = 真（供「斯文扫地」），本特性 MUST NOT 自行解锁任何成就。

**落点**：`tests/KFL.Tests/Rules/CollectivePunishmentTests.cs`。

---

## 场景 6：查询面是纯函数（FR-029、FR-030 / SC-008）

**前提**：一份状态快照（可用于调用前后逐字段比对）；构造「同月 3 人服刑」的场景。

**步骤**：对 `PunishmentQueries` 与 `BribeRules` 的七个查询各调用一次，比对调用前后的 `GameState`
与 `IRandomService` 调用次数。

**期望**：
1. ①「某考生 × 某级别的当前应付金额 / 当前被抓概率 / 是否已触顶」、②「某成员三计时剩余月数 +
   是否有官职」、③「某考生在某月是否处于行贿窗口」、④「某月服刑人数」——四类查询
   **调用前后 `GameState` 逐字段差异项 = 0 且随机消费 = 0**（SC-008）；
2. ④ 在「同月 3 人服刑」的场景下返回 **3**（SC-008 的后半）；
3. `PunishmentQueries` 的**纯函数性**是其唯一存在理由：U1 的快进置灰、U2 的徽章、U4 的按钮
   都只消费它（章程原则 III、原则 VI）；
4. 本特性**不新增任何界面接口**：`KFL.Presentation` 与 `KFL.App` 一行不动。

**落点**：`tests/KFL.Tests/Rules/BribeQueryTests.cs`。

---

## 场景 7：存档往返与版本闸门（FR-032 / SC-009）

**前提**：一份把三个新计数与整本 `PunishmentJournal` 都设成**非默认值**的完整状态夹具
（`SaveFixtures.RichState()` 必须扩展——否则往返测试会静默通过）。

**步骤**：以**二进制**与 **JSON** 各存读一轮（临时目录，沿用 004 的测试沙箱），再对读回状态继续结算。

**期望**：
1. 新增字段在两种格式下**往返逐字段相等**（差异项 = 0），两格式承载**同一信息集**；
2. `SaveFileFormat.Version` = **3**；版本闸门对 **2**（旧）与 **4**（更新）**都拒绝**且早于主体解析
   （FR-032：MUST NOT 实现旧→新迁移）；
3. 键表与成员数常量同步：`PersonDto` **25 → 28**、`FamilyDto` **7 → 8**、新增
   `PunishmentJournalDto`（7 键），且「二进制 walker 的成员数常量与 DTO 键数逐一相等」全绿
   （含 `MessagePackSaveFormat.cs:309` 的尾随 `Skip` 代数）；
4. 读回后继续结算与「从未落盘」的基线逐字段相同；
5. `SaveStateValidator` 的第 ⑪ 条（三个计数 `>= 0`）与第 ⑫ 条（日志行的 `PersonId` 必须存在于
   `Family.Members`）各有一条反例断言。

**落点**：`tests/KFL.Tests/Infrastructure/{SaveFixtures,SaveStateComparer,SaveFormatContractTests,SaveStateValidatorTests,SaveFailureTests}.cs`。

---

## 场景 8：确定性与门禁（FR-033~FR-035 / SC-001、SC-010、SC-011）

**步骤**：用**同一固定种子**跑两遍完整的「行贿 → 被抓 → 惩罚 → 服刑 → 释放」链
（含同月多人与亲属禁考）；然后跑门禁四条与架构守卫。

**期望**：
1. 两次独立运行的**逐字段差异项 = 0**（含计数、资金、账本、状态位与计时、功名记录、快照）（SC-001）；
2. 整月随机消费次序**逐次可断言**：米价（②）→ 考课掷骰（④-d）→ 储蓄利率（仅 1 月，⑤）→
   贷款计息利率（⑦）→ 科举判定（⑧）→ 及第入仕待阙（⑧）——**惩罚推进（③）恰好 0 次**、
   行贿的 `NextDouble` 发生在结算之外（FR-033；[契约十六 §1.3](./contracts/punishment.md#13-整月随机消费次序006-后仍逐次可断言)）；
3. 配置字面量违规数 = **0**：新增的 **3** 项小数（`1.6m` / `0.15m` / `0.35m`）已登记，
   整数规则值未登记，`GameConfig.Bribe` / `GameConfig.Punishment` 无自带数值字面量，
   矩阵固定罚金的期望值经配置成员取得（SC-002；[契约十七 §4](./contracts/config-registry.md#4-与-configliteraltestsregisteredvalues-的对照)）；
4. 门禁四条全过，且跑完后 `git status --porcelain` **无残留**（SC-010）；
5. **回写核对**（SC-011）：`specs/002-*/contracts/monthly-settlement.md` 的 `MUST NOT 存在`清单中
   **不再包含**「罚金与 12 格惩罚矩阵」「连坐与服刑计时」；`specs/005-*/contracts/exam-session.md`
   中 `NeutralBribeMultiplier` 的占位说明已被「⑥ 已落地、经同一入口传实际值」取代；
   `LedgerCategory` 的类别计数断言为 **22**；`SaveFileFormat.Version` = **3**；
   [plan.md](./plan.md) 的「回写义务清单」**17** 项在同一提交内全部更新，并已在 `tasks.md` 登记。

**落点**：`tests/KFL.Tests/Rules/DeterminismTests.cs`、`Architecture/ConfigLiteralTests.cs`、
`Rules/SpecAnchorTests.cs`、`Core/LedgerInvariantTests.cs`。
