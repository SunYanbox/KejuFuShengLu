# 阶段 0 研究：解决方案骨架与家族领域模型

**Feature**: `001-core-skeleton` | **Date**: 2026-10-05 | **Spec**: [spec.md](./spec.md)

本文件只记录**本阶段真实存在**的技术抉择。凡在 001 范围内无消费者的结构一律不引入
（章程「复杂度 MUST 被论证」）。下列结论中带「实测」的，均在本机 .NET SDK 10.0.401
上实际执行验证过，不是推断。

---

## R-01 .NET 10 基线与 SDK 固定方式

**Decision**: 仓库根新增 `global.json`，只约束到 **.NET 10 主次版本带**：
`"version": "10.0.100"` + `"rollForward": "latestFeature"`（接受任意已安装的 `10.0.x`
SDK），**不锁具体补丁号**。非 UI 工程显式声明 `net10.0`，UI 工程显式声明
`net10.0-windows`。

**Rationale**: 用户裁决——「只限制到 .NET 10，不应限制具体版本号，避免影响未来可能的
CI/CD」。锁死补丁号会让任何尚未安装该补丁的 CI 机器直接无法构建；只约束到 10.0 版本带
仍然满足章程原则 I 的真正目的：**不掉回 .NET 9、不降级、不多目标**。实测本机
`dotnet --version` = `10.0.401`，`dotnet --list-sdks` 另有 `9.0.308`、`9.0.311`、
`10.0.201`；`Microsoft.WindowsDesktop.App 10.0.12` 已安装，WPF 可运行。在
`10.0.100` + `latestFeature` 下本机会选中 10.0.401，未来装了 10.0.6xx 也照常工作。

**章程同步**: 章程原则 I 原文是「SDK 版本 MUST 通过仓库根的 `global.json` 固定，保证
本机与 CI 使用同一基线」，与本裁决有措辞冲突，已按治理条款发 **PATCH 至 v1.1.1**：
明确「同一基线」指同一 .NET 10 运行时与语言基线而非同一 SDK 构建号，并禁止
`rollForward: disable`。

`TargetFramework` **不**写进 `Directory.Build.props` 默认值——若给了默认值，
某工程漏写或写错 TFM 时会被静默补上正确值，守卫测试就抓不到真实错误。

**Alternatives considered**:
- 锁 `10.0.401`：对未来 CI 不友好，已按用户裁决弃用。
- `rollForward: disable`：把「没装这个补丁」变成硬失败，收益不抵可用性损失。
- 不建 `global.json`：无法阻止 SDK 回落到 .NET 9，违反章程原则 I。

---

## R-02 `.slnx` 结构与 `<TestProject>` 陷阱

**Decision**: 六个工程在 `KejuFuShengLu.slnx` 中**全部**用 `<Project Path="..." />`
扁平声明，路径用反斜杠；仓库中 MUST NOT 出现 `.sln`。

**Rationale**: 实测 SDK 10.0.401，把规格书 §2 原样的
`<TestProject Path="tests\T\T.csproj" />` 写进 `.slnx` 时：`dotnet build` **成功且零
错误**，但只构建了类库工程；`dotnet sln list` 也只列出 `src\A\A.csproj`——该元素被
**静默忽略**。改成 `<Project>` 后 `dotnet sln list` 与 `dotnet build` 均包含测试工程，
`dotnet test <slnx>` 正常跑出 1 passed。天真的实现会让 `KFL.Tests` 悄悄掉出解决方案，
直接击穿 §16「`dotnet test` 全绿」的门禁（全绿是因为根本没跑）。

已按 §17 裁决回写规格书 §2，并在此留证。

**Alternatives considered**:
- 沿用 `<TestProject>`：不可行，见上。
- 采用 CLI 生成的 `<Folder Name="/tests/">` 分组形式：可用，但偏离 §2 的扁平布局，且引入目录分组的额外概念。

---

## R-03 唯一标识（UUID）的来源

