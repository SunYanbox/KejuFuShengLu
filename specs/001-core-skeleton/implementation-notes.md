# 001 实施记录（归档）

**来源**：本文原为仓库根 `HANDOFF.md`（会话交接文档，未纳入版本控制）的第 3.1 与第 11 节。
001 完成后 `HANDOFF.md` 已删除，其中**在仓库里没有第二个落点**的内容移到这里，
以免它们只存在于某次会话的上下文里。实施日期：2026-10-05。

**这份文件不承载任何需求**：需求的唯一真源仍是 `spec.md` / `data-model.md` / `contracts/`；
下面记的是「实施出来的东西与规格之间的差额」以及一个本机环境坑。

---

## 1. 四条门禁的实测输出

删光全部 `bin`/`obj` 后在仓库根执行裸命令：

| 门禁 | 实测输出 |
| --- | --- |
| `dotnet build KejuFuShengLu.slnx` | **0 个警告、0 个错误** |
| `dotnet test KejuFuShengLu.slnx` | **163 项全部通过、0 skipped** |
| `dotnet sln KejuFuShengLu.slnx list` | 恰好六个工程（含 `tests\KFL.Tests\KFL.Tests.csproj`） |
| 递归 `*.sln` | 无输出 |

> 上表是**归档当时**的实测值。此后按一致性分析修复了 I4/I5/S1 三项、按 S2 裁决收紧了
> 外来者辈分，带来用例增删——**当前**总计 **165** 项（Core 116 / Infrastructure 23 /
> Architecture 26），门禁仍为 0 警告 0 错误、0 skipped。

补充证据：

| 项 | 实测输出 |
| --- | --- |
| `--filter FullyQualifiedName~Core` / `~Determinism` / `~GuardSelf` / `~Architecture` | 归档当时 113 / 6 / 7 / 27 项，当前 116 / 6 / 7 / 26 项，均全绿（`~X` 简写会被 vstest 判为「格式不正确」，必须写全 `FullyQualifiedName~X`） |
| `dotnet run --project src\KFL.App` | 打开空窗口，`MainWindowTitle` = 「科举浮生录」（主界面属阶段③） |
| T035 手工演练 | 临时从 `.slnx` 删掉一行 `<Project>` 后，G-02 报出「实际 5 个」与缺失路径；还原后文件逐字节一致。G-05/G-06 的同类演练**未做**——守卫失败与编译失败在该场景下无法区分 |

对应提交（`main`）：
`f2b7c0c build: 建立 .NET 10 解决方案骨架与六个工程` →
`97458a0 feat: 交付家族领域档案与两组注入接缝` →
`9d6e6d5 test: 交付领域测试、接缝测试与八条架构守卫` →
`45a45f6 docs: 标记 001 任务清单全部完成` →
`4e68a80 fix(tests): 绕开 testhost 父进程看门狗导致的测试中止`

---

## 2. 环境注记：testhost 的「父进程看门狗」

**这不是代码问题，但会让人误以为代码有问题**，故留档。

**现象**：`dotnet test` 的生成阶段成功，测试阶段以 `TESTRUNABORT` 中止，testhost 抛未处理异常：

```
System.ComponentModel.Win32Exception (5): 拒绝访问
  at System.Diagnostics.Process.set_EnableRaisingEvents(Boolean)
  at ...TestPlatform.PlatformAbstractions.ProcessHelper.SetExitCallback(Int32, Action`1)
  at ...TestHost.DefaultEngineInvoker.SetParentProcessExitCallback(...)   line 242
  at ...TestHost.DefaultEngineInvoker.Invoke(...)                         line 138
