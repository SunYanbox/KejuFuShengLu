using KFL.Core.Enums;
using KFL.Rules.Config;

namespace KFL.Presentation;

/// <summary>
/// 界面按钮的「完整说明」浮窗文本（2026-10-19 第九轮玩家反馈 8：没人知道受特奏名的条件，
/// 鼠标悬浮应显示帮助窗口；托媒等按钮同样需要浮窗给出完整描述）。
/// </summary>
/// <remarks>
/// <para>
/// 全部文本由配置常量拼出，MUST NOT 在 XAML 里硬编码数字——数值真源在
/// <c>KFL.Rules/Config</c>，改数值时浮窗自动跟随。
/// </para>
/// <para>
/// 用 <c>ToolTip="{x:Static presentation:HelpTexts.ExamRegister}"</c> 直接引用即可，
/// 无需经视图模型转发。
/// </para>
/// </remarks>
public static class HelpTexts
{
    /// <summary>「报名」按钮：报名窗口与资格。</summary>
    public static string ExamRegister { get; } =
        $"报名：{ExamPolicy.MinimumAge} 岁及以上、在册（未亡未外嫁）、未服刑、未禁考、非待阙，"
        + "且功名对应下一级考试——白身 → 解试，举人 → 省试，贡士 → 殿试，进士无可再报。\n"
        + "报名窗口 = 考试当月与其前一个月（即「本月或下月是解试/省试月」时可报）。\n"
        + "报名只登记意向，考试在考试月结算时判定（见成员详情的录取率明细）。";

    /// <summary>「受特奏名」按钮：完整资格条件。</summary>
    public static string SpecialTribute { get; } =
        "受特奏名（恩科）：给屡试不第者的一次恩赐入仕。\n"
        + "需同时满足四条：\n"
        + $"① 年满 {ExamPolicy.SpecialTributeMinimumAge} 岁；\n"
        + $"② 省试累计失败 ≥ {ExamPolicy.SpecialTributeFailureThreshold} 次；\n"
        + "③ 当前无官阶（尚是白身/举人/贡士）；\n"
        + "④ 不在待阙期（未持「待阙」状态）。\n"
        + "满足即直接授官（不入功名史）；不满足时日志给出具体原因。";

    /// <summary>「贿解试 / 贿省试 / 贿殿试」按钮：行贿的作用与后果。</summary>
    public static string Bribe { get; } =
        "行贿：为选中成员在本级考试上打点，抬高该级录取率（详见成员详情的「行贿乘数」一路）。\n"
        + "费用随级别与次数递增（解试 100 贯 / 省试 500 贯 / 殿试 2000 贯起）；\n"
        + "被查处会记入案件并可能带来禁考、禁升等处罚——行贿次数越多风险越大。";

    /// <summary>「托媒」按钮：完整婚配描述。</summary>
    public static string Matchmake { get; } =
        $"托媒：为选中成员向本代、异性、未婚、在册的成年成员择偶，支付媒金 {MarriagePolicy.MatchmakerFeeGuan} 贯"
        + $"（再乘支出系数），成功率 {Percent(MarriagePolicy.MatchmakerSuccessChance)}。\n"
        + $"成功后对方家庭会提亲：随机提亲的应允率基础值 {Percent(MarriagePolicy.ProposalBaseChance)}"
        + $"（再按难度与资产折算）；「买功名婚姻」按年俸 × {MarriagePolicy.DegreeMarriageFeeRatio} 加价，"
        + $"应允率提升到 {Percent(MarriagePolicy.ImperialProposalBaseChance)}。\n"
        + $"候选者的年龄差上限 {MarriagePolicy.CandidateAgeSpread} 岁；同代内婚走「族内通婚」下拉框。";

    /// <summary>「买功名婚姻」按钮：加价换应允率。</summary>
    public static string DegreeMarriage { get; } =
        $"买功名婚姻：在托媒之外，按年俸 × {MarriagePolicy.DegreeMarriageFeeRatio} 追加聘财，"
        + $"把对方家庭的应允率从 {Percent(MarriagePolicy.ProposalBaseChance)} 提到 {Percent(MarriagePolicy.ImperialProposalBaseChance)}。\n"
        + "适用于功名较低、需要靠聘财打动对方士族的场合。";

