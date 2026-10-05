## 沙箱命令执行有误解决方法

> 受限宿主（DSH 等 Agent 沙箱）里会踩的环境坑，避免误判成代码缺陷。

### 坑 1：并行 MSBuild 节点 IPC 被拦

- **现象**：`dotnet build` / `dotnet test` 输出「生成失败」+ 0 警告 0 错误；`-v:n` 只看到
  `_FilterRestoreGraphProjectInputItems` 失败；伴生 `MSB4276`（本机 SDK 缺 workload locator）噪声。
- **判定**：环境问题，非代码缺陷——不受限环境下裸命令即可通过。
- **处置**：加 `-m:1 -nodeReuse:false`。

### 坑 2：testhost 父进程看门狗被拒

- **现象**：生成成功，测试阶段 `TESTRUNABORT` 中止；testhost 抛
  `Win32Exception (5) 拒绝访问`（`Process.set_EnableRaisingEvents`）。
- **原因**：testhost 启动时会打开父进程（`vstest.console`）句柄，用于「命令行死掉时自己也自杀」
  的保护；受限宿主不允许。与工程、TFM、xunit 版本、语言级别无关。
  首次定位过程见 `specs/001-core-skeleton/implementation-notes.md` 第 2 节。
- **处置**：加 `-p:_MSTestEnableParentProcessQuery=false`，**不必改任何文件**。
- **维护提示**：该属性是 `Microsoft.TestPlatform.TestHost.targets` 的**私有**开关
  （下划线前缀，且该 targets 在本工程之后求值、会无条件写入该项），升级 `Microsoft.NET.Test.Sdk`
  后若现象回归，先检查它是否被改名。
- **仓库约定**：`tests/KFL.Tests/KFL.Tests.csproj` 里该属性**默认注释，MUST NOT 以启用状态提交**；
  确需改文件时，验证后必须立即改回注释。

---

## 本仓库门禁（提交前四条全过）

```powershell
dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false     # 0 警告 0 错误
dotnet test  KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false
dotnet sln   KejuFuShengLu.slnx list                       # 恰好六个工程
Get-ChildItem -Recurse -Filter *.sln | Where-Object { $_.FullName -notmatch '\\\.git\\' }   # 期望无输出
```

第三条不能省：`.slnx` 解析器**不认识 `<TestProject>` 且不报错**，测试工程可能悄悄掉出解决方案，
「全绿」就变成「一个都没跑」。架构约束由 `tests/KFL.Tests/Architecture/` 的 G-01~G-08 在构建期强制。
