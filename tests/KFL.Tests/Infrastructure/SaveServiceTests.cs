using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save;
using KFL.Infrastructure.Services;
using KFL.Rules.Config;
using KFL.Rules.Save;
using KFL.Tests.Fixtures;
using KFL.Tests.Rules;
using Xunit;

namespace KFL.Tests.Infrastructure;

/// <summary>
/// <c>ISaveService</c> 的验收测试（004-save-system；US1 = 存/读，US3 = 档名/目录/格式）。
/// </summary>
/// <remarks>
/// <para>覆盖 tasks.md T029（US1 往返与等价）、T038（真文件节奏）、T043（US3 命名/改名/备份）。</para>
/// <para>
/// 全部用例走 <see cref="SaveTestSandbox"/> 的临时根，不碰仓库目录、不依赖 <c>KFL.App</c>。
/// </para>
/// </remarks>
public sealed partial class SaveServiceTests
{
    /// <summary>US1：两种格式都把厚状态逐字段读回。</summary>
    /// <param name="format">格式。</param>
    [Theory]
    [InlineData(SaveFormat.Binary)]
    [InlineData(SaveFormat.Json)]
    public void 两种格式都能把厚状态原样读回(SaveFormat format)
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var state = SaveFixtures.RichState();
        var slot = new SaveSlot("宋家三十年", format);

        service.Save(state, slot, overwrite: false);
        var loaded = service.Load(slot);

        // T064：夹具里必须真的含有前史纪年（0 与负数），否则本用例对「0 / 负年份」的覆盖是空的。
        Assert.Contains(state.Family.Members, person => person.BirthDate.Year < 0);
        Assert.Contains(
            state.Family.Members.SelectMany(person => person.DegreeHistory),
            record => record.ChangedAt.Year == 0);

