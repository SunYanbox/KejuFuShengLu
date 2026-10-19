# 契约十九：婚配、生育与收养（007 · 逻辑轨 ⑦）

**Feature** | **Date**: 2026-10-17 | **Spec**: [../spec.md](../spec.md) | **Plan**: [../plan.md](../plan.md)

> 本契约是**资格、金额与动作次序**的权威。数值本身在 `src/KFL.Rules/Config/MarriagePolicy.cs`、
> `ChildbirthPolicy.cs`、`PopulationPurchasePolicy.cs`（见 [config-registry.md](./config-registry.md)）。
> 上游契约：[契约五 §3 计口筛选](../../002-monthly-settlement-economy/contracts/monthly-settlement.md)、
>[契约十一 新局装配](../../003-opening-assets-officialdom/contracts/new-game-setup.md)。

## 1. 阶层判定（`MarriageClass`）

**单一判据**，MUST NOT 另立：

```
 if (family.HasShiStatus)                  -> Official   // 仕族：优先
 else if (origin is Commerce or Scholar)   -> Scholar    // 士人
 else                                      -> Commoner   // 平民（Farming / Artisan）
```

- 「家族」的阶层由 `Family.HasShiStatus`（`src/KFL.Core/Entities/Family.cs:48`）判定，**不是**逐人。
- `Origin` 取 `GameState.Origin`（`src/KFL.Core/Entities/GameState.cs:92`）。
- **MUST NOT** 新增「家族声望」属性（Q1 裁决）。

## 2. 婚配资格（全部同时成立）

1. 我方成员：`CountedMembers.IsRegistered`（未亡、未外嫁）、`SpouseId == null`、
   `AgeBracketPolicy.IsAdult(gender, AgeAt(date))`（`src/KFL.Rules/Config/AgeBracketPolicy.cs:42-50`，男 12 / 女 14）。
2. 对象：年龄与成员年龄之差 `<= MarriagePolicy.CandidateAgeSpread`（5），且成年判据同上。
3. 一夫一妻：`SpouseId != null` 者不可再婚；丧偶（`FormerSpouseIds` 非空且 `SpouseId == null`）**可以**再婚。
   丧偶的终止动作由 ⑧-g `DeathSettlement` 对**一切死因**统一施加（`Family.EndMarriage`，FR-021），
   与具体死因（患病/寿终/饥馑/分娩/处死）无关。
4. 家族内部婚配**只**允许「收养的旁系成员（`Person.IsOutsider`）× 家族内成员」；
   血亲之间 MUST NOT 成婚（用 `FatherId`/`MotherId` 相同或互为亲子判定，禁止祖先/同胞）。
5. `Lifecycle.Proposal` 非空且未过期时，`TryMatchmake` MUST 返回 `NotMarriageable`
   （必须先应答或让提议失效，避免一个月内两条婚配线并行）。

## 3. 托媒（`LifecycleEntry.TryMatchmake`）

| 项 | 值（配置成员） |
| --- | --- |
| 费用 | `MarriagePolicy.MatchmakerFeeGuan`（3 贯）`× DifficultyRates.ExpenseFactor(Difficulty)` |
| 支付 | 现银 + 储蓄**全额**；不足 ⇒ 拒绝（`InsufficientFunds`），**随机 0 次**、不转贷款、不记 0 金额条目 |
| 成功率 | `MarriagePolicy.MatchmakerSuccessChance`（50%），承接方向**不影响**成功率（Assumption 16） |
| 失败 | 不退费；除 ④a 的当月名额记录外 `Lifecycle` 无变化；同一成员同一月至多一次（名额落 `FamilyLifecycleState.MatchmakeApplications`，只在扣款成功后追加；下次尝试自然落在下月） |

**动作次序（固定）**

```
① 校验资格（纯，失败即返回；0 次随机）
①a 当月名额（`Lifecycle.HasAppliedForMatchmaking`，命中 ⇒ AlreadyAppliedThisMonth；0 次随机、0 次支付）
② 算托媒费 → ③ CanCover → ④ Pay(MatchmakerFee)          // 现银+储蓄，恒不转贷款
④a RecordMatchmakeApplication（扣款成功后即占名额，成败一律占）
⑤ 掷成功判定（1 次 NextDouble）—— 失败 ⇒ 返回 Accepted=false（不退费）
⑥ 抽对方阶层（1 次 NextDouble 的分段映射；三档权重见 §5）
⑦ 初始化候选者：年龄（1 次 Next）→ 性别（1 次 Next）→ 天赋 ×4（4×2 次 NextDouble）
   → 学业（2 次）→ 体质（2 次）→ 寿数（2 次）→ 姓名（INameGenerator）
⑧ 婚嫁金额（§5）→ ⑧a CanCover(我方应付额)（不足 ⇒ 整单失败、不退费；§5 的支付能力校验）
   → Family.Marry（:245）→ 账本条目 → 事件条目 MarriageFormed
⑨ 若方向为「嫁出」：归档（§6）
```

