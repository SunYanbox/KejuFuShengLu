using System.Text;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Rules.Config;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Infrastructure;

public sealed partial class SaveServiceTests
{
    [Fact]
    public void 过期提议指向已婚成员时读档自愈为无提议()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("甲档");

        // 造历史缺陷：提议在成员成婚**之后**才写进去（旧实现里成婚路径不清提议，
        // 于是存档变成「提议的 OurMemberId 已有配偶」，读档被第 13 条拒绝）。
        var state = SaveFixtures.PlainState();
        var member = state.Family.Members.First();
        state.Family.Marry(member.Id, AddSpouse(state.Family, member));

        state.Family.Lifecycle.SetProposal(new MarriageProposal
        {
            Kind = MarriageProposalKind.Matchmaker,
            OurMemberId = member.Id,
            CandidateName = "陈氏",
            CandidateGender = Gender.Female,
            CandidateAge = 18,
            TalentAgriculture = 50,
            TalentCommerce = 50,
            TalentScholar = 50,
            TalentCraft = 50,
            CandidateStudy = 30,
            CandidateHealth = 80,
            CandidateLifespan = 60,
            CandidateDegree = DegreeLevel.BaiShen,
            Class = MarriageClass.Commoner,
            BetrothalGift = Money.FromGuan(20m),
            Dowry = Money.FromGuan(10m),
            ProposedAt = state.CurrentDate,
        });

        service.Save(state, slot, overwrite: false);

        // 读档 MUST 成功，且过期提议被就地清空（其余字段一字未改）。
        var loaded = service.Load(slot);

        Assert.Null(loaded.Family.Lifecycle.Proposal);
        Assert.Equal(member.Id, loaded.Family.Members.First().Id);
        Assert.NotNull(loaded.Family.Members.First().SpouseId);
    }

    /// <summary>给 <paramref name="head"/> 添一位成年异性配偶（外来者），返回其标识。</summary>
    private static PersonId AddSpouse(Family family, Person head)
    {
        var spouse = family.AddOutsider(FamilyFixtures.NewPerson(
            99,
            "外氏",
            Gender.Female,
            20,
            1,
            generation: head.Generation));

        return spouse.Id;
    }

    [Fact]
    public void 生育申请后离异的档案读档自愈为无簿记()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("甲档", SaveFormat.Json);

        // 造历史缺陷：本年度已申请生育的夫妇后来离了婚（旧实现不清生育簿记，
        // 于是存档变成「已申请生育的夫妇不再互为配偶」，读档被第 14 条拒绝）。
        var state = SaveFixtures.PlainState();
        var husband = state.Family.Members.First();
        var wifeId = AddSpouse(state.Family, husband);
        state.Family.Marry(husband.Id, wifeId);

        var couple = new CoupleKey(husband.Id, wifeId);
        state.Family.Lifecycle.RecordBirthApplication(couple, state.CurrentDate.Year);
        state.Family.Lifecycle.ScheduleBirth(new PendingBirth(couple, state.CurrentDate));

        service.Save(state, slot, overwrite: false);

        // 直接改盘上 JSON：只断配偶链、留簿记——正是旧版本写出来的那种「半截状态」。
        BreakSpouseLinks(sandbox.PathOf("甲档", SaveFormat.Json), husband.Id, wifeId);

        var loaded = service.Load(slot);

        Assert.Empty(loaded.Family.Lifecycle.AppliedBirthCouples);
        Assert.Empty(loaded.Family.Lifecycle.PendingBirths);
    }

    [Fact]
    public void 活着且互为配偶的生育申请不会被读档自愈误删()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("甲档");

        var state = SaveFixtures.PlainState();
        var husband = state.Family.Members.First();
        var wifeId = AddSpouse(state.Family, husband);
        state.Family.Marry(husband.Id, wifeId);

        var couple = new CoupleKey(husband.Id, wifeId);
        state.Family.Lifecycle.RecordBirthApplication(couple, state.CurrentDate.Year);
        state.Family.Lifecycle.ScheduleBirth(new PendingBirth(couple, state.CurrentDate));

        service.Save(state, slot, overwrite: false);

        var loaded = service.Load(slot);

        Assert.Single(loaded.Family.Lifecycle.AppliedBirthCouples);
        Assert.Single(loaded.Family.Lifecycle.PendingBirths);
    }

    /// <summary>把 JSON 档里这两名成员的 <c>spouseId</c> 改成 <c>null</c>（模拟旧版本写出的半截状态）。</summary>
    private static void BreakSpouseLinks(string path, PersonId first, PersonId second)
    {
        var text = Encoding.UTF8.GetString(SaveTestSandbox.ReadBytes(path));
        var root = System.Text.Json.Nodes.JsonNode.Parse(text)!;
        var targetIds = new[] { first.Value, second.Value };
        var patched = 0;

        foreach (var node in root["State"]!["family"]!["members"]!.AsArray())
        {
            if (node is null || !targetIds.Contains(node["id"]!.GetValue<Guid>()))
            {
                continue;
            }

            node["spouseId"] = null;
            patched++;
        }

        Assert.Equal(2, patched);

        var patchedText = root.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

        SaveTestSandbox.WriteBytes(path, Encoding.UTF8.GetBytes(patchedText));
    }
}
