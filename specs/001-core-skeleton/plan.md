# Implementation Plan: 解决方案骨架与家族领域模型

**Branch**: `001-core-skeleton` | **Date**: 2026-10-05 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-core-skeleton/spec.md`

## Summary

交付规格书 §16 阶段①：一个**能构建、能验证、且被自动化守卫**的解决方案骨架，以及后续
十二个阶段共同读写的家族档案。

技术路线：`KejuFuShengLu.slnx`（六工程，无 `.sln`）+ 根 `global.json` **只锁 .NET 10
版本带**（`10.0.100` + `latestFeature`，不锁补丁号）+ 根 `Directory.Build.props` 打开
warnings-as-errors；`KFL.Core` 承载成员/家族/存档状态与值类型；`KFL.Infrastructure`
交付两组注入接缝（随机来源、游戏时间来源）；`KFL.Rules` 只占位；两个 UI 工程保持可构建
的空壳；`KFL.Tests` 用**可注入输入的纯函数守卫**把依赖方向、目标框架与平台泄漏钉死。

本阶段**不做任何数值**：无生活费、无收入、无贷款、无科举、无存档落盘。规则数值只在
`KFL.Rules/Config/GameConfig.cs` 留一个空壳位置。

## Technical Context

**Language/Version**: C# 14 / .NET 10；仓库根 `global.json` 只约束到 .NET 10 版本带
（`"version": "10.0.100"` + `"rollForward": "latestFeature"`），不锁具体补丁号。实测本机
已装 SDK 10.0.401 且 `Microsoft.WindowsDesktop.App 10.0.12` 可用；同一配置在未来装了
其他 `10.0.x` 的机器或 CI 上照常工作（章程原则 I 已 PATCH 至 v1.1.1）。

**Primary Dependencies**: 本阶段**只**引入测试栈 `xunit` 2.9.x +
`xunit.runner.visualstudio` 3.1.x + `Microsoft.NET.Test.Sdk` 17.14.x。
章程依赖基线中的 CommunityToolkit.Mvvm、MessagePack-CSharp、Bogus、Serilog、
LiveChartsCore **本阶段一个都不装**——001 没有消费者，章程「复杂度 MUST 被论证」同样
适用于依赖（各自阶段带需求引入）。

**Storage**: N/A。存档（MessagePack/JSON/备份）属阶段④；001 只定义字段，不落盘。

**Testing**: xUnit v2 + VSTest，`dotnet test KejuFuShengLu.slnx`（实测在 net10.0 上可用）。
测试分两类：领域/规则测试（无界面、无文件系统、无网络）与架构守卫测试
（读本仓库工程文件与源码，判定逻辑为可注入输入的纯函数）。

**Target Platform**: Windows（WPF 表现层）。非 UI 三层目标 `net10.0` 且不得引用任何
平台类型；UI 两层目标 `net10.0-windows`。

**Project Type**: 桌面应用（WPF + MVVM）+ 分层类库，单仓库多工程。

**Performance Goals**: 本阶段无运行时性能目标——规格书 §3 规定逻辑推进仅由用户输入
驱动，不存在实时循环。可衡量的只有门禁本身：构建零警告零错误、测试全绿。

**Constraints**:
- `TreatWarningsAsErrors`、`Nullable`、`EnforceCodeStyleInBuild`、`AnalysisLevel=latest-Recommended`；
  `TargetFramework` **不**在 `Directory.Build.props` 里给默认值（否则守卫抓不到写错的 TFM）。
- 仓库恰有一个 `.slnx`，递归无 `*.sln`。
- 非 UI 层零系统时钟、零全局随机、零文件系统、零网络（静态断言）。
- 测试工程只额外读取本仓库工程文件与源码（FR-015 的收敛，见 research R-13）。

**Scale/Scope**: 6 个工程；`KFL.Core` **17 个领域类型**（3 实体 + 6 值类型/记录 + 8 枚举）
**+ 1 个值域常量类**（`Config/AttributeLimits.cs`，非领域类型，故不计入 17）；
`KFL.Infrastructure` 2 组接缝 + 3 个实现；架构守卫 8 条断言；0 行数值逻辑；0 个界面功能。

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| 章程条款 | 本计划如何满足 | 结论 |
| --- | --- | --- |
| **I. .NET 10 单一运行时基线** | 非 UI `net10.0`、UI `net10.0-windows`；`global.json` 只锁 .NET 10 版本带（不锁补丁号，禁止 `rollForward: disable`，已随章程 v1.1.1）；不设 `TargetFrameworks`（禁止多目标）；FR-002 | ✅ |
| **II. 分层解耦与单向依赖** | 现有依赖边严格单向且无环；核心三层不引平台类型、CSProj 无 `UseWPF`/`UseWindowsForms`；规则层无 `DateTime.Now`/`Random`/文件/网络；实体只承载数据与自身不变量，跨实体一致性由 `Family` 聚合承担（不承担结算编排）；FR-003、FR-013、FR-014 | ✅ |
| **III. WPF + MVVM 表现层契约** | 本阶段无 ViewModel、无 code-behind 业务：`KFL.App` 只有 `App.xaml`/空 `MainWindow.xaml`，不含任何规则数值；主界面属界面轨 U1。本阶段对原则三**无违反**，只有「尚未开始」 | ✅（N/A 部分已说明） |
| **IV. 单测优先与结果确定性** | 两组接缝全部构造注入，无静态单例；`SeededRandomService` 保证同种子同序列；`GameState` 的唯一标识也走注入，避免隐式 `Guid.NewGuid()`；测试与实现同批提交；FR-013、FR-015、SC-002 | ✅ |
| **V. 约定式提交与中文提交信息** | 全部提交走 `build`/`feat`/`test`/`docs` 类型 + 中文描述；本次规格书修订单独成一次 `docs` 提交，实现另起 `feat`/`test` 提交 | ✅（流程约束） |
| **VI. 调试通道不复制规则** | 本阶段不建控制台（界面轨 U4），因此不存在「上限被复制一遍」的通道；同时先把 `KFL.Rules/Config/GameConfig.cs` 的**位置**建出来，使后续上限有唯一归属 | ✅ |
| **技术栈与工程约束** | `.slnx` 唯一、无 `.sln`；依赖按需引入并说明必要性；无游戏引擎；数值集中（实体值域常量归 `KFL.Core/Config`、规则常量归 `KFL.Rules/Config`，已获所有者确认，见 research R-06）；金额 `decimal` 本阶段无金额故 N/A；Serilog 与存档路径属后续阶段 | ✅ |
| **开发工作流与质量门禁** | 严格对位规格书 §16 阶段①，无跨阶段提前动工；三项门禁可执行；复杂度逐项论证（资产池、系统时钟、存档/成就/事件总线三接口均推迟） | ✅ |

**两项已裁决，均不再挂起**：

- **R-01（SDK 固定粒度）**：只约束到 .NET 10 版本带，不锁具体补丁号，避免未来 CI/CD 因
  缺某个补丁而无法构建。章程原则 I 原文写的是「固定 SDK 版本」，与本裁决有措辞冲突，
  已按治理条款发 **PATCH 至 v1.1.1** 并写明「同一基线」的所指。
- **R-06（常量归属）**：实体自不变量的值域（天赋/学业/体质 0~100）放
  `KFL.Core/Config/AttributeLimits.cs`。因为依赖方向 `Core ← Infrastructure ← Rules`
  决定了 `KFL.Core` **不可能**引用 `KFL.Rules`，把常量搬进 `GameConfig` 只会让实体失去
  自校验。所有者已确认可接受，章程原文的「等常量/配置类」足以覆盖，无需修订。

**无违规项** → `Complexity Tracking` 无需填写（见文末）。

## Project Structure

### Documentation (this feature)

```text
specs/001-core-skeleton/
├── plan.md                          # 本文件
├── spec.md                          # 规格（含 §17 裁决后的修订）
├── research.md                      # 阶段 0：16 项技术抉择（R-01~R-16，含实测证据）
├── data-model.md                    # 阶段 1：字段、不变量、需求映射
├── quickstart.md                    # 阶段 1：人工可复现验收流程
├── contracts/
│   ├── architecture-guard.md        # 契约一：8 条守卫断言 + 守卫自证
│   └── injection-seams.md           # 契约二：两组接缝的行为契约
├── checklists/requirements.md       # 规格质量清单
├── tasks.md                         # 阶段 2 输出（$speckit-tasks 生成，非本命令产物）
└── implementation-notes.md          # 实现期追加（$speckit-implement 产物）：实施与规格的差额、门禁实测
```

### Source Code (repository root)

```text
KejuFuShengLu.slnx                   # 唯一的解决方案文件（六工程，全用 <Project>）
global.json                          # 只锁 .NET 10 版本带（10.0.100 + latestFeature）
Directory.Build.props                # 统一 warnings-as-errors / Nullable / 分析器级别
.gitignore                           # 追加 bin/ obj/ TestResults/

