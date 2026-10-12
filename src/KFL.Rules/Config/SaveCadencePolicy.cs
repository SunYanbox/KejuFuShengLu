using KFL.Core.ValueObjects;

namespace KFL.Rules.Config;

/// <summary>
/// 存档节奏政策（004-save-system；契约十一 §1/§4）——「什么时候存」的全部规则数值。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么回合数要算而不用计数器</b>：一次成功结算 = 一个游戏月（结算第 ⑦ 步已经推进
/// <c>CurrentDate</c>），所以「第几回合」完全由当前日期决定，是个<b>导出量</b>。
/// 落一个计数器就等于引入第二份真源——它必须与时间保持同步，一旦漏加或重复加就与日期
/// 矛盾；而且计数器是<b>跨特性形状变更</b>（要进 <c>GameState</c>）。故本类只做算术
/// （契约十一 §1；data-model §5）。
/// </para>
/// <para>
/// <b>不读钟</b>：本类不读墙钟，也不用文件的最后写入时间（契约十一 §1 条款 3）。
/// </para>
/// <para>
/// <b>数值出处</b>：备份间隔 = 3 回合（规格书 §14）、纪元 = 1 年 1 月（规格书 §3）。
/// 这两个数是本特性**仅有的**新增规则数值，且只在这里出现一次；<c>KFL.Infrastructure</c>
/// 的存档代码 MUST NOT 有副本（它看不到本层，由 G-05 结构上保证）。
/// <c>3</c> 是整数，按 002 契约五 §5 不进 <c>ConfigLiteralTests.RegisteredValues</c>。
/// </para>
/// </remarks>
public static class SaveCadencePolicy
{
    /// <summary>模拟纪元年——模拟总是从这一年的第一个月开始（规格书 §3）。</summary>
    public const int EpochYear = 1;

    /// <summary>模拟纪元月。</summary>
    public const int EpochMonth = 1;

    /// <summary>自动备份间隔（回合数；规格书 §14「每 3 回合」）。</summary>
    public const int BackupIntervalTurns = 3;

    /// <summary>
    /// 截至 <paramref name="current"/> 已完成的回合数 = 已成功结算的月数。
    /// </summary>
    /// <remarks>
    /// 结算第 ⑦ 步推进日期之后再取，故第 N 次成功结算后恒等于 N。
    /// 初始状态（1 年 1 月）⇒ 0。
    /// </remarks>
    /// <param name="current">当前游戏日期。</param>
    /// <returns>已完成回合数。</returns>
    public static int CompletedTurnsAt(GameDate current) =>
        ((current.Year - EpochYear) * 12) + (current.Month - EpochMonth);

    /// <summary>
    /// 本回合是否该做备份。
    /// </summary>
    /// <remarks>
    /// <c>turns &gt; 0</c> 这一条不能省：没有它，初始状态（0 回合）会被判成「3 的倍数」。
    /// </remarks>
    /// <param name="completedTurns">已完成回合数。</param>
    /// <returns>该备份 ⇒ <c>true</c>。</returns>
    public static bool ShouldBackup(int completedTurns) =>
        completedTurns > 0 && (completedTurns % BackupIntervalTurns == 0);
}
