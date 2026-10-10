using System.Buffers.Binary;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Services;
using KFL.Rules.Config;

namespace KFL.Rules.Start;

/// <summary>
/// 新建存档的入参（契约六 §1；FR-001）。
/// </summary>
/// <param name="Origin">出身（§10.1）。</param>
/// <param name="Difficulty">难度（§11）。</param>
/// <param name="Surname">家族姓氏；<c>null</c> = 经 <see cref="INameGenerator.NextSurname"/> 随机取一个。</param>
/// <param name="StartDate">起始年月；新建存档 MUST 为 1 年 1 月。</param>
public readonly record struct NewGameRequest(
    Origin Origin,
    Difficulty Difficulty,
    string? Surname,
    GameDate StartDate);

/// <summary>
/// 新建存档的结果**薄视图**（契约六 §1 条款 4）：只持有 <see cref="State"/>，其余全部派生。
/// </summary>
/// <remarks>
/// MUST NOT 另存成员集合或余额副本——<see cref="State"/> 是唯一真源，本类型只是断言用的窗口。
/// </remarks>
public sealed class NewGameSetupResult
{
    /// <summary>构造并持有唯一真源。</summary>
    /// <param name="state">新建的存档级状态。</param>
    internal NewGameSetupResult(GameState state) => State = state;

    /// <summary>唯一真源。</summary>
    public GameState State { get; }

    /// <summary>家主标识（派生自 <see cref="GameState.Family"/>）。</summary>
    public PersonId? HeadId => State.Family.HeadId;

    /// <summary>起始年月（派生自 <see cref="GameState.CurrentDate"/>）。</summary>
    public GameDate StartDate => State.CurrentDate;

    /// <summary>全部成员（派生自 <see cref="GameState.Family"/>，按加入顺序）。</summary>
    public IReadOnlyCollection<Person> Members => State.Family.Members;
}

/// <summary>
/// 新建存档入口（规格书 §10.1、§3；FR-001~FR-011；契约六）——**一次性编排**，与逐月结算分离。
/// </summary>
/// <remarks>
/// <para>
/// **编排次序**（契约六 §2，逐条可断言）：① 姓氏 → ② <see cref="Family"/>(surname) 并按
/// 家主（<see cref="Family.AddFoundingMember"/>）→ 配偶（<see cref="Family.AddOutsider"/>）→
/// 孩子 1..n（<see cref="Family.AddChild"/>）加入 → ③ <see cref="Family.Marry"/> +
/// <see cref="Family.SetHead"/> → ④ 士出身向家主追加一条「举人 / Initial」记录 → ⑤ 组装
/// <see cref="Treasury"/> / <see cref="Holdings"/> / 空 <see cref="Ledger"/> 与
/// <see cref="FamilyEconomy"/> → ⑥ 经 <see cref="GameStateFactory"/> 取唯一标识。
/// </para>
/// <para>
/// **随机消费次序**（契约六 §3）：① 姓氏（仅当 <see cref="NewGameRequest.Surname"/> 为 <c>null</c>）→
/// ② 家主（年龄 → 天赋 农/商/仕/工 → 学业 → 体质 → 寿数 → 姓名）→ ③ 配偶（同序，学业取
/// <c>N(30,15)</c>、体质取 <c>N(85,10)</c>）→ ④ 孩子 i（性别 → 年龄 → 天赋×4 → 学业常量
/// （**不掷骰**）→ 体质 → 寿数 → 姓名）→ ⑤ 存档唯一标识（<c>NextBytes(16)</c>）。
/// 全部随机只来自入参 <see cref="IRandomService"/>。
/// </para>
/// <para>
/// **不落账本条目**（FR-010）：初始资产**只经构造入参**写入 <see cref="Treasury"/> 与
/// <see cref="Holdings"/>，MUST NOT 经 <see cref="FamilyEconomy.Apply"/>——开局资产是**初始余额**，
/// 不是一笔收入。
/// </para>
/// <para>
/// **明确不含**：界面与存档落盘（界面轨 U1 / 逻辑轨 ④）、「仕」身份的授予（逻辑轨 ⑤）、
/// 孩子的天赋遗传（逻辑轨 ⑦）、官名（规格书未定义）。
/// </para>
/// </remarks>
public static class NewGameSetup
{
    /// <summary>成员标识的字节数（与 <see cref="PersonId"/> 的底层 <see cref="Guid"/> 一致）。</summary>
    private const int IdSizeInBytes = 16;

    /// <summary>确定性标识混合式的乘子（FNV-1a 质数）。</summary>
    private const uint IdMixMultiplier = 16777619u;

    /// <summary>确定性标识混合式的初值（FNV-1a 偏移基数）。</summary>
    private const uint IdOffsetBasis = 2166136261u;

    /// <summary>新建存档的唯一合法起始年月（§3：从 1 年 1 月开始推演）。</summary>
    private static readonly GameDate StartOfGame = new(1, 1);

