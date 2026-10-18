using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Services;
using KFL.Rules.Achievements;
using KFL.Rules.Config;
using KFL.Rules.Settlement;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// T059：绝嗣口径——「家族在册成员为零」是**唯一**判据；已亡与外嫁属归档，囚犯 / 待铨 / 患病 / 家主空缺
/// 都仍在册。判定的触发与冻结时机也必须幂等。
/// </summary>
/// <remarks>
/// 口径依据规格书 §12.1：在册 = 非已亡且非外嫁。<c>Family.SettledOutsiders</c> 等簿记**不参与**在册判定。
/// </remarks>
public class ExtinctionTests
{
    private static readonly Guid StateId = Guid.Parse("11223344-5566-4788-99aa-bbccddeeff00");

    [Fact]
    public void 零在册成员即判绝嗣()
    {
        // 家族连成员都没有（例如全部被买走 / 已亡且已归档）——在册为零。
        Assert.True(ExtinctionJudge.IsExtinct(StateOf(RulesHarness.FamilyOf())));
    }

    [Fact]
    public void 全员已亡即判绝嗣()
    {
        var member = Healthy(1, Gender.Male, 40);
        var family = RulesHarness.FamilyOf(member);

        // 身故 MUST 发生在立家之后：`Family.SetHead` 拒绝把已归档者立为家主（规格书 §4.4）。
        member.Status |= StatusFlag.Deceased;

        var state = StateOf(family);

        Assert.Empty(state.Family.RegisteredMembers);
        Assert.True(ExtinctionJudge.IsExtinct(state));
    }

    [Fact]
    public void 全员外嫁归档同样判绝嗣()
    {
        var daughter = Healthy(1, Gender.Female, 20);
        var family = RulesHarness.FamilyOf(daughter);

        // 外嫁归档 MUST 发生在立家之后（同上：家主人选必须在册）。
        daughter.Status |= StatusFlag.MarriedOut;

        var state = StateOf(family);

        // 外嫁者仍在 Family.Members 里（档案可读），但已归档，不构成在册。
        Assert.Single(state.Family.Members);
        Assert.Single(state.Family.ArchivedMembers);
        Assert.True(ExtinctionJudge.IsExtinct(state));
    }

    [Theory]
    [InlineData(StatusFlag.ServingSentence)]
    [InlineData(StatusFlag.AwaitingPost)]
    [InlineData(StatusFlag.Ill)]
    [InlineData(StatusFlag.ExamBanned)]
    [InlineData(StatusFlag.PromotionBanned)]
    [InlineData(StatusFlag.Retired)]
    public void 带任何非归档状态的成员都仍在册(StatusFlag extra)
    {
        var member = Healthy(1, Gender.Male, 40);
        member.Status |= extra;

        var state = StateOf(RulesHarness.FamilyOf(member));

        Assert.Contains(member, state.Family.RegisteredMembers);
        Assert.False(ExtinctionJudge.IsExtinct(state));
    }

    [Fact]
    public void 家主空缺但在册有人不算绝嗣()
    {
        var first = Healthy(1, Gender.Male, 40);
        var second = Healthy(2, Gender.Male, 20);
        var family = RulesHarness.FamilyOf(first, second);

        // 家主辞世 / 空缺：HeadId 归 null，但在册仍有两名成员。
        family.SetHead(null);
        var state = StateOf(family);

        Assert.Null(family.HeadId);
        Assert.Equal(2, state.Family.RegisteredMembers.Count);
        Assert.False(ExtinctionJudge.IsExtinct(state));
    }

    [Fact]
    public void 绝嗣只触发一次且绝嗣时刻冻结()
    {
        var state = StateOf(RulesHarness.FamilyOf());

        Assert.True(ExtinctionJudge.TryTrigger(state));
        Assert.True(state.Ending.IsEnded);
        var frozen = state.Ending.EndedAt;
        var chronicle = state.Ending.Chronicle;

        // 再推进一个月：TryTrigger 返回 false，且 MUST NOT 改写 EndedAt / Chronicle（幂等）。
        state.AdvanceMonth();
        Assert.False(ExtinctionJudge.TryTrigger(state));
        Assert.Equal(frozen, state.Ending.EndedAt);
        Assert.Same(chronicle, state.Ending.Chronicle);
    }

    [Fact]
    public void 编年史落定后重写被拒绝()
    {
        var state = StateOf(RulesHarness.FamilyOf());
        Assert.True(ExtinctionJudge.TryTrigger(state));

        var endedAt = state.Ending.EndedAt!.Value;
        var stale = ChronicleBuilder.Build(state);

        // EndingState 自己也要幂等：第二次 End 只回报 false，不改任何字段。
        state.AdvanceMonth();
        Assert.False(state.Ending.End(state.CurrentDate, stale));
        Assert.Equal(endedAt, state.Ending.EndedAt);
    }

