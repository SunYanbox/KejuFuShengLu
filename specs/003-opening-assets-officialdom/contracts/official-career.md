# 契约七：官吏体系（待阙 / 授官 / 考课 / 政绩 / 致仕）

**Feature**: `003-opening-assets-officialdom` | **Date**: 2026-10-08

本契约固定 §8.2 全部条目的**状态机、月内次序、三态俸禄与拒绝矩阵**。它是 US2~US4 与
SC-003~SC-007 的可验收边界。字段与不变量见 [data-model.md](../data-model.md) §2/§3，
数值出处见 [contracts/config-registry.md](./config-registry.md)。
**本契约只消费** `PromotionBanned` / `ServingSentence` 状态位与计时，MUST NOT 建立或递减它们（逻辑轨 ⑥）。

## 1. 参与状态

| 概念 | 表达 | 说明 |
| --- | --- | --- |
| 待阙 | `StatusFlag.AwaitingPost` + `StatusTimers.AwaitingPostRemainingMonths` + `Person.EntryTrack` | 剩余月数为 0 时 MUST NOT 保留状态位（FR-013）；入仕途径与待阙同生命周期 |
| 在任 | `Person.Rank != null` ∧ ¬`Retired` | 官阶唯一存储处是 `Person.Rank`（001 已交付） |
| 致仕 | `StatusFlag.Retired`（官阶**保留**） | 幂等；MUST NOT 清官阶 |
| 在职月数 | `Person.MonthsInOffice`（`>= 0`） | 授官时置 0，逐月 +1，判定后重置为 0 |
| 政绩 | `Person.Merit`（`>= 0`） | 上限由 `OfficialCareerPolicy.MeritMaximum` 在推进时钳制 |
| 三态 | `SalaryMode`（**派生**） | `None` / `Active` / `AwaitingPost` / `Retired`，判定见 §5 |

**非官员不产生任何仕途状态变化**：`Rank == null` ∧ ¬`AwaitingPost` 的成员在月度推进里 MUST NOT 被写。
**推进的成员集合** MUST 为 002 的**在册**口径（`CountedMembers.Registered`：未亡且未外嫁，**含服刑
与待阙**），按 `PersonId` 升序处理；**已亡与外嫁者 MUST NOT 被推进**（其待阙计时与仕途随归档停止，
spec Edge Case）；服刑者仍在集合内、按下一条**显式暂停**。
**服刑者全程暂停**（Q5 裁决）：其待阙计时、在职计时与政绩**不推进，也不重置**；俸禄本就为 0（不计口）。

## 2. 月内次序（FR-019；③ 是本特性新增的整步）

```text
① 提升待生效的难度与生活费档位                      （002 原有）
② 米价系数游走并 clamp                              （002 原有）
③ 官吏推进（成员按 PersonId 升序）                  ← 本特性新增
   ③-a 待阙计时递减；递减到 0 的当月【授官】（按入口记录的入仕途径）
   ③-b 政绩 +1（在任者；钳制 ≤ MeritMaximum）
   ③-c 致仕判定（在任 ∧ 年龄 ≥ RetirementAge ⇒ 置 Retired）
   ③-d 在职计时 +1；命中 AppraisalPeriodMonths ⇒ 考课判定
④ 收入（俸禄按三态计）                              （002 原有）
⑤ 生活费 + 饥馑状态机                               （002 原有）
⑥ 贷款先计息、后划扣                                （002 原有）
⑦ AdvanceMonth                                      （002 原有）
```

| 条款 | 要求 |
| --- | --- |
| 1 | ③ MUST 在**收入（④）之前**：当月授官者当月起领俸、当月满 70 岁者当月即按半俸计（FR-019） |
| 2 | ③-a ~ ③-d 的相对次序 MUST 固定；单测 MUST 能逐步断言（例如「同月待阙期满且满 70 岁」⇒ 先授官、后致仕，当月按**新授官阶**的半俸计） |
| 3 | ③-c MUST 在 ③-d 之前：满 70 岁当月的成员 MUST NOT 再接受考课，故 MUST NOT 出现「同月既升一级、又按升级后的级别计半俸」；③-d 的条件另含 `AgeAt(month) < RetirementAge`（§4 条款 3），使该保证在 ③-c 落地之前也成立 |
| 4 | 本月 ③-b 的 +1 MUST 计入本月到期的考课概率（先加政绩、后判概率） |
| 5 | 同一月内每名成员的「授官 / 晋升 / 致仕」各 MUST 至多发生一次；MUST NOT 出现「有官阶却从未授官」或「先致仕、后授官」 |
| 6 | 成员处理顺序 MUST 为 `PersonId` 升序（保证「同种子 → 同结果」，章程原则 IV） |

## 3. 待阙与授官

