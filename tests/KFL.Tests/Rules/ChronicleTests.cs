using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Services;
using KFL.Rules.Achievements;
using KFL.Rules.Config;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// T060：编年史七项口径——①②整数年 / 辈分、③ 峰值资产取自成就状态、④⑤含已亡与外嫁（按人去重）、
/// ⑥ 三均值、⑦ 谱系按 <c>Family.Members</c> 的原序。
/// </summary>
/// <remarks>
/// 四项「明确不校验」字段（峰值资产 / 最高功名 / 进士数 / 平均寿命）在 <c>SaveStateValidator</c> 里
/// 不做重算比对，因此这里钉死的是**生成侧**的口径。
/// </remarks>
public class ChronicleTests
{
    private static readonly Guid StateId = Guid.Parse("aabbccdd-eeff-4a1b-8c2d-3e4f50617283");

    [Fact]
    public void 存续年数是整数年差而不是月数()
    {
        var member = Healthy(1, Gender.Male, 40);
        var family = RulesHarness.FamilyOf(member);

        // 先立家、后身故：唯一在册者归零才算绝嗣（`SetHead` 拒绝已归档者，规格书 §4.4）。
        member.Status |= StatusFlag.Deceased;

        var state = StateOf(family, new GameDate(102, 1));
        Assert.True(ExtinctionJudge.TryTrigger(state));

        var chronicle = state.Ending.Chronicle!;
        var endedAt = state.Ending.EndedAt!.Value;

        Assert.Equal(endedAt.Year - 1, chronicle.SurvivedYears);

        // 若按月份计，102 年 1 月会得到 ~1212 或 ~1224 之类的量级；整数年口径恰为 101。
        Assert.Equal(101, chronicle.SurvivedYears);
    }

    [Fact]
    public void 辈分数是最大辈分加一()
    {
        var founder = Healthy(1, Gender.Male, 60);
        var family = RulesHarness.FamilyOf(founder);

        // 血亲链条：子（辈分 1）→ 孙（辈分 2）。RulesHarness.Member 只造辈分 0，故此处显式构造。
        var child = Blood(2, Gender.Male, 30, generation: 1, fatherId: founder.Id);
        var grandchild = Blood(3, Gender.Male, 5, generation: 2, fatherId: child.Id);
        family.AddChild(child);
        family.AddChild(grandchild);

        var state = StateOf(family, RulesHarness.Date);
        var chronicle = ChronicleBuilder.Build(state);

        var maxGeneration = 0;
        foreach (var member in state.Family.Members)
        {
            maxGeneration = Math.Max(maxGeneration, member.Generation);
        }

        Assert.Equal(2, maxGeneration);
        Assert.Equal(maxGeneration + 1, chronicle.Generations);
        Assert.Equal(3, chronicle.Generations);
    }

    [Fact]
    public void 无成员时辈分为零且不抛()
    {
        var chronicle = ChronicleBuilder.Build(StateOf(RulesHarness.FamilyOf(), RulesHarness.Date));

        Assert.Equal(0, chronicle.Generations);
        Assert.Empty(chronicle.Genealogy);
        Assert.Equal(0m, chronicle.AverageLifespan);
        Assert.Equal(DegreeLevel.BaiShen, chronicle.HighestDegree);
        Assert.Equal(0, chronicle.TotalJinShi);
    }

    [Fact]
    public void 峰值资产取自成就状态而不是即时重算()
    {
        var state = StateOf(RulesHarness.FamilyOf(Healthy(1, Gender.Male, 40)), RulesHarness.Date);

        // 家族经济面现金只有 1 万贯，但成就状态记着更高的历史峰值 —— 编年史 ③ 必须取后者。
        state.Achievements.ObserveTotalAssets(Money.FromGuan(88_888m));

        var chronicle = ChronicleBuilder.Build(state);

        Assert.Equal(Money.FromGuan(88_888m), chronicle.PeakTotalAssets);
    }

    [Fact]
    public void 最高功名与进士数包含已亡与外嫁()
    {
        var jinShi = Healthy(1, Gender.Male, 60);
        AppendDegree(jinShi, DegreeLevel.JinShi, ImperialPlacement.ZhuangYuan);

        var other = Healthy(2, Gender.Male, 40);
        AppendDegree(other, DegreeLevel.JinShi, null);

        var marriedOut = Healthy(3, Gender.Female, 30);
        AppendDegree(marriedOut, DegreeLevel.JuRen, null);

        var family = RulesHarness.FamilyOf(jinShi, other, marriedOut);

        // 归档状态在立家之后施加（同上）：家主人选必须在册。
        jinShi.Status |= StatusFlag.Deceased;
        marriedOut.Status |= StatusFlag.MarriedOut;

        var chronicle = ChronicleBuilder.Build(StateOf(family, RulesHarness.Date));

        // 归档不是删除：已亡的状元与外嫁的举人都必须进入统计。
        Assert.Equal(DegreeLevel.JinShi, chronicle.HighestDegree);
        Assert.Equal(2, chronicle.TotalJinShi);
        Assert.Equal(3, chronicle.Genealogy.Count);
    }

