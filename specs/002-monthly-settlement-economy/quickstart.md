# 快速验收：月度结算与经济引擎

**Feature**: `002-monthly-settlement-economy` | **Date**: 2026-10-06

本文件是**可运行**的人工验收流程：每条场景都能用一条命令跑出来，且不依赖界面、存档或网络。
断言细节见 [contracts/](./contracts/)，字段与不变量见 [data-model.md](./data-model.md)。

## 1. 前置条件

- .NET 10 SDK（仓库根 `global.json` 只锁 `10.0` 版本带，任意 `10.0.x` 均可）。
- 仓库根目录执行；不新增工程，仍为六个。
- 受限宿主（Agent 沙箱）里的两个已知环境坑与处置见 `AGENTS.md`：并行 MSBuild 节点 IPC 被拦
  （加 `-m:1 -nodeReuse:false`）、testhost 父进程看门狗被拒（加
  `-p:_MSTestEnableParentProcessQuery=false`，**不要**改文件——该属性在
  `tests/KFL.Tests/KFL.Tests.csproj` 里默认注释，MUST NOT 以启用状态提交）。

## 2. 门禁（三条，全过才算完成）

```powershell
dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false                              # 期望：0 警告 0 错误
dotnet test  KejuFuShengLu.slnx -m:1 -nodeReuse:false `
  -p:_MSTestEnableParentProcessQuery=false                                        # 期望：全绿，0 skipped
dotnet sln   KejuFuShengLu.slnx list                                              # 期望：恰好六个工程
```

> 第三条不能省：`.slnx` 解析器**不认识 `<TestProject>` 且不报错**，测试工程可能悄悄掉出
> 解决方案，「全绿」就变成「一个都没跑」（001 R-02 的实测陷阱）。

`dotnet test` 后若 `bin\` 下的 DLL 删不掉，是上一次被强杀留下的孤儿 `testhost` 占用
（`Get-Process -Name testhost | Stop-Process -Force` 后再删）——**不要去改文件 ACL**。

## 3. 场景化验收

每条场景对应一个测试文件（`tests/KFL.Tests/Rules/`），也可用 `--filter` 单独跑。

### S1 生活费按规格书公式进出（US1、SC-001）

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false `
  -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~LivingCostTests"
```

- **夹具**：多代同堂（含儿童 / 青年 / 成人 / 老人）+ 田 + 宅 + 铺面 + 一名在职位成员，固定种子。
- **断言**：逐年龄档日耗与人数 → `30 × 米价系数 × 难度支出系数 × 农出身独立乘区 × 一般乘区`；
  四个乘区各自可分别读出；生日当月转档（12 岁男 / 14 岁女）。
- **期望**：普通档、非农、无救济的儿童 = `5 × 30 × 米价系数 × 难度支出系数 × 1.00 × 0.50`；
  同条件农出身儿童 = 上式再乘 `0.80`；救济期再按一般乘区 `1 − 0.5 − 0.2 = 0.3`。

### S2 收入分项（US1、FR-006~FR-012）

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false `
  -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~IncomeTests"
```

- 自耕 40 亩 = `40 × 0.5 ÷ 12 = 1.667 贯/月`（**不是** 2 贯，R-18）；
- 第 21 亩起转田租口径；每成人 20 亩上限与「按耕作效率降序分配」（E-11）；
- 无田时务农 2 贯/月/计口成人，与自耕互斥；做工需指派、城市宅加成 = `min(做工人数, 城市宅数)`；
- 商本 99 贯无收益 / 100 贯有收益（边界各一条）；商出身 ×1.1；
- 俸禄表 18 级逐级 + 锚点 72 / 420 / 5100（SC-004）；
- **储蓄利息不乘难度收益系数**（负例断言）。

### S3 贷款：先本后息、20/40/80、12 月计息不滚本金（US2、SC-002、§16 必测）

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false `
  -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~LoanTests"
```

- 划扣 = `min(净利润 × 比例, 本金 + 欠息)`，先冲本金、不足再冲欠息；
- 三档比例各一条（仕 20% / 工农 40% / 商 80%）；净利润 ≤ 0 不划扣且不重置计时；
- 第 12 个月按**当时剩余本金**计息一次并只增加欠息（本金 MUST NOT 因计息增加），
  且**先计息后划扣**（E-05）；本金清零后转冲欠息；皆清即结清。

### S4 储蓄生息与工 bonus（US3）

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false `
  -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~SavingsTests"
