# 契约十八：月度结算十步与 ⑧ 的八个子步（007 · 逻辑轨 ⑦）

**Feature** | **Date**: 2026-10-17 | **Spec**: [../spec.md](../spec.md) | **Plan**: [../plan.md](../plan.md)

> 本契约是**结算次序**的权威：十步的边界、⑧ 的八个子步、每个子步消耗的随机与落下的条目。
> 上游契约是 [契约五：月度结算](../../002-monthly-settlement-economy/contracts/monthly-settlement.md)，
> 本契约在其「九步」之后插入第 ⑧ 步并把原 ⑧/⑨ 后移为 ⑨/⑩。
> 数值真源 = 《科举浮生录规格书》§4.3/§5.4/§9；本文件只引用 `src/KFL.Rules/Config/` 的成员名。

## 1. 十步表

| 步 | 动作 | 消耗随机 | 落条目 |
| --- | --- | --- | --- |
| ① | 提升待生效的难度 / 生活费档位并清空待生效位 | 0 | — |
| ② | 米价系数游走并 clamp | 1 次 `NextDouble` | — |
| ③ | 惩罚推进（006：`PunishmentAdvance.Run`） | **0** | 006 的罚金类 |
| ④ | 官吏推进（003：`OfficialCareerAdvance.Run`，按 `PersonId` 升序） | 考课掷骰 | `OfficialSalary` 等 |
| ⑤ | 收入：1 月 roll 储蓄利率、12 月计息，再按 §6 各来源入账 | 1 月 +1 次 | 收入类 |
| ⑥ | 生活费：足额或部分支付 + 饥馑四阶段状态机 | 0 | `LivingCost`、饥馑事件 |
| ⑦ | 贷款：先计息、后划扣 | 计息月 1 次 | `LoanInterestRepaid` 等 |
| **⑧** | **人口与健康推进（本特性；`PopulationAdvance.Run`）** | 见 §2 | 见 §2 与 §3 |
| ⑨ | 科举季（005：`ExamSeasonRunner.Run`，按 `PersonId` 升序） | 录取/进甲/名次/待阙 | `ExamTravelCost` |
| ⑩ | `GameState.AdvanceMonth()` 并返回快照 | 0 | — |

**条款**

1. ⑧ MUST 在 ⑨ **之前**——这是 `specs/005-exam-cycle/contracts/exam-session.md:21` 的硬约束：
   当月病死／外嫁者 MUST NOT 再被 ⑨ 当作考生处理。
2. ⑧ MUST NOT 改变 ②~⑦ 与 ⑨ 的既有随机次数与次序（FR-010）。
3. ⑧ 内 MUST NOT 调用 ⑨ 的任何入口；⑨ MUST NOT 回读 ⑧ 的中间状态（只读 `Family.Members` 的终态）。
4. `Settle` 仍是 `MonthlySettlementEngine` 的**唯一**公开实例方法（`tests/KFL.Tests/Rules/SettlementEngineTests.cs:313`）。
5. `SettlementResult` 的公开属性 20 → **21**（尾部 `Lifecycle`），`:315-337` 的 `expectedProperties` 同步。

## 2. ⑧ 的八个子步

`PopulationAdvance.Run(GameState state, IRandomService random, INameGenerator names)` 内**固定**如下次序，
MUST NOT 增删、MUST NOT 穿插、MUST NOT 把某个子步拆成「结算前 / 结算后」两段（FR-007）。

| 子步 | 执行器 | 处理的集合 | 消耗随机（逐人，除注明） | 产出 |
| --- | --- | --- | --- | --- |
| ⑧-a | `MarriageMarket.Run` | 待应答提议 + 上月考季一甲及第者 | 捉婿：1 次概率 + 命中后 1 次金额 `Next(500,1001)`；提亲：1 次概率；命中后候选抽样按 §2.1 | `Lifecycle.Proposal` |
| ⑧-b | `Childbirth.Run` | `Lifecycle.PendingBirths`（按 `CoupleKey` 升序） | 每对：1 次分娩死亡 + 新生儿 1 次性别 `Next(0,2)` + 4 次天赋 + 2 次学业 + 2 次体质 + 2 次寿数（含姓名） | `ChildBorn` 事件条目、`Family.AddChild` |
| ⑧-c | `IllnessAdvance.Run` | 在册成员（**含服刑/待阙**；**不含**外嫁与已亡） | 旧病：每人 1 次清除（`>=1` 短路 ⇒ 0 次）；新感染：`AgeAt >= 3` 者 1 次，命中再 1 次程度；`AgeAt < 3` **0 次** | `IllnessFact`、`Health`/`Status`/`Timers` |
| ⑧-d | `GrowthAdvance.Run` | 同 ⑧-c 的集合 | **0 次** | `Study`/`Health`、`TutorFee` 条目 |
| ⑧-e | `FamineHealth.Run` | 计口（`CountedMembers.Counted`） | 严重期每人 1 次（5% 死亡判定）；非严重期 0 次 | 死亡登记表、`Health` |
| ⑧-f | `LifespanAdvance.Run` | **在册 + 外嫁**（外嫁只承载寿数判定） | 每人 1 次（夭折 10%）；`AgeAt >= 3` 者 0 次 | 死亡登记表 |
| ⑧-g | `DeathSettlement.Run` | 当月死亡登记表（按 `PersonId` 升序） | **0 次** | `Status \|= Deceased`、归档、`MemberDeceased` 条目；有配偶者另经 `Family.EndMarriage`（亡者 `SpouseId` 置空、双方 `FormerSpouseIds` 追加，FR-021） |
| ⑧-h | `HeadSuccession.Run` | 死亡/外嫁处理完毕后的在册男性 | **0 次** | `Family.SetHead`、`NewHeadId`/`HeadBecameVacant` |

