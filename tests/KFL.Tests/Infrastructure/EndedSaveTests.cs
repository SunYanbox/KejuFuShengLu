using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Achievements;
using KFL.Infrastructure.Save;
using KFL.Infrastructure.Services;
using KFL.Rules.Achievements;
using KFL.Rules.Save;
using KFL.Tests.Rules;
using Xunit;

namespace KFL.Tests.Infrastructure;

/// <summary>
/// T066（并入契约二十二 §5 的 <c>EndingRefusalTests</c> 行）：结束态的三面拒绝 —— 推进被拒、
/// **零状态变更**、存档文件未被触碰；而不推进的存档能力（写盘 / 读回 / 改名 / 备份）全部照旧可用，
/// 读档后结束态逐字段复原，备份回退后该档可继续玩且既有成就保持解锁。
/// </summary>
/// <remarks>
/// SC-010 / <c>spec.md</c> US4 AS1~AS3。拒绝**只**发生在 <c>Advance</c>：<c>Save</c> / <c>Load</c> /
/// <c>Rename</c> / 备份判定 MUST NOT 检查 <c>Ending.IsEnded</c>（契约二十四「条款 19/20」）。
/// </remarks>
public class EndedSaveTests
{
    [Fact]
    public void 推进已结束的档被拒且零状态变更()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("自动档");
        var random = new CountingRandomService(new SeededRandomService(20261018));
        var runner = new TurnRunner(random, service, slot, new FixedNameGenerator(), new AchievementStore(service));

        var state = SaveFixtures.PlainState();
        runner.Advance(state);
        service.Save(state, slot, overwrite: true);

        MakeExtinct(state);

        var primary = sandbox.PathOf(slot.Name, SaveFormat.Binary);
        var modifiedAt = SaveTestSandbox.LastWriteTimeUtc(primary);
        var dateBefore = state.CurrentDate;
        var ledgerBefore = state.Economy.Ledger.Entries.Count;
        var drawsBefore = random.DoublesRequested;
        var unlocksBefore = state.Achievements.Completed.Count;

        // ① 明确异常拒绝（MUST NOT 静默返回「无变化的 TurnOutcome」）。
        var refused = Assert.Throws<InvalidOperationException>(() => runner.Advance(state));

        Assert.Contains("绝嗣", refused.Message, StringComparison.Ordinal);

        // ② 零状态变更：日期没推进、账本没新增、随机源一次没取、成就面没有新解锁或新入队。
        Assert.Equal(dateBefore, state.CurrentDate);
        Assert.Equal(ledgerBefore, state.Economy.Ledger.Entries.Count);
        Assert.Equal(drawsBefore, random.DoublesRequested);
        Assert.Equal(unlocksBefore, state.Achievements.Completed.Count);
        Assert.Empty(state.Achievements.PendingUnlocks);

