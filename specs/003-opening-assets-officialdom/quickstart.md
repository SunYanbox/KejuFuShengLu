# 快速验收：开局资产与官吏体系

**Feature**: `003-opening-assets-officialdom` | **Date**: 2026-10-08

本文件是**可运行**的人工验收流程：每条场景都能用一条命令跑出来，且不依赖界面、存档或网络。
断言细节见 [contracts/](./contracts/)，字段与不变量见 [data-model.md](./data-model.md)，
抉择依据见 [research.md](./research.md)。

## 1. 前置条件

- .NET 10 SDK（仓库根 `global.json` 只锁 `10.0` 版本带，任意 `10.0.x` 均可）。
- 仓库根目录执行；**不新增工程**，仍为六个。
- 本特性**新增 1 个 NuGet 依赖**：`Bogus 35.6.1`（仅 `KFL.Infrastructure`）。
  它属章程依赖基线的既有项，本机 NuGet 缓存中已存在（含 `net6.0` 资产），离线可还原；
  若某台机器还原失败，症状是 `NU1101`/`NU1102`，与代码无关。
- 受限宿主（Agent 沙箱）里的两个已知环境坑与处置见 `AGENTS.md`：并行 MSBuild 节点 IPC 被拦
  （加 `-m:1 -nodeReuse:false`）、testhost 父进程看门狗被拒（加
  `-p:_MSTestEnableParentProcessQuery=false`，**不要**改文件——该属性在
  `tests/KFL.Tests/KFL.Tests.csproj` 里默认注释，MUST NOT 以启用状态提交）。

## 2. 门禁（四条，全过才算完成）

```powershell
dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false                              # 期望：0 警告 0 错误
dotnet test  KejuFuShengLu.slnx -m:1 -nodeReuse:false `
  -p:_MSTestEnableParentProcessQuery=false                                        # 期望：全绿，0 skipped
dotnet sln   KejuFuShengLu.slnx list                                              # 期望：恰好六个工程
Get-ChildItem -Recurse -Filter *.sln | Where-Object { $_.FullName -notmatch '\\\.git\\' }   # 期望：无输出
```

> 第三条不能省：`.slnx` 解析器**不认识 `<TestProject>` 且不报错**，测试工程可能悄悄掉出
> 解决方案，「全绿」就变成「一个都没跑」（001 R-02 的实测陷阱）。

`dotnet test` 后若 `bin\` 下的 DLL 删不掉，是上一次被强杀留下的孤儿 `testhost` 占用
（`Get-Process -Name testhost | Stop-Process -Force` 后再删）——**不要去改文件 ACL**。

## 3. 场景化验收

每条场景对应一个测试文件（`tests/KFL.Tests/Rules/`），也可用 `--filter` 单独跑。

### S1 四出身开局逐项（US1、SC-001、SC-002）

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false `
  -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~NewGameSetupTests"
```

- **夹具**：`SeededRandomService(固定种子)` + 固定姓氏（另加一条「姓氏随机」的用例）。
- **断言**（四种出身各 8 类事实，共 32 项）：成员数与性别、年龄区间（家主 28±5／士 30±5、
  配偶 25±5、孩子 0~8）、辈分（家主与配偶 0、孩子 1）、家主、婚姻与父母引用、
  四项天赋/学业/体质/天命寿数的初始化口径、初始资产与资金池、士家主的功名记录。
- **期望**：农 = 现金 80 + 田 40 + 农村宅 1；工 = 现金 80 + 农村宅 1；商 = 现金 500 + 城市宅 1 +
  商本 **300（商本池）**；士 = 现金 200 + 农村宅 1 + 1 条「举人 / Initial」记录。
  四者的 `Family.HasShiStatus` 全部为 `false`。
- **反向验证商本不错池**：300 贯 MUST NOT 进入「付不起生活费」的可付额，也 MUST NOT 进入
  工出身 bonus 的资产基数（两条 002 既有口径）；工 bonus 的对照用**工**出身存档另跑一次 12 月。

### S2 开局不落条目与确定性（FR-010、FR-011、SC-008）

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false `
  -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~NewGameSetupTests|FullyQualifiedName~DeterminismTests"
