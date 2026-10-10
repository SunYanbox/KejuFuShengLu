# 契约八：配置数值登记表（SC-009 的验证物）

**Feature**: `003-opening-assets-officialdom` | **Date**: 2026-10-08

SC-009 要求「本特性涉及的每一项数值都能在 `KFL.Rules/Config/` 中定位到唯一出处，
配置类之外不存在同一数值的第二份副本」。本表是**核对表**：每行给出「数值 → 规格书章节 →
配置成员 → 断言锚点」。数值的唯一出处是 `KFL.Rules/Config/` 的常量；**本表与源码冲突时以源码为准**，
本表不是第二真源。002 的 [契约五](../../002-monthly-settlement-economy/contracts/config-registry.md)
是同一手法的前身；本表**只登记本特性新增的数值**，不复制 002 已登记的项。

「配置成员」列统一写成数值总表入口 `GameConfig` 的路径（`GameConfig` 只做逐成员转发，
本身不含数值字面量，故常量仍是唯一出处）；**`GameConfig` 未转发的区间端点**（`OriginStartTable`
的均值与上下界）按**真实声明处**列出——本表的用途是「能定位到 SC-009 的唯一出处」，
指向不存在的符号会让核对链断在这里。

「断言锚点」列写**真实存在的测试名**：SC-002/003/004/005/007 的**数值**全部由
`tests/KFL.Tests/Rules/SpecAnchorTests.cs` 用字面量锚定（契约八 §3 条款 2 的行级豁免），
**行为**由各测试类覆盖。

## 1. 开局（§10.1、§4.1、§4.2、§5.3）

| 数值 | 配置成员 | 断言锚点 |
| --- | --- | --- |
| 初始现金 **80 / 80 / 500 / 200** 贯（农/工/商/士） | `GameConfig.NewGame.InitialCashGuan(origin)` | `SpecAnchorTests.四出身的初始资产逐格等于规格书`（SC-002） |
| 初始田 **40** 亩（仅农） | `GameConfig.NewGame.InitialFarmlandMu(origin)` | 同上：农 40、其余 0 |
| 初始农村宅 **1** 座（农/工/士） | `GameConfig.NewGame.InitialRuralHouses(origin)` | 同上（农舍与农村宅同价，§5.3） |
| 初始城市宅 **1** 座（仅商） | `GameConfig.NewGame.InitialUrbanHouses(origin)` | 同上：商 1、其余 0 |
| 初始商本 **300** 贯（仅商，**入商本池**） | `GameConfig.NewGame.InitialMerchantCapitalGuan(origin)` | 同上；「可付额不含商本」与「工 bonus 基数不含商本」另有两条反向验证（`NewGameSetupTests.商出身的三池逐格与商本不错池` / `商本不进入工出身bonus的资产基数`） |
| 配偶 **1** 名；孩子 **2 / 1 / 2 / 1** 名（农/工/商/士） | `GameConfig.NewGame.SpouseCount` / `GameConfig.NewGame.ChildCount(origin)` | `SpecAnchorTests.四出身的成员构成与年龄学业体质区间等于规格书`；行为侧 `NewGameSetupTests.成员数与性别`（FR-003） |
| 家主年龄 **28±5**（士 **30±5**）；配偶 **25±5**；孩子 **0~8** | `OriginStartTable.AgeSpread` / `HeadAgeMin`·`HeadAgeMax(origin)` / `SpouseAgeMean` / `ChildAgeMin`·`ChildAgeMax`；取样 `GameConfig.NewGame.HeadAge(origin, random)` / `SpouseAge(random)` / `ChildAge(random)` | `SpecAnchorTests.四出身的成员构成与年龄学业体质区间等于规格书`（区间两端逐格）；行为侧 `NewGameSetupTests.年龄区间` |
| 家主学业 士 **60** / 其余 **10~30** | `OriginStartTable.ScholarHeadStudy` / `CommonerHeadStudyMin` / `CommonerHeadStudyMax`；取样 `GameConfig.NewGame.HeadStudy(origin, random)` | 同上 |
| 家主体质 **80~100**；孩子体质 **90~100**；孩子学业 **0** | `OriginStartTable.HeadHealthMin`·`HeadHealthMax` / `ChildHealthMin`·`ChildHealthMax` / `ChildStudyValue`；取样 `GameConfig.NewGame.HeadHealth(random)` / `ChildHealth(random)` / `ChildStudy` | 同上 |
| 天赋 **N(60, 20)** 四项独立；学业 **N(30, 15)**；体质 **N(85, 10)** | `GameConfig.Attributes.TalentMean`·`TalentSigma` / `StudyMean`·`StudySigma` / `HealthMean`·`HealthSigma`；取样 `NextTalent` / `NextStudy` / `NextHealth` | `SpecAnchorTests.属性分布参数等于规格书`；取样口径 `SpecAnchorTests.标准正态取样恰好消耗两次NextDouble`（R-08） |
| 天命寿数 男 **N(60.7, 8)** / 女 **N(62.3, 8)** | `GameConfig.Attributes.LifespanMean(gender)`·`LifespanSigma(gender)`；取样 `NextLifespan(gender, random)` | `SpecAnchorTests.属性分布参数等于规格书`；**出身不改变该分布**与「无上下限」另见 `SpecAnchorTests.天命寿数分布与出身无关` 与 `NewGameSetupTests.天命寿数只clamp下界不设上限`（FR-006） |
| 士出身的功名带入 **举人 / Initial** | `GameConfig.NewGame.ScholarOriginHasJuRenRecord(origin)` | `NewGameSetupTests.士家主的功名记录与仕身份为假`：士有 1 条记录、其余 0 条 |
| 开局生活费档位 **普通**（沿用 002 单点） | `GameConfig.LivingCost.InitialStandard`（**已登记，不复制**） | 与 002 同一条断言 |
| 开局米价系数 **1.0**（沿用 002 单点） | `GameConfig.GrainPrice.Initial`（**已登记，不复制**） | 与 002 同一条断言 |

