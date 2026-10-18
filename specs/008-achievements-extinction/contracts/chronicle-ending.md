# 契约二十四：绝嗣判定与编年史结局

**特性**：`008-achievements-extinction`
**上游**：规格书 §15（:643-646）；`spec.md` FR-026~FR-032、FR-012、FR-013 ③、Assumptions 5~7、19；契约二十二 §2
**下游**：U2「结局画面」（编年史七项 + 读档/回主菜单）；`specs/004-save-system/contracts/save-format.md`（结束态的三个键）
**状态**：活工件，MUST 与实现一致。

> 本契约是**绝嗣与结局**的判定口径唯一副本。编年史七项的**取值**逐条在此钉死；
> 数值真源仍是《科举浮生录规格书》§15，本契约只回答「从哪个既有面取、按什么时点取」。

---

## §1 编年史七项数据来源

**条款 1**：`ChronicleSnapshot` 的七个成员 MUST 在**绝嗣成立的那一次 `Advance`** 内一次算定并冻结：

| # | 成员 | 口径 | 数据来源（既有面） |
| --- | --- | --- | --- |
| ① | `SurvivedYears` | 存续年数 = `CurrentDate.Year − 1` | §2 |
| ② | `Generations` | 传承代数 = `Family.Members.Max(p => p.Generation) + 1` | `KFL.Core/Entities/Family.cs`、`Person.Generation` |
| ③ | `PeakTotalAssets` | 历史月末总资产极大值 | `AchievementState.PeakTotalAssets`（累计量 ④，`data-model.md §2.4`） |
| ④ | `HighestDegree` | 族谱历史最高功名 = **全量** `Family.Members` 的 `Person.CurrentDegree` 的最大 `DegreeLevel`（**含已亡与外嫁归档**；空族谱取 `DegreeLevel.None`） | `Family.Members` 全量的 `Person.CurrentDegree`（由 `Person.DegreeHistory` 末条派生，`src/KFL.Core/Entities/Person.cs:173`） |
| ⑤ | `TotalJinShi` | 族谱历史累计进士人数（**按人计**） | `Family.Members` 全量中 `DegreeHistory` 含 `DegreeLevel.JinShi` 的成员数 |
| ⑥ | `AverageLifespan` | 已故成员享年分布的三均值 | `AchievementState.Lifespans`（累计量 ③）+ §4 |
| ⑦ | `Genealogy` | 全量 `Family.Members.Select(p => p.Id)`（**含归档**） | `Family.Members` |

**条款 2（交叉引用，MUST NOT 复制）**：③ MUST 读累计量 `PeakTotalAssets`，MUST NOT 在本契约内重算
「月末总资产」；总资产的算式唯一出处是 `src/KFL.Rules/Economy/TotalAssetsCalculator.cs` 的 `Of`
（现金 + 储蓄 + 田宅铺市值 − 贷款）。

**条款 3**：④⑤ MUST 遍历**全量** `Family.Members`（含 `StatusFlag.Deceased` 与外嫁归档），
MUST NOT 只数在册 —— 与 FR-028 的「含已亡与外嫁」和 `Assumptions 14`（历史口径数全量成员）一致。
这两项**不在**绝嗣后重算，也**不被**一致性校验（契约二十三 §7 的排除清单）。

**条款 4**：⑦ MUST 是 `PersonId` 的**次序敏感**序列，MUST NOT 复制 `Person` 的字段
（`research.md` R-09）：结局画面需要的姓名/功名由 UI 用 `Genealogy` 回查当前存档的族谱；
绝嗣后成员仍在 `Family.Members` 里（只是不在册），故回查必有结果。

---

## §2 存续年数

**条款 5**：`SurvivedYears = state.CurrentDate.Year − 1`。

**理由**：开局固定在**第 1 年 1 月**（`src/KFL.Rules/Start/NewGameSetup.cs` 的 `StartOfGame`），
且判定发生在月结算第 ⑩ 步**之后**（`CurrentDate` 已推进到下一月）。
故「第 1 年结束时」存续 **1** 年；若在第 1 年 1 月绝嗣，存续 **0** 年。
`Assumptions 5` 与 `spec.md` FR-028 ① 逐字如此。

