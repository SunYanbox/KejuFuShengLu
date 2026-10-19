# 契约十六：惩罚推进、12 格矩阵与连坐（贿赂全链路 · 逻辑轨 ⑥）

**Feature**: `006-bribery-punishment` | **Date**: 2026-10-13 | **Spec**: [../spec.md](../spec.md) | **Plan**: [../plan.md](../plan.md)

本契约把「被抓之后」钉成两段：**同刻施加**（12 格矩阵 / 罚金 / 连坐 / 死刑，在结算之外）与
**按月推进**（结算的固定新增第 ③ 步：递减、释放、降级、解除）。数值真源 = 规格书 §7.3 / §7.4 / §7.5 /
§11，登记见 [契约十七](./config-registry.md)。

---

## 1. 结算第③步（FR-020）

### 1.1 在月末结算中的位置

`MonthlySettlementEngine.Settle(GameState)` MUST 严格按此顺序执行，一次结算**只能**执行一遍：

| 步 | 动作 |
| --- | --- |
| ① | 提升待生效值 |
| ② | 米价系数游走 |
| **③** | **惩罚推进**（**006 追加**，FR-020；`PunishmentAdvance.Run(state)`） |
| ④ | 官吏推进（**003**，原 ③） |
| ⑤ | 收入（原 ④） |
| ⑥ | 生活费（原 ⑤） |
| ⑦ | 贷款（原 ⑥） |
| ⑧ | 科举季（**005**，原 ⑦） |
| ⑨ | 推进时间（原 ⑧） |

> **006 后记（2026-10-13）**：**③ 是本契约由 006 追加的整步**。① ② 的语义不变，仅原 ③~⑧ 的序号顺移为
> ④~⑨，全月结算由八步变**九步**。位置钉死在「② 米价之后、官吏推进之前」（Q2 = A）：递减 → 释放/降级/
> 解除在同月的仕途推进与科举季**之前**生效，故「禁考 N 月」恰封锁 N 个考试月、**期满当月即可报名**，
> 停职计时也自释放当月起恢复推进。理由见 [../research.md](../research.md) R-09。
> 它**不是**「MUST NOT 存在」清单的成员——它是固定步骤。

### 1.2 步内固定次序

```text
① 投影并清空日志：PunishmentJournal → PunishmentSnapshot，随后 Clear()
② 逐人递减（按 PersonId 升序，仅当计时非空）：服刑 −1、禁考 −1、禁升 −1（三者互相独立、并行）
③ 归零处理：服刑 ⇒ 释放（清计时与状态位 → DemoteOnRelease → 仍在禁考期则继续禁考）；
            禁考 / 禁升 ⇒ 清对应计时与状态位
④ 统计当月在册服刑人数 ⇒ PrisonerCount
```