**Decision**: `GameState.Id` 由注入的 `IRandomService` 生成的 16 字节转 `Guid`。

**Rationale**: 章程原则 IV 明文「MUST NOT 使用全局 `Random` 或 `Guid.NewGuid()` 作为
隐式随机源」，而 FR-012 又要求存档级状态持有唯一标识。复用 `IRandomService` 有三重
收益：合规；「同种子 → 同 UUID」可在单测中复现，正好给 US3 的确定性断言一个真实
消费者；不新增第三个接口。

**Alternatives considered**:
- 专用 `IIdGenerator`（默认实现用 `Guid.NewGuid()`）：语义更干净，但 001 没有「标识
  必须真随机」的证据，属于为假想需求预置抽象。
- **已记录风险**：若调试控制台（阶段⑦）允许重置随机种子后新建存档，理论上可能复现
  同一 UUID，而成就按 UUID 归属（§14）。该风险在 001 无触发路径（无控制台、无存档），
  留作阶段⑦的议题。

---

## R-04 游戏时间来源的形状

**Decision**: `IGameClock { GameDate Current { get; } }`，与 §2 的接口清单同层定义在
`KFL.Infrastructure`；001 提供以 `GameState` 当前年月为后端的实现。**不**引入系统
时钟抽象（`IClock` / `DateTime.Now` 那一类）。

**Rationale**: 规格书 §3 规定「逻辑推进仅由用户输入驱动」，游戏时间是推演出来的年月
（1 回合 = 1 游戏月），不是墙钟时间。经用户确认，采用「交付游戏时间抽象」方案：它让
规则层通过注入来源读取当前年月、而不是伸手进 `GameState` 的字段，使「同输入 → 同
结果」在 001 就有一个真实且诚实的断言对象。系统时钟在 001 没有任何消费者，其首次
出现是阶段④（存档写入时间戳），届时再引入。

**Alternatives considered**:
- 不引入任何时间抽象（`GameState` 裸字段，阶段②再谈）：最省，但放弃了规格书 Assumptions 已声明的「本阶段引入时间与随机来源两组抽象」。
- 引入墙钟 `IClock`：001 内零消费者，属预置结构。

---

## R-05 架构守卫的实现方式

**Decision**: 守卫集中在 `tests/KFL.Tests/Architecture/`，用三类互补证据：

| 证据类型 | 手段 | 抓什么 |
| --- | --- | --- |
| 工程级 | `XDocument` 解析 `.slnx` 与每个 `.csproj`（不改写） | 工程清单齐全、TFM 正确、非 UI 工程无 `UseWPF`/`UseWindowsForms`、`ProjectReference` 图单向且无环、仓库无 `.sln` |
| 程序集级 | `typeof(锚点类型).Assembly.GetReferencedAssemblies()` | 核心三工程不引用 `PresentationCore`/`PresentationFramework`/`WindowsBase`/`System.Drawing*`/`System.Windows.Forms` |
| 源码级 | 非 UI 工程源码文本扫描 | `DateTime.Now`/`UtcNow`/`Today`、`DateTimeOffset.Now`、`Environment.TickCount`、`new Random`、`Random.Shared`、`Guid.NewGuid()`、`File.`/`Directory.`/`Path.`、`HttpClient`/`Socket`/`Dns` |

**Rationale**: 只有工程级断言能抓住「依赖边」与「TFM」——它们不存在于程序集元数据里；
程序集级能抓住真实的类型引用（比文本扫描更硬）；源码级能抓住尚未被调用的环境依赖。
三者交叉覆盖 SC-003/SC-004。仓库根由 `AppContext.BaseDirectory` 向上寻找 `*.slnx`
定位（判定为找不到即失败）。

**Alternatives considered**:
- 引入 NetArchTest / Roslyn 分析器：章程要求「新增依赖 MUST 说明其必要性」，而上述三类
  证据已能覆盖本阶段全部断言；先用零依赖实现，待断言复杂度上升再论证引入。

---

