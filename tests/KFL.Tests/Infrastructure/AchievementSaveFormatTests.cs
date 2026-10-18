using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save;
using KFL.Infrastructure.Save.Serialization;
using KFL.Rules.Achievements;
using KFL.Tests.Fixtures;
using MessagePack;
using Xunit;

namespace KFL.Tests.Infrastructure;

/// <summary>
/// T067（并入契约二十二 §5 的 <c>AchievementSaveRoundTripTests</c> 行）：档内成就面与终局状态的
/// 序列化形状 —— 版本闸门、键表、两种格式的信息集等价与**次序**保真，以及「持久字段不超过 8」。
/// </summary>
/// <remarks>
/// SC-011 / SC-014。次序不可丢的三处：<c>Lifespans</c>（寿命序列，编年史 ⑥ 的输入）、
/// <c>Completed</c> / <c>PendingUnlocks</c>（先完成者保持先完成）、<c>Genealogy</c>（编年史 ⑦）。
/// </remarks>
public class AchievementSaveFormatTests
{
    [Fact]
    public void 存档格式版本为五且主体恰十个键()
    {
        Assert.Equal(5, SaveFileFormat.Version);

        var members = typeof(SaveStateDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (
                Key: property.GetCustomAttribute<KeyAttribute>(),
                Name: property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name))
            .ToList();

        Assert.Equal(10, members.Count);
        Assert.All(members, member =>
        {
            Assert.NotNull(member.Key);
            Assert.NotNull(member.Name);
        });

        var ordered = members.OrderBy(member => member.Key!.IntKey!.Value).ToList();

        Assert.Equal(Enumerable.Range(0, 10), ordered.Select(member => member.Key!.IntKey!.Value));
        Assert.Equal("achievements", ordered[8].Name);
        Assert.Equal("ending", ordered[9].Name);

        // 键 8 是必填（档内队列恒在），键 9 是**可空**（未绝嗣 ⇒ null）：不带 [JsonRequired]。
        Assert.Null(typeof(SaveStateDto).GetProperty("Ending")!.GetCustomAttribute<JsonRequiredAttribute>());
        Assert.NotNull(typeof(SaveStateDto).GetProperty("Achievements")!.GetCustomAttribute<JsonRequiredAttribute>());
    }

    [Fact]
    public void 成就面五个DTO的键数与契约一致()
    {
        AssertKeyCount<AchievementStateDto>(8);
        AssertKeyCount<AchievementMarkDto>(4);
        AssertKeyCount<PendingUnlockDto>(5);
        AssertKeyCount<EndingStateDto>(3);
        AssertKeyCount<ChronicleDto>(7);
    }

    [Fact]
    public void 未绝嗣时两种格式都成功往返且ending为空()
    {
        var state = SaveFixtures.PlainState();

        Assert.False(state.Ending.IsEnded);

        // 二进制侧：键 9 走 TryReadNil()（walker 遇 0xC0 即跳过，不当作数组头）。
        using (var binarySandbox = new SaveTestSandbox())
        {
            var slot = new SaveSlot("甲档", SaveFormat.Binary);
            var service = binarySandbox.CreateService();

            service.Save(state, slot, overwrite: false);
            var loaded = service.Load(slot);

            Assert.False(loaded.Ending.IsEnded);
            SaveStateComparer.AssertEquivalent(state, loaded);
        }

        // JSON 侧：`ending` 键**存在但为 null**（键的存在性由格式自己的清单校验，
        // 而不是靠 [JsonRequired] —— 这正是 007 后记里那条对称性）。
        using var jsonSandbox = new SaveTestSandbox();
        var jsonSlot = new SaveSlot("乙档", SaveFormat.Json);
        var jsonService = jsonSandbox.CreateService();

        jsonService.Save(state, jsonSlot, overwrite: false);

        // 用 `JsonDocument` 而不是 `JsonNode`：后者的索引器把「键在且值为 null」与「键不在」
        // 都返回 `null`，读不出「键存在性」这一面。
        using var document = JsonDocument.Parse(SaveTestSandbox.ReadBytes(jsonService.FilePathOf(jsonSlot)));
        var stateElement = document.RootElement.GetProperty(StateName);

        Assert.True(stateElement.TryGetProperty("ending", out var ending));
        Assert.Equal(JsonValueKind.Null, ending.ValueKind);
        Assert.False(jsonService.Load(jsonSlot).Ending.IsEnded);
    }

