# 快速验证指南：解决方案骨架与家族领域模型

**Feature**: `001-core-skeleton` | **Date**: 2026-10-05

本指南是 001 的**人工可复现验收流程**。全部命令在仓库根执行（`E:\ProjectCsharp\Games\KejuFuShengLu`）。
设计细节见 [data-model.md](./data-model.md)，约束见 [contracts/](./contracts/)。

## 0. 前置条件

- Windows + **.NET 10 SDK**（`10.0.x` 任一版本即可；仓库根 `global.json` 只锁到 .NET 10
  版本带，`dotnet --version` 应输出 `10.0.*`）。本机实测为 `10.0.401`，且
  `Microsoft.WindowsDesktop.App 10.0.12` 已安装，WPF 可运行。
- 首次还原需要 NuGet 网络访问；还原完成后**构建与测试本身**不需要网络。

## 1. 三条门禁命令（对应规格书 §16「每阶段验收」）

```powershell
dotnet build KejuFuShengLu.slnx      # 期望：0 个警告、0 个错误
dotnet test  KejuFuShengLu.slnx      # 期望：全部通过，0 skipped
dotnet sln   KejuFuShengLu.slnx list # 期望：恰好列出六个工程
```

第三条命令不能省。`.slnx` 解析器**不认识 `<TestProject>` 且不报错**（见 R-02 实测），
「构建成功」完全可能意味着测试工程根本没进解决方案——`dotnet test` 的「全绿」会变成
「一个测试都没跑」。`dotnet sln list` 是唯一能一眼看穿的检查。

第四项门禁（`.slnx` 存在且无 `.sln` 共存）：

```powershell
Get-ChildItem -Recurse -Filter *.sln | Where-Object { $_.FullName -notmatch '\\\.git\\' }
# 期望：无输出
```

## 2. 演练一：守卫自证（唯一可靠的违规验证路径）

守卫写在 `tests/KFL.Tests/Architecture/`，其判定逻辑是**可注入输入的纯函数**（契约一 §1），
所以「违规必被抓」这件事由单测证明，不需要手工破坏仓库：

```powershell
dotnet test KejuFuShengLu.slnx --filter "FullyQualifiedName~GuardSelf"
```

**期望**：六组合成输入逐条被抓——① 反向依赖 → **G-05**；② 工程级平台泄漏
（`<UseWPF>true</UseWPF>`）→ **G-04**；③ 源码级平台泄漏（`using System.Windows.Media;`）
→ **G-06**（不是 G-07，该 token 不在契约一 §2.1 的清单里）；④ `var now = DateTime.Now;`
→ **G-07**；⑤ `.slnx` 只列五个 `<Project>` → **G-02**；⑥ 真实仓库内容 → 零违规。

**为什么反向依赖与平台泄漏不能手工演练**：本方案的允许边集是**全序**
（Core ← Infrastructure ← Rules ← Presentation / App），任何反向边都必然闭合成环；
而 `using System.Windows.Media;` 在 `net10.0` 工程里缺少 WPF 引用，也必然编译失败。
两种做法都会让「守卫失败」与「编译失败」不可区分——**编译失败不算守卫生效的证据**。
因此这两类违规的唯一可执行验证物是上面的合成输入。

## 3. 演练二：守卫真的会红吗（手工，选一条能编译的）

能手工制造、且**不影响编译**的违规只存在于工程清单类断言（G-01 / G-02 / G-08）。用 G-02：

1. 从 `KejuFuShengLu.slnx` 中临时删掉一行 `<Project>`（例如 `KFL.Presentation` 那行）。
2. 运行 `dotnet test tests\KFL.Tests\KFL.Tests.csproj`——**直接指定测试工程、不经 `.slnx`**，
   否则被删工程可能连带影响测试发现，跑出来的红是另一回事。
3. **期望**：架构守卫失败，失败信息中出现被删工程的路径。
4. **还原**该行，重跑确认恢复全绿（`git status --short` 应为干净）。

同类可编译的手工演练：在仓库根放一个同名 `.sln`（应报 G-01 违规）、把 `global.json` 的
`rollForward` 改成 `disable`（应报 G-08 违规）。

## 4. 演练三：确定性可复现

无需手工操作，由测试覆盖。人工核对点在测试名里：以固定种子构造两次随机来源，断言
两次抽取序列逐位相同；以固定种子 + 固定游戏时间基准两次构造存档级状态，断言两次的
唯一标识与起始年月完全一致。运行：