## R-06 值域常量的归属（Core 还是 Rules）

**Decision**: 实体自不变量的值域常量（天赋、学业、体质 0~100）放
`KFL.Core/Config/AttributeLimits.cs`；影响平衡的规则数值放
`KFL.Rules/Config/GameConfig.cs`（本阶段该文件为空壳，只建位置）。

**Rationale**: 依赖方向是 `Core ← Infrastructure ← Rules`，`KFL.Core` **不可能**引用
`KFL.Rules`，因此把 0~100 写进 `GameConfig` 就等于让实体失去自校验（FR-005/FR-006 会
退化成只在测试里口头成立）。0~100 是实体的取值域，不是可调平衡数值；章程原文
「集中在 `KFL.Rules/Config/GameConfig.cs` **等常量/配置类**中」的「等」字，为 Core
侧同类物留了位置。

**Alternatives considered**:
- 全部数值归 Rules、Core 只存裸 `int`：实体不变量形同虚设。
- 修订章程把这条写死：**未做**。所有者已确认该归属可接受（2026-10-05），章程原文的
  「等常量/配置类」足以覆盖 Core 侧常量，无需修订。

**注**: 政绩上限 100（§8.2）**不**在本阶段进入 Core——它随阶段⑧的规则实现落地。

---

## R-07 年龄的表达

**Decision**: `Person.BirthDate : GameDate`，年龄由 `AgeAt(GameDate)` 派生；不存裸
「年龄」整数。

**Rationale**: §4.3 要求「生日当月转成年（男满 12 / 女满 14）」，裸年龄无法表达生日；
§12.2 要展示的年龄可由出生年月派生。`AgeAt` 只做日期算术，**不含** 12/14 等规则数值；
成年判定、青年/老人/儿童档位划分全部属阶段②。

**Alternatives considered**:
- 存 `Age` + `BirthMonth` 两个字段：冗余且二者可失配。

---

## R-08 亲属引用的方向

**Decision**: `Person` 存 `FatherId` / `MotherId` / `SpouseId` / `FormerSpouseIds`；
**子女引用由 `Family` 的索引派生**；配偶双向对称由 `Family` 的唯一变更入口保证。

**Rationale**: 双向冗余是引用失配的经典来源，而 spec 的 Edge Case 明确要求「亲属引用
不得产生重复节点或指向自身的闭环」。让 `Family` 作为聚合并持有派生索引，既满足「从任一
成员一次查询定位父母/配偶/子女」（FR-011、SC-006），也让章程原则 II「实体 MUST NOT
承担跨实体结算编排职责」自然成立。

**Alternatives considered**:
- `Person` 双向存 `ChildIds`：需要两处同步，失配即测试红。

---

## R-09 测试栈与运行方式

**Decision**: xUnit v2（`xunit` 2.9.x + `xunit.runner.visualstudio` 3.1.x +
`Microsoft.NET.Test.Sdk` 17.14.x），`TargetFramework=net10.0`；用
`dotnet test KejuFuShengLu.slnx` 运行。

**Rationale**: 章程依赖基线明列 xUnit。实测 SDK 模板组合在 net10.0 上
`dotnet test Probe.slnx` 正常（1 passed），且这是 SDK 默认组合，风险最低。

**Alternatives considered**:
- xUnit v3 / Microsoft.Testing.Platform：无收益，且偏离 SDK 默认模板。

---

## R-10 「零警告零错误」的落地

**Decision**: 根 `Directory.Build.props` 统一
`TreatWarningsAsErrors=true`、`Nullable=enable`、`ImplicitUsings=enable`、
`EnforceCodeStyleInBuild=true`、`AnalysisLevel=latest-Recommended`；**不**在其中设置
`TargetFramework`（见 R-01）。

**Rationale**: 实测新建的 classlib 与 WPF 模板在上述组合下均 **0 警告 0 错误**，说明
门禁可以开到最严而不会一开工就红。`TreatWarningsAsErrors` 把 §16 的门禁从「靠人记得看
输出」变成「构建直接失败」。