| 条款 | 要求 |
| --- | --- |
| 1 | 「及第入仕」入口 MUST 把无官职的进士置为待阙，剩余月数 = `Next(AwaitingPostMinMonths, AwaitingPostMaxMonths + 1)`（**整数均匀、含两端点**，消耗 1 次 `Next`） |
| 2 | 入口 MUST NOT 写官阶、MUST NOT 写 `MonthsInOffice`、MUST NOT 动账本 |
| 3 | 待阙期间该成员 MUST NOT 产生俸禄条目（其计口身份照常计入生活费，§5.1） |
| 4 | 剩余月数 MUST 逐月递减 1；**递减到 0 的当月**授官：读 `Person.EntryTrack` 得 track，写 `Rank = InitialRankOf(track)`、`MonthsInOffice = 0`、清 `AwaitingPostRemainingMonths`、`EntryTrack` 与 `AwaitingPost` 位（**先清计时与途径、后清位**） |
| 5 | 授官 MUST 幂等/互斥：已有官阶或已在待阙者再触发入口 MUST 被拒绝，且 MUST NOT 重置剩余月数 |
| 6 | 初始官阶 MUST 按 track 映射：`FirstClass→L11`、`SecondClass→L13`、`ThirdClass→L15`、`SpecialTribute→L18`；track 取自**入口写入** `Person.EntryTrack` 的值，MUST NOT 在授官时从功名记录重新派生（待阙期内功名记录可能被逻辑轨 ⑤/⑥改写） |
| 7 | 进士的甲第缺失（末条记录 `Class == null`）或与名次矛盾时，入口 MUST **拒绝**，MUST NOT 猜一个等级 |
| 8 | 官阶级数 MUST 始终落在 `SalaryTable.HighestLevel ~ LowestLevel`（无 L0、无 L19） |

## 4. 政绩与考课

| 条款 | 要求 |
| --- | --- |
| 1 | 在任者每月政绩 **+1**，MUST NOT 超过 `MeritMaximum`（100） |
| 2 | 非官员、待阙者、已致仕者 MUST NOT 增长政绩（服刑者同，见 §1） |
| 3 | 在职月数 MUST 每月 +1（授官当月记 1），`>= AppraisalPeriodMonths`（36）的当月开展考课判定；判定条件 MUST 同时含 `AgeAt(month) < RetirementAge`，使「满 70 岁当月不参与考课」在 ③-c 落地前后都成立（与 §2 条款 3 的 ③-c 构成冗余双闸） |
| 4 | 考课概率 MUST = `min(PromotionBaseChance + merit × PromotionChancePerMerit, PromotionChanceCap)`；判定 MUST 恰好消耗 1 次 `NextDouble()` |
| 5 | 判定成功 ⇒ 官阶级数 **−1**（数值更小 = 更高品）；失败 ⇒ 不变；`Level == HighestLevel` 时成功 MUST 维持 L1，MUST NOT 越界 |
| 6 | 判定结束后（成功、失败、或**因禁升被跳过**）在职月数 MUST 重置为 0；**服刑者不构成该情形**——服刑是**暂停**（不推进、不重置），MUST NOT 记成「跳过」 |
| 7 | **禁升**（`PromotionBanned`）期内到期的判定 MUST 被**跳过**——既不掷骰（MUST NOT 消耗随机）也不升迁，且 MUST NOT 补判；「跳过」（禁升，重置计时）与「暂停」（服刑，不重置）是**两种语义**，快照里 MUST 分别记为 `AppraisalSkipped` 与 `AppraisalPaused` |
| 8 | 本特性 MUST NOT 递减、新建或清除 `PromotionBanRemainingMonths` |
| 9 | 「第 35 个在职月 MUST NOT 判定、第 36 个在职月判定」两个方向都必须有断言 |

## 5. 俸禄三态

| 三态 | 判定（优先级从上到下） | 月俸 |
| --- | --- | --- |
| `Retired` | `Rank != null` ∧ `Retired` | `年俸 ÷ 12 × 难度收益系数 ×（士出身 ×1.05） × 50%` |
| `Active` | `Rank != null` ∧ ¬`Retired` | `年俸 ÷ 12 × 难度收益系数 ×（士出身 ×1.05）` |
| `AwaitingPost` | `Rank == null` ∧ `AwaitingPost` | **不发**（MUST NOT 落 0 金额条目） |
| `None` | 其余 | **不发** |

