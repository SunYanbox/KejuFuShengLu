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

    /// <summary>快进到下一次科举报名窗口（需求 6）；最多一年，避免死循环。</summary>
    private void FastForwardToExam()
    {
        if (_session is null || !_session.CanAdvance)
        {
            return;
        }

        for (var step = 0; step < 12 && _session.CanAdvance; step++)
        {
            if (ExamRegistration.RegisterLevelOf(_session.State.CurrentDate) is not null)
            {
                break;
            }

            var pause = _session.AdvanceMonth();
            AfterAdvance();
            if (pause)
            {
                _session.Note(_session.State.Ending.IsEnded ? "快进停止：家族绝嗣。" : "快进停止：出现待决策事件。");
                break;
            }
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
