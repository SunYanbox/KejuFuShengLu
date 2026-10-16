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

### 坑 3：被打断的测试留下孤儿 testhost，锁死 `bin\`

- **现象**：`bin\` 下的 DLL 删不掉（`UnauthorizedAccessException`），或清理产物目录失败；
  `git status` 看不到它们（`bin` 已被忽略），于是残留会一直躺在仓库里。
- **原因**：坑 2 的开关一并关掉了「父进程死掉时 testhost 自杀」的看门狗，而宿主被强杀时
  `vstest.console` 不会回收子进程 → 孤儿 `testhost.exe` 继续占着自己的 `bin\...\*.dll`。
- **判定**：`Get-CimInstance Win32_Process -Filter "Name='testhost.exe'"` 里 `ParentProcessId`
  已不在进程表中，即为孤儿；**不要去改文件 ACL**。
- **处置**：`Get-Process -Name testhost | Stop-Process -Force` 后再删。

---

## Agent 工作流：子代理并发上限

> `$speckit-plan` / `$speckit-tasks` 这类流程要求事实**精确到行号**（成员数、步骤序号、既有签名、
> 上游键表锚点），很容易诱使「一次派一堆子代理去通读」。但**受限宿主的速率额度是主会话与子代理
> 共用的**，并行度直接乘在额度上；子代理读得越全，单次消耗越大。

### 现象与判定

- **现象**：一轮内派出 4~5 个「深度通读型」子代理后触发速率限制——本轮后续请求被拒或显著变慢，
  用户侧直接可感。
- **原因**：子代理与主会话共用同一速率额度；更糟的是**任务边界重叠**——同一个文件被两个子代理
  各读一遍，属于纯粹的重复消耗。
- **判定**：一份文件在**同一轮**里被（主会话 + 子代理）或（两个子代理）同时读取 = 浪费，应改写分工；
  子代理把完整报告**投递两遍** = 重复 token，prompt 里 MUST 事先禁止。

### 约定

1. **单轮并发派发 MUST NOT 超过 2**；优先按「**代码事实**（`src/` + `tests/`）」与
   「**文档与工件锚点**（`科举浮生录规格书.md` + `specs/*`）」两刀切。
2. 派发前先问一句「**这个文件我自己读是不是更快**」：主会话已经在读的同一批文件，
   MUST NOT 另派子代理重读。
3. 子代理 MUST NOT 再下钻派发子代理（本仓库约定，避免层级放大消耗）。
4. 确实需要更多并发时改为**串行分批**：宁可多花一轮，也不炸额度。
5. 子代理的 prompt MUST 要求「只回结论 + 精确锚点（`path:line-line` + 逐字签名 / 数值）」，
   MUST NOT 回传整段原文，收尾消息 MUST NOT 复述一遍完整报告。

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

---

## 跨特性形状变更的回写义务

真源与工件的权威顺序见章程「需求真源」（`.specify/memory/constitution.md`）与规格书 §16.2：

- **规格书是唯一的需求与数值真源**。工件与规格书不一致时，以规格书为准**修正工件**；
  规格书变更 MUST 与受影响的 `spec.md` / `plan.md` / `tasks.md` **同步提交**。
- **活工件**（`spec.md`、`plan.md`、`research.md`、`data-model.md`、`contracts/`、`quickstart.md`）
  MUST 与当前实现一致；**历史记录类工件**（`tasks.md` 的既有条目、`implementation-notes.md`、
  既有 `checklists/`）MUST **NOT 回改**——它们记录当时的阶段事实。

### 铁律：变更授权在变更源，回写义务在同一提交

对**上游特性**的类型/契约（`KFL.Core` 领域类型、已有 `contracts/`）做**形状变更**时
（字段增删、签名变更、步骤序列插入、状态语义扩展），MUST 先在**变更源的 `spec.md` / `plan.md`**
中声明（涉数值者须先经规格书 §17 裁决回写），并在**同一提交**内完成三件事：

1. **回写上游活工件**：被触碰的字段表、不变量、接缝表、步骤序列、签名表全部同步；
2. **回写自身 `spec.md`**：它是 skills（`speckit-converge`）定义的 **sole source of intent**，
   实现与契约改了而 `spec.md` 没改，等于真源失真；
3. **在 `tasks.md` 登记回写范围**：写清「同步了哪些文件、同步到哪里」，供后续审计核对。

**反例（本项目真实发生过）**：下游特性把 `StatusTimers` 从 3 个字段扩到 4 个、把月度结算从
六步扩到七步、把官俸从「有官即全俸」改成三态，代码与下游契约都改了，但上游 `data-model.md`
的不变量与上游契约的步骤表没跟上——**变更本身有授权，缺的只是同步清单**。范本见
`specs/003-opening-assets-officialdom/tasks.md` 的 T003（它已写对，只是当时只登记了一处）。

**禁止反向推理**：「代码已经这么写」**不构成**「文档过时」的证据。变更若只存在于代码或
`tasks.md`、任何 `spec.md` / `plan.md` / 契约都没声明，那要修的是**代码**（按 `speckit-converge`
追加收敛任务），或先经规格书 §17 裁决回写规格书，再改工件。