**候选者初始化** MUST 复用无父母参照者口径（`AttributePolicy.NextTalent/NextStudy/NextHealth/NextLifespan`），
随机次序照 `specs/003-opening-assets-officialdom/contracts/new-game-setup.md:59-79`：
**年龄 → 天赋 ×4 → 学业 → 体质 → 寿数 → 姓名**。`Lifespan` 只 clamp 下界、无上界。
姓取 `Family.Name`（娶入配偶冠家族姓氏，Assumption 2）。

## 4. 随机提亲与榜下捉婿（⑧-a）

### 4.1 提亲（`Kind = Matchmaker`）

| 项 | 值 |
| --- | --- |
| 频率 | **每月至多 1 次** |
| 概率 | `MarriagePolicy.ProposalBaseChance`（5%）`× DifficultyRates.NegativeEventFactor(Difficulty)`（简单 2.5% / 普通 5% / 困难 6.5% / 地狱 7%） |
| 候选集合 | 我方全部满足 §2 第 1 条的成员，**先按 §5 权重抽阶层**（复用 ⑤ 的同一次概率值分段，0 次额外随机），再在「该阶层下、本家付得起其应付额」的子集（娶入 = 对方阶层聘礼、嫁出 = 本家阶层嫁妆）里均匀抽 1（`Next(0, 候选数)`，1 次额外随机）；子集为空则不提议（概率值已消费、状态不变） |
| 生效 | 只写 `Lifecycle.Proposal`，**不改任何 `Person` 状态**；当月未应答即失效 |

### 4.2 榜下捉婿（`Kind = ImperialProposal`）

| 项 | 值 |
| --- | --- |
| 触发窗口 | 新科**一甲及第**的**次月**（`DegreeRecord.ImperialClass == 一甲` 且 `ChangedAt` 为上月） |
| 概率 | `MarriagePolicy.ImperialProposalBaseChance`（30%）`× NegativeEventFactor ÷ (1 + P)` |
| 官员压力 `P` | `OfficialPressure.Compute(family) = Σ OfficialPressure.WeightOf(level)`，`level` 取 `RegisteredMembers` 中 `Rank != null` 者 |
| `w(L)` | `MarriagePolicy.PressureWeightBottom`（0.02，L18）、`PressureWeightTop`（1.50，L1）、`LowestLevel`（18）、`HighestLevel`（1）；`w(L) = 0.02 + 1.48 × (18 − L) / 17` |
| 功名分档 | `P < 1` ⇒ 白身；`1 <= P < 3` ⇒ 举人；`P >= 3` ⇒ 贡士（**MUST NOT 进士**） |
| 金额 | **对方支付**给本家族：`Next(500, 1001) × (1 + P)` 贯，四舍五入到整贯；**不乘支出系数** |
| 不叠加 | MUST NOT 再走 §5 的基础聘礼/嫁妆 |
| 拒绝 | 无支付、无状态变更、提议失效；**名额已消耗**（MUST NOT 次月重掷） |

`P` MUST 是**判定时的快照**：当月 ④ 的任免已生效，当月死亡/继任**不回溯**；MUST NOT 落字段、MUST NOT 跨月缓存（Assumption 19）。

### 4.3 应答（`LifecycleEntry.RespondToProposal`）

- `accept = false` ⇒ 清空提议，**0 次随机、0 金额、0 状态变更**。
- `accept = true` ⇒ 按提议快照里的属性把候选者落成一个 `Person`（经 `Family.Marry`）：
  - 基础婚配：`BetrothalGiftPaid`（娶入支出聘礼）+ `BetrothalGiftReceived`? **否**——
    娶入只记 `BetrothalGiftPaid`（支出对方阶层聘礼）与 `DowryReceived`（收入对方阶层嫁妆），见 §5。
  - 捉婿：只记 `DowryReceived`（对方支付的捉婿金额）。
  - 买功名婚姻：额外 `DegreeMarriageFeePaid`（§7）。
  - **支付能力校验**（§5）：我方应付额（娶入 = 提议快照的 `BetrothalGift`、嫁出 = `Dowry`）不足
    ⇒ `Accepted = false` + `InsufficientFunds`、**提议被清空（名额已消耗）**、不成婚、无婚嫁条目。

## 5. 婚嫁金额表（SC-007 的 24 格）

阶层金额（`MarriagePolicy`）：

| 阶层 | 聘礼（`...GiftGuan`） | 嫁妆（`...DowryGuan`） |
| --- | --- | --- |
| `Commoner` 平民 | 6 | 12 |
| `Scholar` 士人 | 12 | 18 |
| `Official` 仕族 | 30 | 60 |