| 条款 | 要求 |
| --- | --- |
| 1 | 三态 MUST 都乘**难度收益系数**；「出身 = 士」的成员 MUST 再 ×1.05（FR-021） |
| 2 | 「士出身 ×1.05」与 §10.2 的「仕」身份 MUST NOT 混为一谈：本特性 MUST NOT 读取 `HasShiStatus` 来定俸禄 |
| 3 | 半俸比例 MUST **只在 `Settlement/IncomeCalculator.AddSalaries` 里乘一次**（系数取 `OfficialCareerPolicy.RetirementSalaryRatio`）；`Career/SalaryModePolicy` MUST NOT 返回已打折的金额、MUST NOT 接收 `Money` / `Origin` / `Difficulty`（§5 的唯一施加点）。致仕 MUST 幂等，MUST NOT 累乘（不得从 50% 变 25%） |
| 4 | 俸禄条目归属 MUST 为该成员本人；MUST NOT 落成家族级条目 |
| 5 | 俸禄条目类别 MUST 沿用既有的 `LedgerCategory.OfficialSalary`（本契约 MUST NOT 新增账本类别） |
| 6 | 人群口径 MUST 沿用 002 的「计口成员」：服刑与外嫁者 MUST NOT 产生俸禄条目，但其档案仍可读 |

## 6. 致仕

| 条款 | 要求 |
| --- | --- |
| 1 | 在任者 `AgeAt(month) >= RetirementAge`（70，生日当月即算）⇒ 状态置 `Retired`、**官阶保留** |
| 2 | 自**当月**起按半俸发放至寿终（当月即生效） |
| 3 | 致仕后 MUST NOT 再增长政绩、MUST NOT 再推进在职计时、MUST NOT 再参与考课；**致仕当月仍有一次政绩 +1**（③-b 先于 ③-c，该成员此刻仍在任），但该月 MUST NOT 参与考课——「致仕后」的停摆自**次月**起算 |
| 4 | 致仕 MUST 幂等：已致仕者再判 MUST 无变化 |
| 5 | 从未有官职的 70 岁成员 MUST NOT 产生 `Retired` 状态（无官可致仕） |
| 6 | `Retired` MUST 与其它状态位（患病 / 饥馑 / 禁考 / 服刑 / 待阙 / 外嫁 / 已亡）并存且互不覆盖 |

## 7. 拒绝与幂等矩阵

| 触发 | 结果 |
| --- | --- |
| 「及第入仕」而该成员已有官阶 | 拒绝（`InvalidOperationException`） |
| 「及第入仕」而该成员已在待阙 | 拒绝，且 MUST NOT 重置剩余月数 |
| 进士入口而末条功名不是进士 | 拒绝 |
| 进士入口而甲第缺失 | 拒绝 |
| 待阙剩余月数递减到 0 | 授官（幂等：只在转 0 的那一次发生） |
| 同一成员在同一月再次被判致仕 | 无变化（幂等） |
| 服刑者遇到 ③-a/③-b/③-d | **暂停**（不推进、不重置，Q5）——与「禁升跳过」不是同一语义 |

**失败原子性**：全部拒绝路径 MUST 在校验通过**之前**不产生任何写入（与 002 的
`FamilyEconomy.Apply` 同一口径：不存在「改了一半」的中间态）。

## 8. 随机消费次序（逐条断言）

```text
米价系数游走（②）
  → 考课掷骰（③-d，仅命中周期的成员，按 PersonId 升序，每人 1 次 NextDouble）
  → 储蓄利率（④ 内的 1 月年度项）
  → 贷款计息利率（⑥）
```

| 条款 | 要求 |
| --- | --- |
| 1 | 待阙**时长**的随机**不在**月度步骤内：它属于「及第入仕」入口（契约六 §3 是**开局**的次序，两者相互独立） |
| 2 | 「未命中 36 个月周期」与「因禁升被跳过」都 MUST NOT 消耗随机（是否命中由状态决定，不是随机决定） |
| 3 | 相同种子 + 相同推进序列 MUST 得到相同的官阶轨迹、待阙月数与政绩序列 |

## 9. 快照与断言面

`SettlementResult.Career` MUST 提供本次结算的逐人增量：授官（成员 → 新官阶）、政绩增量、
晋升（旧级 → 新级）、致仕、`AppraisalSkipped`（因禁升跳过，计时已重置）与 `AppraisalPaused`
（因服刑暂停，计时未重置）这一对**不可合并**的记录、以及每人的三态。
它是**增量快照**，MUST NOT 成为与 `Person` 并存的第二真源（MUST NOT 存「当前官阶表」之类的副本）。

## 10. 本契约明确不包含

科举周期与功名推进、特奏名的触发与「可拒绝」（逻辑轨 ⑤）；贿赂、惩罚矩阵、连坐、死刑、
服刑与禁考/禁升计时的建立与递减、释放降级（逻辑轨 ⑥）；疾病、寿命与死亡判定、家主继任
（逻辑轨 ⑦）；存档落盘与成就（逻辑轨 ④/⑧）；界面（界面轨 U1~U5）；官名表（规格书未定义）。
