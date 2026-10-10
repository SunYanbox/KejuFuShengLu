using KFL.Core.Enums;
using KFL.Infrastructure.Abstractions;

namespace KFL.Infrastructure.Services;

/// <summary>
/// 内置宋风姓名生成器（规格书 §2 的「内置宋风姓氏 / 名字字库兜底」，**产品默认路径**；FR-009）。
/// </summary>
/// <remarks>
/// <para>
/// **全部随机经注入的 <see cref="IRandomService"/>**：本类 MUST NOT 触碰任何全局随机源，
/// 同种子构造两次即得到逐项相同的取值序列（FR-011）。
/// </para>
/// <para>
/// 字库本身是**文本**而不是规则数值：它不含任何游戏平衡数值（SC-009 的扫描判据按数值识别，
/// 字符串内容也被抹除）。字库的对外可见面只有 <see cref="Surnames"/> /
/// <see cref="MaleGivenNames"/> / <see cref="FemaleGivenNames"/>，供单测作白名单断言。
/// </para>
/// </remarks>
public sealed class SongStyleNameGenerator : INameGenerator
{
    /// <summary>宋风姓氏字库（规格书 §12.4 的兜底实现；只作<b>随机取一</b>的来源）。</summary>
    public static readonly IReadOnlyList<string> Surnames =
    [
        "赵", "钱", "孙", "李", "周", "吴", "郑", "王", "冯", "陈",
        "褚", "卫", "蒋", "沈", "韩", "杨", "朱", "秦", "许", "何",
        "吕", "施", "张", "孔", "曹", "严", "华", "金", "魏", "陶",
        "姜", "戚", "谢", "邹", "喻", "柏", "窦", "章", "苏", "潘",
        "葛", "范", "彭", "鲁", "韦", "马", "苗", "方", "俞", "任",
        "袁", "柳", "鲍", "史", "唐", "费", "岑", "薛", "雷", "贺",
        "倪", "汤", "罗", "毕", "郝", "安", "常", "于", "傅", "卞",
        "齐", "康", "伍", "余", "顾", "孟", "黄", "穆", "萧", "尹",
        "姚", "邵", "汪", "祁", "毛", "狄", "米", "贝", "明", "臧",
        "成", "戴", "谈", "宋", "庞", "熊", "纪", "舒", "屈", "项",
    ];

    /// <summary>宋风男性名字库（每项都是完整的名）。</summary>
    public static readonly IReadOnlyList<string> MaleGivenNames =
    [
        "文远", "子昂", "伯淳", "仲淹", "叔夜", "尧臣", "廷玉", "元晦",
        "明复", "德昭", "景仁", "从简", "慎思", "养浩", "浩然", "致远",
        "承宗", "世昌", "延之", "复礼", "处厚", "得臣", "应辰", "必大",
        "为中", "正叔", "知白", "守节", "大猷", "中行", "立言", "希哲",
        "君实", "资深", "清臣", "师道", "介甫", "安国", "子固", "茂叔",
    ];

    /// <summary>宋风女性名字库（每项都是完整的名）。</summary>
    public static readonly IReadOnlyList<string> FemaleGivenNames =
    [
        "婉清", "淑娴", "静仪", "玉娘", "蕙兰", "素娥", "珍娘", "秀英",
        "昭华", "湘云", "月娥", "星娥", "莲娘", "荷香", "梅英", "竹君",
        "菊仙", "娇娘", "令仪", "若兰", "宜静", "妙音", "清照", "道升",
        "淑真", "幼卿", "婉如", "玉真", "蕊娘", "顺仪", "宝琴", "惜春",
        "含章", "抱月", "拾翠", "流芳", "端容", "徽音", "明月", "雪梅",
    ];

    private readonly IRandomService _random;

    /// <summary>构造并注入随机来源。</summary>
    /// <param name="random">随机来源（姓氏与名的唯一来源）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="random"/> 为 <c>null</c>。</exception>
    public SongStyleNameGenerator(IRandomService random)
    {
        ArgumentNullException.ThrowIfNull(random);
        _random = random;
    }

    /// <inheritdoc />
    public string NextSurname() => Pick(Surnames);

    /// <inheritdoc />
    public string NextGivenName(Gender gender) =>
        Pick(gender == Gender.Male ? MaleGivenNames : FemaleGivenNames);

    /// <summary>从字库里**整数均匀**取一项（含两端点：<c>Next</c> 的上界是开区间）。</summary>
    private string Pick(IReadOnlyList<string> pool) => pool[_random.Next(0, pool.Count)];
}
