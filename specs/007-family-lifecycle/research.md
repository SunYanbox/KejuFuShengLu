# 阶段 0 研究：家族生命周期（逻辑轨 ⑦）的技术抉择

**Feature**: `007-family-lifecycle` | **Date**: 2026-10-17 | **Spec**: [spec.md](./spec.md)
**Plan**: [plan.md](./plan.md) | **上游真源**: 《科举浮生录规格书》§4.1~§4.4、§5.4、§9.1~§9.5、§12.1、§12.3、§16.1

> 本文件只记录**技术抉择**（怎么落地），不重述需求与数值。一切数值的可检索出处是
> `contracts/config-registry.md` 与规格书；`spec.md` 的六项裁决（Q1~Q6）已定数值，
> 本阶段不再有「要不要调参」的开放问题。

## 采纳状况总览

| 编号 | 议题 | 结论是否已定 |
|---|---|---|
| R-01 | 结算序列九步 → 十步与 ⑧ 步的编排载体 | 已定（静态 Run 类） |
| R-02 | `INameGenerator` 如何进入结算路径 | 已定（构造注入） |
| R-03 | 疾病程度 `s` 的落盘与确定性量化 | 已定（万分之一整数） |
| R-04 | `StatusTimers` 扩项形状 | 已定（尾部追加两项） |
| R-05 | 「宽限月」如何表达 | 已定（子步次序隐式表达） |
| R-06 | 主导天赋与两组乘数的取用 | 已定（最高项 + 固定并列序） |
| R-07 | 患病 / 清除概率的公式落点与 `min(1, …)` 短路 | 已定（概率函数内短路） |
| R-08 | 体质变化的来源隔离（疾病 / 成长 / 饥馑） | 已定（分子步、各扣一次） |
| R-09 | 当月死亡登记表与死因优先级 | 已定（⑧-g 统一执行） |
| R-10 | 死因载体（不落 `Person` 字段） | 已定（事实段 + 事件条目） |
| R-11 | 外嫁归档成员的差异化遍历 | 已定（只叠加寿数判定） |
| R-12 | 夭折窗口如何判定 | 已定（月对齐派生，零新字段） |
| R-13 | 家主继任候选序列实体化 | 已定（查询面与结算共用一函数） |
| R-14 | 同月多人死亡的继任时机 | 已定（死亡集合处理完毕后一次判定） |
| R-15 | 官员压力 `P` 的派生与快照 | 已定（每轮 ⑧-a 现算一次） |
| R-16 | 「每科一次」与「当月未应答即失效」如何不落字段 | 已定（时间派生 + 月末清空） |
| R-17 | 待应答提亲提议的持久形状 | 已定（快照 record，不造幽灵 `Person`） |
| R-18 | 家族级新增持久状态的组织方式 | 已定（新增 `FamilyLifecycleState` 聚合） |
| R-19 | 玩家动作入口与结果形状 | 已定（静态 Entry + `LifecycleOutcome`） |
| R-20 | 查询面全纯函数与拒绝原因枚举 | 已定（仿 `PunishmentQueries`） |
| R-21 | 学业成长与体质写入的取整、clamp 次序 | 已定（decimal 累加后 AwayFromZero） |
| R-22 | 遗传取样实现的落点与随机消费次序 | 已定（复用 `AttributePolicy.NextNormal`） |
| R-23 | 存档 Version 3 → 4 的形状 | 已定（无迁移路径） |
| R-24 | 既有确定性断言的处置 | 已定（绝对值断言改相对/分段） |
| R-25 | ⑧-a 内部两个事件的判定次序 | 已定（先捉婿、后提亲） |

---

## R-01 结算序列九步 → 十步与 ⑧ 步的编排载体

- **Decision**：新增 `src/KFL.Rules/Family/PopulationAdvance.cs`，公开入口
  `public static LifecycleSnapshot Run(GameState state, IRandomService random, INameGenerator names)`；
  `MonthlySettlementEngine.Settle` 在调用 `ExamSeasonRunner.Run` **之前**调用它（插入点即今
  `MonthlySettlementEngine.cs:203` 之前），原 ⑧ 科举季顺延为 ⑨、⑨ 推进时间顺延为 ⑩。
  引擎**公开面保持恰为一个实例方法 `Settle`**，不为新步新增任何公开成员。
