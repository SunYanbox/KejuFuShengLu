# 契约三：月度结算（002 与其后所有阶段的构建期契约）

**Feature**: `002-monthly-settlement-economy` | **Date**: 2026-10-06

本契约把「推演一个月」钉成**可断言的操作序列**。此后任何阶段（随机事件、属性成长、科举、
授官）都只能在既定槽位上插入，MUST NOT 改动既有槽位的相对次序与语义——因为
`SettlementResult`、饥馑状态机与「同种子 → 同结果」都建立在这个序列上。

## 1. 结算的唯一步骤序列

`MonthlySettlementEngine.Settle(GameState)` MUST 严格按此顺序执行，一次结算**只能**执行一遍：

| 步 | 动作 | 消耗随机 | 落条目 |
| --- | --- | --- | --- |
| ① | **提升待生效值**：`PendingDifficulty` → `Difficulty`、`PendingLivingStandard` → `LivingStandard`，并清空待生效位（未设置则沿用当前值） | — | — |
| ② | **米价系数游走**：按 §4 的公式游走并 clamp，当月生效 | 1 次（米价） | — |
| ③ | **惩罚推进**（**006 追加**，FR-020；`PunishmentAdvance.Run(state)`）：① 投影并清空惩罚日志（`PunishmentJournal` → `PunishmentSnapshot`）→ ② 逐人按 `PersonId` 升序递减服刑/禁考/禁升剩余月数（三者独立并行）→ ③ 归零处理（服刑递减到 0 ⇒ 释放：先清状态位后清计时；`Rank is not null` ⇒ 官阶 +1，已是 L18 则维持）→ ④ 统计当月在册服刑人数 | **0 次** | — |
| ④ | **官吏推进**（**003 追加**，FR-019；`OfficialCareerAdvance.Run`，成员按 `PersonId` 升序）：④-a 待阙递减与递减到 0 的当月授官、④-b 政绩 +1、④-c 致仕判定、④-d 在职计时与考课掷骰 | 命中考课周期的成员各 1 次 | —（俸禄差异经 ⑤ 入账；MUST NOT 新增账本类别） |
| ⑤ | **收入**：§6 的全部来源入账（俸禄按 §6 的**三态**口径），含 12 月的储蓄利息与工出身 bonus | 1 月时 1 次（储蓄利率） | 资金类若干 |
| ⑥ | **生活费**：按 §5 计算应付额 → 支付（足额或部分）→ 按 §8 判定饥馑阶段迁移 | — | `LivingCost` + 事件类若干 |
| ⑦ | **贷款**：先计息（第 12 个月节点），后划扣 | 命中节点时每笔 1 次 | `LoanInterestAccrued`（事件类）+ 划扣（资金类） |
| ⑧ | **人口与健康推进**（**007 追加**，FR-041；`PopulationAdvance.Run`，八子步见 §1.1）：⑧-a 婚配事件（榜下捉婿 → 提亲）→ ⑧-b 分娩 → ⑧-c 患病与病程 → ⑧-d 成长与遗忘 → ⑧-e 饥馑体质损失 → ⑧-f 寿终与夭折 → ⑧-g 死亡落账（**当月唯一置 `Deceased` 处**）→ ⑧-h 家主继任（每月至多一次） | 见 §4 的「人口与健康推进」段；⑧-d / ⑧-g / ⑧-h **0 次** | `ChildBorn` / `MemberDeceased` / `MarriageFormed` / `MemberMarriedOut`（事件类，金额 0）；⑧-c 的 `MedicineExpense` / ⑧-d 的 `TutorFee`（资金类） |
| ⑨ | **科举季**（**005 追加**，FR-019；`ExamSeasonRunner.Run`，逐人按 `PersonId` 升序）：① 自动报名 → ② 路费扣除与弃考 → ③ 免解行使 → ④ 录取/进甲判定 → ⑤ 及第入仕（待阙月数）→ ⑥ 特奏名询问；非考试月六段全部跳过 | 见 §4 的「科举判定」与「及第入仕待阙」两段 | `ExamTravelCost`（资金类，仅省试月；MUST NOT 新增账本类别） |
| ⑩ | **推进时间**：调用 `GameState.AdvanceMonth()` 推进 `CurrentDate`，返回 `SettlementResult` | — | — |

> **③ 是本契约原六步之外的整步**（2026-10-08 由 003 插入，FR-019）：①②④⑤⑥⑦ 的**语义不变**，
> 仅原 ③~⑥ 的序号顺移为 ④~⑦。③ 的逐子步次序、子步间的强制关系与「③ MUST 在收入之前」的
> 理由（当月授官者当月起领俸、当月满 70 岁者当月即按半俸计）见
> [契约七 §2](../../003-opening-assets-officialdom/contracts/official-career.md)。

