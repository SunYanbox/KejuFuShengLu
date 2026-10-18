using Xunit;

namespace KFL.Tests.Architecture;

/// <summary>
/// T059 / SC-008：规则数值**只**住在 <c>src/KFL.Rules/Config/</c> 的常量声明处，
/// 配置类之外没有第二份副本（契约五 §5 条款 1~5）。
/// </summary>
/// <remarks>
/// <para>
/// 本文件是**外壳**：用 <see cref="RepositoryLocator.Load"/> 读真实仓库，把源码字典与下面的
/// 「登记表清单」交给纯函数 <see cref="ConfigLiteralRules.Evaluate"/>。
/// </para>
/// <para>
/// **清单的来源**：契约五 §1~§4 逐行的数值（逐字抄入）。清单本身是 SC-008 的**验证物**，
/// 不是生产数值的第二出处——冲突时以源码为准；且它住在 <c>Architecture/</c>，
/// 而该目录在扫描范围之外，故清单不会自己把自己判违规。
/// </para>
/// <para>
/// **清单的取舍**：<b>小数全收</b>（日耗 8.5/17.5、乘区 0.90、亩产 0.5、利润率 0.02、利率 0.005/0.024、
/// 系数 0.2/0.4/0.6/0.7、比例 0.40…——最容易被人「顺手复制」进计算器的那一类）；
/// <b>4 位及以上官俸数额全收</b>（1020~5100），<b>低品官俸中实测零撞车的 5 项也收</b>
/// （860/720/235/130/72——原「低品数额一律与成员编号/夹具金额撞车」的判断经实测不成立，见下）。
/// 余下的 1~3 位整数（亩数 20、月数 12/30、年龄 12/14/18/60、除数 200/400、门槛 100、
/// 宅价 1/10/100/300、饥馑 3/12、低品官俸 600/500/420）在已提交的领域/夹具测试里同时也是
/// **成员编号、年龄与世代号、夹具金额**（<c>NewPerson(420, …)</c>、<c>generation: 3</c>、
/// <c>Money.FromGuan(10m)</c>、<c>GameDate(10, 5)</c>、<c>TalentSet(100, …)</c>），逐行判据无法区分二者
/// ——实测把宅价四值 1/10/100/300 入单会产生 **431** 处命中（<c>1</c> 一项 345 处、<c>10</c> 47 处、
/// <c>100</c> 33 处、<c>300</c> 6 处，且绝大多数是 <c>Generation + 1</c>、<c>1 &lt;&lt; 3</c>、
/// <c>year &lt; 1</c> 一类无关同值），扫描将退化成噪声源。
/// </para>
/// <para>
/// **整数规则值的缺口由第二条判据补**：<see cref="ConfigLiteralRules.EvaluateMoneyLiterals"/>
/// 按「金额构造点」而非按数值识别，专抓 <c>Money.FromGuan(300m)</c> 这类硬编码——它不需要把
/// 1/10/100/300 放进数值清单，故不受同值撞车影响（产品源码实测零命中），
/// 见 <see cref="产品代码的金额字面量只经配置成员取得"/>。整数规则值的唯一性另由配置表自身的
/// XML 注释、以及 T062 的 FR/SC 覆盖核对（每一行都要落到读配置成员的断言）兜底。
/// </para>
/// <para>
/// **小数的夹具撞车用行级豁免**（T059 条款 ⑤）：测试里作为**用例输入**的夹具数值不算第二出处，
/// 例如 <c>Money.FromGuan(0.5m)</c> 这笔任意借款金额、<c>new FixedRandomService(0.5d)</c> 这个
/// 随机取值，都只是恰好等于某个规则数值。这些行以 <c>// arch-guard:allow 理由</c> 标注；
/// <c>SalaryTableTests</c> 的 SC-004 锚点行则沿用先写下的 <c>// config-literal:allow</c> 别名。
/// </para>
/// </remarks>
public class ConfigLiteralTests
{
    /// <summary>失败信息最多列出的违规条数（避免一次失败刷屏）。</summary>
    private const int ReportLimit = 60;