src/
├── KFL.Core/                        # net10.0 —— 实体与值类型，无任何环境依赖
│   ├── KFL.Core.csproj
│   ├── Entities/                    # Person.cs  Family.cs  GameState.cs
│   ├── ValueObjects/                # GameDate.cs  PersonId.cs  TalentSet.cs
│   │                                # DegreeRecord.cs  OfficialRank.cs  StatusTimers.cs
│   ├── Enums/                       # Gender.cs  DegreeLevel.cs  ImperialPlacement.cs
│   │                                # DegreeChangeCause.cs  Occupation.cs  Origin.cs
│   │                                # Difficulty.cs  StatusFlag.cs
│   └── Config/                      # AttributeLimits.cs（实体值域，已确认归 Core）
├── KFL.Infrastructure/              # net10.0 —— 两组注入接缝及其实现
│   ├── KFL.Infrastructure.csproj
│   ├── Abstractions/                # IRandomService.cs  IGameClock.cs
│   └── Services/                    # SeededRandomService.cs  GameStateClock.cs
│                                    # GameStateFactory.cs（16 字节 → UUID）
│                                    #   必须在 Infrastructure：Core 看不到接缝（G-05）
├── KFL.Rules/                       # net10.0 —— 本阶段只有常量位置，无规则实现
│   ├── KFL.Rules.csproj
│   └── Config/                      # GameConfig.cs（空壳，阶段②起填充）
├── KFL.Presentation/                # net10.0-windows, UseWPF=true —— 本阶段无内容
│   └── KFL.Presentation.csproj
└── KFL.App/                         # net10.0-windows, WinExe —— 空壳窗口
    ├── KFL.App.csproj
    ├── App.xaml / App.xaml.cs       # 入口点，StartupUri 指向 MainWindow
    └── MainWindow.xaml / .xaml.cs   # 空窗口（主界面属界面轨 U1）

