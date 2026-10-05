# 《科举浮生录》项目章程

## Core Principles

### I. .NET 10 单一运行时基线（NON-NEGOTIABLE）

全解决方案 MUST 统一以 .NET 10 为唯一运行时基线：游戏核心库（`KFL.Core`、
`KFL.Infrastructure`、`KFL.Rules`、`KFL.Presentation`）目标框架为 `net10.0`，WPF
应用与视图层项目（`KFL.App`）为 `net10.0-windows`。

- 任何项目 MUST NOT 降级到更早的 .NET 版本，也 MUST NOT 多目标（multi-target）
  到旧框架；确需多目标的场景必须先修订本章程。
- 语言特性 MUST 使用 .NET 10 / C# 14 的当前能力，禁止为兼容旧运行时而写退化代码。
- 引入的 NuGet 依赖 MUST 支持 `net10.0`（或 `net10.0-windows`）；不受支持或已停止
  维护的包 MUST NOT 引入。
- SDK 版本 MUST 通过仓库根的 `global.json` 固定，保证本机与 CI 使用同一基线。

理由：统一基线是"底层与 UI 层分离"能够被验证的前提。一旦允许框架版本漂移，
`KFL.Rules` 就会被无意间拖入 Windows 专属类型，解耦随之失效。

### II. 分层解耦与单向依赖（NON-NEGOTIABLE）

依赖方向 MUST 严格单向：`KFL.Core` ← `KFL.Infrastructure` ← `KFL.Rules` ←
`KFL.Presentation` / `KFL.App`。箭头表示"被依赖"，下层 MUST NOT 知晓上层。

- `KFL.Rules` MUST NOT 引用任何 WPF / Windows 专属类型（`System.Windows.*`、
  `System.Drawing`、`PresentationCore` 等），其项目文件 MUST NOT 使用
  `UseWPF`、`UseWindowsForms`。
- 下层 MUST NOT 反向依赖上层；跨层协作 MUST 通过定义在下层的接口（如
  `ISaveService`、`IAchievementStore`、`IRandomService`、`IEventBus`）完成，
  由上层注入实现。禁止循环依赖。
- 规则逻辑 MUST 表达为纯函数，或仅依赖可注入的服务接口；`KFL.Rules` 内
  MUST NOT 出现 `DateTime.Now`、`Random`、文件系统、网络等隐式环境依赖，
  时间与随机性 MUST 由参数或接口传入。
- 领域实体（`Person`、`Family`、`GameState` 等）MUST 只承载数据与自身不变量，
  MUST NOT 承担跨实体的结算编排职责。

理由：把规则引擎与 UI 彻底隔离，规则才能脱离界面被单测、被重放、被替换表现层。
耦合一旦渗入，修复成本随功能数量指数上升。

### III. WPF + MVVM 表现层契约

表现层 MUST 采用 WPF + MVVM，并使用 CommunityToolkit.Mvvm 的源生成器
（`[ObservableProperty]`、`[RelayCommand]`）实现绑定与命令。

- View 只负责布局、样式与绑定；业务逻辑 MUST NOT 出现在 code-behind 中，
  code-behind 仅限于纯粹的视图管线代码（如焦点、纯视觉动画）。
- ViewModel MUST NOT 直接引用 View 类型或具体控件类型，MUST NOT 持有
  `Window`、`Control` 等实例；交互 MUST 通过命令、绑定、消息或接口抽象完成。
- ViewModel 的依赖 MUST 通过构造函数注入，MUST NOT 直接 new 出仓储、存档服务
  或规则引擎。
- 逻辑推进 MUST 仅由用户输入驱动；`DispatcherTimer` 只允许用于 UI 动画，
  MUST NOT 用于实时游戏循环。
- 表现层不得复制规则：任何数值计算 MUST 委托给 `KFL.Rules`。

理由：MVVM 的价值在于 ViewModel 可脱离 UI 被测试与替换。只要 View 与
ViewModel 之间保持单向、无控件类型的绑定关系，界面重构就不必触碰业务代码。

### IV. 单元测试优先与结果确定性（NON-NEGOTIABLE）

单元测试是本项目的核心交付物之一，不是可选项。

- 每一处规则逻辑 MUST 有对应的单元测试；新增或修改规则时，测试 MUST 与实现
  在同一提交中给出（测试先行或至少同批），MUST NOT 留待"以后补测"。
- 所有涉及随机性的逻辑 MUST 通过可设种子的 `IRandomService` 注入随机源；
  给定相同种子与相同输入，规则 MUST 产生完全可复现的相同输出。MUST NOT 使用
  全局 `Random` 或 `Guid.NewGuid()` 作为隐式随机源。
- 规则层测试 MUST 在无 UI、无文件系统、无网络依赖的条件下运行；涉及存档、
  时间、外部的部分 MUST 用测试替身（stub / fake）注入。
- 《科举浮生录规格书》第 16 节"必测单测清单"是覆盖下限，包含但不限于：
  贿赂累进与封顶、12 格惩罚矩阵逐格断言、连坐范围、遗传公式与 clamp 边界、
  贷款先本后息与月划扣、饥馑四阶段流转、天命寿数分布、俸禄表锚点、绝嗣判定。
- `dotnet test` 必须全绿方可提交；任何被跳过的测试（skip）MUST 在提交信息或
  任务跟踪中说明理由与恢复条件。

