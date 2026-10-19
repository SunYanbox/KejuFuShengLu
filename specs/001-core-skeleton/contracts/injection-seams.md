# 契约二：注入接缝（随机来源与游戏时间来源）

**Feature**: `001-core-skeleton` | **Date**: 2026-10-05

001 只交付两组接缝。它们是「规则层可脱离 UI 被单测、可重放」这一条章程要求的物理落点：
此后任何需要随机或需要「现在」的规则，都从这里取。

## 1. `IRandomService`（`KFL.Infrastructure`）

```csharp
public interface IRandomService
{
    double NextDouble();                              // [0.0, 1.0)
    int Next(int minInclusive, int maxExclusive);     // [min, max)
    void NextBytes(Span<byte> destination);           // 填满 destination
}
```

| 条款 | 要求 |
| --- | --- |
| 确定性 | 以同一种子构造两次，**逐位相同**的调用序列 MUST 得到逐位相同的结果 |
| 范围 | `Next` 对 `maxExclusive <= minInclusive` MUST 抛 `ArgumentOutOfRangeException`；返回值 MUST 落在 `[minInclusive, maxExclusive)` |
| 填充 | `NextBytes` MUST 写满整个 `destination`，不得部分填充 |
| 唯一实现 | 001 只交付 `SeededRandomService(int seed)`；MUST NOT 使用全局 `Random`、`Random.Shared`、`Guid.NewGuid()` 作为隐式源 |
| 消费者 | `GameState` 的唯一标识（16 字节 → `Guid`），**经 `KFL.Infrastructure/Services/GameStateFactory`**——`KFL.Core` 看不到本接缝，转换不能在 Core 内完成，见 R-03；**姓名来源**（003 追加：`INameGenerator` 取姓氏与名字，见 §3）——姓氏池/字库的随机取一 MUST 从本接缝取，MUST NOT 引入第二个随机源 |

**为什么用同一个接口产生唯一标识**：章程原则 IV 禁止隐式 `Guid.NewGuid()`；复用本接口
使「同种子 → 同 UUID」可复现，并避免为 001 预置第三个接口。风险（调试控制台重置种子后
新建存档可能复现同一 UUID）已在 R-03 登记，无 001 触发路径。

## 2. `IGameClock`（`KFL.Infrastructure`）

```csharp
public interface IGameClock
{
    GameDate Current { get; }   // 当前游戏年月，不是墙钟时间
}
```

| 条款 | 要求 |
| --- | --- |
| 语义 | 返回的是**推演出的游戏年月**（§3：1 回合 = 1 游戏月），不是系统时钟 |
| 实现 | 001 只交付以 `GameState.CurrentDate` 为后端的实现；MUST NOT 读取 `DateTime.Now` |
| 注入 | 规则层需要「现在」时 MUST 从注入的 `IGameClock` 取；MUST NOT 直接从 `GameState` 字段取，也 MUST NOT 用静态全局。月度结算引擎（002）按**构造函数**注入 `IGameClock` 与 `IRandomService`；MUST NOT 把 `GameState.CurrentDate` 直接当作「现在」（该值由 `IGameClock` 承载），也 MUST NOT 用静态全局 |
| 测试替身 | 测试用固定日期的替身驱动，断言「同输入 → 同结果」 |

**为什么不交付系统时钟抽象**：001 内不存在墙钟时间的消费者（存档时间戳属阶段④）。
按章程「复杂度 MUST 被论证」，墙钟接缝等其首次出现时再引入。
**已由 004 复核为「无该需求」**——004 明确不落盘任何写入时间戳（[004 `contracts/save-service.md` §1 条款 3](../004-save-system/contracts/save-service.md)），故墙钟接缝至今仍未引入，本结论维持。

## 3. `INameGenerator`（`KFL.Infrastructure`，003 追加）

001 只交付前两组接缝；姓名来源由 `003-opening-assets-officialdom` 首次消费（该特性 FR-009），
在此登记为跨阶段接缝。

```csharp
public interface INameGenerator
{
    string NextSurname();                  // 姓氏池随机取一（§12.4「随机姓氏」）
    string NextGivenName(Gender gender);   // 只取名；姓氏由家族姓氏决定（§12.4）
}
```

| 条款 | 要求 |
| --- | --- |
| 位置 | 接口位于 `KFL.Infrastructure/Abstractions/INameGenerator.cs`；两个实现位于 `KFL.Infrastructure/Services/`（与 §4 条款 3 同口径，不得落在 `KFL.Core`） |
| 规则层依赖 | `KFL.Rules` MUST 只依赖本抽象；`KFL.Rules` 的源码文本 MUST NOT 出现 Bogus 类型（含 `Bogus` 命名空间与类型名）。**该 MUST 由 `KFL.Rules.csproj` 的依赖边保证**——该工程 MUST NOT 引用 `Bogus` 包；G-05 只校验 `ProjectReference`、不读 `PackageReference`（G-07 的 14 个禁用 token 里没有 `Bogus`，契约一 §2.1 的清单亦 MUST NOT 增删改），故此处**没有**源码文本层面的静态断言 |
| 产品默认实现 | `SongStyleNameGenerator(IRandomService)`：**产品默认路径**，内置宋风姓氏/名字库（规格书 §2 的产品默认实现；§17 裁决回写 2026-10-11），不依赖外部包 |
| Bogus 适配实现 | `BogusNameGenerator(IRandomService)`：Bogus `zh_CN` 适配器（规格书 §2 的**可选**实现）；以注入随机取一个整数种子 → `new Randomizer(seed)`（**局部**实例），只在该局部实例上取名。**MUST NOT** 赋值全局 `Bogus.Randomizer.Seed`（全局可变状态 = 隐式全局随机源，章程原则 IV） |
| 随机来源 | 两个实现的全部随机 MUST 经注入的 `IRandomService`（见 §1 消费者列）；姓名来源自身 MUST NOT 引入第二个随机源，以保证同种子 → 同姓名可复现 |