**条款 6（与成就门槛的关系，口径已在契约二十二 §2 钉死，此处只登记一致性）**：

| 成就 | 门槛 | 首次成立时的存续年数 |
| --- | --- | --- |
| `CenturyHouse`（百年世家） | `SurvivedYears > 100` | 102（该月 `CurrentDate.Year == 102`） |
| `MillenniumHouse`（千秋万代） | `SurvivedYears > 300` | 302（该月 `CurrentDate.Year == 302`） |
| `NarrowEscape`（绝处逢生，地狱） | `SurvivedYears >= 100` | 100（该月 `CurrentDate.Year == 101`） |
| `HellEnding`（大梦一场） | 地狱 ∧ 绝嗣 | 任意 |

**注意**：「超过 100 年」**不含** 100（§14.1:611 逐字「存续时间**超过** 100 年」），
而「绝处逢生」§14.1:570 无「超过」字样 ⇒ 取 `>=`（`spec.md` 次级口径 A）。

**条款 7**：`SurvivedYears` MUST 用**整数年**的差，MUST NOT 换算成月数再除 12。

---

## §3 绝嗣判据与在册口径

**条款 8（判据，唯一实现面）**：`Family.RegisteredMembers.Count == 0`。

`RegisteredMembers` 的定义唯一出处是 `src/KFL.Core/Entities/Family.cs`（既有成员），
本契约 MUST NOT 重写其谓词。口径逐字（规格书 §15:645）：

| 状态 | 是否算在册 |
| --- | --- |
| 存活且在家 | ✅ 算 |
| 服刑（`StatusFlag.Imprisoned`） | ✅ 算 |
| 待阙（`awaitingPostRemainingMonths` 非空 / `StatusFlag.AwaitingPost`） | ✅ 算 |
| 患病（`StatusFlag.Ill`） | ✅ 算 |
| 致仕（`StatusFlag.Retired`） | ✅ 算 |
| **外嫁归档**（`StatusFlag.MarriedOut`） | ❌ **不计** |
| 已亡（`StatusFlag.Deceased`） | ❌ 不计 |

**条款 9（必测清单第 9 条的落点）**：FR-048 要求的「绝嗣判定（外嫁不计）」由
`契约二十二 §5` 登记的测试映射覆盖；**本契约是那条口径的权威来源**，
测试 MUST 至少含四个用例：① 仅剩外嫁归档者 ⇒ **绝嗣**；② 仅剩服刑者 ⇒ **不绝嗣**；
③ 仅剩待阙者 ⇒ **不绝嗣**；④ 仅剩患病者 ⇒ **不绝嗣**。

**条款 10（明确排除）**：`SettlementResult.Lifecycle.HeadBecameVacant` / `Family.HeadId == null`
**不是**绝嗣判据（`spec.md` Edge Cases 逐字写明）。家主空缺与家族绝嗣是两件事：
前者可由女性家主或旁系继任化解，后者是族无人。

**条款 11**：判据 MUST NOT 计入 `Family.SettledOutsiders` / `MarriageAlignedOutsiders`
（那是离婚/外嫁的归档清单，与「在册」无关）。

---

## §4 平均寿命：Tukey 三均值

**条款 12（输入）**：`AchievementState.Lifespans` —— **已故成员享年的完整序列**（累计量 ③，
FR-013 ③ 明写「MUST 为完整序列而非『和 + 个数』」）。逐项在成员死亡的**当月**追加其
`Person.AgeAt`（死亡当月享年），包含：夭折、连坐处死、饥馑/患病死亡、外嫁归档成员。
`n == 0` ⇒ 结果 `0m`，MUST NOT 抛异常、MUST NOT 返回 `null`。

**条款 13（算法）**：