        SaveStateComparer.AssertEquivalent(state, loaded);
    }

    /// <summary>US1：落盘后恰好一个主档，扩展名随格式。</summary>
    /// <param name="format">格式。</param>
    [Theory]
    [InlineData(SaveFormat.Binary)]
    [InlineData(SaveFormat.Json)]
    public void 落盘后主档路径与扩展名符合契约(SaveFormat format)
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("宋家三十年", format);

        service.Save(SaveFixtures.PlainState(), slot, overwrite: false);

        var expectedExtension = format == SaveFormat.Json ? ".json" : ".dat";
        Assert.Equal(sandbox.PathOf("宋家三十年", format), service.FilePathOf(slot));
        Assert.EndsWith(expectedExtension, service.FilePathOf(slot), StringComparison.Ordinal);
        Assert.Single(sandbox.RootFiles());
        Assert.Empty(sandbox.BackupFiles());
    }

    /// <summary>US1 / FR-021：中文与金额标度位级保留。</summary>
    /// <param name="format">格式。</param>
    [Theory]
    [InlineData(SaveFormat.Binary)]
    [InlineData(SaveFormat.Json)]
    public void 金额标度与中文字符在往返后位级一致(SaveFormat format)
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();

        var plain = SaveFixtures.PlainState();
        var state = new GameState(
            plain.Id,
            plain.CurrentDate,
            plain.Difficulty,
            plain.Origin,
            plain.Family,
            new FamilyEconomy(
                LivingStandard.Normal,
                new GrainPriceIndex(1.2500m),
                new Treasury(Money.FromWen(1.2300m))));

        var slot = new SaveSlot("宋家三十年", format);
        service.Save(state, slot, overwrite: false);
        var loaded = service.Load(slot);

        SaveStateComparer.AssertBits(1.2300m, loaded.Economy.Treasury.Cash.Wen);
        SaveStateComparer.AssertBits(1.2500m, loaded.Economy.GrainPriceIndex.Value);
        Assert.Equal("宋家三十年", loaded.Family.Name);
        Assert.Equal("宋家主", loaded.Family.Members.First().Name);
    }

    /// <summary>US1 / AS4：读回后再存，字节完全一致（不漂移）。</summary>
    /// <param name="format">格式。</param>
    [Theory]
    [InlineData(SaveFormat.Binary)]
    [InlineData(SaveFormat.Json)]
    public void 读回后再存两档字节完全一致(SaveFormat format)
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("甲档", format);

        service.Save(SaveFixtures.RichState(), slot, overwrite: false);
        var first = SaveTestSandbox.ReadBytes(service.FilePathOf(slot));

        // 同一个标识 MUST NOT 并存于两个档名（契约九 §5），故这里覆盖同一个档再比字节。
        service.Save(service.Load(slot), slot, overwrite: true);

        Assert.Equal(first, SaveTestSandbox.ReadBytes(service.FilePathOf(slot)));
    }

    /// <summary>场景 8 / FR-018 / R-23：同一状态两次存档**字节完全相同**（盘上不含任何写入时间戳）。</summary>
    /// <param name="format">格式。</param>
    [Theory]
    [InlineData(SaveFormat.Binary)]
    [InlineData(SaveFormat.Json)]
    public void 同一状态两次存档字节完全相同(SaveFormat format)
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("甲档", format);

        service.Save(SaveFixtures.RichState(), slot, overwrite: false);
        var first = SaveTestSandbox.ReadBytes(service.FilePathOf(slot));

        // 稍等一下再存第二次：若实现里藏了墙钟，字节就不可能相同。
        Thread.Sleep(20);

        service.Save(SaveFixtures.RichState(), slot, overwrite: true);

        Assert.Equal(first, SaveTestSandbox.ReadBytes(service.FilePathOf(slot)));
    }

    /// <summary>US1 / AS6：归档成员（身故、出嫁）一并复原。</summary>
    /// <param name="format">格式。</param>
    [Theory]
    [InlineData(SaveFormat.Binary)]
    [InlineData(SaveFormat.Json)]
    public void 归档成员随存档一并复原(SaveFormat format)
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var state = SaveFixtures.RichState();
        var slot = new SaveSlot("宋家三十年", format);

        service.Save(state, slot, overwrite: false);
        var loaded = service.Load(slot);

        Assert.Equal(state.Family.Members.Count, loaded.Family.Members.Count);
        Assert.Equal(state.Family.ArchivedMembers.Count, loaded.Family.ArchivedMembers.Count);
        Assert.Equal(
            state.Family.ArchivedMembers.Select(person => person.Id).OrderBy(id => id.Value),
            loaded.Family.ArchivedMembers.Select(person => person.Id).OrderBy(id => id.Value));
    }

    /// <summary>US1：落籍旁系/联姻对齐的记账集合在往返后仍然生效（行为断言，因集合是 Core 内部态）。</summary>
    /// <param name="format">格式。</param>
    [Theory]
    [InlineData(SaveFormat.Binary)]
    [InlineData(SaveFormat.Json)]
    public void 落籍旁系记账在往返后仍然生效(SaveFormat format)
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var state = SaveFixtures.RichState();
        var collateralId = state.Family.Members.Single(person => person.Name == "陈旁系").Id;
        var slot = new SaveSlot("宋家三十年", format);

        service.Save(state, slot, overwrite: false);
        var loaded = service.Load(slot);

        // 已落籍者 MUST NOT 再次指定辈分——这条拒绝只有在记账集合被复原后才会发生。
        Assert.Throws<InvalidOperationException>(
            () => loaded.Family.SetOutsiderGeneration(collateralId, loaded.Family.BoughtCollateralGeneration));
    }

    /// <summary>US2 / AS5：开启自动存档不改变模拟结果（与「不存档」逐字段一致）。</summary>
    [Fact]
    public void 开启自动存档不改变模拟结果()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        const int Seed = 20251011;
        const int Turns = 6;

        var withSave = SaveFixtures.PlainState();
        var baseline = SaveFixtures.PlainState();

        var recording = new TurnRunner(new SeededRandomService(Seed), service, new SaveSlot("自动档"), new FixedNameGenerator());
        var plain = new TurnRunner(new SeededRandomService(Seed), saveService: null, new SaveSlot("自动档"), new FixedNameGenerator());

        for (var turn = 0; turn < Turns; turn++)
        {
            recording.Advance(withSave);
            plain.Advance(baseline);
        }

        SaveStateComparer.AssertEquivalent(baseline, withSave);
    }

    /// <summary>US1 / FR-014：读档失败不修改原文件、不返回半成品。</summary>
    [Fact]
    public void 读档失败不修改原文件也不返回半成品()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("坏档");

        service.Save(SaveFixtures.PlainState(), slot, overwrite: false);
        var path = service.FilePathOf(slot);
        SaveTestSandbox.TruncateTo(path, SaveTestSandbox.ReadBytes(path).Length / 2);
        var truncated = SaveTestSandbox.ReadBytes(path);

        Assert.Throws<SaveFileException>(() => service.Load(slot));
        Assert.Equal(truncated, SaveTestSandbox.ReadBytes(path));
    }

    // ───────────────────────────── T050：SC-002 长跑 ─────────────────────────────

    /// <summary>
    /// T050 / SC-002：连续落盘 **24 个月**后读回，与「从未落盘」的基线逐字段（含账本条目数与逐条内容、
    /// 三池、全部成员档案）差异 = 0；两路再各推进 **12 个月**，差异仍 = 0。
    /// </summary>
    /// <param name="format">存档格式（两路都必须成立）。</param>
    [Theory]
    [InlineData(SaveFormat.Binary)]
    [InlineData(SaveFormat.Json)]
    public void 连续落盘二十四个月后读回并与不落盘基线一致(SaveFormat format)
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("长跑", format);
        const int Seed = 20251011;
        const int PersistedMonths = 24;
        const int ContinuedMonths = 12;

        // 两路喂同一个种子：存档 MUST NOT 消耗随机流（FR-017），故「落盘 + 每回合覆盖写」与
        // 「从不落盘」必须逐字段一致。
        var persisted = LongRunState();
        var baseline = LongRunState();

        var recording = new TurnRunner(new SeededRandomService(Seed), service, slot, new FixedNameGenerator());
        var plain = new TurnRunner(new SeededRandomService(Seed), saveService: null, slot, new FixedNameGenerator());

        for (var month = 0; month < PersistedMonths; month++)
        {
            var persistedOutcome = recording.Advance(persisted);
            var plainOutcome = plain.Advance(baseline);

            Assert.True(persistedOutcome.Saved);
            Assert.False(plainOutcome.Saved);
            // 场景前提（E-23）：SC-002 的 24 + 12 月长跑要求家族不在中途绝嗣——绝嗣后 008 的结束闸门
            // （契约二十四「条款 19」）会拒绝继续推进。
            Assert.False(persisted.Ending.IsEnded, $"长跑夹具在第 {month + 1} 月绝嗣，24 + 12 月的场景不再成立。");
        }

        Assert.True(persisted.Economy.Ledger.Entries.Count > 0);

        // ① 读回与基线逐字段差异 = 0（账本条目数与逐条内容、三池、全部成员档案都在比对范围内）。
        var loaded = service.Load(slot);
        SaveStateComparer.AssertEquivalent(baseline, loaded);

        // ② 读回后继续推进 12 个月：与「从未落盘」的基线仍逐字段差异 = 0。
        for (var month = 0; month < ContinuedMonths; month++)
        {
            recording.Advance(loaded);
            plain.Advance(baseline);
        }

        SaveStateComparer.AssertEquivalent(baseline, loaded);
    }

    /// <summary>
    /// 008 修订（E-23）：长跑所需的「能活过 24 个月」的薄状态。
    /// </summary>
    /// <remarks>
    /// `SaveFixtures.PlainState` 的家主生于 1 年 1 月（开局当月），第 13 月因**襁褓死亡**而绝嗣；
    /// 008 的结束闸门（契约二十四「条款 19」）此后拒绝推进，24 个月长跑无从进行（诊断：第 13 月
    /// 绝嗣，死因 <c>Infancy</c>）。本夹具沿用同一薄经济与同一存档标识，只把家主抬到成年
    /// （生于前史纪年）并把体质置为设计均值（与本特性其它用例的既有做法一致）——
    /// **断言本身未放宽**，24 + 12 月的等价性照旧被逐字段检验。
    /// </remarks>
    /// <returns>一个新的状态实例。</returns>
    private static GameState LongRunState()
    {
        var family = new Family("宋家三十年");
        var head = family.AddFoundingMember(
            FamilyFixtures.NewPerson(1, "宋家主", Gender.Male, birthYear: -19, birthMonth: 1, generation: 0));
        head.Health = (int)GameConfig.Attributes.HealthMean;
        family.SetHead(head.Id);

        var economy = new FamilyEconomy(LivingStandard.Normal, new GrainPriceIndex(1.000m));

        return new GameState(
            SaveFixtures.PlainStateId, new GameDate(1, 1), Difficulty.Normal, Origin.Farmer, family, economy);
    }
}