### 2.1 ⑧-a 的候选抽样次序（R-25）

1. **先捉婿、后提亲**。两者在同一个月都**各自掷一次**（不得因为捉婿命中就跳过提亲的掷骰，
   否则「每月至多 1 次提亲」的名额语义会随捉婿结果漂移）。
2. 捉婿命中后写 `Lifecycle.Proposal`（`Kind = ImperialProposal`）；随后提亲若也命中，
   **MUST NOT 覆盖**已有的捉婿提议——名额已消耗，提亲不落地（只掷骰，不改状态）。
3. 提亲命中后按阶层加权三档抽一个阶层（复用同一次概率值分段，**0 次额外随机**），再在本家
   **付得起应付聘礼/嫁妆**的候选成员里，以 `Next(0, 候选数)` **均匀抽 1**（**1 次额外随机**，
   FR-020）；候选集合为空则提议不落地（概率值已消费、状态不变）。
4. 提议**当月未应答即失效**：不做任何清理动作，只由 `LifecycleQueries.PendingProposal` 以
   「`ProposedAt` 的年月 ≠ 当月 ⇒ 返回 `null`」表达（Assumption 11）。

### 2.2 子步内的人员次序

- ⑧-b~⑧-f 一律按 `PersonId` **升序**（FR-008）。既有风格参照：
  `src/KFL.Rules/Career/OfficialCareerAdvance.cs:55` 的
  `foreach (var person in CountedMembers.Registered(family).OrderBy(p => p.Id.Value))`。
- ⑧-g 的死亡条目按 `PersonId` 升序归档；同月多人死亡 MUST NOT 按子步顺序排。

### 2.3 死亡登记表（子步隔离的唯一手段）

`DeathRegister` 是 ⑧-b~⑧-f 之间的**唯一**交流通道：

```csharp
internal sealed class DeathRegister
{
    public void Add(Person person, DeathCause cause);
    public IReadOnlyList<(Person Person, DeathCause Cause)> Ordered { get; }  // 按 PersonId 升序
    public bool Contains(Person person);
}
```

**条款**：登记不改变 `Person.Status`；同一人在多个子步被登记时**只保留首个**（
优先级 ⑧-b 分娩 > ⑧-c 病亡 > ⑧-e 饥馑 > ⑧-f 寿终/夭折），后续登记 MUST 被忽略。
⑧-g 是当月**唯一**置 `Deceased` 的地方（`specs/006-bribery-punishment/spec.md:466` 的
「本特性只置已亡并归档」在此兑现）。

## 3. 当月随机消费次序（全月一条式子）

```
米价（②）
→ 惩罚推进（③）—— 0 次
→ 考课掷骰（④-d，逐人升序）
→ 储蓄利率（仅 1 月，⑤）
→ 贷款计息利率（⑦，命中才掷）
→ 婚配事件（⑧-a：捉婿 1 次 + 命中金额 1 次；提亲 1 次 + 命中后阶层分段复用该值 + 候选取人 1 次 `Next(0, 候选数)`）
→ 分娩（⑧-b，逐对升序：分娩死亡 1 + 新生儿包）
→ 患病与病程（⑧-c，逐人升序：旧病清除 / 新感染 + 程度）
→ 成长（⑧-d）—— 0 次
→ 饥馑死亡判定（⑧-e，仅在严重期）
→ 寿终与夭折（⑧-f，逐人升序：仅 `AgeAt < 3`）
→ 科举判定（⑨，逐人升序：录取/进甲，进甲者紧接名次）
→ 及第入仕待阙（⑨，1 次 `Next(6, 25)`，逐人升序）
```

**条款**

1. 上式 MUST 与 `src/KFL.Rules/Settlement/MonthlySettlementEngine.cs:37-42` 的注释逐字一致。
2. 「0 次」的子步 MUST NOT 调用 `IRandomService` 的任何成员（含 `NextBytes`）。
3. ⑧ 之外各步的消费次数 MUST NOT 因插入 ⑧ 而变化（FR-010）——`DeterminismTests` 改相对断言（R-24）。
4. 任何新增随机消费 MUST 先登记进本表，再写实现（FR-009）。

## 4. 事实段形状（`SettlementResult.Lifecycle`）

`LifecycleSnapshot` 的 10 个成员见 [../data-model.md](../data-model.md) §3.2。本契约追加三条：

1. 事实段只描述**当月发生的事**，MUST NOT 承载累积值或汇总计数（FR-026 的「累计出生（含夭折）」
   经 `ChildBorn` 事件条目聚合，不另存）。
2. `PendingProposal` 是**当月新产出**的提议；上月遗留且未应答的提议 MUST NOT 出现在事实段。
3. `Deaths` 的 `DeathCause` MUST 覆盖全部五种取值；MUST NOT 出现「处死」（006 的路径不产生死因）。

## 5. 验证物

| 断言 | 落点 |
| --- | --- |
| 十步次序、⑧ 在 ⑨ 前 | `tests/KFL.Tests/Rules/FamilyLifecycleTests.cs`（SC-001） |
| ⑧ 八个子步的隔离（死亡登记表不被提前置位） | 同上，逐子步时间点快照 |
| ⑧ 的随机消费点数 | `DeterminismTests.cs`（相对断言） |
| `SettlementResult` 属性恰 21 个 | `SettlementEngineTests.cs:315-345` |
| 连续 120 月逐字段 diff = 0 | SC-002，`DeterminismTests.cs` 扩展 |
| ⑧-d/⑧-g/⑧-h 零随机消费 | 计数替身 `CountingRandomService`（`DeterminismTests.cs:529-544`） |
