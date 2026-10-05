using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Core;

/// <summary>
/// T025：US1 AS3 与 FR-010——九种状态标记可并存，以及 <see cref="Person.Timers"/> 与
/// <see cref="Person.Status"/> 的一致性。
/// </summary>
public class StatusTests
{
    private static readonly StatusFlag[] AllNineFlags =
    [
        StatusFlag.Ill,
        StatusFlag.Famine,
        StatusFlag.ServingSentence,
        StatusFlag.AwaitingPost,
        StatusFlag.ExamBanned,
        StatusFlag.PromotionBanned,
        StatusFlag.MarriedOut,
        StatusFlag.Retired,
        StatusFlag.Deceased,
    ];

    [Fact]
    public void 状态标记是Flags枚举且恰好九位()
    {
        Assert.NotNull(typeof(StatusFlag).GetCustomAttributes(typeof(FlagsAttribute), inherit: false).SingleOrDefault());

        var named = Enum.GetValues<StatusFlag>().Where(v => v != StatusFlag.None).ToArray();
        Assert.Equal(9, named.Length);
        Assert.Equal(AllNineFlags, named);

        // 每一位都只占一个二进制位，互不重叠。
        Assert.Equal(9, named.Select(v => (int)v).Distinct().Count());
        Assert.All(named, v => Assert.Equal(1, System.Numerics.BitOperations.PopCount((uint)v)));
    }

    [Fact]
    public void 九位可任意组合并存()
    {
        var all = AllNineFlags.Aggregate(StatusFlag.None, (acc, flag) => acc | flag);

        foreach (var flag in AllNineFlags)
        {
            Assert.True(all.HasFlag(flag), $"{flag} 应在并存集合中为真。");
        }

        Assert.Equal(0b1_1111_1111, (int)all);
    }

    [Fact]
    public void 服刑与禁考同时为真且互不覆盖()
    {
        var person = FamilyFixtures.NewPerson(801, "囚徒", Gender.Male, 1, 1, generation: 0);

        person.Status = StatusFlag.ServingSentence | StatusFlag.ExamBanned;

        Assert.True(person.Status.HasFlag(StatusFlag.ServingSentence));
        Assert.True(person.Status.HasFlag(StatusFlag.ExamBanned));
        Assert.False(person.Status.HasFlag(StatusFlag.PromotionBanned));

        // 独立并行计时（规格书 §7.5）：两者可各持有自己的剩余月数。
        person.Timers = new StatusTimers(sentenceRemainingMonths: 60, examBanRemainingMonths: 36);
        Assert.Equal(60, person.Timers.SentenceRemainingMonths.GetValueOrDefault(-1));
        Assert.Equal(36, person.Timers.ExamBanRemainingMonths.GetValueOrDefault(-1));
    }

    [Fact]
    public void 状态位为假时对应计时字段必须为空()
    {
        var person = FamilyFixtures.NewPerson(802, "测试", Gender.Male, 1, 1, generation: 0);

        Assert.Equal(StatusFlag.None, person.Status);

        Assert.Throws<ArgumentException>(() => person.Timers = new StatusTimers(sentenceRemainingMonths: 12));
        Assert.Throws<ArgumentException>(() => person.Timers = new StatusTimers(examBanRemainingMonths: 12));
        Assert.Throws<ArgumentException>(() => person.Timers = new StatusTimers(promotionBanRemainingMonths: 12));
    }

    [Fact]
    public void 清除状态位前必须先清空对应计时()
    {
        var person = FamilyFixtures.NewPerson(803, "测试", Gender.Male, 1, 1, generation: 0);
        person.Status = StatusFlag.ServingSentence;
        person.Timers = new StatusTimers(sentenceRemainingMonths: 24);

        Assert.Throws<ArgumentException>(() => person.Status = StatusFlag.None);

        person.Timers = default;
        person.Status = StatusFlag.None;

        Assert.Equal(StatusFlag.None, person.Status);
        Assert.Null(person.Timers.SentenceRemainingMonths);
    }

    [Fact]
    public void 计时非空值必须非负()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StatusTimers(sentenceRemainingMonths: -1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FamilyFixtures.NewPerson(804, "测试", Gender.Male, 1, 1, generation: 0).Timers =
                new StatusTimers(promotionBanRemainingMonths: -5));
    }

    [Fact]
    public void 已亡与外嫁决定归档归类()
    {
        var fixture = FamilyFixtures.MultiGeneration();
        var family = fixture.Family;
        var person = family.TryGet(fixture.SonId);

        Assert.NotNull(person);
        Assert.Contains(person, family.RegisteredMembers);

        person.Status |= StatusFlag.Deceased;
        Assert.DoesNotContain(person, family.RegisteredMembers);
        Assert.Contains(person, family.ArchivedMembers);

        person.Status |= StatusFlag.MarriedOut;
        Assert.True(person.Status.HasFlag(StatusFlag.Deceased));
        Assert.True(person.Status.HasFlag(StatusFlag.MarriedOut));
    }
}