```
输入 lifespans = [x_1 … x_n]（已按升序排序的副本；MUST 排序，MUST NOT 原地改累计量序列）

n == 0            ⇒ 0m
排序 s = sort(lifespans)
Q2 = median(s)                                  // n 偶数 ⇒ 中间两数均值；n 奇数 ⇒ 正中元素
Q1 = median(s[0 .. mid])                        // 下半：n 偶数取 [0, n/2)；n 奇数取 [0, (n+1)/2) 含中位
Q3 = median(s[mid .. n-1])                      // 上半：n 偶数取 [n/2, n)；n 奇数取 [n/2, n) 含中位
TM = (Q1 + 2·Q2 + Q3) / 4
返回 TM，保留 3 位小数，MidpointRounding.AwayFromZero
```

**条款 14（Tukey hinges 的关键口径）**：**奇数样本时，中位数（`Q2`）MUST 同时计入下半与上半**。
这就是 R `fivenum` 的口径，也是 `Assumptions 7` 的判别例所要钉死的东西。
MUST NOT 采用「奇数样本弃中位」的变体。

**条款 15（金色算例，MUST 逐行进测试）**：

| 输入 `lifespans` | Q1 / Q2 / Q3 | 期望结果 | 备注 |
| --- | --- | --- | --- |
| `[50]` | 50 / 50 / 50 | `50.000` | 单元素 |
| `[40, 60]` | 40 / 50 / 60 | `50.000` | 偶数 |
| `[10, 50, 90]` | 30 / 50 / 70 | `50.000` | 偶数样本性质：`Q1 = (10+50)/2` |
| `[10, 40, 60, 70]` | 25 / 50 / 65 | `47.500` | |
| `[10, 20, 50, 60, 70]` | 20 / 50 / 60 | `45.000` | 奇数，中位计入两半 |
| `[10, 20, 30, 40, 100]` | 20 / 30 / 40 | **`30.000`** | **判别例** |
| `[]` | — | `0.000` | 空序列不抛 |
| `[1..9]` | 3 / 5 / 7 | `5.000` | |

**条款 16（判别例的用意）**：`[10, 20, 30, 40, 100]` 若误用「奇数样本弃中位」，
将得 `Q1 = 15`、`Q3 = 70`、`TM = 36.250`。**测试 MUST 同时断言 `30.000` 与「≠ 36.250」**，
让这条口径无法被「顺手简化」悄悄改掉（`spec.md` SC-009 逐字要求）。

**条款 17（运算纪律）**：全程 `decimal`，MUST NOT 用 `double`/`float` 中转；
除法的唯一一步是 `(Q1 + 2·Q2 + Q3) / 4`；取整发生在**最后**，且只用
`Math.Round(value, 3, MidpointRounding.AwayFromZero)`。MUST NOT 对 Q1/Q2/Q3 各自取整后再合成。

**条款 18（放哪）**：算法住在 `src/KFL.Rules/Achievements/TukeyTrimean.cs`（`data-model.md §3.7`），
MUST NOT 塞进 `ExtinctionJudge` 或 `ChronicleBuilder` —— 它在本特性**只有一个调用点**：编年史 ⑥
（`ChronicleBuilder`）。ACH-012 的存续年数是**整数年差**（`CurrentDate.Year − 1`），与三均值无关。
MUST NOT 在判据与编年史里各写一份。

---

## §5 已结束存档的拒绝与冻结

**条款 19（拒绝）**：`TurnRunner.Advance` 的**第一行** MUST 是结束检查：

```csharp
if (state.Ending.IsEnded) throw new InvalidOperationException("家族已绝嗣，存档不可继续推进。");
```

**条款 20（零状态变更）**：抛异常时 MUST 已经发生**零**状态变更：随机源未被消费、账本无新条目、
`CurrentDate` 未推进、存档文件 mtime 不变。测试 MUST 断言这四点（`spec.md` SC-010 逐字要求
「随机未消费 / 无新账本条目 / mtime 不变」）。

**条款 21（异常类型）**：取 `InvalidOperationException`（`Assumptions 20`）；MUST NOT 自定义异常类型，
MUST NOT 返回「无变化的 `TurnOutcome`」——静默无操作会让调用方以为推进成功。

**条款 22（仍可用的能力，FR-030）**：已结束的存档 MUST 仍然可以**写盘 / 读回 / 改名 / 备份**：
`ISaveService.Save` / `Load` / `Rename` / 备份判定 MUST NOT 检查 `Ending.IsEnded`。
拒绝只发生在 `Advance`。理由：结局画面提供的「读档」本身就需要读；改名/备份是存档管理能力。

