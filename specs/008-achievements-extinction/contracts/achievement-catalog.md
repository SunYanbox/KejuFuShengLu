# 契约二十二：成就目录与判定

**特性**：`008-achievements-extinction`（逻辑轨 ⑧）
**上游**：规格书 §14 / §14.1（:555-642）、§17 裁决块「2026-10-18」；`specs/008-achievements-extinction/spec.md`（FR-001~FR-048）
**下游**：本特性的 `achievement-store.md`（契约二十三）、`chronicle-ending.md`（契约二十四）；U1~U5 界面轨
**状态**：活工件，MUST 与实现一致。

> **唯一副本声明**：本文件的 §1（标识三元组）与 §2（判据与阈值）是**代码与测试之外的唯一登记处**。
> 其他文档（含规格书）MUST NOT 复制这两张表；实现 MUST 从 `AchievementCatalog` / `AchievementPolicy` 取值，
> 判据文字与本文件逐条对齐。

---

## §1 标识三元组表（68 行）

**条款 1**：`AchievementId` 的每个成员 MUST 恰对应一行；`Code` 为稳定外部标识（`ACH-001`~`ACH-068`），
一旦发布 MUST NOT 变更或复用。`Id` 的数值 MUST 等于 `Code` 的十进制序号（连续、从 1 起、无洞）。

**条款 2**：系列划分（`AchievementSeries`）与行数 MUST 满足
`Emerging 12 / ExamProgression 8 / OfficialCareer 11 / Lineage 12 / CollectiveRuin 16 / Famine 4 / DebugSecrets 5`，合计 68。