```

- 开局后的 `economy.Ledger.Entries` MUST 为空；第一次月末结算起才有条目。
- 相同出身/姓氏/难度/种子**连续两次**开局：成员数、性别、年龄、姓名、四项天赋、学业、体质、
  天命寿数、辈分、婚姻与父母引用、资产与资金池**逐字段完全相同**（契约六 §3 的次序）。

### S3 待阙与授官（US2、SC-003、SC-004）

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false `
  -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~AppointmentTests"
```

- 造一名带进士功名（含甲第）的成员 → 触发「及第入仕」→ 状态为待阙、剩余月数落 **6~24 含端点**、
  当月俸禄 **0**；待阙期内逐月结算都不出现 `OfficialSalary` 条目。
- 推进到剩余月数为 0 的当月：官阶 = 按甲第映射（一甲 L11 / 二甲 L13 / 三甲 L15 /
  特奏名 L18），待阙状态清除，**当月**开始出现俸禄条目（FR-019；本步在收入之前）。
- 重复触发「及第入仕」MUST 被拒绝且 MUST NOT 重置剩余月数；
  「名次是状元但甲第写成二甲」MUST 在构造期就被拒（`DegreeRecord` 的相容性不变量），
  「进士但甲第缺失」MUST 在授官入口被拒（不猜等级）。

### S4 政绩与考课（US3、SC-005、SC-006）

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false `
  -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~CareerAdvanceTests"
```

- 政绩每月 +1 且封顶 100；非官员、待阙者、已致仕者 MUST NOT 增长（各 1 条）。
- 在职月数：第 **35** 个月 MUST NOT 判定、第 **36** 个月判定一次，判定后计时重置；
  概率 = `min(25% + 政绩 × 0.3%, 70%)`（用固定取值的随机替身逐点断言）；
  成功级数 −1、失败不变、L1 时成功仍维持 L1。
- 禁升期成员到期时 MUST **跳过**判定（不消耗随机、不升迁），且 MUST NOT 补判。
- 边界断言：`Merit = 100` 时概率 55%（规格书的封顶 70% 在 0~100 区间内不可达，故以边界值断言）。

### S5 致仕与半俸（US4、SC-007）

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false `
  -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~RetirementTests|FullyQualifiedName~SalaryModeTests"
```

- 69 岁 11 个月的在任官员：结算一个月后年龄 70、状态 `Retired`、**官阶保留**；
  自当月起俸禄 = 全俸 × 50%（含难度收益系数与「士出身 ×1.05」），政绩与在职计时不再变化。
- 已致仕者再判 MUST 幂等（不得 50% → 25%）；同时患病/禁考时状态位并存。
- 70 岁但从未有官职者 MUST NOT 产生 `Retired`。
- **同月边界**：待阙期满且满 70 岁 ⇒ 先授官、后致仕，当月按**新授官阶**的半俸计；
  考课成功同月满 70 岁 ⇒ 不晋升（致仕判定先于考课）。
- **锚点三态复核**：L18 / L15 / L1 的月俸在**在任**与**致仕**两态下分别等于
  `72 / 420 / 5100 贯/年 ÷ 12 × 系数` 与上式的一半（SC-004 + FR-024）。

### S6 姓名来源（FR-009、FR-011）

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false `
  -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~NameGeneratorTests"
```

- 两个实现（内置宋风字库 / Bogus `zh_CN` 适配器）都能出非空的中文名，且**同种子 → 同序列**；
- MUST NOT 触碰全局 `Bogus.Randomizer.Seed`（用「两个实例交替取值互不干扰」的断言侧证）；
- 规则层只依赖 `INameGenerator` 抽象（`src/KFL.Rules` 文本中 MUST NOT 出现 Bogus 类型）。

### S7 架构与数值唯一性（SC-009、SC-010）

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false `
  -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~Architecture"
