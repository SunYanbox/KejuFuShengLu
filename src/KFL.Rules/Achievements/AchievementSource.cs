namespace KFL.Rules.Achievements;

/// <summary>
/// 成就的「来源族」：说明一条成就主要读哪一类事实（契约二十二 §1 的最后一列）。
/// </summary>
/// <remarks>
/// <para>
/// <b>文档性元数据</b>：本枚举只用于目录登记与测试可读性，MUST NOT 参与判定路径的分支——
/// <see cref="AchievementPredicates.IsSatisfied"/> 的分派键是
/// <see cref="KFL.Core.Enums.AchievementId"/>，不是来源族（`data-model.md` §3.2）。
/// </para>
/// <para>
/// <b><see cref="Pending"/> = 待接线</b>：<c>TradeLoss</c>（ACH-041）与调试秘辛 5 条
/// （ACH-064~068）在本特性交付时<b>恒不解锁</b>——判据存在，但没有产生者（契约二十二 §4 条款 12；
/// FR-042）。测试 MUST 断言「未接线 ⇒ 不解锁」，MUST NOT 造任何假数据。
/// </para>
/// </remarks>
public enum AchievementSource
{
    /// <summary>新局创建（ACH-001）。</summary>
    Creation = 0,

    /// <summary>功名记录（举人 / 进士 / 鼎甲名次）。</summary>
    Degree = 1,

    /// <summary>家族资产（现金与产业合计）。</summary>
    Asset = 2,

    /// <summary>在册人口与世代结构。</summary>
    Household = 3,

    /// <summary>官职与仕途。</summary>
    Career = 4,

    /// <summary>科场表现（失败累计、特奏名授官等）。</summary>
    Exam = 5,

    /// <summary>刑罚与贿赂后果。</summary>
    Punishment = 6,

    /// <summary>饥馑与救济。</summary>
    Famine = 7,

    /// <summary>007 生命历程的当月事实（婚育、寿终、外嫁等）。</summary>
    Lifecycle = 8,

    /// <summary>由 <see cref="KFL.Core.Entities.AchievementState"/> 的累计量判定。</summary>
    Cumulative = 9,

    /// <summary>结局（绝嗣）。</summary>
    Ending = 10,

    /// <summary>跨档全局档案（<c>achievements.json</c> 的出身达成段）。</summary>
    GlobalArchive = 11,

    /// <summary>待接线：本次交付恒不解锁。</summary>
    Pending = 12,
}
