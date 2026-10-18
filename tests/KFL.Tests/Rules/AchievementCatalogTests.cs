using KFL.Core.Enums;
using KFL.Rules.Achievements;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// T029 / SC-001：成就目录的 68 行登记与七系列计数（契约二十二 §1、§5）。
/// </summary>
/// <remarks>
/// <para>
/// 七系列计数的**规范值**取自契约二十二 §1 条款 2 与 §5：初露锋芒 12 / 科名累进 8 /
/// 宦海沉浮 11 / 宗族血脉 17 / 覆巢之下 11 / 饥馑求生 4 / 调试秘辛 5。
/// <c>tasks.md</c> T029 正文里的 <c>12+8+11+12+16+4+5</c> 是已知陈旧勘误（E-16/T092），
/// 本文件 MUST NOT 照抄。
/// </para>
/// <para>
/// 「成员名 ↔ <c>ACH-xxx</c> ↔ 中文名」三元组的唯一载体是 <see cref="AchievementCatalog"/>
/// （其类注释明言测试 MUST NOT 再抄一份），故本文件只做结构性断言与少量锚点抽查，
/// 不复制 68 行表。
/// </para>
/// </remarks>
public class AchievementCatalogTests
{
    /// <summary>契约 §1 登记的成就总数。</summary>
    private const int 成就总数 = 68;

    /// <summary>七个系列的规范枚举次序。</summary>
    private static readonly AchievementSeries[] 七系列 =
    [
        AchievementSeries.Emerging,
        AchievementSeries.ExamProgression,
        AchievementSeries.OfficialCareer,
        AchievementSeries.Lineage,
        AchievementSeries.CollectiveRuin,
        AchievementSeries.Famine,
        AchievementSeries.DebugSecrets,
    ];

    [Fact]
    public void 目录恰好六十八条且标识取值为一到六十八连续升序()
    {
        var 标识 = Enum.GetValues<AchievementId>();

        Assert.Equal(成就总数, 标识.Length);
        Assert.Equal(成就总数, AchievementCatalog.All.Count);

        for (var i = 0; i < 标识.Length; i++)
        {
            Assert.Equal(i + 1, (int)标识[i]);
        }
    }