```

- G-01~G-08 全绿：六工程、TFM、依赖方向、平台泄漏、`global.json`；
- G-07 禁用 token 在 `src/KFL.Core`、`src/KFL.Infrastructure`、`src/KFL.Rules` 与四个测试目录零命中
  （含新增的 `Rules/Start`、`Rules/Career` 与 `Infrastructure/NameGeneratorTests.cs`）；
- SC-009：`ConfigLiteralTests` 的数值清单**已增补 0.25 / 0.003 / 60.7 / 62.3**，
  且清单内每个数值在 `src/KFL.Rules/Config/` 之外零命中；金额字面量判据零命中。

### S8 分价位与「不做」的边界（FR-026、Out of Scope）

```powershell
git status --short src/KFL.Presentation src/KFL.App      # 期望：无输出
git diff --stat -- src/KFL.Presentation src/KFL.App      # 期望：无输出
```

- `KFL.Presentation` / `KFL.App` 一行不动；无存档落盘读写、无科举、无惩罚矩阵、无婚育疾病死亡；
- `KFL.Core` 内 MUST NOT 出现考课概率、官职映射、开局资产等规则数值
  （`grep -n "0\.25\|0\.003\|L11\|5100" src/KFL.Core` 期望：仅注释/无命中）。

## 4. 完成判据（§16 阶段验收 + 本特性 Success Criteria）

| 判据 | 怎么验 |
| --- | --- |
| 构建零警告零错误、测试全绿、六个工程、无 `.sln` 共存 | §2 的四条门禁 |
| SC-001 四出身各 8 类事实逐项可断言（4×8 = 32 项） | S1 |
| SC-002 初始资产与 §10.1 逐格一致、商本不错池 | S1（含两条反向验证） |
| SC-003 待阙 100% 落在 6~24、期内俸禄条目为 0、两端点各 1 条断言 | S3 |
| SC-004 四档初始官阶逐档断言、甲第缺失/矛盾被拒 | S3 |
| SC-005 36 月周期（35 月不判）、概率公式、成功 −1、L1 上限、禁升跳过 | S4 |
| SC-006 政绩 +1 / 上限 100 / 三类不增长 | S4 |
| SC-007 致仕四条 + 无官职不致仕 | S5 |
| SC-008 同种子两次开局与两次仕途推进 100% 相同 | S2 |
| SC-009 数值唯一出处（清单已增补） | S7（`~Architecture`） |
| SC-010 非 UI 层 0 次环境直接访问 | S7（G-07） |

## 5. 本阶段**不**验收的项（避免误判为遗漏）

主界面与图表、下月/快进按钮（界面轨 U1/U2/U3）、调试控制台（界面轨 U4）、设置面板（界面轨 U5）、
存档落盘与备份、成就（逻辑轨 ④/⑧）、科举周期与功名产出（逻辑轨 ⑤）、贿赂与惩罚矩阵、
服刑与禁考/禁升计时的建立与递减、释放降级（逻辑轨 ⑥）、婚育、遗传、疾病、寿命与死亡判定、
家主继任、买人口（逻辑轨 ⑦）。这些能力在本阶段**没有任何写入通道**（不可达）；
页面上看不到它们是预期结果（FR-026 已裁决本特性只做规则层）。

## 6. 落地时必须同步提交的工件（章程「需求真源」）

1. `科举浮生录规格书.md` 新增 §17 裁决回写：§3（前史纪年）、§4.1（功名记录增「甲第」）、
   §7.5 与 §8.2（服刑期仕途计时暂停）、§8.2（考课计时口径）。
2. `specs/001-core-skeleton/spec.md`（FR-008 附近）与 `data-model.md`（`DegreeRecord` 小节）
   各补一句：甲第字段由本特性向后兼容扩展（指向 FR-014）。
3. `tests/KFL.Tests/Architecture/ConfigLiteralTests.cs` 的数值清单增补
   **0.25 / 0.003 / 60.7 / 62.3**（详见 [contracts/config-registry.md](./contracts/config-registry.md) §3 条款 3）。
4. 001 的既有断言更新：`ValueObjectTests.GameDate拒绝非法年份`（→ 前史纪年）、
   `DegreeRecord四个成员均为必需`（→ 五个成员、第 5 个可选）、一甲记录构造点补甲第、
   `StatusTimers` 第 4 字段与 `Person` 可写属性由八个变九个。
5. **历史记录类工件保留旧编号，MUST NOT 回改**（001/002 的 `tasks.md`、`implementation-notes.md`、
   既有 `checklists/`），见规格书 §16.2 的迁移口径。
