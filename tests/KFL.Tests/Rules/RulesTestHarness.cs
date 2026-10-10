using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Career;
using KFL.Rules.Settlement;
using KFL.Tests.Fixtures;

namespace KFL.Tests.Rules;

/// <summary>
/// 规则测试的共享构造件与替身：固定参照年月、确定性成员工厂、固定取值的随机替身。
/// </summary>
/// <remarks>
/// 全部初值都是**固定常量**，MUST NOT 读取环境（G-07 的扫描范围包含本目录）。
/// 规则数值一律经 <c>KFL.Rules/Config</c> 的成员取得，本文件 MUST NOT 另存副本（SC-008）。
/// </remarks>
internal static class RulesHarness
{
    /// <summary>固定参照年月：各测试成员的年龄档归属都以它为准。</summary>
    public static readonly GameDate Date = new(80, 1);

    /// <summary>构造一名年龄恰为 <paramref name="age"/> 的成员（出生月与参照月相同，故生日当月即计入）。</summary>
    /// <param name="index">夹具标识下标。</param>
    /// <param name="gender">性别。</param>
    /// <param name="age">参照年月下的周岁数。</param>
    /// <param name="agriculture">农天赋（0~100）。</param>
    /// <param name="commerce">商天赋（0~100）。</param>
    /// <param name="officialdom">仕天赋（0~100）。</param>
    /// <param name="craft">工天赋（0~100）。</param>
    public static Person Member(
        int index,
        Gender gender,
        int age,
        int agriculture = 0,
        int commerce = 0,
        int officialdom = 0,
        int craft = 0) =>
        new(
            FamilyFixtures.Id(index),
            $"P{index}",
            gender,
            new GameDate(Date.Year - age, Date.Month),
            new TalentSet(agriculture, commerce, officialdom, craft),
            70,
            0);

    /// <summary>把给定成员全部作为开局成员（辈分 0、无父母）加入一个新家族，并指定首名为家主。</summary>
    /// <param name="members">成员。</param>
    /// <returns>新家族。</returns>
    public static Family FamilyOf(params Person[] members)
    {
        var family = new Family("测");

        foreach (var member in members)
        {
            family.AddFoundingMember(member);
        }

        if (members.Length > 0)
        {
            family.SetHead(members[0].Id);
        }

        return family;
    }

    /// <summary>构造一套资产组合。</summary>
    /// <param name="farmlandMu">田（亩）。</param>
    /// <param name="urbanHouses">城市宅（座）。</param>
    /// <param name="shops">铺面（间）。</param>
    /// <param name="ruralHouses">农村宅（座）。</param>
    public static Holdings HoldingsOf(
        int farmlandMu = 0, int urbanHouses = 0, int shops = 0, int ruralHouses = 0) =>
        new()
        {
            FarmlandMu = farmlandMu,
            RuralHouses = ruralHouses,
            UrbanHouses = urbanHouses,
            Shops = shops,
        };

    /// <summary>只带商本的资金状态（收入计算只需读商本池）。</summary>
    /// <param name="merchantCapital">商本池。</param>
    public static Treasury TreasuryWith(Money merchantCapital) =>
        new(Money.Zero, Money.Zero, merchantCapital);

    /// <summary>算一次收入（固定用 <see cref="Date"/>）。</summary>
    /// <param name="family">家族。</param>
    /// <param name="holdings">资产。</param>
    /// <param name="treasury">资金状态（只读商本）。</param>
    /// <param name="difficulty">难度。</param>
    /// <param name="origin">出身。</param>
    public static IncomeComputation Income(
        Family family, Holdings holdings, Treasury treasury, Difficulty difficulty, Origin origin) =>
        IncomeCalculator.Compute(family, Date, holdings, treasury, difficulty, origin);

    /// <summary>以「贯」为单位的金额。</summary>
    /// <param name="value">贯数。</param>
    public static Money Guan(decimal value) => Money.FromGuan(value);

    /// <summary>
    /// 在任官员夹具（003）：给定官阶、在职月数与政绩；其余字段同 <see cref="Member"/>。
    /// </summary>
    /// <param name="index">夹具标识下标。</param>
    /// <param name="level">官阶级数（1~18）。</param>
    /// <param name="monthsInOffice">在职月数（&gt;= 0）。</param>
    /// <param name="merit">政绩（&gt;= 0）。</param>
    /// <param name="gender">性别。</param>
    /// <param name="age">参照年月下的周岁数。</param>
    public static Person Official(
        int index, int level, int monthsInOffice = 0, int merit = 0, Gender gender = Gender.Male, int age = 40)
    {
        var person = Member(index, gender, age);
        person.Rank = new OfficialRank(level);
        person.MonthsInOffice = monthsInOffice;
        person.Merit = merit;
        return person;
    }