- **Rationale**：③ 惩罚推进（`PunishmentAdvance.Run`）、④ 官吏推进（`OfficialCareerAdvance.Run`）、
  ⑧ 科举季（`ExamSeasonRunner.Run`）已是「静态 Run 类 + 引擎只做编排」的既有形状，直接沿用可零新增抽象；
  且 `tests/KFL.Tests/Rules/SettlementEngineTests.cs:313` 断言引擎 public instance 方法名**恰为 `["Settle"]`**，
  把 ⑧ 做成引擎方法会立刻撞红该门禁。`specs/005-exam-cycle/contracts/exam-session.md:21` 与
  `specs/006-bribery-punishment/spec.md:461-463` 都要求逻辑轨 ⑦ 的步落在科举季**之前**，插入点唯一。
- **Alternatives**：(a) 引擎内新增 8 个 private 子步方法——引擎现 312 行会继续膨胀，且
  `SettlementEngineTests.cs:313` 的门禁语义（公开面不越界）将被稀释；(b) 把 `Settle` 拆成两个公开入口
  （经济 / 人口）——直接违反上述断言，且 002 契约 §1 的「恰十步」将无法表达。

## R-02 `INameGenerator` 如何进入结算路径

- **Decision**：`MonthlySettlementEngine` 构造签名由 `(IRandomService, IGameClock)` 扩为
  `(IRandomService, IGameClock, INameGenerator)`；`TurnRunner` 透传（其构造同步扩一参）；
  既有测试构造点同步补一个替身。**不新增注入接缝**。
- **Rationale**：⑧-a 的提亲/捉婿对象与 ⑧-b 的新生儿都在**结算步内部** roll 姓名
  （spec FR-014 / FR-023），而 `INameGenerator` 是 003 已登记的既有接缝
  （`src/KFL.Infrastructure/Abstractions/INameGenerator.cs:19`，`NextSurname()` / `NextGivenName(Gender)`），
  故 `specs/001-core-skeleton/contracts/injection-seams.md:91-99` 的「未定义接缝」清单**不需要**改动，
  也就无需 FR-047③ 的授权动作。
- **Alternatives**：(a) 由 `GameState` 携带——`KFL.Core` 不得持有服务，违反章程 II；(b) 结算步用
  「家族姓氏 + 序号」造名——偏离 §4.2「名经注入的姓名来源」，与 003 开局口径不一致；
  (c) 把姓名 roll 推迟到玩家动作——分娩与提亲对象都产生在结算内，做不到。

## R-03 疾病程度 `s` 的落盘与确定性量化

- **Decision**：`s` 一旦 roll 出即量化为**万分之一整数**（`0 ≤ n ≤ 10000`，`s = n / 10000m`），
  存 `StatusTimers.IllnessSeverityBasisPoints`；全部公式统一用该 decimal 值。
- **Rationale**：`s` 来自 `IRandomService.NextDouble()`（double），直接落 double 会让「同种子逐字段可复现」
  （SC-002 / FR-048）与存档「往返相等」（SC-010）依赖浮点格式化细节；万分之一量化对
  `2 + 8s` 与 `0.05 + 0.45s` 两条线性式的相对误差 ≤ 4×10⁻⁵ 贯，远低于任何断言精度，
  同时让患病概率/扣减可写逐点金色值。
- **Alternatives**：(a) 存 double——存档往返比较需容差，违背 SC-010 的「相等」；(b) 不落 `s`、每月重掷——
  FR-029③ 逐字禁止「逐月重掷 `s`」；(c) 存 0~10 的整档——丢失 FR-029③ 的「`s` 唯一决定两条线性式」。

## R-04 `StatusTimers` 扩项形状

- **Decision**：`src/KFL.Core/ValueObjects/StatusTimers.cs` 由 4 项扩到 6 项：
  `IllnessSeverityBasisPoints`（`int?`）与 `TreatmentRemainingMonths`（`int?`），
  在位置参数 ctor **尾部追加**（默认 `null`），`EnsureNonNegative` 覆盖新增两项。