    /// <summary>
    /// 契约五 §1~§4 的数值清单。每一项都注明出处行，便于与 <c>contracts/config-registry.md</c> 对照。
    /// </summary>
    private static readonly decimal[] RegisteredValues =
    [
        // §1 生活费：三档日耗（成人/青年/老人/儿童）拮据 20/7/14/5、普通 25/8.5/17.5/5、体面 30/10/21/5
        // ——非整数的两格（8.5、17.5）入单；整数格见类注释
        8.5m, 17.5m,
        // §1 月 = 日耗 × 30；年龄边界 男 12 / 女 14 / 青年上界 18 / 老人下界 60（整数，见类注释）
        // §1 农出身独立乘区 成年 0.90 / 未成年 0.80
        0.90m, 0.80m,
        // §1 生活费一般乘区修正项 未成年 −0.50、救济期 −0.20
        0.50m,
        // §1 米价系数 初始 1.0（整数）、游走幅度 0.2（= ±10%）、clamp 0.7~3.0（上限为整数）、
        //        派生值 (系数 − 0.4) ÷ 0.6
        0.7m, 0.4m, 0.6m, 0.2m,
        // §1 难度支出系数 0.6/1.0(整数)/1.1/1.3 与 §2 难度收益系数 1.4/1.0(整数)/0.9/0.8
        1.1m, 1.3m, 1.4m, 0.9m, 0.8m,
        // §2 自耕 0.5 贯/亩/年（与夹具金额 0.5 贯、随机回退值 0.5d 撞车，靠行级豁免区分）
        0.5m,
        // §2 田租 0.1 贯/亩/年
        0.1m,
        // §2 务农 2 贯/月（整数）、做工 1.5 贯/月
        1.5m,
        // §2 经商 2%（与夹具利息金额 0.02 贯撞车，靠行级豁免区分）、门槛 100 贯（整数）、商出身 ×1.1
        0.02m,
        // §2 天赋除数 农 200、工 400、商 200（与成员编号撞车，见类注释）
        // §2 铺面年租 20%、工出身 bonus 6%
        0.06m,
        // §2 官俸 18 级：4 位及以上者入单（低品数额与成员编号/夹具金额撞车，见类注释）
        5100m, 4250m, 3560m, 2980m, 2490m, 2090m, 1750m, 1460m, 1220m, 1020m,
        // §2 官俸低品中**实测零撞车**的 5 项（860/720/235/130/72：全仓库扫描 0 处命中，
        //    故无需行级豁免；余下 600/500/420 与成员编号撞车，仍见类注释）
        860m, 720m, 235m, 130m, 72m,
        // §2 士出身当官 ×1.05
        1.05m,
        // §3 田 1 贯/亩、农村宅 10 贯、城市宅 100 贯、铺面 300 贯（整数，见类注释）
        // §4 储蓄与贷款利率区间 0.5%~2.4%（下限与夹具金额 0.005 贯撞车，靠行级豁免区分）
        0.005m, 0.024m,
        // §4 划扣比例 仕 20% / 工农 40% / 商 80%
        0.40m,
        // §4 饥馑 3 月转救济、救济 12 月（整数，见类注释）、救济折扣 20%（同 0.50/0.40 的形式）
        // ---- 003（逻辑轨 ③）新增：「开局资产与官吏体系」契约八 §3 条款 3 的四项小数 ----
        // 基础升级率 25%（§8.2）
        0.25m,
        // 每点政绩的升级概率加成 0.3%（§8.2）
        0.003m,
        // 天命寿数均值：男 60.7 / 女 62.3（§4.1）
        60.7m, 62.3m,
        // 注意：封顶 0.70 与米价 clamp 上限同值（0.7m 已登记，MUST NOT 重复登记）；
        //       半俸 0.50 与生活费一般乘区修正 −0.50 同值（0.5m 已登记，MUST NOT 重复登记）。
        //       整数规则值一律不入单（6 / 24 / 11 / 13 / 15 / 18 / 36 / 70 / 100 / 12 / 14 / 60 /
        //       23~33 / 0~8 / 80~100 / 90~100 / 10~30），见类注释与第二条判据。
        // ---- 005（逻辑轨 ⑤）新增：「科举周期」契约十四 §1 的**四项**小数 ----
        // 解试基础录取率 1%（§6）
        0.01m,
        // 殿试基础进甲率 5%（§6）
        0.05m,
        // 学业系数每点增量 1.2%（§6）
        0.012m,
        // 需掷骰概率的封顶 89.9999%（§7.1）
        0.899999m,
        // 注意：省试基础录取率 0.10 与田租 0.1 同值、学业系数基数 0.4 与米价派生值 0.4 同值、
        //       仕族加成 1.1 与难度支出系数 1.1 同值 —— 三者已登记，MUST NOT 重复登记；
        //       贿赂乘数 1.0 与整数同值（第二判据）；
        //       整数规则值一律不入单（14 / 400 / 50 / 3 / 50 / 6 / 10 / 20 / 40 / 80），见类注释。
        // ---- 006（逻辑轨 ⑥）新增：「贿赂与惩罚」契约十七 §2 的**三项**小数 ----
        // 解试效果乘数底数 1.6（§7.1）
        1.6m,
        // 省试基础被抓率 15%（§7.1；与普通档殿试的比例罚金 0.15 同值，只登记一次）
        0.15m,
        // 殿试基础被抓率 35%（§7.1）
        0.35m,
        // 注意：基础被抓率 0.05 与殿试基础进甲率同值、封顶 1.0 与整数同值、金额递进步长 0.2
        //       与米价游走幅度同值、比例罚金 0.02 / 0.01 / 0.10 / 0.20 分别与经商率 / 田租 /
        //       省试录取率 / 米价步长同值、被抓风险系数 0.2 / 1.0 / 1.5 / 3.0 与既有项同值、
        //       效果乘数 k 的 1.3 / 1.1 与难度支出系数同值 —— 全部已登记，MUST NOT 重复登记。
        //       整数规则值一律不入单（100 / 500 / 2000 / 50000 / 12 / 36 / 24 / 48 / 60 / 180 /
        //       240 / 120 / 18 / 200 / 起始各级别 1），见类注释与第二条判据。
        // ---- 007（逻辑轨 ⑦）新增：「家族生命周期」契约二十一 §1~§3 的小数 ----
        // §1 成长：延名师月加成 2.25（学习/月 1.5 与学业遗忘 0.5、士出身 ×1.10、病程减半 ÷2 均同值，
        //          MUST NOT 重复登记）
        2.25m,
        // §2 疾病：满体质感染率 0.0025、商清除系数 0.95、农清除系数 1.20、药费斜率 0.45、
        //          幼年清除加成 0.30、青年清除加成 0.60（同值 0.6 已由米价派生值登记）、
        //          1.10/0.80/0.40/1.05/0.05 分别与难度系数 / 幼苗清除 / 划扣比例 / 士出身 / 殿试进甲同值
        0.0025m, 0.95m, 1.20m, 0.45m, 0.30m,
        // §3 婚配生育：阶层权重 0.70/0.45/0.20/0.50/0.15/0.40/0.25（0.25 / 0.15 / 0.40 / 0.50
        //          已由既有项登记）、测试专用难度期望值 0.025 / 0.07 / 0.12（简单·地狱分娩率、
        //          地狱夭折率）、0.10 与田租 0.1 同值
        0.025m, 0.07m, 0.12m,
        // 注意：1.00 与 1.50 / 1.10 / 0.80 / 0.60 / 0.20 / 0.10 / 0.05 / 0.40 等一律同值已登记；
        //       1.00 与整数 1 同值故 MUST NOT 入单（会与下标、计数撞车）；
        //       整数规则值一律不入单（10000 / 层号 / 年龄线 3 / 16 / 20 / 45 / 100 / 岁数 8 / 16）。
        // ---- 008（逻辑轨 ⑧）新增：「成就与绝嗣结局」契约二十二 §2 的阈值**全为整数** ----
        // AchievementPolicy 的 44 个成员一律 `public const int`（10 / 20 / 50 / 100 / 300 / 500 /
        //   1_000 / 3_000 / 10_000 / 50_000 一类门槛与档位），**本特性零个非整数阈值**，
        //   故本清单无新增项——此即「以守卫变红为准确认」的核对结果：不入单不是漏登记，
        //   而是没有可入单的值（判据类只读 `AchievementPolicy` 成员，金额经 `Money.FromGuan(成员)`）；
        //   金额条款另由第二条判据（`EvaluateMoneyLiterals`，按构造点识别）覆盖本特性的判据文件。
    ];

