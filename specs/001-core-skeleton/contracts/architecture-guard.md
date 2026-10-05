# 契约一：架构守卫（001 与其后所有阶段的构建期契约）

**Feature**: `001-core-skeleton` | **Date**: 2026-10-05

001 不对外暴露任何用户可见接口，它对外暴露的是**约束**：从此以后，任何人改工程结构、
加依赖、引平台库，都必须先过这张表。守卫测试即本契约的可执行形式。

## 1. 判定逻辑 MUST 为纯函数（可自证）

守卫 MUST 拆成两部分：

| 部分 | 职责 | 是否可注入输入 |
| --- | --- | --- |
| `ArchitectureRules`（纯函数/分析器） | 输入「工程清单 + 各 csproj 文本 + 源码文本」，输出违规列表 | 是 |
| 文件读取外壳（守卫测试） | 定位仓库根、读 `.slnx`/`.csproj`/`*.cs`，交给 `ArchitectureRules`，断言违规为空 | 否 |

**理由**：SC-003 要求「故意引入的违规被 100% 捕获」。若判定逻辑与文件读取焊死，就只能
靠真的去改仓库文件再改回来验证，既脆弱又可能留下脏改动。拆开后，用**合成的违规输入**
就能证明守卫真的会红，且全程不碰仓库。

## 2. 断言清单

| 编号 | 断言 | 违规示例 | 来源 |
| --- | --- | --- | --- |
| G-01 | 仓库中恰有 `KejuFuShengLu.slnx`，且递归（排除 `.git`、`bin`、`obj`）不存在任何 `*.sln` | 手滑生成 `KejuFuShengLu.sln` | §1、§16 |
| G-02 | `.slnx` 解析出**恰好六个** `<Project>`，路径集合 = `src\KFL.Core`、`src\KFL.Infrastructure`、`src\KFL.Rules`、`src\KFL.Presentation`、`src\KFL.App`、`tests\KFL.Tests` | 测试工程漏登记（见 R-02 的真实陷阱） | FR-001、SC-001 |
| G-03 | 每个工程的 TFM：非 UI = `net10.0`；UI = `net10.0-windows`；且任何工程都不得出现 `TargetFrameworks` | `KFL.Rules` 被改成 `net10.0-windows` | FR-002、章程原则 I |
| G-04 | 非 UI 工程的 csproj 不得出现 `UseWPF` / `UseWindowsForms` 的真值 | `KFL.Rules` 加 `<UseWPF>true</UseWPF>` | 章程原则 II |
| G-05 | `ProjectReference` 边集 ⊂ 层序允许集，且图中无环：Core(0) ← Infrastructure(1) ← Rules(2) ← Presentation(3) / App(4)；`KFL.Tests` 豁免，可引用任意工程 | `KFL.Core` 引用 `KFL.Rules` | FR-003、章程原则 II |
| G-06 | `KFL.Core` / `KFL.Infrastructure` / `KFL.Rules` 的程序集引用不含 `PresentationCore`、`PresentationFramework`、`WindowsBase`、`System.Drawing*`、`System.Windows.Forms` | 规则层 `using System.Windows.Media` | FR-003、SC-003 |
| G-07 | 非 UI 工程的 `*.cs` 文本不含禁用 token（下表） | 规则层写 `DateTime.Now` | FR-013、SC-004 |
| G-08 | 仓库根存在 `global.json`，其 `sdk.version` 主版本为 10 | SDK 漂移到 9.x | FR-002、章程原则 I |

### 2.1 G-07 禁用 token 清单

`DateTime.Now`、`DateTime.UtcNow`、`DateTime.Today`、`DateTimeOffset.Now`、
`Environment.TickCount`、`new Random(`、`Random.Shared`、`Guid.NewGuid()`、
`File.`、`Directory.`、`Path.`、`HttpClient`、`Socket`、`Dns.`

扫描 MUST 忽略注释与字符串字面量之外的误报不可控情形，因此实现上只做**词法级子串
匹配 + 行级豁免注释 `// arch-guard:allow`**；需要豁免时 MUST 在同一行说明理由。

## 3. 允许边集（G-05 的判定依据）

```text
KFL.Core            -> (无)
KFL.Infrastructure  -> KFL.Core
KFL.Rules           -> KFL.Core, KFL.Infrastructure
KFL.Presentation    -> KFL.Core, KFL.Infrastructure, KFL.Rules
KFL.App             -> KFL.Core, KFL.Infrastructure, KFL.Rules, KFL.Presentation
KFL.Tests           -> 以上全部（豁免层序）
```

## 4. 守卫自身的验收（SC-003）

| 用例 | 合成输入 | 期望 |
| --- | --- | --- |
| 反向依赖 | csproj 文本：`KFL.Core` 含指向 `KFL.Rules` 的 `ProjectReference` | 报 G-05 违规，且信息里出现两个工程名 |
| 平台泄漏（工程级） | csproj 文本：`KFL.Rules` 带 `<UseWPF>true</UseWPF>` | 报 G-04 违规 |
| 平台泄漏（源码级） | 源码文本：`using System.Windows.Media;` | 报 G-06 或 G-07 违规 |
| 环境依赖 | 源码文本：`var now = DateTime.Now;` | 报 G-07 违规 |
| 测试工程漏登记 | slnx 文本只列五个 `<Project>` | 报 G-02 违规 |
| 合法输入 | 真实仓库内容 | 零违规 |

**注意**：`<TestProject>` 元素被 `.slnx` 解析器静默忽略（R-02 实测），因此 G-02 的判定
MUST 基于 `.slnx` 的**可解析工程集合**，而不是文本里出现过几个 `Path=`。
