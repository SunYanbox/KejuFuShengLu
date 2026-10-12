using System.Text.Json;
using KFL.Core.Enums;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save;
using KFL.Infrastructure.Save.Serialization;
using MessagePack;
using Xunit;

namespace KFL.Tests.Infrastructure;

/// <summary>
/// 存档**字节形态**的契约测试（004-save-system；契约十 §2~§5 / tasks.md T030）。
/// </summary>
/// <remarks>
/// 这里断言的是「盘上长什么样」，不是「读回来对不对」——后者由 <c>SaveServiceTests</c> 负责。
/// 两者缺一不可：只比往返会让「两个格式都少存同一个字段」蒙混过关（契约十 §5 条款 3）。
/// </remarks>
public sealed class SaveFormatContractTests
{
    /// <summary>契约十 §2：二进制头 = <c>0x93</c>（fixarray(3)）+ 版本 + 主体长度 + 标识。</summary>
    [Fact]
    public void 二进制头是三元素数组且成员次序固定()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("甲档", SaveFormat.Binary);
        var state = SaveFixtures.PlainState();

        service.Save(state, slot, overwrite: false);
        var bytes = SaveTestSandbox.ReadBytes(service.FilePathOf(slot));

        Assert.Equal(0x93, bytes[0]);

        var header = new MessagePackSaveFormat().ReadHeader(bytes);
        Assert.Equal(SaveFileFormat.Version, header.FormatVersion);
        Assert.Equal(state.Id, header.SaveId);
        Assert.Equal(bytes.Length - HeaderLength(bytes), header.PayloadLength);
    }

    /// <summary>契约十 §2：JSON 头上恰好三个成员，<c>State</c> 紧随其后。</summary>
    [Fact]
    public void JSON头成员只有三个且次序固定()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("甲档", SaveFormat.Json);

        service.Save(SaveFixtures.PlainState(), slot, overwrite: false);

        using var document = JsonDocument.Parse(SaveTestSandbox.ReadBytes(service.FilePathOf(slot)));
        var names = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();

        Assert.Equal(["FormatVersion", "PayloadLength", "SaveId", "State"], names);
        Assert.Equal(SaveFileFormat.Version, document.RootElement.GetProperty("FormatVersion").GetInt32());
    }

    /// <summary>契约十 §4：JSON 键表逐条等于契约表（键名 MUST NOT 随 C# 改名而变）。</summary>
    [Fact]
    public void JSON键表与契约键表逐条一致()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("甲档", SaveFormat.Json);

        service.Save(SaveFixtures.RichState(), slot, overwrite: false);

        using var document = JsonDocument.Parse(SaveTestSandbox.ReadBytes(service.FilePathOf(slot)));
        var paths = new SortedSet<string>(StringComparer.Ordinal);
        CollectPaths(document.RootElement.GetProperty("State"), string.Empty, paths);

        Assert.Equal(ContractKeyTable, paths);
    }

    /// <summary>契约十 §3：枚举在 JSON 里写名字，在二进制里写原始整数。</summary>
    [Fact]
    public void 枚举在JSON写名字在二进制写整数()
    {
        // JSON 侧：难度是字符串 "Hard"。
        using (var sandbox = new SaveTestSandbox())
        {
            var service = sandbox.CreateService();
            var slot = new SaveSlot("甲档", SaveFormat.Json);
            service.Save(SaveFixtures.RichState(), slot, overwrite: false);

            using var document = JsonDocument.Parse(SaveTestSandbox.ReadBytes(service.FilePathOf(slot)));
            var difficulty = document.RootElement.GetProperty("State").GetProperty("difficulty");

            Assert.Equal(JsonValueKind.String, difficulty.ValueKind);
            Assert.Equal(nameof(Difficulty.Hard), difficulty.GetString());
        }

        // 二进制侧：难度是原始整数（位置式数组的第 4 个元素）。
        var dto = new SaveStateDto
        {
            Id = SaveFixtures.PlainStateId,
            CurrentYear = 1,
            CurrentMonth = 1,
            Difficulty = Difficulty.Hard,
            Origin = Origin.Farmer,
        };

        var payload = MessagePackSerializer.Serialize(dto, SaveFileFormat.BinaryOptions);
        var reader = new MessagePackReader(payload);

        Assert.Equal(8, reader.ReadArrayHeader());
        Assert.Equal(SaveFixtures.PlainStateId, Guid.Parse(reader.ReadString()!));
        Assert.Equal(1, reader.ReadInt32());
        Assert.Equal(1, reader.ReadInt32());
        Assert.Equal((int)Difficulty.Hard, reader.ReadInt32());
    }

    /// <summary>契约十 §3 / data-model §3.9：<c>StatusFlag.None</c> 写成空数组，而非 <c>["None"]</c>。</summary>
    [Fact]
    public void 无状态位在JSON里写成空数组()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("甲档", SaveFormat.Json);

        service.Save(SaveFixtures.PlainState(), slot, overwrite: false);

        using var document = JsonDocument.Parse(SaveTestSandbox.ReadBytes(service.FilePathOf(slot)));
        var status = document.RootElement
            .GetProperty("State")
            .GetProperty("family")
            .GetProperty("members")[0]
            .GetProperty("status");

        Assert.Equal(JsonValueKind.Array, status.ValueKind);
        Assert.Equal(0, status.GetArrayLength());
    }

    /// <summary>契约十 §5：两种格式承载同一信息集（同一状态经两格式读回后逐字段相等）。</summary>
    /// <remarks>
    /// 两侧各用一个**独立根**：同一个根下「同一 <c>SaveId</c> 并存于两个文件」会被
    /// <c>SaveIdentityConflictException</c> 拒绝（契约九 §5 的唯一性不变式），
    /// 那不是本用例要验证的东西。
    /// </remarks>
    [Fact]
    public void 两种格式承载同一信息集()
    {
        var state = SaveFixtures.RichState();

        using var binarySandbox = new SaveTestSandbox();
        var binarySlot = new SaveSlot("甲档", SaveFormat.Binary);
        binarySandbox.CreateService().Save(state, binarySlot, overwrite: false);
        var fromBinary = binarySandbox.CreateService().Load(binarySlot);

        using var jsonSandbox = new SaveTestSandbox();
        var jsonSlot = new SaveSlot("甲档", SaveFormat.Json);
        jsonSandbox.CreateService().Save(state, jsonSlot, overwrite: false);
        var fromJson = jsonSandbox.CreateService().Load(jsonSlot);

        SaveStateComparer.AssertEquivalent(fromBinary, fromJson);
    }

    /// <summary>FR-021 / SC-006：1200 回合的账本单文件全量落盘，损失数为 0。</summary>
    [Theory]
    [InlineData(SaveFormat.Binary)]
    [InlineData(SaveFormat.Json)]
    public void 千二百回合的账本单文件全量落盘(SaveFormat format)
    {
        const int Entries = 1200;
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("长局", format);

        service.Save(LongLedgerState(Entries), slot, overwrite: false);
        var loaded = service.Load(slot);

        Assert.Equal(Entries, loaded.Economy.Ledger.Entries.Count);
        Assert.Single(sandbox.RootFiles());
        Assert.Empty(sandbox.BackupFiles());
    }

    /// <summary>造一个账本有 <paramref name="entryCount"/> 条的状态。</summary>
    /// <param name="entryCount">账本条目数。</param>
    /// <returns>状态。</returns>
    private static KFL.Core.Entities.GameState LongLedgerState(int entryCount)
    {
        var plain = SaveFixtures.PlainState();
        var ledger = new KFL.Core.Entities.Ledger();

        for (var index = 0; index < entryCount; index++)
        {
            ledger.Append(new KFL.Core.ValueObjects.LedgerEntry(
                new KFL.Core.ValueObjects.GameDate(1 + (index / 12), (index % 12) + 1),
                personId: null,
                LedgerCategory.LivingCost,
                KFL.Core.ValueObjects.Money.FromWen(-(index + 1))));
        }

        var economy = new KFL.Core.Entities.FamilyEconomy(
            LivingStandard.Normal,
            new KFL.Core.ValueObjects.GrainPriceIndex(1.0m),
            treasury: null,
            holdings: null,
            ledger);

        return new KFL.Core.Entities.GameState(
            plain.Id,
            plain.CurrentDate,
            plain.Difficulty,
            plain.Origin,
            plain.Family,
            economy);
    }

    /// <summary>数出二进制头有多少字节（0x93 + 版本 + 长度 + 标识）。</summary>
    /// <param name="bytes">文件字节。</param>
    /// <returns>头长度。</returns>
    private static int HeaderLength(byte[] bytes)
    {
        var reader = new MessagePackReader(bytes);
        Assert.Equal(3, reader.ReadArrayHeader());
        reader.ReadInt32();
        reader.ReadInt32();
        reader.ReadString();

        return checked((int)reader.Consumed);
    }

    /// <summary>递归收集「成员路径」（数组元素用 <c>[]</c> 标记，同名元素只留一份）。</summary>
    /// <param name="element">当前元素。</param>
    /// <param name="path">到达当前元素的路径。</param>
    /// <param name="paths">收集结果。</param>
    private static void CollectPaths(JsonElement element, string path, ISet<string> paths)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var child = path.Length == 0 ? property.Name : $"{path}.{property.Name}";
                paths.Add(child);
                CollectPaths(property.Value, child, paths);
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectPaths(item, $"{path}[]", paths);
            }
        }
    }

    /// <summary>契约十 §4 的键表（逐条抄自契约，按序数排序）。</summary>
    private static readonly string[] ContractKeyTable =
    [
        "currentMonth",
        "currentYear",
        "difficulty",
        "economy",
        "economy.famine",
        "economy.famine.elapsedMonths",
        "economy.famine.stage",
        "economy.grainPriceIndex",
        "economy.holdings",
        "economy.holdings.farmlandMu",
        "economy.holdings.ruralHouses",
        "economy.holdings.shops",
        "economy.holdings.urbanHouses",
        "economy.ledger",
        "economy.ledger[].amountWen",
        "economy.ledger[].category",
        "economy.ledger[].month",
        "economy.ledger[].personId",
        "economy.ledger[].year",
        "economy.livingStandard",
        "economy.pendingLivingStandard",
        "economy.treasury",
        "economy.treasury.cash",
        "economy.treasury.loan",
        "economy.treasury.loan.accruedInterest",
        "economy.treasury.loan.monthsSinceInterest",
        "economy.treasury.loan.principal",
        "economy.treasury.merchantCapital",
        "economy.treasury.savings",
        "economy.treasury.savingsRate",
        "economy.treasury.savingsRateYear",
        "family",
        "family.hasShiStatus",
        "family.headId",
        "family.marriageAlignedOutsiders",
        "family.members",
        "family.members[].birthMonth",
        "family.members[].birthYear",
        "family.members[].degreeHistory",
        "family.members[].degreeHistory[].cause",
        "family.members[].degreeHistory[].changedMonth",
        "family.members[].degreeHistory[].changedYear",
        "family.members[].degreeHistory[].imperialClass",
        "family.members[].degreeHistory[].level",
        "family.members[].degreeHistory[].placement",
        "family.members[].entryTrack",
        "family.members[].fatherId",
        "family.members[].formerSpouseIds",
        "family.members[].gender",
        "family.members[].generation",
        "family.members[].health",
        "family.members[].id",
        "family.members[].lifespan",
        "family.members[].merit",
        "family.members[].monthsInOffice",
        "family.members[].motherId",
        "family.members[].name",
        "family.members[].occupation",
        "family.members[].rankLevel",
        "family.members[].spouseId",
        "family.members[].status",
        "family.members[].study",
        "family.members[].talents",
        "family.members[].talents.agriculture",
        "family.members[].talents.commerce",
        "family.members[].talents.craft",
        "family.members[].talents.officialdom",
        "family.members[].timers",
        "family.members[].timers.awaitingPostRemainingMonths",
        "family.members[].timers.examBanRemainingMonths",
        "family.members[].timers.promotionBanRemainingMonths",
        "family.members[].timers.sentenceRemainingMonths",
        "family.name",
        "family.settledOutsiders",
        "id",
        "origin",
        "pendingDifficulty",
    ];
}
