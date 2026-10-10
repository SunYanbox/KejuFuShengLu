# Implementation Plan: 月度结算与经济引擎

**Branch**: `002-monthly-settlement-economy` | **Date**: 2026-10-06 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-monthly-settlement-economy/spec.md`

## Summary

交付规格书 §16 阶段②：把 001 交付的静态家族档案变成一台**会自己算账**的机器——每推演一个月，
按规格书 §5.1/§5.2 算生活费与各来源收入，按 §5.4 先本后息还债、储蓄生息、断炊时按四阶段时间线
演进，并把每一文钱的进出落成一条流水账条目（§12.3 的形状由 001 research R-16 定下）。

技术路线：**经济状态归 `KFL.Core`，结算编排归 `KFL.Rules`**。

- `KFL.Core` 新增 `FamilyEconomy` 聚合（现金 / 储蓄 / 商本 / 贷款 / 田宅铺 / 流水账 / 米价系数 /
  生活费档位 / 饥馑状态），挂在 `GameState.Economy` 上；它是**资金流动的唯一入口**——
  任何一次资金池变动都必须在同一调用里追加一条流水账条目，使「只动资金池而不落条目」
  在类型系统层面不可能。
- `KFL.Rules` 新增 `Config/`（生活费日耗表、收入系数、资产价格、利率区间、饥馑时间线、
  难度系数、18 级俸禄表、米价策略、划扣比例）与 `Settlement/`（`MonthlySettlementEngine`
  与五个纯函数：`LivingCostCalculator`、`IncomeCalculator`、`LoanSettlement`、`SavingsSettlement`、
  `FamineController`）、`Economy/`（资产买卖、支付原语）。
- 全部随机性经注入的 `IRandomService` 取得，消费次序（米价 → 储蓄利率 → 贷款利率）
  写进契约并逐条断言（章程原则 IV）。

本阶段**不做**界面、存档落盘、随机事件、属性成长/死亡、科举、罚金、婚育与授官（spec
`Out of Scope` 已逐条列出）。

## Technical Context

**Language/Version**: C# 14 / .NET 10（沿用 001 基线；`global.json` 仍只锁 .NET 10 版本带）。
非 UI 三层 `net10.0`、UI 两层 `net10.0-windows`，不新增工程、不新增框架。

**Primary Dependencies**: **零新增 NuGet 依赖**。本阶段只需要 001 已交付的 xUnit 测试栈与
`IRandomService` / `IGameClock` 两组接缝；章程依赖基线里的 CommunityToolkit.Mvvm、
MessagePack、Bogus、Serilog、LiveChartsCore 在本阶段仍然**没有消费者**（分别属界面轨 U1/
阶段④/界面轨 U1/界面轨 U1/U3）。

**Storage**: N/A。存档落盘（MessagePack/JSON/备份）属阶段④；本阶段只定义字段与聚合方式。
流水账按 001 R-16 登记为「追加式、条目数随月份线性增长」，落盘体量与分块仍在阶段④ 实测后决定。

**Testing**: xUnit v2 + VSTest，`dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false
-p:_MSTestEnableParentProcessQuery=false`（受限宿主的两个已知坑见 `AGENTS.md`）。
新增测试目录 `tests/KFL.Tests/Rules/`（经济规则）与 `tests/KFL.Tests/Core/` 扩写（新值类型与聚合不变量）；
全部无 UI、无文件系统、无网络。

**Target Platform**: Windows（WPF 表现层，本阶段不涉及）。规则层不引用任何平台类型。

**Project Type**: 桌面应用（WPF + MVVM）+ 分层类库，单仓库六工程（结构不变）。

**Performance Goals**: 无运行时性能目标（§3：逻辑推进仅由用户输入驱动）。
本阶段可衡量的只有门禁本身（构建零警告零错误、测试全绿）与 SC-001~SC-009 的可断言性。

**Constraints**:
- 章程原则 II：`KFL.Rules` 内 MUST NOT 出现 `DateTime.Now`、`Random`、文件系统、网络
  （G-07 静态断言，扫描范围本阶段扩至 `tests/KFL.Tests/Rules/`，见 R-14）。
- 章程「技术栈与工程约束」：金额一律以**文**为单位的 `decimal` 计算，1 贯 = 1000 文；
  任何规则数值 MUST 集中在 `KFL.Rules/Config/`（SC-008 要求「配置类之外不存在同一数值的第二份副本」）。
- 随机性只能来自注入的 `IRandomService`；「现在」只能来自注入的 `IGameClock`（契约二）。
- 地图与依赖边不新增：`KFL.Core ← KFL.Infrastructure ← KFL.Rules`（G-05 不变）。

**Scale/Scope**: 新增 8 个 Core 枚举文件（7 枚举 + 1 个与枚举同目录的元数据静态类
`LedgerCategoryMetadata`）、3 个 Core 值类型、6 个 Core 经济实体（聚合 + 成员）；
`KFL.Rules` 新增 10 个配置类（`LivingCostTable` / `AgeBracketPolicy` / `IncomeRateTable` /
`AssetPriceTable` / `InterestPolicy` / `FamineTimeline` / `GrainPricePolicy` / `DifficultyRates` /
`SalaryTable` / `LoanPolicy`）+ 11 个结算/经济类型（`SettlementResult`、`CountedMembers`、
`LivingCostCalculator`、`IncomeCalculator`、`MonthlySettlementEngine`、`LoanSettlement`、
`SavingsSettlement`、`FamineController`、`AssetMarket`、`PaymentPrimitive`、`GameConfig`）；
测试新增 10 个测试文件（`tests/KFL.Tests/Rules/` 的 T038~T041、T046、T049、T053、T055、T057、
T060；另有 Foundational 的 T024~T026）；0 个界面功能、0 行持久化代码。
**该行是生成前的粗估，此处按计划结构树校正。**

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| 章程条款 | 本计划如何满足 | 结论 |
| --- | --- | --- |
| **I. .NET 10 单一运行时基线** | 不新增工程、不改任何 `TargetFramework`；不改 `global.json`；不引入任何新依赖 | ✅ |
| **II. 分层解耦与单向依赖** | 经济**状态**（资金池、资产、流水账、米价系数、档位、饥馑）归 `KFL.Core`；结算**编排**（顺序、公式、状态机）归 `KFL.Rules`；`Core` 不引用 `Rules`（G-05 不变）；`Rules` 无 `DateTime.Now`/`Random`/文件/网络，随机与「现在」均由构造注入（`IRandomService`、`IGameClock`）；计算器全部是纯函数，实体只承载数据与自身不变量（`Loan` 的「先本后息」是单实体内不变量，跨实体编排只在 `MonthlySettlementEngine`） | ✅ |
| **III. WPF + MVVM 表现层契约** | 本阶段无 ViewModel、无 View、无 code-behind 改动；`KFL.Presentation` 与 `KFL.App` 一行不动（界面属界面轨 U1） | ✅（N/A 已说明） |
| **IV. 单测优先与结果确定性** | 三项 §16 必测清单（贷款先本后息 + 月划扣 20/40/80 + 12 月计息不滚本金；饥馑 4 阶段流转；俸禄表 18 级锚点 72/420/5100）逐条落地；随机消费次序写成契约（契约「月度结算」§4）以保证「同种子 → 同结果」；测试与实现同批提交 | ✅ |
| **V. 约定式提交与中文提交信息** | 规格书/spec 的回写单独成 `docs` 提交；Core 类型 `feat(core)`、Rules 引擎 `feat(rules)`、测试 `test(rules)` 各起一次 | ✅（流程约束） |
| **VI. 调试通道不复制规则** | 本阶段不建控制台（界面轨 U4）；划扣比例、利率区间等上限只声明在 `KFL.Rules/Config/`，调试通道将来复用同一入口 | ✅ |
| **技术栈与工程约束** | `.slnx` 唯一、六工程不变；无游戏引擎；金额 `decimal`（新增 `Money` 值类型把「贯/文」口径钉死在类型上）；数值全部集中 `KFL.Rules/Config/`；无日志/存档代码 | ✅ |
| **开发工作流与质量门禁** | 严格对位 §16 阶段②，无跨阶段提前动工（界面/存档/随机事件/死亡/科举/罚金/买人口逐条列入 Out of Scope）；三项门禁可执行；复杂度逐项论证（见 Complexity Tracking） | ✅ |
| **需求真源** | 本次 18 项裁决（E-01~E-18）全部**回写规格书**（§3、§5.1、§5.2、§5.4、§10.1、§12.3），并同步修订 `spec.md` 的对应条款；`plan.md`/`research.md`/`data-model.md` 逐条标注规格书章节号 | ✅ |

**无违规项** → `Complexity Tracking` 仅登记两项「新增抽象」的论证（见文末）。

### 本次裁决（2026-10-06，用户逐项裁决；均已回写规格书）

| 编号 | 议题 | 裁决 | 回写位置 |
| --- | --- | --- | --- |
| **E-01** | 米价口径：§5.1 公式写 `米价系数 = 0.4+0.6×米价`，紧接又写「米价系数：初始 1.0、游走 ±10%、clamp 0.7~3.0」 | **状态量 = 米价系数**（初始 1.0、每月 ±10% 游走、clamp 0.7~3.0），**直接作生活费乘数**；米价 = `(系数−0.4)/0.6` 仅作派生展示值（界面轨 U1 用） | 规格书 §5.1 |
| **E-02** | §5.2 收入来源的触发条件（是否需要职业指派、务农与自耕是否互斥） | **自耕/田租只看田地与成人计口数**（每成人 20 亩，无需指派）；**务农仅在家族无田可耕时**给 2 贯/月，与自耕**互斥**；**做工需 ≥1 名成人指派为「做工」**；经商沿用「商本 ≥100 贯且 ≥1 名成人指派经商」 | 规格书 §5.2 |
| **E-03** | §5.2 每个公式都带 `(1+农/200)×(1+工/400)` 一类乘数，但没说用「谁」的天赋 | **按人计**：自耕/做工/经商的乘数用**参与者本人**天赋；**家族级来源（田租）用家主天赋**；家主为 `null` 或非计口成员时该乘数取 `1.0`（无加成） | 规格书 §5.2 |
| **E-04** | §3 的月末结算顺序 | **修订为**：①提升待生效的难度/生活费档位 → ②米价系数游走并 clamp → ③**收入**（含 12 月的储蓄利息与工出身 bonus）→ ④**生活费** → ⑤贷款**先计息、后划扣**。当月净利润 = ③−④ | 规格书 §3、§5.4 |
| **E-05** | 同月既赶上「每满 12 个月计息」又产生划扣时的先后 | **先计息**（按**月初剩余本金**独立 roll 一次并累入欠息），**后划扣** | 规格书 §5.4 |
| **E-06** | 当月现金 + 储蓄付不起生活费时实际扣多少 | **能付多少付多少**：生活费条目金额 = `min(应付生活费, 现金+储蓄)`，现金与储蓄清零；差额不入账、不转贷款（与 §5.4「不存在主动借贷」一致） | 规格书 §5.4 |
| **E-07** | `spec.md` FR-015 尾句「净利润 ≤ 0 时 MUST NOT 改变计息计时」与 §5.4「每 12 个月按当时剩余本金计息一次」冲突 | **以规格书为准**：计息计时按**自然月**推进，与当月净利润是否为负无关；`spec.md` 该子句修正为「MUST NOT 重置或跳过计息计时」 | spec.md FR-015（规格书无需改动） |
| **E-08** | 账本里既要有「进入饥馑」这类**不涉及资金**的条目（US4 AS1），又要满足「条目金额合计 = 资金池变动额」（SC-005） | 条目显式分**资金类**（参与 SC-005 求和）与**事件类**（阶段迁移、贷款计息入欠息）两类；分类是**结构事实**不是平衡数值，故声明在 `KFL.Core`（同 001 R-06 的「实体自不变量值域随实体所在层」） | 规格书 §12.3；spec.md FR-021/SC-005 |
| **E-09** | §5.2 自耕写「月收入 = 自耕亩数 × 0.5 ÷ 12 贯（即 40 亩 = 2 贯/月）」，但 `40 × 0.5 ÷ 12 = 1.667 ≠ 2`（真源自身算不平） | **以公式为准**：自耕 = 亩数 × 0.5 ÷ 12 贯（40 亩 = 1.667 贯/月）；括号里的「2 贯/月」作为笔误修正 | 规格书 §5.2 |
| **E-10** | §5.1「出身修正（农：成人 −10%、未成年 −50%）」的乘区归属，以及 §5.1 四年龄档下「成人」究竟盖哪几档 | 生活费拆成**四个乘区**：`总日耗 → ×米价系数 → ×难度支出系数 → ×[农出身独立乘区] → ×[生活费一般乘区]`。**农出身独立乘区**（仅农出身，乘算）：已成年（青年/成人/老人）`0.90`、未成年（儿童）`0.80`。**生活费一般乘区**：`1 + Σ同区百分比修正`，当前的修正项为「**未成年 −50%**」（**所有出身共有**，非农出身专属表述已从 §5.1 移除）与「**救济期全体支出 −20%**」（§5.4，同为百分比修正故加算，选择 `Relief` 阶段时生效）。例：普通档农出身儿童 = `5 × 30 × 米价 × 难度 × 0.80 × 0.50` | 规格书 §5.1、§5.4、§10.1 |
| **E-11** | 多名成人的农/工天赋不同，40 亩该由谁耕（20/20 与 10/30 的总额不同） | 自耕亩数按 `(1+农/200)×(1+工/400)` **降序**依次填满每成人 20 亩；并列时按年龄降序，再按 `PersonId`（保证确定性） | 规格书 §5.2 |
| **E-12** | §5.2 的「务农 2 贯/月」「做工 1.5 贯/月」是每名成员一份还是家族一份 | **按人头各得一份**：务农 = 每名计口成人（家族无田可耕时）；做工 = 每名被指派「做工」的计口成人。城市宅 +1 贯/月按 **`min(做工人数, 城市宅数)`** 计（一座宅只提供一份加成） | 规格书 §5.2 |
| **E-13** | 规格书 §5.1 与 `spec.md` 均**未规定**开局的当前生活费档位初值（生成 `tasks.md` 时从真源里发现的缺口） | **开局 = 普通档**：新建存档的当前档位即「普通」，该值只作用于「新建存档」这一时点，此后切换仍按次月生效；单点声明在 `KFL.Rules/Config/LivingCostTable.InitialStandard`。`FamilyEconomy` 构造仍**要求显式传入**、不设默认值——`KFL.Infrastructure` 看不到 `KFL.Rules`（G-05），在 `KFL.Core` 抄一份「普通」会造出第二个出处（SC-008） | 规格书 §5.1；spec.md FR-005、Assumptions；data-model §3.6/§4.1 |
| **E-14** | 契约 §8 只写「转入计时 1」「累计满 3 个月」，**没规定 `FamineState.Tick()` 在结算第⑤步（003 插入第③步后顺移；原第④步）的调用时点**——「满 3 月」落在第 3 还是第 4 个饥馑月，差一整月，且现有断言分辨不出 | **第 3 个饥馑月当月转救济**：转入当月记 1，其后每月在⑤的**足额判定之前**先 `Tick()`，再用推进后的计时比较阈值。`X` 月进入饥馑 → `X+2` 月（计 3）当即转救济，`−20%` 自 `X+3` 的应付额起生效；救济同理在第 12 个救济月转 `Severe` | 规格书 §5.4；契约 §8；spec FR-018；data-model §3.5/§6.1/§6.3 |
| **E-15** | 真源 §5.2 做工行把 `×(1+工/400)` 写在整格末尾（对括号内的 `+1 贯/月` 一并生效），但加成是 `min(做工人数, 城市宅数)` 份，**谁得到、乘谁的乘数、落几条条目**无工件规定 | **按人头**：按各被指派者本人的 `(1+工/400)` **降序**取前 `min(做工人数, 城市宅数)` 名，各得 `1 贯/月 × 本人 (1+工/400)`；并列时按年龄降序、再按 `PersonId`（沿用 E-11 的排序口径），逐人各落一条 `CraftingIncome` | 规格书 §5.2；契约 §6；spec FR-008；research R-19 |
| **E-16** | 真源 §5.2 原表与 §17 裁决 2 都写「成年成员」，契约 §6 与 T036 写「计口成人」，spec FR-009 又写「成年成员」——同一工件集自相矛盾；且 §4.3 的「成年」（男满 12/女满 14）与 FR-002 的「成人档」（19~59）**不是同一个集合** | **可指派与收入人力口径 = 计口 ∧ 成年**：成年按 §4.3（男满 12 / 女满 14，生日当月生效）判定，**无年龄上限**（青年与老人均可被指派），未成年不可；「计口」= 在册且未服刑。全工件统一此口径，「成人/计口成人/成年成员」三种措辞指同一集合 | 规格书 §5.2；契约 §3/§6；spec FR-007/FR-008/FR-009/FR-031；research R-19 |
| **E-17** | 真源 §5.2 裁决 2「务农仅在家族**无田可耕**时产生（与自耕互斥）」的**判据未定义**：「无田」= 田亩为 0，还是「自耕未填满每成人 20 亩的容量」 | **判据 = `Holdings.FarmlandMu == 0`**：有田且存在计口成年成员时自耕必然产生（第 21 亩起转田租），故不存在「有田却无田可耕」；「容量未填满」那种读法会让务农与自耕并存，与「互斥」直接冲突 | 规格书 §5.2；契约 §6；spec FR-031；research R-19 |
| **E-18** | 契约 §6 写 `TradeIncome`「归属被采用的那名成员」，而 spec FR-009 与 research R-19#5 写「**家族级**单一份」——与条目二分类里「家族级 = `PersonId == null`」的封闭术语相撞 | **归属被采用的那名成员**（乘数最大者）：「家族级单一份」指**不按人头重复**（商本是同一笔本金），**不是**无归属角色；故 FR-009/R-19#5 的措辞改为「单一份（不按人头重复）」，US5 AS2 与 T055⑤ 的 `PersonId == null` 清单**不含**经商是对的 | 规格书 §5.2；契约 §6；spec FR-009；research R-19；data-model §5 |

> `E-01`~`E-06`、`E-09`~`E-12` 十项若按原样实现都会改变数值，故全部走 §17③ 提问后裁决；
> `E-07` 是工件与真源不一致，按章程「需求真源」条款直接以规格书为准并修工件；
> `E-08` 由 spec 自身两条要求互相牵制推出（非新增数值）。
> `E-10` 中「救济 −20% 进一般乘区」是把用户给定的「相似乘区加算」规则**应用**到 §5.4 的
> −20% 上：二者同为作用于生活费的百分比修正，故按加算处理（`Relief` 期的未成年人 = `1 − 0.5 − 0.2 = 0.3`）。
> 该指定已在 `research.md` R-17 逐字记录；若与预期不符，只需改 `LivingCostTable` 一个乘区列表。
> `E-13` 是**生成 `tasks.md` 时**从真源里发现并当场裁决的缺口——它不是数值变更，而是真源原先
> **没有的一个值**（开局生活费档位）；已回写规格书 §5.1、spec.md FR-005/Assumptions 与
> data-model §3.6/§4.1，取值单点在 `LivingCostTable.InitialStandard`。
> 问完 E-12 后仍残留的**子口径**（收入来源是否互相叠加、经商多名被指派者、田租在家主缺失时）
> 不再逐项打扰用户，改为按 §17①/② 推导并**逐条登记**在 `research.md` R-19，供实现与复核时逐项否决。
> `E-14`~`E-18` 是**动工前对 002 工件做 `speckit-analyze` 时发现、再经用户逐项裁决**的五项：
> 前两条（E-14 饥馑计时时点、E-15 城市宅加成归属与乘数）会**改变阶段阈值或金额**，故按 §17③ 提问后裁决；
> 后三条（E-16 人群口径、E-17 务农判据、E-18 经商归属）是**真源已定、工件漂移**——不含任何新增数值，
> 按 §17① 与章程「需求真源」条款以规格书为准修正工件。五项均已回写规格书 §5.2/§5.4。

## Project Structure

### Documentation (this feature)

```text
specs/002-monthly-settlement-economy/
├── plan.md                          # 本文件
├── spec.md                          # 规格（含 E-01~E-08 的对应修订）
├── research.md                      # 阶段 0：19 项抉择（R-01~R-19，全部有结论）
├── data-model.md                    # 阶段 1：值类型 / 枚举 / 聚合 / 不变量 / 需求映射
├── quickstart.md                    # 阶段 1：可运行的人工验收流程
├── contracts/
│   ├── monthly-settlement.md        # 契约三：结算顺序、随机消费次序、饥馑状态机、贷款规则
│   ├── ledger.md                    # 契约四：流水账条目形状、二分类、两个聚合维度
│   └── config-registry.md           # 契约五：数值 → 规格书章节 → 配置成员（SC-008 的验证物）
└── tasks.md                         # 阶段 2 输出（$speckit-tasks 生成，非本命令产物）
```

> 001 的 `contracts/architecture-guard.md` 与 `contracts/injection-seams.md` 是**跨阶段契约**，
> 本阶段**继承**并做两处最小修订（均在 R-14、R-03 中说明）：G-07 扫描范围新增
> `tests\KFL.Tests\Rules\`；契约二 §2「注入」条款补一句「结算引擎按构造注入 `IGameClock`」。

### Source Code (repository root)

```text
KejuFuShengLu.slnx                   # 不动（仍六工程）
global.json / Directory.Build.props  # 不动