## 2. 官吏体系（§8.1、§8.2）

| 数值 | 配置成员 | 断言锚点 |
| --- | --- | --- |
| 待阙 **6~24** 月（整数均匀、含端点） | `GameConfig.Career.AwaitingPostMinMonths` / `AwaitingPostMaxMonths` | `SpecAnchorTests.官吏体系数值等于规格书`（两端点各 1 行）；行为侧 `AppointmentTests.待阙时长的下界恰好是六` / `待阙时长的上界恰好是二十四`（SC-003） |
| 初始官阶 **一甲 L11 / 二甲 L13 / 三甲 L15 / 特奏名 L18** | `GameConfig.Career.InitialRankOf(track)` | `SpecAnchorTests.官吏体系数值等于规格书` 四档逐档、误差为 0（SC-004）；行为侧 `AppointmentTests.待阙期满的当月授官且当月开始领俸` |
| 政绩 **+1/月**、上限 **100** | `GameConfig.Career.MeritPerMonth` / `MeritMaximum` | `SpecAnchorTests.官吏体系数值等于规格书`；行为侧 `CareerAdvanceTests.在任者政绩每月加一` / `政绩封顶于上限` / `非官员待阙者与已致仕者都不增长政绩`（SC-006） |
| 考课周期 **36** 月 | `GameConfig.Career.AppraisalPeriodMonths` | 同上；行为侧 `CareerAdvanceTests.第三十五个在职月不判定而第三十六个在职月判定并重置计时`（SC-005） |
| 基础升级率 **25%**、每点政绩 **+0.3%**、封顶 **70%** | `GameConfig.Career.PromotionBaseChance` / `PromotionChancePerMerit` / `PromotionChanceCap` / `PromotionChance(merit)` | `SpecAnchorTests.官吏体系数值等于规格书` 与 `SpecAnchorTests.考课概率公式逐点等于规格书`（0 点 / 100 点 / 封顶分支）；行为侧 `CareerAdvanceTests.判定成功减一级失败不变而最高品时成功仍维持`（SC-005） |
| 致仕年龄 **70**、半俸 **50%** | `GameConfig.Career.RetirementAge` / `RetirementSalaryRatio` | `SpecAnchorTests.官吏体系数值等于规格书`；行为侧 `RetirementTests` 四条 + 「70 岁无官职不致仕」（SC-007） |
| 俸禄 18 级与月摊、士出身 **×1.05**、难度收益系数 | `GameConfig.Salary.*` / `GameConfig.DifficultyRate.RevenueFactor`（**已登记，不复制**） | `SpecAnchorTests.官吏体系数值等于规格书`（×1.05）；三态下复核锚点 **72 / 420 / 5100** 见 `SalaryModeTests.俸禄锚点在两态下复核`（SC-004、FR-024） |

## 3. 数值唯一性条款

1. 本表数值 MUST 只出现在 `KFL.Rules/Config/` 内的常量声明处；**MUST NOT** 出现在
   `KFL.Core`（实体只存状态与自身值域）、`KFL.Infrastructure`（姓名来源不含任何游戏数值）、
   ViewModel 或测试期望之外的任何位置。
