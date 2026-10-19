# 快速验证：家族生命周期（007 · 逻辑轨 ⑦）

**Feature** | **Date**: 2026-10-17 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

> 每个场景都是「一条命令 + 一个可证断言」。全部命令在本仓库根目录执行；
> 受限宿主（DSH 等）必须带 `-m:1 -nodeReuse:false` 与 `-p:_MSTestEnableParentProcessQuery=false`
> （见 `AGENTS.md` 的「沙箱命令执行有误解决方法」）。

## 0. 前置

```powershell
dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false
dotnet test  KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false
dotnet test  KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~FamilyLifecycle|Illness|Inheritance|Marriage|Childbirth|Lifespan|HeadSuccession|Purchase"
```

三条全绿才算本特性可交付；第三条是**本特性专属**的快速回路。

## 场景 1：十步结算且 ⑧ 在 ⑨ 之前

`US1~US5 / FR-006~FR-010 / SC-001`

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~FamilyLifecycleTests"
```

**期望**：`MonthlySettlementEngine` 的步骤清单含十项，`PopulationAdvance.Run` 的八个子步按
⑧-a → ⑧-h 各执行一次；⑧ 内死亡的成员在 ⑨ 的考生集合里**不存在**；
`SettlementResult` 的公开属性恰 **21** 个（`SettlementEngineTests.cs:319-342` 同步改过）。

## 场景 2：连续 120 月逐字段可复现

`FR-048 / SC-002`

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~DeterminismTests|FullyQualifiedName~连续落盘二十四个月"
```

**期望**：同一种子跑两遍，逐月 `SettlementResult` 与 `GameState` 的每个字段 `diff == 0`；
存档往返（`MessagePackSaveFormat` → `SaveStateMapper.ToDomain`）后继续 12 个月仍一致。
**反例检查（2026-10-17 实测结论）**：原稿假设「把 `IllnessSeverityBasisPoints` 换成 `double` 后本用例在
JSON 存档上应变红」。实测**不成立**——`System.Text.Json` 以「最短可往返」形式写出 `double`，任意双精度值
序列化后原样回读（一次性探针验证后已删除）。故整数口径（万分之）的理由是**派生运算的确定性**：
`s` 经 `2 + 8s`、`0.05 + 0.45s`、`体质 × m_q` 等链条反复参与金额与扣减，浮点在长序列下会累积尾差，
而整数磅值使同一 `s` 在任何月份、任何难度下都得到**逐位相同**的扣减与药费。R-03 的结论（用整数）不变，
仅理由更正；本条不构成行为偏离。

## 场景 3：遗传公式逐点

`US2 / FR-024 / SC-003`

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~InheritanceTests"
```

**期望**：`σ = abs(父 − 母) / 2 + 5` 逐点成立；学业均值折半且**共用同一 σ**；
clamp `[0,100]`；体质下限 20；双方缺失时取 `N(60,20)` / `N(30,15)` / `N(85,10)`；
`AttributePolicy.NextNormal` 每次调用**恰消费 2 次** `NextDouble`。

## 场景 4：天命寿数与夭折

`US3 / FR-026 / FR-032 / SC-004`

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~LifespanTests"
```

**期望**：`AgeAt(date) >= Lifespan` 当月即死，**服刑 / 患病 / 外嫁**三态概率均为 1；
夭折只在 `AgeAt < 3` 的生日月判定（出生当月 + 1 岁 + 2 岁，共 3 次），满 3 岁后**零随机消费**；
SC-015 的存活阈值金色表（容差 ±0.5）全绿。

## 场景 5：患病与疗程的边界

`US3 / FR-029 / FR-030 / SC-014`

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~IllnessTests"
```

**期望**：

- 体质 100 ⇒ 0.25%、体质 1 ⇒ 40%，中间值按 `0.25 + 39.75 × (100 − c) / 99` 线性插值；
- 四组 `m_p`/`m_q` 逐点；同一 seed 下四难度结果**完全相同**（不乘负面事件系数）；
- `AgeAt < 3` 不掷判定且该成员当月消费 **0 次**；
- 年龄增益边界：2 岁免病 / 3 岁 +0.60 / 11 岁 +0.60 / 12 岁 +0.30 / 23 岁 +0.30 / 24 岁 +0；
- 宽限月：确诊当月 0 扣体质 / 0 药费 / 0 清除随机；
- 疗程第 1 个月起「先扣体质、再掷清除」；`min(1,…)` 钳到 1 时必愈且不消费随机。

## 场景 6：婚配与 24 格金额表

`US1 / FR-012~FR-021 / SC-007 / SC-013`

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~MarriageTests"
```

