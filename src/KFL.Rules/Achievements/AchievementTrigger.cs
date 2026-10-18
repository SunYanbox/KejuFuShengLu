namespace KFL.Rules.Achievements;

/// <summary>
/// 动作型**即时**判定入口（契约二十二 §3，FR-009）：12 个玩家动作各自对应的触发器。
/// </summary>
/// <remarks>
/// <para>
/// <b>唯一副本</b>：契约二十二 §3 的表是 FR-009 清单的唯一定义处；本枚举是它在代码里的唯一载体。
/// 新增玩家动作入口时 MUST 同步该表与本枚举（跨特性形状变更，按 AGENTS.md 铁律在同一提交回写）。
/// </para>
/// <para>
/// <b>零值语义</b>：<see cref="None"/> = 「非触发路径」（回合末判定、读档复原等）。故首个真实触发器
/// 从 1 起——这与 <c>CollectiveOutcome</c>「不设零值」的决定相反，因为本枚举是**调用方**参数，
/// 需要一个可表达的「不触发任何动作」默认值。
/// </para>
/// <para>
/// <b>不是持久数据</b>：本枚举 MUST NOT 出现在存档 / 全局档案的任何键上（判定是瞬时的）。
/// </para>
/// </remarks>
public enum AchievementTrigger
{
    /// <summary>非触发路径（回合末判定、读档复原、显式不触发）。</summary>
    None = 0,

    /// <summary>新局创建成功（契约二十二 §3 行 1）；直接受益：<c>ACH-001</c>。</summary>
    NewGameCreated = 1,

    /// <summary>资产买入成功（行 2）；直接受益：<c>ACH-038</c>~<c>ACH-040</c>、<c>ACH-007</c>/<c>ACH-008</c>。</summary>
    AssetPurchased = 2,

    /// <summary>资产卖出成功（行 3）；直接受益：同买入（卖出亦可抬高现金）。</summary>
    AssetSold = 3,

    /// <summary>缺口转贷款发生（行 4）；直接受益：<c>ACH-042</c>、<c>ACH-043</c>。</summary>
    LoanOriginated = 4,

    /// <summary>贷款提前清偿成功（行 5）；直接受益：<c>ACH-043</c>。</summary>
    LoanRepaid = 5,

    /// <summary>托媒发起成功（行 6）；直接受益：<c>ACH-035</c>（若当月成交）。</summary>
    MatchmakingCommenced = 6,

    /// <summary>提议应答成功（接受或拒绝，行 7）；直接受益：<c>ACH-035</c>。</summary>
    ProposalAnswered = 7,

    /// <summary>买功名婚姻发生（行 8）；直接受益：<c>ACH-035</c>。</summary>
    DegreeMarriagePurchased = 8,

    /// <summary>买人成功（行 9）；直接受益：<c>ACH-032</c>。</summary>
    PersonPurchased = 9,

    /// <summary>行贿提交成功（行 10）；直接受益：<c>ACH-049</c>~<c>ACH-054</c>。</summary>
    BribeSubmitted = 10,

    /// <summary>特奏名问询应答成功（行 11）；直接受益：<c>ACH-027</c>、<c>ACH-028</c>。</summary>
    ExamInquiryAnswered = 11,

    /// <summary>调试控制台唤醒上报（行 12，U4 未来调用）；直接受益：<c>ACH-064</c>~<c>ACH-068</c>。</summary>
    DebugWakeReported = 12,
}
