# 契约二十：成长、疾病、疗程、饥馑、寿数与夭折（007 · 逻辑轨 ⑦）

**Feature** | **Date**: 2026-10-17 | **Spec**: [../spec.md](../spec.md) | **Plan**: [../plan.md](../plan.md)

> 本契约是**公式与短路**的权威。数值成员名在 `src/KFL.Rules/Config/GrowthPolicy.cs` 与
> `IllnessPolicy.cs`（见 [config-registry.md](./config-registry.md)）。上游契约：
> [契约五 §8 饥馑状态机](../../002-monthly-settlement-economy/contracts/monthly-settlement.md)。

## 1. 成长（⑧-d，零随机）

| 项 | 规则 | 配置成员 |
| --- | --- | --- |
| 读书加成 | `Occupation == Occupation.Studying` ⇒ 每月 `+1.5` | `GrowthPolicy.StudyPerMonth` |
| 延名师 | `Person.IsTutoredByMaster` ⇒ 每月额外 `+2.25`，且**先**扣 `5 贯 × ExpenseFactor` | `TutorStudyBonusPerMonth`、`TutorFeeGuanPerMonth` |
| 士出身 | `Origin == Scholar` ⇒ 乘 `1.10` | `ScholarStudyMultiplier` |
| 遗忘 | `AgeAt > 16` 且 `Occupation != Studying` ⇒ 每月 `-0.5` | `ForgetPerMonth`、`ForgetAgeThreshold` |
| 患病期 | `Ill` 置位 ⇒ **当月全部学业变化减半** | `IllnessStudyPenaltyDivisor` |
| 上限 | `Study <= 100` | `AttributeLimits.Max` |

**取整口径（Assumption 13，封印）**：全部加成先用 `decimal` **累加**（含患病减半），
最后一次 `MidpointRounding.AwayFromZero` 取整，再 `Math.Clamp(0, 100)` 后写入 `Person.Study`。

**名师费次序**：①核对 `CanCover(5 贯 × ExpenseFactor)` → ②付得起则 `Pay(TutorFee)` 并给加成；
付不起 ⇒ **按普通速度**、`IsTutoredByMaster` **保持**、MUST NOT 转贷款（Assumption 9）。
付费**先于**加成计算。

`AgeAt > 16` 的边界：`AgeAt == 16` 不遗忘、`AgeAt == 17` 起遗忘（「16 岁以上」取严格大于；
该边界 MUST 与 `GrowthPolicy.ForgetAgeThreshold == 16` 单点对应）。

## 2. 体质（三向变化 + 饥馑，分属三个子步）

| 来源 | 子步 | 变化 | 条件 |
| --- | --- | --- | --- |
| 自然恢复 | ⑧-d | `+1`（上限 100） | 生活费**足额付清**、非饥馑、**非患病** |
| 患病扣减 | ⑧-c | `-(2 + 8s)`（`2 ~ 10`） | `Ill` 置位且**过了宽限月** |
| 饥馑扣减 | ⑧-e | `-10` | 全家处于 `FamineStage.Severe` |
| 救济期 | — | **不减**（含患病减免） | `FamineStage.Relief` 或 `ReliefMode` 期间 **MUST NOT** 下降体质（Assumption 4，取字面） |

**MUST NOT 互替**：患病与饥馑各扣一次；两者同月 ⇒ 两次都扣。**写入前 MUST 自行 clamp 到 `0`**，
`Person.Health` 的 setter 越界会抛 `ArgumentOutOfRangeException`（`Person.cs:157`）。

**归零后果**：`Health == 0` ⇒ 由**产生该次扣减的子步**登记死亡（⑧-c 患病 ⇒ `Illness`；⑧-e 饥馑 ⇒ `Famine`），
口径是「先扣体质、再判死亡」；**死亡优先于清除判定**（同月既扣到 0 又掷到清除 ⇒ 仍死亡）。

## 3. 患病概率（⑧-c 第二段，新感染）

```
c   = clamp(当月初 Health, IllnessPolicy.HealthFloor(1), IllnessPolicy.HealthCeiling(100))
p   = ( IllnessPolicy.InfectionChanceAtFloorHealth(0.40)                     // 体质 = 1 时 40%
      + (InfectionChanceAtFullHealth(0.0025) - InfectionChanceAtFloorHealth) // 体质 = 100 时 0.25%
        × (HealthCeiling - c) / IllnessPolicy.InterpolationSpan(99) ) × m_p
```

- 百分数口径；单次 `NextDouble()` 与 `p` 比较。
- `m_p` 由**主导天赋**（四项 `Talents` 最高者；并列按 **农 → 商 → 仕 → 工**）决定：
  农 `0.80`、仕 `0.95`、工 `1.10`、商 `1.05`。**乘算**，MUST NOT 二次封顶（FR-029 ②）。