**期望**：

- 3 阶层 × 娶入/嫁出 × 4 支出系数（0.6 / 1.0 / 1.1 / 1.3）**24 格**逐格等于契约表；
- 托媒 3 贯 × 系数、50% 单次判定、失败**不退费**、资金不足 ⇒ 拒绝且**零随机**；
- 提亲每月至多 1 次；四难度概率 2.5 / 5 / 6.5 / 7%；
- 捉婿 `P = 0` 时概率恰 `30% × 系数`、金额落 500~1000；`P` 增大 ⇒ 概率单调降、金额单调升；
- 功名分档在 `P = 0.99 / 1.00 / 2.99 / 3.00` 取 白身 / 举人 / 举人 / 贡士；
- 拒绝提议 ⇒ 0 支付、0 状态变更、逐字段与拒绝前完全相同，且名额已消耗。

## 场景 7：家主继任四档与空缺

`US4 / FR-036 / FR-037 / SC-005`

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~HeadSuccessionTests"
```

**期望**：四档各至少一条断言（①在册男性子女 ②已亡儿子的在册男性后代（代际就近 + 同代最长）
③亲兄弟 ④在册男性中年龄最大且子代最多）；同月多人死亡时**先归档完再继任一次**；
无在册男性 ⇒ `HeadId == null` 且 `HeadBecameVacant == true`、**不判绝嗣**；
任意月份结算结束（含 001 `SaveStateValidator` 的第 4 条）`HeadId` 永不指向已亡/外嫁。

## 场景 8：收养与存档往返

`US5 / FR-039~FR-041 / FR-047 / SC-006 / SC-010`

```powershell
dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~PurchaseTests|FullyQualifiedName~SaveFailureTests"
```

**期望**：

- 本年第 1/2/3 名 = 10 / 20 / 30 贯；跨年（1 月）重置回 10 贯；
- 年龄恒落 `[8,16]`（含端点，`Next(8, 17)`）、性别两值都可能、`FatherId`/`MotherId` 均 `null`、
  辈分 = 家主辈分 + 1、`IsOutsider == true`；
- `HeadId == null` ⇒ `HeadVacant`，**零随机**；
- 存档 `Version == 4`；`Version == 3` 的旧档被 `UnsupportedVersion` 拒绝；
  `PersonDto` 29 键、`FamilyDto` 9 键、`StatusTimersDto` 6 键往返无损。

## 场景 9：门禁四条（交付前必跑）

`FR-051 / SC-008 / SC-011`

```powershell
dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false
dotnet test  KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false
dotnet sln   KejuFuShengLu.slnx list
Get-ChildItem -Recurse -Filter *.sln | Where-Object { $_.FullName -notmatch '\\\.git\\' }
```

**期望**：0 警告 0 错误；全绿（含 G-01~G-08）；`dotnet sln list` 恰六个工程；最后一条**无输出**。

> **平台口径（非 Windows 宿主）**：第二条「全绿」以 **Windows** 宿主为权威。全量套件中有 8 例
> （`SaveServiceTests.原子写的临时名不随档名变长` + `Architecture/` 守卫 7 例）的期望值内建了
> Windows 宿主语义——存档路径的 `MAX_PATH` 259 字符预算、`net10.0-windows` 目标框架判定、
> 大小写不敏感的文件系统，**存档路径类用例以 Windows 为准**。实测它们在 Linux 等宿主上
> **于 `main` 上同样失败**（PR #15 审核结论第一节），属既有环境性差异，与本特性无关；
> 非 Windows 宿主上核验本特性时，以场景 1~8 的专属筛选与「本特性新增用例零失败」为准。

**回写核对（SC-011）**：`plan.md` 的「回写义务清单」10 项逐项在仓库里可检索到；
`specs/001-core-skeleton/tasks.md:88`、`specs/002-monthly-settlement-economy/tasks.md:83`/`:279`
的「阶段⑧」残句**未被回改**（历史记录类工件）。