方向语义（`x` = 对方阶层金额，`y` = 本家阶层金额，均 `× ExpenseFactor`）：

| 方向 | 支出 | 收入 |
| --- | --- | --- |
| **娶入**（对方成员进我家） | `BetrothalGiftPaid` = 对方阶层聘礼 | `DowryReceived` = 对方阶层嫁妆 |
| **嫁出**（我方成员去他家） | `DowryPaid` = 本家阶层嫁妆 | `BetrothalGiftReceived` = 对方阶层聘礼 |

⇒ 24 格 = 3 阶层 × 2 方向 × 4 支出系数（0.6 / 1.0 / 1.1 / 1.3）。断言在
`tests/KFL.Tests/Rules/MarriageTests.cs`，逐格 `Assert.Equal`。

**支付能力校验（§17 裁决 2026-10-17 Q7，规格书 §5.4）**：对方阶层确定后，「我方应付额」
（**娶入** = 对方阶层聘礼 `BetrothalGiftPaid`；**嫁出** = 本家阶层嫁妆 `DowryPaid`）MUST 先经
`PaymentPrimitive.CanCover` 校验；不足 ⇒ **整单失败**：

- `LifecycleOutcome.Accepted = false` + `LifecycleRejectReason.InsufficientFunds`；
- **不成婚**（不写 `SpouseId`、候选者不入册、嫁出不置 `MarriedOut`）、**无婚嫁条目**、成员状态零变更；
- **已付的托媒费与买功名费不退还**（`Outcome.Amount` 如实体现在此前的支付额上）；
- MUST NOT 转贷款（与生育费、收养价、药费、名师学费同一口径）；
- `LifecycleEntry.RespondToProposal` 的接受路径同理：校验不通过则**提议被清空**（名额已消耗）。

⇒ `SettleAmounts` 内部的 `PaymentPrimitive.Pay` 恒不会把缺口转成本金；查询面
`LifecycleQueries.MatchmakeFeasibility` 的 `BetrothalGift*` / `Dowry*` 只作**金额预览**
（可付性由调用方拿 `Treasury.Cash + Savings` 自行比较，`Available` 仍只表达资格）。

**买功名婚姻的额外项**（FR-017/FR-018；2026-10-19 第七轮 Q1/Q2 取消官职与功名前置）：

- **无前置**：任一可婚配成员皆可勾选；**仅娶入**可用，嫁出方向 ⇒ `DegreeMarriageBrideIntoOnly`
  （0 扣费、0 次随机、0 账本条目）。
- 配偶功名 = `LifecycleQueries.SpouseDegreeFor(member)` = `LowerDegreeOf(CurrentDegree) ?? JuRen`
  （进士 → 贡士 → 举人；举人与白身一律 **举人**），降级后学业按 `贡士 N(60,10)` / `举人 N(50,10)` 重掷；
  `DegreeRecord(cause: Initial)` 追加。
- 费用（`LifecycleQueries.DegreeMarriageFee`）：我方成员 `Rank != null` ⇒
  `SalaryTable.AnnualSalaryGuan(level) × MarriagePolicy.DegreeMarriageFeeRatio(1.50)`；
  `Rank == null` ⇒ **档位价**（`DegreeMarriageGongShiFeeGuan` = 180 贯 / `DegreeMarriageJuRenFeeGuan` = 60 贯，
  再乘支出系数）；娶入记 `DegreeMarriageFeePaid`。
- 嫁出且外嫁者本人 `Rank != null`：额外收入 `DegreeMarriageDowryReceived`
  = 其 `AnnualSalaryGuan(level) × 1.50`。

## 6. 嫁出与外嫁归档

1. 置 `Status |= StatusFlag.MarriedOut`，进 `Family.ArchivedMembers`；**不计口**、不可指派读书、不可科举。
2. 数据**保留**供 006 的连坐追及（`PunishmentQueries` 仍可读）。
3. 外嫁成员 MUST 继续承载**天命寿数判定**（⑧-f），但 MUST NOT 参与 ⑧-c/⑧-d/⑧-e；
   MUST NOT 重回在册（FR-032、Assumption 7）。
4. 丧偶一律经 `Family.EndMarriage`（`Family.cs:280`）；成婚一律经 `Family.Marry`（`:245`）。
5. 只剩外嫁成员时 MUST NOT 判绝嗣（绝嗣不在本特性，FR-046）。
6. **徽记文案**（2026-10-19 第七轮 Q3）：同一 `MarriedOut` 位按性别分读——本家出嫁女性显示「外嫁」，
   经 `Family.AddOutsider` 引入的男性婿显示「女婿」；派生集合口径不因文案而变。

## 7. 生育（`LifecycleEntry.ApplyChildbirth` + ⑧-b）