- **Rationale**：FR-029 明文「疾病状态载体 MUST 复用既有 `StatusFlag.Ill` 位，并把程度 `s` 与
  疗程剩余月数记入 `StatusTimers` 的形状变更项」；`readonly record struct` 尾部追加带默认值的参数
  不破坏既有 4 处省略实参的调用点，`Core/StatusTests.cs:110-127` 的「建立=先状态位后计时 /
  清除=先清计时后状态位」次序断言不受影响。
- **Alternatives**：(a) 给 `Person` 加疾病字段——001 `data-model.md:188-225` 的「无写入通道成员全集」
  与不变量 6 明禁；(b) 新开一个 `PersonIllness` 值对象——本特性只有一个疾病、无复数场景，
  属章程「复杂度 MUST 被论证」的反面。

## R-05 「宽限月」如何表达

- **Decision**：**不落任何字段**。⑧-c 内部固定两段：
  第一段只遍历「进入本子步前已带 `StatusFlag.Ill`」的成员——先按 `2 + 8s` 扣体质（clamp ≥ 0），
  体质归零即登记「病亡」并结束该成员本月处理；否则再掷清除；
  第二段才遍历健康成员掷患病判定，命中即置 `Ill` + roll `s` + 置 `TreatmentRemainingMonths = 0`
  （**不**扣体质、**不**掷清除）。新确诊者当月在 ⑧-d 因带 `Ill` 也不得获得 `+1` 体质。
- **Rationale**：spec FR-029④ 逐字「宽限月由子步次序**隐式**表达，MUST NOT 为它新增 `Person`
  或 `StatusTimers` 字段」；此实现天然满足「确诊当月 0 扣减 / 0 药费 / 0 清除随机消费」三条
  SC-014 断言，并且不需要区分「本月新确诊」与「已患病多时」。
- **Alternatives**：(a) 加 `IllnessOnsetMonth` 字段——明文禁止；(b) 用 `TreatmentRemainingMonths == null`
  编码「新确诊」——把两个正交语义塞进一个字段，且疗程结束后仍患病时语义冲突。

## R-06 主导天赋与两组乘数的取用

- **Decision**：`src/KFL.Rules/Config/IllnessPolicy.cs` 声明 `TalentMultiplierIllness(Origin)` /
  `TalentMultiplierCleanse(Origin)` 两组乘数，以及 `DominantOrigin(TalentSet)`：
  四项天赋取最大者，**并列按 农 → 商 → 仕 → 工** 固定顺序取先者。
- **Rationale**：Assumptions 3c 已由 Q6 裁决确认「主导天赋 = 数值最高者、并列按 农→商→仕→工、
  四项不叠加」；固定顺序而非「按枚举声明序」使并列结果与 `Origin` 枚举的后续扩项解耦。
  MUST NOT 让四项连乘（会使乘数与天赋值脱钩）。
- **Alternatives**：按天赋值线性缩放乘数（`m_p = 1 − 0.20 × t / 100`）——被 Q6 否决，
  且会强制重算 SC-015 的全部金色值。

## R-07 患病 / 清除概率的公式落点与 `min(1, …)` 短路

- **Decision**：`src/KFL.Rules/Family/IllnessAdvance.cs` 两个概率计算（`DrawInfection` 取值 / `Recovers` 的愈判）：
  `IllnessProbability(int health, Origin dominant)` → `[0, 1]`；
  `CleanseProbability(int healthAfterDeduction, Origin dominant, int ageYears, bool underTreatment)` → `[0, 1]`。
  **短路在函数内部无法表达**（函数不接随机源），故由 `PopulationAdvance` 统一按
  「`p >= 1m` ⇒ 直接判愈、不调用 `NextDouble`；否则调用一次」执行，且该判断**只写一次**。
- **Rationale**：SC-014 要求「钳到 1 时该月必愈且 MUST NOT 因此产生额外随机消费」，FR-030 要求
  「先按 `≥ 1` 短路以保证确定性且不消耗随机」；把「概率计算」与「随机判定」分成两个函数，
  概率函数就能被 SC-014 / SC-015 纯数值断言直接调用，无需构造随机源。
