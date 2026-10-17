namespace KFL.Core.Enums;

/// <summary>
/// 待应答提议的来源（规格书 §9.1 / §9.2；007 的 R-25）。
/// </summary>
/// <remarks>
/// 两类提议共用同一份待应答状态（<c>Family.Lifecycle.Proposal</c>），
/// 但金额方向与账本类别不同：<see cref="Matchmaker"/> 是家族向对方支付聘礼 / 收受嫁妆，
/// <see cref="ImperialProposal"/> 是对方**单向支付**给本家族。
/// </remarks>
public enum MarriageProposalKind
{
    /// <summary>随机提亲：⑧-a 第二段（§9.1），概率 <c>5% × 负面事件系数</c>。</summary>
    Matchmaker = 0,

    /// <summary>榜下捉婿：⑧-a 第一段（§9.2），窗口 = 新科一甲及第的次月。</summary>
    ImperialProposal = 1,
}