    /// <summary>
    /// 待阙夹具（003）：位、计时与**入仕途径**成对设置（位先真、计时与途径后落），
    /// 并保留其余计时字段。
    /// </summary>
    /// <param name="person">待置入待阙的成员。</param>
    /// <param name="remainingMonths">待阙剩余月数。</param>
    /// <param name="track">
    /// 入仕途径；<c>null</c> = 按 <see cref="AppointmentEntry.TrackOf"/> 从功名记录派生
    /// （与「及第入仕」入口同口径）。
    /// </param>
    public static Person Awaiting(Person person, int remainingMonths, AppointmentTrack? track = null)
    {
        ArgumentNullException.ThrowIfNull(person);

        var timers = person.Timers;
        person.Status |= StatusFlag.AwaitingPost;
        person.Timers = new StatusTimers(
            timers.SentenceRemainingMonths,
            timers.ExamBanRemainingMonths,
            timers.PromotionBanRemainingMonths,
            remainingMonths);
        person.EntryTrack = track ?? AppointmentEntry.TrackOf(person);

        return person;
    }

    /// <summary>
    /// 禁升期官员夹具（003）：本特性**只消费**该计时、不清算也不建立（逻辑轨 ⑥）。
    /// </summary>
    /// <param name="index">夹具标识下标。</param>
    /// <param name="remainingMonths">禁升剩余月数。</param>
    /// <param name="level">官阶级数。</param>
    /// <param name="monthsInOffice">在职月数。</param>
    /// <param name="merit">政绩。</param>
    public static Person PromotionBannedOfficial(
        int index, int remainingMonths, int level, int monthsInOffice = 0, int merit = 0)
    {
        var person = Official(index, level, monthsInOffice, merit);
        var timers = person.Timers;

        person.Status |= StatusFlag.PromotionBanned;
        person.Timers = new StatusTimers(
            timers.SentenceRemainingMonths,
            timers.ExamBanRemainingMonths,
            remainingMonths,
            timers.AwaitingPostRemainingMonths);

        return person;
    }

    /// <summary>服刑成员夹具（003）：位与计时成对设置；Q5 裁决下其仕途计时与政绩一律暂停。</summary>
    /// <param name="person">服刑的成员。</param>
    /// <param name="remainingMonths">服刑剩余月数。</param>
    public static Person ServingSentence(Person person, int remainingMonths)
    {
        ArgumentNullException.ThrowIfNull(person);

        var timers = person.Timers;
        person.Status |= StatusFlag.ServingSentence;
        person.Timers = new StatusTimers(
            remainingMonths,
            timers.ExamBanRemainingMonths,
            timers.PromotionBanRemainingMonths,
            timers.AwaitingPostRemainingMonths);

        return person;
    }
}

/// <summary>
/// 固定姓名来源替身（003；与 <see cref="FixedRandomService"/> 同款）：无论性别都返回固定姓名，
/// 使开局的随机消费只由随机替身决定。
/// </summary>
internal sealed class FixedNameGenerator : INameGenerator
{
    private readonly string _surname;
    private readonly string _maleGivenName;
    private readonly string _femaleGivenName;

    /// <summary>用固定姓氏与固定名构造。</summary>
    /// <param name="surname">姓氏。</param>
    /// <param name="maleGivenName">男性名。</param>
    /// <param name="femaleGivenName">女性名。</param>
    public FixedNameGenerator(string surname = "测", string maleGivenName = "文", string femaleGivenName = "婉")
    {
        _surname = surname;
        _maleGivenName = maleGivenName;
        _femaleGivenName = femaleGivenName;
    }

    /// <inheritdoc />
    public string NextSurname() => _surname;

    /// <inheritdoc />
    public string NextGivenName(Gender gender) => gender == Gender.Male ? _maleGivenName : _femaleGivenName;
}

/// <summary>
/// 固定取值序列的随机来源替身：按给定次序给出 <see cref="NextDouble"/>，用尽后恒返回中值。
/// </summary>
/// <remarks>
/// 它让「同种子 → 同结果」之外的**边界**（取 0 与极大值）也可断言。
/// </remarks>
internal sealed class FixedRandomService : IRandomService
{
    /// <summary>用尽给定序列后返回的中值。</summary>
    private const double Fallback = 0.5d;  // arch-guard:allow 随机替身的取值回退（非规则数值副本）

    private readonly Queue<double> _values;

    /// <summary>按给定次序构造。</summary>
    /// <param name="values">依次返回的取值。</param>
    public FixedRandomService(params double[] values) => _values = new Queue<double>(values);

    /// <inheritdoc />
    public double NextDouble() => _values.Count > 0 ? _values.Dequeue() : Fallback;

    /// <inheritdoc />
    public int Next(int minInclusive, int maxExclusive) => minInclusive;

    /// <inheritdoc />
    public void NextBytes(Span<byte> destination) => destination.Clear();
}