| Code | 枚举成员 | 中文名 | 系列 | 来源族（`AchievementSource`） |
| --- | --- | --- | --- | --- |
| ACH-001 | `FoundationLaid` | 开基立业 | Emerging | Creation |
| ACH-002 | `FirstJuRen` | 首个举人 | Emerging | Degree |
| ACH-003 | `FirstJinShi` | 首个进士 | Emerging | Degree |
| ACH-004 | `GoldenList` | 金榜题名 | Emerging | Degree |
| ACH-005 | `ThreeGenerations` | 三代同堂 | Emerging | Household |
| ACH-006 | `FiveGenerations` | 五世其昌 | Emerging | Household |
| ACH-007 | `TenThousandGuan` | 家资万贯 | Emerging | Asset |
| ACH-008 | `WealthOfTheRegion` | 富甲一方 | Emerging | Asset |
| ACH-009 | `Longevity` | 寿比南山 | Emerging | Lifecycle |
| ACH-010 | `CleanRecord` | 清白传家 | Emerging | Punishment |
| ACH-011 | `PrisonRelease` | 铁窗生涯 | Emerging | Punishment |
| ACH-012 | `NarrowEscape` | 绝处逢生 | Emerging | Cumulative |
| ACH-013 | `JuRenThree` | 杏林初茂 | ExamProgression | Degree |
| ACH-014 | `JuRenTen` | 桂子飘香 | ExamProgression | Degree |
| ACH-015 | `JuRenHundred` | 满门朱紫 | ExamProgression | Degree |
| ACH-016 | `JuRenThousand` | 文运昌隆 | ExamProgression | Degree |
| ACH-017 | `JinShiThree` | 雁塔题名 | ExamProgression | Degree |
| ACH-018 | `JinShiTen` | 簪缨世族 | ExamProgression | Degree |
| ACH-019 | `JinShiHundred` | 朝堂半壁 | ExamProgression | Degree |
| ACH-020 | `JinShiThousand` | 天下文宗 | ExamProgression | Degree |
| ACH-021 | `FirstAppointment` | 释褐登仕 | OfficialCareer | Career |
| ACH-022 | `Prefect` | 牧守一方 | OfficialCareer | Career |
| ACH-023 | `Chancellor` | 位极人臣 | OfficialCareer | Career |
| ACH-024 | `Retirement` | 悬车致仕 | OfficialCareer | Career |
| ACH-025 | `OfficeHolders` | 袍笏登场 | OfficialCareer | Career |
| ACH-026 | `Merit` | 政通人和 | OfficialCareer | Career |
| ACH-027 | `JieShiFailuresThree` | 屡挫屡奋 | OfficialCareer | Exam |
| ACH-028 | `VenerableScholar` | 皓首穷经 | OfficialCareer | Exam |
| ACH-029 | `ImperialSweep` | 鼎甲包揽 | OfficialCareer | Degree |
| ACH-030 | `ExamFailuresTwenty` | 科场遗珠 | OfficialCareer | Exam |
| ACH-031 | `FallenAtShengShi` | 名落孙山 | OfficialCareer | Exam |
| ACH-032 | `LargeHousehold` | 人丁兴旺 | Lineage | Household |
| ACH-033 | `FiftyBirths` | 百子千孙 | Lineage | Cumulative |
| ACH-034 | `ImperialGroomPick` | 榜下捉婿 | Lineage | Lifecycle |
| ACH-035 | `DegreeMarriage` | 秦晋之好 | Lineage | Lifecycle |
| ACH-036 | `DeathInChildbirth` | 红颜薄命 | Lineage | Lifecycle |
| ACH-037 | `InfantDeath` | 襁褓之殇 | Lineage | Lifecycle |
| ACH-038 | `FarmlandHundred` | 阡陌交通 | Lineage | Asset |
| ACH-039 | `FarmlandFiveHundred` | 良田万顷 | Lineage | Asset |
| ACH-040 | `FirstShop` | 旺铺之主 | Lineage | Asset |
| ACH-041 | `TradeLoss` | 商海浮沉 | Lineage | **Pending** |
| ACH-042 | `DebtBurden` | 债台高筑 | Lineage | Asset |
| ACH-043 | `DebtFree` | 无债一身轻 | Lineage | Cumulative |
| ACH-044 | `FamilyExtinct` | 烟消云散 | CollectiveRuin | Ending |
| ACH-045 | `CenturyHouse` | 百年世家 | CollectiveRuin | Cumulative |
| ACH-046 | `MillenniumHouse` | 千秋万代 | CollectiveRuin | Cumulative |
| ACH-047 | `AllFourOrigins` | 四民皆历 | CollectiveRuin | GlobalArchive |
| ACH-048 | `HellEnding` | 大梦一场 | CollectiveRuin | Ending |
| ACH-049 | `FirstSuccessfulBribe` | 投石问路 | CollectiveRuin | Punishment |
| ACH-050 | `BribeThree` | 暗度陈仓 | CollectiveRuin | Cumulative |
| ACH-051 | `BribeTen` | 权钱交易 | CollectiveRuin | Cumulative |
| ACH-052 | `BribeHundred` | 只手遮天 | CollectiveRuin | Cumulative |
| ACH-053 | `BribeThousand` | 买断功名 | CollectiveRuin | Cumulative |
| ACH-054 | `BribeCapReached` | 无法无天 | CollectiveRuin | Punishment |
| ACH-055 | `ExecutedForBribe` | 身首异处 | CollectiveRuin | Punishment |
| ACH-056 | `CollectivePunishment` | 覆巢之下 | CollectiveRuin | Punishment |
| ACH-057 | `MarriedOutPunished` | 殃及池鱼 | CollectiveRuin | Punishment |
| ACH-058 | `FamilyWideBan` | 斯文扫地 | CollectiveRuin | Punishment |
| ACH-059 | `ThreePrisoners` | 囹圄之灾 | CollectiveRuin | Punishment |
| ACH-060 | `FirstRelief` | 嗟来之食 | Famine | Famine |
| ACH-061 | `ReliefRecoveredPaidInFull` | 枯木逢春 | Famine | Famine |
| ACH-062 | `ReliefRecoveredShortfall` | 沉疴难起 | Famine | Famine |
| ACH-063 | `FamineDeath` | 路有冻骨 | Famine | Famine |
| ACH-064 | `DebugWakeOne` | 天机泄露 | DebugSecrets | **Pending** |
| ACH-065 | `DebugWakeTen` | 窥探虚实 | DebugSecrets | **Pending** |
| ACH-066 | `DebugWakeHundred` | 篡改命数 | DebugSecrets | **Pending** |
| ACH-067 | `DebugWakeThousand` | 虚空主宰 | DebugSecrets | **Pending** |
| ACH-068 | `DebugWakeTenThousand` | 代码飞升 | DebugSecrets | **Pending** |