理由：本项目规则密度高、数值相互牵连，人工试玩无法覆盖分支组合。可确定复现的
单测是防止数值回归的唯一可靠手段，也是解耦是否真实的客观证据。

### V. 约定式提交与中文提交信息

仓库的 Git 提交历史 MUST 遵循约定式提交（Conventional Commits）规范，
且提交信息 MUST 使用中文描述。

- 标题格式 MUST 为 `<类型>(<范围>): <中文描述>`；范围可省略。类型限定为
  `feat`、`fix`、`docs`、`style`、`refactor`、`perf`、`test`、`build`、
  `ci`、`chore`、`revert`。
- 中文描述 MUST 说明"做了什么"，简洁且可读；MUST NOT 使用 `update`、`修改`、
  `调整` 等无信息量表述，也 MUST NOT 中英混杂拼写。
- 类型 MUST 与实际改动一致：新增功能用 `feat`、缺陷修复用 `fix`、纯测试用
  `test`、纯格式用 `style`、依赖与构建配置用 `build` 或 `chore`。
- 破坏性变更 MUST 在类型后加 `!`（如 `feat(core)!: ...`），并在正文以
  `BREAKING CHANGE: <中文说明与迁移方式>` 脚注说明影响与迁移。
- 一次提交 MUST 只做一件事；MUST NOT 把无关改动混入同一提交。正文（可选）
  用于说明"为什么"，必要时引用规格书章节号。

理由：可机械解析的提交类型让变更日志、版本号与影响面分析可以自动化；中文描述
让本项目的全部参与者无需翻译即可读懂历史。两者结合才使历史既机器可读、
又对人有用。

## 技术栈与工程约束

- 解决方案 MUST 使用 `.slnx`（XML）格式，禁止生成 `.sln`，两者 MUST NOT 共存。
- 依赖基线：CommunityToolkit.Mvvm（MVVM 源生成器）、MessagePack-CSharp
  （二进制存档）、Bogus `zh_CN`（姓名生成）、Serilog（日志）、LiveChartsCore
  （统计图表）、xUnit（测试）。新增依赖 MUST 说明其必要性。
- MUST NOT 引入任何游戏引擎。2D 表现仅限 WPF 控件、卡片、文字与几何图形；
  禁止外部美术资源与 emoji。
- 所有游戏规则数值 MUST 集中在 `KFL.Rules/Config/GameConfig.cs` 等常量/配置类中，
  严禁散落硬编码；影响平衡的数值 MUST NOT 写死在视图、ViewModel 或事件处理中。
- 金额内部一律以"文"为单位的 `decimal` 计算（1 贯 = 1000 文），仅在展示层换算为
  贯/文。
- 存档、日志等运行时路径 MUST 可配置；存档目录默认取可执行文件相对路径 `saves/`。
- 日志 MUST 使用 Serilog 结构化输出；MUST NOT 以裸 `Console.WriteLine` 充当日志。

## 开发工作流与质量门禁

- 开发 MUST 按《科举浮生录规格书》第 16 节既定顺序推进；跨阶段提前动工 MUST 在
  计划文档中说明理由。
- 阶段验收门禁（三项同时满足）：`dotnet build` 零警告零错误；`dotnet test` 全绿；
  `.slnx` 存在且无 `.sln` 共存。
- 提交前 MUST 在本地完成构建与测试，MUST NOT 提交已知失败的代码。
- 涉及规则、数值或存档格式的改动 MUST 同步核对第 IV 条要求的测试覆盖，并在
  代码审查中作为必查项。
- 代码审查 MUST 核验本章程各项约束，重点为：依赖方向是否被破坏、规则层是否
  混入 UI/环境依赖、提交信息是否符合第 V 条。
- 规格书未覆盖的细节 MUST 按第 17 节裁决规则处理：先循同节类似规则的模式，
  再取对玩家最直观的解释，仍无法确定时暂停并向用户提问。MUST NOT 自创影响
  平衡的核心数值。
- 复杂度 MUST 被论证：新增抽象或分层必须解决当下已存在的问题，MUST NOT 为
  假想的未来需求预置结构。

## Governance

本章程优先于其他开发实践，与规格书冲突时以规格书的数值定义为准、以本章程的
架构与流程约束为准，冲突本身 MUST 作为修订议题提出。

- **修订程序**：任何修订 MUST 以文档形式提出（改动内容、理由、对既有代码的影响、
  迁移计划），经项目所有者批准后，方可通过 `/speckit-constitution` 写入
  `.specify/memory/constitution.md`，并同步更新版本号与日期。
- **版本策略**：版本号遵循语义化版本 `MAJOR.MINOR.PATCH`。
  MAJOR——删除或重新定义原则等不向后兼容的治理变更；MINOR——新增原则或章节、
  实质性扩展既有指引；PATCH——措辞澄清、错别字修正等非语义调整。
- **合规审查**：每次代码审查与每个功能的收尾验收 MUST 核验本章程各项约束。
  偏离 MUST 被显式记录并说明理由，或先经修订放宽约束。无法核验合规的改动
  MUST NOT 合入。
- **运行时指引**：开发期的具体结构与命令以《科举浮生录规格书》与
  `.specify/templates/plan-template.md` 为准；两者 MUST NOT 与本章程冲突。

**Version**: 1.0.0 | **Ratified**: 2026-10-05 | **Last Amended**: 2026-10-05