| 项 | 值 |
| --- | --- |
| 资格 | 双方均在册、`SpouseId` 双向一致、女方 `16 <= AgeAt(date) <= 45` |
| 频率 | **每对夫妇每自然年至多一次**（`Lifecycle.BirthApplicationYear` 与 `AppliedBirthCouples`） |
| 费用 | `ChildbirthPolicy.ChildbirthExpenseGuan`（10 贯）`× ExpenseFactor`，现银 + 储蓄全额，不足拒绝（0 随机） |
| 分娩 | **申请当月不分娩**；次月 ⑧-b 消费 `PendingBirths` |
| 拒绝原因 | `FertilityWindowClosed` / `AlreadyAppliedThisYear` / `InsufficientFunds` / `NotMarriageable` |

**⑧-b 的固定次序（逐对，按 `CoupleKey` 升序）**

```
① 掷分娩死亡（1 次 NextDouble，5% / 简单 2.5% / 地狱 7%）
   命中 ⇒ 母亲登记进 DeathRegister（DeathCause.Childbirth），新生儿**照常出生**
② 新生儿性别（1 次 Next(0,2)，50/50）
③ 遗传：天赋 ×4 → 学业 → 体质（公式见 [illness-lifespan.md](./illness-lifespan.md) §6）
④ 寿数（NextLifespan，只 clamp 下界）
⑤ 姓名（INameGenerator.NextGivenName(gender)，冠家族姓氏）
⑥ Family.AddChild（:143），辈分 = 父母辈分 + 1
⑦ ChildBorn 事件条目（金额 0 不入资金类）
⑧ 母亲死亡时 MUST 触发其配偶 SpouseId 置空
```

**条款**

1. 新生儿 MUST NOT 进家主候选（⑧-h 只取在册**男性**且 `AgeAt >= 0` 的自然成员；新生儿是自然人，
   但只要当月出生就不可能在「已亡儿子的后代」档外胜出——真正的排除来自「不判已亡者」，
   故不额外加年龄条件）。
2. 夭折判定在**同月 ⑧-f 的不同子步**（出生当月即第一次判定）；MUST NOT 在 ⑧-b 内判定。
   概率按难度分档 **简单 5% / 普通 10% / 困难 11% / 地狱 12%**（`ChildbirthPolicy.InfantDeathFactor`，
   规格书 §9.4 与 §17 Q3；详见 [illness-lifespan.md](./illness-lifespan.md) §7.2）。
3. `PendingBirths` 在 ⑧-b 处理完毕后 MUST 清空（`ConsumeBirths`）。

## 8. 收养（`LifecycleEntry.BuyPerson`）

| 项 | 值 |
| --- | --- |
| 定价 | 本年度第 n 名 = `PopulationPurchasePolicy.BasePriceGuan × n`（10n 贯），**无上限** |
| 重置 | 每年 1 月（`Lifecycle.NextPurchaseOrdinal` 内按年重置） |
| 支付 | 现银 + 储蓄全额；不足 ⇒ 拒绝 `InsufficientFunds`（0 随机、不转贷款、不记 0 金额条目） |
| 家主空缺 | `Family.HeadId == null` ⇒ 拒绝 `HeadVacant`（Assumption 8） |
| 成员初始化 | 年龄 `Next(8, 17)`（8~16 含端点）→ 性别 `Next(0,2)` → 天赋 ×4 → 学业 → 体质 → 寿数 → 姓名；`FatherId`/`MotherId` 均为 `null` |
| 姓与辈分 | 冠家族姓氏；辈分 = `Family.BoughtCollateralGeneration`（`:89-92`）= 家主辈分 + 1 |
| 入册 | `Family.AddOutsider`（`:176`）；计入在册与计口 |
| 条目 | 1 条 `PersonPurchase`（资金类，负额）+ 1 条 `PersonBought`（事件类） |

## 9. 验证物

| 断言 | 落点 |
| --- | --- |
| 24 格金额表逐格 | `MarriageTests.cs`（SC-007） |
| 提亲四难度概率、每月至多 1 次、阶层加权三档 | `MarriageTests.cs`（SC-013） |
| 捉婿 `P = 0` 退化 + P 单调性 + 功名分档四边界 | `MarriageTests.cs` |
| 拒绝后逐字段不变、0 支付、名额已消耗 | `MarriageTests.cs` |
| 托媒扣费/失败不退/资金不足拒绝 | `MarriageTests.cs` |
| 买功名降级与灰置 | `MarriageTests.cs` |
| 生育资格、年度一次、次月分娩、分娩死亡仍出生 | `ChildbirthTests.cs` |
| 夭折三档与累计 ≈ 27.1% | `ChildbirthTests.cs` |
| 收养 10/20/30 与跨年重置、`HeadVacant` | `PurchaseTests.cs`（SC-006） |
| 外嫁归档口径 | `FamilyLifecycleTests.cs` |
