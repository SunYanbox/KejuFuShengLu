using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Rules.FamilyLifecycle;

/// <summary>
/// 当月事实段（data-model §3.2；契约十八 §4）：⑧ 人口与健康推进子步产出的**增量**事实。
/// </summary>
/// <remarks>
/// <para>
/// 只描述**当月**、只读、MUST NOT 承载累计值、MUST NOT 回写 <c>GameState</c>。
/// 各集合按 <c>PersonId</c> 升序；<see cref="PendingProposal"/> 是**当月新产出**的待应答提议
/// （过期提议由 <c>LifecycleQueries.PendingProposal</c> 视为 <c>null</c>）。
/// </para>
/// <para>
/// 「死亡原因」只活在这里：<c>Person</c> 字段与账本条目 MUST NOT 出现 <see cref="DeathCause"/>
/// （缓存的 <c>MemberDeceased</c> 事件条目只记身份，不记死因）。
/// </para>
/// </remarks>
public sealed record LifecycleSnapshot
{
    /// <summary>当月新生儿（⑧-b）。</summary>
    public required IReadOnlyList<BirthFact> Births { get; init; }

    /// <summary>当月死亡（⑧-b 分娩 / ⑧-c 病亡 / ⑧-e 饥馑 / ⑧-f 寿终与夭折），按 <c>PersonId</c> 升序。</summary>
    public required IReadOnlyList<DeathFact> Deaths { get; init; }

    /// <summary>当月成婚（⑧-a 捉婿 / 提亲 / 玩家托媒）。</summary>
    public required IReadOnlyList<MarriageFact> Marriages { get; init; }

    /// <summary>当月因嫁出而归档的成员。</summary>
    public required IReadOnlyList<MarriedOutFact> MarriedOut { get; init; }

    /// <summary>当月买入的人口。</summary>
    public required IReadOnlyList<PurchaseFact> Purchases { get; init; }

    /// <summary>当月患病与痊愈（⑧-c）。</summary>
    public required IReadOnlyList<IllnessFact> Illnesses { get; init; }

    /// <summary>当月跨越年龄档的成员（⑧-d 的 0 随机派生事实）。</summary>
    public required IReadOnlyList<AgeBracketCrossFact> Crossings { get; init; }

    /// <summary>当月新产出、待玩家应答的提议（⑧-a）；无则为 <c>null</c>。</summary>
    public required MarriageProposal? PendingProposal { get; init; }

    /// <summary>⑧-h 选出的新家主；未变动为 <c>null</c>（现况不变的家主在任不算变动）。</summary>
    public required PersonId? NewHeadId { get; init; }

    /// <summary>家主之位因无在册男性而空悬（⑧-h）；**不等于绝嗣**。</summary>
    public required bool HeadBecameVacant { get; init; }

    /// <summary>
    /// 空事实段（T022 的骨架期与八子步未命中时使用）。<paramref name="month"/> 仅供调用方自我说明
    /// 「这份空段属于哪个月」，事实段本身不承载年月（只描述当月）。
    /// </summary>
    /// <param name="month">该空段所属的月份。</param>
    /// <returns>全空的事实段。</returns>
    public static LifecycleSnapshot None(GameDate month)
    {
        _ = month;
        return new LifecycleSnapshot
        {
            Births = Array.Empty<BirthFact>(),
            Deaths = Array.Empty<DeathFact>(),
            Marriages = Array.Empty<MarriageFact>(),
            MarriedOut = Array.Empty<MarriedOutFact>(),
            Purchases = Array.Empty<PurchaseFact>(),
            Illnesses = Array.Empty<IllnessFact>(),
            Crossings = Array.Empty<AgeBracketCrossFact>(),
            PendingProposal = null,
            NewHeadId = null,
            HeadBecameVacant = false,
        };
    }
}

/// <summary>当月新生儿事实（⑧-b）。</summary>
/// <param name="PersonId">新生儿。</param>
/// <param name="Gender">性别。</param>
/// <param name="FatherId">父亲。</param>
/// <param name="MotherId">母亲。</param>
public sealed record BirthFact(PersonId PersonId, Gender Gender, PersonId FatherId, PersonId MotherId);

/// <summary>当月死亡事实（⑧-b / ⑧-c / ⑧-e / ⑧-f）。</summary>
/// <param name="PersonId">亡者。</param>
/// <param name="Cause">死因。</param>
public sealed record DeathFact(PersonId PersonId, DeathCause Cause);

/// <summary>当月成婚事实。</summary>
/// <param name="PersonId">本家族一方。</param>
/// <param name="SpouseId">配偶。</param>
/// <param name="Class">配偶阶层。</param>
/// <param name="IsDegreeMarriage">是否为买功名婚姻。</param>
/// <param name="BetrothalGift">聘礼（方向语义见契约十九 §5）。</param>
/// <param name="Dowry">嫁妆（方向语义见契约十九 §5）。</param>
public sealed record MarriageFact(
    PersonId PersonId,
    PersonId SpouseId,
    MarriageClass Class,
    bool IsDegreeMarriage,
    Money BetrothalGift,
    Money Dowry);

/// <summary>当月因嫁出而归档的事实。</summary>
/// <param name="PersonId">外嫁成员。</param>
/// <param name="Class">配偶阶层。</param>
public sealed record MarriedOutFact(PersonId PersonId, MarriageClass Class);

/// <summary>当月买入人口的事实。</summary>
/// <param name="PersonId">买来者。</param>
/// <param name="Ordinal">本年第几名（1 起）。</param>
/// <param name="Amount">实付金额。</param>
public sealed record PurchaseFact(PersonId PersonId, int Ordinal, Money Amount);

/// <summary>当月患病或痊愈的事实（⑧-c）。</summary>
/// <param name="PersonId">成员。</param>
/// <param name="Contracted">当月新患病。</param>
/// <param name="Recovered">当月痊愈。</param>
public sealed record IllnessFact(PersonId PersonId, bool Contracted, bool Recovered);

/// <summary>当月跨越年龄档的事实（⑧-d 派生，0 随机消费）。</summary>
/// <param name="PersonId">成员。</param>
/// <param name="From">月初档位。</param>
/// <param name="To">月末档位。</param>
public sealed record AgeBracketCrossFact(PersonId PersonId, AgeBracket From, AgeBracket To);