    /// <summary>真实仓库零违规——这就是 SC-008 的判定。</summary>
    [Fact]
    public void 登记表数值只出现在配置类的常量声明处()
    {
        var input = RepositoryLocator.Load();
        var violations = ConfigLiteralRules.Evaluate(input.SourceFiles, RegisteredValues);

        Assert.True(
            violations.Count == 0,
            FormattableString.Invariant(
                $"SC-008 违规 {violations.Count} 处（清单 {RegisteredValues.Length} 项，扫描 {CountScanned(input.SourceFiles)} 个文件）：{Environment.NewLine}{Describe(violations)}"));
    }

    /// <summary>
    /// SC-008 的金额条款：产品代码里的金额一律经配置成员或派生表达式取得，
    /// MUST NOT 出现 <c>Money.FromGuan(300m)</c> 这类裸字面量——真实仓库零违规。
    /// </summary>
    [Fact]
    public void 产品代码的金额字面量只经配置成员取得()
    {
        var input = RepositoryLocator.Load();
        var violations = ConfigLiteralRules.EvaluateMoneyLiterals(input.SourceFiles);

        Assert.True(
            violations.Count == 0,
            FormattableString.Invariant(
                $"金额条款违规 {violations.Count} 处（产品源码 {CountProductFiles(input.SourceFiles)} 个文件）：{Environment.NewLine}{Describe(violations)}"));
    }

