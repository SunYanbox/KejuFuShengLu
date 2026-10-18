namespace KFL.Rules.Achievements;

/// <summary>
/// 平均寿命的三均值（Tukey trimean；规格书 §15:639；契约二十四 §4；SC-009）。
/// </summary>
/// <remarks>
/// <para>
/// 算法：`TM = (Q1 + 2·Q2 + Q3) / 4`，其中 `Q1`/`Q2`/`Q3` 取 **Tukey hinges**、
/// **奇数样本时中位计入两半**（R `fivenum` 口径；契约二十四「条款 14」）。
/// MUST NOT 采用「奇数样本弃中位」的变体（判别例 `[10,20,30,40,100]`：
/// 正解 `30.000`，「弃中位」变体得 `36.250`）。
/// </para>
/// <para>
/// **运算纪律（条款 17）**：全程 <see cref="decimal"/>，唯一除法是 `(Q1 + 2·Q2 + Q3) / 4`，
/// 取整只发生在最后一步且只用
/// <see cref="Math.Round(decimal, int, MidpointRounding)"/> + <see cref="MidpointRounding.AwayFromZero"/>。
/// MUST NOT 对 `Q1`/`Q2`/`Q3` 各自取整后再合成。
/// </para>
/// <para>
/// **独立成类**（`plan.md:286`）：MUST NOT 内联进 <c>ChronicleBuilder</c>——本特性只有一个调用点
/// （编年史 ⑥），单独成类才让八行金色算例可逐行钉死。
/// </para>
/// </remarks>
public static class TukeyTrimean
{
    /// <summary>计算三均值。</summary>
    /// <param name="values">已故成员享年序列；`null` 视为调用方缺陷。</param>
    /// <returns>保留 3 位小数的三均值；空序列返回 `0m`（**不抛异常、不返回 `null`**）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> 为 <c>null</c>。</exception>
    public static decimal Of(IReadOnlyList<int> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var n = values.Count;
        if (n == 0)
        {
            return 0.000m;
        }

        // 升序**副本**：MUST NOT 原地改累计量序列（条款 13）。
        var sorted = new int[n];
        for (var i = 0; i < n; i++)
        {
            sorted[i] = values[i];
        }

        Array.Sort(sorted);

        var q2 = Median(sorted, 0, n);

        // 奇数样本时中位同时计入两半（条款 14）。
        var lowerEnd = n % 2 == 0 ? n / 2 : (n + 1) / 2;
        var upperStart = n / 2;

        var q1 = Median(sorted, 0, lowerEnd);
        var q3 = Median(sorted, upperStart, n);

        var trimean = (q1 + (2m * q2) + q3) / 4m;

        // 刻度钉死为 `AchievementPolicy.AverageLifespanDecimals`（= 3）位：`Math.Round` 只按需截断，
        // 数值恰为整时不补零（47.5 ⇒ 47.5），而 004 FR-021 按**位**比对十进制值 ⇒ 末尾补 0.000 定标。
        return Math.Round(trimean, 3, MidpointRounding.AwayFromZero) + 0.000m;
    }

    /// <summary>取升序数组切片 `[start, end)` 的中位数。</summary>
    private static decimal Median(int[] sorted, int start, int end)
    {
        var count = end - start;
        var mid = start + (count / 2);

        return count % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2m;
    }
}