**Alternatives considered**:
- 只在 CI 开：本机提交时漏网，等于把门禁推到最后一环。

---

## R-11 WPF 工程在 001 的最小形态

**Decision**: `KFL.Presentation` = `net10.0-windows` 类库（`UseWPF=true`，不含窗口）；
`KFL.App` = `WinExe` + `App.xaml`/`MainWindow.xaml` 空壳（`StartupUri` 指向空窗口）。

**Rationale**: §16 阶段③ 才是「App 主界面 + 下月/快进」，001 只要求六个工程存在且可
构建（FR-001、SC-001）。`WinExe` 必须有入口点，`App.xaml` 正好提供，不会为了「能编译」
而写无意义的手写 `Main`。

**Alternatives considered**:
- `KFL.App` 做成类库：与 §2「MainWindow、调试控制台面板」的定位不符。
- 不建 App：违反 FR-001。

---

## R-12 规格书 §5.1 年龄档归属（本次用户裁决）

**Decision**: 成年 = **男满 12 周岁 / 女满 14 周岁**（与 §4.3 一致，生日当月生效）；
**未成年一律按儿童档**（男 0~11 岁 / 女 0~13 岁，5 文/日）；已成年且 ≤18 岁按青年档；
≥60 岁按老人档；其余按成人档。

**Rationale**: 用户直接裁决，且以 §4.3 为成年基准。原 §5.1 表头「儿童 0~12」与 §4.3
「男满 12 周岁成年」在 12 岁男性上重叠，必须消歧。该口径同时统一 §10.1 出身修正中的
「未成年」（农 −50%）。

**已回写**: 规格书 §5.1「年龄档归属」新增条目；§4.3 青年条目补未成年按儿童档的交叉引用。

**玩家可见后果（如实记录）**: 12~13 岁女性按儿童档（5 文/日），同龄男性按青年档
（7/8.5/10 文/日）——这是「未成年按儿童档」与「男 12 / 女 14 成年」两条规则叠加的必然
结果，不是笔误。

**Alternatives considered**:
- 儿童 = 0~11 且女性 12~13 单独设档：会新增规格书没有的档位与数值，违反 §17「禁止自创
  影响平衡的核心数值」。

---

## R-13 FR-015 的收敛（守卫测试与「无文件系统依赖」的冲突）

**Decision**: 把 spec FR-015 / SC-002 从「**全部**测试无文件系统依赖」收敛为
「**领域与规则测试**无界面、无文件系统、无网络依赖；架构守卫测试 MAY 读取本仓库的
工程文件与源码」。

**Rationale**: 守卫测试要断言 `.csproj`/`.slnx`/依赖边，必然要读文件；原表述把 FR-014
与 FR-015 放在同一句话体系里自相矛盾。章程原则 IV 的原文本来就只约束「**规则层测试**
MUST 在无 UI、无文件系统、无网络依赖的条件下运行」，收敛后与章程一致。

**Alternatives considered**:
- 让守卫改用 MSBuild 生成的数据源：仍要读文件，且新增构建耦合。
- 放弃工程级断言：SC-001/SC-003 无法验证。

---

## 未决事项（不阻塞本阶段，已登记）

| 事项 | 处置 |
| --- | --- |
| R-03 的 UUID 重复风险（调试控制台重置种子） | 阶段⑦ 开放议题，001 无触发路径 |
| R-01 的 SDK 固定粒度 | **已裁决**（2026-10-05）：只锁 .NET 10 版本带，章程已 PATCH 至 v1.1.1 |
| R-06 的常量归属解释 | **已确认**（2026-10-05）：实体值域常量放 `KFL.Core` 可接受，无需章程修订 |
| 资产/现金/储蓄/贷款池建模 | 阶段②（规格书 §5.3、§5.4），spec 已列入 Out of Scope |
| 系统时钟抽象 | 阶段④（存档时间戳）首次需要时引入 |
