using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Services;
using KFL.Rules.Config;
using KFL.Rules.FamilyLifecycle;
using KFL.Rules.Settlement;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// T061 / SC-005：⑧-h 家主继任的四档顺序与边界（规格书 §4.4；契约二十 §8）。
/// </summary>
/// <remarks>
/// 断言分两层：① 直接调 <see cref="HeadSuccession.Candidates(Family, GameDate)"/> 验四档顺序；
/// ② 经一次结算的公开面验「同月一次继任」与「家主空缺不是绝嗣」。
/// </remarks>
public class HeadSuccessionTests
{
    private static readonly Guid StateId = Guid.Parse("b6f4a2d8-5c31-4e70-9d18-7f3a2b6c8e05");

    /// <summary>第一档：已亡家主的在册男性子女中年龄最大者当选。</summary>
    [Fact]
    public void 第一档取已亡家主的在册男性子女中最年长者()
    {
        var family = Pedigree(out var head, out var elderSon, out var youngerSon, out var grandson);
        head.Status |= StatusFlag.Deceased;

        var candidates = HeadSuccession.Candidates(family, RulesHarness.Date);

        Assert.Equal(elderSon.Id, HeadSuccession.Select(candidates));
        Assert.Equal(
            [elderSon.Id, youngerSon.Id, grandson.Id],
            candidates.Select(person => person.Id));
    }

    /// <summary>第二档：家主诸子皆亡时，取已亡儿子的在册男性后代（代际就近优先）。</summary>
    [Fact]
    public void 第二档在家主子女皆亡时取后代且代际就近()
    {
        var family = Pedigree(out var head, out var elderSon, out var youngerSon, out var grandson);
        head.Status |= StatusFlag.Deceased;
        elderSon.Status |= StatusFlag.Deceased;
        youngerSon.Status |= StatusFlag.Deceased;

        // 再补一名更晚代的后代：它的优先级低于孙子（代际就近优先）。
        var greatGrandson = Person(5, "陈曾孙", Gender.Male, 3, 3, grandson.Id);
        family.AddChild(greatGrandson);

        var candidates = HeadSuccession.Candidates(family, RulesHarness.Date);

        Assert.Equal(grandson.Id, HeadSuccession.Select(candidates));
        Assert.Equal([grandson.Id, greatGrandson.Id], candidates.Select(person => person.Id));
    }

    /// <summary>第三档：家主无后时取在册亲兄弟（父辈在世也不越档）。</summary>
    [Fact]
    public void 第三档在家主无后时取在册亲兄弟()
    {
        var founder = Person(1, "陈祖", Gender.Male, 70, generation: 0, lifespan: 80);
        var head = Person(2, "陈伯", Gender.Male, 50, generation: 1, founder.Id);
        var brother = Person(3, "陈仲", Gender.Male, 45, generation: 1, founder.Id);
        var family = RulesHarness.FamilyOf(founder);
        family.AddChild(head);
        family.AddChild(brother);
        family.SetHead(head.Id);
        head.Status |= StatusFlag.Deceased;

        var candidates = HeadSuccession.Candidates(family, RulesHarness.Date);

        Assert.Equal(brother.Id, HeadSuccession.Select(candidates));
        Assert.Equal([brother.Id, founder.Id], candidates.Select(person => person.Id));
    }

    /// <summary>第四档：兜底取在册最年长男性，同岁取在册子代数更多者。</summary>
    [Fact]
    public void 第四档兜底取在册最年长男性且同岁比子代数()
    {
        var head = Person(1, "陈祖", Gender.Male, 70, generation: 0, lifespan: 60);
        var family = RulesHarness.FamilyOf(head);
        var childless = Person(2, "陈甲", Gender.Male, 40, generation: 0);
        var fruitful = Person(3, "陈乙", Gender.Male, 40, generation: 0);
        var heir = Person(4, "陈乙子", Gender.Male, 12, generation: 1, fruitful.Id);
        family.AddOutsider(childless);
        family.AddOutsider(fruitful);
        family.AddChild(heir);
        head.Status |= StatusFlag.Deceased;

        var candidates = HeadSuccession.Candidates(family, RulesHarness.Date);

        Assert.Equal(fruitful.Id, HeadSuccession.Select(candidates));

        var older = Person(5, "陈丙", Gender.Male, 45, generation: 0);        family.AddOutsider(older);

        Assert.Equal(older.Id, HeadSuccession.Select(HeadSuccession.Candidates(family, RulesHarness.Date)));
    }

