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

    /// <summary>US1 / AS5：开启自动存档不改变模拟结果（与「不存档」逐字段一致）。</summary>
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

    /// <summary>Q7 / 契约九 §4 条款 1.1：同名判定**大小写不敏感**、且**跨格式**。</summary>
    [Fact]
    public void 大小写不同视为同名且跨格式算存在()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();

        service.Save(SaveFixtures.PlainState(), new SaveSlot("SongJia"), overwrite: false);

        Assert.True(service.Exists(new SaveSlot("songjia")));
        Assert.True(service.Exists(new SaveSlot("SONGJIA", SaveFormat.Json)));
        Assert.Throws<SaveNameConflictException>(
            () => service.Save(SaveFixtures.RichState(), new SaveSlot("songjia"), overwrite: false));
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