**条款 23（冻结）**：`EndingState.End` 是**幂等**的（`data-model.md §2.5`）：已结束 ⇒ 原样返回 `false`。
绝嗣成立后，成就判定 MUST 停止（`spec.md` Edge Cases「结束态冻结成就判定」）——
`AchievementAdjudicator.Judge` / `Report` 在 `state.Ending.IsEnded` 为真时 MUST 立即返回空结果，
MUST NOT 消费累计量、MUST NOT 追加待落盘项。

**条款 24（读档后的冻结）**：读回一个已结束的存档 MUST 逐字段复原 `EndedAt` 与 `Chronicle`，
使 `Ending.IsEnded == true`；MUST NOT 有「读档后结束态丢失、可以继续推进」的路径
（`spec.md` SC-011）。

---

## §6 判定次序

**条款 25（先成就、后绝嗣）**：绝嗣成立的**当月**，`AchievementAdjudicator.Judge` MUST 先判 68 条成就、
再判绝嗣并冻结 `Ending`。这样：
1. 触发绝嗣的那一次结算产生的成就事实（如 `路有冻死` = 当月饥馑死亡）能正常解锁；
2. 绝嗣自身解锁的成就（`烟消云散` ACH-044、`大梦一场` ACH-048）在同一轮内入 `PendingUnlocks`，
   随即由 ⑤ 落全局（契约二十三 §5）；

**条款 26**：本契约是「先成就后绝嗣」的**唯一副本**（`spec.md` Assumptions 19 取其为准），
`data-model.md §5.1` 与 `TurnRunner` 的注释只引用不复制。测试 MUST 用
「同月既满足某成就判据又绝嗣」的用例钉死次序（`spec.md` Edge Cases 要求「明写并由测试钉死」）。

**条款 27**：绝嗣与成就 MUST 在同一次编排里完成（FR-032），MUST NOT 拆成两次 `Advance`。

---

## §7 与 §15 / FR-028 的对应与排除项

**条款 28**：规格书 §15:643-646 逐字要求的七项与 §1 的表 MUST 一一对应；
「提供读档 / 回主菜单」是**界面**义务（U2），本契约只保证数据齐备，MUST NOT 在规则层做任何导航。

**条款 29（明确不包含）**：
- 不实现结局画面/弹窗（界面轨）；
- 不实现「无胜利条件」之外的任何终局分支 —— 规格书 §15 只有绝嗣一种；
- 不做「家族复兴/续存」（绝嗣后族谱冻结、存档只读推进）；
- 不给 `ChronicleSnapshot` 加「结局原因」字段（`data-model.md §2.7`：原因只有一种，加字段等于为
  不存在的分支留位置）。

**条款 30**：本契约的七项与 `AchievementState` 的累计量 ④⑥ 有**单向读取**关系
（编年史读累计量，累计量不读编年史）；这个方向 MUST NOT 反转（读档时的复原次序依赖它：
先复原 `Achievements`，再由 `Ending.Restore` 复原快照）。

---

## 后记

- **与契约二十三 §7 的呼应**：一致性校验**只**校验能由当前状态证伪的东西（`Genealogy` 的次序、
  `IsEnded` 与 `EndedAt`/`Chronicle` 的三态一致、`SurvivedYears == EndedAt.Year − 1`），
  **不**校验 `PeakTotalAssets` / `HighestDegree` / `TotalJinShi` / `AverageLifespan` 的重算一致 ——
  它们是冻结的历史，重算只会在合法档上误报（契约二十三 条款 33 的排除清单）。
- **数字口令**：本契约的算例表（条款 15）是测试的**逐行**依据；改这里 MUST 同时改
  `spec.md` Assumptions 7（真源）与 `tests/` 的对应断言（SC-009）。
- **与 004 的关系**：`EndingStateDto` 的三个键（`endedYear` / `endedMonth` / `chronicle`）与
  `ChronicleDto` 的七个键已登记在契约二十三 §6 的键表里；本契约不重复键表。