        // ③ 存档文件未被触碰（连一次写都没有，故 mtime 不变）。
        Assert.Equal(modifiedAt, SaveTestSandbox.LastWriteTimeUtc(primary));
    }

    [Fact]
    public void 结束态仍可写盘读回改名备份()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("自动档");
        var runner = new TurnRunner(
            new SeededRandomService(20261018), service, slot, new FixedNameGenerator(), new AchievementStore(service));

        var state = SaveFixtures.PlainState();
        runner.Advance(state);
        MakeExtinct(state);

        // 写盘：结束态照样能存（拒绝只在 Advance）。
        service.Save(state, slot, overwrite: true);
        Assert.True(service.Exists(slot));

        // 读回：结束态逐字段复原。
        var loaded = service.Load(slot);

        Assert.True(loaded.Ending.IsEnded);
        Assert.Equal(state.Ending.EndedAt, loaded.Ending.EndedAt);
        Assert.NotNull(loaded.Ending.Chronicle);
        Assert.Equal(state.Ending.Chronicle!.SurvivedYears, loaded.Ending.Chronicle!.SurvivedYears);
        Assert.Equal(state.Ending.Chronicle.Generations, loaded.Ending.Chronicle.Generations);
        Assert.Equal(state.Ending.Chronicle.PeakTotalAssets, loaded.Ending.Chronicle.PeakTotalAssets);
        Assert.Equal(state.Ending.Chronicle.HighestDegree, loaded.Ending.Chronicle.HighestDegree);
        Assert.Equal(state.Ending.Chronicle.TotalJinShi, loaded.Ending.Chronicle.TotalJinShi);
        Assert.Equal(state.Ending.Chronicle.AverageLifespan, loaded.Ending.Chronicle.AverageLifespan);
        Assert.Equal(state.Ending.Chronicle.Genealogy, loaded.Ending.Chronicle.Genealogy);

        // 备份：结束态也照样进备份目录。
        service.Backup(slot);
        Assert.Single(sandbox.BackupFiles());

        // 改名：结束态也照样能改名。
        var renamed = service.Rename(slot, "绝嗣之后");

        Assert.Equal("绝嗣之后", renamed.Name);
        Assert.True(service.Exists(renamed));
        Assert.False(service.Exists(slot));
    }

    [Fact]
    public void 读档复原结束态且继续推进仍被拒()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("自动档");
        var runner = new TurnRunner(
            new SeededRandomService(20261018), service, slot, new FixedNameGenerator(), new AchievementStore(service));

        var state = SaveFixtures.PlainState();
        runner.Advance(state);
        MakeExtinct(state);
        service.Save(state, slot, overwrite: true);

        var loaded = service.Load(slot);

        Assert.True(loaded.Ending.IsEnded);
        Assert.Throws<InvalidOperationException>(() => runner.Advance(loaded));

        // 结束态的两入口冻结（契约二十四「条款 23」）。
        var outcome = AchievementAdjudicator.Report(loaded, AchievementTrigger.NewGameCreated);

        Assert.Empty(outcome.NewlyUnlocked);
        Assert.False(outcome.ExtinctionJustTriggered);
    }

    [Fact]
    public void 备份回退后可继续推进且既有成就保持解锁()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("自动档");
        var runner = new TurnRunner(
            new SeededRandomService(20261018), service, slot, new FixedNameGenerator(), new AchievementStore(service));

        var state = SaveFixtures.PlainState();

        // 五回合：备份在「第 3 回合」的节奏点写下（此后未再刷新），主档已推到第 5 回合。
        for (var turn = 0; turn < 5; turn++)
        {
            runner.Advance(state);
        }

        Assert.NotEmpty(sandbox.BackupFiles());
        Assert.True(AchievementQuery.IsCompleted(state, AchievementId.FoundationLaid));

        MakeExtinct(state);
        service.Save(state, slot, overwrite: true);

        // 回退：把备份复制回主档 ⇒ 该档回到绝嗣之前。
        sandbox.CopyBackupOverPrimary(slot.Name, SaveFormat.Binary);
        var restored = service.Load(slot);

        Assert.False(restored.Ending.IsEnded);
        Assert.Null(restored.Ending.Chronicle);

        // 可以继续玩（推进不再被拒），且此前已解锁的成就仍然解锁。
        var outcome = runner.Advance(restored);

        Assert.NotNull(outcome.Settlement);
        Assert.True(AchievementQuery.IsCompleted(restored, AchievementId.FoundationLaid));
        Assert.True(AchievementQuery.CompletedCount(restored) > 0);
    }

    /// <summary>把家族推到绝嗣并把结束态落定（判据与判定的唯一入口是 <see cref="ExtinctionJudge"/>）。</summary>
    /// <param name="state">游戏状态（就地变更）。</param>
    private static void MakeExtinct(GameState state)
    {
        foreach (var member in state.Family.Members)
        {
            member.Status |= StatusFlag.Deceased;
            state.Achievements.AppendLifespan(member.AgeAt(state.CurrentDate));
        }

        state.Family.SetHead(null);

        Assert.True(ExtinctionJudge.TryTrigger(state));
    }

    /// <summary>包一层计数：只统计 <see cref="NextDouble"/> 的调用次数。</summary>
    private sealed class CountingRandomService : IRandomService
    {
        private readonly IRandomService _inner;

        /// <summary>用内层替身构造。</summary>
        /// <param name="inner">取值来源。</param>
        public CountingRandomService(IRandomService inner) => _inner = inner;

        /// <summary>至今被请求的 <see cref="NextDouble"/> 次数。</summary>
        public int DoublesRequested { get; private set; }

        /// <inheritdoc />
        public double NextDouble()
        {
            DoublesRequested++;
            return _inner.NextDouble();
        }

        /// <inheritdoc />
        public int Next(int minInclusive, int maxExclusive) => _inner.Next(minInclusive, maxExclusive);

        /// <inheritdoc />
        public void NextBytes(Span<byte> destination) => _inner.NextBytes(destination);
    }
}