    [Fact]
    public void 外部代码恰为ACH三位连号且与标识数值一一对应()
    {
        var 已见代码 = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < AchievementCatalog.All.Count; i++)
        {
            var 条目 = AchievementCatalog.All[i];
            var 期望代码 = $"ACH-{i + 1:D3}";

            Assert.Equal(期望代码, 条目.Code);
            Assert.Equal(i + 1, (int)条目.Id);
            Assert.True(已见代码.Add(条目.Code), $"外部代码重复：{条目.Code}");
        }
    }

    [Fact]
    public void 每条的中文名系列与来源族均已登记且中文名互不重复()
    {
        var 已见名称 = new HashSet<string>(StringComparer.Ordinal);

        foreach (var 条目 in AchievementCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(条目.Name));
            Assert.True(Enum.IsDefined(条目.Series), $"{条目.Code} 的系列不是已定义成员。");
            Assert.True(Enum.IsDefined(条目.Source), $"{条目.Code} 的来源族不是已定义成员。");
            Assert.True(已见名称.Add(条目.Name), $"中文名重复：{条目.Name}");
        }
    }

    [Theory]
    [InlineData(AchievementId.FoundationLaid, "开基立业")]
    [InlineData(AchievementId.ThreeGenerations, "三代同堂")]
    [InlineData(AchievementId.FiveGenerations, "五世其昌")]
    [InlineData(AchievementId.NarrowEscape, "绝处逢生")]
    [InlineData(AchievementId.JuRenThree, "杏林初茂")]
    [InlineData(AchievementId.JinShiThousand, "天下文宗")]
    [InlineData(AchievementId.FirstAppointment, "释褐登仕")]
    [InlineData(AchievementId.FallenAtShengShi, "名落孙山")]
    [InlineData(AchievementId.LargeHousehold, "人丁兴旺")]
    [InlineData(AchievementId.ImperialGroomPick, "榜下捉婿")]
    [InlineData(AchievementId.InfantDeath, "襁褓之殇")]
    [InlineData(AchievementId.TradeLoss, "商海浮沉")]
    [InlineData(AchievementId.CenturyHouse, "百年世家")]
    [InlineData(AchievementId.FirstSuccessfulBribe, "投石问路")]
    [InlineData(AchievementId.ThreePrisoners, "囹圄之灾")]
    [InlineData(AchievementId.FirstRelief, "嗟来之食")]
    [InlineData(AchievementId.FamineDeath, "路有冻骨")]
    [InlineData(AchievementId.DebugWakeTenThousand, "代码飞升")]
    public void 锚点行的中文名与契约二十二一号表一致(AchievementId id, string 期望名称)
    {
        Assert.Equal(期望名称, AchievementCatalog.Of(id).Name);
    }

    [Theory]
    [InlineData(AchievementId.NarrowEscape, AchievementSeries.Emerging, AchievementSource.Cumulative)]
    [InlineData(AchievementId.JuRenThree, AchievementSeries.ExamProgression, AchievementSource.Degree)]
    [InlineData(AchievementId.VenerableScholar, AchievementSeries.OfficialCareer, AchievementSource.Exam)]
    [InlineData(AchievementId.FiftyBirths, AchievementSeries.Lineage, AchievementSource.Cumulative)]
    [InlineData(AchievementId.CollectivePunishment, AchievementSeries.CollectiveRuin, AchievementSource.Punishment)]
    [InlineData(AchievementId.ReliefRecoveredShortfall, AchievementSeries.Famine, AchievementSource.Famine)]
    [InlineData(AchievementId.DebugWakeHundred, AchievementSeries.DebugSecrets, AchievementSource.Pending)]
    public void 锚点行的系列与来源族与契约二十二一号表一致(
        AchievementId id,
        AchievementSeries 期望系列,
        AchievementSource 期望来源)
    {
        var 条目 = AchievementCatalog.Of(id);

        Assert.Equal(期望系列, 条目.Series);
        Assert.Equal(期望来源, 条目.Source);
    }

    [Theory]
    [InlineData(AchievementSeries.Emerging, 12)]
    [InlineData(AchievementSeries.ExamProgression, 8)]
    [InlineData(AchievementSeries.OfficialCareer, 11)]
    [InlineData(AchievementSeries.Lineage, 17)]
    [InlineData(AchievementSeries.CollectiveRuin, 11)]
    [InlineData(AchievementSeries.Famine, 4)]
    [InlineData(AchievementSeries.DebugSecrets, 5)]
    public void 七系列的规范行数与目录逐行计数一致(AchievementSeries series, int 规范行数)
    {
        Assert.Equal(规范行数, AchievementCatalog.CountFor(series));
        Assert.Equal(规范行数, AchievementCatalog.All.Count(条目 => 条目.Series == series));
    }

    [Fact]
    public void 七系列计数合计六十八且系列枚举恰有七个成员()
    {
        Assert.Equal(七系列.Length, Enum.GetValues<AchievementSeries>().Length);

        var 合计 = 0;
        foreach (var 系列 in 七系列)
        {
            合计 += AchievementCatalog.CountFor(系列);
        }

        Assert.Equal(成就总数, 合计);
    }

    [Fact]
    public void 标识与目录条目双向一一对应()
    {
        // 每条已定义标识恰对应一条目录行。
        foreach (var id in Enum.GetValues<AchievementId>())
        {
            Assert.Equal(id, AchievementCatalog.Of(id).Id);
        }

        // 每条目录行的标识都是已定义成员，且没有两条行共用同一个标识。
        foreach (var 条目 in AchievementCatalog.All)
        {
            Assert.True(Enum.IsDefined(条目.Id), $"{条目.Code} 的标识不是已定义成员。");
        }

        Assert.Equal(成就总数, AchievementCatalog.All.Select(条目 => 条目.Id).Distinct().Count());
    }

    [Fact]
    public void 待接线来源恰为商海浮沉与调试秘辛五条()
    {
        var 期望标识 = new[]
        {
            AchievementId.TradeLoss,
            AchievementId.DebugWakeOne,
            AchievementId.DebugWakeTen,
            AchievementId.DebugWakeHundred,
            AchievementId.DebugWakeThousand,
            AchievementId.DebugWakeTenThousand,
        };

        var 期望代码 = new[] { "ACH-041", "ACH-064", "ACH-065", "ACH-066", "ACH-067", "ACH-068" };

        var 实得 = AchievementCatalog.All
            .Where(条目 => 条目.Source == AchievementSource.Pending)
            .ToArray();

        Assert.Equal(期望标识, 实得.Select(条目 => 条目.Id).ToArray());
        Assert.Equal(期望代码, 实得.Select(条目 => 条目.Code).ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(69)]
    [InlineData(999)]
    public void 未登记的标识取定义时抛参数越界异常(int 原始值)
    {
        var 异常 = Assert.Throws<ArgumentOutOfRangeException>(
            () => AchievementCatalog.Of((AchievementId)原始值));

        Assert.Equal("id", 异常.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void 未定义的系列取计数时抛参数越界异常(int 原始值)
    {
        var 异常 = Assert.Throws<ArgumentOutOfRangeException>(
            () => AchievementCatalog.CountFor((AchievementSeries)原始值));

        Assert.Equal("series", 异常.ParamName);
    }
}
