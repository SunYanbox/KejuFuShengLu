using KFL.Core.Enums;

namespace KFL.Rules.Achievements;

/// <summary>
/// 一条成就的定义：标识三元组（<see cref="Id"/> / <see cref="Code"/> / <see cref="Name"/>）
/// 加系列与来源族（契约二十二 §1）。
/// </summary>
/// <param name="Id">成就标识；数值等于 <paramref name="Code"/> 的十进制序号。</param>
/// <param name="Code">稳定外部标识（<c>ACH-001</c>~<c>ACH-068</c>）；一旦发布 MUST NOT 变更或复用。</param>
/// <param name="Name">中文名；<b>只供展示与测试可读性</b>，MUST NOT 出现在线上键或判据里。</param>
/// <param name="Series">系列归属（唯一真源 = 规格书 §14.1 的七个系列标题）。</param>
/// <param name="Source">来源族；文档性元数据，MUST NOT 参与判定分支。</param>
public sealed record AchievementDefinition(
    AchievementId Id,
    string Code,
    string Name,
    AchievementSeries Series,
    AchievementSource Source);

/// <summary>
/// 成就目录：68 条成就的登记处（契约二十二 §1）。
/// </summary>
/// <remarks>
/// <para>
/// <b>唯一副本</b>：契约二十二 §1 是「成员名 ↔ <c>ACH-xxx</c> ↔ 中文名」三元组的唯一登记处；
/// 本类是它在代码里的<b>唯一</b>载体。MUST NOT 在判据、测试或其它文档里再抄一份
/// （<c>data-model.md</c> §3.2）。
/// </para>
/// <para>
/// <b>只读</b>：全部条目在类型初始化时一次成型，无任何可变状态，不读时钟 / 文件 / 随机（G-07）。
/// </para>
/// <para>
/// <b>判据不在这里</b>：本类只回答「有哪些成就、叫什么、属于哪个系列」；「是否成立」由
/// <see cref="AchievementPredicates.IsSatisfied"/> 回答。
/// </para>
/// </remarks>
public static class AchievementCatalog
{
    private static readonly AchievementDefinition[] Definitions =
    [
        new(AchievementId.FoundationLaid, "ACH-001", "开基立业", AchievementSeries.Emerging, AchievementSource.Creation),
        new(AchievementId.FirstJuRen, "ACH-002", "首个举人", AchievementSeries.Emerging, AchievementSource.Degree),
        new(AchievementId.FirstJinShi, "ACH-003", "首个进士", AchievementSeries.Emerging, AchievementSource.Degree),
        new(AchievementId.GoldenList, "ACH-004", "金榜题名", AchievementSeries.Emerging, AchievementSource.Degree),
        new(AchievementId.ThreeGenerations, "ACH-005", "三代同堂", AchievementSeries.Emerging, AchievementSource.Household),
        new(AchievementId.FiveGenerations, "ACH-006", "五世其昌", AchievementSeries.Emerging, AchievementSource.Household),
        new(AchievementId.TenThousandGuan, "ACH-007", "家资万贯", AchievementSeries.Emerging, AchievementSource.Asset),
        new(AchievementId.WealthOfTheRegion, "ACH-008", "富甲一方", AchievementSeries.Emerging, AchievementSource.Asset),
        new(AchievementId.Longevity, "ACH-009", "寿比南山", AchievementSeries.Emerging, AchievementSource.Lifecycle),
        new(AchievementId.CleanRecord, "ACH-010", "清白传家", AchievementSeries.Emerging, AchievementSource.Punishment),
        new(AchievementId.PrisonRelease, "ACH-011", "铁窗生涯", AchievementSeries.Emerging, AchievementSource.Punishment),
        new(AchievementId.NarrowEscape, "ACH-012", "绝处逢生", AchievementSeries.Emerging, AchievementSource.Cumulative),
        new(AchievementId.JuRenThree, "ACH-013", "杏林初茂", AchievementSeries.ExamProgression, AchievementSource.Degree),
        new(AchievementId.JuRenTen, "ACH-014", "桂子飘香", AchievementSeries.ExamProgression, AchievementSource.Degree),
        new(AchievementId.JuRenHundred, "ACH-015", "满门朱紫", AchievementSeries.ExamProgression, AchievementSource.Degree),
        new(AchievementId.JuRenThousand, "ACH-016", "文运昌隆", AchievementSeries.ExamProgression, AchievementSource.Degree),
        new(AchievementId.JinShiThree, "ACH-017", "雁塔题名", AchievementSeries.ExamProgression, AchievementSource.Degree),
        new(AchievementId.JinShiTen, "ACH-018", "簪缨世族", AchievementSeries.ExamProgression, AchievementSource.Degree),
        new(AchievementId.JinShiHundred, "ACH-019", "朝堂半壁", AchievementSeries.ExamProgression, AchievementSource.Degree),
        new(AchievementId.JinShiThousand, "ACH-020", "天下文宗", AchievementSeries.ExamProgression, AchievementSource.Degree),
        new(AchievementId.FirstAppointment, "ACH-021", "释褐登仕", AchievementSeries.OfficialCareer, AchievementSource.Career),
        new(AchievementId.Prefect, "ACH-022", "牧守一方", AchievementSeries.OfficialCareer, AchievementSource.Career),
        new(AchievementId.Chancellor, "ACH-023", "位极人臣", AchievementSeries.OfficialCareer, AchievementSource.Career),
        new(AchievementId.Retirement, "ACH-024", "悬车致仕", AchievementSeries.OfficialCareer, AchievementSource.Career),
        new(AchievementId.OfficeHolders, "ACH-025", "袍笏登场", AchievementSeries.OfficialCareer, AchievementSource.Career),
        new(AchievementId.Merit, "ACH-026", "政通人和", AchievementSeries.OfficialCareer, AchievementSource.Career),
        new(AchievementId.JieShiFailuresThree, "ACH-027", "屡挫屡奋", AchievementSeries.OfficialCareer, AchievementSource.Exam),
        new(AchievementId.VenerableScholar, "ACH-028", "皓首穷经", AchievementSeries.OfficialCareer, AchievementSource.Exam),
        new(AchievementId.ImperialSweep, "ACH-029", "鼎甲包揽", AchievementSeries.OfficialCareer, AchievementSource.Degree),
        new(AchievementId.ExamFailuresTwenty, "ACH-030", "科场遗珠", AchievementSeries.OfficialCareer, AchievementSource.Exam),
        new(AchievementId.FallenAtShengShi, "ACH-031", "名落孙山", AchievementSeries.OfficialCareer, AchievementSource.Exam),
        new(AchievementId.LargeHousehold, "ACH-032", "人丁兴旺", AchievementSeries.Lineage, AchievementSource.Household),
        new(AchievementId.FiftyBirths, "ACH-033", "百子千孙", AchievementSeries.Lineage, AchievementSource.Cumulative),
        new(AchievementId.ImperialGroomPick, "ACH-034", "榜下捉婿", AchievementSeries.Lineage, AchievementSource.Lifecycle),
        new(AchievementId.DegreeMarriage, "ACH-035", "秦晋之好", AchievementSeries.Lineage, AchievementSource.Lifecycle),
        new(AchievementId.DeathInChildbirth, "ACH-036", "红颜薄命", AchievementSeries.Lineage, AchievementSource.Lifecycle),
        new(AchievementId.InfantDeath, "ACH-037", "襁褓之殇", AchievementSeries.Lineage, AchievementSource.Lifecycle),
        new(AchievementId.FarmlandHundred, "ACH-038", "阡陌交通", AchievementSeries.Lineage, AchievementSource.Asset),
        new(AchievementId.FarmlandFiveHundred, "ACH-039", "良田万顷", AchievementSeries.Lineage, AchievementSource.Asset),
        new(AchievementId.FirstShop, "ACH-040", "旺铺之主", AchievementSeries.Lineage, AchievementSource.Asset),
        new(AchievementId.TradeLoss, "ACH-041", "商海浮沉", AchievementSeries.Lineage, AchievementSource.Pending),
        new(AchievementId.DebtBurden, "ACH-042", "债台高筑", AchievementSeries.Lineage, AchievementSource.Asset),
        new(AchievementId.DebtFree, "ACH-043", "无债一身轻", AchievementSeries.Lineage, AchievementSource.Cumulative),
        new(AchievementId.FamilyExtinct, "ACH-044", "烟消云散", AchievementSeries.Lineage, AchievementSource.Ending),
        new(AchievementId.CenturyHouse, "ACH-045", "百年世家", AchievementSeries.Lineage, AchievementSource.Cumulative),
        new(AchievementId.MillenniumHouse, "ACH-046", "千秋万代", AchievementSeries.Lineage, AchievementSource.Cumulative),
        new(AchievementId.AllFourOrigins, "ACH-047", "四民皆历", AchievementSeries.Lineage, AchievementSource.GlobalArchive),
        new(AchievementId.HellEnding, "ACH-048", "大梦一场", AchievementSeries.Lineage, AchievementSource.Ending),
        new(AchievementId.FirstSuccessfulBribe, "ACH-049", "投石问路", AchievementSeries.CollectiveRuin, AchievementSource.Punishment),
        new(AchievementId.BribeThree, "ACH-050", "暗度陈仓", AchievementSeries.CollectiveRuin, AchievementSource.Cumulative),
        new(AchievementId.BribeTen, "ACH-051", "权钱交易", AchievementSeries.CollectiveRuin, AchievementSource.Cumulative),
        new(AchievementId.BribeHundred, "ACH-052", "只手遮天", AchievementSeries.CollectiveRuin, AchievementSource.Cumulative),
        new(AchievementId.BribeThousand, "ACH-053", "买断功名", AchievementSeries.CollectiveRuin, AchievementSource.Cumulative),
        new(AchievementId.BribeCapReached, "ACH-054", "无法无天", AchievementSeries.CollectiveRuin, AchievementSource.Punishment),
        new(AchievementId.ExecutedForBribe, "ACH-055", "身首异处", AchievementSeries.CollectiveRuin, AchievementSource.Punishment),
        new(AchievementId.CollectivePunishment, "ACH-056", "覆巢之下", AchievementSeries.CollectiveRuin, AchievementSource.Punishment),
        new(AchievementId.MarriedOutPunished, "ACH-057", "殃及池鱼", AchievementSeries.CollectiveRuin, AchievementSource.Punishment),
        new(AchievementId.FamilyWideBan, "ACH-058", "斯文扫地", AchievementSeries.CollectiveRuin, AchievementSource.Punishment),
        new(AchievementId.ThreePrisoners, "ACH-059", "囹圄之灾", AchievementSeries.CollectiveRuin, AchievementSource.Punishment),
        new(AchievementId.FirstRelief, "ACH-060", "嗟来之食", AchievementSeries.Famine, AchievementSource.Famine),
        new(AchievementId.ReliefRecoveredPaidInFull, "ACH-061", "枯木逢春", AchievementSeries.Famine, AchievementSource.Famine),
        new(AchievementId.ReliefRecoveredShortfall, "ACH-062", "沉疴难起", AchievementSeries.Famine, AchievementSource.Famine),
        new(AchievementId.FamineDeath, "ACH-063", "路有冻骨", AchievementSeries.Famine, AchievementSource.Famine),
        new(AchievementId.DebugWakeOne, "ACH-064", "天机泄露", AchievementSeries.DebugSecrets, AchievementSource.Pending),
        new(AchievementId.DebugWakeTen, "ACH-065", "窥探虚实", AchievementSeries.DebugSecrets, AchievementSource.Pending),
        new(AchievementId.DebugWakeHundred, "ACH-066", "篡改命数", AchievementSeries.DebugSecrets, AchievementSource.Pending),
        new(AchievementId.DebugWakeThousand, "ACH-067", "虚空主宰", AchievementSeries.DebugSecrets, AchievementSource.Pending),
        new(AchievementId.DebugWakeTenThousand, "ACH-068", "代码飞升", AchievementSeries.DebugSecrets, AchievementSource.Pending),
    ];

    private static readonly Dictionary<AchievementId, AchievementDefinition> ById =
        Definitions.ToDictionary(definition => definition.Id);

    /// <summary>全部 68 条，按 <see cref="AchievementId"/> 升序。</summary>
    public static IReadOnlyList<AchievementDefinition> All { get; } = Definitions;

    /// <summary>取一条成就的定义。</summary>
    /// <param name="id">成就标识。</param>
    /// <returns>该标识的定义。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> 不是已定义的成员。</exception>
    public static AchievementDefinition Of(AchievementId id) =>
        ById.TryGetValue(id, out var definition)
            ? definition
            : throw new ArgumentOutOfRangeException(nameof(id), id, "未定义的成就标识。");

    /// <summary>某个系列的行数（用于七系列计数断言）。</summary>
    /// <param name="series">系列。</param>
    /// <returns>该系列的条目数。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="series"/> 不是已定义的成员。</exception>
    public static int CountFor(AchievementSeries series)
    {
        if (!Enum.IsDefined(series))
        {
            throw new ArgumentOutOfRangeException(nameof(series), series, "未定义的成就系列。");
        }

        var count = 0;
        foreach (var definition in Definitions)
        {
            if (definition.Series == series)
            {
                count++;
            }
        }

        return count;
    }
}
