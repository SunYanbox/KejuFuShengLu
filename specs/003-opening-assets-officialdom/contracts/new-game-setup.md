# 契约六：新建存档（开局）入口

**Feature**: `003-opening-assets-officialdom` | **Date**: 2026-10-08

本契约固定**新建存档**这一时点的接口形状、四出身表、随机消费次序与「不落账本条目」的口径。
它是 US1/SC-001/SC-002/SC-008 的可验收边界。数值出处见
[data-model.md](../data-model.md) §3.1 与 [contracts/config-registry.md](./config-registry.md)。

## 1. 入口形状

```csharp
// KFL.Rules/Start/NewGameSetup
public readonly record struct NewGameRequest(
    Origin Origin, Difficulty Difficulty, string? Surname, GameDate StartDate);

public sealed class NewGameSetupResult     // 薄视图：只持有 State
{
    public GameState State { get; }
    public PersonId? HeadId { get; }
    public GameDate StartDate { get; }
    public IReadOnlyCollection<Person> Members { get; }
}

public static NewGameSetupResult Create(
    NewGameRequest request, IRandomService random, INameGenerator names);
```

| 条款 | 要求 |
| --- | --- |
| 1 | `StartDate` MUST 为 **1 年 1 月**；本特性 MUST NOT 接受其它起始年月（推演从 1 年 1 月起，§3） |
| 2 | `Surname` 为 `null` 时 MUST 经 `INameGenerator.NextSurname()` 取得随机姓氏；非空时 MUST 原样使用（§12.4） |
| 3 | 全部随机 MUST 来自入参 `IRandomService`；MUST NOT 使用全局随机源、`Guid.NewGuid()` 或 Bogus 的全局种子（章程原则 IV） |
| 4 | `NewGameSetupResult` MUST NOT 另存成员集合/余额副本——`State` 是唯一真源，其余成员均为派生只读属性 |
| 5 | `GameState` 的唯一标识 MUST 经 `KFL.Infrastructure/Services/GameStateFactory` 取得（16 字节 → `Guid`），MUST NOT 在规则层重写该转换 |
| 6 | 入参非法（姓氏为空白串、起始年月非 1 年 1 月）MUST 抛异常且**不产生任何部分状态** |

## 2. 编排次序（可逐条断言）

1. 姓氏（§1 条款 2）。
2. `new Family(surname)` → 依次生成并加入 **家主 → 配偶 → 孩子 1..n**：
   - 家主：`AddFoundingMember`（无父母、辈分 0、性别男）；
   - 配偶：`AddOutsider`（无父母、辈分 0、性别女）；
   - 孩子：`AddChild`（辈分 1、父母引用**同指二人**、性别 50/50）。
3. `Family.Marry(家主, 配偶)`；`Family.SetHead(家主)`。
4. 「士」出身：向家主的功名变迁历史**追加**一条
   `(DegreeLevel.JuRen, placement: null, changedAt: StartDate, cause: DegreeChangeCause.Initial, imperialClass: null)`。
5. 组装资金与资产：`Treasury(cash, savings: 0, merchantCapital)` + `Holdings{…}` + 空 `Ledger`；
   `FamilyEconomy(LivingCostTable.InitialStandard, new GrainPriceIndex(GrainPricePolicy.Initial), …)`。
6. `GameStateFactory.Create(StartDate, difficulty, origin, family, economy)`。

| 条款 | 要求 |
| --- | --- |
| 1 | 步骤 2 的**成员顺序** MUST 为「家主 → 配偶 → 孩子（按生成顺序）」，且 MUST 影响随机消费次序 |
| 2 | 步骤 5 的资产 MUST **只**经构造入参写入：现金 → 现金池、商本 → 商本池、田/宅 → `Holdings`；**MUST NOT 错池**（商本 MUST NOT 记作现金） |
| 3 | 步骤 6 之后 MUST 满足 `economy.Ledger.Entries` **为空**（开局资产是初始余额，不是一笔收入） |
| 4 | 士出身 MUST NOT 置 `Family.HasShiStatus`；任何出身下它 MUST 为 `false`（§10.2、FR-008） |
| 5 | 三处初值 MUST 取自既有单点：生活费档位 ← `LivingCostTable.InitialStandard`；米价系数 ← `GrainPricePolicy.Initial`；资产单价 MUST NOT 在本特性复制 |

## 3. 随机消费次序（同种子 → 逐字段相同）

次序**固定**为：