- **Alternatives**：让概率函数接受 `IRandomService` 并内部判定——则 SC-015 的存活阈值回归
  被迫构造随机替身，且 `>= 1` 与 `NextDouble() < p` 的边界语义会糊在一起。

## R-08 体质变化的来源隔离

- **Decision**：⑧-c 只处理疾病（`−(2 + 8s)`）；⑧-d 只处理成长（学业增减 + **健康月 `+1`**，
  条件为「当月生活费付清 且 非饥馑 且 未带 `Ill` 且未达上限 100」）；⑧-e 只处理饥馑
  （`−10` 与 5% 死亡判定，救济期跳过扣体质）。三者**同一月各扣一次，互不替代**。
- **Rationale**：spec Edge Cases 逐字「患病与饥馑叠加：体质下降 MUST NOT 重复计算同一原因…
  不得相互替换」；健康月 `+1` 归入 ⑧-d（成长）使「新确诊当月拿不到 `+1`」由子步次序自动成立
  （⑧-c 先于 ⑧-d），无需额外标记。Q6 附带明确「健康月 `+1` 是存活阈值公式成立的前提，MUST NOT 改动」。
- **Alternatives**：合并为单个「健康结算」子步——破坏 FR-007 的固定子步序（⑧-c/⑧-d/⑧-e 分列）。

## R-09 当月死亡登记表与死因优先级

- **Decision**：`PopulationAdvance` 在**当次调用内**维护一张本地登记表
  `List<PendingDeath>`（`PersonId` + `DeathCause`，不落盘、不跨月），⑧-b/⑧-c/⑧-e/⑧-f 只**登记**，
  统一由 ⑧-g 按 `PersonId` 升序执行「置 `Deceased` + 落事件条目」。
  同一成员被多个子步登记时**取最先登记者**（次序即 ⑧-b 分娩 → ⑧-c 病亡 → ⑧-e 饥馑 → ⑧-f 寿终/夭折）。
- **Rationale**：spec Edge Cases 要求「每个死亡者的归档与继任判定 MUST 按 `PersonId` 升序逐一进行」
  且「死亡优先于清除判定」（体质为 0 即病亡，不得出现「已死之人痊愈」）；集中登记使死因有唯一裁决点，
  也避免后续子步对「本月刚死」的人继续掷随机（会在同月引入不可解释的随机消费）。
- **Alternatives**：(a) 各子步就地归档——⑧-e 归档后 ⑧-f 需再判寿数，会产生「双死因」；
  (b) 把登记表存进 `FamilyLifecycleState`——它是月内瞬态，落盘即无意义且要新增 DTO 键。

## R-10 死因载体

- **Decision**：新增 `src/KFL.Core/Enums/DeathCause.cs`（`OldAge` 寿终 / `Illness` 病亡 / `Famine` 饥馑 /
  `Infancy` 夭折 / `Childbirth` 分娩），**只**出现在当月事实段 `LifecycleSnapshot.Deaths` 的条目上；
  **不落 `Person` 字段，也不落账本条目**——`LedgerEntry` 的形状是
  `LedgerEntry(GameDate date, PersonId? personId, LedgerCategory category, Money amount)`
  （`src/KFL.Core/ValueObjects/LedgerEntry.cs:30`，**无 `Note` 字段**），
  故 `MemberDeceased` 事件条目只记「谁在何月去世」，死因由事实段承载。
- **Rationale**：FR-033 与 001 `data-model.md:227` 不变量 6（「出生时辰与『已亡』时间不落字段」）。
  006 的「处死」路径只置状态位、不产生死因，本枚举 MUST NOT 为它补一个取值
  （FR-033 要求与 006 的处死路径**区分**，而非并入）。把死因局限在事实段也保证了
  「同一存档 + 同一动作序列逐字段可复现」（FR-048）不依赖账本的展示文本。
- **Alternatives**：(a) 给 `LedgerEntry` 加 `Note` 字段——上游形状变更波及 004 的
  `LedgerEntryMemberCount = 5`（`MessagePackSaveFormat.cs:74`）与全部五个 `Skip` 点，
  且 001 的账本不变量表要改写，为本特性一个展示需求付代价不成立；
  (b) 把死因拼进 `MemberDeceased` 的分类——会让事件类枚举出现「病亡 / 寿终」这种与来源耦合的取值。