- **MUST NOT** 乘 `DifficultyRates.NegativeEventFactor`（Q6；四难度同种子同结果）。
- `AgeAt < 3` ⇒ **不掷判定、不消费随机**（FR-029 ① 的「恰 0 次」）。
- 体质取**当月初值**（⑧-c 第一段已扣完旧病，故新感染的 `c` 是扣减后的当月初值）。

**命中后**：`Status |= Ill`；紧接 `1 次 NextDouble` 定 `s`（均匀 `[0,1]`），
`Timers.IllnessSeverityBasisPoints = round(s × 10000)`；`Timers.TreatmentRemainingMonths = null`。
`s` MUST NOT 逐月重掷、MUST NOT 设预设病程。

## 4. 病程推进与痊愈（⑧-c 第一段）

⑧-c 的**内部次序**是本契约最容易被写反的一条：

```
第一段：对「进入本子步前 Status 已含 Ill」的成员，按 PersonId 升序：
   ① 若在疗程中（TreatmentRemainingMonths > 0）：付药费（CanCover → Pay(MedicineExpense)）
      付不起 ⇒ 疗程中止（TreatmentRemainingMonths = null），本月按未治疗口径
   ② 扣体质 -(2 + 8s)                       // 先扣
   ③ 体质归零 ⇒ 登记死亡(Illness)，本成员结束
   ④ 掷清除（见 §4.1）—— 命中 ⇒ 解除 Ill、清 Timers 两项
   ⑤ 疗程月数递减（>0 时）
第二段：对「健康」成员，按 PersonId 升序：按 §3 掷感染，命中则置 Ill（不扣体质、不掷清除）
```

**宽限月的表达**：新确诊者只在第二段被置 `Ill`，当月自然不再进入第一段 ⇒ 0 扣体质 / 0 药费 /
0 清除判定。MUST NOT 为此新增 `Person`/`StatusTimers` 字段（FR-029 ④）。

### 4.1 清除概率

```
b   = 3 <= AgeAt < 12 ? IllnessPolicy.ClearBonusChild(0.60)
    : 12 <= AgeAt < 24 ? IllnessPolicy.ClearBonusYouth(0.30)
    : 0
q   = 未治疗 ?  min(1, Health × IllnessPolicy.BaseRecoveryCoefficient(1.00) / 100 × m_q + b)
    : 疗程中  ?  min(1, (Health × IllnessPolicy.TreatedRecoveryCoefficient(1.10) / 100
                          + IllnessPolicy.TreatedRecoveryOffset(0.20)) × m_q + b)
```

- `Health` 取**扣减之后**的值（先扣后掷）。
- `m_q`：农 `1.20`、仕 `1.10`、工 `1.10`、商 `0.95`（主导天赋同 §3）。
- **短路**：`min(1, …)` 钳到 1 ⇒ **必愈且 MUST NOT 消耗随机**（FR-029 ④、Assumption 15）。
  已消费的随机只有「命中且 < 1 时的那一次 `NextDouble`」。
- 清除命中后该月 MUST NOT 再有后续随机消费（Assumption 15）。
- 未治疗不过期：不清除就一直患病（每月继续扣体质、继续掷）。

### 4.2 疗程（`LifecycleEntry.StartTreatment`）

| 项 | 值 |
| --- | --- |
| 期限 | `IllnessPolicy.TreatmentMonths`（3 个月） |
| 起算 | 患病**次月**（宽限月内 MUST NOT 支付药费） |
| 月费 | `(IllnessPolicy.MedicineBaseGuan(0.05) + IllnessPolicy.MedicineSeverityGuan(0.45) × s) 贯 × ExpenseFactor`，落 `MedicineExpense` |
| 结束 | 疗程月数归零后仍未愈 ⇒ 可**再发起**；不发起则按未治疗口径继续 |
| 不治疗 | 不付药费、清除概率用 §4.1 的未治疗式 |

药费档位是**开放项**（`spec.md` 的 Assumptions 开放项 ②）：若实机观察治疗过便宜，MAY 升到
`0.08 + 0.55s` / `0.10 + 0.70s`，但 MUST NOT 与下调痊愈概率同时进行——**实现 MUST NOT 自行调整**
（须先经规格书 §17 裁决）。

## 5. 饥馑（⑧-e）

1. 只**读** `GameEconomy.Famine.Stage`，MUST NOT 改写 002 的阶段迁移/时限/折扣。
2. `FamineStage.Severe` ⇒ 计口每人 `-10` 体质，随后每人 `1 次 NextDouble` 与
   `GrowthPolicy.FamineDeathChance`（5%）比较，命中即登记死亡（`Famine`）。
3. 救济期（`Relief` / 12 个月）⇒ 体质**不减**、**不掷**死亡判定。
4. 付得起即解除饥馑（由 002 的 ⑥ 步决定，本特性只跟随）。

## 6. 遗传（⑧-b 第 ③ 步，`Inheritance`）

