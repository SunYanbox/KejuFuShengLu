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
/// 第 ⑩ 步「一致性校验」的验收测试（004-save-system；契约十 §6 第 ⑧ 步、001 R-04；
/// 006-bribery-punishment 追加第 11、12 条；007-family-lifecycle 追加第 13~15 条）。
/// </summary>
/// <remarks>
/// <para>
/// 这些条款此前**零直接覆盖**：把 <c>SaveService.Load</c> 里 <c>ValidateIdentity</c> / <c>Validate</c>
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

    /// <summary>R-04 第 11 条：三个行贿计数 MUST <c>&gt;= 0</c>（006-bribery-punishment）。</summary>
    /// <remarks>
    /// Core 侧的 <c>RestoreBribeCounts</c> 也会拒绝负值，故本条在脚本层就已被拦下；
    /// 用例把「无论走哪条路径，失败形态恒为 InvalidStructure」钉死。
    /// </remarks>
    [Fact]
    public void 行贿计数为负时报InvalidStructure()
    {
        var dto = Baseline();
        dto.Family.Members[0].DianShiBribeCount = -1;

        AssertInvalidStructure(dto, dto.Id);
    }

    /// <summary>R-04 第 12 条：日志每一行的 <c>PersonId</c> MUST 存在于 <c>Family.Members</c>（含归档者）。</summary>
    /// <remarks>
    /// 核心承重的一条：<c>PunishmentJournal.Restore</c> 只做追加、不校验成员归属，
    /// 故「日志指向一个不存在的成员」是校验器**唯一**的守门人——把它摘掉这条用例必然失败。
    /// </remarks>
    [Fact]
    public void 日志指向家族外成员时报InvalidStructure()
    {
        var dto = Baseline();
        dto.Family.PunishmentJournal.Releases =
        [
            new ReleaseFactDto
            {
                Date = new GameDateDto { Year = 1, Month = 1 },
                Subject = ForeignId,
                NewRankLevel = 7,
            },
        ];

        AssertInvalidStructure(dto, dto.Id);
    }

    /// <summary>R-04 第 4 条（007 不变量 13）：有在册男性时家主 MUST NOT 为空。</summary>
    /// <remarks>
    /// 校验器承重的一条：`SetHead(null)` 在 Core 侧合法（只在「再无在册男性」时才该这么写），
    /// 故「明明还有男性却家主空缺」只有本校验能发现。
    /// </remarks>
    [Fact]
    public void 有在册男性而家主为空时报InvalidStructure()
    {
        var dto = Baseline();
        dto.Family.HeadId = null;

        AssertInvalidStructure(dto, dto.Id);
    }

    /// <summary>R-04 第 5 条（007 不变量 1）：患病位与疾病程度同生同灭。</summary>
    [Fact]
    public void 患病位为真而疾病程度为空时报InvalidStructure()
    {
        var dto = Baseline();
        dto.Family.Members[0].Status |= StatusFlag.Ill;

        AssertInvalidStructure(dto, dto.Id);
    }

    /// <summary>R-04 第 5 条（007 不变量 1）：疾病程度非空时患病位 MUST 为真。</summary>
    [Fact]
    public void 疾病程度非空而患病位为假时报InvalidStructure()
    {
        var dto = Baseline();
        dto.Family.Members[0].Timers.IllnessSeverityBasisPoints = 500;

        AssertInvalidStructure(dto, dto.Id);
    }

    /// <summary>R-04 第 5 条（007 不变量 2）：疗程只在患病时非空。</summary>
    [Fact]
    public void 疗程非空而患病位为假时报InvalidStructure()
    {
        var dto = Baseline();
        dto.Family.Members[0].Timers.TreatmentRemainingMonths = 2;

        AssertInvalidStructure(dto, dto.Id);
    }

    /// <summary>R-04 第 13 条：待应答提议的我方成员 MUST 是家族成员。</summary>
    [Fact]
    public void 提议指向家族外成员时报InvalidStructure()
    {
        var dto = Baseline();
        dto.Family.Lifecycle.Proposal = ProposalFor(ForeignId);

        AssertInvalidStructureInPlace(dto);
    }

    /// <summary>R-04 第 13 条：待应答提议的我方成员 MUST NOT 是已身故者。</summary>
    [Fact]
    public void 提议指向已身故成员时报InvalidStructure()
    {
        var dto = Baseline();
        dto.Family.Members[0].Status = StatusFlag.Deceased;
        dto.Family.Lifecycle.Proposal = ProposalFor(dto.Family.Members[0].Id);

        AssertInvalidStructure(dto, dto.Id);
    }

    /// <summary>R-04 第 14 条：待分娩记录 MUST ⊆ 已申请夫妇。</summary>
    [Fact]
    public void 待产记录不在已申请夫妇里时报InvalidStructure()
    {
        var dto = Baseline();
        dto.Family.Lifecycle.PendingBirths =
        [
            new PendingBirthDto
            {
                HusbandId = dto.Family.Members[0].Id,
                WifeId = ForeignId,
                AppliedYear = 1,
                AppliedMonth = 1,
            },
        ];

        AssertInvalidStructureInPlace(dto);
    }

    /// <summary>R-04 第 14 条：已申请夫妇的配偶关系 MUST 双向一致。</summary>
    [Fact]
    public void 已申请夫妇的配偶关系非双向时报InvalidStructure()
    {
        var dto = Baseline();
        var wife = DeepClone(dto.Family.Members[0]);
        wife.Id = Guid.Parse("33333333-3333-4333-8333-333333333333");
        wife.Gender = Gender.Female;
        dto.Family.Members.Add(wife);

        var couple = new CoupleKeyDto { HusbandId = dto.Family.Members[0].Id, WifeId = wife.Id };
        dto.Family.Lifecycle.AppliedBirthCouples = [couple];

        AssertInvalidStructureInPlace(dto);
    }

    /// <summary>R-04 第 15 条：当年购人计数 MUST 与账本里的 <c>PersonPurchase</c> 条数一致。</summary>
    [Fact]
    public void 当年购人计数与账本不符时报InvalidStructure()
    {
        var dto = Baseline();
        dto.Family.Lifecycle.PurchaseYear = 1;
        dto.Family.Lifecycle.PurchaseCount = 1;

        AssertInvalidStructure(dto, dto.Id);
    }

    /// <summary>R-04 第 15 条：购人年份 MUST NOT 超过当年。</summary>
    [Fact]
    public void 购人年份超过当年时报InvalidStructure()
    {
        var dto = Baseline();
        dto.Family.Lifecycle.PurchaseYear = 2;

        AssertInvalidStructure(dto, dto.Id);
    }

    /// <summary>构造一条指向 <paramref name="memberId"/> 的合法待应答提议（19 个字段全给值）。</summary>
    /// <param name="memberId">我方成员标识。</param>
    /// <returns>提议 DTO。</returns>
    private static MarriageProposalDto ProposalFor(Guid memberId) => new()
    {
        Kind = MarriageProposalKind.Matchmaker,
        OurMemberId = memberId,
        CandidateName = "王氏",
        CandidateGender = Gender.Female,
        CandidateAge = 18,
        TalentAgriculture = 50,
        TalentCommerce = 50,
        TalentScholar = 50,
        TalentCraft = 50,
        CandidateStudy = 50,
        CandidateHealth = 60,
        CandidateLifespan = 60,
        CandidateDegree = DegreeLevel.BaiShen,
        Class = MarriageClass.Commoner,
        BetrothalGift = 10_000m,
        Dowry = 20_000m,
        ProposedYear = 1,
        ProposedMonth = 1,
        CandidatePlacement = null,
    };

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

    /// <summary>
    /// 直接对（未经读档自愈的）领域对象跑校验器，断言以 InvalidStructure 拒绝。
    /// </summary>
    /// <remarks>
    /// 用于**第 13 / 14 条**这两类不变量：读档流水线在 ⑦′ 步会先做一次自愈
    /// （<c>SaveStateRepair</c>：清掉过期的提议 / 生育簿记），故它们**经文件往返是读得进来的**
    /// （站在玩家角度是「旧档还能打开」）。想验证的若只是「校验器仍如实拒绝」，就必须绕开自愈这一步。
    /// </remarks>
    private static void AssertInvalidStructureInPlace(SaveStateDto dto)
    {
        var state = SaveStateMapper.ToDomain(dto);

        var exception = Assert.Throws<SaveFileException>(() => SaveStateValidator.Validate(state));
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