2. 测试 MUST 通过配置成员取期望值，**MUST NOT** 复制字面量——只有**锚点断言**
   （俸禄 72/420/5100 与 `SpecAnchorTests` 钉住 §10.1/§4.1/§8.2 的那些数）允许写字面量，
   因为锚点本身是被验证对象，并 MUST 加行级豁免注释
   （`// arch-guard:allow 理由` 或历史别名 `// config-literal:allow`）。
   **没有锚点断言的行，等于「配置等于自己」**：把 `InitialCashGuan(Merchant)` 从 500 改成 5，
   行为测试不会红——故 §1/§2 的每个数值都 MUST 在 `SpecAnchorTests` 里有对应的字面量断言。
3. **扫描清单必须同步增补**：新增的小数 **0.25**（基础升级率）、**0.003**（每点政绩加成）、
   **60.7** / **62.3**（天命寿数均值）MUST 加入
   `tests/KFL.Tests/Architecture/ConfigLiteralTests.RegisteredValues`。
   `0.70`（封顶）与 `0.50`（半俸）与 002 已登记的同值项**共用同一项**，MUST NOT 重复登记。
   **不增补等于假绿**：清单里没有的数值，`ConfigLiteralRules.Evaluate` 根本不会去比对它。
4. **整数规则值不入清单**（沿用 002 的裁决）：6 / 24 / 11 / 13 / 15 / 18 / 36 / 70 / 100 / 12 / 14 / 60 /
   23~33 / 0~8 / 80~100 / 90~100 / 10~30 与成员编号、年龄、世代号、夹具金额同值，
   入单会产出数百处无关命中。它们的唯一性由**第二条判据**
   （`ConfigLiteralRules.EvaluateMoneyLiterals`：产品代码里的 `Money.FromGuan/Wen(字面量)`）
   与**类注释 + 本表逐行核对**兜底。
5. **新数值 MUST NOT 落在 `KFL.Core`**：`AttributeLimits` 只有 `Min = 0` / `Max = 100`
   （实体自不变量值域），政绩上限 100 属 `OfficialCareerPolicy`；两者同名不同义，
   MUST NOT 让任一处引用另一处（否则会把「属性值域」与「政绩规则」绑成一条依赖）。
6. `GameConfig`（数值总表入口）MUST 只转发既有成员，MUST NOT 声明自己的数值字面量。

## 4. 扫描范围与验证物

| 判据 | 实现 | 本特性的影响 |
| --- | --- | --- |
| 数值清单判据（小数全收 + 4 位及以上官俸 + 实测零撞车项） | `tests/KFL.Tests/Architecture/ConfigLiteralRules.Evaluate` | 清单增补 4 项小数（§3 条款 3）；扫描范围已含 `src/KFL.Core`、`src/KFL.Infrastructure`、`src/KFL.Rules` 与四个测试目录——`SpecAnchorTests`（`Rules\`）在其中，故其锚点行 MUST 带行级豁免 |
| 金额条款判据（`Money.FromGuan/Wen(字面量)`） | 同上 `EvaluateMoneyLiterals` | 新增的开局/生涯代码 MUST 只用配置成员或派生表达式构造金额（**零命中**是验收线） |
| 环境依赖（G-07） | `ArchitectureRules.CheckForbiddenEnvironmentTokens` | 新增的 `src/KFL.Infrastructure/Services/BogusNameGenerator.cs` 在扫描范围内：MUST NOT 出现 `new Random(`（Bogus 内部实现不在此文件的文本里，故不命中） |
| 工程与依赖方向（G-01~G-08） | `ArchitectureRules` | 六工程、依赖边、TFM、`global.json` 全部不变；`KFL.Infrastructure` 新增的 `PackageReference` 不改变 `ProjectReference` 边集 |

> **「数值被测试锚定」的验证物是 `tests/KFL.Tests/Rules/SpecAnchorTests.cs`**：它把 §1/§2 的
> 每个数值用字面量钉在配置成员上。判据不是「有断言」，而是**改坏配置后该文件必须变红**
> （实测：把 `InitialCashGuan(Merchant)` 由 500 改成 5，行为测试 41 项全绿而
> `SpecAnchorTests.四出身的初始资产逐格等于规格书` 失败）。

## 5. 本契约明确不包含

科举（§6）、贿赂与惩罚矩阵（§7）的全部数值（逻辑轨 ⑤/⑥）；婚育、遗传、疾病与寿命判定
（§4.2 新生儿口径、§9，逻辑轨 ⑦）；买人口递增计价（§9.5，逻辑轨 ⑦）；存档与成就（§14，
逻辑轨 ④/⑧）；调试控制台的可变上限（§13，界面轨 U4）；界面文案与官名（规格书未定义，§17 禁止自创）。
