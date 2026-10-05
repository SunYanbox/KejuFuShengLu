using KFL.Core.ValueObjects;

namespace KFL.Infrastructure.Abstractions;

/// <summary>
/// 游戏时间来源接缝（契约二 §2）。签名**逐字**取自契约二。
/// </summary>
/// <remarks>
/// 语义是**推演出的游戏年月**（规格书 §3：1 回合 = 1 游戏月），不是墙钟时间。
/// 001 内**不引入**系统时钟抽象——它没有消费者（research R-04）。
/// </remarks>
public interface IGameClock
{
    /// <summary>当前游戏年月。</summary>
    GameDate Current { get; }
}
