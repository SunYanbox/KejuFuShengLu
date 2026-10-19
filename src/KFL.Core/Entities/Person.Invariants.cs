using KFL.Core.Enums;

namespace KFL.Core.Entities;

public sealed partial class Person
{
    private static void EnsureStatusFlagBacksTimers(
        StatusFlag status, StatusFlag flag, int? timer, string paramName)
    {
        if (!status.HasFlag(flag) && timer is not null)
        {
            throw new ArgumentException(
                $"清除「{flag}」状态位前 MUST 先把对应计时字段置空（规格书 §7.5）。", paramName);
        }
    }

    private static void EnsureTimerBackedByStatus(
        StatusFlag status, StatusFlag flag, int? timer, string paramName)
    {
        if (timer is not null && !status.HasFlag(flag))
        {
            throw new ArgumentException(
                $"计时字段仅在「{flag}」状态位为真时可非空（data-model §2.1 不变量 4）。", paramName);
        }
    }

    private static void EnsureStatusFlagBacksEntryTrack(
        StatusFlag status, AppointmentTrack? entryTrack, string paramName)
    {
        if (!status.HasFlag(StatusFlag.AwaitingPost) && entryTrack is not null)
        {
            throw new ArgumentException(
                "清除「AwaitingPost」状态位前 MUST 先把入仕途径置空（data-model §2.1 不变量 5）。", paramName);
        }
    }
}
