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
| 规则层依赖 | `KFL.Rules` MUST 只依赖本抽象；`KFL.Rules` 的源码文本 MUST NOT 出现 Bogus 类型（含 `Bogus` 命名空间与类型名，由架构守卫覆盖） |
| 产品默认实现 | `SongStyleNameGenerator(IRandomService)`：**产品默认路径**，内置宋风姓氏/名字库（规格书 §2 的兜底实现），不依赖外部包 |
| Bogus 适配实现 | `BogusNameGenerator(IRandomService)`：Bogus `zh_CN` 适配器；以注入随机取一个整数种子 → `new Randomizer(seed)`（**局部**实例），只在该局部实例上取名。**MUST NOT** 赋值全局 `Bogus.Randomizer.Seed`（全局可变状态 = 隐式全局随机源，章程原则 IV） |
| 随机来源 | 两个实现的全部随机 MUST 经注入的 `IRandomService`（见 §1 消费者列）；姓名来源自身 MUST NOT 引入第二个随机源，以保证同种子 → 同姓名可复现 |

## 4. 三组接缝共同的禁令

1. 非 UI 层 MUST NOT 出现对系统时钟、全局随机、文件系统、网络的直接访问（G-07 静态断言）。
2. 接缝 MUST 通过构造函数注入；MUST NOT 以静态单例、`ServiceLocator` 或属性注入隐藏依赖。
3. 接缝的实现 MUST 位于 `KFL.Infrastructure`（§1~§3 的接口清单所在层），
   MUST NOT 位于 `KFL.Core`（Core 是最底层，不得持有环境边界）。

## 5. 本契约明确不包含

`ISaveService`、`IAchievementStore`、`IEventBus`——规格书 §2 虽把它们列在
`KFL.Infrastructure`，但 001 无消费者（阶段④/逻辑轨 ⑧）。等各自阶段带着真实需求回来定义，
而不是现在猜签名。
