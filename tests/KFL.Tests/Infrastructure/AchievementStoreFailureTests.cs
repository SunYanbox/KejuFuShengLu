using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Achievements;
using KFL.Infrastructure.Save;
using KFL.Infrastructure.Services;
using KFL.Rules.Save;
using KFL.Tests.Rules;
using Xunit;

namespace KFL.Tests.Infrastructure;

/// <summary>
/// T035：成就面的**写失败**不阻断回合——月结算照常完成、存档与备份不受影响、<c>Flush</c> 只回报失败而
/// 不抛异常、待落盘队列保留原年月，恢复后一次性补写。
/// </summary>
/// <remarks>
/// 契约二十三「条款 22」：③存档失败属于**用户数据**（抛且不回滚内存），⑤全局落盘失败属于**元进度**
/// （不抛、队列留待下次），两者语义刻意不同。
/// </remarks>
public class AchievementStoreFailureTests
{
    [Fact]
    public void 落点不可写时只回报失败而不抛()
    {
        using var sandbox = new SaveTestSandbox();
        var store = new AchievementStore(sandbox.CreateService());
        SaveTestSandbox.BlockFileWithDirectory(store.FilePath);

        try
        {
            // 读向：目标位置是目录 ⇒ 存在性判定认为「档案不存在」 ⇒ 空档、无诊断、不隔离。
            var archive = store.Read();
            Assert.Empty(archive.Achievements);
            Assert.False(archive.Diagnostics.WasQuarantined);

            // 写向：MUST NOT 抛；失败经返回值表达。
            var result = store.Flush(
                [new PendingUnlock(AchievementId.FoundationLaid, Difficulty.Normal, new KFL.Core.ValueObjects.GameDate(80, 1), Guid.Parse("11111111-2222-4333-8444-555555555555"))],
                []);

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.FailureReason);
            Assert.Null(result.QuarantinePath);
        }
        finally
        {
            SaveTestSandbox.UnblockFile(store.FilePath);
        }

        // 恢复后立刻可用：失败没有破坏档案面。
        Assert.True(store.Flush(
            [new PendingUnlock(AchievementId.FoundationLaid, Difficulty.Normal, new KFL.Core.ValueObjects.GameDate(80, 1), Guid.Parse("11111111-2222-4333-8444-555555555555"))],
            []).IsSuccess);
        Assert.Single(store.Read().Achievements);
    }

    [Fact]
    public void 写失败不阻断月结算与存档备份()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("自动档");
        var store = new AchievementStore(service);
        var state = SaveFixtures.PlainState();

        SaveTestSandbox.BlockFileWithDirectory(store.FilePath);

        try
        {
            var settledMonth = state.CurrentDate;
            var runner = new TurnRunner(
                new SeededRandomService(20261018), service, slot, new FixedNameGenerator(), store);

            var outcome = runner.Advance(state);

            // ①月结算照常完成：结算报告覆盖的是**被结算**的那个月，推进后当前月恰好 +1。
            Assert.Equal(settledMonth, outcome.Settlement.Month);
            Assert.Equal(new GameDate(settledMonth.Year, settledMonth.Month + 1), state.CurrentDate);

            // ②存档与备份不受成就面失败影响。
            Assert.True(outcome.Saved);
            Assert.Equal(slot, service.FindBySaveId(state.Id));
            Assert.NotNull(service.Load(slot));

            // ③失败经 TurnOutcome 表达，且队列**保留**（留待下次补写）。
            Assert.NotNull(outcome.AchievementFlush);
            Assert.False(outcome.AchievementFlush!.IsSuccess);
            Assert.NotEmpty(state.Achievements.PendingUnlocks);
        }
        finally
        {
            SaveTestSandbox.UnblockFile(store.FilePath);
        }
    }

    [Fact]
    public void 恢复后按原解锁年月一次性补写()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("自动档");
        var store = new AchievementStore(service);
        var state = SaveFixtures.PlainState();
        var runner = new TurnRunner(
            new SeededRandomService(20261018), service, slot, new FixedNameGenerator(), store);

        // 第一回合：判定成功（解锁 开基立业）但落盘失败。
        SaveTestSandbox.BlockFileWithDirectory(store.FilePath);
        var failed = runner.Advance(state);
        Assert.False(failed.AchievementFlush!.IsSuccess);

        // 解除阻塞：此刻队列仍保留着**原**解锁年月与来源。
        SaveTestSandbox.UnblockFile(store.FilePath);
        var expected = state.Achievements.PendingUnlocks
            .Single(item => item.Achievement == AchievementId.FoundationLaid);

        runner.Advance(state);

        // 补写成功后队列清空，且档案里记的是原年月与原来源（不是补写当时的月份）。
        Assert.Empty(state.Achievements.PendingUnlocks);

        var record = store.Read().Achievements
            .Single(item => item.Achievement == AchievementId.FoundationLaid);

        Assert.Equal(expected.UnlockedAt, record.CompletedAt);
        Assert.Equal(expected.SourceSaveId, record.SourceSaveId);
    }

    [Fact]
    public void 恢复后补写不会把同一成就写成两条()
    {
        using var sandbox = new SaveTestSandbox();
        var service = sandbox.CreateService();
        var slot = new SaveSlot("自动档");
        var store = new AchievementStore(service);
        var state = SaveFixtures.PlainState();
        var runner = new TurnRunner(
            new SeededRandomService(20261018), service, slot, new FixedNameGenerator(), store);

        SaveTestSandbox.BlockFileWithDirectory(store.FilePath);
        runner.Advance(state);
        SaveTestSandbox.UnblockFile(store.FilePath);

        // 连续推进三回合：第二回合补写队列，第三回合队列已空（同一成就只落一条）。
        runner.Advance(state);
        runner.Advance(state);

        var archive = store.Read();

        // 同一 (成就, 难度) 只落一条：补写既有队列不会造成重复项。
        Assert.Equal(
            archive.Achievements.Count,
            archive.Achievements.Select(item => (item.Achievement, item.Difficulty)).Distinct().Count());
        Assert.Contains(archive.Achievements, item => item.Achievement == AchievementId.FoundationLaid);
    }
}
