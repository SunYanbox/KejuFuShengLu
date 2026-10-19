using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Rules.Config;
using KFL.Rules.FamilyLifecycle;

namespace KFL.Presentation;

public sealed partial class GameSession
{
    private static string FamineStageText(FamineStage stage) => stage switch
    {
        FamineStage.Famine => "饥荒",
        FamineStage.Relief => "赈济",
        FamineStage.Severe => "大饥",
        _ => "平常",
    };

    private static string FamineTimelineText(FamineState state)
        => $"{FamineTimelineRemaining(state)} 月";

    private static int FamineTimelineRemaining(FamineState state)
        => KFL.Rules.Config.FamineTimeline.RemainingMonths(state);

    private static string BracketText(AgeBracket bracket) => bracket switch
    {
        AgeBracket.Child => "幼年",
        AgeBracket.Youth => "少年",
        AgeBracket.Adult => "成年",
        AgeBracket.Elder => "老年",
        _ => bracket.ToString(),
    };

    private static string DeathText(DeathCause cause) => cause switch
    {
        DeathCause.OldAge => "寿终",
        DeathCause.Illness => "病故",
        DeathCause.Famine => "饥荒",
        DeathCause.Infancy => "夭折",
        DeathCause.Childbirth => "产厄",
        _ => cause.ToString(),
    };

    private static string TrackText(KFL.Core.Enums.AppointmentTrack track) => track switch
    {
        KFL.Core.Enums.AppointmentTrack.FirstClass => "一甲",
        KFL.Core.Enums.AppointmentTrack.SecondClass => "二甲",
        KFL.Core.Enums.AppointmentTrack.ThirdClass => "三甲",
        KFL.Core.Enums.AppointmentTrack.SpecialTribute => "特奏名",
        _ => track.ToString(),
    };

    private static string CollectiveText(CollectiveOutcome outcome) => outcome switch
    {
        CollectiveOutcome.Executed => "处死",
        CollectiveOutcome.DegreeDemoted => "降一级功名",
        CollectiveOutcome.FamilyWideBan => "受全族禁令",
        _ => outcome.ToString(),
    };
}