    /// <summary>「应允提亲 / 婉拒提亲」按钮。</summary>
    public static string Proposal { get; } =
        "应允提亲：接受当前待决的提亲，双方成婚并支付聘礼 / 收下嫁妆（按平民 / 士人 / 仕族档位）。\n"
        + "婉拒提亲：拒绝本次提亲，对方不再保留意向；不产生费用，但也不会有嫁妆进项。\n"
        + "「买功名应允」= 应允时按功名加价，换取对方更痛快的同意。";

    /// <summary>「申请生育」按钮：完整生育描述。</summary>
    public static string Childbirth { get; } =
        $"申请生育：为选中成员与其配偶申请生育，支付申请费 {ChildbirthPolicy.ChildbirthExpenseGuan} 贯（再乘支出系数）。\n"
        + $"女方需在 {ChildbirthPolicy.FertilityAgeMin}~{ChildbirthPolicy.FertilityAgeMax} 岁的生育窗口内，且已婚、在册。\n"
        + $"每次分娩有 {Percent(ChildbirthPolicy.ChildbirthDeathChance)} 的难产死亡概率（再乘难度分档）；"
        + $"子女在出生当月、满 1 岁、满 2 岁共判定 {ChildbirthPolicy.InfantCheckCount} 次夭折"
        + $"（{ChildbirthPolicy.InfantExemptAge} 岁后不再判定）。";

    /// <summary>「收养」按钮：完整收养描述（术语见规格书 §17 第六轮）。</summary>
    public static string Adopt { get; } =
        "收养：绝嗣危机时的快速补充手段，从族外收养一名成员入族。\n"
        + $"被收养者年龄 {PopulationPurchasePolicy.AgeMin}~{PopulationPurchasePolicy.AgeMax} 岁，性别随机，"
        + "冠本族姓氏，辈分取家族最新一代（可被立为家主嫡系），视为正式成员。\n"
        + "本年度第 n 名的费用为 10 × n 贯（现金优先，不足部分从储蓄扣）；收养成员及其组内通婚所生的子女，"
        + "其身份标记口径见家谱「收养标记」开关。";

    /// <summary>「就医」按钮：疗程与费用。</summary>
    public static string Treat { get; } =
        $"就医：为选中的患病成员开一个疗程（{IllnessPolicy.TreatmentMonths} 个月）。\n"
        + $"药费 = {IllnessPolicy.MedicineBaseGuan} 贯 + 病情严重度 × {IllnessPolicy.MedicineSeverityGuan}（再乘支出系数）。\n"
        + $"疗程期间康复率提升（系数 {IllnessPolicy.TreatedRecoveryCoefficient} + {IllnessPolicy.TreatedRecoveryOffset}），"
        + "健康条与「治疗中」标志见家谱卡片与成员详情。";

    /// <summary>「名师开关」按钮。</summary>
    public static string Tutor { get; } =
        $"名师：为选中成员聘师，每月束脩 {GrowthPolicy.TutorFeeGuanPerMonth} 贯（再乘支出系数）。\n"
        + "受教期间学业有额外增长；是否启用是家族级开关，按自然月结算扣费，可在成员详情与家谱卡片上看到「名师」标志。";

    /// <summary>「立为家主」按钮。</summary>
    public static string SetHead { get; } =
        "立为家主：把选中成员立为家主。\n"
        + "要求该成员在册（未亡、未外嫁）；若非顺位继承人选也可强立，但继任顺位仍按「直系优先于旁系」计算。\n"
        + "家主本人会在成员详情标【家主】，家谱卡片名称旁同样标注，并以独立样式标出其嫡系一脉的男性后裔。";

    /// <summary>「快进到科举」按钮。</summary>
    public static string FastForwardExam { get; } =
        "快进到科举：连续推进月份，直到家族中确实有成员可以报名的最近报名窗口"
        + "（解试 / 省试的考试当月及其前一月），停在窗口起点，便于先报名、再推进到考试月。\n"
        + "窗口内无人符合报考条件时会继续往后找，不会空停；途中遇到提亲、生育、疾病等决策会按规则暂停，"
        + "处理后可再次点击继续，只有家族绝嗣才中止。";

    private static string Percent(decimal value)
        => (value * 100m).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%";
}