    /// <summary>按四出身与既有单点创建一份新存档。</summary>
    /// <param name="request">入参。</param>
    /// <param name="random">随机来源（开局随机与存档标识的唯一来源）。</param>
    /// <param name="names">姓名来源（姓氏与名）。</param>
    /// <returns>只持有 <see cref="GameState"/> 的薄视图。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="random"/> 或 <paramref name="names"/> 为 <c>null</c>。</exception>
    /// <exception cref="ArgumentException">起始年月非 1 年 1 月，或姓氏为空白串。**不产生任何部分状态**。</exception>
    public static NewGameSetupResult Create(
        NewGameRequest request, IRandomService random, INameGenerator names)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(names);

        // 校验先于任何随机消费与任何写入（失败原子性：不存在「改了一半」的中间态）。
        if (request.StartDate != StartOfGame)
        {
            throw new ArgumentException(
                "新建存档的起始年月 MUST 为 1 年 1 月（规格书 §3；契约六 §1 条款 1）。", nameof(request));
        }

        // ① 姓氏。
        var surname = ResolveSurname(request.Surname, names);

        var family = new Family(surname);

        // ② 家主 → 配偶 → 孩子 1..n（次序即随机消费次序）。
        var head = CreateHead(request.Origin, surname, request.StartDate, random, names);
        family.AddFoundingMember(head);

        var spouse = CreateSpouse(request.Origin, surname, request.StartDate, random, names);
        family.AddOutsider(spouse);

        for (var index = 0; index < OriginStartTable.ChildCount(request.Origin); index++)
        {
            family.AddChild(CreateChild(
                surname, request.StartDate, head.Id, spouse.Id, index, request.Origin, random, names));
        }

        // ③ 成婚与家主。
        family.Marry(head.Id, spouse.Id);
        family.SetHead(head.Id);

        // ④ 士出身的功名带入（「仕」身份仍为 false：FR-008 的**任何出身**都不置位）。
        AppendScholarOriginDegree(head, request);

        // ⑤ 资金与资产只经构造入参写入（不落账本条目），档位与米价系数取既有单点。
        var economy = CreateEconomy(request.Origin);

        // ⑥ 存档唯一标识：复用 GameStateFactory 的「16 字节 → Guid」，MUST NOT 在规则层重写。
        var state = new GameStateFactory(random).Create(
            request.StartDate, request.Difficulty, request.Origin, family, economy);