> **005 后记（2026-10-12）**：**⑦ 是本契约由 005 追加的整步**（005 FR-019）：①②③④⑤⑥ 的
> **语义不变**，仅原 ⑦「推进时间」顺移为 ⑧，全月结算由七步变**八步**。本步的位置钉死在
> 「⑥ 贷款之后、⑧ 推进时间之前」，MUST NOT 调用 `AdvanceMonth`、MUST NOT 修改 `CurrentDate`
> （用考试当月的日期与当月值判定）；步内次序、随机消费与快照面见
> [契约十三 §1](../../005-exam-cycle/contracts/exam-session.md)。

> **006 后记（2026-10-13）**：**③ 是本契约由 006 追加的整步**（006 FR-020）：①② 的**语义不变**，
> 原 ③~⑧ 的序号顺移为 ④~⑨，全月结算由八步变**九步**。本步的位置钉死在「② 米价游走之后、
> ④ 官吏推进之前」，MUST NOT 调用 `AdvanceMonth`、MUST NOT 修改 `CurrentDate`，且**消费随机
> 恰好 0 次**（日志投影、计时递减、释放归零与服刑人数统计全为确定性操作）。步内次序与快照面见
> [契约十六 §1](../../006-bribery-punishment/contracts/punishment.md)。
> **整月随机消费次序随之更新为**：米价（②）→ **惩罚推进（③）—— 0 次** → 考课掷骰（④-d）→
> 储蓄利率（仅 1 月，⑤）→ 贷款计息利率（⑦）→ 科举判定（⑨）→ 及第入仕待阙（⑨）。
>
> **007 后记（2026-10-17）**：**⑧ 是本契约由 007 追加的整步**（007 FR-041）：①②③④⑤⑥⑦ 与
> 原 ⑧（科举季）的**语义不变**，原 ⑧「科举季」顺移为 **⑨**、原 ⑨「推进时间」顺移为 **⑩**，
> 全月结算由九步变**十步**。本步的位置钉死在「⑦ 贷款之后、⑨ 科举季之前」，MUST NOT 调用
> `AdvanceMonth`、MUST NOT 修改 `CurrentDate`（用当月日期判定），且 MUST NOT 改变 ②~⑦ 与 ⑨
> 的既有随机次序与次数。步内八子步与随机消费见 §1.1；`SettlementResult` 的公开属性由 20 变为 **21**
> （尾部新增 `Lifecycle`）。
> **整月随机消费次序随之更新为**：米价（②）→ **惩罚推进（③）—— 0 次** → 考课掷骰（④-d）→
> 储蓄利率（仅 1 月，⑤）→ 贷款计息利率（⑦）→ **⑧-a 婚配事件（每月 1 次，捉婿命中另 1 次）**
> → **⑧-b 分娩（每对夫妇 1 次分娩死亡骰 + 1 次性别 + 属性与寿数）** → **⑧-c 患病与病程
> （逐人升序）** → **⑧-d 成长（0 次）** → **⑧-e 饥馑（仅严重期，逐人 1 次）** →
> **⑧-f 寿终与夭折（逐人升序，`AgeAt < 3` 的生日月 1 次）** → **⑧-g 死亡落账（0 次）** →
> **⑧-h 家主继任（0 次）** → 科举判定（⑨）→ 及第入仕待阙（⑨）。
> 任何后续阶段新增随机消费 MUST 先登记进此式。

#### 1.1 第 ⑧ 步的八子步（**007 追加**）

| 子步 | 执行器 | 消费随机 | 说明 |
| --- | --- | --- | --- |
| ⑧-a | `MarriageMarket.Run` | 每月恰 1 次（提亲骰）；榜下捉婿命中时另 1 次（金额） | 先捉婿后提亲；两者同月各自掷；提亲 MUST NOT 覆盖同月捉婿提议；只写 `Family.Lifecycle.Proposal` |
| ⑧-b | `Childbirth.Run` | 每对夫妇：分娩死亡 1 + 性别 1 + 天赋 8 + 学业 2 + 体质 2 + 寿数 2 | 按 `WifeId` 升序；消费**上一月**排入的待产记录；新生儿经 `Family.AddChild` 注册 |
| ⑧-c | `IllnessAdvance.Run` | 旧病：清除 1 次（`>= 1` 短路 0 次）；新感染：`AgeAt >= 3` 者 1 次，命中再 1 次（程度） | 两段式；**先扣体质、再掷清除**；疗程中先付药费；清除命中后该月不再有后续随机消费 |
| ⑧-d | `GrowthAdvance.Run` | **0 次** | 学业增减与遗忘、体质自然恢复（生活费足额、非饥馑、非患病） |
| ⑧-e | `FamineHealth.Run` | 仅 `FamineStage.Severe`：计口每人 1 次；`Relief` / 未进入饥馑 0 次 | 只读饥馑阶段机，MUST NOT 改写 002 的阶段机 |
| ⑧-f | `LifespanAdvance.Run` | 每人：夭折 1 次（仅 `AgeAt < 3` 且为生日月）；其余 0 次 | 遍历集合 = 在册 ∪ 已外嫁归档；寿终/患病/服刑均不豁免 |
| ⑧-g | `DeathSettlement.Run` | **0 次** | 按 `PersonId` 升序去重落 `Deceased` + `MemberDeceased` 事件条目；幂等；死因不落字段 |
| ⑧-h | `HeadSuccession` | **0 次** | 在 ⑧-g 之后执行**一次**；无候选 ⇒ `SetHead(null)` + 快照标记「家主空缺」 |

