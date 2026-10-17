using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    /// <summary>FR-021 / SC-010：1200 回合的账本单文件全量落盘，逐条一致、损失数为 0。</summary>
    [Theory]
    [InlineData(SaveFormat.Binary)]
    [InlineData(SaveFormat.Json)]
    public void 千二百回合的账本单文件全量落盘(SaveFormat format)
    {
        const int Entries = 1200;
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("长局", format);
        var state = LongLedgerState(Entries);

        service.Save(state, slot, overwrite: false);
        var loaded = service.Load(slot);

        // T054 / SC-010：不只数条目数——**逐条**比对日期、类别、归属与金额（含标度）。
        // 顺序被反转、条目被替换、金额掉标度，都会在这里亮。
        Assert.Equal(Entries, loaded.Economy.Ledger.Entries.Count);

        for (var index = 0; index < Entries; index++)
        {
            var expected = state.Economy.Ledger.Entries[index];
            var actual = loaded.Economy.Ledger.Entries[index];

            Assert.Equal(expected.Date, actual.Date);
            Assert.Equal(expected.Category, actual.Category);
            Assert.Equal(expected.PersonId, actual.PersonId);
            SaveStateComparer.AssertBits(expected.Amount.Wen, actual.Amount.Wen);
        }

        Assert.Single(sandbox.RootFiles());
        Assert.Empty(sandbox.BackupFiles());

        // 契约九 §5 条款 3「按标识定位只读头」：档远大于调用方给的头探测前缀（4 KiB）时，
        // 前缀读 MUST 仍能读出完整的头——否则 2.8 的最小读取面会把大档定位成「找不到」。
        Assert.True(SaveTestSandbox.ReadBytes(service.FilePathOf(slot)).Length > 4096, "测试前提失败：这档没超过头探测前缀。");
        Assert.Equal(slot, service.FindBySaveId(state.Id));
    }

    // ───────────────────────────── T053：嵌套位置式数组的长度 ─────────────────────────────

    /// <summary>
    /// T053 / 契约十 §4 / §5 条款 3：**嵌套**的位置式数组短了 MUST 被检出为结构不符。
    /// </summary>
    /// <remarks>
    /// MessagePack-CSharp 2.5.305 对「短于 <c>maxKey</c> 的数组」**不抛异常**，只把缺失成员静默
    /// 留成默认值（实测：4 成员的 <c>TalentDto</c> 砍成 <c>fixarray(3)</c> 后得到 <c>Craft = 0</c>）。
    /// 这条用例把「静默默认化」钉死在失败侧：缺成员 ⇒ <c>InvalidStructure</c>，绝不悄悄变 0。
    /// </remarks>
    [Fact]
    public void 二进制嵌套数组短了被检出为结构不符()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("短档", SaveFormat.Binary);
        var state = SaveFixtures.RichState();

        // 先把合法档写下去（顺带建出存档根），再用「砍短的嵌套数组」覆盖同一路径。
        service.Save(state, slot, overwrite: false);

        var shortened = ShortenFirstTalentArray(MessagePackSerializer.Serialize(
            SaveStateMapper.ToDto(state),
            SaveFileFormat.BinaryOptions));

        SaveTestSandbox.WriteBytes(sandbox.PathOf(slot.Name, slot.Format), ComposeFile(state.Id, shortened));

        var error = Assert.Throws<SaveFileException>(() => service.Load(slot));
        Assert.Equal(SaveFileErrorKind.InvalidStructure, error.Kind);
    }

    /// <summary>把主体里第一个 <c>TalentDto</c> 位置式数组砍掉最后一个成员。</summary>
    /// <param name="payload">合法主体字节。</param>
    /// <returns>砍短后的主体字节。</returns>
    private static byte[] ShortenFirstTalentArray(byte[] payload)
    {
        var reader = new MessagePackReader(payload);
        reader.ReadArrayHeader();                       // SaveStateDto
        SkipValues(ref reader, 6);                      // 0..5 标量
        reader.ReadArrayHeader();                       // FamilyDto
        SkipValues(ref reader, 3);                      // 0..2 标量
        Assert.True(reader.ReadArrayHeader() > 0);      // Members

        reader.ReadArrayHeader();                       // PersonDto
        SkipValues(ref reader, 6);                      // 0..5 标量

        var talentsStart = checked((int)reader.Consumed);
        Assert.Equal(4, reader.ReadArrayHeader());      // TalentDto：4 个成员
        Assert.Equal(0x94, payload[talentsStart]);      // fixarray(4)：头恰 1 字节

        SkipValues(ref reader, 3);
        var afterThree = checked((int)reader.Consumed);
        reader.Skip();
        var afterFour = checked((int)reader.Consumed);

        var shortened = new List<byte>(payload.Length - 1);
        shortened.AddRange(payload[..talentsStart]);
        shortened.Add(0x93);                            // fixarray(3)
        shortened.AddRange(payload[(talentsStart + 1)..afterThree]);
        shortened.AddRange(payload[afterFour..]);
        return [.. shortened];
    }

    /// <summary>按契约十 §2 拼一个二进制文件（头 = fixarray(3) + 版本 + 主体长度 + 标识）。</summary>
    /// <param name="saveId">存档标识。</param>
    /// <param name="payload">主体字节。</param>
    /// <returns>文件字节。</returns>
    private static byte[] ComposeFile(Guid saveId, byte[] payload)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(3);
        writer.Write(SaveFileFormat.Version);
        writer.Write(payload.Length);
        writer.Write(saveId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
        writer.Flush();

        var file = new byte[buffer.WrittenCount + payload.Length];
        buffer.WrittenSpan.CopyTo(file);
        payload.CopyTo(file, buffer.WrittenCount);
        return file;
    }

    private static void SkipValues(ref MessagePackReader reader, int count)
    {
        for (var index = 0; index < count; index++)
        {
            reader.Skip();
        }
    }

    // ───────────────────────────── T055：[Key(n)] 的键序号 ─────────────────────────────

    /// <summary>
    /// T055 / 契约十 §4 + §5 条款 4：每个 DTO 的 <c>[Key(n)]</c> MUST 恰为 <c>0..N-1</c>，
    /// 且第 <c>n</c> 个键 MUST 落在契约键表第 <c>n</c> 个成员上。
    /// </summary>
    /// <remarks>
    /// 位置式数组的「位置」就是 <c>[Key]</c> 序号：只断言集合是 <c>0..N-1</c> 还不够——
    /// 交换两个 <c>[Key]</c> 后集合照旧完整，但盘上的字节与契约错位。故这里按序号排好序后
    /// 与契约 §4 的成员次序逐位比对。
    /// </remarks>
    [Fact]
    public void 二进制键序号自零连续且与契约键表逐位一致()
    {
        foreach (var (type, namesByKey) in BinaryKeyTables)
        {
            var members = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => (
                    Key: property.GetCustomAttribute<KeyAttribute>(),
                    Name: property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name))
                .ToList();

            // 成员数与契约键表一致，且每个成员都带 [Key] / [JsonPropertyName]。
            Assert.Equal(namesByKey.Length, members.Count);
            Assert.All(members, member =>
            {
                Assert.NotNull(member.Key);
                Assert.NotNull(member.Name);
            });

            var ordered = members.OrderBy(member => member.Key!.IntKey!.Value).ToList();

            // 序号 MUST 恰为 0..N-1：不重、不漏、不从 1 起。
            Assert.Equal(
                Enumerable.Range(0, namesByKey.Length),
                ordered.Select(member => member.Key!.IntKey!.Value));

            // 第 n 个键 MUST 是契约键表的第 n 个成员。
            Assert.Equal(namesByKey, ordered.Select(member => member.Name));
        }
    }

    // ─────────────────── T053/T055 的钉死面：walker 的成员数常量 ───────────────────

    /// <summary>
    /// 契约十 §4 + §5 条款 4：<c>MessagePackSaveFormat</c> walker 的成员数常量 MUST 与各 DTO 的
    /// <c>[Key]</c> 数量逐一相等，头记录恒为 3 个成员。
    /// </summary>
    /// <remarks>
    /// walker 是**手写**的位置式遍历：给某个 DTO 增删一个 <c>[Key]</c> 而忘了改常量时，
    /// 构建与 T055 都照旧全绿，walker 却会静默走错位置（审核 2.10）。本用例把这张对应表钉死，
    /// 使它不再只靠注释声称。
    /// </remarks>
    [Fact]
    public void 二进制walker的成员数常量与DTO键数逐一相等()
    {
        var constants = typeof(MessagePackSaveFormat)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(int))
            .ToDictionary(
                field => field.Name,
                field => (int)field.GetRawConstantValue()!,
                StringComparer.Ordinal);

        (string Constant, Type Dto)[] mapping =
        [
            ("SaveStateMemberCount", typeof(SaveStateDto)),
            ("FamilyMemberCount", typeof(FamilyDto)),
            ("PersonMemberCount", typeof(PersonDto)),
            ("TalentMemberCount", typeof(TalentDto)),
            ("StatusTimersMemberCount", typeof(StatusTimersDto)),
            ("DegreeRecordMemberCount", typeof(DegreeRecordDto)),
            ("FamilyEconomyMemberCount", typeof(FamilyEconomyDto)),
            ("TreasuryMemberCount", typeof(TreasuryDto)),
            ("LoanMemberCount", typeof(LoanDto)),
            ("HoldingsMemberCount", typeof(HoldingsDto)),
            ("LedgerEntryMemberCount", typeof(LedgerEntryDto)),
            ("FamineMemberCount", typeof(FamineDto)),
            ("PunishmentJournalMemberCount", typeof(PunishmentJournalDto)),
            ("BribeFactMemberCount", typeof(BribeFactDto)),
            ("SentenceFactMemberCount", typeof(SentenceFactDto)),
            ("FineFactMemberCount", typeof(FineFactDto)),
            ("RelativeBanFactMemberCount", typeof(RelativeBanFactDto)),
            ("CollectivePunishmentFactMemberCount", typeof(CollectivePunishmentFactDto)),
            ("ReleaseFactMemberCount", typeof(ReleaseFactDto)),
            ("FamilyLifecycleMemberCount", typeof(FamilyLifecycleDto)),
            ("PendingBirthMemberCount", typeof(PendingBirthDto)),
            ("CoupleKeyMemberCount", typeof(CoupleKeyDto)),
            ("MatchmakeApplicationMemberCount", typeof(MatchmakeApplicationDto)),
            ("MarriageProposalMemberCount", typeof(MarriageProposalDto)),
        ];

        foreach (var (constant, dto) in mapping)
        {
            Assert.True(constants.ContainsKey(constant), $"MessagePackSaveFormat 缺成员数常量 {constant}。");

            var keyCount = dto.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Count(property => property.GetCustomAttribute<KeyAttribute>() is not null);

            Assert.Equal(keyCount, constants[constant]);
        }

        // 头记录 = 版本 + 主体长度 + 标识（契约十 §2）。
        Assert.Equal(3, constants["HeaderMemberCount"]);
    }

    /// <summary>契约十 §4 的二进制键表：<c>[Key(0..N-1)]</c> 依次对应的 JSON 成员名。</summary>
    private static readonly (Type Type, string[] NamesByKey)[] BinaryKeyTables =
    [
        (typeof(SaveStateDto),
            ["id", "currentYear", "currentMonth", "difficulty", "pendingDifficulty", "origin", "family", "economy"]),
        (typeof(FamilyDto),
            ["name", "hasShiStatus", "headId", "members", "settledOutsiders", "marriageAlignedOutsiders",
                "autoExamApplication", "punishmentJournal", "lifecycle"]),
        (typeof(PersonDto),
            [
                "id", "name", "gender", "birthYear", "birthMonth", "generation", "talents", "study",
                "health", "lifespan", "rankLevel", "merit", "status", "timers", "occupation",
                "monthsInOffice", "entryTrack", "fatherId", "motherId", "spouseId", "formerSpouseIds",
                "degreeHistory", "jieShiFailureCount", "shengShiFailureCount", "appliedExam",
                "jieShiBribeCount", "shengShiBribeCount", "dianShiBribeCount", "isTutoredByMaster",
            ]),
        (typeof(TalentDto), ["agriculture", "commerce", "officialdom", "craft"]),
        (typeof(StatusTimersDto),
            ["sentenceRemainingMonths", "examBanRemainingMonths", "promotionBanRemainingMonths",
                "awaitingPostRemainingMonths", "illnessSeverityBasisPoints", "treatmentRemainingMonths"]),
        (typeof(DegreeRecordDto),
            ["level", "placement", "changedYear", "changedMonth", "cause", "imperialClass"]),
        (typeof(FamilyEconomyDto),
            ["livingStandard", "pendingLivingStandard", "grainPriceIndex", "treasury", "holdings", "ledger", "famine"]),
        (typeof(TreasuryDto),
            ["cash", "savings", "merchantCapital", "loan", "savingsRate", "savingsRateYear"]),
        (typeof(LoanDto), ["principal", "accruedInterest", "monthsSinceInterest"]),
        (typeof(HoldingsDto), ["farmlandMu", "ruralHouses", "urbanHouses", "shops"]),
        (typeof(LedgerEntryDto), ["year", "month", "personId", "category", "amountWen"]),
        (typeof(FamineDto), ["stage", "elapsedMonths"]),

        // 006-bribery-punishment：惩罚日志与六类事实行（键均自 0 连续，与 Core 记录成员逐一对应）。
        (typeof(PunishmentJournalDto),
            ["familyBanDeclared", "bribes", "sentences", "fines", "relativeBans", "collectivePunishments",
                "releases"]),
        (typeof(GameDateDto), ["year", "month"]),
        (typeof(BribeFactDto),
            ["date", "examinee", "level", "nth", "amountWen", "caught", "capReached"]),
        (typeof(SentenceFactDto), ["date", "subject", "months"]),
        (typeof(FineFactDto), ["date", "subject", "amountWen", "paidFromTreasuryWen", "toLoanWen"]),
        (typeof(RelativeBanFactDto),
            ["date", "subject", "relative", "examBanMonths", "promotionBanMonths"]),
        (typeof(CollectivePunishmentFactDto),
            ["date", "subject", "outcome", "relative", "degreeBefore", "degreeAfter"]),
        (typeof(ReleaseFactDto), ["date", "subject", "newRankLevel"]),

        // 007-family-lifecycle：家族生命周期簿记、一条待应答提议与一条当月托媒名额（键均自 0 连续）。
        (typeof(FamilyLifecycleDto),
            ["purchaseCount", "purchaseYear", "pendingBirths", "appliedBirthCouples", "birthApplicationYear",
                "proposal", "matchmakeApplications"]),
        (typeof(PendingBirthDto), ["husbandId", "wifeId", "appliedYear", "appliedMonth"]),
        (typeof(CoupleKeyDto), ["husbandId", "wifeId"]),
        (typeof(MatchmakeApplicationDto), ["memberId", "appliedYear", "appliedMonth"]),
        (typeof(MarriageProposalDto),
            [
                "kind", "ourMemberId", "candidateName", "candidateGender", "candidateAge",
                "talentAgriculture", "talentCommerce", "talentScholar", "talentCraft", "candidateStudy",
                "candidateHealth", "candidateLifespan", "candidateDegree", "class", "betrothalGift",
                "dowry", "proposedYear", "proposedMonth", "candidatePlacement",
            ]),
    ];

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
        "family.autoExamApplication",
        "family.hasShiStatus",
        "family.headId",
        // 007：夹具的厚状态把五组生命周期状态与当月托媒名额都填成非空，故集合元素与嵌套提议的
        //      每个键都会实际出现在 JSON 里；四个新 DTO 的内部键位另由上面的 BinaryKeyTables 与
        //      walker 常量表钉死。
        "family.lifecycle",
        "family.lifecycle.appliedBirthCouples",
        "family.lifecycle.appliedBirthCouples[].husbandId",
        "family.lifecycle.appliedBirthCouples[].wifeId",
        "family.lifecycle.birthApplicationYear",
        "family.lifecycle.matchmakeApplications",
        "family.lifecycle.matchmakeApplications[].appliedMonth",
        "family.lifecycle.matchmakeApplications[].appliedYear",
        "family.lifecycle.matchmakeApplications[].memberId",
        "family.lifecycle.pendingBirths",
        "family.lifecycle.pendingBirths[].appliedMonth",
        "family.lifecycle.pendingBirths[].appliedYear",
        "family.lifecycle.pendingBirths[].husbandId",
        "family.lifecycle.pendingBirths[].wifeId",
        "family.lifecycle.proposal",
        "family.lifecycle.proposal.betrothalGift",
        "family.lifecycle.proposal.candidateAge",
        "family.lifecycle.proposal.candidateDegree",
        "family.lifecycle.proposal.candidateGender",
        "family.lifecycle.proposal.candidateHealth",
        "family.lifecycle.proposal.candidateLifespan",
        "family.lifecycle.proposal.candidateName",
        "family.lifecycle.proposal.candidatePlacement",
        "family.lifecycle.proposal.candidateStudy",
        "family.lifecycle.proposal.class",
        "family.lifecycle.proposal.dowry",
        "family.lifecycle.proposal.kind",
        "family.lifecycle.proposal.ourMemberId",
        "family.lifecycle.proposal.proposedMonth",
        "family.lifecycle.proposal.proposedYear",
        "family.lifecycle.proposal.talentAgriculture",
        "family.lifecycle.proposal.talentCommerce",
        "family.lifecycle.proposal.talentCraft",
        "family.lifecycle.proposal.talentScholar",
        "family.lifecycle.purchaseCount",
        "family.lifecycle.purchaseYear",
        "family.marriageAlignedOutsiders",
        "family.members",
        "family.members[].appliedExam",
        "family.members[].birthMonth",
        "family.members[].birthYear",
        "family.members[].degreeHistory",
        "family.members[].degreeHistory[].cause",
        "family.members[].degreeHistory[].changedMonth",
        "family.members[].degreeHistory[].changedYear",
        "family.members[].degreeHistory[].imperialClass",
        "family.members[].degreeHistory[].level",
        "family.members[].degreeHistory[].placement",
        "family.members[].dianShiBribeCount",
        "family.members[].entryTrack",
        "family.members[].fatherId",
        "family.members[].formerSpouseIds",
        "family.members[].gender",
        "family.members[].generation",
        "family.members[].health",
        "family.members[].id",
        "family.members[].isTutoredByMaster",
        "family.members[].jieShiBribeCount",
        "family.members[].jieShiFailureCount",
        "family.members[].lifespan",
        "family.members[].merit",
        "family.members[].monthsInOffice",
        "family.members[].motherId",
        "family.members[].name",
        "family.members[].occupation",
        "family.members[].rankLevel",
        "family.members[].shengShiBribeCount",
        "family.members[].shengShiFailureCount",
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
        "family.members[].timers.illnessSeverityBasisPoints",
        "family.members[].timers.promotionBanRemainingMonths",
        "family.members[].timers.sentenceRemainingMonths",
        "family.members[].timers.treatmentRemainingMonths",
        "family.name",
        "family.punishmentJournal",
        "family.punishmentJournal.bribes",
        "family.punishmentJournal.bribes[].amountWen",
        "family.punishmentJournal.bribes[].capReached",
        "family.punishmentJournal.bribes[].caught",
        "family.punishmentJournal.bribes[].date",
        "family.punishmentJournal.bribes[].date.month",
        "family.punishmentJournal.bribes[].date.year",
        "family.punishmentJournal.bribes[].examinee",
        "family.punishmentJournal.bribes[].level",
        "family.punishmentJournal.bribes[].nth",
        "family.punishmentJournal.collectivePunishments",
        "family.punishmentJournal.collectivePunishments[].date",
        "family.punishmentJournal.collectivePunishments[].date.month",
        "family.punishmentJournal.collectivePunishments[].date.year",
        "family.punishmentJournal.collectivePunishments[].degreeAfter",
        "family.punishmentJournal.collectivePunishments[].degreeBefore",
        "family.punishmentJournal.collectivePunishments[].outcome",
        "family.punishmentJournal.collectivePunishments[].relative",
        "family.punishmentJournal.collectivePunishments[].subject",
        "family.punishmentJournal.familyBanDeclared",
        "family.punishmentJournal.fines",
        "family.punishmentJournal.fines[].amountWen",
        "family.punishmentJournal.fines[].date",
        "family.punishmentJournal.fines[].date.month",
        "family.punishmentJournal.fines[].date.year",
        "family.punishmentJournal.fines[].paidFromTreasuryWen",
        "family.punishmentJournal.fines[].subject",
        "family.punishmentJournal.fines[].toLoanWen",
        "family.punishmentJournal.relativeBans",
        "family.punishmentJournal.relativeBans[].date",
        "family.punishmentJournal.relativeBans[].date.month",
        "family.punishmentJournal.relativeBans[].date.year",
        "family.punishmentJournal.relativeBans[].examBanMonths",
        "family.punishmentJournal.relativeBans[].promotionBanMonths",
        "family.punishmentJournal.relativeBans[].relative",
        "family.punishmentJournal.relativeBans[].subject",
        "family.punishmentJournal.releases",
        "family.punishmentJournal.releases[].date",
        "family.punishmentJournal.releases[].date.month",
        "family.punishmentJournal.releases[].date.year",
        "family.punishmentJournal.releases[].newRankLevel",
        "family.punishmentJournal.releases[].subject",
        "family.punishmentJournal.sentences",
        "family.punishmentJournal.sentences[].date",
        "family.punishmentJournal.sentences[].date.month",
        "family.punishmentJournal.sentences[].date.year",
        "family.punishmentJournal.sentences[].months",
        "family.punishmentJournal.sentences[].subject",
        "family.settledOutsiders",
        "id",
        "origin",
        "pendingDifficulty",
    ];
}