tests/
└── KFL.Tests/                       # net10.0
    ├── KFL.Tests.csproj
    ├── Architecture/
    │   ├── ArchitectureRules.cs     # 纯函数判定：输入工程/源码文本 → 违规列表
    │   ├── RepositoryLocator.cs     # 从 AppContext.BaseDirectory 向上找 *.slnx
    │   ├── SolutionShapeTests.cs    # G-01 / G-02 / G-08
    │   ├── TargetFrameworkTests.cs  # G-03 / G-04
    │   ├── DependencyDirectionTests.cs  # G-05
    │   ├── PlatformLeakageTests.cs  # G-06
    │   ├── EnvironmentDependencyTests.cs # G-07
    │   └── GuardSelfTests.cs        # 契约一第 4 节：合成违规输入必被捕获
    ├── Core/                        # 字段边界、九状态并存、亲属引用一致性
    ├── Infrastructure/              # 接缝确定性与契约行为
    └── Fixtures/                    # 五类夹具构造器：多代同堂 / 有配偶 / 有子女
                                     #   / 娶入配偶 / 买来的旁系
```

**Structure Decision**: 工程划分与依赖方向**逐字**采用规格书 §2（六个工程、`.slnx` 扁平
列出、前缀 `KFL.`），不做任何增删。工程内部目录是规格书未规定处，按**关注点**划分
（`Entities` / `ValueObjects` / `Enums` / `Config` / `Abstractions` / `Services`），
不按功能切片——本阶段只有一个关注点，过早切分只会制造空目录（章程「复杂度 MUST 被
论证」）。测试目录按**证据类型**划分（架构 / 领域 / 接缝 / 夹具），使「守卫红在哪一类」
一眼可判。

## Complexity Tracking

> 无需填写：Constitution Check 无违规项，`Complexity Tracking` 保留为空。
> 先前挂起的两项已全部裁决——R-01（SDK 只锁 .NET 10 版本带）已随章程 v1.1.1 落地，
> R-06（实体值域常量归 `KFL.Core`）已获所有者确认，均不再需要豁免或后续动作。

## 阶段 0 / 阶段 1 小结

`research.md` 的 16 项抉择全部有结论，无 `NEEDS CLARIFICATION` 残留。其中两项是**实测
发现的真问题**，而不是纸上推演：

1. **`<TestProject>` 被 `.slnx` 解析器静默忽略**（R-02）：照抄规格书 §2 会让 `KFL.Tests`
   悄悄掉出解决方案，「全绿」变成「没跑」。已按 §17 裁决回写规格书 §2。
2. **守卫测试与 FR-015「全部测试无文件系统依赖」自相矛盾**（R-13）：已把 FR-015/SC-002
   收敛为「领域与规则测试无环境依赖；架构守卫可读仓库工程文件」，与章程原则 IV 原文
   （只约束规则层测试）一致。

另有本次用户裁决五项：

- **R-12**（生活费等档）：规格书 §5.1「儿童 0~12」与 §4.3 成年年龄重叠，裁为「成年 = 男满
  12 / 女满 14，未成年一律按儿童档」，已回写规格书 §5.1 与 §4.3。
- **R-01**（SDK 固定粒度）：`global.json` 只锁 .NET 10 版本带，已发章程 PATCH v1.1.1。
- **R-14**（功名变迁历史）：功名改为一串按时间排序的变迁记录（含 §7.4 的降级），当前功名
  由末条派生；已回写规格书 §4.1、§6、§7.4、§12.2 与 spec FR-008。
- **R-15**（辈分与家主）：辈分对血亲出生即定、对外来者由家族指定且须在尚无子女时落定；
  新增 `Family.HeadId`，继任判定留给逻辑轨 ⑦；已回写规格书 §4.4。
- **R-16**（按角色月度收支）：不挂在 `Person` 上，改为 `GameState` 的家族级流水账，属阶段②；
  spec 已列入 Out of Scope。同次回写还包括规格书 §9.5（买人口）与 §12.3（统计口径）。

## 下一步

`$speckit-tasks` 依据本计划与 [data-model.md](./data-model.md)、
[contracts/](./contracts/) 生成依赖有序的 `tasks.md`；随后 `$speckit-implement` 执行。