```text
① 姓氏（仅当 Surname == null）
② 家主：   年龄 → 天赋(农) → 天赋(商) → 天赋(仕) → 天赋(工) → 学业 → 体质 → 寿数 → 姓名
③ 配偶：   年龄 → 天赋×4 → 学业(N(30,15)) → 体质(N(85,10)) → 寿数 → 姓名
④ 孩子 i： 性别 → 年龄 → 天赋×4 → 学业(0，不掷骰) → 体质(N(90~100 整数均匀)) → 寿数 → 姓名
⑤ 存档唯一标识（NextBytes(16)）
```

| 条款 | 要求 |
| --- | --- |
| 1 | 「天赋×4」的次序 MUST 为 **农、商、仕、工**（与 `TalentSet` 的参数次序一致） |
| 2 | 正态取样 MUST 恰好消耗 **2 次** `NextDouble()`（Box–Muller，无静态缓存，R-08） |
| 3 | 区间型取值（`±5`、`0~8`、`80~100`、`90~100`、`10~30`）MUST 为**整数均匀、含两端点**，实现取 `Next(min, max + 1)`（`Next` 的上界是开区间） |
| 4 | 士出身家主的学业 MUST 为常量 **60**（不掷骰）；其余出身取 `10~30` 均匀 |
| 5 | 孩子的学业 MUST 为常量 **0**（不掷骰）；孩子的天赋 MUST 取 §4.2「开局成员」口径 `N(60,20)`，MUST NOT 从父母推导（R-19#1） |
| 6 | 正态结果 MUST **四舍五入取整**后 clamp 到 0~100；**天命寿数是唯一例外——只 clamp 下界 0、不设上限**（FR-006） |
| 7 | 相同 `request` + 相同种子 MUST 产生逐字段相同的结果（成员数、性别、年龄、姓名、四项天赋、学业、体质、寿数、辈分、婚姻与父母引用、资产与资金池、功名记录） |

> **注（`INameGenerator` 实现自身的随机消耗）**：上表的次序**只描述 `Create` 内部的消费**。
> 实现可自带消耗——产品默认的 `SongStyleNameGenerator` 构造期不消耗随机；
> `BogusNameGenerator(IRandomService)` 在**构造期**取一次 `Next(int.MinValue, int.MaxValue)` 作局部种子
> （`new Randomizer(seed)`，不碰全局 `Randomizer.Seed`）。若调用方把同一个 `IRandomService` 既
> 交给它、又交给 `Create`，则 `Create` 的消费从该流的**下一个槽位**开始——**仍然确定**
> （同种子 → 同结果），但不再是「流的第 1 次调用」。故两者 MUST **各自持有独立的随机来源**，
> 或在构造姓名来源之后再开始记录契约六的次序。

## 4. 四出身表（逐格断言对象）

| 出身 | 现金（贯） | 田（亩） | 宅 | 商本（贯） | 成员 | 家主年龄 | 家主学业 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 农 | 80 | 40 | 农村宅 ×1 | 0 | 夫妇 + 2 孩 | 28±5 | 10~30 |
| 工 | 80 | 0 | 农村宅 ×1 | 0 | 夫妇 + 1 孩 | 28±5 | 10~30 |
| 商 | 500 | 0 | 城市宅 ×1 | 300 | 夫妇 + 2 孩 | 28±5 | 10~30 |
| 士 | 200 | 0 | 农村宅 ×1（含藏书） | 0 | 夫妇 + 1 子 | **30±5** | **60** |

| 条款 | 要求 |
| --- | --- |
| 1 | 「农舍」与「农村宅」MUST 按同一价目处理，即 `RuralHouses == 1`；MUST NOT 出现第三种宅类（§5.3） |
| 2 | 配偶年龄恒 **25±5**、孩子年龄恒 **0~8**（四个出身一致） |
| 3 | 家主体质恒 `80~100`；孩子体质恒 `90~100`；四者均**整数均匀、含端点** |
| 4 | 商出身的 **300 贯商本** MUST 只影响商本池：它 MUST NOT 进入「付不起生活费」的可付额，也 MUST NOT 进入工出身 bonus 的资产基数（002 两条既有口径的反向验证） |
| 5 | 任一出身下 `Family.HasShiStatus == false`（R-14、FR-008） |

## 5. 本契约明确不包含

界面与存档落盘（界面轨 U1 / 逻辑轨 ④）、随机事件与属性成长（逻辑轨 ⑦）、
孩子的天赋遗传与新生儿口径（逻辑轨 ⑦）、「仕」身份的授予（逻辑轨 ⑤）、
官名（规格书未定义）、任何数值的第二出处（数值单点在 [contracts/config-registry.md](./config-registry.md)）。
