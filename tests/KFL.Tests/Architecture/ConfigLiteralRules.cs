using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KFL.Tests.Architecture;

/// <summary>一条 SC-008 违规：规则数值出现在了不该出现的位置。</summary>
/// <param name="Path">仓库相对路径。</param>
/// <param name="Line">行号（1 起）。</param>
/// <param name="Value">命中的数值字面量。</param>
/// <param name="LineText">命中行的原文（已去注释与字符串内容，供人眼定位）。</param>
public sealed record ConfigLiteralViolation(string Path, int Line, decimal Value, string LineText)
{
    /// <summary>可直接贴进失败信息的定位串。</summary>
    public override string ToString() => $"{Path}:{Line} → {Value} ｜ {LineText}";
}

/// <summary>
/// SC-008 的**纯函数**判定逻辑（契约五 §5 条款 1~5）：给定「源码字典 + 数值清单」，
/// 输出「文件:行 + 数值」形式的违规集合。
/// </summary>
/// <remarks>
/// <para>
/// 判据是「**可执行代码里出现了与清单相同的数值字面量**」，不是「出现了任何数字」——
/// 命中需要与该清单逐一相等（按数值相等，故 <c>0.90m</c> 与 <c>0.9</c> 同判）。
/// </para>
/// <para>
/// **先剥注释、再认代码**（T059 条款 ③④）：契约一/实体与配置表的 XML 注释里本就写着
/// <c>0.5%~2.4%</c>、<c>≥100 贯</c>、<c>每成人 20 亩</c> 等**归属说明性数值**；错误信息字符串里
/// 也常引用 <c>data-model §1.3</c>、<c>FR-010</c> 之类编号。两类都不是数值的第二出处，故
/// <see cref="CodeView"/> 把注释**与字符串/字符字面量的内容**都抹成空格（换行保留，行号不变）。
/// 注意这两步必须一起做：只剥注释会把「字符串里的 <c>//</c>」误当注释起点。
/// </para>
/// <para>
/// 范围与 G-07 相同（<c>src\KFL.Core</c>/<c>KFL.Infrastructure</c>/<c>KFL.Rules</c> 与
/// <c>tests\KFL.Tests\{Core,Infrastructure,Fixtures,Rules}</c>），并额外豁免两处：
/// 数值的合法住处 <c>src\KFL.Rules\Config\</c>，以及本目录（它的职责就是读仓库文件与源码）。
/// </para>
/// <para>
/// 行级豁免：命中行若含 <see cref="ExemptionComment"/> 即跳过（须在同一行写明理由）。
/// 这是为「夹具输入」与「锚点断言」准备的口子——锚点（如 <c>5100</c>）本身是被验证对象。
/// </para>
/// <para>
/// **两条判据**：① 数值清单命中（<see cref="Evaluate"/>，按**数值**识别，覆盖小数与 4 位及以上官俸）；
/// ② 金额字面量（<see cref="EvaluateMoneyLiterals"/>，按**上下文**识别，覆盖清单收不进的整数规则值
/// 如宅价 1/10/100/300 与门槛 100）。两者互补：① 抓「规则数值被抄到非配置处」，
/// ② 抓「产品代码把金额写成硬编码」。
/// </para>
/// </remarks>
public static class ConfigLiteralRules
{
    /// <summary>数值的合法住处：只有这里的常量声明处可以出现清单里的字面量。</summary>
    public const string ConfigRoot = @"src\KFL.Rules\Config\";

    /// <summary>本目录：职责是读仓库文件与源码，故不在扫描范围内（契约一 §2 的 G-07 行同款豁免）。</summary>
    public const string ArchitectureRoot = @"tests\KFL.Tests\Architecture\";

    /// <summary>行级豁免注释；命中该注释的行不参与扫描（契约 T059 条款 ③）。</summary>
    public const string ExemptionComment = "// arch-guard:allow";

    /// <summary>
    /// 行级豁免注释的**历史别名**：T033 写 <c>SalaryTableTests</c> 的锚点断言时先用了
    /// <c>// config-literal:allow</c>，两者的语义完全一致，故一并承认（避免为改名去动已验证的断言）。
    /// </summary>
    public const string LegacyExemptionComment = "// config-literal:allow";

