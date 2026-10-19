using KFL.Core.Entities;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save;
using KFL.Infrastructure.Services;
using KFL.Rules.Config;
using KFL.Rules.Save;
using KFL.Tests.Rules;
using Xunit;

namespace KFL.Tests.Infrastructure;

public sealed partial class SaveServiceTests
{
    // ───────────────────────────── T038：真文件节奏 ─────────────────────────────

    /// <summary>T038 / SC-003：真文件跑 12 回合 ⇒ 主档一份、备份目录**恰好一份**。</summary>
    [Fact]
    public void 推进十二回合后备份目录只留一份()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("自动档");
        var state = SaveFixtures.PlainState();
        var runner = new TurnRunner(new SeededRandomService(20251011), service, slot, new FixedNameGenerator());

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

    /// <summary>
    /// 玩家反馈 13（规格书 §14 的 2026-10-19 裁决 ②）：改名后**另存为** MUST 成功，
    /// 且新档持有**新的存档标识**——旧实现把同一 UUID 写到另一个档名下，被唯一性闸门拒绝。
    /// </summary>
    [Fact]
    public void 改名后另存为铸造新标识且两档各自可读回()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var state = SaveFixtures.RichState();
        var slot = new SaveSlot("旧名");

        service.Save(state, slot, overwrite: false);
        var renamed = service.Rename(slot, "新名");

        // 改名后按旧实现（同标识写另一个档名）会抛 SaveIdentityConflictException。
        var forked = service.SaveAsFork(state, new SaveSlot("分支名"));

        Assert.NotEqual(state.Id, forked.Id);
        Assert.Equal(new SaveSlot("分支名", SaveFormat.Binary), service.FindBySaveId(forked.Id));

        // 原档保持不变、仍指向旧标识。
        Assert.Equal(state.Id, service.Load(renamed).Id);
        Assert.Equal(new SaveSlot("新名", SaveFormat.Binary), service.FindBySaveId(state.Id));

        // 两个档各读各的：分支档是可独立推进的深拷贝，只换标识（故不比对 Id）。
        SaveStateComparer.AssertEquivalent(state, service.Load(renamed));
        SaveStateComparer.AssertEquivalent(state, service.Load(new SaveSlot("分支名")), compareId: false);
    }

    /// <summary>玩家反馈 13：另存为到已占用的档名 ⇒ 拒绝（与 <c>Save</c> 同口径）。</summary>
    [Fact]
    public void 另存为到已占用档名被拒()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var state = SaveFixtures.PlainState();

        service.Save(state, new SaveSlot("甲档"), overwrite: false);

        Assert.Throws<SaveNameConflictException>(
            () => service.SaveAsFork(state, new SaveSlot("甲档")));
    }

    /// <summary>
    /// 玩家反馈 14 / 15：备份落地为**压缩包**（<c>存档名.dat.zip</c>），
    /// 且 <c>ListBackups</c> / <c>LoadBackup</c> 能把它读回。
    /// </summary>
    [Theory]
    [InlineData(SaveFormat.Binary)]
    [InlineData(SaveFormat.Json)]
    public void 备份是压缩包且可被读回(SaveFormat format)
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("甲档", format);
        var state = SaveFixtures.RichState();

        service.Save(state, slot, overwrite: false);
        service.Backup(slot);

        // 扩展名是 .zip（也就是「备份是压缩包」这一句的可执行形式）。
        var backup = Assert.Single(sandbox.BackupFiles());
        Assert.EndsWith(".zip", backup, StringComparison.Ordinal);

        var listed = Assert.Single(service.ListBackups());
        Assert.Equal(slot, listed);
        SaveStateComparer.AssertEquivalent(state, service.LoadBackup(listed));
    }

    /// <summary>玩家反馈 14：压缩包不是单条目 ⇒ 判为损坏（MUST NOT 猜内容）。</summary>
    [Fact]
    public void 备份压缩包条目数异常时报损坏()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("甲档");

        service.Save(SaveFixtures.PlainState(), slot, overwrite: false);
        service.Backup(slot);

        // 重写成一个「零条目」的 zip：结构合法，但不是我们写出来的东西。
        using (var stream = new FileStream(Assert.Single(sandbox.BackupFiles()), FileMode.Create, FileAccess.Write))
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create))
        {
            _ = archive; // arch-guard:allow 测试要故意造一个空压缩包
        }

        var error = Assert.Throws<SaveFileException>(() => service.LoadBackup(slot));
        Assert.Equal(SaveFileErrorKind.Malformed, error.Kind);
    }
}