```

**定位**：崩点唯一——testhost 启动时按 `--parentprocessid` 去打开父进程句柄，
`Process.EnableRaisingEvents` 报「拒绝访问」。该调用的用途是 VSTest 的**看门狗**
（`vstest.console` 死掉时 testhost 自杀），与工程、TFM、xunit 版本、语言级别无关。

- **实测触发条件是路径**：同一份 `bin` 输出、同一条命令，测试程序集放在工作区外的目录下
  **163 项全过**，放在本仓库路径下必崩。是本机进程宿主给的父进程句柄打不开，属环境侧现象。
- **已排除**：`global.json`、`Directory.Build.props`、xunit 版本、CWD、`cmd`/`pwsh` 外壳、
  MSBuild 节点复用、后台作业；新建的裸 `dotnet new xunit` 工程放进本仓库同样崩。
- **处置**（已落到仓库，见 `tests/KFL.Tests/KFL.Tests.csproj` 的注释）：置私有属性
  `_MSTestEnableParentProcessQuery=false`，让 testhost 跳过这次父进程查询。它就是
  `Microsoft.TestPlatform.TestHost.targets` 自己读取的开关（默认 `true`）；该 targets 还会
  无条件写入一个 `RuntimeHostConfigurationOption`，且在工程之后求值，所以**只能用这个属性，用 ItemGroup 覆盖会被盖掉**。
- **代价**：放弃「console 死掉时 testhost 自杀」这一项保护，测试的发现 / 执行 / 结果不受影响；
  唯一影响是 `vstest.console` 异常退出时可能留下孤儿 testhost（CI 上由作业超时兜底）。
- **回归提示**：升级 `Microsoft.NET.Test.Sdk` 后若本现象重现，先回来检查该私有属性是否被改名。

---

## 3. 实施期间产生的、需要裁决或知情的事项

| # | 事项 | 现状与建议 |
| --- | --- | --- |
| 1 | **T009 原文要求 `KFL.Tests` 引用全部五个工程**，但 `net10.0` 的测试工程在物理上引用不了 `net10.0-windows` 的两个 UI 工程，还原期报 `NU1201` | 已按「最小忠实」只保留三个非 UI 工程的 `ProjectReference`（守卫只读文本，不需要 UI 程序集）。**已回写**：T009 的措辞改成了三个非 UI 工程并写明 NU1201 理由，不再只看 tasks.md 就会读到错误事实 |
| 2 | **`SeededRandomService` 用播种的局部 `Random` 实例**，命中 G-07 字面清单里的 `new Random(` | 按契约一 §2.1 的行级豁免机制在**同一行**标注了理由。语义上它是显式播种的局部实例，不是隐式全局源（契约二 §1 禁的是「全局 `Random` / `Random.Shared` / `Guid.NewGuid()`」）。**是否改成自实现 PRNG 以彻底消除豁免待定** |
| 3 | **`System.Random` 对给定种子的算法不保证跨 .NET 版本稳定** | 001 无落盘，「同种子 → 同结果」在单次运行内成立。若阶段④要求跨版本重放存档，需改用自实现的确定性算法。已写进 `SeededRandomService` 的类型注释 |
| 4 | **`Family.HeadId` 的「指向在册成员」不变量只在 `SetHead` 时校验** | 家主**上任后**死亡 / 外嫁会让该不变量暂时失效，直到阶段⑧的继任判定修正。001 的**产品代码**无死亡推进路径、不可抵达该状态，但**测试里构造过**（夹具已 `SetHead(founder)`，用例随后把 founder 置 `Deceased`，见 `FamilyTests` 的归档用例）。未做任何隐藏处理 |
| 5 | **`KFL.Tests.csproj` 为绕开本机 testhost 崩溃，置了 `_MSTestEnableParentProcessQuery=false`**（见上节） | 该属性是 `Microsoft.TestPlatform.TestHost.targets` 的私有开关，会让 testhost 不再监视父进程。**对测试结果零影响**。若不接受仓库里带这个环境性开关，可删掉它并把构建输出挪到工作区外（`-p:BaseOutputPath=<工作区外>`）绕过——但那样必须同时恢复 `RepositoryLocator` 的兜底起点，因为契约 T028 规定它只从 `AppContext.BaseDirectory` 向上查找 |
| 6 | **外来者辈分的变动次数**（在此之前规格书 §4.4、tasks T020 与代码三处口径不一致） | **2026-10-05 裁决**：一名外来者的辈分**总共只变动两次**——家族指定（落定）一次 + **首次在家族内成婚**时对齐配偶辈分一次，此后终身不可变更。已落到规格书 §4.4、data-model §2.1/§2.2、tasks T020/T021 与 `Family` 的入口校验，并补了三条测试（至多落定一次 / 首次成婚额外变动 / 丧偶再婚不再变动）。未用的那一次机会**不保留**：首次家族内成婚一旦发生，辈分即终局 |
| 7 | **`Family.Marry` 的写入顺序**：先写双方 `SpouseId`，再对齐外来者辈分 | 对齐那一步若抛异常（外来者已有子女、辈分无法在此时落定），婚姻关系**已经写进双方**——失败的调用留下半提交状态。001 内需要「外来者先有子女、后成婚」才会触发，无产品路径，但这是一处真实的可重入风险。**未修**，等裁决：是否改成「先校验、后写入」（同 `Register` 的做法） |

2026-10-05 后续修复：一致性分析报出的 **I1~I6、D1、S1** 均已按裁决改完——I5 删掉了
`SolutionShapeTests` 里那条自加的行计数断言（G-02 已由权威判定覆盖），S1 改引规格书
§4.1/§4.4/§12.1/§1 的真实出处，I4 修掉 T035 的「唯一」措辞。

另有三处**代码级**的、为实现而必须做的取舍（理由都写在源码注释里，不是悄悄加的）：

- `StatusFlag` 加了 `None = 0` 作空集合零值——九种状态仍是原九种，`None` 不算第十种。
- `Family.BoughtCollateralGeneration`（= 家主辈分 + 1）与 `AddFoundingMember` / `AddChild` /
  `AddOutsider` / `SetOutsiderGeneration` / `Marry` / `EndMarriage` / `SetHead` 是为了让
  规格书 §4.4 的辈分规则与 §9.1 的一夫一妻**由领域强制**、而不是只在测试里口头成立；
  构造成员的入口是必需的（夹具 MUST 只用公开 API 构造）。
- `IRandomService.Next`（`SeededRandomService` 自身**没有任何特性**）与 `StatusFlag` 各有一处
  `[SuppressMessage]`：前者因 `Next` 是 VB 保留字（签名逐字取自契约二，不能改名），后者因
  `CA1711` 与 data-model §1.3 固定的类型名冲突。都是**带理由的定点抑制**，不是全局关规则，
  也没有用 `#pragma`。
- 行级豁免 `// arch-guard:allow` 共**两处**，且都必需：一处在 `SeededRandomService` 的播种实例上
  （见本表第 2 条）；另一处在 `tests/KFL.Tests/Fixtures/FamilyFixtures.cs` 的第 20 行——
  该行是**说明性注释**，为了写明「夹具标识一律确定性生成、不用全局随机源」而不得不写出被禁的
  字面量，而 `Fixtures/` 正在 G-07 的扫描范围内（注释同样计入扫描）。

---

## 4. 下一步

```
$speckit-analyze      # 对「已实现代码 × spec/plan/tasks」再做一轮一致性分析
$speckit-converge     # 若发现未落地的需求，追加任务
```

然后按规格书 §16 进入**阶段②（月度结算与经济）**。