```

- 1 月 roll 出 `0.5%~2.4%` 且当年内不变，次年重 roll；
- 12 月末利息 = 储蓄本金 × 当年利率并并入本金（复利）；
- 工出身：`(现金 + 储蓄 + 田宅铺市值 − 本金 − 欠息) × 6%`，基数为 0 或负不发、非工出身不发。

### S5 饥馑四阶段流转（US4、SC-003、§16 必测）

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false `
  -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~FamineTimelineTests"
```

- 逐月推进断言：`None → Famine`（3 个月）`→ Relief`（12 个月）`→ Severe`；
- 付不起时生活费条目 = `min(应付额, 现金 + 储蓄)`、现金与储蓄清零、**不出现负余额**（E-06；可付额口径，见 R-05）；
- 任一月付得起 → 全部解除且计时归零（MUST NOT 有残留）；
- 救济期的 −20% 体现在条目金额上，而不是事后修正。

### S6 每一文钱都有出处（US5、SC-005）

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false `
  -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~LedgerTests"
```

- 对任意单月与任意连续区间：`Σ(资金类条目) == 资金池期末 − 期初`（差额为 0）；
- 家族维度与角色维度聚合出的是**同一批条目**；已归档成员的历史条目仍可读；
- 铺面租、储蓄利息、工 bonus 归为**家族级**条目，MUST NOT 被塞给某个成员或丢失；
- 事件类条目（饥馑迁移、贷款计息）不参与求和，但 MUST 存在且可读。

### S7 确定性与环境隔离（SC-006、SC-007）

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false `
  -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~DeterminismTests|FullyQualifiedName~Architecture"
```

- 同种子 + 同初始状态 + 同一月份序列，两次运行的 `SettlementResult`、资金池、账本条目
  逐位相同（随机消费次序见契约「月度结算」§4）；
- G-07 禁用 token 扫描覆盖 `tests/KFL.Tests/Rules/`（R-14 的扩容已落地）；
- G-01~G-08 全绿（工程、TFM、依赖方向、平台泄漏、global.json）。

### S8 计口与非计口（FR-029、FR-030、SC-009）

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false `
  -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~LivingCostTests|FullyQualifiedName~IncomeTests"
```

- 同夹具加入一名**服刑**成员与一名**外嫁**成员：两人对生活费合计与各收入分项的贡献为 0；
- 但两人的档案、族谱位置与既往账目仍 100% 可读；
- **待阙**成员照常计入生活费（**在册 ≠ 计口**）。

## 4. 完成判据（§16 阶段验收 + 本阶段 Success Criteria）

| 判据 | 怎么验 |
| --- | --- |
| 构建零警告零错误、测试全绿、`.slnx` 存在且无 `.sln` 共存 | §2 的三条门禁 |
| SC-001 一次结算可在单条测试命令内逐项断言 | S1~S3、S6 |
| SC-002 贷款三规则独立断言、三档比例各 ≥1 条 | S3 |
| SC-003 饥馑 4/4 转移 + 解除后计时归零 | S5 |
| SC-004 俸禄 18 级锚点误差为 0 | S2 |
| SC-005 条目合计与资金池变动完全相等 | S6 |
| SC-006 同种子 100% 相同 | S7 |
| SC-007 非 UI 层 0 次环境直接访问 | S7（G-07） |
| SC-008 数值唯一出处 | `contracts/config-registry.md` §5 的扫描测试 |
| SC-009 服刑与外嫁贡献为 0、档案仍可读 | S8 |

## 5. 本阶段**不**验收的项（避免误判为遗漏）

主界面与图表（阶段③/⑨/⑩）、存档落盘与备份（阶段④）、科举周期（阶段⑤）、
罚金/惩罚矩阵/连坐/服刑计时（阶段⑥）、调试控制台（阶段⑦）、官职与婚育、疾病与死亡、
随机事件（阶段⑧）、成就与绝嗣结局（阶段⑪）、难度设置面板（阶段⑫）。
这些能力在本阶段**没有任何写入通道**（不可达），页面上看不到它们是预期结果。
