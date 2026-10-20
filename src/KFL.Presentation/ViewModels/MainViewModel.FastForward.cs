using KFL.Rules.Exam;
using KFL.Presentation.Mvvm;

namespace KFL.Presentation.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    public bool IsFastForwarding
    {
        get => _isFastForwarding;
        private set
        {
            if (Set(ref _isFastForwarding, value))
            {
                Raise(nameof(AdvanceLabel));
                // 规格书 §3.3：快进期间行贿入口置灰（其余面板仍可用）。
                Raise(nameof(CanBribe));
            }
        }
    }

    /// <summary>快进期间行贿入口是否可用（规格书 §3.3）。</summary>
    public bool CanBribe => !IsFastForwarding;

    public string AdvanceLabel => IsFastForwarding ? $"快进中…余 {_fastForwardMonthsLeft} 月" : "下月";

    /// <summary>推进一个月之后的钩子：自动治疗（需求 10）与生育申请标记清理。</summary>
    private void AfterAdvance()
    {
        if (_session is null)
        {
            return;
        }

        if (AutoTreat)
        {
            _session.AutoTreatAll();
        }

        // 「申请生育」只在次月分娩，跨月后标记失效。
        _childbirthPending.Clear();
    }

    // ───────────────────────── 快进 ─────────────────────────

    /// <summary>
    /// 快进到下一次**家里确实有人可报名**的科举窗口（规格书 §3，需求 6 的修正）。
    /// </summary>
    /// <remarks>
    /// 旧实现只看「当前月是否处于报名窗口」，窗口内若全族无人合格也会停住，玩家再点一次便原地不动，
    /// 与「快进到下一次可报名的月份」的预期不符。本实现逐月推进，直到
    /// <see cref="ExamRegistration.HasRegistrable"/> 为真。途中若出现决策类事件（提亲应答 / 生育 /
    /// 疾病治疗等），仍按规格书 §3「决策类事件自动暂停」停住，交给玩家处理；只有家族绝嗣才中止。
    /// 安全上限 20 年：足以覆盖新生儿长到报考年龄（14 岁）并跨过若干科年，避免无人可报时死循环。
    /// </remarks>
    private void FastForwardToExam()
    {
        if (_session is null || !_session.CanAdvance)
        {
            return;
        }

        const int maxMonths = 240;
        var paused = false;
        var reached = false;
        var ended = false;

        for (var step = 0; step < maxMonths && _session.CanAdvance; step++)
        {
            if (ExamRegistration.HasRegistrable(_session.State.Family, _session.State.CurrentDate))
            {
                reached = true;
                break;
            }

            var pause = _session.AdvanceMonth();
            AfterAdvance();

            if (pause)
            {
                if (_session.State.Ending.IsEnded)
                {
                    ended = true;
                }
                else
                {
                    paused = true;
                }

                break;
            }
        }

        // 上限的最后一步可能刚好推进到可报名月份：补一次判点，避免把「第 240 个月可报名」漏报。
        if (!paused && !ended && !reached && _session.CanAdvance
            && ExamRegistration.HasRegistrable(_session.State.Family, _session.State.CurrentDate))
        {
            reached = true;
        }

        if (ended)
        {
            _session.Note("快进停止：家族绝嗣。");
        }
        else if (reached)
        {
            var date = _session.State.CurrentDate;
            _session.Note(FormattableString.Invariant(
                $"快进到科举：停在 {date.Year} 年 {date.Month} 月报名窗口，家中有成员可报名。"));
        }
        else if (paused)
        {
            _session.Note("快进停止：出现待决策事件，处理后可再次点击继续。");
        }
        else if (!_session.CanAdvance)
        {
            _session.Note("快进停止：局面已结束。");
        }
        else
        {
            _session.Note("快进到科举：推进 20 年仍无可报名月份（族人年龄 / 身份不满足报考条件）。");
        }

        RefreshLog();
        RefreshAll();
    }

    private void FastForward(string monthsText)
    {
        if (_session is null || !_session.CanAdvance)
        {
            return;
        }

        _fastForwardMonthsLeft = int.Parse(monthsText, System.Globalization.CultureInfo.InvariantCulture);
        IsFastForwarding = true;
        _fastForwardTimer.Start();
    }

    private void FastForwardTick()
    {
        if (_session is null || _fastForwardMonthsLeft <= 0)
        {
            StopFastForward();
            return;
        }

        var pause = _session.AdvanceMonth();
        AfterAdvance();
        _fastForwardMonthsLeft--;
        Raise(nameof(AdvanceLabel));

        if (pause)
        {
            _session.Note(_session.State.Ending.IsEnded ? "快进停止：家族绝嗣。" : "快进停止：出现待决策事件。");
            StopFastForward();
        }

        RefreshLog();
        RefreshAll();
    }

    private void StopFastForward()
    {
        _fastForwardTimer.Stop();
        _fastForwardMonthsLeft = 0;
        IsFastForwarding = false;
        RefreshLog();
        RefreshAll();
    }
}