**为什么收入在生活费之前**（§17 裁决 E-04，规格书 §3 已回写）：玩家直觉是「先领俸禄、
收田租，再拿这些钱买粮」。原顺序（生活费 → 收入）会让「本月有俸禄可领」的家庭在发放前
被判为断炊，与玩家的因果预期相反。

**为什么计息在划扣之前**（E-05）：§5.4 规定「每 12 个月按**当时**剩余本金计息一次」，
先计息使「当时本金」= 月初结余本金，不受本月划扣影响。

**MUST NOT 存在**的步骤（各自阶段负责）：随机事件总表与成就解锁（逻辑轨 ⑦ 的余项）、
绝嗣判定（逻辑轨 ⑧）、结局与统计（逻辑轨 ⑨/U2）、存档写入（阶段④）。
**007 已从本句交付的项**：属性成长/衰老/疾病/死亡判定（**已由 007 的 ⑧ 接管**，2026-10-17；
`SettlementResult.Lifecycle` 承载当月事实段），以及 2026-10-13 由 006 交付的惩罚推进。
**仍不交付**：怀孕率（007 明确不含）、家族声望、随机事件总表（007 只交付 §9.1 提亲与 §9.2 捉婿）。

> **005 后记（2026-10-12）**：原清单中的「**科举季触发（阶段⑤）**」**已解除**——`005-exam-cycle`
> 把它落成为结算的固定新整步 ⑦「科举季」（位于 ⑥ 贷款之后、⑧ 推进时间之前），见
> [契约十三 §1.1](../../005-exam-cycle/contracts/exam-session.md) 与 005 FR-019。
> 本清单其余各项仍成立。

## 2. 时间语义

| 项 | 规则 |
| --- | --- |
| 结算对象 | 注入的 `IGameClock.Current` **当月的账目**——MUST NOT 直接读 `GameState.CurrentDate`（契约二 §2 条款「注入」；`MonthlySettlementEngine` 从不读 `state.CurrentDate`） |
| 推进 | 结算末尾调用 `GameState.AdvanceMonth()`（+1 个月、跨年进位）；`CurrentDate` **没有** public setter |
| 起始 | 新建存档为 1 年 1 月（§3） |
| 「次月生效」 | 提升点在第①步，因此「第 N 月结算后切换 → 第 N+1 月结算生效」；同一月份内 MUST NOT 出现半新半旧 |

## 3. 计口筛选（§5.1、§12.1；FR-029/FR-030）

生活费与收入的成员集合 = **计口成员** = 在册（未亡且未外嫁）**且未服刑**。

| 成员形态 | 计口 | 生活费 | 收入/人力 | 档案与历史条目 |
| --- | --- | --- | --- | --- |
| 正常在册 | ✅ | 计入 | 可产生 | 保留 |
| **待阙** | ✅ | **计入**（§8.2：无俸、靠积蓄） | 可产生（但无俸禄） | 保留 |
| **服刑** | ❌ | 不计 | 不产生 | 保留且界面可见 |
| **外嫁** | ❌ | 不计 | 不产生 | 保留且界面可见 |
| **已亡** | ❌ | 不计 | 不产生 | 保留 |

**在册 ≠ 计口**：绝嗣判定与家族列表用**在册**口径，生活费与收入用**计口**口径
（FR-030）。二者 MUST NOT 互相顶替。

## 4. 随机消费次序与映射

同一次结算内的随机取值**次序固定**为：`米价（②）→ 惩罚推进（③）—— 0 次 → 考课掷骰（④-d，逐人按 PersonId 升序）→ 储蓄利率（仅 1 月，⑤）→ 贷款计息利率（⑦）→ 婚配事件（⑧-a）→ 分娩（⑧-b，逐对按 WifeId 升序）→ 患病与病程（⑧-c，逐人按 PersonId 升序）→ 成长（⑧-d）—— 0 次 → 饥馑死亡判定（⑧-e，仅严重期）→ 寿终与夭折（⑧-f，逐人按 PersonId 升序）→ 死亡落账（⑧-g）—— 0 次 → 家主继任（⑧-h）—— 0 次 → 科举判定（⑨，逐人按 PersonId 升序）→ 及第入仕待阙（⑨，逐人按 PersonId 升序）`。
除考课掷骰按命中人数各 1 次、⑧-b 的性别与 ⑧-a 的金额、以及 ⑨ 的「一甲名次」与「待阙月数」用 `Next(...)` 外，其余每个取值都是 `IRandomService.NextDouble()` 的一次调用，映射公式如下（`r ∈ [0,1)`）：