## R-11 外嫁归档成员的差异化遍历

- **Decision**：⑧-f 的遍历集合 = 「`Family.ArchivedMembers` 中带 `MarriedOut` 者」∪ `RegisteredMembers`；
  已带 `Deceased` 者一律跳过。⑧-b/⑧-c/⑧-d/⑧-e 只遍历 `RegisteredMembers`。
- **Rationale**：FR-032 逐字「外嫁归档成员 MUST 继续承载天命寿数判定（唯一被 §4.1 明示不豁免的项），
  但 MUST NOT 重新进入在册/计口集合，其余月度判定（患病、成长、饥馑）MUST NOT 施加于外嫁成员」。
  `ArchivedMembers` 是既有派生集合（`src/KFL.Core/Entities/Family.cs:77-78`），无需新字段。
- **Alternatives**：给外嫁成员保留在册标记——违反 002 契约 §3 的计口口径（外嫁不计口）。

## R-12 夭折窗口如何判定

- **Decision**：判定当且仅当 `AgeAt(date) < 3` **且** `BirthDate.Month == date.Month`
  （即出生当月、满 1 岁生日月、满 2 岁生日月，共 3 次）；命中即登记「夭折」。
- **Rationale**：Q3 裁决「每年掷一次、出生当月即为第一次…共 3 次判定，满 3 岁后 MUST NOT 再判定」；
  按「月对齐」比较即可复用既有 `GameDate` 形态（`BirthDate.Month`），**零新增字段**。
  出生在 ⑧-b 而首次夭折判定在 ⑧-f，同一结算内先后到达，天然满足「两者 MUST 分属不同子步」。
- **Alternatives**：(a) 记录「已判次数」——新增持久字段且无必要；(b) 按日对齐——001 不变量 5
  禁止落裸年龄/日粒度口径。

## R-13 家主继任候选序列实体化

- **Decision**：`src/KFL.Rules/Family/HeadSuccession.cs`：
  `public static IReadOnlyList<Person> Candidates(Family family, GameDate date)` 返回**四档有序**候选序列，
  `public static Person? Select(Family family, GameDate date)` 取首个（空则 `null`）；
  ⑧-h 与 FR-044 的查询面调用**同一函数**。同档内以 `AgeAt` 降序、同岁以 `PersonId` 升序决胜。
- **Rationale**：SC-005 要求四档各至少 1 条断言、且「查询面与结算不漂移」；沿用 001
  `tasks.md:320-323` 的 2026-10-05 裁决「不增设归档回调或事件通知」，继任**只**由月结算步驱动。
- **Alternatives**：在 `Family.EndMarriage` / 归档路径挂回调——被该裁决明文否决。

## R-14 同月多人死亡的继任时机

- **Decision**：⑧-g 先把当月**全部**死亡者按 `PersonId` 升序归档完毕；⑧-h 再对「处理完毕后的」
  在册集合**只做一次**继任判定（不逐人死亡各判一次）。
- **Rationale**：spec Edge Cases 逐字「若家主与其唯一在世儿子同月死亡，则继任 MUST 落在
  『死亡集合处理完毕之后』的候选集上（不得选出已亡者）」。一次判定同时消掉
  「结算结束但 `HeadId` 悬空」的中间态（FR-037 / SC-005）。
- **Alternatives**：每死一人判一次——同一月可能选出一个随即死亡的人，违反上述逐字要求。

## R-15 官员压力 `P` 的派生与快照

- **Decision**：`src/KFL.Rules/Family/OfficialPressure.cs`：
  `public static decimal Compute(IReadOnlyList<Person> registeredOfficials)`，
  `public static decimal WeightOf(int rankLevel)`（`w(L) = 0.02 + 1.48 × (18 − L) / 17`）。
  ⑧-a **开头**取一次快照（`CountedMembers.Registered(family)` 中 `Rank != null` 者）算出 `P`，本轮不重算。