        return new NewGameSetupResult(state);
    }

    /// <summary>① 姓氏：非空则原样使用（空白串抛异常），<c>null</c> 则经姓名来源随机取一个。</summary>
    /// <param name="surname">入参姓氏。</param>
    /// <param name="names">姓名来源。</param>
    /// <returns>家族姓氏。</returns>
    private static string ResolveSurname(string? surname, INameGenerator names)
    {
        if (surname is null)
        {
            var generated = names.NextSurname();
            ArgumentException.ThrowIfNullOrWhiteSpace(generated);
            return generated;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(surname);
        return surname;
    }

    /// <summary>② 家主：男、辈分 0、无父母；内部次序 年龄 → 天赋×4 → 学业 → 体质 → 寿数 → 姓名。</summary>
    private static Person CreateHead(
        Origin origin, string surname, GameDate startDate, IRandomService random, INameGenerator names)
    {
        var gender = Gender.Male;
        var age = OriginStartTable.NextHeadAge(origin, random);
        var talents = NextTalents(random);
        var study = OriginStartTable.NextHeadStudy(origin, random);
        var health = OriginStartTable.NextHeadHealth(random);
        var lifespan = AttributePolicy.NextLifespan(gender, random);
        var name = surname + names.NextGivenName(gender);

        return new Person(
            MemberId(origin, surname, 0),
            name,
            gender,
            BirthDateOf(startDate, age),
            talents,
            lifespan,
            generation: 0)
        {
            Study = study,
            Health = health,
        };
    }

    /// <summary>③ 配偶：女、辈分 0、无父母（外来者）；学业 <c>N(30,15)</c>、体质 <c>N(85,10)</c>。</summary>
    private static Person CreateSpouse(
        Origin origin, string surname, GameDate startDate, IRandomService random, INameGenerator names)
    {
        var gender = Gender.Female;
        var age = OriginStartTable.NextSpouseAge(random);
        var talents = NextTalents(random);
        var study = AttributePolicy.NextStudy(random);
        var health = AttributePolicy.NextHealth(random);
        var lifespan = AttributePolicy.NextLifespan(gender, random);
        var name = surname + names.NextGivenName(gender);

        return new Person(
            MemberId(origin, surname, 1),
            name,
            gender,
            BirthDateOf(startDate, age),
            talents,
            lifespan,
            generation: 0)
        {
            Study = study,
            Health = health,
        };
    }

    /// <summary>
    /// ④ 孩子 i：辈分 1、父母引用**同指**家主与配偶、性别 50/50；
    /// 内部次序 性别 → 年龄 → 天赋×4 → 学业（常量，**不掷骰**）→ 体质 → 寿数 → 姓名。
    /// </summary>
    private static Person CreateChild(
        string surname,
        GameDate startDate,
        PersonId fatherId,
        PersonId motherId,
        int index,
        Origin origin,
        IRandomService random,
        INameGenerator names)
    {
        var gender = random.Next(0, 2) == 0 ? Gender.Male : Gender.Female;
        var age = OriginStartTable.NextChildAge(random);
        var talents = NextTalents(random);
        var study = OriginStartTable.ChildStudyValue;
        var health = OriginStartTable.NextChildHealth(random);
        var lifespan = AttributePolicy.NextLifespan(gender, random);
        var name = surname + names.NextGivenName(gender);

        return new Person(
            MemberId(origin, surname, index + 2),
            name,
            gender,
            BirthDateOf(startDate, age),
            talents,
            lifespan,
            generation: 1,
            fatherId,
            motherId)
        {
            Study = study,
            Health = health,
        };
    }

    /// <summary>四项天赋独立取 <c>N(60,20)</c>，次序固定为**农、商、仕、工**（与 <see cref="TalentSet"/> 一致）。</summary>
    private static TalentSet NextTalents(IRandomService random)
    {
        var agriculture = AttributePolicy.NextTalent(random);
        var commerce = AttributePolicy.NextTalent(random);
        var officialdom = AttributePolicy.NextTalent(random);
        var craft = AttributePolicy.NextTalent(random);

        return new TalentSet(agriculture, commerce, officialdom, craft);
    }

    /// <summary>
    /// 出生年月：由「起始年月 − 年龄」派生（年龄**不落裸字段**，research R-07）。
    /// 前史纪年（年份 ≤ 0）由 <see cref="GameDate"/> 的 §17 裁决回写（Q4）允许。
    /// </summary>
    private static GameDate BirthDateOf(GameDate startDate, int age) =>
        new(startDate.Year - age, startDate.Month);

    /// <summary>④ 士出身：向家主追加一条「举人 / Initial」功名记录（§10.1、FR-008）。</summary>
    private static void AppendScholarOriginDegree(Person head, NewGameRequest request)
    {
        if (!OriginStartTable.ScholarOriginHasJuRenRecord(request.Origin))
        {
            return;
        }

        head.AppendDegree(new DegreeRecord(
            DegreeLevel.JuRen,
            placement: null,
            request.StartDate,
            DegreeChangeCause.Initial,
            imperialClass: null));
    }

    /// <summary>
    /// ⑤ 组装经济聚合：现金 → 现金池、商本 → 商本池、田宅 → <see cref="Holdings"/>（**MUST NOT 错池**），
    /// <see cref="Ledger"/> 为空（开局资产是初始余额，不是一笔收入）。
    /// </summary>
    private static FamilyEconomy CreateEconomy(Origin origin)
    {
        var treasury = new Treasury(
            Money.FromGuan(OriginStartTable.InitialCashGuan(origin)),
            savings: Money.Zero,
            Money.FromGuan(OriginStartTable.InitialMerchantCapitalGuan(origin)));

        var holdings = new Holdings
        {
            FarmlandMu = OriginStartTable.InitialFarmlandMu(origin),
            RuralHouses = OriginStartTable.InitialRuralHouses(origin),
            UrbanHouses = OriginStartTable.InitialUrbanHouses(origin),
        };

        return new FamilyEconomy(
            LivingCostTable.InitialStandard,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            treasury,
            holdings,
            new Ledger());
    }

    /// <summary>
    /// 成员的**确定性标识**：由（出身、姓氏、序号）派生，**与随机消费次序解耦**——
    /// 契约六 §3 的随机次序不含成员标识，若标识改从随机取，会挤掉后续成员的取值槽位。
    /// </summary>
    /// <param name="origin">存档出身。</param>
    /// <param name="surname">家族姓氏。</param>
    /// <param name="ordinal">加入序号（家主 0、配偶 1、孩子 i 为 i + 2）。</param>
    /// <returns>非空且在同一家族内唯一的标识。</returns>
    private static PersonId MemberId(Origin origin, string surname, int ordinal)
    {
        var hash = IdOffsetBasis;
        hash = (hash ^ (uint)(ordinal + 1)) * IdMixMultiplier;
        hash = (hash ^ (uint)((int)origin + 1)) * IdMixMultiplier;

        foreach (var ch in surname)
        {
            hash = (hash ^ ch) * IdMixMultiplier;
        }

        Span<byte> bytes = stackalloc byte[IdSizeInBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[..4], hash);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[4..8], hash ^ IdMixMultiplier);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[8..12], hash + IdMixMultiplier);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[12..], ~hash);

        return new PersonId(new Guid(bytes));
    }
}