```powershell
dotnet test KejuFuShengLu.slnx --filter "FullyQualifiedName~Determinism"
```

**期望**：全部通过。若把种子改成不同值，序列必须立刻不同——这正是「确定性来自注入，
而不是碰巧」。

## 5. 演练四：领域档案可读可校验

```powershell
dotnet test KejuFuShengLu.slnx --filter "FullyQualifiedName~Core"
```

**期望**：覆盖以下五类夹具的全绿结果——多代同堂（≥3 代，可向上向下遍历且无环）、
娶入配偶与买来的旁系（**均为家族内无父母的外来者**，辈分按规格书 §4.4 指定）、
有配偶（双向一致）、有子女（父母指向正确）。字段边界（天赋/学业/体质 0 与 100 可存、
-1 与 101 被拒、功名 `Placement` 仅进士可非空、官阶 `null` 与 L1/L18 两端、
九种状态可任意并存）同样在这一步验证。

另有两组必须在此步看住的新断言（均来自 2026-10-05 的复审裁决）：

- **功名变迁历史**（research R-14）：历史按年月升序、只追加不改写；`CurrentDegree` /
  `CurrentPlacement` 严格等于末条；**追加一条降级记录后当前功名随之下降**（进士→贡士，
  且 `CurrentPlacement` 变回 `null`）；「尚无记录（出生即白身）」与「举人被降为白身后
  追加了一条白身记录」是两种不同状态，测试须分别断言。
- **辈分与家主**（research R-15）：血亲成员的辈分 = 父母辈分 + 1 且无写入通道；外来者
  （娶入配偶、买来的旁系）的辈分由家族指定，且在其已有子女后拒改；`HeadId` 为 `null`
  或指向**在册**成员，指向已亡/外嫁成员被拒，指向非本家族成员被拒。
- **婚姻与出身**（FR-011、§9.1、§10.2）：已有配偶者再被指定配偶时被拒（一夫一妻）；
  「丧偶 → 再婚」后前任进 `FormerSpouseIds`、`FormerSpouseIds` 不含当前配偶且无重复、
  子女的父母引用不变；四出身 × `HasShiStatus` 两值共八种组合互不约束（含「士出身 +
  无仕身份」即开局士出身不计仕身份）。
- **两处有意留白**（不是遗漏）：**开局成员辈分 = 0**（§4.4「辈分自创始者为 0」），
  据此「买来的旁系 = 家主辈分 + 1」在开局家主治下为 **1**；`Lifespan` 只校验 `>= 0`，
  **上界属逻辑轨 ⑦**（§4.2 天命寿数分布），本阶段 MUST NOT 断言上限。

## 6. 用户可见产物（本阶段刻意为空）

```powershell
dotnet run --project src\KFL.App
```

**期望**：打开一个**空窗口**。这不是缺陷——规格书 §16 把「App 主界面 + 下月/快进」
排在界面轨 U1，001 只要求 `KFL.App` 存在且可构建启动（FR-001）。

## 7. 验收对照

| 判据 | 怎么看出来 |
| --- | --- |
| SC-001 | 第 1 节 `dotnet build` 输出 0 警告 0 错误；`dotnet sln list` 六个工程 |
| SC-002 | 第 1 节 `dotnet test` 全绿；领域/规则测试不依赖界面、文件系统、网络 |
| SC-003 | 第 2 节合成输入用例（反向依赖、平台泄漏各一，即契约一第 4 节用例①③）+ 第 3 节 G-02 手工演练 |
| SC-004 | 守卫 G-07 静态扫描：非 UI 工程禁用 token 命中 0 次 |
| SC-005 | 第 5 节字段读写夹具：规格书 §4.1 字段与九种状态 100% 有对应表示 |
| SC-006 | 第 5 节多代同堂夹具：任一成员一次查询即可定位父母、配偶、子女 |

## 8. 常见误判（照抄规格书原样会踩的坑）

| 现象 | 原因 | 处置 |
| --- | --- | --- |
| `dotnet build` 成功但测试一个没跑 | `.slnx` 里的 `<TestProject>` 被静默忽略 | 用 `<Project>`；跑 `dotnet sln list` 核对（R-02，已回写规格书 §2） |
| 构建报 TFM 冲突或找不到 WPF | 把 `TargetFramework` 写进了 `Directory.Build.props` 默认值 | TFM 必须由每个 csproj 显式声明（R-01） |
| `DateTime.Now` 守卫误报 | 出现在注释或豁免行 | 用行级 `// arch-guard:allow` 并在同注释说明理由 |
