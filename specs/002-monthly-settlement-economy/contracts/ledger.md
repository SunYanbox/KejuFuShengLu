# 契约四：流水账（002 与其后所有阶段的构建期契约）

**Feature**: `002-monthly-settlement-economy` | **Date**: 2026-10-06

流水账是 §12.3 全部按月统计项的**唯一数据来源**：家族总收支、收益来源明细、支出明细、
按角色的每月与累计收支，全部由同一批条目聚合得出，**不存第二份汇总值**（001 R-16）。

## 1. 条目形状与语义

```csharp
readonly record struct LedgerEntry(GameDate Date, PersonId? PersonId, LedgerCategory Category, Money Amount);
```

| 字段 | 语义 |
| --- | --- |
| `Date` | 归属年月（结算月份，即 `SettlementResult.Month`） |
| `PersonId` | 归属角色；`null` = **家族级**（铺面租、储蓄利息、工 bonus、买人口、田租） |
| `Category` | 类别；**类别全集与二分类的唯一真源** = `KFL.Core/Enums/LedgerCategoryMetadata.cs`（可执行形式）与 [data-model.md](../data-model.md) §2.1（文档形式），本契约不复制表格 |
| `Amount` | **资金类**：本次事件对资金池的变动额（正 = 流入、负 = 流出），`Amount == 0` 的资金类条目不合法；**事件类**：该事件自身的金额语义（阶段迁移为 0，计息为利息额） |

**MUST NOT** 把 `Amount` 解释成余额：条目是**变动量**，余额的唯一真源是 `FamilyEconomy.Treasury`。

## 2. 二分类（E-08）

| 种类 | 定义 | 是否参与 SC-005 求和 | 举例 |
| --- | --- | --- | --- |
| **资金类**（`LedgerEntryKind.Treasury`） | 引起资金池变动的收支 | ✅ | 生活费、各来源收入、储蓄利息、偿还本金/欠息、资产买卖 |
| **事件类**（`LedgerEntryKind.Event`） | 不引起资金池变动的阶段/负债事件 | ❌ | 饥馑四类阶段迁移、贷款计息入欠息 |

**为什么必须二分类**：spec 自身要求「进入饥馑」留下条目（US4 AS1，该事件不涉及资金），
同时要求「条目金额合计 = 资金池变动额」（SC-005）。二者只有显式分类才能同时成立。

## 3. 唯一写入通道

| 入口 | 职责 |
| --- | --- |
| `FamilyEconomy.Apply(category, personId, amount, date)` | 追加条目**并在同一次调用内**按 `LedgerCategoryMetadata` 改动资金池（正额入 `CreditTarget`；负额先现金后储蓄）。资金不足 MUST 抛异常 |
| `FamilyEconomy.RepayLoan(amount, date)` | 扣付资金池 + `Loan` 先本后息 + 落 1~2 条资金类条目 |
| `FamilyEconomy.AccrueLoanInterest(interest, date)` | `Loan.AccrueInterest` + 落 1 条事件类条目 |
| `FamilyEconomy.EnterFamineStage(stage, amount, date)` | 落对应的饥馑阶段迁移事件条目 |
| `Ledger.Append(entry)` | **仅供上述四者使用**；单独调用它会造成「条目与资金池不一致」 |

**条款**
1. 非 UI 层 MUST NOT 直接改 `Treasury.Cash` / `Savings` / `MerchantCapital`：这三个成员
   只有内部转账方法（不落条目）与聚合的资金流动方法（落条目）两条路径。
2. **「只动资金池而不落条目」在类型层面不可表达**——这是 SC-005 的物理落点。
3. 内部转账（现金 ↔ 储蓄、商本注入/撤回）MUST NOT 落条目：资金池口径不变，落条目会让
   求和口径与池变动不等（R-05）。

## 4. 不变量

| # | 不变量 | 验证方式 |
| --- | --- | --- |
| 1 | `Ledger.TreasuryDeltaIn(from, to) == TreasuryPoolAfter − TreasuryPoolBefore`（任意连续月份区间，差额为 0） | **这就是 SC-005**；在任意单月与任意连续区间上断言 |
| 2 | 每一次资金池变动都有对应条目 | 单次结算内：`SettlementResult.Entries` 与账本新增条目**逐条相同** |
| 3 | 追加式：既有条目 MUST NOT 被改写或删除 | 追加前后逐条比对（引用/值相等） |
| 4 | 资金类条目的 `Amount` 非 0；资金池任一时点 `>= 0` | 边界夹具（付不起、资产卖光） |
| 5 | 一切资金流动 MUST 落条目（FR-021） | 由 §3 的唯一通道保证（不可达的旁路） |
| 6 | MUST NOT 存月度汇总值 | 源码级断言：不存在「月度汇总」类型/字段；聚合每次都从条目算 |

## 5. 两个聚合维度

| 维度 | 查询 | 覆盖 |
| --- | --- | --- |
| 家族级 | `TotalsByCategory(from, to)`、`TreasuryDeltaIn(from, to)` | §12.3「家族收益、支出总额 + 收益来源明细 + 支出明细」 |
| 角色级 | `TotalsByPerson(id, from, to)`、`MonthlyByPerson(id, from, to)` | §12.3「按角色的每月收支与累计收支」，**含已归档成员**（FR-022、SC-009） |

**条款**：两个维度 MUST 基于**同一批条目**（US5 AS4）；家族维度的合计 MUST 等于各角色维度
合计与家族级条目之和，MUST NOT 依赖任何预先存储的月度汇总值。

## 6. 性能与索引（如实记录）

聚合是线性扫描。条目数按月线性增长（每月约 10~40 条；001 R-16 估计 100 年数万条）。
本阶段**不建索引、不分块**——阶段④ 落盘时实测后再决定（001 R-16 已登记该未决事项）。

## 7. 本契约明确不包含

CSV 导出、图表、统计页的筛选器（界面轨 U3）、存档序列化格式（阶段④）、买人口的支出类别
（逻辑轨 ⑦ 新增类别时须同时补 `LedgerCategoryMetadata` 的两种查询口径）。
