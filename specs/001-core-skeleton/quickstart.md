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

## 2. 演练一：守卫真的会红吗（反向依赖）

守卫写在 `tests/KFL.Tests/Architecture/`，其判定逻辑是**可注入输入的纯函数**，所以
「违规必被抓」这件事是被单测证明的，不需要手工破坏仓库。手工演练如下：

1. 在 `src\KFL.Core\KFL.Core.csproj` 中临时加入对 `KFL.Rules` 的 `ProjectReference`。
2. 运行 `dotnet test KejuFuShengLu.slnx`。
3. **期望**：架构守卫失败，失败信息中出现 `KFL.Core` 与 `KFL.Rules` 两个工程名，
   并指出这是一条向上（反向）的依赖边。
4. **删除临时改动**，重跑确认恢复全绿（`git status --short` 应为干净）。

若第 2 步因为「Core 引用了 Rules 导致编译错误」而失败，那是另一回事——守卫的价值恰恰在于
当依赖边**能编译通过**时仍然把它拦下。

## 3. 演练二：守卫真的会红吗（平台类型泄漏）

1. 在 `src\KFL.Rules` 下任意一个 `.cs` 文件顶部临时加入 `using System.Windows.Media;`。
2. 运行 `dotnet test KejuFuShengLu.slnx`。
3. **期望**：架构守卫失败并指出违规工程与文件。
4. 还原并重跑确认全绿。

同类演练：把 `KFL.Rules` 的 TFM 改成 `net10.0-windows`，或用 `<UseWPF>true</UseWPF>`，
都应在守卫中失败（G-03/G-04）。

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

**期望**：覆盖以下三类夹具的全绿结果——多代同堂（≥3 代，可向上向下遍历且无环）、
有配偶（双向一致）、有子女（父母指向正确）。字段边界（天赋/学业/体质 0 与 100 可存、
-1 与 101 被拒、功名 `Placement` 仅进士可非空、官阶 `null` 与 L1/L18 两端、
九种状态可任意并存）同样在这一步验证。

## 6. 用户可见产物（本阶段刻意为空）

```powershell
dotnet run --project src\KFL.App
```

**期望**：打开一个**空窗口**。这不是缺陷——规格书 §16 把「App 主界面 + 下月/快进」
排在阶段③，001 只要求 `KFL.App` 存在且可构建启动（FR-001）。

## 7. 验收对照

| 判据 | 怎么看出来 |
| --- | --- |
| SC-001 | 第 1 节 `dotnet build` 输出 0 警告 0 错误；`dotnet sln list` 六个工程 |
| SC-002 | 第 1 节 `dotnet test` 全绿；领域/规则测试不依赖界面、文件系统、网络 |
| SC-003 | 第 2、3 节演练 + 守卫自身的合成输入用例（见契约一第 4 节） |
| SC-004 | 守卫 G-07 静态扫描：非 UI 工程禁用 token 命中 0 次 |
| SC-005 | 第 5 节字段读写夹具：规格书 §4.1 字段与九种状态 100% 有对应表示 |
| SC-006 | 第 5 节多代同堂夹具：任一成员一次查询即可定位父母、配偶、子女 |

## 8. 常见误判（照抄规格书原样会踩的坑）

| 现象 | 原因 | 处置 |
| --- | --- | --- |
| `dotnet build` 成功但测试一个没跑 | `.slnx` 里的 `<TestProject>` 被静默忽略 | 用 `<Project>`；跑 `dotnet sln list` 核对（R-02，已回写规格书 §2） |
| 构建报 TFM 冲突或找不到 WPF | 把 `TargetFramework` 写进了 `Directory.Build.props` 默认值 | TFM 必须由每个 csproj 显式声明（R-01） |
| `DateTime.Now` 守卫误报 | 出现在注释或豁免行 | 用行级 `// arch-guard:allow` 并在同注释说明理由 |