    /// <summary>扫描范围 = G-07 的产品源码根 + 四个领域/接缝/夹具测试目录（R-13/R-14）。</summary>
    public static readonly string[] ScannedRoots =
    [
        @"src\KFL.Core\",
        @"src\KFL.Infrastructure\",
        @"src\KFL.Rules\",
        @"tests\KFL.Tests\Core\",
        @"tests\KFL.Tests\Infrastructure\",
        @"tests\KFL.Tests\Fixtures\",
        @"tests\KFL.Tests\Rules\",
    ];

    /// <summary>产品源码根：<see cref="EvaluateMoneyLiterals"/> 的扫描范围（数值合法住处 <c>Config\</c> 除外）。</summary>
    public static readonly string[] ProductRoots =
    [
        @"src\KFL.Core\",
        @"src\KFL.Infrastructure\",
        @"src\KFL.Rules\",
    ];

    private static readonly string[] IgnoredPathSegments = [".git", "bin", "obj"];

    /// <summary>
    /// 数值字面量：<c>数字[.数字][m|f|d]</c>，两侧不得紧贴标识符字符或点号
    /// （这样 <c>net10.0</c>、<c>KFL2</c>、<c>0x1F</c>、<c>1e-3</c>、<c>v1.2.3</c> 都不会被误读）。
    /// </summary>
    private static readonly Regex LiteralPattern = new(
        @"(?<![A-Za-z0-9_.])(\d+(?:\.\d+)?)(?:[mMfFdD])?(?![A-Za-z0-9_])",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// `Money.FromGuan(字面量)` / `Money.FromWen(字面量)`——<see cref="EvaluateMoneyLiterals"/> 的判据。
    /// 实参须**紧接**在左括号（可含空白）之后，故 `Money.FromGuan(guan * 1000m)` 之类派生表达式不命中。
    /// </summary>
    private static readonly Regex MoneyArgumentLiteralPattern = new(
        @"Money\.From(?:Guan|Wen)\(\s*(-?\d+(?:\.\d+)?)(?:[mMfFdD])?(?![A-Za-z0-9_])",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// 在给定源码集合里找违规；零违规即 SC-008 成立。
    /// </summary>
    /// <param name="sourceFiles">仓库相对路径 → 文件原文。</param>
    /// <param name="registeredValues">登记表清单（契约五 §1~§4 的数值）。</param>
    /// <returns>违规列表（按文件、行号排序）。</returns>
    /// <exception cref="ArgumentNullException">任一参数为 <c>null</c>。</exception>
    public static IReadOnlyList<ConfigLiteralViolation> Evaluate(
        IReadOnlyDictionary<string, string> sourceFiles, IReadOnlyList<decimal> registeredValues)
    {
        ArgumentNullException.ThrowIfNull(sourceFiles);
        ArgumentNullException.ThrowIfNull(registeredValues);

        var registered = new HashSet<decimal>(registeredValues);
        var violations = new List<ConfigLiteralViolation>();
        var seen = new HashSet<(string Path, int Line, decimal Value)>();

        foreach (var (path, text) in EnumerateSources(sourceFiles))
        {
            // 行级豁免注释 MUST 在**原文**上判定（去注释后注释文本本身已经没了），
            // 数值则在代码视图上判定；两者按行号一一对应。
            var rawLines = Lines(text);
            var codeLines = CodeView(text).Split('\n');

            for (var index = 0; index < codeLines.Length; index++)
            {
                var line = codeLines[index];

                if (index < rawLines.Length && IsExempt(rawLines[index]))
                {
                    continue;
                }

                foreach (var value in LiteralsIn(line))
                {
                    if (!registered.Contains(value) || !seen.Add((path, index + 1, value)))
                    {
                        continue;
                    }

                    violations.Add(new ConfigLiteralViolation(path, index + 1, value, line.Trim()));
                }
            }
        }

        return violations
            .OrderBy(v => v.Path, StringComparer.Ordinal)
            .ThenBy(v => v.Line)
            .ToList();
    }

    /// <summary>
    /// SC-008 的**金额条款**：产品源码里 MUST NOT 把裸数值字面量直接喂给
    /// <c>Money.FromGuan</c> / <c>Money.FromWen</c>——那个字面量必然是规则数值的第二份副本。
    /// </summary>
    /// <remarks>
    /// <para>
    /// **为何另立一条判据**：<see cref="Evaluate"/> 的清单只能收「小数全收 + 4 位及以上官俸」——
    /// 宅价 1/10/100/300、门槛 100、亩数 20、月数 30 这类整数规则值一旦入单，会与成员编号、年龄、
    /// <c>GameDate</c> 年号、<c>TalentSet(100)</c>、夹具金额撞车（实测：宅价四值合计 **431** 处命中，
    /// 其中 <c>1</c> 一项就占 **345** 处），扫描会退化成噪声源。本条改按**上下文**识别：
    /// 产品代码里金额的构造点只有 <c>Money.FromGuan</c> / <c>Money.FromWen</c> 两处，
    /// 给它们喂字面量即「**离开配置表就无法成立**」的数值副本（产品代码的金额一律经配置成员
    /// 或派生表达式取得）。判别不依赖数值大小，故不会与任何同值噪声混淆。
    /// </para>
    /// <para>
    /// **范围只到产品源码**（<see cref="ProductRoots"/>，数值的合法住处 <c>Config\</c> 除外）：
    /// 测试里的 <c>Money.FromGuan(10m)</c> 是夹具金额（契约五 §5 条款 ⑤），不是规则数值的第二出处。
    /// 行级豁免注释同样生效。
    /// </para>
    /// </remarks>
    /// <param name="sourceFiles">仓库相对路径 → 文件原文。</param>
    /// <returns>违规列表（按文件、行号排序）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sourceFiles"/> 为 <c>null</c>。</exception>
    public static IReadOnlyList<ConfigLiteralViolation> EvaluateMoneyLiterals(
        IReadOnlyDictionary<string, string> sourceFiles)
    {
        ArgumentNullException.ThrowIfNull(sourceFiles);

        var violations = new List<ConfigLiteralViolation>();

        foreach (var (path, text) in EnumerateSources(sourceFiles))
        {
            if (!ProductRoots.Any(root => path.StartsWith(root, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var rawLines = Lines(text);
            var codeLines = CodeView(text).Split('\n');

            for (var index = 0; index < codeLines.Length; index++)
            {
                var line = codeLines[index];

                if (index < rawLines.Length && IsExempt(rawLines[index]))
                {
                    continue;
                }

                foreach (Match match in MoneyArgumentLiteralPattern.Matches(line))
                {
                    if (!decimal.TryParse(
                        match.Groups[1].Value,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var value))
                    {
                        continue;
                    }

                    violations.Add(new ConfigLiteralViolation(path, index + 1, value, line.Trim()));
                }
            }
        }

        return violations
            .OrderBy(v => v.Path, StringComparer.Ordinal)
            .ThenBy(v => v.Line)
            .ToList();
    }

    /// <summary>取一行里出现的全部数值字面量（按出现顺序，可能重复）。</summary>
    /// <param name="line">已过 <see cref="CodeView"/> 的一行源码。</param>
    /// <returns>数值列表。</returns>
    public static IReadOnlyList<decimal> LiteralsIn(string line)
    {
        var values = new List<decimal>();

        foreach (Match match in LiteralPattern.Matches(line))
        {
            if (decimal.TryParse(
                match.Groups[1].Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    /// <summary>
    /// 剥离注释：<c>//</c> 行注释、<c>/* */</c> 块注释、<c>///</c> XML 文档注释全部剔除；
    /// **字符串/字符字面量原样保留**（其内部不当作注释起点）。
    /// </summary>
    /// <param name="text">文件原文。</param>
    /// <returns>与原文换行结构相同的去注释文本。</returns>
    public static string StripComments(string text) => Transform(text, blankLiterals: false);

    /// <summary>
    /// 只留可执行代码：注释与字符串/字符字面量的**内容**都抹成空格（字面量的引号保留，
    /// 以便同一行的后续字面量仍能被正确识别）。
    /// </summary>
    /// <param name="text">文件原文。</param>
    /// <returns>与原文换行结构相同的代码视图。</returns>
    public static string CodeView(string text) => Transform(text, blankLiterals: true);

    private static string Transform(string text, bool blankLiterals)
    {
        var output = new StringBuilder(text.Length);
        var index = 0;

        while (index < text.Length)
        {
            var current = text[index];

            if (current == '/' && index + 1 < text.Length && text[index + 1] == '/')
            {
                while (index < text.Length && text[index] != '\n')
                {
                    output.Append(text[index] == '\r' ? '\r' : ' ');
                    index++;
                }

                continue;
            }

            if (current == '/' && index + 1 < text.Length && text[index + 1] == '*')
            {
                while (index < text.Length
                    && !(text[index] == '*' && index + 1 < text.Length && text[index + 1] == '/'))
                {
                    output.Append(text[index] is '\n' or '\r' ? text[index] : ' ');
                    index++;
                }

                if (index < text.Length)
                {
                    output.Append("  ");
                    index += 2;
                }

                continue;
            }

            if (current == '"')
            {
                index = CopyStringLiteral(text, index, output, blankLiterals);
                continue;
            }

            if (current == '\'')
            {
                index = CopyCharLiteral(text, index, output, blankLiterals);
                continue;
            }

            output.Append(current);
            index++;
        }

        return output.ToString();
    }

    private static int CopyStringLiteral(string text, int start, StringBuilder output, bool blank)
    {
        var quotes = 1;

        while (start + quotes < text.Length && text[start + quotes] == '"')
        {
            quotes++;
        }

        if (quotes >= 3)
        {
            return CopyRawStringLiteral(text, start, quotes, output, blank);
        }

        var verbatim = start > 0 && text[start - 1] == '@';

        output.Append(text[start]);
        var index = start + 1;

        while (index < text.Length)
        {
            var current = text[index];
            output.Append(blank && current is not ('\n' or '\r') ? ' ' : current);
            index++;

            if (verbatim && current == '"')
            {
                if (index < text.Length && text[index] == '"')
                {
                    output.Append(blank ? ' ' : text[index]);
                    index++;
                    continue;
                }

                return index;
            }

            if (!verbatim && current == '\\' && index < text.Length)
            {
                output.Append(blank && text[index] is not ('\n' or '\r') ? ' ' : text[index]);
                index++;
                continue;
            }

            if (!verbatim && current == '"')
            {
                return index;
            }
        }

        return index;
    }

    private static int CopyRawStringLiteral(string text, int start, int quotes, StringBuilder output, bool blank)
    {
        output.Append(text, start, quotes);
        var index = start + quotes;

        while (index < text.Length)
        {
            if (text[index] == '"')
            {
                var run = 0;
                while (index + run < text.Length && text[index + run] == '"')
                {
                    run++;
                }

                output.Append(text, index, run);
                index += run;

                if (run >= quotes)
                {
                    return index;
                }

                continue;
            }

            output.Append(blank && text[index] is not ('\n' or '\r') ? ' ' : text[index]);
            index++;
        }

        return index;
    }

    private static int CopyCharLiteral(string text, int start, StringBuilder output, bool blank)
    {
        output.Append(text[start]);
        var index = start + 1;

        while (index < text.Length)
        {
            var current = text[index];
            output.Append(blank && current != '\'' ? ' ' : current);
            index++;

            if (current == '\\' && index < text.Length)
            {
                output.Append(blank ? ' ' : text[index]);
                index++;
                continue;
            }

            if (current == '\'')
            {
                return index;
            }
        }

        return index;
    }

    private static IEnumerable<(string Path, string Text)> EnumerateSources(
        IReadOnlyDictionary<string, string> sourceFiles)
    {
        var roots = ScannedRoots.Select(Normalize).ToArray();

        foreach (var pair in sourceFiles)
        {
            var path = Normalize(pair.Key);

            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || !IsRelevantPath(path))
            {
                continue;
            }

            if (!roots.Any(root => path.StartsWith(root, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // 数值的合法住处，与「读仓库文件」的本目录都在扫描范围之外。
            if (path.StartsWith(ConfigRoot, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(ArchitectureRoot, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return (path, pair.Value);
        }
    }

    private static string[] Lines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    private static bool IsExempt(string line) =>
        line.Contains(ExemptionComment, StringComparison.Ordinal)
        || line.Contains(LegacyExemptionComment, StringComparison.Ordinal);

    private static bool IsRelevantPath(string path) =>
        !path.Split('\\').Any(segment => IgnoredPathSegments.Contains(segment, StringComparer.OrdinalIgnoreCase));

    private static string Normalize(string path) =>
        path.Replace('/', '\\').Trim().TrimStart('\\').TrimStart('.').TrimStart('\\');
}
