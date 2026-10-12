using System.Buffers;
using System.Globalization;
using KFL.Core.Enums;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save;
using KFL.Infrastructure.Save.Serialization;
using MessagePack;
using Xunit;

namespace KFL.Tests.Infrastructure;

/// <summary>
/// 第 ⑩ 步「九条一致性校验」的验收测试（004-save-system；契约十 §6 第 ⑧ 步、001 R-04）。
/// </summary>
/// <remarks>
/// <para>
/// 这九条此前**零直接覆盖**：把 <c>SaveService.Load</c> 里 <c>ValidateIdentity</c> / <c>Validate</c>
/// 那两行注释掉，门禁依然全绿（审核 2.4，变异探针）。本文件逐条从**真实读档路径**
/// （<c>SaveService.Load</c>）走一遍：手工构造违反某条不变量的主体、拼出合法信封，
/// 断言失败分类恒为 <see cref="SaveFileErrorKind.InvalidStructure"/>。
/// </para>
/// <para>
/// 第 7 条（饥馑阶段与计时）与第 8 条（头标识与主体标识）**Core 自身的 setter 拦不住**，
/// 校验器是它们唯一的守门人——这两条用例在校验器被摘掉后必然失败。其余各条 Core 也会拒绝
/// （键值校验、计时与状态位的 setter 前置条件等），本文件把两条路径下的**失败形态**一并钉死。
/// </para>
/// </remarks>
public sealed class SaveStateValidatorTests
{
    /// <summary>不是任何成员的标识（用于第 3、4 条）。</summary>
    private static readonly Guid ForeignId = new("11111111-1111-4111-8111-111111111111");

    /// <summary>与主体标识不同的头标识（用于第 8 条）。</summary>
    private static readonly Guid OtherSaveId = new("22222222-2222-4222-8222-222222222222");

    /// <summary>R-04 第 1 条：成员标识唯一。</summary>
    [Fact]
    public void 成员标识重复时报InvalidStructure()
    {
        var dto = Baseline();
        dto.Family.Members.Add(DeepClone(dto.Family.Members[0]));

        AssertInvalidStructure(dto, dto.Id);
    }

    /// <summary>R-04 第 2 条：配偶关系 MUST 双向一致——指向自身即违反。</summary>
    [Fact]
    public void 配偶指向自身时报InvalidStructure()
    {
        var dto = Baseline();
        var member = dto.Family.Members[0];
        member.SpouseId = member.Id;

        AssertInvalidStructure(dto, dto.Id);
    }

    /// <summary>R-04 第 3 条：<c>FormerSpouseIds</c> 每项都指向本家族成员。</summary>
    [Fact]
    public void 前任配偶不是本家族成员时报InvalidStructure()
    {
        var dto = Baseline();
        dto.Family.Members[0].FormerSpouseIds.Add(ForeignId);

        AssertInvalidStructure(dto, dto.Id);
    }

    /// <summary>R-04 第 4 条：<c>HeadId</c> 为 <c>null</c> 或指向在册成员。</summary>
    [Fact]
    public void 家主不是本家族成员时报InvalidStructure()
    {
        var dto = Baseline();
        dto.Family.HeadId = ForeignId;

        AssertInvalidStructure(dto, dto.Id);
    }

    /// <summary>R-04 第 5 条：每个非空计时都 MUST 对应其状态位为真。</summary>
    [Fact]
    public void 计时非空而状态位为假时报InvalidStructure()
    {
        var dto = Baseline();
        dto.Family.Members[0].Timers.SentenceRemainingMonths = 6;

        AssertInvalidStructure(dto, dto.Id);
    }

    /// <summary>R-04 第 6 条：<c>EntryTrack</c> 非空 ⇒「待阙」位为真。</summary>
    [Fact]
    public void 入仕途径非空而待阙位为假时报InvalidStructure()
    {
        var dto = Baseline();
        dto.Family.Members[0].EntryTrack = AppointmentTrack.ThirdClass;

        AssertInvalidStructure(dto, dto.Id);
    }

    /// <summary>R-04 第 7 条前半：阶段为零 ⇔ 计时为零（**校验器承重**）。</summary>
    [Fact]
    public void 饥馑阶段为零而计时非零时报InvalidStructure()
    {
        var dto = Baseline();
        dto.Economy.Famine = new FamineDto { Stage = FamineStage.None, ElapsedMonths = 3 };

        AssertInvalidStructure(dto, dto.Id);
    }

    /// <summary>R-04 第 7 条后半：阶段非零 ⇒ 计时 ≥ 1（**校验器承重**）。</summary>
    [Fact]
    public void 饥馑阶段非零而计时为零时报InvalidStructure()
    {
        var dto = Baseline();
        dto.Economy.Famine = new FamineDto { Stage = FamineStage.Famine, ElapsedMonths = 0 };

        AssertInvalidStructure(dto, dto.Id);
    }

    /// <summary>R-04 第 8 条：头记录 <c>SaveId</c> MUST 等于主体 <c>Id</c>（**校验器承重**）。</summary>
    [Fact]
    public void 头标识与主体标识不一致时报InvalidStructure()
    {
        var dto = Baseline();

        AssertInvalidStructure(dto, OtherSaveId);
    }

    /// <summary>R-04 第 9 条：资金类条目金额 MUST 非零。</summary>
    [Fact]
    public void 资金类条目金额为零时报InvalidStructure()
    {
        var dto = Baseline();
        dto.Economy.Ledger.Add(new LedgerEntryDto
        {
            Year = 1,
            Month = 1,
            PersonId = null,
            Category = LedgerCategory.LivingCost,
            AmountWen = 0m,
        });

        AssertInvalidStructure(dto, dto.Id);
    }

    /// <summary>干净基线：把 <c>PlainState</c> 投影成 DTO，再逐条篡改。</summary>
    private static SaveStateDto Baseline() => SaveStateMapper.ToDto(SaveFixtures.PlainState());

    /// <summary>借序列化做深拷贝（成员 DTO 有 22 个字段，手抄易错）。</summary>
    private static PersonDto DeepClone(PersonDto dto) => MessagePackSerializer.Deserialize<PersonDto>(
        MessagePackSerializer.Serialize(dto, SaveFileFormat.BinaryOptions),
        SaveFileFormat.BinaryOptions);

    /// <summary>写出「信封合法、主体违反不变量」的二进制档，断言读档以 InvalidStructure 失败。</summary>
    private static void AssertInvalidStructure(SaveStateDto dto, Guid headerSaveId)
    {
        using var sandbox = new SaveTestSandbox();
        SaveCatalog.EnsureDirectory(sandbox.Root);
        var service = sandbox.CreateService();
        var slot = new SaveSlot("校检");

        var payload = MessagePackSerializer.Serialize(dto, SaveFileFormat.BinaryOptions);
        SaveTestSandbox.WriteBytes(service.FilePathOf(slot), ComposeBinaryFile(headerSaveId, payload));

        var exception = Assert.Throws<SaveFileException>(() => service.Load(slot));
        Assert.Equal(SaveFileErrorKind.InvalidStructure, exception.Kind);
    }

    /// <summary>按契约十 §2 的二进制形状拼出「三元素头 + 主体字节」。</summary>
    private static byte[] ComposeBinaryFile(Guid saveId, byte[] payload)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(3);
        writer.Write(SaveFileFormat.Version);
        writer.Write(payload.Length);
        writer.Write(saveId.ToString("D", CultureInfo.InvariantCulture));
        writer.Flush();

        return [.. buffer.WrittenSpan, .. payload];
    }
}
