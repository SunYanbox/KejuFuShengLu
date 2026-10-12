using System.Text;
using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save;
using KFL.Infrastructure.Services;
using KFL.Rules.Save;
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
public sealed class SaveServiceTests
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

        var recording = new TurnRunner(new SeededRandomService(Seed), service, new SaveSlot("自动档"));
        var plain = new TurnRunner(new SeededRandomService(Seed), saveService: null, new SaveSlot("自动档"));

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
        var persisted = SaveFixtures.PlainState();
        var baseline = SaveFixtures.PlainState();
        var recording = new TurnRunner(new SeededRandomService(Seed), service, slot);
        var plain = new TurnRunner(new SeededRandomService(Seed), saveService: null, slot);

        for (var month = 0; month < PersistedMonths; month++)
        {
            var persistedOutcome = recording.Advance(persisted);
            var plainOutcome = plain.Advance(baseline);

            Assert.True(persistedOutcome.Saved);
            Assert.False(plainOutcome.Saved);
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

    // ───────────────────────────── T038：真文件节奏 ─────────────────────────────

    /// <summary>T038 / SC-003：真文件跑 12 回合 ⇒ 主档一份、备份目录**恰好一份**。</summary>
    [Fact]
    public void 推进十二回合后备份目录只留一份()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("自动档");
        var state = SaveFixtures.PlainState();
        var runner = new TurnRunner(new SeededRandomService(20251011), service, slot);

        for (var turn = 0; turn < 12; turn++)
        {
            var outcome = runner.Advance(state);

            Assert.True(outcome.Saved);
            Assert.Equal(turn + 1, outcome.CompletedTurns);
            Assert.Equal(turn is 2 or 5 or 8 or 11, outcome.BackedUp);
        }

        Assert.Single(sandbox.RootFiles());
        Assert.Single(sandbox.BackupFiles());
        Assert.Equal(slot, service.FindBySaveId(state.Id));

        // T058(b)：盘上内容 MUST 与内存里的最后一回合状态逐字段一致——只比内存会漏掉
        // 「某一回合的自动存档写丢/写歪」这类缺口（SC-003）。
        SaveStateComparer.AssertEquivalent(state, service.Load(slot));

        // 备份也 MUST 是**可完整读回**的同一份（契约九 §7 条款 4）：把备份复制回主档位置再读。
        sandbox.CopyBackupOverPrimary("自动档", SaveFormat.Binary);
        SaveStateComparer.AssertEquivalent(state, service.Load(slot));
    }

    // ───────────────────────────── T043：US3 档名 / 目录 / 格式 ─────────────────────────────

    /// <summary>FR-007 / Q3：Windows 的九个非法字符全部被拒，且一个字节都不落。</summary>
    [Fact]
    public void 九类非法档名字符全部被拒且不落一个字节()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var state = SaveFixtures.PlainState();

        foreach (var illegal in "\\/:*?\"<>|")
        {
            Assert.Throws<ArgumentException>(
                () => service.Save(state, new SaveSlot($"宋{illegal}家"), overwrite: false));
        }

        Assert.Empty(sandbox.RootFiles());
    }

    /// <summary>FR-007：空、全空白、超长、保留设备名、首尾空白、结尾点号一律拒绝且不落字节。</summary>
    /// <param name="badName">非法档名。</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" 宋家")]
    [InlineData("宋家 ")]
    [InlineData("宋家.")]
    [InlineData("CON")]
    [InlineData("con.dat")]
    [InlineData("COM1")]
    [InlineData("LPT9")]
    public void 非法档名一律拒绝且不落一个字节(string badName)
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();

        Assert.Throws<ArgumentException>(
            () => service.Save(SaveFixtures.PlainState(), new SaveSlot(badName), overwrite: false));
        Assert.Empty(sandbox.RootFiles());
    }

    /// <summary>FR-007：长度上限 255，超出即拒。</summary>
    [Fact]
    public void 档名超过二百五十五字符被拒()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();

        Assert.Throws<ArgumentException>(
            () => service.Save(SaveFixtures.PlainState(), new SaveSlot(new string('甲', 256)), overwrite: false));
        Assert.Empty(sandbox.RootFiles());
    }

    /// <summary>FR-007 / Q3：短横与中文是**合法**档名字符（原规格书的「-」是「*」的笔误）。</summary>
    [Fact]
    public void 短横与中文档名允许()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("宋-家三十年");

        service.Save(SaveFixtures.PlainState(), slot, overwrite: false);

        Assert.True(service.Exists(slot));
        Assert.Single(sandbox.RootFiles());

        // R-25：档名与 Family.Name 是两件事，档名不进存档内容。
        Assert.Equal("宋家三十年", service.Load(slot).Family.Name);
    }

    /// <summary>
    /// T063 / 契约九 §4 条款 1：档名长度在**校验层**的上界是 255，但**写得下多少**由「根路径 + 档名 + 扩展名」
    /// 的实际长度决定（OS 的 <c>MAX_PATH</c>）；原子写 MUST NOT 在这笔预算之外再按档名长度加开销。
    /// </summary>
    /// <remarks>
    /// 实测（本机 NTFS；.NET 10；测试根约 113 字符）：255 字符档名在原子写路径上**写不下**——
    /// 在最后一步原子改名处抛 <c>IOException</c>（路径 372 &gt; <c>MAX_PATH</c>），这是**根路径**决定的，
    /// 不是档名自身的上界；也正因如此，不能把「实测出来的可写长度」写进 <c>SaveName</c>（它会随部署路径变化）。
    /// 收口办法是让临时名字长度恒定：改造前 <c>.&lt;整档名&gt;.&lt;32 位随机&gt;.tmp</c>（额外 +37 字符），
    /// 改造后 <c>.&lt;档名前 16 字符&gt;.&lt;32 位随机&gt;.tmp</c>（+54 字符，与档名长度无关）。
    /// 本用例按本根的预算取名，长度足以让「整档名进临时名」的旧实现先失败。
    /// </remarks>
    [Fact]
    public void 原子写的临时名不随档名变长()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var extension = SaveFileFormat.ExtensionOf(SaveFormat.Binary);

        // MAX_PATH = 259 可用字符；减去根、分隔符与扩展名，剩下的都给档名。
        var budget = MaxPathBudget - sandbox.Root.Length - extension.Length - 1;
        var nameLength = Math.Min(SaveName.MaxLength, budget);
        Assert.True(nameLength > 0, $"测试前提失败：临时根太长（{sandbox.Root.Length} 字符），放不下档名。");

        var slot = new SaveSlot(new string('甲', nameLength));

        service.Save(SaveFixtures.PlainState(), slot, overwrite: false);

        Assert.True(service.Exists(slot));
        Assert.NotNull(service.Load(slot));

        // 写完 MUST 只剩主档一个文件：临时文件（名字里有随机后缀）绝不留到提交点之后。
        Assert.Single(sandbox.RootFiles());
    }

    /// <summary>可用路径预算（<c>MAX_PATH</c> 含结尾空字符，故按 259 计）。</summary>
    private const int MaxPathBudget = 259;

    /// <summary>Q7 / 契约九 §4 条款 1.1：同名判定**大小写不敏感**、且**跨格式**。</summary>
    /// <remarks>T062(a)：大小写 MUST NOT 被静默规整——盘上的档名就是玩家输入的那一份。</remarks>
    [Fact]
    public void 大小写不同视为同名且跨格式算存在()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();

        service.Save(SaveFixtures.PlainState(), new SaveSlot("SongJia"), overwrite: false);

        // 盘上（而不是只在内存里）保留原大小写。
        Assert.Equal(sandbox.PathOf("SongJia", SaveFormat.Binary), Assert.Single(sandbox.RootFiles()));

        Assert.True(service.Exists(new SaveSlot("songjia")));
        Assert.True(service.Exists(new SaveSlot("SONGJIA", SaveFormat.Json)));
        Assert.Throws<SaveNameConflictException>(
            () => service.Save(SaveFixtures.RichState(), new SaveSlot("songjia"), overwrite: false));
    }

    /// <summary>T062(b) / 契约九 §4 条款 1：只有**首尾**空白被拒，档名**内部**的空格放行。</summary>
    [Fact]
    public void 档名内部空格放行()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("宋 家 三十年");

        service.Save(SaveFixtures.PlainState(), slot, overwrite: false);

        Assert.True(service.Exists(slot));
        Assert.Equal(sandbox.PathOf("宋 家 三十年", SaveFormat.Binary), Assert.Single(sandbox.RootFiles()));
        Assert.NotNull(service.Load(slot));
    }

    /// <summary>
    /// T062(c) / 契约九 §5 条款 1：<c>Exists</c> 只看**主目录**——备份目录里留着同名备份
    /// 不算「档存在」（备份不参与唯一性判定，§5 条款 5）。
    /// </summary>
    [Fact]
    public void 只剩备份时主档视为不存在()
    {
        using var sandbox = new SaveTestSandbox();
        var writer = new AtomicFileWriter();
        var service = sandbox.CreateService(writer);
        var slot = new SaveSlot("甲档");

        service.Save(SaveFixtures.PlainState(), slot, overwrite: false);
        service.Backup(slot);
        Assert.Equal(sandbox.BackupPathOf("甲档", SaveFormat.Binary), Assert.Single(sandbox.BackupFiles()));

        // 主档被手工删掉：现场只剩 backups/ 里的那一份。
        writer.Delete(service.FilePathOf(slot));

        Assert.Empty(sandbox.RootFiles());
        Assert.False(service.Exists(slot));
        Assert.False(service.Exists(new SaveSlot("甲档", SaveFormat.Json)));
    }

    /// <summary>契约九 §5：同名其它格式的档存在时，<c>overwrite: false</c> 也一律拒绝。</summary>
    [Fact]
    public void 同名未确认覆盖时拒绝()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();

        service.Save(SaveFixtures.PlainState(), new SaveSlot("甲档", SaveFormat.Binary), overwrite: false);

        Assert.Throws<SaveNameConflictException>(
            () => service.Save(SaveFixtures.PlainState(), new SaveSlot("甲档", SaveFormat.Json), overwrite: false));
        Assert.Single(sandbox.RootFiles());
    }

    /// <summary>契约九 §5：确认覆盖并切换格式 ⇒ 新格式档就位、旧格式档被移除。</summary>
    [Fact]
    public void 覆盖切换格式时旧格式档被移除()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var state = SaveFixtures.PlainState();

        service.Save(state, new SaveSlot("甲档", SaveFormat.Binary), overwrite: false);
        service.Save(state, new SaveSlot("甲档", SaveFormat.Json), overwrite: true);

        var remaining = Assert.Single(sandbox.RootFiles());
        Assert.Equal(sandbox.PathOf("甲档", SaveFormat.Json), remaining);
    }

    /// <summary>
    /// 契约九 §5 条款 5、§7 条款 2：确认切换格式 ⇒ **旧格式备份也在同一时点被移除**
    /// （备份不参与唯一性，但 MUST NOT 留下「同名同标识的两个候选」）。
    /// </summary>
    [Fact]
    public void 切换格式后旧格式备份被移除()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var state = SaveFixtures.PlainState();
        var binarySlot = new SaveSlot("甲档", SaveFormat.Binary);

        service.Save(state, binarySlot, overwrite: false);
        service.Backup(binarySlot);
        Assert.Equal(sandbox.BackupPathOf("甲档", SaveFormat.Binary), Assert.Single(sandbox.BackupFiles()));

        service.Save(state, new SaveSlot("甲档", SaveFormat.Json), overwrite: true);

        Assert.Equal(sandbox.PathOf("甲档", SaveFormat.Json), Assert.Single(sandbox.RootFiles()));
        Assert.Empty(sandbox.BackupFiles());
    }

    /// <summary>
    /// 契约九 §5 条款 5（末句）：切换格式**写失败** ⇒ 旧格式的主档与备份 MUST 原样保留
    /// （「只在新档写成功后移除」）。
    /// </summary>
    [Fact]
    public void 切换格式写失败时旧格式主档与备份保留()
    {
        using var sandbox = new SaveTestSandbox();
        var writer = new AtomicFileWriter();
        var service = sandbox.CreateService(writer);
        var state = SaveFixtures.PlainState();
        var binarySlot = new SaveSlot("甲档", SaveFormat.Binary);

        service.Save(state, binarySlot, overwrite: false);
        service.Backup(binarySlot);
        var backupBefore = SaveTestSandbox.ReadBytes(sandbox.BackupPathOf("甲档", SaveFormat.Binary));

        writer.OnChunkWritten = _ => throw new IOException("注入：切换格式写到一半失败");

        Assert.Throws<SaveAccessException>(
            () => service.Save(state, new SaveSlot("甲档", SaveFormat.Json), overwrite: true));

        Assert.Equal(sandbox.PathOf("甲档", SaveFormat.Binary), Assert.Single(sandbox.RootFiles()));
        Assert.Equal(backupBefore, SaveTestSandbox.ReadBytes(sandbox.BackupPathOf("甲档", SaveFormat.Binary)));
        SaveStateComparer.AssertEquivalent(state, service.Load(binarySlot));
    }

    /// <summary>契约九 §5 / 001 R-03：同一标识换档名 ⇒ 写**零**字节。</summary>
    [Fact]
    public void 同标识换档名被拒且不落一个字节()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var state = SaveFixtures.PlainState();

        service.Save(state, new SaveSlot("甲档"), overwrite: false);

        Assert.Throws<SaveIdentityConflictException>(
            () => service.Save(state, new SaveSlot("乙档"), overwrite: true));
        Assert.True(service.Exists(new SaveSlot("甲档")));
        Assert.False(service.Exists(new SaveSlot("乙档")));
        Assert.Single(sandbox.RootFiles());
    }

    /// <summary>契约九 §4 条款 3：未配置存档根 ⇒ 可执行文件旁的 <c>saves/</c>。</summary>
    [Fact]
    public void 未配置存档根时默认落在可执行文件旁()
    {
        var service = new SaveService(new SaveOptions());

        Assert.EndsWith(SaveFileFormat.DefaultDirectoryName, service.RootDirectory, StringComparison.Ordinal);
    }

    /// <summary>契约九 §4:目录建不出来（根路径被文件占住）⇒ <c>SaveAccessException</c>，绝不静默吞掉。</summary>
    [Fact]
    public void 存档根建不出来时报SaveAccessException()
    {
        using var sandbox = new SaveTestSandbox();
        sandbox.BlockRootWithFile();
        var service = sandbox.CreateService();

        Assert.Throws<SaveAccessException>(
            () => service.Save(SaveFixtures.PlainState(), new SaveSlot("甲档"), overwrite: false));
    }

    /// <summary>契约九 §5：<c>FindBySaveId</c> **只读头**，主体坏掉也照常命中；找不到即 <c>null</c>。</summary>
    [Fact]
    public void 按标识定位只读头不受主体损坏影响()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var state = SaveFixtures.PlainState();
        var slot = new SaveSlot("甲档");
        var unknown = Guid.Parse("11111111-1111-1111-1111-111111111111");

        service.Save(state, slot, overwrite: false);

        Assert.Equal(slot, service.FindBySaveId(state.Id));
        Assert.Null(service.FindBySaveId(unknown));

        // 把主体截短：头还在 ⇒ 探针照常命中（证明它没去解析主体）。
        SaveTestSandbox.TruncateTo(service.FilePathOf(slot), 60);

        Assert.Equal(slot, service.FindBySaveId(state.Id));
    }

    /// <summary>
    /// T062(d) / 契约九 §5 条款 3：JSON 档的 <c>FindBySaveId</c> 同样**只读头**——
    /// 正文被截断也照常命中（若它顺手解析正文，这里会落成 <c>Truncated</c> 而返回 <c>null</c>）。
    /// </summary>
    [Fact]
    public void JSON档按标识定位只读头不解析正文()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var state = SaveFixtures.PlainState();
        var slot = new SaveSlot("甲档", SaveFormat.Json);

        service.Save(state, slot, overwrite: false);

        // 截到 State 成员名之后的一点点正文里：头三个成员与「State」这个键名都完整，正文只剩零头。
        var text = Encoding.UTF8.GetString(SaveTestSandbox.ReadBytes(service.FilePathOf(slot)));
        var bodyStart = text.IndexOf("\"State\":", StringComparison.Ordinal);
        Assert.True(bodyStart > 0, "测试前提失败：JSON 头里没有 State 成员。");
        var valueStart = text.IndexOf(':', bodyStart) + 1;
        SaveTestSandbox.TruncateTo(service.FilePathOf(slot), Encoding.UTF8.GetByteCount(text[..valueStart]) + 8);

        Assert.Equal(slot, service.FindBySaveId(state.Id));
    }

    /// <summary>FR-012 / 契约九 §6：改名是**纯移动** ⇒ 字节不变、<c>SaveId</c> 不变、<c>Family.Name</c> 不变。</summary>
    [Fact]
    public void 改名不改一个字节也不改家族名()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var state = SaveFixtures.RichState();
        var slot = new SaveSlot("旧名");

        service.Save(state, slot, overwrite: false);
        var familyName = state.Family.Name;
        var before = SaveTestSandbox.ReadBytes(service.FilePathOf(slot));

        var renamed = service.Rename(slot, "新名");

        Assert.Equal(new SaveSlot("新名", SaveFormat.Binary), renamed);
        Assert.Equal(before, SaveTestSandbox.ReadBytes(service.FilePathOf(renamed)));
        Assert.Equal(new SaveSlot("新名", SaveFormat.Binary), service.FindBySaveId(state.Id));
        Assert.Equal(state.Id, service.Load(renamed).Id);
        Assert.Equal(familyName, service.Load(renamed).Family.Name);
        Assert.False(service.Exists(slot));
        Assert.Single(sandbox.RootFiles());
    }

    /// <summary>契约九 §6 条款 5 / Q5：改名把备份一并带走，且不留下旧名备份。</summary>
    [Fact]
    public void 改名同时带走备份且不留下旧名()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("旧名");

        service.Save(SaveFixtures.PlainState(), slot, overwrite: false);
        service.Backup(slot);

        var renamed = service.Rename(slot, "新名");

        Assert.Equal(sandbox.BackupPathOf("新名", SaveFormat.Binary), Assert.Single(sandbox.BackupFiles()));
        Assert.Equal(renamed, new SaveSlot("新名", SaveFormat.Binary));
    }

    /// <summary>契约九 §6 条款 2：目标档名已被占用 ⇒ 拒绝改名。</summary>
    [Fact]
    public void 改到已占用档名被拒()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();

        service.Save(SaveFixtures.PlainState(), new SaveSlot("甲档"), overwrite: false);
        service.Save(SaveFixtures.RichState(), new SaveSlot("乙档"), overwrite: false);

        Assert.Throws<SaveNameConflictException>(() => service.Rename(new SaveSlot("甲档"), "乙档"));
        Assert.True(service.Exists(new SaveSlot("甲档")));
        Assert.True(service.Exists(new SaveSlot("乙档")));
    }

    /// <summary>
    /// T060 / 契约九 §6 条款 5：备份改名失败 MUST NOT 损坏主档，且 MUST 以
    /// <see cref="SaveAccessException"/> 家族暴露（MUST NOT 静默吞）。
    /// </summary>
    /// <remarks>
    /// 注入点是**文件系统事实**而非生产接缝：在备份要改到的目标路径上先放一个同名文件，
    /// <c>AtomicFileWriter.Move</c>（不带 overwrite）必然抛 <c>IOException</c>。主档的改名此时**已经完成**，
    /// 故本用例同时钉住「主档不被回滚成半吊子」这一点。
    /// </remarks>
    [Fact]
    public void 备份改名失败时报SaveAccessException且主档不受损()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var state = SaveFixtures.PlainState();
        var slot = new SaveSlot("甲档");

        service.Save(state, slot, overwrite: false);
        service.Backup(slot);
        var backupBefore = SaveTestSandbox.ReadBytes(sandbox.BackupPathOf("甲档", SaveFormat.Binary));

        // 备份改名的目标位置被一个陈旧文件占住。
        SaveTestSandbox.WriteBytes(sandbox.BackupPathOf("乙档", SaveFormat.Binary), [0x01, 0x02]);

        var exception = Assert.Throws<SaveAccessException>(() => service.Rename(slot, "乙档"));

        Assert.Contains("改名", exception.Message, StringComparison.Ordinal);

        // 主档：已按新名就位，MUST 仍是完整可读的那一份。
        Assert.Equal(sandbox.PathOf("乙档", SaveFormat.Binary), Assert.Single(sandbox.RootFiles()));
        SaveStateComparer.AssertEquivalent(state, service.Load(new SaveSlot("乙档", SaveFormat.Binary)));

        // 旧备份：MUST 原地未损（不带 overwrite 的移动失败时不搬走源文件）。
        Assert.Equal(backupBefore, SaveTestSandbox.ReadBytes(sandbox.BackupPathOf("甲档", SaveFormat.Binary)));
    }

    /// <summary>
    /// T061 / FR-008、契约九 §4 条款 1.1：只差大小写的改名目标是**同一个槽位**（不算「已占用」），
    /// 改名后盘上的档名 MUST 反映新的大小写。
    /// </summary>
    /// <remarks>
    /// 实测结论（NTFS + .NET 10）：不带 overwrite 的同目录移动对「只改大小写」的改名**能成功**——
    /// Windows 把它当作同一对象的改名，不会报 already exists。故 <see cref="SaveService.Rename"/> 无需为
    /// 该场景加特殊分支；本用例把这一行为钉死，若未来换成会自冲突的实现（例如先探目标是否已存在再拒绝），
    /// 这里会失败。
    /// </remarks>
    [Fact]
    public void 只差大小写的改名走同一槽位()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var state = SaveFixtures.PlainState();
        var slot = new SaveSlot("甲A档");

        service.Save(state, slot, overwrite: false);

        var renamed = service.Rename(slot, "甲a档");

        Assert.Equal(new SaveSlot("甲a档", SaveFormat.Binary), renamed);
        Assert.Equal(sandbox.PathOf("甲a档", SaveFormat.Binary), Assert.Single(sandbox.RootFiles()));
        SaveStateComparer.AssertEquivalent(state, service.Load(renamed));
    }

    /// <summary>FR-016 / 契约九 §7：备份是**完整可读的档**，把它复制回主档位置即可回退。</summary>
    [Fact]
    public void 备份复制回主档后仍能读档()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("甲档");
        var state = SaveFixtures.RichState();

        service.Save(state, slot, overwrite: false);
        service.Backup(slot);

        // 把主档弄坏，再用备份覆盖回来——这就是「回退」的全部操作。
        SaveTestSandbox.WriteBytes(service.FilePathOf(slot), [0x00, 0x01, 0x02]);
        sandbox.CopyBackupOverPrimary("甲档", SaveFormat.Binary);

        SaveStateComparer.AssertEquivalent(state, service.Load(slot));
    }

    /// <summary>契约九 §7：覆盖式，每档**只留一份**，绝不按时间戳追加。</summary>
    [Fact]
    public void 备份每档只留一份且覆盖式()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("甲档");

        service.Save(SaveFixtures.PlainState(), slot, overwrite: false);
        service.Backup(slot);
        service.Backup(slot);

        Assert.Equal(sandbox.BackupPathOf("甲档", SaveFormat.Binary), Assert.Single(sandbox.BackupFiles()));
    }

    /// <summary>
    /// T052 / SC-003 / 契约九 §7 第 7 条：<c>Backup</c> 入口也必须清掉 backups/ 下**同标识**的其它备份。
    /// </summary>
    /// <remarks>
    /// 造出「同标识、异档名」的陈旧备份（旧名遗留 / 改名中途失败都会留下它），再走一次
    /// <c>Backup</c>：它 MUST 被清掉，backups/ 恒只剩当前档一份。
    /// </remarks>
    [Fact]
    public void 备份入口清掉同标识的其它备份()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("甲档");

        var state = SaveFixtures.PlainState();
        service.Save(state, slot, overwrite: false);
        service.Backup(slot);

        // 陈旧备份：与当前档同标识、不同档名（直接落盘，绕过 Save 的唯一性闸门）。
        var stale = sandbox.BackupPathOf("甲档旧名", SaveFormat.Binary);
        SaveTestSandbox.WriteBytes(stale, SaveTestSandbox.ReadBytes(sandbox.BackupPathOf("甲档", SaveFormat.Binary)));
        Assert.Equal(2, sandbox.BackupFiles().Count);

        service.Backup(slot);

        Assert.Equal(sandbox.BackupPathOf("甲档", SaveFormat.Binary), Assert.Single(sandbox.BackupFiles()));
    }

    /// <summary>契约九 §2：备份一个不存在的档 ⇒ <c>Missing</c>。</summary>
    [Fact]
    public void 备份不存在的档报Missing()
    {
        using var sandbox = new SaveTestSandbox();

        var error = Assert.Throws<SaveFileException>(
            () => sandbox.CreateService().Backup(new SaveSlot("无此档")));

        Assert.Equal(SaveFileErrorKind.Missing, error.Kind);
    }
}