- **Rationale**：FR-019 与 Assumptions 18/19「取判定时快照、MUST NOT 落字段、MUST NOT 跨月缓存」；
  `CountedMembers.Registered`（`src/KFL.Rules/Settlement/CountedMembers.cs:58`）是既有在册口径，
  直接复用可避免第四节「四个口径各写一遍」的老问题。
- **Alternatives**：落 `Family.OfficialPressure` 字段——违反 §12.3「不另存汇总值」。

## R-16 「每科一次」与「当月未应答即失效」如何不落字段

- **Decision**：⑧-a 的两个事件都不引入新持久标记：
  ① 榜下捉婿候选 = 「结算月**上一个月**的 `DegreeHistory` 中存在 `ExamPass` 且位次为 `ImperialPlacement.First`
  的记录」的成员 ⇒ 「每科只判定一次」与「拒绝后名额已消耗」**自动成立**（次月不再是「上月一甲」）；
  ② ⑧-a 的**第一步**即把上一月残留的 `PendingProposal` 置 `null`（当月未应答即失效）。
- **Rationale**：Assumptions 11 / 21 的两条要求都能由「时间派生 + 月末清空」满足，零新字段；
  FR-019 明文「判定时机 = 及第**次月**的 ⑧-a 子步」。
- **Alternatives**：落 `Person` 的「已用于捉婿的科次」→ 新增「无写入通道成员」，须回写 001
  `data-model.md:188-225` 的全集，且与 Assumptions 21 的语义重复。

## R-17 待应答提亲提议的持久形状

- **Decision**：新增 `src/KFL.Core/ValueObjects/MarriageProposal.cs`：
  `MarriageProposalSource`（`RandomProposal` / `ImperialSonInLaw`）与
  `MarriageDirection`（`BrideInto` / `GroomOut`）两个枚举 + `MarriageProposal` record，
  内含**已 roll 的完整候选快照**（姓名、性别、年龄、四项天赋、学业、体质、`Lifespan`、功名、阶层）
  + `Amount` + `ProposedOn` + `Source` + `Direction`。提议**不创建 `Person`**；成婚时按快照构造正式成员。
- **Rationale**：FR-019⑤ / FR-020 要求「玩家接受或拒绝**之前**即可读全部数值」；Key Entities 逐字
  「两种提议共用同一状态」。落盘是 FR-047⑤ 的明文要求。
- **Alternatives**：提议里直接持有一个未入册的 `Person`——会让 `Family.Members` 出现幽灵成员，
  破坏 `RegisteredMembers` / `CountedMembers` 的一切口径。

## R-18 家族级新增持久状态的组织方式

- **Decision**：新增 `src/KFL.Core/Entities/FamilyLifecycleState.cs`，经 `Family.Lifecycle` 属性暴露，
  承载五组状态：① 本年度收养计数 + 所属年份；② 本年度已申请生育的夫妇键集合（`(PersonId, PersonId)`
  归一化排序对）+ 所属年份；③ 待分娩记录列表（夫妇 + 申请年月）；④ 待应答提亲提议（`MarriageProposal?`）；
  ⑤ 本月已托媒成员集合 + 所属年月。
- **Rationale**：`Family.cs` 已 467 行、且其公开成员表写在 001 `data-model.md:252-263`，
  平铺 5 组字段会让该表一次增加 5 行并各自需要复原通道；收进一个聚合类后 `FamilyDto` 只加 **1** 个键、
  序列化只需 **1** 个新 DTO，且「年度重置」逻辑集中在一个类型里（`RollIfNewYear(GameDate)`）。
- **Alternatives**：5 组字段平铺进 `Family`——公开面 +5、复原通道 +5、上游不变量表 +5 行，
  且「年份 + 计数」这类成对状态容易被写歪（只重置一半）。

## R-19 玩家动作入口与结果形状

- **Decision**：`src/KFL.Rules/Family/LifecycleEntry.cs`（静态分部类，逐方法实现散在同名基文件与
  `LifecycleEntry.{Childbirth,Family}.cs` 两分册；仿 `src/KFL.Rules/Bribery/BribeEntry.cs`）：
  `ProposeMatchmaker(GameState, Person, IRandomService, INameGenerator)`、
  `AnswerProposal(GameState, bool accept, IRandomService, INameGenerator)`、
  `MarryOut(GameState, Person, MarriageTier counterpart)`、
  `ApplyForBirth(GameState, Person wife)`、
  `BuyPerson(GameState, IRandomService, INameGenerator)`、
  `StartTreatment(GameState, Person patient)`；
  统一返回 `LifecycleOutcome`（`Accepted` / `RejectReason` / `Amount` / `FromCash` / `FromSavings` / `Proposal`）。