src/
├── KFL.Core/                        # net10.0
│   ├── Entities/
│   │   ├── GameState.cs             # 【改】新增 Economy 属性与构造参数（R-13）
│   │   ├── FamilyEconomy.cs         # 【新】经济聚合：资金流动的唯一入口
│   │   ├── Treasury.cs              # 【新】现金 / 储蓄 / 商本 / 贷款 / 当年储蓄利率
│   │   ├── Holdings.cs              # 【新】田亩 / 农村宅 / 城市宅 / 铺面（只存数量，价格在 Rules）
│   │   ├── Loan.cs                  # 【新】本金 + 欠息 + 距上次计息月数（先本后息是自身不变量）
│   │   ├── Ledger.cs                # 【新】追加式流水账 + 两个维度的聚合查询
│   │   ├── FamineState.cs           # 【新】饥馑阶段 + 已持续月数
│   │   ├── Person.cs / Family.cs    # 不动
│   │   └── ...
│   ├── ValueObjects/
│   │   ├── Money.cs                 # 【新】以「文」为单位的 decimal；无内部舍入
│   │   ├── LedgerEntry.cs           # 【新】年月 / 归属角色（可空）/ 类别 / 金额
│   │   ├── GrainPriceIndex.cs       # 【新】米价系数（>0；clamp 区间属配置，不硬编码于此）
│   │   └── ...
│   ├── Enums/
│   │   ├── LivingStandard.cs        # 【新】拮据 / 普通 / 体面
│   │   ├── AgeBracket.cs            # 【新】儿童 / 青年 / 成人 / 老人
│   │   ├── LedgerCategory.cs        # 【新】§12.3 的类别全集
│   │   ├── LedgerEntryKind.cs       # 【新】资金类 / 事件类（E-08）
│   │   ├── LedgerCreditTarget.cs    # 【新】正额入账目标池：现金 / 储蓄（§5.4）
│   │   ├── FamineStage.cs           # 【新】无 / 饥馑 / 救济 / 第三阶段
│   │   ├── AssetKind.cs             # 【新】田 / 农村宅 / 城市宅 / 铺面
│   │   └── LedgerCategoryMetadata.cs # 【新】类别的结构事实：种类 + 入账目标池
│   └── Config/AttributeLimits.cs    # 不动（实体自不变量值域仍随实体所在层）
├── KFL.Infrastructure/              # net10.0 —— 本阶段只复用既有两组接缝，零改动
│   ├── Abstractions/IRandomService.cs / IGameClock.cs
│   └── Services/SeededRandomService.cs / GameStateClock.cs / GameStateFactory.cs（随 GameState 构造变更而改）
├── KFL.Rules/                       # net10.0
│   ├── Config/
│   │   ├── GameConfig.cs            # 【改】空壳→数值总表（唯一数值真源的总入口）
│   │   ├── LivingCostTable.cs       # 【新】三档 × 四年龄档日耗、出身修正、难度支出系数
│   │   ├── AgeBracketPolicy.cs      # 【新】年龄档归属边界（男 12 / 女 14 / 18 / 60）
│   │   ├── IncomeRateTable.cs       # 【新】自耕/田租/务农/做工/经商/铺面 的系数与门槛
│   │   ├── AssetPriceTable.cs       # 【新】田 1 / 农村宅 10 / 城市宅 100 / 铺面 300 贯，购售同价
│   │   ├── InterestPolicy.cs        # 【新】储蓄与贷款利率区间 0.5%~2.4%、计息周期 12 月
│   │   ├── FamineTimeline.cs        # 【新】饥馑 3 月、救济 12 月、救济折扣 −20%
│   │   ├── GrainPricePolicy.cs      # 【新】初始 1.0、±10% 游走、clamp 0.7~3.0、米价派生值 (系数−0.4)÷0.6
│   │   ├── DifficultyRates.cs       # 【新】§11 四难度的收益/支出/贿赂风险/负面事件系数
│   │   ├── SalaryTable.cs           # 【新】§8.1 十八级年俸 + 月摊 + 士出身 1.05
│   │   └── LoanPolicy.cs            # 【新】划扣比例 20/40/80 与「仕」判定
│   ├── Economy/
│   │   ├── AssetMarket.cs           # 【新】田宅铺买入/售出（购售同价，FR-027）
│   │   └── PaymentPrimitive.cs      # 【新】现金 → 储蓄 → 余额转贷款（FR-017）
│   ├── Settlement/
│   │   ├── MonthlySettlementEngine.cs   # 【新】唯一编排入口（顺序、随机消费次序）
│   │   ├── CountedMembers.cs            # 【新】计口/在册两口径（纯函数，FR-029/FR-030）
│   │   ├── LivingCostCalculator.cs      # 【新】§5.1 生活费与年龄档明细（纯函数）
│   │   ├── IncomeCalculator.cs          # 【新】§5.2 全部收入来源的纯函数
│   │   ├── LoanSettlement.cs            # 【新】计息与划扣（纯函数，作用于 Loan + Money）
│   │   ├── SavingsSettlement.cs         # 【新】1 月 roll 利率、12 月计息（纯函数）
│   │   ├── FamineController.cs          # 【新】四阶段状态机（纯函数）
│   │   └── SettlementResult.cs          # 【新】一次结算的可断言快照（含明细，非第二真源）
│   └── ...
├── KFL.Presentation/                # 不动（界面轨 U1）
└── KFL.App/                         # 不动（界面轨 U1）