    /// <summary>候选含服刑与待阙，排除已亡与外嫁；只剩一名服刑男性时他当选。</summary>
    [Fact]
    public void 候选含服刑排除已亡与外嫁()
    {
        var family = Pedigree(out var head, out var prisoner, out var marriedOut, out _);
        head.Status |= StatusFlag.Deceased;
        prisoner.Status |= StatusFlag.ServingSentence;
        marriedOut.Status |= StatusFlag.MarriedOut;

        var candidates = HeadSuccession.Candidates(family, RulesHarness.Date);

        Assert.Equal(prisoner.Id, HeadSuccession.Select(candidates));
        Assert.DoesNotContain(marriedOut.Id, candidates.Select(person => person.Id));
        Assert.DoesNotContain(head.Id, candidates.Select(person => person.Id));
    }

    /// <summary>现任家主健在且未外嫁 ⇒ 本子步不做任何事（0 随机、<c>NewHeadId == null</c>）。</summary>
    [Fact]
    public void 现任家主健在时继任子步不做事()
    {
        var family = Pedigree(out var head, out _, out _, out _, headLifespan: 80);

        var result = Settle(State(family), new FixedRandomService(), months: 1);

        Assert.Null(result.Lifecycle.NewHeadId);
        Assert.False(result.Lifecycle.HeadBecameVacant);
        Assert.Equal(head.Id, family.HeadId);
    }

    /// <summary>家主亡故 ⇒ 同月一次继任给在册最年长男性。</summary>
    [Fact]
    public void 家主亡故后同月传位给在册最年长男性()
    {
        var family = Pedigree(out var head, out var elderSon, out _, out _);
        var state = State(family);

        var result = Settle(state, new FixedRandomService(), months: 1);

        Assert.Single(result.Lifecycle.Deaths, death => death.PersonId == head.Id);
        Assert.Equal(elderSon.Id, result.Lifecycle.NewHeadId);
        Assert.False(result.Lifecycle.HeadBecameVacant);
        Assert.Equal(elderSon.Id, family.HeadId);
    }

    /// <summary>同月多人死亡 ⇒ 先归档完再继任一次（MUST NOT 选出已亡者）。</summary>
    [Fact]
    public void 同月多人死亡时先归档完再继任一次()
    {
        var family = Pedigree(out var head, out var elderSon, out var youngerSon, out _, elderLifespan: 50);
        var state = State(family);

        var result = Settle(state, new FixedRandomService(), months: 1);

        Assert.Equal(2, result.Lifecycle.Deaths.Count);
        Assert.Equal(youngerSon.Id, result.Lifecycle.NewHeadId);
        Assert.NotEqual(elderSon.Id, result.Lifecycle.NewHeadId);
        Assert.Equal(youngerSon.Id, family.HeadId);
    }

    /// <summary>无在册男性 ⇒ 家主空缺（<c>HeadId == null</c>），且不产生绝嗣判定。</summary>
    [Fact]
    public void 无在册男性时家主空缺而非绝嗣()
    {
        var head = Person(1, "陈祖", Gender.Male, 70, generation: 0, lifespan: 60);
        var daughter = Person(2, "陈女", Gender.Female, 40, generation: 1, head.Id);
        var family = RulesHarness.FamilyOf(head);
        family.AddChild(daughter);
        daughter.Status |= StatusFlag.MarriedOut;
        var state = State(family);

        var result = Settle(state, new FixedRandomService(), months: 1);

        Assert.Null(family.HeadId);
        Assert.True(result.Lifecycle.HeadBecameVacant);
        Assert.Null(result.Lifecycle.NewHeadId);
    }