## 4. 三组接缝共同的禁令

1. 非 UI 层 MUST NOT 出现对系统时钟、全局随机、文件系统、网络的直接访问（G-07 静态断言）。
2. 接缝 MUST 通过构造函数注入；MUST NOT 以静态单例、`ServiceLocator` 或属性注入隐藏依赖。
3. 接缝的实现 MUST 位于 `KFL.Infrastructure`（§1~§3 的接口清单所在层），
   MUST NOT 位于 `KFL.Core`（Core 是最底层，不得持有环境边界）。
4. **程序集级接缝（004 追加）**：`KFL.Core/AssemblyInfo.cs` 的
   `[assembly: InternalsVisibleTo("KFL.Infrastructure")]`——它不是注入接缝（不注入任何对象、
   不由构造函数传入），而是把 Core 的既有 `internal` 通道开给**唯一**接收方。
   **目的**：存档复原必须**精确落值**，须经 `Person.SetGeneration` / `SetSpouse` /
   `AddFormerSpouse`（001 起即为 `internal`，为 `Family` 而设）与 004 新增的
   `Family.CaptureOutsiderBookkeeping` / `RestoreOutsiderBookkeeping`（簿记进出，见
   [001 `data-model.md` §2.2/§2.4](../data-model.md)）、`FamilyEconomy.RestoreFamine`。
   **范围**：接收方**仅** `KFL.Infrastructure`（且声明 `MUST NOT` 同时放行任何其它程序集）。
   **禁令**：MUST NOT 扩大接收方（特别是 `tests/KFL.Tests`——测试经公开 API 与 `ISaveService`
   往返观察这些面，既不直连 Core 的 `internal`、也不用反射）；MUST NOT 因本接缝新增任何
   **公开**写入通道（`FamineState.Stage` / `ElapsedMonths` 的公开面仍是只读，见
   [002 `data-model.md` §3.5](../002-monthly-settlement-economy/data-model.md)）。

## 5. 本契约明确不包含

`ISaveService`、`IAchievementStore`、`IEventBus`——规格书 §2 虽把它们列在
`KFL.Infrastructure`，但 001 无消费者（阶段④/逻辑轨 ⑧）。等各自阶段带着真实需求回来定义，
而不是现在猜签名。

**`ISaveService` 已由 004 定义**（2026-10-11）——签名与失败矩阵见
[004 `contracts/save-service.md`](../004-save-system/contracts/save-service.md)。

**`IAchievementStore` 已由 008 定义**（2026-10-18）——签名、失败矩阵与落盘点见
[008 `contracts/achievement-store.md` §1](../008-achievements-extinction/contracts/achievement-store.md)；
`IEventBus` 仍未定义（逻辑轨 ⑧ 等后续阶段）。

**007 未新增接缝**（2026-10-17）：007（家族生命周期，`IllnessPolicy` / 婚姻市场 / 收养 /
疾病治疗抉择）**MUST NOT 自建 `IEventBus` 或 `IAchievementStore`**（007 FR-045）。决策类事件
（提亲应答、生育决策、疾病治疗抉择）对 U1 快进栈的「暂停信号」与玩家可见性，一律经
**结算快照** `SettlementResult.Lifecycle`（`LifecycleSnapshot` 的十段事实）与
**纯函数查询面** `LifecycleQueries` 表达：暂停信号 = **上一月**产出的
`LifecycleSnapshot.Illnesses` 中 `Contracted == true` 的成员（007 FR-045、
[007 data-model §3.3/§3.4](../007-family-lifecycle/data-model.md)）。

**008 首增一项接缝**（2026-10-18）：接缝集合自 004 起首次**新增**——
`IGameClock` / `IRandomService` / `ISaveService` / `INameGenerator`（003 追加）四者之外，
008（成就与绝嗣结局）定义 `IAchievementStore`（跨档**全局**档案的读写接缝；`IEventBus`
仍**未定义**且 MUST NOT 由 008 自建）。签名与失败矩阵见
[008 `contracts/achievement-store.md` §1](../008-achievements-extinction/contracts/achievement-store.md)；
注入点 = `TurnRunner` 构造函数**可选尾参** `IAchievementStore? achievementStore = null`
（`null` ⇒ 跳过回合末 ⑤ 全局落盘，判定与存档内队列照常运行；契约二十三「条款 20」）。
007 的「零新增」结论**不变**（它描述 007 当时的阶段事实）。
