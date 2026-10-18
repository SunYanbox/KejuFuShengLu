using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Bribery;
using KFL.Rules.FamilyLifecycle;

namespace KFL.Presentation;

/// <summary>下拉框选项（把枚举 + 中文名一起绑给 ComboBox）。</summary>
public sealed class Choice<T>(T value, string text)
{
    public T Value { get; } = value;

    public string Text { get; } = text;

    public override string ToString() => Text;
}

/// <summary>全部界面文案与数值格式化（集中一处，避免散落的分支）。</summary>
public static class Display
{
    public static string Money(Money money)
    {
        var guan = money.Guan;
        return Math.Abs(guan) >= 1m ? $"{guan:0.##} 贯" : $"{money.Wen:0.#} 文";
    }

    public static string Origin(Origin origin) => origin switch
    {
        KFL.Core.Enums.Origin.Farmer => "农户",
        KFL.Core.Enums.Origin.Artisan => "匠户",
        KFL.Core.Enums.Origin.Merchant => "商户",
        KFL.Core.Enums.Origin.Scholar => "士人",
        _ => origin.ToString(),
    };

    public static string Difficulty(Difficulty difficulty) => difficulty switch
    {
        KFL.Core.Enums.Difficulty.Easy => "简单",
        KFL.Core.Enums.Difficulty.Normal => "普通",
        KFL.Core.Enums.Difficulty.Hard => "困难",
        KFL.Core.Enums.Difficulty.Hell => "地狱",
        _ => difficulty.ToString(),
    };

    public static string Degree(DegreeLevel degree) => degree switch
    {
        DegreeLevel.BaiShen => "白身",
        DegreeLevel.JuRen => "举人",
        DegreeLevel.GongShi => "贡士",
        DegreeLevel.JinShi => "进士",
        _ => degree.ToString(),
    };

    public static string Gender(KFL.Core.Enums.Gender gender)
        => gender == KFL.Core.Enums.Gender.Male ? "男" : "女";

    public static string LivingStandard(LivingStandard standard) => standard switch
    {
        KFL.Core.Enums.LivingStandard.Frugal => "拮据",
        KFL.Core.Enums.LivingStandard.Normal => "普通",
        KFL.Core.Enums.LivingStandard.Comfortable => "体面",
        _ => standard.ToString(),
    };

    public static string ExamLevel(ExamLevel level) => level switch
    {
        KFL.Core.Enums.ExamLevel.JieShi => "解试",
        KFL.Core.Enums.ExamLevel.ShengShi => "省试",
        KFL.Core.Enums.ExamLevel.DianShi => "殿试",
        _ => level.ToString(),
    };

    public static string Asset(AssetKind kind) => kind switch
    {
        KFL.Core.Enums.AssetKind.Farmland => "田亩",
        KFL.Core.Enums.AssetKind.RuralHouse => "农舍",
        KFL.Core.Enums.AssetKind.UrbanHouse => "城市宅",
        KFL.Core.Enums.AssetKind.Shop => "商铺",
        _ => kind.ToString(),
    };

    public static string Series(AchievementSeries series) => series switch
    {
        AchievementSeries.Emerging => "初露锋芒",
        AchievementSeries.ExamProgression => "科名累进",
        AchievementSeries.OfficialCareer => "宦海沉浮",
        AchievementSeries.Lineage => "宗族血脉",
        AchievementSeries.CollectiveRuin => "覆巢之下",
        AchievementSeries.Famine => "饥馑求生",
        AchievementSeries.DebugSecrets => "调试秘辛",
        _ => series.ToString(),
    };

    public static string Placement(ImperialPlacement? placement) => placement switch
    {
        ImperialPlacement.ZhuangYuan => "一甲第一名（状元）",
        ImperialPlacement.BangYan => "一甲第二名（榜眼）",
        ImperialPlacement.TanHua => "一甲第三名（探花）",
        _ => string.Empty,
    };

    /// <summary>一甲名次配色（规格书 §12.2：第 1 金 / 第 2 紫 / 第 3 蓝）。</summary>
    public static string PlacementBrushKey(ImperialPlacement? placement) => placement switch
    {
        ImperialPlacement.ZhuangYuan => "PlacementGoldBrush",
        ImperialPlacement.BangYan => "PlacementPurpleBrush",
        ImperialPlacement.TanHua => "PlacementBlueBrush",
        _ => "PlacementInkBrush",
    };

    /// <summary>行贿拒绝原因（规格书 §7；契约十五 §2）——界面 MUST NOT 直接展示枚举名。</summary>
    public static string BribeReason(BribeRejectReason reason) => reason switch
    {
        BribeRejectReason.None => "可行",
        BribeRejectReason.OutsideWindow => "不在窗口（考试当月及前一月）",
        BribeRejectReason.NotRegistered => "不在家族册上",
        BribeRejectReason.LevelMismatch => "与当前功名不匹配",
        BribeRejectReason.CapReached => "该级别已触顶",
        BribeRejectReason.InsufficientFunds => "现银+储蓄不足（不转贷款）",
        _ => "不可行",
    };

    public static string LifecycleReason(LifecycleRejectReason reason) => reason switch
    {
        LifecycleRejectReason.None => "可行",
        LifecycleRejectReason.NotMarriageable => "不满足婚配条件",
        LifecycleRejectReason.InsufficientFunds => "银钱不足",
        LifecycleRejectReason.NoLowerDegree => "无可买功名（需现任官员）",
        LifecycleRejectReason.FertilityWindowClosed => "生育窗口已关",
        LifecycleRejectReason.AlreadyAppliedThisYear => "本年已申请",
        LifecycleRejectReason.HeadVacant => "家主空缺",
        LifecycleRejectReason.NoPendingProposal => "无待应答提亲",
        LifecycleRejectReason.NotHead => "须家主在册",
        LifecycleRejectReason.NoRegisteredMale => "无在册男丁",
        LifecycleRejectReason.AlreadyAppliedThisMonth => "本月已托媒",
        _ => reason.ToString(),
    };

    public static string StatusOf(KFL.Core.Entities.Person person, GameDate date)
    {
        var flags = new List<string>();
        if (person.Status.HasFlag(StatusFlag.Deceased))
        {
            return "身故";
        }

        if (person.Status.HasFlag(StatusFlag.Ill))
        {
            flags.Add("患病");
        }

        if (person.Status.HasFlag(StatusFlag.ServingSentence))
        {
            flags.Add($"服刑({person.Timers.SentenceRemainingMonths}月)");
        }

        if (person.Status.HasFlag(StatusFlag.AwaitingPost))
        {
            flags.Add($"待阙({person.Timers.AwaitingPostRemainingMonths}月)");
        }

        if (person.Status.HasFlag(StatusFlag.ExamBanned))
        {
            flags.Add($"禁考({person.Timers.ExamBanRemainingMonths}月)");
        }

        if (person.Status.HasFlag(StatusFlag.PromotionBanned))
        {
            flags.Add($"禁升({person.Timers.PromotionBanRemainingMonths}月)");
        }

        if (person.Status.HasFlag(StatusFlag.MarriedOut))
        {
            flags.Add("外嫁");
        }

        if (person.Status.HasFlag(StatusFlag.Retired))
        {
            flags.Add("致仕");
        }

        _ = date;
        return flags.Count == 0 ? "在册" : string.Join(' ', flags);
    }
}