    /// <summary>
    /// 金额条款的自检：产品代码里喂字面量即报；喂变量/表达式、写在配置类里、
    /// 写在测试目录里、或加了行级豁免都不报。
    /// </summary>
    [Fact]
    public void 金额字面量判据只在产品代码的构造点上生效()
    {
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [@"src\KFL.Rules\Settlement\Sample.cs"] = """
                var a = Money.FromGuan(300m);
                var b = Money.FromWen(-1m);
                var c = Money.FromGuan(someGuan * 1000m);
                var d = Money.FromGuan(AssetPriceTable.ShopGuan);
                var e = Money.FromWen(100m); // arch-guard:allow 锚点即被验证对象
                """,
            [@"src\KFL.Rules\Config\SampleTable.cs"] = "var f = Money.FromGuan(300m);",
            [@"tests\KFL.Tests\Core\SampleTests.cs"] = "var g = Money.FromGuan(300m);",
        };

        var violations = ConfigLiteralRules.EvaluateMoneyLiterals(sources);

        // a 与 b 命中（第 1、2 行）；c/d 是派生表达式不命中；e 有行级豁免；
        // 配置类是数值合法住处；测试目录不在金额条款范围内。
        Assert.Equal(2, violations.Count);
        Assert.Equal(300m, violations[0].Value);
        Assert.Equal(1, violations[0].Line);
        Assert.Equal(-1m, violations[1].Value);
        Assert.Equal(2, violations[1].Line);
    }

    /// <summary>扫描范围非空洞：每个根目录都真的读到了文件，否则上面的断言可能恒真。</summary>
    [Fact]
    public void 扫描范围覆盖全部七个根目录()
    {
        var input = RepositoryLocator.Load();
        var scanned = ScannedPaths(input.SourceFiles).ToList();

        Assert.NotEmpty(scanned);

        foreach (var root in ConfigLiteralRules.ScannedRoots)
        {
            Assert.Contains(
                scanned,
                path => path.StartsWith(root, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// 守卫自检：注入的命中/不命中四种情形逐条判定——配置类内不报、注释内不报、
    /// 执行代码内报、行级豁免注释生效。
    /// </summary>
    [Fact]
    public void 注入的数值在配置类内与注释内不报违规()
    {
        var inCore = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [@"src\KFL.Core\Entities\Ledger.cs"] = "var rate = 0.024m;\n// 归属说明：区间 0.024\nvar x = 1;",
        };

        var coreViolations = ConfigLiteralRules.Evaluate(inCore, RegisteredValues);

        // 只有第一行的 0.024 命中：注释行（0.024）与 1（不在清单里）都不报。
        Assert.Single(coreViolations);
        Assert.Equal(1, coreViolations[0].Line);

        var inConfig = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [@"src\KFL.Rules\Config\InterestPolicy.cs"] = "private const decimal MaxRate = 0.024m;",
        };

        Assert.Empty(ConfigLiteralRules.Evaluate(inConfig, RegisteredValues));

        var exempted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [@"tests\KFL.Tests\Rules\SampleAnchorTests.cs"] =
                "Assert.Equal(5100m, SalaryTable.AnnualSalaryGuan(1)); // arch-guard:allow 锚点即被验证对象",
        };

        Assert.Empty(ConfigLiteralRules.Evaluate(exempted, RegisteredValues));

        var inArchitecture = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [@"tests\KFL.Tests\Architecture\ConfigLiteralTests.cs"] = "0.024m, 5100m",
        };

        Assert.Empty(ConfigLiteralRules.Evaluate(inArchitecture, RegisteredValues));
    }

    /// <summary>
    /// 守卫自检：数值只认**可执行代码**里的字面量——错误信息字符串里的引用（<c>§1.3</c>、<c>FR-010</c>）
    /// 与注释里的数值都不算第二出处。
    /// </summary>
    [Fact]
    public void 数值只认可执行代码里的字面量()
    {
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [@"src\KFL.Rules\Settlement\IncomeCalculator.cs"] = """
                var text = "利率上限 0.024（data-model §1.3，FR-010）";
                /* 块注释里的 5100
                   与 0.005 都不算 */
                var rate = 0.024m;
                """,
        };

        var violations = ConfigLiteralRules.Evaluate(sources, RegisteredValues);

        Assert.Single(violations);
        Assert.Equal(0.024m, violations[0].Value);
        Assert.Equal(4, violations[0].Line);
    }

    /// <summary>
    /// 守卫自检：注释剥离器认得普通/逐字/原始字符串与块注释——注释起点在字符串里时 MUST NOT 生效。
    /// </summary>
    [Fact]
    public void 注释剥离器不把字符串里的斜杠当注释()
    {
        const string source = """"
            var a = "// 不是注释 0.024";
            var b = @"C:\path"; // 这才是注释
            var c = """
                原始串里的 5100 // 仍是字符串
                """;
            /* 块注释
               跨行 0.005 */
            var d = 'x';
            """";

        var stripped = ConfigLiteralRules.StripComments(source);

        // 字符串字面量保留（普通串与原始串里的数值都还在）……
        Assert.Contains("0.024", stripped, StringComparison.Ordinal);
        Assert.Contains("5100", stripped, StringComparison.Ordinal);
        Assert.Contains("C:\\path", stripped, StringComparison.Ordinal);
        Assert.Contains("var d = 'x';", stripped, StringComparison.Ordinal);

        // ……而两种注释被剥掉。
        Assert.DoesNotContain("这才是注释", stripped, StringComparison.Ordinal);
        Assert.DoesNotContain("0.005", stripped, StringComparison.Ordinal);

        // 行数不变：违规定位可以照抄原文行号。
        Assert.Equal(
            source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Length,
            stripped.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Length);
    }

    private static int CountScanned(IReadOnlyDictionary<string, string> sourceFiles) =>
        ScannedPaths(sourceFiles).Count();

    /// <summary>金额条款的扫描面：产品源码根，除去数值的合法住处 <c>Config\</c>。</summary>
    private static int CountProductFiles(IReadOnlyDictionary<string, string> sourceFiles) =>
        sourceFiles.Keys
            .Select(path => path.Replace('/', '\\'))
            .Count(path =>
                path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                && ConfigLiteralRules.ProductRoots.Any(
                    root => path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                && !path.StartsWith(ConfigLiteralRules.ConfigRoot, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> ScannedPaths(IReadOnlyDictionary<string, string> sourceFiles) =>
        sourceFiles.Keys
            .Select(path => path.Replace('/', '\\'))
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => ConfigLiteralRules.ScannedRoots.Any(
                root => path.StartsWith(root, StringComparison.OrdinalIgnoreCase)));

    private static string Describe(IReadOnlyList<ConfigLiteralViolation> violations) =>
        string.Join(
            Environment.NewLine,
            violations.Take(ReportLimit).Select(v => v.ToString()));
}
