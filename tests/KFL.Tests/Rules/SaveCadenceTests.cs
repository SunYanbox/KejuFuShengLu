using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save;
using KFL.Infrastructure.Services;
using KFL.Rules.Config;
using KFL.Rules.Save;
using KFL.Tests.Infrastructure;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// 存档节奏的验收测试（004-save-system；契约十一 §5 / SC-003）。
/// </summary>
/// <remarks>
/// 全部用 <see cref="RecordingSaveService"/> 这个**内存替身**观察「什么时候调了存档/备份」，
/// 因节奏断言的对象是调用序列，不是盘上字节（真文件的对应断言在
/// <c>SaveServiceTests</c> 的 T038 用例里）。
/// </remarks>
public sealed class SaveCadenceTests
{
    private const int Seed = 20251011;

    /// <summary>SC-003：推进十二回合 ⇒ 当前档写十二次、备份四次（第 3/6/9/12 回合）。</summary>
    [Fact]
    public void 推进十二回合时当前档写十二次备份写四次()
    {
        var state = SaveFixtures.PlainState();
        var recorder = new RecordingSaveService();
        var runner = new TurnRunner(new SeededRandomService(Seed), recorder, new SaveSlot("自动档"));

        for (var turn = 0; turn < 12; turn++)
        {
            runner.Advance(state);
        }

        Assert.Equal(12, recorder.SaveCount);
        Assert.Equal(4, recorder.BackupCount);
        Assert.Equal(
            ["Save", "Save", "Save", "Backup", "Save", "Save", "Save", "Backup",
             "Save", "Save", "Save", "Backup", "Save", "Save", "Save", "Backup"],
            recorder.Calls.Select(call => call.Kind));
    }

    /// <summary>SC-003：回合数自纪元逐月递增——初始 0，第 N 次成功结算后为 N。</summary>
    [Fact]
    public void 回合数自纪元逐月递增()
    {
        var state = SaveFixtures.PlainState();
        var recorder = new RecordingSaveService();
        var runner = new TurnRunner(new SeededRandomService(Seed), recorder, new SaveSlot("自动档"));

        Assert.Equal(0, SaveCadencePolicy.CompletedTurnsAt(state.CurrentDate));

        for (var turn = 1; turn <= 12; turn++)
        {
            var outcome = runner.Advance(state);
            Assert.Equal(turn, outcome.CompletedTurns);
        }

        Assert.Equal(12, SaveCadencePolicy.CompletedTurnsAt(state.CurrentDate));
    }

    /// <summary>契约十一 §2 条款：结算抛错 ⇒ 本回合既不存档也不备份（结构性，不靠 catch）。</summary>
    [Fact]
    public void 结算抛错时不存档也不备份()
    {
        var state = BrokenSavingsState();
        var recorder = new RecordingSaveService();
        var runner = new TurnRunner(new SeededRandomService(Seed), recorder, new SaveSlot("自动档"));

        Assert.Throws<ArgumentOutOfRangeException>(() => runner.Advance(state));

        Assert.Equal(0, recorder.SaveCount);
        Assert.Equal(0, recorder.BackupCount);
    }

    /// <summary>Q6：存档抛错 ⇒ 内存状态保留（已推进）、不备份、失败向上抛。</summary>
    [Fact]
    public void 存档抛错时不备份且内存状态保留()
    {
        var state = SaveFixtures.PlainState();
        var recorder = new RecordingSaveService();
        var runner = new TurnRunner(new SeededRandomService(Seed), recorder, new SaveSlot("自动档"));

        runner.Advance(state);
        runner.Advance(state);

        var beforeFailure = state.CurrentDate;
        recorder.SaveFailures.Enqueue(new SaveAccessException("磁盘满了"));

        Assert.Throws<SaveAccessException>(() => runner.Advance(state));

        Assert.Equal(3, recorder.SaveCount);
        Assert.Equal(0, recorder.BackupCount);
        Assert.True(state.CurrentDate > beforeFailure, "结算已经改了内存状态，MUST NOT 回滚。");
    }

    /// <summary>Q6：失败之后继续推进，下一次存档写下的是**当前完整状态**（含未落盘的月），不跳不补。</summary>
    [Fact]
    public void 存档失败后继续推进下一次写下完整状态()
    {
        var state = SaveFixtures.PlainState();
        var recorder = new RecordingSaveService();
        var runner = new TurnRunner(new SeededRandomService(Seed), recorder, new SaveSlot("自动档"));

        runner.Advance(state);
        runner.Advance(state);

        recorder.SaveFailures.Enqueue(new SaveFileException(SaveFileErrorKind.Malformed, "注入失败"));
        Assert.Throws<SaveFileException>(() => runner.Advance(state));

        var outcome = runner.Advance(state);

        Assert.Equal(4, outcome.CompletedTurns);
        Assert.Equal(4, recorder.SaveCount);
        Assert.Equal(state.CurrentDate, recorder.LastSavedState!.CurrentDate);
        Assert.Equal(new GameDate(1, 5), recorder.LastSavedState!.CurrentDate);
    }

    /// <summary>US2 AS4：不注入存档服务 ⇒ 零次存档，且模拟结果与开启自动存档时逐字段一致。</summary>
    [Fact]
    public void 不注入存档服务时零次存档且结果一致()
    {
        var plain = SaveFixtures.PlainState();
        var recorded = new RecordingSaveService();
        var withSave = SaveFixtures.PlainState();

        var offRunner = new TurnRunner(new SeededRandomService(Seed), saveService: null, new SaveSlot("自动档"));
        var onRunner = new TurnRunner(new SeededRandomService(Seed), recorded, new SaveSlot("自动档"));

        for (var turn = 0; turn < 6; turn++)
        {
            offRunner.Advance(plain);
            onRunner.Advance(withSave);
        }

        // 关掉的那一台根本没有服务可记录；开着的那一台按节奏存了 6 次、备份 2 次。
        Assert.Equal(6, recorded.SaveCount);
        Assert.Equal(2, recorded.BackupCount);
        SaveStateComparer.AssertEquivalent(withSave, plain);
    }

    /// <summary>FR-012：退出存档与自动存档走**同一个** Save 入口、同一个当前档。</summary>
    [Fact]
    public void 退出存档走同一入口同一档()
    {
        var state = SaveFixtures.PlainState();
        var recorder = new RecordingSaveService();
        var runner = new TurnRunner(new SeededRandomService(Seed), recorder, new SaveSlot("手动档"));

        runner.Advance(state);
        runner.SaveOnExit(state);

        Assert.Equal(2, recorder.SaveCount);
        Assert.All(recorder.Calls, call => Assert.Equal("手动档", call.Slot.Name));
        Assert.All(recorder.Calls, call => Assert.True(call.Overwrite));
    }

    /// <summary>造一个「结算必抛」的状态：12 月的储蓄利率为负——<c>SavingsSettlement.Accrue</c> 会拒绝。</summary>
    /// <returns>状态。</returns>
    private static GameState BrokenSavingsState()
    {
        var plain = SaveFixtures.PlainState();
        var treasury = new Treasury(Money.FromGuan(10m));
        treasury.SavingsRateYear = 12;
        treasury.SavingsRate = -0.01m;

        return new GameState(
            plain.Id,
            new GameDate(12, 12),
            Difficulty.Normal,
            Origin.Farmer,
            plain.Family,
            new FamilyEconomy(LivingStandard.Normal, new GrainPriceIndex(1.0m), treasury));
    }
}