- **Rationale**：006 已确立「静态行动入口 + `Outcome` / `RejectReason` record」的既有形状，
  复用能让 U1 与调试控制台走同一条路径（章程 VI「调试通道不复制规则」）；
  `BribeOutcome` 的「被拒时 `Punishment` 为 null」模式可原样套用于 `LifecycleOutcome.Proposal`。
- **Alternatives**：为每个动作建一个 `I...Service`——章程「复杂度 MUST 被论证」，且 006 已证明静态入口足够。

## R-20 查询面全纯函数与拒绝原因枚举

- **Decision**：`src/KFL.Rules/Family/LifecycleQueries.cs` 全部 `static`，不写状态、不消费随机，
  覆盖 FR-044 的九项；拒绝原因 `LifecycleRejectReason`：
  `None` / `InsufficientFunds` / `NotMarriageable` / `NoLowerDegree` / `FertilityWindowClosed` /
  `AlreadyAppliedThisYear` / `HeadVacant` / `NotIll` / `TreatmentInProgress` / `NoProposal`。
- **Rationale**：`src/KFL.Rules/Punishment/PunishmentQueries.cs:13-14` 的既有注释逐字
  「MUST NOT 改状态、MUST NOT 消费随机」；FR-044 要求「拒绝原因 MUST 复用 006 引入的既有风格
  （如 `InsufficientFunds`）并新增 `NotMarriageable` / `NoLowerDegree` / `FertilityWindowClosed` /
  `AlreadyAppliedThisYear` / `HeadVacant`」，故 `InsufficientFunds` MUST 与 `BribeRejectReason.InsufficientFunds`
  同名同义但**不同枚举**（跨枚举复用会强迫 `KFL.Rules` 的两个子系统互相引用命名空间）。
- **Alternatives**：扩 `BribeRejectReason`——把婚姻拒绝原因塞进行贿枚举，语义漂移。

## R-21 学业成长与体质写入的取整、clamp 次序

- **Decision**：当月学业增量以 **decimal** 累加
  「基础（`1.5` / `2.25`）× 士出身藏书（`×1.1`）× 患病减半（`×0.5`）」，再
  `Math.Round(..., MidpointRounding.AwayFromZero)` 取整后**一次性**写 `Person.Study`；
  未读书者的 `−0.5` 同口径取整。体质写入同理。写入前 MUST 自行 clamp 到 `0~100`。
- **Rationale**：Assumptions 13 要求「先算出当月全部增速（含 1.5/2.25、士出身 ×1.1）再乘 0.5 并四舍五入取整」；
  `Math.Round` 的 AwayFromZero 口径与 `AttributePolicy.Round`（`AttributePolicy.cs:118`）一致。
  **关键陷阱**：`Person.Study` / `Person.Health` 的 setter 会调用
  `AttributeLimits.EnsureInRange`（`Person.cs:153` / `:164`）并抛 `ArgumentOutOfRangeException`，
  **不是静默 clamp**——调用方漏 clamp 会直接崩，故 R-21 把它写成 MUST。
- **Alternatives**：每月保留小数余额（累加器落盘）——新增持久字段且 001 无先例。

## R-22 遗传取样实现的落点与随机消费次序

- **Decision**：`src/KFL.Rules/Family/Inheritance.cs`：
  `public static int Inherit(int a, int b, int floor, IRandomService random)` 与
  `public static int InheritStudy(int a, int b, IRandomService random)`；
  MUST 复用 `AttributePolicy.NextNormal`（`AttributePolicy.cs:66`，Box–Muller，**恰好 2 次 `NextDouble`**）。
  `src/KFL.Rules/Config/InheritancePolicy.cs` 只声明参数（`Divisor = 2`、`SigmaBase = 5`、
  `StudyMeanFactor = 0.5`、`HealthFloor = 20`、上下界 `0/100`）。
  随机消费次序照抄 003 开局口径：年龄 → 天赋 ×4 → 学业 → 体质 → 寿数 → 姓名。