| 条款 | 要求 |
| --- | --- |
| 1 | 步内 MUST **恰好调用一遍**日志读取与清空（`IsEmpty` 时投影为空集合、不清出错误） |
| 2 | 逐人处理 MUST 按 `PersonId.Value` 升序（与 003/005 同款，便于逐条断言） |
| 3 | 递减 MUST **只**作用于 `StatusTimers` 的三个字段；MUST NOT 触碰 `AwaitingPostRemainingMonths`（待阙属 003） |
| 4 | 本步 MUST NOT 调用 `GameState.AdvanceMonth()`、MUST NOT 修改 `CurrentDate` |
| 5 | **本步消费随机恰好 0 次**——行贿的 `NextDouble` 发生在结算之外（[契约十五 §2](./bribery.md#2-行贿行动的固定次序fr-003fr-010)） |
| 6 | 置位次序：建立时**先置状态位后写计时**；清除时**先清计时（写 `null`）后清状态位**（满足 001 的「计时非空 ⟺ 状态位为真」，`Person.Status` / `Person.Timers` 的交叉校验 setter 会强制；原写「先写计时后置状态位」，已按实现修正，登记为计划勘误 #5） |

### 1.3 整月随机消费次序（006 后仍逐次可断言）

```text
米价（②）→ 考课掷骰（④-d，逐人升序）→ 储蓄利率（仅 1 月，⑤）→ 贷款计息利率（⑦）
→ 科举判定（⑧，逐人升序）→ 及第入仕待阙（⑧，逐人升序）
```

与 005 的次序**逐项同构**，只是序号顺移（`③-d`→`④-d`、`④`→`⑤`、`⑥`→`⑦`、`⑦`→`⑧`）。
`DeterminismTests.随机消费次序固定为米价储蓄利率再计息()` 的逐月 `NextDouble` 计数（1 次；1 月 2 次；
计息节点再 +1）MUST **保持全绿**。

---

## 2. 12 格惩罚矩阵（FR-014~FR-016、SC-004）

**当事人 = 参加考试者；相关亲属 = 当事人的「妻子/丈夫、儿子、女儿、父亲、母亲」中在世者**（§7.3）。

| 难度 | 殿试被抓 | 省试被抓 | 解试被抓 |
| --- | --- | --- | --- |
| 简单 | 服刑 5 年 + 罚金 2%（**无禁考**） | 服刑 3 年 + 罚金 1%（**无禁考**） | 服刑 1 年（**无罚金**） |
| 普通 | 服刑 15 年 + 罚金 15% + 亲属禁考禁升 36 月 | 服刑 5 年 + 罚金 200 贯 + 亲属禁考禁升 24 月 | 服刑 3 年 + 罚金 100 贯（**无亲属禁考**） |
| 困难 | **当事人死刑** + 罚金 20% + 亲属禁考禁升 48 月 | 服刑 20 年 + 罚金 10% + 亲属**禁考** 36 月 | 服刑 10 年 + 罚金 5% + 亲属**禁考** 36 月 |
| 地狱 | **当事人死刑 + 连坐** + 罚金 20% + **全族**禁考禁升 48 月 | 服刑 20 年 + 罚金 10% + 亲属**禁考** 36 月 | 服刑 10 年 + 罚金 5% + 亲属**禁考** 36 月 |

**条款**：

1. 服刑月数 = 年数 × **12**（5 年 = 60 月）。
2. 「**无禁考**」格（简单档）MUST NOT 建立任何禁考或禁升计时（两者的剩余月数 MUST 保持 `null`）。
3. 「相关亲属**禁考** N 月」格（困难 / 地狱的解试与省试）MUST **只**建立禁考，**MUST NOT** 建立禁升。
4. 表由 `PunishmentPolicy.CellOf(difficulty, level)` 单点给出；12 格 MUST **逐格**有一条断言
   （SC-004：服刑月数、罚金比例/固定额、「只禁考 / 无禁考」标记各一条）。
5. 比例罚金的比例：简单 `2%` / `1%`；普通 `15%`；困难与地狱 `20%` / `10%` / `5%`。

---

## 3. 服刑、禁考与禁升的建立（FR-014、FR-015）

| 项 | 写入 | 状态位 | 计时字段 |
| --- | --- | --- | --- |
| 服刑 | 被抓命中含服刑的格子 | `StatusFlag.ServingSentence` | `StatusTimers.SentenceRemainingMonths` |
| 禁考 | 见矩阵的禁考月数 | `StatusFlag.ExamBanned` | `StatusTimers.ExamBanRemainingMonths` |
| 禁升 | 见矩阵的禁升标记 | `StatusFlag.PromotionBanned` | `StatusTimers.PromotionBanRemainingMonths` |

**条款**：

1. 置位次序 MUST 为**先状态位后计时**；清除时 MUST 为**先清计时（写 `null`）后清状态位**
   （I-5 的交叉校验 setter 在 001 起就强制这个方向——反序必抛 `ArgumentException`。
   本条原写作「先计时后状态位」，已按既有实现修正，登记为计划勘误 #5）。
2. **不叠加、取替换**：同一成员被告知两次时取**本次施加值**，MUST NOT 相加、MUST NOT 使剩余月数**增加**
   （服刑同理取**替换**，否则「5 年 + 再次被抓」会变成规格书里不存在的 25 年；spec Edge Cases）。
3. **全族禁考禁升 48 月**（地狱殿试）以**逐人计时**落地：施加当刻对**每名在世在册成员**各写
   **48 月禁考 + 48 月禁升**；MUST NOT 新增家族级计时字段；施加后新出生/娶入的成员**不受**该次覆盖
   （Q3 = A）。「宣告全族禁考禁升触发」的事实经 `PunishmentJournal.FamilyBanDeclared` 在当月快照中可见。

---

## 4. 罚金（FR-011~FR-013）

### 4.1 基数（FR-011）

```text
总资产 = 现金 + 储蓄 + AssetPriceTable.MarketValue(田宅铺) − 贷款本金 − 欠息
```

**单一访问器**：`TotalAssetsCalculator.Of(FamilyEconomy)`。既有的「12 月工出身 bonus 的总资产为正」
判定 MUST **复用同一位**（消除 `MonthlySettlementEngine.cs:138-140` 的就地计算第二真源）。
基数为负或 0 ⇒ 按 **0** 处理。**不含商本池**（§5.2 的 §17 裁决；§7.5 同口径）。

### 4.2 金额（FR-012）

```text
比例罚金 = min( 比例 × 总资产 × DifficultyRates.ExpenseFactor(difficulty), 50000 贯 )
固定罚金 = 固定额 × DifficultyRates.ExpenseFactor(difficulty)
```

| 条款 | 要求 |
| --- | --- |
| 1 | `50000` 贯是**全域封顶**，对**最终金额**生效（先乘系数、再封顶） |
| 2 | 罚金**照乘**难度支出系数（§11 的「全部支出 × 支出系数」，§7.3 未把罚金列入「不乘」例外，而 §7.2 明写贿赂金额乘系数）；该口径登记为待回写的 §17 项，若裁决为「与路费同款不乘」，只需改本条与对应断言 |
| 3 | 罚金 MUST 经**既有** `PaymentPrimitive.Pay(economy, LedgerCategory.Fine, month, amount, subject.Id)` 支付（现金 → 储蓄 → **余额转贷款**）——本特性是它的**首个产品调用方** |
| 4 | 付不起 **MUST NOT** 阻止惩罚、**MUST NOT** 产生负余额：现金与储蓄清零、缺口转 `Loan.Principal`、各池余额 ≥ 0 |
| 5 | 金额为 `0` 时 MUST **NOT** 落条目（资金类条目不变量） |
| 6 | **条目金额 = 本次的资金池变动额**（规格书 §12.3 的 §17 裁决）：池足额时恰等于罚金额；付不起时**只按池内实际付出额**落一条 `Fine`（负额），缺口转贷款**不落**资金条目。`PaymentResult.Total`（池内付出 + 转贷款）仍恰等于罚金额。本口径于实现期（T029）发现，已同批修正 `spec.md` 的 SC-005 与 US2 AS5，登记为[plan.md](../plan.md)「勘误登记」#4 |

### 4.3 支付原语的可选形参

```csharp
public static PaymentResult Pay(
    FamilyEconomy economy, LedgerCategory category, GameDate date, Money amount,
    PersonId? personId = null);
```

默认 `null` ⇒ 既有行为**逐位不变**（`LoanTests` 的三步语义断言无需改动）；行贿与罚金传入本人 `Id`，
使契约四 §5 的角色级聚合能正确归属「谁把家败了」。三段次序、不做部分支付、`amount <= 0` 的拒绝语义
**一字不动**。该签名变更 MUST 回写 [002 的 data-model §4.3](../../002-monthly-settlement-economy/data-model.md)
与 [契约三 §7](../../002-monthly-settlement-economy/contracts/monthly-settlement.md)。

---

## 5. 服刑期间的暂停与禁考并存（FR-017~FR-019）

| 条款 | 要求 |
| --- | --- |
| 1 | 服刑者 MUST **不计口**（不消耗生活费、不产生收益与功名），但 MUST **保留在家族列表与卡片上**、对界面可见（徽章所需的剩余月数由查询面提供） |
| 2 | 服刑期间该成员的**待阙计时、在职计时与政绩一律暂停**——**不推进、不重置**，刑满释放后从暂停处继续 |
| 3 | 「暂停」（服刑）与「跳过」（禁升期内到期的考课：不掷骰、不补判、计时重置）是**两种语义**，快照里 MUST 分别记为 `AppraisalPaused` 与 `AppraisalSkipped`（003 契约七 §4 条款 6/7） |
| 4 | 服刑计时与禁考/禁升计时 MUST **互相独立、并行递减**；禁考的建立或存续 MUST NOT 使现任官员失去官职或俸禄（禁考只禁考试） |
| 5 | 释放时若仍在禁考期则**继续禁考**（MUST NOT 因释放而提前清零禁考/禁升计时） |

---

## 6. 连坐与免死（FR-022~FR-026）

### 6.1 触发范围封印

**只在地狱难度 × 殿试 × 被抓时触发**；其余 **11** 格 MUST NOT 触发连坐（触发数 = 0 是 SC-007 的判据）。

### 6.2 逐人三分支（互斥）

| 亲属状态 | 结果 | 记录 |
| --- | --- | --- |
| 无功名 ∧ 无官职 | **处死**（置已亡、归档、进族谱） | `CollectivePunishmentFact(Executed)` |
| 有功名 ∧ 无官职 | **降一级功名**（进士→贡士→举人→白身；白身无降） | `CollectivePunishmentFact(DegreeDemoted, before, after)` + 恰 **1** 条 `DegreeRecord(PunishmentDemotion)` |
| 有官职 | 受「**全族禁考禁升 48 月**」覆盖（**不存在独立的禁升 60 月**） | `CollectivePunishmentFact(FamilyWideBan)` + 逐人 `RelativeBanFact(48, 48)` |

### 6.3 免死例外（FR-024）

当事人的**配偶有官职** ∧ **该孩子未成年** ⇒ 该孩子 **MUST 免死**（就**连坐分支的后果**而言状态逐字段
不变：不置已亡、不降功名、不产生 `CollectivePunishmentFact`）；「全族禁考禁升
48 月」照常适用（故他的 `StatusTimers` 两条禁令计时**照常写入**），**期满可考**。免死 MUST NOT
扩展到配偶以外的亲属，也**不适用于当事人本人**。
> **判读口径（2026-10-13 实现期登记）**：免死封印的是**连坐三分支**，不是第 ⑦ 步的**全族禁令**——
> 该孩子仍属「在世在册成员」，故照常被全族 48 月覆盖（与 §6.4 的「连坐范围」封印同一口径）。
> 本句早先写作「状态逐字段不变」，若按字面读会与「全族 48 月照常适用」自相矛盾。
>
> **007 后记（步骤编号，2026-10-17）**：本句的「第 ⑦ 步」指**科举季**。该步在 006 之后已被
> 007 顺移为**第 ⑨ 步**（006 插 ③ 惩罚推进、007 插 ⑧ 人口与健康推进；位置恒为「贷款之后、
> 推进时间之前」）。本后记只订正编号，不改变判读口径；**外嫁成员**（007 新增的归档类）仍留在
> `Family.Members`（数据保留），故本节与 §6.4 的跨族追及**照常命中**，只是不再计入「计口」与
> ⑧-c/⑧-d/⑧-e（见 [007 data-model §6](../../007-family-lifecycle/data-model.md) 的不变量 13）。
「未成年」沿用 002 的 `AgeBracketPolicy`（男 < 12 / 女 < 14，生日当月生效），MUST NOT 另立判据。

### 6.4 范围与追及（FR-025、FR-016）

| 条款 | 要求 |
| --- | --- |
| 1 | 亲属集合 = **配偶、儿子、女儿、父亲、母亲**中的**在世者**；兄弟、孙辈、姻亲 MUST **NOT** 被波及 |
| 2 | 亲属集合取 `Family.Members`（**含归档成员**），使**已出嫁归档的女儿被娘家跨族追及**自然成立（她照样被处死 / 降功名 / 禁考禁升，并产出「殃及池鱼」所需事实） |
| 3 | 已亡者跳过（「在世者」是判定前提）——不得复活、不得追加记录 |
| 4 | 连坐对象已在服刑者：照常被判（处死 / 降级 / 禁考禁升），服刑状态与连坐后果并存 |

### 6.5 死刑（FR-026）

**死刑立即执行**：置 `StatusFlag.Deceased`、归档、进族谱、计入死亡结算；MUST **NOT** 建立服刑计时
（当事人与连坐对象同款）。本特性 MUST NOT 引入「已判未决」的持久状态，也 MUST NOT 自行判定游戏结束
（绝嗣属 ⑧）。

---

## 7. 释放与降级（FR-020）

服刑剩余月数递减到 0 ⇒ **释放**：

| 次序 | 动作 |
| --- | --- |
| ① | 清除 `SentenceRemainingMonths` 与 `StatusFlag.ServingSentence`（先清计时、后清状态位） |
| ② | **曾有官职者**（释放时 `Rank is not null`）⇒ `new OfficialRank(Rank.Level + 1)`；已是 **L18**（`SalaryTable.LowestLevel`）则**维持** |
| ③ | **功名保留**、可继续考试；**释放时若仍在禁考期则继续禁考** |

**条款**：判据取「释放时 `Rank` 非 null」，MUST NOT 新增「曾为官」标记（第二真源）。降级 MUST 恰好发生
**1 次**（`ReleaseFact.NewRankLevel` 可断言）；降级落在**官阶级数**上，与 003 的考课晋升（`Level − 1`）
方向相反，MUST NOT 与 `AppraisalSkipped` / `AppraisalPaused` 混用。**DebugEdit 之外的功名变迁 MUST 只**
经 `Person.AppendDegree`。

---

## 8. 查询面（FR-029、FR-030）

| 成员 | 签名 | 语义 |
| --- | --- | --- |
| ② | `PunishmentStatus StatusOf(Person person)` | 服刑 / 禁考 / 禁升**剩余月数** + 「是否有官职」（供 U2 的「服刑中（剩 X 年 X 月）」徽章） |
| ④ | `int PrisonerCount(Family family, GameDate date)` | 某月在册服刑人数（构造「同月 3 人服刑」时返回 3） |
| 附加 | `bool IsBribeWindowOpen(...)` | 见 [契约十五 §6](./bribery.md#6-派生查询面fr-029-fr-030) |
| 附加 | `bool WasBribeFreeAtImperialPass(Person)` | 「清白传家」的派生事实 |
| 附加 | `bool WasExecutedByState(Person)` | 「身首异处」的派生事实：**当事人本人**被处死（见下方条款） |
| 附加 | `int FamilyCumulativeBribes(Family)` | 「投石问路 / 暗度陈仓 / 权钱交易 / 只手遮天 / 买断功名」 |

**条款**：全部为**纯函数**（不改状态、不消费随机）；U1/U2/U4 与 ⑧ MUST 只经该查询面取值；
⑧ 要求的其余事实（连坐致亲属死亡或降级、外嫁女儿被追及、全族禁考禁升触发、某月服刑 ≥ 3 人、
服刑期满释放）经 [§9](#9-快照与断言面fr-028fr-030) 的当月快照消费。

**「当事人本人的死刑」的口径**：它**不落** §9 快照的六类事实行，而是由**状态派生**——
`PunishmentQueries.WasExecutedByState(Person)` 读 `Person.Status` 的 `StatusFlag.Deceased`
（配套的归档、进族谱与死亡结算同时成立）。依据：规格书 §7.5 死刑**立即执行**、无「已判未决」
持久状态，故状态位即事实；`CollectivePunishmentFact.Outcome == Executed` 只覆盖**被连坐的亲属**。
⑧ 与 U2 MUST 经该入口取值，MUST NOT 自行读 `Person.Status` 判成就。本特性 MUST **NOT** 自行解锁任何成就。

---

## 9. 快照与断言面（FR-028、FR-030）

`SettlementResult` MUST 新增**必填子段**：

```csharp
public required PunishmentSnapshot Punishment { get; init; }
```

`PunishmentSnapshot`（`KFL.Rules.Punishment`）含：`Month`、`Bribes`、`Sentences`、`Fines`、
`RelativeBans`、`CollectivePunishments`、`Releases`（六类事实行）、`FamilyBanDeclared`、`PrisonerCount`。

**「当事人本人的死刑」不经本快照**：它由 `Person.Status` 的已亡位派生（见 [§8](#8-查询面fr-029-fr-030)
的 `WasExecutedByState`）——快照的六类事实行里**没有**、也 MUST NOT 新增「当事人本人被处死」这一类；
`CollectivePunishmentFact.Outcome == Executed` 只表达**被连坐的亲属**。

**条款**：

1. 它是**增量、只读**的当月快照：只含本月发生的事，MUST NOT 回写 `GameState`、MUST NOT 成为与 `Person`
   并存的第二真源（001/002 的既有口径）。
2. 全部集合 **按 `PersonId` 升序**（并列时按 `Level` / `Relative` 再排序），便于逐条断言。
3. 它是 `PunishmentJournal` 的**投影**：第③步在清空前一次性构造；日志为空时各集合为空、
   `PrisonerCount` 仍按当月在册服刑人数如实给出（FR-029④）。
4. **罚金金额既进快照也进账本**是允许的（`Entries` 含 `Fine` 条目，快照含 `FineFact`）；但
   **服刑事件不进账本**——FR-027 明令新增账本类别只有资金类两个（plan.md 勘误 #1）。
5. 快照 MUST 足以让 ⑧ 解锁 §14.1 的诸项（**唯一例外**是「当事人本人的死刑（身首异处）」，
   它经 §8 的 `WasExecutedByState` 状态派生，不经本快照；见上）；本特性 MUST NOT 自行解锁
   （判据 = 事件与计数齐备，不判成就、不判绝嗣）。

> **008 消费注（2026-10-18）**：逻辑轨 ⑧ 把本快照当作「当月报告」读，接缝如下——
> ① 成就「投石问路 / 暗度陈仓 / 权钱交易 / 只手遮天 / 买断功名」读
> `Bribes` 中 `Caught == false` 的条数（**成功**行贿，累加进 `AchievementState.SuccessfulBribeCount`），
> **MUST NOT** 改用 §8 的 `FamilyCumulativeBribes`（它数的是**尝试**次数，口径不同）；
> ② 「无法无天」读 `Bribes` 中 `CapReached == true` 的当月条目；③「身首异处」读
> `Caught == true` 的**殿试**条目与 `PunishmentPolicy.CellOf(...)` 的执行口径；
> ④「覆巢之下 / 殃及池鱼 / 斯文扫地」读 `CollectivePunishments` / `RelativeBans` / `FamilyBanDeclared`；
> ⑤「囹圄之灾」读 `PrisonerCount >= 3`（**当月**同月口径，不复算历史）。
> 这些读法只**读**快照，MUST NOT 回写日志或快照（001/002 的「快照是增量只读投影」不变）。
> 契约真源仍是 [008 `achievement-catalog.md` §2](../../008-achievements-extinction/contracts/achievement-catalog.md)。

---

## 10. 明确不包含

界面（U1~U5）、成就解锁与绝嗣判定（⑧）、成长/衰老/疾病/寿命与「狱中达龄死亡」（⑦；本特性只保证
服刑状态与计时**不与它们冲突**）、婚育/收养/买功名婚姻（⑦）、族谱排序与死亡结算的实现、
存档迁移（004 的 Out of Scope）、官名表、「死囚待决」的持久状态、以及越限能力的装配期接缝（U4 开放项）。
