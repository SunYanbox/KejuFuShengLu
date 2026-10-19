using System.Buffers;
using System.Text.Json;
using KFL.Core.Enums;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save;
using KFL.Infrastructure.Save.Serialization;
using MessagePack;
using Xunit;

namespace KFL.Tests.Infrastructure;

public sealed partial class SaveFormatContractTests
{
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

        Assert.Equal(10, reader.ReadArrayHeader());
        Assert.Equal(SaveFixtures.PlainStateId, Guid.Parse(reader.ReadString()!));
        Assert.Equal(1, reader.ReadInt32());
        Assert.Equal(1, reader.ReadInt32());
        Assert.Equal((int)Difficulty.Hard, reader.ReadInt32());
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
}