tests/
└── KFL.Tests/                       # net10.0
    ├── Architecture/                # 【改】G-07 扫描范围（R-14）
    ├── Core/                        # 【扩】Money / Ledger / Treasury / Loan / FamilyEconomy 不变量
    ├── Infrastructure/              # 【改】GameState 构造变更后的既有测试
    ├── Fixtures/                    # 【扩】经济夹具：多代同堂 + 有田宅铺 + 在职位成员
    └── Rules/                       # 【新】生活费 / 收入 / 贷款 / 储蓄 / 饥馑 / 账本 / 确定性
```

**Structure Decision**: 工程划分与依赖方向**不动**（规格书 §2 六个工程）。新增代码按
**关注点**落在既有目录语义内：`Core/Entities`（状态与自身不变量）、`Core/ValueObjects`、
`Core/Enums`、`Rules/Config`（全部数值）、`Rules/Economy`（资产与支付原语）、
`Rules/Settlement`（月度编排与计算结果）。测试按**证据类型**分目录，新增 `Rules/`
（经济规则断言）——它必须与 `Core`/`Infrastructure`/`Fixtures` 处在 G-07 的扫描范围内，
否则「规则测试无环境依赖」这一半约束对新目录失效（R-14）。

## Complexity Tracking

> 本阶段无章程违规项。下面登记两项**新增抽象**的论证（章程「复杂度 MUST 被论证」）。

| 新增抽象 | 为什么现在需要 | 更简单的替代为何被否 |
| --- | --- | --- |
| `Money` 值类型（`KFL.Core/ValueObjects`） | 规格书同时用「贯」（§5.2/§5.3 表格、俸禄年额）与「文」（§5.1 日耗）表达金额，而章程要求内部一律以**文**计算。本阶段是贯/文混用首次同时出现的地方（生活费按文、收入按贯、俸禄按贯/年折月），口径混淆是**当下已存在**的错误来源 | 裸 `decimal` + 命名约定：`decimal cashGuan` 与 `decimal costWen` 在编译期无法区分，一处漏乘 1000 会让整条时间线的余额与测试期望一起漂移，且没有任何守卫能拦住 |
| `FamilyEconomy` 聚合（`KFL.Core/Entities`） | FR-021 要求「一切资金流动 MUST 落条目」，SC-005 要求「条目合计 = 资金池变动」——这两条只有把**资金变动与追加条目做成同一次调用**才可验证。聚合还让 `GameState` 不再逐个暴露 6 个可写成员 | 把 `Treasury`/`Ledger`/`Holdings` 各自挂在 `GameState` 上：追加条目与改余额成为两次独立调用，任何一处遗漏都不会被类型系统或守卫发现，只能靠人眼评审——正是 SC-005 要防的那类缺陷 |

## 阶段 0 / 阶段 1 小结

`research.md` 的 19 项抉择全部有结论，**无 `NEEDS CLARIFICATION` 残留**。其中三项是本次
读规格书时发现的**真问题**，而非纸上推演：

1. **§3 的结算顺序与「先领收入再付口粮」的直觉相反**（E-04）：`spec.md` 的 PE 与「净利润」
   定义都建立在旧顺序上，本次一并修订，否则「付不起」的判定会与玩家预期相反。
2. **`spec.md` FR-015 与规格书 §5.4 对「计息计时」的表述相反**（E-07）：按章程
   「需求真源」以规格书为准，修工件。
3. **SC-005 与 US4 AS1 互相牵制**（E-08）：「进入饥馑」条目必须存在（不涉及资金），
   而「条目合计 = 资金池变动」要求条目都是资金条目——条目必须显式二分类，否则这条
   SC 无法同时成立。

另有 `E-01`~`E-03`、`E-05`、`E-06` 五项**影响数值**的裁决，全部在提问后由用户定夺，
MUST NOT 由实现自行推断。

## 下一步

`$speckit-tasks` 依据本计划与 [data-model.md](./data-model.md)、[contracts/](./contracts/)
生成依赖有序的 `tasks.md`；随后 `$speckit-implement` 执行。