    [Fact]
    public void 进士数按人去重而不是按变迁条数()
    {
        var member = Healthy(1, Gender.Male, 50);
        AppendDegree(member, DegreeLevel.JinShi, null);
        AppendDegree(member, DegreeLevel.JinShi, ImperialPlacement.BangYan);

        var chronicle = ChronicleBuilder.Build(
            StateOf(RulesHarness.FamilyOf(member), RulesHarness.Date));

        Assert.Equal(1, chronicle.TotalJinShi);
    }

    [Fact]
    public void 平均寿命走三均值口径()
    {
        var young = Healthy(1, Gender.Male, 80);
        var old = Healthy(2, Gender.Male, 80);

        var family = RulesHarness.FamilyOf(young, old);

        // 归档状态在立家之后施加（同上）：家主人选必须在册。
        young.Status |= StatusFlag.Deceased;
        old.Status |= StatusFlag.Deceased;

        var state = StateOf(family, RulesHarness.Date);

        // 两名成员分别按 10 岁与 90 岁记入寿命序列 ⇒ 三均值 (10 + 2×50 + 90) / 4 = 50.000。
        state.Achievements.AppendLifespan(10);
        state.Achievements.AppendLifespan(90);

        Assert.Equal(50.000m, ChronicleBuilder.Build(state).AverageLifespan);
    }

    [Fact]
    public void 谱系与成员集合逐位同序()
    {
        var first = Healthy(1, Gender.Male, 60);
        var second = Healthy(2, Gender.Female, 55);
        var third = Healthy(3, Gender.Male, 20);

        var family = RulesHarness.FamilyOf(first, second, third);
        var state = StateOf(family, RulesHarness.Date);
        var chronicle = ChronicleBuilder.Build(state);

        var expected = new List<PersonId>();
        foreach (var member in family.Members)
        {
            expected.Add(member.Id);
        }

        // 顺序敏感：逐位比对，集合相等不算通过。
        Assert.Equal(expected.Count, chronicle.Genealogy.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.Equal(expected[index], chronicle.Genealogy[index]);
        }
    }

    [Theory]
    [InlineData(101, false)]
    [InlineData(102, true)]
    public void 百年世家在第一百零二年首次达成(int year, bool expected)
    {
        var state = StateOf(RulesHarness.FamilyOf(Healthy(1, Gender.Male, 40)), new GameDate(year, 1));

        var unlocked = AchievementAdjudicator
            .Report(state, AchievementTrigger.NewGameCreated)
            .NewlyUnlocked;

        Assert.Equal(expected, unlocked.Contains(AchievementId.CenturyHouse));
    }

    [Theory]
    [InlineData(301, false)]
    [InlineData(302, true)]
    public void 千秋万代在第三百零二年首次达成(int year, bool expected)
    {
        var state = StateOf(RulesHarness.FamilyOf(Healthy(1, Gender.Male, 40)), new GameDate(year, 1));

        var unlocked = AchievementAdjudicator
            .Report(state, AchievementTrigger.NewGameCreated)
            .NewlyUnlocked;

        Assert.Equal(expected, unlocked.Contains(AchievementId.MillenniumHouse));
    }

    [Theory]
    [InlineData(100, false)]
    [InlineData(101, true)]
    public void 绝处逢生在第一百零一年首次达成且仅限地狱(int year, bool expected)
    {
        var state = StateOf(
            RulesHarness.FamilyOf(Healthy(1, Gender.Male, 40)), new GameDate(year, 1), Difficulty.Hell);

        var unlocked = AchievementAdjudicator
            .Report(state, AchievementTrigger.NewGameCreated)
            .NewlyUnlocked;

        Assert.Equal(expected, unlocked.Contains(AchievementId.NarrowEscape));

        // 非地狱难度即使存续再久也不解锁。
        var normal = StateOf(
            RulesHarness.FamilyOf(Healthy(1, Gender.Male, 40)), new GameDate(year, 1), Difficulty.Normal);

        Assert.DoesNotContain(
            AchievementId.NarrowEscape,
            AchievementAdjudicator.Report(normal, AchievementTrigger.NewGameCreated).NewlyUnlocked);
    }