    [Fact]
    public void 主体缺ending键时仍报InvalidStructure()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("丙档", SaveFormat.Json);

        service.Save(SaveFixtures.PlainState(), slot, overwrite: false);

        // 键 9 不带 [JsonRequired]，但「键**缺失**」仍必须被格式闸门拒绝（悄悄默认成 null 不行）。
        RewritePayload(
            service.FilePathOf(slot),
            state => Assert.True(state.Remove("ending"), "测试前提失败：主体里没有 ending。"));

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));

        Assert.Equal(SaveFileErrorKind.InvalidStructure, exception.Kind);
    }

    [Fact]
    public void 两种格式承载同一成就信息集且次序不变()
    {
        var state = EndedAchievementState();

        using var binarySandbox = new SaveTestSandbox();
        var binarySlot = new SaveSlot("甲档", SaveFormat.Binary);
        binarySandbox.CreateService().Save(state, binarySlot, overwrite: false);
        var fromBinary = binarySandbox.CreateService().Load(binarySlot);

        using var jsonSandbox = new SaveTestSandbox();
        var jsonSlot = new SaveSlot("乙档", SaveFormat.Json);
        jsonSandbox.CreateService().Save(state, jsonSlot, overwrite: false);
        var fromJson = jsonSandbox.CreateService().Load(jsonSlot);

        // 逐字段往返一致（含 Lifespans / Completed / PendingUnlocks / Genealogy 的**次序**）。
        SaveStateComparer.AssertEquivalent(state, fromBinary);
        SaveStateComparer.AssertEquivalent(state, fromJson);

        // 母本本身就是「次序不平凡」的：寿命序列不是升序、编年史谱系按家族成员次序。
        Assert.Equal(UnsortedLifespans, state.Achievements.Lifespans);
        Assert.Equal(
            state.Family.Members.Select(member => member.Id),
            state.Ending.Chronicle!.Genealogy);

        Assert.True(fromJson.Ending.IsEnded);
        Assert.Equal(state.Ending.EndedAt, fromJson.Ending.EndedAt);
    }

    [Fact]
    public void 档内成就持久字段不超过八个()
    {
        var persisted = typeof(AchievementState)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(8, persisted.Count);
        Assert.Equal(PersistedFieldNames, persisted.Order(StringComparer.Ordinal).ToArray());

        // 序列化侧同样恰 8 个键（DTO 是持久面的一面镜子）。
        AssertKeyCount<AchievementStateDto>(8);
    }

    /// <summary>造一个「已绝嗣 + 三项累计量 + 两条完成项 + 一条待落盘」的厚状态。</summary>
    /// <returns>状态。</returns>
    /// <remarks>
    /// 全部成员都已亡故是**有意为之**：校验第 18 条把「寿命序列条数」钉死在已亡人数上，
    /// 于是只有这种状态才能同时具备非空的 <c>Lifespans</c> 与非空的 <c>Genealogy</c>。
    /// </remarks>
    private static GameState EndedAchievementState()
    {
        var family = new Family("试家");
        var founder = family.AddFoundingMember(
            FamilyFixtures.NewPerson(1, "甲", Gender.Male, 1, 1, generation: 0, lifespan: 70));
        var son = family.AddChild(
            FamilyFixtures.NewPerson(2, "乙", Gender.Male, 25, 3, generation: 1, fatherId: founder.Id, lifespan: 40));
        var grandson = family.AddChild(
            FamilyFixtures.NewPerson(3, "丙", Gender.Male, 50, 5, generation: 2, fatherId: son.Id, lifespan: 55));

        var economy = new FamilyEconomy(LivingStandard.Normal, new GrainPriceIndex(1.000m));
        var state = new GameState(
            Guid.Parse("2f1c4a55-6b0d-4e77-9c31-8a5e2d7f0b13"),
            new GameDate(1, 7),
            Difficulty.Hard,
            Origin.Merchant,
            family,
            economy);

        // 寿命序列按**非升序**追加，令「次序保真」这一面真的能被观察到。
        foreach (var (member, age) in new[] { (founder, 70), (son, 40), (grandson, 55) })
        {
            member.Status |= StatusFlag.Deceased;
            state.Achievements.AppendLifespan(age);
        }

        family.SetHead(null);

        state.Achievements.RecordSuccessfulBribes(3);
        state.Achievements.RecordBirths(2);
        state.Achievements.ObserveTotalAssets(Money.FromGuan(123.456m));
        state.Achievements.RecordLoanOriginated();
        state.Achievements.MarkCompleted(
            new AchievementMark(AchievementId.FirstJuRen, Difficulty.Hard, new GameDate(1, 2)));
        state.Achievements.MarkCompletedAndEnqueue(
            new AchievementMark(AchievementId.FirstJinShi, Difficulty.Hard, new GameDate(1, 5)),
            state.Id);
        state.Achievements.MarkCompletedAndEnqueue(
            new AchievementMark(AchievementId.CenturyHouse, Difficulty.Hard, new GameDate(1, 6)),
            state.Id);

        Assert.True(ExtinctionJudge.TryTrigger(state));

        return state;
    }

    private static void AssertKeyCount<TDto>(int expected)
    {
        var keyCount = typeof(TDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Count(property => property.GetCustomAttribute<KeyAttribute>() is not null);

        Assert.Equal(expected, keyCount);
    }

    /// <summary>改写 JSON 主体并**重算 PayloadLength**（与 <c>SaveFailureTests</c> 同款做法）。</summary>
    /// <param name="path">档路径。</param>
    /// <param name="mutate">对主体对象的就地修改。</param>
    private static void RewritePayload(string path, Action<JsonObject> mutate)
    {
        var file = JsonNode.Parse(SaveTestSandbox.ReadBytes(path))!.AsObject();
        var state = file[StateName]!.AsObject();
        mutate(state);

        var stateJson = state.ToJsonString(PayloadJsonOptions);
        var payloadLength = Encoding.UTF8.GetByteCount(stateJson);
        var version = file[FormatVersionName]!.GetValue<int>();
        var saveId = file[SaveIdName]!.GetValue<string>();

        var text = string.Concat(
            "{\n  \"", FormatVersionName, "\": ", version.ToString(CultureInfo.InvariantCulture),
            ",\n  \"", PayloadLengthName, "\": ", payloadLength.ToString(CultureInfo.InvariantCulture),
            ",\n  \"", SaveIdName, "\": \"", saveId,
            "\",\n  \"", StateName, "\": ", stateJson, "\n}");

        SaveTestSandbox.WriteBytes(path, Encoding.UTF8.GetBytes(text));
    }

    private const string FormatVersionName = "FormatVersion";

    private const string PayloadLengthName = "PayloadLength";

    private const string SaveIdName = "SaveId";

    private const string StateName = "State";

    /// <summary>母本的寿命序列：刻意非升序，用来观察「次序保真」。</summary>
    private static readonly int[] UnsortedLifespans = [70, 40, 55];

    /// <summary>八项持久字段的字面名单（契约二十三 §7.1 的 <c>AchievementStateDto</c> 键表）。</summary>
    private static readonly string[] PersistedFieldNames =
    [
        "BirthCount", "Completed", "DebugWakeCount", "EverHadLoan", "Lifespans",
        "PeakTotalAssets", "PendingUnlocks", "SuccessfulBribeCount",
    ];

    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
