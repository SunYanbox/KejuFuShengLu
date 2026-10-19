using System.Text;
using KFL.Core.Entities;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save;
using KFL.Rules.Config;
using Xunit;

namespace KFL.Tests.Infrastructure;

public sealed partial class SaveServiceTests
{
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
}