    [Fact]
    public void 成就判定先于绝嗣补判且编年史在绝嗣时落定()
    {
        var state = StateOf(
            RulesHarness.FamilyOf(Healthy(1, Gender.Male, 40)), new GameDate(302, 1));

        // 判定月内唯一在册者已外嫁 ⇒ 家族已绝嗣。若「先绝嗣后判定」，Judge 会在入口因 IsEnded 直接返回空，
        // 044/048 就永远不会入档；因此 68 条 MUST 先跑（本月事实照常吸收、千秋万代照常成立），再补判绝嗣。
        var outcome = AchievementAdjudicator.Judge(state, SettlementOf(state), [], []);

        Assert.Contains(AchievementId.MillenniumHouse, outcome.NewlyUnlocked);
        Assert.Contains(AchievementId.FamilyExtinct, outcome.NewlyUnlocked);
        Assert.DoesNotContain(AchievementId.HellEnding, outcome.NewlyUnlocked);

        // 百年世家带「未绝嗣」前置：同月绝嗣 ⇒ 不成立。
        Assert.DoesNotContain(AchievementId.CenturyHouse, outcome.NewlyUnlocked);

        Assert.True(state.Ending.IsEnded);
        Assert.Equal(301, state.Ending.Chronicle!.SurvivedYears);

        // 落定之后再判定一律空转（入口即返回）。
        Assert.Empty(AchievementAdjudicator.Judge(state, SettlementOf(state), [], []).NewlyUnlocked);
    }

    /// <summary>跑一次真实结算，作为判定的当月事实来源。</summary>
    /// <param name="state">游戏状态。</param>
    private static KFL.Rules.Settlement.SettlementResult SettlementOf(GameState state)
    {
        // 先把唯一在册者外嫁，再照常结算：结算面不依赖在册人数，判定面才能看到零在册。
        foreach (var member in state.Family.Members)
        {
            member.Status |= StatusFlag.MarriedOut;
        }

        var engine = new KFL.Rules.Settlement.MonthlySettlementEngine(
            new FixedRandomService(), new GameStateClock(state), new FixedNameGenerator());

        return engine.Settle(state);
    }

    /// <summary>追加一条功名变迁记录。</summary>
    /// <param name="person">成员。</param>
    /// <param name="level">功名。</param>
    /// <param name="placement">鼎甲名次（可空）。</param>
    /// <remarks>
    /// 鼎甲名次非空 ⇒ 甲第 MUST 为一甲（`DegreeRecord` 的构造校验）：状元/榜眼/探花都属一甲。
    /// </remarks>
    private static void AppendDegree(Person person, DegreeLevel level, ImperialPlacement? placement) =>
        person.AppendDegree(new DegreeRecord(
            level,
            placement,
            new GameDate(RulesHarness.Date.Year - 1, RulesHarness.Date.Month),
            DegreeChangeCause.ExamPass,
            placement is null ? null : ImperialClass.FirstClass));

    /// <summary>构造健康成员；夹具默认体质 0，月结算的 ⑧-c 会当场归零身故。</summary>
    /// <param name="index">夹具下标。</param>
    /// <param name="gender">性别。</param>
    /// <param name="age">周岁。</param>
    private static Person Healthy(int index, Gender gender, int age)
    {
        var member = RulesHarness.Member(index, gender, age);
        member.Health = (int)AttributePolicy.HealthMean;

        return member;
    }

    /// <summary>构造血亲成员：辈分与父母引用必须显式给定（<c>AddChild</c> 会校验辈分 = 父母辈分 + 1）。</summary>
    /// <param name="index">夹具下标。</param>
    /// <param name="gender">性别。</param>
    /// <param name="age">周岁。</param>
    /// <param name="generation">辈分。</param>
    /// <param name="fatherId">父亲引用。</param>
    private static Person Blood(int index, Gender gender, int age, int generation, PersonId fatherId)
    {
        var member = new Person(
            new PersonId(new Guid($"00000000-0000-4000-8000-{index:D12}")),
            $"B{index}",
            gender,
            new GameDate(RulesHarness.Date.Year - age, RulesHarness.Date.Month),
            new TalentSet(0, 0, 0, 0),
            70,
            generation,
            fatherId);
        member.Health = (int)AttributePolicy.HealthMean;

        return member;
    }

    /// <summary>构造状态。</summary>
    /// <param name="family">家族。</param>
    /// <param name="date">当前年月。</param>
    /// <param name="difficulty">难度。</param>
    private static GameState StateOf(Family family, GameDate date, Difficulty difficulty = Difficulty.Normal)
    {
        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(Money.FromGuan(10_000m)),
            new Holdings());

        return new GameState(StateId, date, difficulty, Origin.Scholar, family, economy);
    }
}