---

## §2 判据与阈值表（68 行）

**条款 3**：每条判据 MUST 由 `AchievementPredicates.IsSatisfied` 分派到**一个**纯谓词实现；
谓词 MUST NOT 写状态、MUST NOT 消费随机、MUST NOT 访问时钟/文件/网络（G-07 扫描 `src\KFL.Rules\`）。

**条款 4**：表中的每个阈值 MUST 是 `src/KFL.Rules/Config/AchievementPolicy.cs` 的常量，
并由 `GameConfig.Achievements` 转发。判据代码与 `AchievementCatalog` 里 MUST NOT 出现裸数值字面量（SC-008）。

**条款 5**：判据里的「成员」口径 —— 除判据逐字写「**在册**」者外，一律指 `Family.Members` 的**全量**成员
（含已亡与外嫁归档者）。「当月报告」指 `SettlementResult` 的对应子段。

| Code | 判据（逐字） | 阈值常量 |
| --- | --- | --- |
| ACH-001 | 新局创建成功（判定器在创建后触发一次） | — |
| ACH-002 | 存在成员带 `Cause == ExamPass ∧ Level >= JuRen` 的功名记录 | `DegreeLevel.JuRen` |
| ACH-003 | 存在成员带 `Cause == ExamPass ∧ Level == JinShi` 的功名记录 | `DegreeLevel.JinShi` |
| ACH-004 | 存在成员带 `Placement ∈ {ZhuangYuan, BangYan, TanHua}` 的进士记录 | 三个 `ImperialPlacement` |
| ACH-005 | **在册**成员中存在父–子–孙三代同时存活（经 `Person.FatherId` 链；= `Generation` 0/1/2 各有在册存活者） | — |
| ACH-006 | 存在**在册**成员 `Generation >= 4` | `FiveGenerationsMinGeneration = 4` |
| ACH-007 | `TotalAssetsCalculator.Of` ≥ 10,000 贯 | `RichAssetsGuan = 10000` |
| ACH-008 | `TotalAssetsCalculator.Of` ≥ 50,000 贯 | `WealthyAssetsGuan = 50000` |
| ACH-009 | 存在成员 `AgeAt(当前月) >= 80` | `LongevityAge = 80` |
| ACH-010 | 存在成员 `PunishmentQueries.WasBribeFreeAtImperialPass(person)` | — |
| ACH-011 | 当月报告 `Punishment.Releases` 非空 | — |
| ACH-012 | `Difficulty == Hell ∧ 存续年数 >= 100` | `NarrowEscapeSurvivedYears = 100`（`>=`） |
| ACH-013 | 家族累计举人人数 ≥ 3 | `JuRenThreeThreshold = 3` |
| ACH-014 | ≥ 10 | `JuRenTenThreshold = 10` |
| ACH-015 | ≥ 100 | `JuRenHundredThreshold = 100` |
| ACH-016 | ≥ 1000 | `JuRenThousandThreshold = 1000` |
| ACH-017 | 家族累计进士人数 ≥ 3 | `JinShiThreeThreshold = 3` |
| ACH-018 | ≥ 10 | `JinShiTenThreshold = 10` |
| ACH-019 | ≥ 100 | `JinShiHundredThreshold = 100` |
| ACH-020 | ≥ 1000 | `JinShiThousandThreshold = 1000` |
| ACH-021 | 当月报告 `Career.Appointments` 非空 | — |
| ACH-022 | 当月晋升中出现官阶 ∈ [5, 8] | `PrefectRankMin = 5` / `PrefectRankMax = 8` |
| ACH-023 | 存在成员 `Rank == 1` | `OfficialRank` 的 1 级（不写字面量，引 `OfficialRank` 成员） |
| ACH-024 | 当月报告出现致仕（`Career.Retirements` 非空） | — |
| ACH-025 | 当月**在册在职**人数 ≥ 5 | `OfficeHoldersCount = 5` |
| ACH-026 | 存在成员 `Merit >= 100` | `MeritThreshold = 100` |
| ACH-027 | 存在成员 `JieShiFailureCount >= 3` | `JieShiFailuresThree = 3`（免解阈值，引 `ExamPolicy`） |
| ACH-028 | 存在成员 `年龄 >= 50 ∧ ShengShiFailureCount >= 6` 且已获特奏名授官 | `VenerableStudyAge = 50` / `VenerableShengShiFailures = 6` |
| ACH-029 | 族谱历史中 `ZhuangYuan` / `BangYan` / `TanHua` 三种名次均出现过 | 三个 `ImperialPlacement` |
| ACH-030 | Σ 成员（解试失败数 + 省试失败数）≥ 20 | `ExamFailuresPileUp = 20` |
| ACH-031 | 当月存在成员 `Level == ShengShi ∧ Passed == false` 且其 `Study >= 80` | `GeniusStudy = 80` |
| ACH-032 | `Family.RegisteredMembers.Count >= 20` | `LargeHouseholdMembers = 20` |
| ACH-033 | 累计出生（含夭折）≥ 50 | `ClanBirthCount = 50`；读 ② |
| ACH-034 | 历史上出现过 `Kind == ImperialProposal` 的提议（**含被拒**） | — |
| ACH-035 | 当月 `MarriageFact.IsDegreeMarriage == true` | — |
| ACH-036 | 当月死亡事实 `Cause == Childbirth` 且亡者为女性 | — |
| ACH-037 | 当月死亡事实的亡者 `AgeAt(死亡月) < 3` | `InfantDeathMaxAge = 3`（`<`） |
| ACH-038 | `Holdings.FarmlandMu >= 100` | `FarmlandHundredMu = 100` |
| ACH-039 | `Holdings.FarmlandMu >= 500` | `FarmlandFiveHundredMu = 500` |
| ACH-040 | `Holdings.Shops >= 1` | — |
| ACH-041 | 经商亏损事件（损失本金 **10~30%**）—— **待接线**，本次恒 false | `TradeLossMinPercent = 10` / `TradeLossMaxPercent = 30` |
| ACH-042 | 历史任一时点贷款本金 > 100 贯 | `DebtBurdenGuan = 100`（严格 `>`） |
| ACH-043 | 曾产生贷款 ∧ 其后 `Loan.IsSettled` 为真 | 读 ⑤ + `Loan.IsSettled` |
| ACH-044 | 绝嗣结局触发 | 由 `ExtinctionJudge.TryTrigger` 补判 |
| ACH-045 | 未绝嗣 ∧ **存续年数 > 100** | `CenturyFamilySurvivedYears = 100`（严格 `>`） |
| ACH-046 | **存续年数 > 300** | `MillenniumFamilySurvivedYears = 300`（严格 `>`） |
| ACH-047 | 全局「出身达成」段中农/工/商/士四者齐备 | `Origin` 四个成员 |
| ACH-048 | `Difficulty == Hell ∧` 绝嗣触发 | 由 `ExtinctionJudge.TryTrigger` 补判 |
| ACH-049 | 存在 `Caught == false` 的行贿事实（首次**成功**） | — |
| ACH-050 | 累计**成功**行贿 ≥ 3 | `BribeThreeThreshold = 3`；读 ① |
| ACH-051 | ≥ 10 | `BribeTenThreshold = 10` |
| ACH-052 | ≥ 100 | `BribeHundredThreshold = 100` |
| ACH-053 | ≥ 1000 | `BribeThousandThreshold = 1000` |
| ACH-054 | 存在 `CapReached == true` 的行贿事实 | — |
| ACH-055 | 当月存在 `Caught == true` 的**殿试**行贿事实，且该「难度 × 殿试」惩罚格为立即执行 | `PunishmentPolicy.CellOf(difficulty, ExamLevel.DianShi).Execution == ExecutionKind.ImmediateSentence` |
| ACH-056 | 当月 `CollectivePunishments` 非空，且后果为处死或降级 | `CollectiveOutcome.Executed` / `.DegreeDemoted` |
| ACH-057 | 当月连坐事实的亲属**已外嫁归档** | `StatusFlag.MarriedOut` |
| ACH-058 | 当月 `Punishment.FamilyBanDeclared == true` | — |
| ACH-059 | 当月 `Punishment.PrisonerCount >= 3` | `PrisonerCount = 3` |
| ACH-060 | 首次进入救济阶段（`FamineStage.Relief`） | `FamineStage.Relief` |
| ACH-061 | 上一月处于救济阶段 ∧ 本月已脱离 ∧ 本月实付生活费 **=** 应付额 | `FamineBefore` / `LivingCostPaid` / `LivingCostPayable` |
| ACH-062 | 上一月处于救济阶段 ∧ 本月已脱离 ∧ 本月实付 **<** 应付 | 同上 |
| ACH-063 | 当月死亡事实 `Cause == Famine` | `DeathCause.Famine` |
| ACH-064 | 唤醒调试控制台累计 ≥ 1 | `DebugWakeOne = 1`；读 ⑥ |
| ACH-065 | ≥ 10 | `DebugWakeTen = 10` |
| ACH-066 | ≥ 100 | `DebugWakeHundred = 100` |
| ACH-067 | ≥ 1000 | `DebugWakeThousand = 1000` |
| ACH-068 | ≥ 10000 | `DebugWakeTenThousand = 10000` |

**条款 6（两条特例的时点口径，MUST 有专测）**：

- **ACH-034** 读「当月 `LifecycleSnapshot.PendingProposal?.Kind`」∪「状态面当前待应答提议的 `Kind`」。
  提议由 ⑧-a 产出，产出月的月末判定在同一 `Advance` 内完成，玩家动作在其后 ⇒「含被拒」天然成立。
  **不**为它设即时触发器、**不**为它加持久字段（理由见 `research.md` R-04）。
- **ACH-037** 是全特性**唯一**使用「事实月份」而非「当前月」的年龄判据
  （`AgeAt(死亡月) < 3`）。其余年龄判据一律取 `state.CurrentDate`（R-06）。

**条款 7（累计量的读向）**：ACH-012/033/045/046/050~053/064~068 读 `AchievementState` 的累计量；
判据 MUST NOT 自行扫描历史事实来「重算」这些量（那会形成第二个真源，且 ③ 的三均值不可重算）。

---

## §3 动作型即时判定入口清单（12 项）

**条款 8**：下表是 FR-009 清单的**唯一副本**。新增玩家动作入口时 MUST 同步本表与 `AchievementTrigger`；
清单变更属跨特性形状变更，按 AGENTS.md 铁律在**同一提交**内回写本文件与受影响契约。

**条款 9**：调用位置 MUST 是「动作执行**成功之后**、入口返回之前」。动作被拒（校验失败、金额不足、
窗口未开等）的路径 MUST NOT 调用判定器。

**条款 10**：即时判定 MUST NOT 落全局文件、MUST NOT 写档、MUST NOT 抛
（唯一全局落盘点是回合末 ⑤，见契约二十三 §5）。

| # | `AchievementTrigger` | 数值 | 调用点（既有面） | 直接受益的成就 |
| --- | --- | --- | --- | --- |
| 1 | `NewGameCreated` | 1 | `src/KFL.Rules/Start/NewGameSetup.cs:99` `Create` 成功后 | ACH-001 |
| 2 | `AssetPurchased` | 2 | 003 资产买入入口成功后 | ACH-038~040，ACH-007/008 |
| 3 | `AssetSold` | 3 | 003 资产卖出入口成功后 | 同上（卖出亦可抬高现金） |
| 4 | `LoanOriginated` | 4 | 002 借款发生入口成功后 | ACH-042、ACH-043 |
| 5 | `LoanRepaid` | 5 | 002 提前清偿入口成功后 | ACH-043 |
| 6 | `MatchmakingCommenced` | 6 | 007 托媒入口成功后 | ACH-035（若当月成交） |
| 7 | `ProposalAnswered` | 7 | 007 提亲应答入口（接受或拒绝）成功后 | ACH-035 |
| 8 | `DegreeMarriagePurchased` | 8 | 007 买功名婚姻入口成功后 | ACH-035 |
| 9 | `PersonPurchased` | 9 | 007 买人口入口成功后 | ACH-032 |
| 10 | `BribeSubmitted` | 10 | 006 行贿提交入口成功后 | ACH-049~054 |
| 11 | `ExamInquiryAnswered` | 11 | 005 科举询问应答入口成功后 | ACH-027、ACH-028 |
| 12 | `DebugWakeReported` | 12 | FR-041 ⑥ 的上报接缝（U4 未来调用） | ACH-064~068 |

**条款 11（为什么是 12 而不是 13）**：「捉婿提议产出」**不**是触发器。它由 `MonthlySettlementEngine`
的 ⑧-a 产生，而 FR-035 禁止结算器反向调用判定器。该事实由当月的 `LifecycleSnapshot.PendingProposal`
承载，回合末判定（②）读取即可（§2 条款 6）。这条说明 MUST 保留，否则后续读者会以为漏了一个入口。

---

## §4 待接线接缝

**条款 12**：`AchievementSource.Pending` 的 6 条（ACH-041、ACH-064~068）MUST 在本特性交付时
**恒不解锁**，且 MUST 各有一条测试断言「未接线 ⇒ 不解锁（无假数据）」（FR-042）。

| 接缝 | 形状 | 真实上报方 |
| --- | --- | --- |
| ACH-041 商海浮沉 | 谓词 `IsTradeLoss(ctx)` 读「经商亏损事件」事实；本次无产生者 ⇒ 恒 false | 规格书日后补「经商亏损事件」时接（§17 裁决块已登记该缺口） |
| ACH-064~068 调试秘辛 | `AchievementTrigger.DebugWakeReported` + `AchievementCounters.ReportsDebugWake(state, count)` + `AchievementAdjudicator.Report(state, DebugWakeReported)` | U4 调试控制台（规格书 §13）；本次只提供入口并使 ⑥ 可被单测直接推进 |
| ACH-047 四民皆历 | `IAchievementStore.Read()` 返回的 `AchievementArchive.Origins` 由 `TurnRunner` 传给 `Judge` | 本特性已接（读面 + 判据齐备），仅界面轨未展示 |

**条款 13**：`ReportsDebugWake` MUST 校验 `count >= 1`（抛 `ArgumentOutOfRangeException`），
并把 ⑥ 的累加与「ACH-064~068 的即时判定」放在同一次调用里，使 U4 只需一个调用点。

---

## §5 测试映射（FR-048）

**条款 14**：必测清单第 9 条（「绝嗣判定（外嫁不计）」）的落点 MUST 是
`tests/KFL.Tests/Rules/ExtinctionTests.cs`，且 MUST 至少覆盖四条：零在册成立 / 只剩已亡不成立 /
只剩外嫁归档不成立 / 在册 ≥ 1 不成立。

| 测试文件 | 覆盖 |
| --- | --- |
| `tests/KFL.Tests/Rules/AchievementCatalogTests.cs` | 68 行齐全、`Id` 与 `Code` 连续无洞、系列计数、枚举与目录双向一一对应 |
| `tests/KFL.Tests/Rules/AchievementJudgmentTests.cs` | 68 条 ×（触发 / 不触发）参数化；含 6 条 `Pending` 的「恒不解锁」 |
| `tests/KFL.Tests/Rules/AchievementCounterTests.cs` | ①②③④⑤⑥ 的更新时点、增量语义、幂等；含 ACH-034/042 的时点特例 |
| `tests/KFL.Tests/Rules/AchievementTriggerTests.cs` | 12 个入口各触发一次；拒绝路径不触发（条款 9） |
| `tests/KFL.Tests/Rules/TukeyTrimeanTests.cs` | 金色算例 8 行 + 判别例 `[10,20,30,40,100] → 30.000` + n=0 |
| `tests/KFL.Tests/Rules/ChronicleTests.cs` | 七项口径；`SurvivedYears == EndYear − 1`；年 102 首次成立百年世家 |
| `tests/KFL.Tests/Rules/ExtinctionTests.cs` | 四条判据面 + `End` 幂等 + ACH-044/048 的补判 |
| `tests/KFL.Tests/Rules/AchievementDeterminismTests.cs` | SC-003 同种子同输入逐字段一致 |
| `tests/KFL.Tests/Infrastructure/AchievementStoreTests.cs` | 原子性、最早日期合并、来源 UUID、损坏隔离、目录不可写（SC-004~007） |
| `tests/KFL.Tests/Infrastructure/AchievementArchiveJsonTests.cs` | 未知键忽略 / 缺失取默认（FR-019）、`Version` 语义 |
| `tests/KFL.Tests/Infrastructure/AchievementSaveRoundTripTests.cs` | SC-011 存档往返（含 `Completed`/`PendingUnlocks` 次序）；`SaveStateComparer` 扩展 |
| `tests/KFL.Tests/Infrastructure/EndingRefusalTests.cs` | SC-010 三面（异常类型、状态零变更、目录清单不变） |
| `tests/KFL.Tests/Architecture/ConfigLiteralTests.cs`（既有） | SC-008 自动覆盖 008 的新阈值落点 |
| `tests/KFL.Tests/Architecture/EnvironmentDependencyTests.cs`（既有） | G-07 自动覆盖判定器 |

---

## §6 与既有契约的关系

| 既有契约 | 关系 |
| --- | --- |
| 契约六 §1（新建存档） | 008 在其 `Create` 成功路径后**追加**一次 `Report(NewGameCreated)`；起始年月校验（`:106-110`）与随机消费次序 MUST NOT 变更 |
| 契约十（存档本体，004） | 008 变更其键表、版本闸门与校验条数；回写见 `achievement-store.md` §6 |
| 契约十一 §2/§3（存档节奏，004） | 008 在 `Advance` 的 ①~④ 之后追加 ⑤；①②③④ 的次序与异常策略 MUST NOT 变更 |
| 契约十六 §8 / §9（贿赂与惩罚，006） | 008 **消费** `PunishmentSnapshot` 与 `PunishmentQueries`，**不修改**其形状；`FamilyCumulativeBribes` 的「尝试次数」语义澄清见 `achievement-store.md` 后记 |
| 契约十九~二十一（007） | 008 **消费** `LifecycleSnapshot`、（无）`CareerAdvanceResult`、`ExamSeasonSnapshot`、`FamineState`；**不修改** 007 的步骤表 |
| 契约一 §2.1（禁用 token，001） | 12 个 token 清单 + 扫描范围 MUST NOT 变更；判定器 MUST 全部遵守 |

**后记**：本契约自「契约二十二」起编号（FR-037）；007 的四个契约依次为契约十八~二十一
（`lifecycle` / `marriage-birth` / `illness-lifespan` / `config-registry`）。
008 交付后，逻辑轨 ①~⑧ 全部落地；规格书 §16.1 剩余的 ⑨ 与界面轨 U1~U5 不在本契约范围。