    [Fact]
    public void 绝嗣在普通难度只补判四四()
    {
        var (state, settlement) = ExtinctionScene(Difficulty.Normal);

        var outcome = AchievementAdjudicator.Judge(state, settlement, [], []);

        Assert.True(outcome.ExtinctionJustTriggered);
        Assert.Contains(AchievementId.FamilyExtinct, outcome.NewlyUnlocked);
        Assert.DoesNotContain(AchievementId.HellEnding, outcome.NewlyUnlocked);
        Assert.True(state.Ending.IsEnded);
    }

    [Fact]
    public void 绝嗣在地狱难度同时补判四八()
    {
        var (state, settlement) = ExtinctionScene(Difficulty.Hell);

        var outcome = AchievementAdjudicator.Judge(state, settlement, [], []);

        Assert.True(outcome.ExtinctionJustTriggered);
        Assert.Contains(AchievementId.FamilyExtinct, outcome.NewlyUnlocked);
        Assert.Contains(AchievementId.HellEnding, outcome.NewlyUnlocked);
    }

    [Fact]
    public void 在册判据与成员集合口径等价()
    {
        var scenes = new List<Family>();

        // 逐幕：**先立家、后归档**——`Family.SetHead` 拒绝把已亡/外嫁者立为家主（规格书 §4.4）。
        scenes.Add(RulesHarness.FamilyOf());
        scenes.Add(RulesHarness.FamilyOf(Healthy(1, Gender.Male, 40)));

        var dead = Healthy(1, Gender.Male, 60);
        scenes.Add(RulesHarness.FamilyOf(dead));
        dead.Status |= StatusFlag.Deceased;

        var marriedOut = Healthy(2, Gender.Female, 20);
        scenes.Add(RulesHarness.FamilyOf(marriedOut));
        marriedOut.Status |= StatusFlag.MarriedOut;

        var deadAndOut = Healthy(1, Gender.Male, 60);
        var daughter = Healthy(2, Gender.Female, 20);
        scenes.Add(RulesHarness.FamilyOf(deadAndOut, daughter));
        deadAndOut.Status |= StatusFlag.Deceased;
        daughter.Status |= StatusFlag.MarriedOut;

        var deadFather = Healthy(1, Gender.Male, 60);
        var boy = Healthy(3, Gender.Male, 12);
        scenes.Add(RulesHarness.FamilyOf(deadFather, boy));
        deadFather.Status |= StatusFlag.Deceased;

        // 判据 MUST 恰为「在册为零」：不多一个来源（如外嫁簿记 / 家主空缺），也不少一个。
        foreach (var family in scenes)
        {
            var state = StateOf(family);
            Assert.Equal(state.Family.RegisteredMembers.Count == 0, ExtinctionJudge.IsExtinct(state));
        }
    }

    /// <summary>构造「当月结算后唯一在册者旋即外嫁」的绝嗣现场。</summary>
    /// <param name="difficulty">难度。</param>
    private static (GameState State, SettlementResult Settlement) ExtinctionScene(Difficulty difficulty)
    {
        var member = Healthy(1, Gender.Male, 40);
        var state = StateOf(RulesHarness.FamilyOf(member), difficulty);
        var settlement = Settle(state);

        // 结算照常（月内确有在册者），判定前夕变成零在册 —— 与真实「最后一人外嫁」同构。
        member.Status |= StatusFlag.MarriedOut;

        return (state, settlement);
    }

    /// <summary>跑一次真实结算，作为判定的当月事实来源。</summary>
    /// <param name="state">游戏状态。</param>
    private static SettlementResult Settle(GameState state)
    {
        var engine = new MonthlySettlementEngine(
            new FixedRandomService(), new GameStateClock(state), new FixedNameGenerator());

        return engine.Settle(state);
    }

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

    /// <summary>构造状态。</summary>
    /// <param name="family">家族。</param>
    /// <param name="difficulty">难度。</param>
    private static GameState StateOf(Family family, Difficulty difficulty = Difficulty.Normal)
    {
        var economy = new FamilyEconomy(
            LivingStandard.Normal,
            new GrainPriceIndex(GrainPricePolicy.Initial),
            new Treasury(Money.FromGuan(10_000m)),
            new Holdings());

        return new GameState(StateId, RulesHarness.Date, difficulty, Origin.Scholar, family, economy);
    }
}