    /// <summary><c>Family.SetHead</c> 的签名不变（contracts/illness-lifespan.md §8 的跨特性约束）。</summary>
    [Fact]
    public void 家主写入通道签名不变()
    {
        var method = typeof(Family).GetMethod(nameof(Family.SetHead));

        Assert.NotNull(method);
        Assert.Equal([typeof(PersonId?)], method.GetParameters().Select(parameter => parameter.ParameterType));
    }

    // ------------------------------------------------------------------ 基座

    /// <summary>
    /// 三代家族：家主（<paramref name="headLifespan"/>）＋两名儿子（长子 <paramref name="elderLifespan"/>）＋一名孙子。
    /// </summary>
    /// <param name="head">家主。</param>
    /// <param name="elderSon">长子（第三档场景里也是新的家主）。</param>
    /// <param name="youngerSon">次子。</param>
    /// <param name="grandson">长孙（长子的儿子）。</param>
    /// <param name="headLifespan">家主寿数（默认 60 ⇒ 年龄 70 的家主本月到龄）。</param>
    /// <param name="elderLifespan">长子寿数（默认 70 ⇒ 年龄 50 的长子存活）。</param>
    /// <returns>家族。</returns>
    private static Family Pedigree(
        out Person head,
        out Person elderSon,
        out Person youngerSon,
        out Person grandson,
        int headLifespan = 60,
        int elderLifespan = 70)
    {
        head = Person(1, "陈祖", Gender.Male, 70, generation: 0, lifespan: headLifespan);
        elderSon = Person(2, "陈长", Gender.Male, 50, generation: 1, head.Id, lifespan: elderLifespan);
        youngerSon = Person(3, "陈次", Gender.Male, 45, generation: 1, head.Id);
        grandson = Person(4, "陈孙", Gender.Male, 25, generation: 2, elderSon.Id);
        var family = RulesHarness.FamilyOf(head);
        family.AddChild(elderSon);
        family.AddChild(youngerSon);
        family.AddChild(grandson);
        return family;
    }

    /// <summary>建一名成员（出生月取本月，故年龄恰为 <paramref name="age"/>）。</summary>
    /// <param name="index"><see cref="FamilyFixtures.Id(int)"/> 的序号。</param>
    /// <param name="name">姓名。</param>
    /// <param name="gender">性别。</param>
    /// <param name="age">年龄。</param>
    /// <param name="generation">辈分。</param>
    /// <param name="fatherId">父引用。</param>
    /// <param name="lifespan">寿数。</param>
    private static Person Person(
        int index,
        string name,
        Gender gender,
        int age,
        int generation,
        PersonId? fatherId = null,
        int lifespan = 70)
        => FamilyFixtures.NewPerson(
            index,
            name,
            gender,
            RulesHarness.Date.Year - age,
            RulesHarness.Date.Month,
            generation,
            fatherId: fatherId,
            lifespan: lifespan);

    /// <summary>装配一个状态（资金充裕、普通难度、工出身）。</summary>
    /// <param name="family">家族。</param>
    private static GameState State(Family family)
    {
        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(RulesHarness.Guan(1000)),
            new Holdings());

        return new GameState(StateId, RulesHarness.Date, Difficulty.Normal, Origin.Artisan, family, economy);
    }

    /// <summary>结算若干个月并返回最后一次快照。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="random">随机源。</param>
    /// <param name="months">月数。</param>
    private static SettlementResult Settle(GameState state, IRandomService random, int months)
    {
        var engine = new MonthlySettlementEngine(random, new GameStateClock(state), new FixedNameGenerator());
        SettlementResult? last = null;

        for (var index = 0; index < months; index++)
        {
            last = engine.Settle(state);
        }

        Assert.NotNull(last);

        return last;
    }
}