```
mean   = (father + mother) / 2
sigma  = |father - mother| / 2 + ChildbirthPolicy.InheritanceSigmaBase(5)
天赋/体质: value = clamp(round(NextNormal(mean, sigma)), 0, 100)
体质另加:  value = max(value, ChildbirthPolicy.HealthFloor(20))
学业:      value = clamp(round(NextNormal(mean × ChildbirthPolicy.StudyMeanHalving(0.5), sigma)), 0, 100)   // 同一 sigma
```

- 复用 `AttributePolicy.NextNormal`（`src/KFL.Rules/Config/AttributePolicy.cs:66-82`，**恰 2 次 `NextDouble`**）。
- 取整 `MidpointRounding.AwayFromZero`。
- 任一方缺失（`FatherId`/`MotherId` 为 `null`）⇒ **取无父母参照者分布**
  （天赋 `N(60,20)`、学业 `N(30,15)`、体质 `N(85,10)`），MUST NOT 单亲外推。

## 7. 天命寿数与夭折（⑧-f）

### 7.1 寿终

`Person.AgeAt(date) >= Person.Lifespan` ⇒ 当月登记死亡（`OldAge`）。

- `Lifespan` 在出生瞬间由 `NextLifespan` 定，**MUST NOT 重掷、MUST NOT 设上限**（只 clamp 下界）。
- **服刑 / 患病 / 外嫁 均不豁免**（FR-032）。
- 遍历集合 = `RegisteredMembers ∪ ArchivedMembers` 中的**自然人**（外嫁只承载本判定，其余月度判定 MUST NOT 施加）。

### 7.2 夭折

| 项 | 值 |
| --- | --- |
| 判定次数 | **3 次**：出生当月 + 满 1 岁生日月 + 满 2 岁生日月 |
| 单次概率 | `ChildbirthPolicy.InfantDeathChance`（10%）；简单 `5%`、地狱 `12%` |
| 截止 | `AgeAt >= 3` 后**不再判定**、**不消费随机** |
| 累计 | ≈ 27.1%（`1 - 0.9³`） |
| 命中 | 登记死亡（`Infancy`）；`Status |= Deceased` 由 ⑧-g 统一置 |

**判据（派生，不落字段）**：`AgeAt(date) < 3 && BirthDate.Month == date.Month`
⇒ 当月是生日月（出生当月天然满足）。这与「每年掷一次」等价，且**零新增字段**（R-12）。

## 8. 死亡结算与继任（⑧-g / ⑧-h）

⑧-g：按 `PersonId` 升序，对登记表去重后的每个人 `Status |= Deceased`、进 `ArchivedMembers`、
落 `MemberDeceased` 事件条目；**不**改动 006 的处死路径行为。

⑧-h：在 ⑧-g 之后**一次**（MUST NOT 每死一个继任一次），按四档取候选：

| 档 | 候选集 |
| --- | --- |
| ① | 已亡家主的在册男性子女 |
| ② | 已亡儿子的在册男性后代（代际就近优先，同代取 `AgeAt` 最大） |
| ③ | 已亡家主的亲兄弟（在册男性） |
| ④ | 在册男性中 `AgeAt` 最大者；同岁取子代数更多者；再同按 `PersonId` 升序 |

- 候选 = **在册男性**（含服刑、待阙），排除已亡与外嫁。
- 无候选 ⇒ `Family.SetHead(null)`，事实段 `HeadBecameVacant = true`；**不构成绝嗣**。
- 现任家主仍健在且未外嫁 ⇒ 本子步**不做任何事**（不消耗随机、不写 `NewHeadId`）。

## 9. 验证物

| 断言 | 落点 |
| --- | --- |
| 患病概率逐点（100 ⇒ 0.25%、1 ⇒ 40%、中间插值）与四组 `m_p` | `IllnessTests.cs` |
| `s` → 扣减 / 药费映射 | `IllnessTests.cs` |
| 「先扣体质、再掷清除」次序 | `IllnessTests.cs` |
| 基础清除 `体质×1.00/100`（100 ⇒ 必愈 0 随机、10 ⇒ 10%） | `IllnessTests.cs` |
| 宽限月（0 扣减 / 0 药费 / 0 清除随机） | `IllnessTests.cs` |
| 疗程 3 月、`>= 1` 短路、未治疗不过期 | `IllnessTests.cs` |
| 患病概率不乘难度系数（四难度同种子同结果） | `IllnessTests.cs`（SC-014） |
| 免病年龄 `<3` 与三段增益边界 2/3/11/12/23/24 | `IllnessTests.cs` |
| 遗传 σ 逐点、学业折半、clamp、体质下限 20、无父母四分布 | `InheritanceTests.cs`（SC-003） |
| 到龄当月死亡 + 服刑/患病/外嫁三态 | `LifespanTests.cs`（SC-004） |
| 存活阈值金色表（容差 ±0.5） | `LifespanTests.cs`（SC-015） |
| 夭折三档 | `ChildbirthTests.cs` |
| 继任四档 + 空缺 | `HeadSuccessionTests.cs`（SC-005） |
| 体质 +1 / 患病 2~10 / 饥馑 −10 / 救济期不减 / 归零死亡 | `FamilyLifecycleTests.cs` |