- **Rationale**：FR-005 明文「正态取样 MUST 复用 `AttributePolicy.NextNormal`」；
  `specs/003-opening-assets-officialdom/contracts/new-game-setup.md:59-79` 是唯一口径，
  FR-014 要求「MUST NOT 另立第二种名字口径」。
- **Alternatives**：另写一份 Box–Muller——漂移风险 + SC-002 逐字段复现已依赖该实现。

## R-23 存档 Version 3 → 4 的形状

- **Decision**：`src/KFL.Infrastructure/Save/SaveFileFormat.cs` 的 `Version` 由 3 升至 4；
  `PersonDto` +1 键（`IsExtendedTutor`）、`StatusTimersDto` +2 键、
  `FamilyDto` +1 键（内嵌 `FamilyLifecycleDto`）；成员数常量与 `Skip` 计数同步。
  MUST NOT 做旧格式迁移（沿用 004 裁决 `specs/004-save-system/spec.md:306-307`）。
- **Rationale**：FR-047 逐字列出五组必落盘状态与 Version 变更要求。
- **Alternatives**：只加 DTO 不提 Version——004 的版本守卫会静默放过形状不符的旧档。

## R-24 既有确定性断言的处置

- **Decision**：`tests/KFL.Tests/Rules/DeterminismTests.cs:198-221` 的**绝对值**断言
  （「1 月 2 次、其余 1 次、全年 13 次」）改为「同种子逐月差分相等」的**相对**断言，
  ⑧ 步的金色次数由 007 新增的夹具在 007 自己的测试里断言；`SettlementEngineTests.cs:302-346`
  的负向断言由「九步」口径更新为「十步」并把 `Lifecycle` 加入 `expectedProperties`。
- **Rationale**：spec FR-010 只要求「②~⑦ 步与 ⑨ 科举季的既有随机消费**次数与次序**不变」，
  并**没有**要求月总量不变；而 ⑧ 步必然新增消费（每名在册成员至少 1 次患病判定 + 1 次饥馑判定），
  故原绝对值断言的**前提**（「除考课外每取值一次」）已被规格书自己的裁决改变。
  保留相对断言仍能守住 002 的确定性内核，同时不误报。
- **Alternatives**：把 ⑧ 步做成「零随机」——违背 FR-020 / FR-026 / FR-029 / FR-031 的判定要求。

## R-25 ⑧-a 内部两个事件的判定次序

- **Decision**：⑧-a 内部固定次序为**先榜下捉婿、后随机提亲**；两者共用 `PendingProposal` 槽位，
  故一旦捉婿命中并产出提议，提亲判定**仍照常掷**但命中时**不再覆盖**已有提议（该月至多 1 条提议）。
- **Rationale**：spec 未规定二者先后（FR-019 / FR-020 只各写「至多 1 条」）；取「更稀有、带每科一次窗口
  的捉婿优先」符合 §9.2「事件性质」的措辞，也保证玩家不会因提亲占位而永久错过一甲成员的捉婿机会。
  随机消费则保持**固定次数**（两个判定都掷），避免「是否已有提议」影响随机消费次数——
  这是 FR-008「MUST NOT 出现未声明的随机消费」的必然要求。
- **Alternatives**：(a) 提议已存在则跳过后续判定——随机消费次数随状态变化，破坏可断言性；
  (b) 允许同月 2 条提议——违反 FR-019 / FR-020 的「至多 1 条」。

---

## 未决事项

**无。** spec.md 的六项 §17 裁决（Q1~Q6）已把原开放项全部收口；本阶段 25 项技术抉择全部有结论，
其中需要回写上游工件的部分已登记在 `plan.md` 的「回写义务清单」。
`spec.md` Assumptions「开放项」② 保留的三项（药费档位、饥馑×疾病叠加手感、年龄增益的加算口径）
属**实机观察项**，MUST NOT 在本阶段由实现自行调整（规格书 §17）。