| 取值 | 公式 | 区间/边界 |
| --- | --- | --- |
| 米价系数游走 | `系数 ← clamp(系数 × (1 + (r × 0.2 − 0.1)), 0.7, 3.0)` | 月游走 ±10%，clamp 0.7~3.0（§5.1） |
| 惩罚推进（**006 追加**，③） | ——（**0 次**：本步 MUST NOT 消费任何随机） | 日志投影、计时递减、释放归零、服刑人数统计全为确定性操作（契约十六 §1） |
| 考课掷骰（**003 追加**） | `r < OfficialCareerPolicy.PromotionChance(merit)`，其中 `PromotionChance(merit) = min(0.25 + merit × 0.003, 0.70)`；成功 ⇒ 官阶级数 −1 | 仅在职月数达 36 且非禁升、非服刑的在册在职成员各 1 次；**禁升**跳过判定时 MUST NOT 消费随机；服刑者暂停（既不计时也不掷骰）（§8.2；契约七 §4/§8） |
| 当年储蓄利率 | `0.005 + r × 0.019` | 0.5%~2.4%（§5.4） |
| 贷款计息利率 | `0.005 + r × 0.019` | 0.5%~2.4%（§5.4） |
| 婚配事件（**007 追加**，⑧-a） | 提亲：每月恰 1 次 `r < ProposalBaseChance × NegativeEventFactor`（`0.05 × 系数`）；命中后用**同一次** `r` 在阶层权重内分段映射并均匀取候选成员（**0 额外随机**）。榜下捉婿：窗口内逐人 1 次 `r < 0.30 × NegativeEventFactor ÷ (1 + P)`；命中后 1 次 `Next(500, 1001)` 取金额 | 捉婿窗口 = 新科一甲及第的**次月**；`P` = 判定时在册有官者的压力权重之和（[007 contracts/marriage-birth.md §4](../../007-family-lifecycle/contracts/marriage-birth.md)） |
| 分娩（**007 追加**，⑧-b） | 每对夫妇：① `r < ChildbirthDeathChance × NegativeEventFactor`（分娩死亡）② `Next(0, 2)`（性别）③ 天赋 ×4 + 学业 + 体质（各 2 次，遗传口径）④ 寿数 2 次 | 按 `WifeId` 升序消费**上一月**排入的待产记录；申请当月的记录不消费（007 FR-042） |
| 患病与病程（**007 追加**，⑧-c） | 旧病清除：`r < min(1, 体质系数 × m_q + b)`（`min` 钳到 1 ⇒ 必愈且**不消费**）；新感染：`r < (0.0025 + 0.3975 × (100 − c) / 99) × m_p`，命中后紧接 1 次 `NextDouble()` 定程度 `s`（`IllnessSeverityBasisPoints = round(s × 10000)`） | `AgeAt < 3` 不掷不消费；感染率**不乘**难度系数；`m_p` / `m_q` 取主导天赋（并列按 农 → 商 → 仕 → 工）（007 contracts/illness-lifespan.md §4/§5） |
| 饥馑死亡判定（**007 追加**，⑧-e） | 严重期计口每人 1 次 `r < FamineDeathChance`（5%，**不乘**难度系数）；`Relief` / 未进入饥馑 0 次 | 扣体质后归零者直接登记死亡、不掷（007 contracts/illness-lifespan.md §6） |
| 寿终与夭折（**007 追加**，⑧-f） | 夭折：`AgeAt < 3` 且为生日月 ⇒ 1 次 `r < ChildbirthPolicy.InfantDeathChance × InfantDeathFactor(难度)`（基准 10%，系数 0.5/1.0/1.1/1.2 ⇒ 5%/10%/11%/**12%**，落差档按规格书 §9.4 明示端点）；寿终 0 次 | 遍历 = 在册 ∪ 已外嫁归档；`Lifespan` 不重掷不设上限（007 contracts/illness-lifespan.md §7） |
| 科举判定（**005 追加**，⑨ ④） | 录取（解试/省试）或进甲（殿试）：1 次 `NextDouble()`；**进甲者紧接 1 次 `Next(1, 4)`** 取一甲名次 | 仅考试月的候选成员各 1 次，逐人按 `PersonId` 升序；**报名阶段（①）MUST NOT 消耗随机**；免解者、判定前资格不符者与现金不足弃考者 MUST NOT 消耗随机；殿试不掷录取骰（契约十三 §1.3） |
| 及第入仕待阙（**005 追加**，⑨ ⑤） | 1 次 `Next(6, 25)` —— 待阙月数（经 `AppointmentEntry` 入口） | 仅供本月及第入仕者各 1 次，逐人按 `PersonId` 升序（契约十三 §1.3） |

**条款**
1. MUST NOT 使用全局随机源、系统时钟或 `Guid.NewGuid()`（章程原则 IV；G-07）。
2. MUST NOT 因分支短路而改变消费次数：未命中的计息节点**不消费**随机数，但它的「不命中」
   必须只由 `Loan.MonthsSinceInterest` 决定，不得依赖任何随机结果。
3. 同一种子 + 同一初始 `GameState` + 同一月份序列 → 逐位相同的 `SettlementResult` 序列、
   逐位相同的资金池与账本（SC-006）。

## 5. 生活费（§5.1；E-06/E-10）

```text
月应付生活费 = 30 × 米价系数 × 难度支出系数
               × Σ各计口成员[ 日耗            // 取自 LivingCostTable[档位][年龄档]
                              × 农出身独立乘区 // 农：成年 0.90、未成年 0.80；非农 1.00
                              × 生活费一般乘区 ] // 1 + Σ同区百分比修正（加算）
```

- **两个成员乘区 MUST 在 Σ 内按成员取值**：农出身独立乘区与生活费一般乘区都只由「该成员是否
  未成年（即是否儿童档）」决定，故 MUST 逐成员（等价地：逐年龄档）相乘后再求和——提到 Σ 外按
  家族统一取一次会在儿童与成人混居的家庭里算出不同金额（`LivingCostCalculator.Compute` 的实现
  即按年龄档逐档相乘）。只有米价系数与难度支出系数是家族共有的、可提到 Σ 外的因子。
- **一般乘区的当前成员**：未成年 −50%（所有出身共有）、救济期全体支出 −20%（§5.4）。
  二者**加算**：`Relief` 期的未成年人 = `1 − 0.5 − 0.2 = 0.3`。米价系数与难度支出系数
  **各自独立相乘**，不进一般乘区（R-17）。
- **年龄档归属**：未成年（男 <12 / 女 <14）一律儿童档；已成年且 ≤18 岁青年档；≥60 岁老人档；
  其余成人档；**生日当月即转档**（§5.1；R-06）。
- **救济期的判定用当月应付额**（即减免后的值）；`Severe` 阶段不再减免，应付额 = 正常档。
- **支付**：先现金、后储蓄；可及储蓄 = 储蓄全额（§5.4 + spec Assumptions）。**支付能力口径 =
  现金 + 储蓄**（可付额，见 R-05；**MUST NOT** 把商本计入）：
  - 现金 + 储蓄 ≥ 应付额 → 足额支付；
  - 现金 + 储蓄 < 应付额 → **部分支付**：`LivingCost` 条目金额 = `min(应付额, 现金 + 储蓄)`，
    现金与储蓄清空，差额**不入账、不转贷款**（E-06；§5.4「不存在主动借贷」）。
- **MUST NOT** 出现负余额，MUST NOT 把生活费缺口写成负数条目。

## 6. 收入（§5.2、§5.3、§8.1、§11；E-02/E-03/E-15/E-16/E-18）

| 来源 | 触发条件 | 归属与金额 | 条目类别 |
| --- | --- | --- | --- |
| 自耕 | 有田且有**计口成年成员** | 亩数按 **`(1+农/200)×(1+工/400)` 降序**依次填满每人 **20 亩**（并列时按年龄降序、再按 `PersonId`）；每人 `亩数 × 0.5 ÷ 12 贯`，乘 **该成员本人** 的农/工乘数 | `SelfFarmingIncome`（归属该成员） |
| 田租 | 有非自耕田亩 | `亩数 × 0.1 ÷ 12 贯`；乘 **家主** 的农/工乘数（家主为 `null` 或非计口时取 `1.0`） | `LandRentIncome`（家族级） |
| 务农 | **家族无田可耕 = `Holdings.FarmlandMu == 0`**（与自耕互斥） | **每名计口成年成员** `2 贯/月`；乘 **本人** 的农/工乘数 | `FarmingWageIncome`（归属该成员） |
| 做工 | **≥1 名计口成年成员指派为「做工」** | **每名被指派者** `1.5 贯/月`，乘 **本人工** `(1+工/400)`；持有城市宅时再按**本人工乘数降序**取前 `min(做工人数, 城市宅数)` 名，各追加 `1 贯/月 × 本人 (1+工/400)`（并列时按年龄降序、再按 `PersonId`），**逐人各落一条** | `CraftingIncome`（归属该成员，一人一条） |
| 经商 | 商本池 ≥ 100 贯 **且** ≥1 名计口成年成员指派经商 | **单一份（不按人头重复，非「无归属角色」）**：`商本 × 2% × (1+商/200) × (1+工/400)`，商出身再 `×1.1`；多名被指派者取乘数最大者（并列时按年龄降序、再按 `PersonId`） | `TradeIncome`（归属**被采用的那名成员**，`PersonId != null`） |
| 官俸 | `SalaryModePolicy.Of(person)` 的**三态**判定（见下方三态表；**003 改写**，FR-021） | 在任全俸、致仕半俸、待阙与无官**不发**；乘区见三态表 | `OfficialSalary`（归属该成员） |
| 铺面租 | 持有铺面 | `间数 × 300 贯 × 20% ÷ 12` | `ShopRentIncome`（家族级） |
| 工出身 bonus | 12 月 **且** 出身 = 工 **且** 总资产为正 | `总资产 × 6%`，**乘**难度收益系数 | `ArtisanBonus`（家族级） |
| 储蓄利息 | 12 月 | 见 §7 | `SavingsInterest`（家族级） |

**收入来源彼此叠加**：`自耕`、`务农`、`做工`、`经商` 等不互相排斥——E-02 已裁决自耕与田租
**无需职业指派**（只由田地与计口成年成员数决定），因此一名成员可以同时贡献自耕收入与做工收入。
职业指派（`Occupation`）是**单一取值**，故「做工」与「经商」互斥、不可同时指派
（R-19 第 1、6 条）。

- **可指派与收入人力口径（E-16）**：凡本表出现的「计口成人」「成人」「成年成员」均指
  **计口 ∧ 成年**——「成年」按 §4.3（男满 12 / 女满 14，生日当月生效）判定且**无年龄上限**
  （青年档与老人档均可被指派、可贡献人力），未成年（儿童档）MUST NOT 被指派「做工」或「经商」，
  也 MUST NOT 计入自耕/务农的人头数或 20 亩容量。
- **务农判据（E-17）**：「家族无田可耕」的判据是 `Holdings.FarmlandMu == 0`；有田且存在计口
  成年成员时自耕必然产生（第 21 亩起转田租），故 MUST NOT 出现「有田却仍发务农」的路径。
- **城市宅加成（E-15）**：`+1 贯/月` 的份数 = `min(做工人数, 城市宅数)`，**每份归属一名**
  被指派者（按本人工乘数降序取前 `min(...)` 名），并乘**该成员本人**的 `(1+工/400)`；
  `Holdings.CityHomes == 0` 时该项退化为 0（资产被卖光不得中断结算）。
- **难度收益系数**：除储蓄利息外，**全部**收入乘之（§5.2 括注、§11、FR-012）。
- **总资产**（工 bonus 与未来罚金共用）= `现金 + 储蓄 + 田宅铺市值 − 贷款本金 − 欠息`，
  **不含商本池**；为 0 或为负则本年不发（§5.2 的 §17 裁决）。
- **「≥ / ≤」的边界**：商本 100 贯**含**（`≥ 100`），每人 20 亩**含**（第 21 亩起转田租），
  每项边界 MUST 有独立断言（spec Edge Cases）。
- **家主缺失时**：`HeadId` 为 `null`、或家主已非计口成员时，家族级来源（田租）的乘数取 `1.0`
  （E-03 的边界）；该情形 MUST 有独立断言。
- **资产被卖光或从未持有**：对应来源金额退化为 0，结算 MUST NOT 中断（spec Edge Cases）。

**官俸三态表（003 改写，FR-021）**——判定与优先级从上到下，与 [契约七 §5](../../003-opening-assets-officialdom/contracts/official-career.md) 逐字一致：

| 三态 | 判定 | 月俸 |
| --- | --- | --- |
| `Retired` | `Rank != null` ∧ `Retired` | `年俸 ÷ 12 × 难度收益系数 ×（士出身 ×1.05） × 50%` |
| `Active` | `Rank != null` ∧ ¬`Retired` | `年俸 ÷ 12 × 难度收益系数 ×（士出身 ×1.05）` |
| `AwaitingPost` | `Rank == null` ∧ `AwaitingPost` | **不发**（MUST NOT 落 0 金额条目） |
| `None` | 其余 | **不发** |

- 半俸 `50%`（`OfficialCareerPolicy.RetirementSalaryRatio`）MUST **只在 `IncomeCalculator.AddSalaries` 里乘一次**，
  MUST NOT 累乘（写成 25% 即 bug）；`SalaryModePolicy` MUST NOT 返回已打折的金额。
- 「士出身 ×1.05」MUST NOT 与 §10.2 的「仕身份」（`HasShiStatus`）混为一谈——`IncomeCalculator` MUST NOT 接收仕身份（002 与 003 的一致口径）。
- 人群口径沿用本节开头的**计口成员**：服刑与外嫁者不产生俸禄条目。

## 7. 储蓄与贷款（§5.4；R-08/R-11）

| 项 | 规则 |
| --- | --- |
| 当年储蓄利率 | 每年 **1 月**第⑤步**无条件** roll 一次（`0.5%~2.4%`），与年份一起存储，当年内不变；次年 1 月重新 roll |
| 储蓄计息 | 每年 **12 月**第⑤步：`利息 = 当时储蓄本金 × 当年利率`，**并入储蓄本金**（复利），落 `SavingsInterest` 条目（入账目标 = 储蓄） |
| 计息计时 | `Loan.MonthsSinceInterest` 每个结算月 +1，达 **12** 时计息并归零；按**自然月**推进，与净利润是否为负无关（E-07）；本金为 0 时计息额为 0 |
| 计息 | 第⑦步**先**执行：`欠息 += 当时本金 × 新 roll 利率`；本金 MUST NOT 增加 |
| 划扣 | 第⑦步**后**执行：`划扣额 = min(净利润 × 比例, 本金 + 欠息)`；净利润 ≤ 0 → 不划扣、无罚则、**不重置也不跳过**计时 |
| 划扣比例 | `HasShiStatus` → 20%；否则 `Origin == Merchant` → 80%；其余 → 40%（§5.4、§10.2） |
| 冲减 | **先本后息**：先冲 `Principal`，不足部分再冲 `AccruedInterest`；落 1~2 条资金类条目，金额为 0 的部分不落条目 |
| 结清 | 本金与欠息**皆清**即结清，此后不再产生划扣条目 |
| 净利润 | `本月全部收入（含储蓄利息与工 bonus，皆按 §6 的系数口径） − 本月生活费支出`；**不**扣除划扣本身，**不**含资产买卖的现金流 |
| 支付原语 | `PaymentPrimitive.Pay`：现金 → 储蓄 → **余额转贷款**（FR-017）；签名 `Pay(FamilyEconomy economy, LedgerCategory category, GameDate date, Money amount, PersonId? personId = null)`——**006 追加可选 `PersonId?` 形参**，默认 `null` 使既有行为逐位不变；**006 是本原语的首个产品调用方**（行贿支出 `BribeExpense`、罚金 `Fine`），见 [契约十六 §4.3](../../006-bribery-punishment/contracts/punishment.md) |

**MUST NOT**：把利息并入本金；对已结清贷款继续划扣；划扣超过剩余债务（多还会产生规格书
没有的「退款」资金流）。

## 8. 饥馑状态机（§5.4；R-12）

`FamineStage ∈ { None, Famine, Relief, Severe }`，`ElapsedMonths` 记录**本阶段**已持续月数。

判定次序（第⑥步内，**先解除、后升级**）：

1. **先把本阶段计时 +1**（`FamineState.Tick()`；阶段为 `None` 时无动作）。这一步 MUST 发生在
   下面的阈值比较**之前**（E-14）——转入当月已记 1，故「满 3 月」在**第 3 个饥馑月当月**成立。
2. 算当月应付额（`Relief` 期 × `0.8`，其余为正常档）。
3. 现金 + 储蓄 ≥ 应付额 → 足额支付；若此前阶段非 `None`，落 `FamineResolved` 事件条目并 `Clear()`
   （阶段归零、计时清零，MUST NOT 残留）。**当月结束**。
4. 否则部分支付（§5），再按转移表推进：

| 起始 | 触发 | 结果 | 条目 |
| --- | --- | --- | --- |
| `None` | 未能足额支付 | `Famine`（计时 1） | `FamineEntered` |
| `Famine` | 推进后计时 ≥ **3**（即第 3 个饥馑月当月）仍未能足额支付 | `Relief`（计时重算为 1，期限 12） | `FamineReliefEntered` |
| `Relief` | 推进后计时 ≥ **12**（即第 12 个救济月当月）仍未能足额支付 | `Severe`（计时重算为 1） | `FamineSevereEntered` |
| `Severe` | —— | 保持 | — |

5. 每个月的阶段迁移 MUST **至多触发一次**；同一个月份 MUST NOT 既转阶段又重复计时
   （spec Edge Cases）。
6. **体质 −10/月、每人每月 5% 死亡判定与救济期的体质减免**已由 **007（逻辑轨 ⑦）** 交付
   （2026-10-17；第 ⑧-e 子步 `FamineHealth.Run`）——本阶段（第⑥步的饥馑状态机）**仍 MUST NOT**
   实现它们；阶段与剩余月数 MUST 可读（`Stage`、`ElapsedMonths`；剩余月数 = 时限 − 已持续月数），
   ⑦ 的 ⑧-e **只读** `Stage` 而 MUST NOT 改写本阶段机。
   见 [007 contracts/illness-lifespan.md §6](../../007-family-lifecycle/contracts/illness-lifespan.md)。

**边界断言（E-14）**：「恰好第 3 月」转 `Relief` 与「恰好第 12 月」转 `Severe` MUST 各有一条
独立断言，且第 2 月与第 11 月 MUST NOT 触发；`Relief` 的 −20% 自转入后的**次月**应付额起生效
（转入当月的应付额已按饥馑阶段算出）。

## 9. 本契约明确不包含

随机事件与灾年米价跳涨、绝嗣判定、
存档落盘与成就、调试控制台入口。以上各项在本阶段**没有写入通道**（不可达），而非「暂未实现」。
**待阙/授官/考课/致仕原列于本清单，已由 003（逻辑轨 ③）在第④步「官吏推进」交付**，见
[契约七](../../003-opening-assets-officialdom/contracts/official-career.md)；
**科举季触发与功名变迁原列于本清单，已由 005（逻辑轨 ⑤）在第⑧步「科举季」交付**，见
[契约十三 §1](../../005-exam-cycle/contracts/exam-session.md) 与 005 FR-019；
**罚金与 12 格惩罚矩阵、连坐与服刑计时原列于本清单，已由 006（逻辑轨 ⑥）在第③步「惩罚推进」交付**，见
[契约十六 §1](../../006-bribery-punishment/contracts/punishment.md) 与 006 FR-020；
本清单其余各项仍成立。

> **006 后记（§9 清单，2026-10-13）**：「**罚金与 12 格惩罚矩阵**」与「**连坐与服刑计时**」两项
> 自本清单**移除**——它们已由 006 交付：罚金走 `PaymentPrimitive.Pay(..., LedgerCategory.Fine, ...)`，
> 12 格惩罚矩阵由 `PunishmentImposer` 在行贿判定命中时施加，服刑/禁考/禁升计时由第③步
> `PunishmentAdvance.Run(state)` 建立与递减（见 [契约十六 §1](../../006-bribery-punishment/contracts/punishment.md)）。
> 本清单其余各项仍成立。

> **007 后记（§9 清单，2026-10-17）**：「**属性成长/衰老/疾病/死亡**」「**婚育与外嫁**」
> 「**买人口**」三项自本清单**移除**——它们已由 007（逻辑轨 ⑦）在第 ⑧ 步「人口与健康推进」交付：
> ⑧-b `Childbirth.Run`（分娩与遗传）、⑧-c `IllnessAdvance.Run`（患病与病程）、
> ⑧-d `GrowthAdvance.Run`（成长与遗忘）、⑧-e `FamineHealth.Run`（饥馑体质损失与死亡判定）、
> ⑧-f `LifespanAdvance.Run`（寿终与夭折）、⑧-g `DeathSettlement.Run`（死亡落账）、
> ⑧-h `HeadSuccession`（家主继任）、⑧-a `MarriageMarket.Run`（提亲与榜下捉婿），
> 婚嫁金额与买人口入口见
> [007 contracts/marriage-birth.md](../../007-family-lifecycle/contracts/marriage-birth.md)；
> 体质成长与疾病死亡口径见
> [007 contracts/illness-lifespan.md](../../007-family-lifecycle/contracts/illness-lifespan.md)。
> 本清单其余各项（随机事件与灾年米价跳涨、绝嗣判定、存档落盘与成就、调试控制台入口）仍成立。

> **005 后记 · 快照面（2026-10-12）**：`SettlementResult` 随之新增**必填子段**
> `public required ExamSeasonSnapshot ExamSeason { get; init; }`（类型 `KFL.Rules.Exam.ExamSeasonSnapshot`），
> 由第⑦步产生。它是**增量、只读**的当月快照——只含本月发生的事，MUST NOT 回写 `GameState`、
> MUST NOT 成为与 `Person` 并存的第二真源；全部集合**按 `PersonId` 升序**，含
> `Level`（`ExamLevel?`，非考试月为 `null`）、`AutoApplications`、`Absences`、`Exemptions`、
> `Outcomes`、`Appointments`、`SpecialTributeInquiries` 与 `HasShiStatusBecomesTrue`；
> **路费金额不进快照**（已在 `Entries` 的 `ExamTravelCost` 条目里，避免第二个金额真源）。
> 形状细则见 [契约十三 §9](../../005-exam-cycle/contracts/exam-session.md)。

> **006 后记 · 快照面（2026-10-13）**：`SettlementResult` 随之新增**必填子段**
> `public required PunishmentSnapshot Punishment { get; init; }`（类型 `KFL.Rules.Punishment.PunishmentSnapshot`），
> 由第③步「惩罚推进」产生。它与 `ExamSeason` 同构：**增量、只读**的当月快照——只含本月发生/推进的
> 惩罚事实，MUST NOT 回写 `GameState`、MUST NOT 成为与 `Person` / `Family.PunishmentJournal` 并存的
> 第二真源。字段：`Month`（`GameDate`）与六个 `IReadOnlyList<…Fact>` 集合 `Bribes` / `Sentences` /
> `Fines` / `RelativeBans` / `CollectivePunishments` / `Releases`（全部**按 `PersonId` 升序**），另有
> `FamilyBanDeclared`（`bool`）与 `PrisonerCount`（`int`，当月在册服刑人数）。
> 快照内容来自第③步对 `Family.PunishmentJournal` 的投影与清空。
> 形状细则见 [契约十六 §9](../../006-bribery-punishment/contracts/punishment.md)。

> **007 后记 · 快照面（2026-10-17）**：`SettlementResult` 随之新增**必填子段**
> `public required LifecycleSnapshot Lifecycle { get; init; }`（类型 `KFL.Rules.FamilyLifecycle.LifecycleSnapshot`），
> 由第 ⑧ 步「人口与健康推进」产生（公开属性 20 → **21**）。它与 `ExamSeason` / `Punishment`
> 同构：**增量、只读**的当月快照——只描述本月发生的事（`Births` / `Deaths` / `Marriages` /
> `MarriedOut` / `Purchases` / `Illnesses` / `Crossings` / `PendingProposal` / `NewHeadId` /
> `HeadBecameVacant`），MUST NOT 承载累计值、MUST NOT 成为与 `Person` / `Family` 并存的第二真源；
> `Deaths` 的死因 `DeathCause`（寿终/病亡/饥馑/夭折/分娩）**只在此段出现**，MUST NOT 落
> `Person` 字段或账本条目。形状与不变量见
> [007 data-model.md §3.2/§6](../../007-family-lifecycle/data-model.md)。
