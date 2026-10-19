using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Achievements;
using KFL.Rules.Config;
using KFL.Rules.Save;

namespace KFL.Presentation;

public sealed partial class GameSession
{
    /// <summary>把一条回合结算翻译成人类可读的日志行。</summary>
    private void DescribeTurn(TurnOutcome outcome, HashSet<AchievementId> achievementsBefore)
    {
        var settlement = outcome.Settlement;
        var parts = new List<string>();

        if (settlement.LivingCostPaid.Wen > 0m)
        {
            parts.Add($"月用 {Display.Money(settlement.LivingCostPaid)}");
        }

        if (settlement.Incomes.Count > 0)
        {
            parts.Add($"进项 {Display.Money(settlement.Incomes.Sum(static line => line.Amount.Wen) is var total ? Money.FromWen(total) : Money.Zero)}");
        }

        parts.Add($"结余 {Display.Money(settlement.NetProfit)}");

        if (settlement.GrainPriceIndexAfter != settlement.GrainPriceIndexBefore)
        {
            parts.Add($"米价系数 {settlement.GrainPriceIndexAfter:0.###}");
        }

        Note(string.Join('，', parts) + "。");

        if (settlement.FamineBefore.Stage != settlement.FamineAfter.Stage)
        {
            Note(settlement.FamineAfter.Stage == FamineStage.None
                ? "☀ 饥荒缓解，恢复平常。"
                : $"⚠ 饥荒进入「{FamineStageText(settlement.FamineAfter.Stage)}」阶段（余 {FamineTimelineText(settlement.FamineAfter)}）。");
        }

        var life = settlement.Lifecycle;
        foreach (var birth in life.Births)
        {
            var baby = State.Family.TryGet(birth.PersonId);
            Note($"👶 {baby?.Name ?? "婴儿"} 降生（{Display.Gender(birth.Gender)}）。");
        }

        foreach (var marriage in life.Marriages)
        {
            var person = State.Family.TryGet(marriage.PersonId);
            var spouse = State.Family.TryGet(marriage.SpouseId);
            Note($"❤ {person?.Name ?? "族人"} 与 {spouse?.Name ?? "新人"} 成婚。");
        }

        foreach (var outMarriage in life.MarriedOut)
        {
            var person = State.Family.TryGet(outMarriage.PersonId);
            Note($"「{person?.Name ?? "族女"}」外嫁，移入外嫁档。");
        }

        foreach (var crossing in life.Crossings)
        {
            var person = State.Family.TryGet(crossing.PersonId);
            Note($"· {person?.Name} 进入 {BracketText(crossing.To)}。");
        }

        foreach (var illness in life.Illnesses)
        {
            var person = State.Family.TryGet(illness.PersonId);
            if (illness.Contracted)
            {
                Note($"✚ {person?.Name} 染疾。");
            }
            else if (illness.Recovered)
            {
                Note($"✓ {person?.Name} 康复。");
            }
        }

        foreach (var death in life.Deaths)
        {
            var person = State.Family.TryGet(death.PersonId);
            Note($"✝ {person?.Name ?? "族人"} 亡故（{DeathText(death.Cause)}）。");
        }

        foreach (var purchase in life.Purchases)
        {
            var person = State.Family.TryGet(purchase.PersonId);
            Note($"⚑ 买入人口 {Display.PersonBrief(person, State.CurrentDate)}，第 {purchase.Ordinal} 名，{Display.Money(purchase.Amount)}。");
        }

        if (life.PendingProposal is { } proposal)
        {
            var our = State.Family.TryGet(proposal.OurMemberId);
            Note($"✉ 有人向「{our?.Name}」提亲：{proposal.CandidateName}（{Display.Gender(proposal.CandidateGender)}，{proposal.CandidateAge} 岁，{Display.Degree(proposal.CandidateDegree)}），聘礼 {Display.Money(proposal.BetrothalGift)}，嫁妆 {Display.Money(proposal.Dowry)} —— 待应答。");
        }

        if (life.HeadBecameVacant)
        {
            Note("※ 家主之位空虚，请指定新家主。");
        }
        else if (life.NewHeadId is { } newHead)
        {
            Note($"※ 新家主：{State.Family.TryGet(newHead)?.Name}。");
        }

        var exam = settlement.ExamSeason;
        if (exam.Level is { } level)
        {
            Note($"📜 {Display.ExamLevel(level)}月：报考 {exam.AutoApplications.Count + exam.Outcomes.Count} 人。");
        }

        foreach (var outcomeLine in exam.Outcomes)
        {
            var person = State.Family.TryGet(outcomeLine.PersonId);
            var name = person?.Name ?? "考生";
            if (outcomeLine.Passed)
            {
                var extra = outcomeLine.Level == ExamLevel.DianShi
                    ? $"（{Display.Placement(outcomeLine.Placement)}）"
                    : string.Empty;
                Note($"🎉 {name} {Display.ExamLevel(outcomeLine.Level)}及第，功名 {Display.Degree(outcomeLine.DegreeAfter)}{extra}！");
            }
            else
            {
                Note($"✗ {name} {Display.ExamLevel(outcomeLine.Level)}落第（累计 {outcomeLine.FailureCountAfter} 次）。");
            }
        }

        foreach (var exemption in exam.Exemptions)
        {
            Note($"※ {State.Family.TryGet(exemption)?.Name} 获免解（解试累计三败）。");
        }

        foreach (var inquiry in exam.SpecialTributeInquiries)
        {
            Note($"✉ {State.Family.TryGet(inquiry)?.Name} 符合特奏名：是否接受授官（L18）？");
        }

        foreach (var appointment in exam.Appointments)
        {
            Note($"■ {State.Family.TryGet(appointment.PersonId)?.Name} 入仕：{TrackText(appointment.Track)}，待阙 {appointment.AwaitingPostMonths} 月。");
        }

        if (exam.HasShiStatusBecomesTrue)
        {
            Note("★ 家族晋为「仕」族！婚嫁按仕族、举族沾光。");
        }

        var career = settlement.Career;
        foreach (var appointment in career.Appointments)
        {
            Note($"■ {State.Family.TryGet(appointment.PersonId)?.Name} 授官 {appointment.Rank}（{TrackText(appointment.Track)}）。");
        }

        foreach (var promotion in career.Promotions)
        {
            Note($"▲ {State.Family.TryGet(promotion.PersonId)?.Name} 考课升迁：{promotion.From} → {promotion.To}。");
        }

        foreach (var retirement in career.Retirements)
        {
            Note($"○ {State.Family.TryGet(retirement)?.Name} 七十致仕，领半俸。");
        }

        var punishment = settlement.Punishment;
        foreach (var bribe in punishment.Bribes)
        {
            var person = State.Family.TryGet(bribe.Examinee);
            Note(bribe.Caught
                ? $"🚨 {person?.Name} 行贿 {Display.ExamLevel(bribe.Level)} 被查！"
                : $"（{person?.Name} 行贿 {Display.ExamLevel(bribe.Level)} 未被察觉，第 {bribe.Nth} 次）");
        }

        foreach (var sentence in punishment.Sentences)
        {
            Note($"⛓ {State.Family.TryGet(sentence.Subject)?.Name} 被判刑 {sentence.Months} 月。");
        }

        foreach (var fine in punishment.Fines)
        {
            Note($"💰 罚金 {Display.Money(fine.Amount)}（家产抵扣 {Display.Money(fine.PaidFromTreasury)}，转贷 {Display.Money(fine.ToLoan)}）。");
        }

        if (punishment.FamilyBanDeclared)
        {
            Note("⛔ 全族禁考禁升 48 月！");
        }

        foreach (var collective in punishment.CollectivePunishments)
        {
            var relative = State.Family.TryGet(collective.Relative);
            Note($"连坐：{relative?.Name ?? "亲属"} —— {CollectiveText(collective.Outcome)}。");
        }

        foreach (var release in punishment.Releases)
        {
            Note($"🔓 {State.Family.TryGet(release.Subject)?.Name} 获释。");
        }

        foreach (var mark in State.Achievements.Completed)
        {
            if (achievementsBefore.Add(mark.Achievement))
            {
                var definition = AchievementCatalog.Of(mark.Achievement);
                Note($"🏅 成就达成：{definition.Code} {definition.Name}（{Display.Difficulty(mark.Difficulty)}）");
            }
        }

        if (State.Ending.IsEnded)
        {
            Note("═══ 家族绝嗣，游戏结束 ═══");
        }
    }
}
