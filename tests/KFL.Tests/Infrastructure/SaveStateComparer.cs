using KFL.Core.Entities;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Save.Serialization;
using Xunit;

namespace KFL.Tests.Infrastructure;

/// <summary>
/// 存档测试的**逐字段等价断言**（004-save-system；契约十 §5 的「信息集等价」判据）。
/// </summary>
/// <remarks>
/// <para>
/// 只有「往返一致」是不够的：两个格式**同时漏掉同一个字段**时，往返仍然一致。
/// 故本类把契约十 §4 键表列到的**每一个字段**都点名比一遍——漏存会在比对里直接暴露。
/// </para>
/// <para>
/// <b>金额用 <see cref="decimal.GetBits(decimal)"/> 比</b>：<c>decimal</c> 的 <c>==</c> 忽略尾随零，
/// <c>1.000m == 1.00m</c> 为真，而 FR-021 要求标度**位级**保留。
/// </para>
/// </remarks>
internal static class SaveStateComparer
{
    /// <summary>逐字段断言两个状态等价（含标度）。</summary>
    /// <param name="expected">期望（通常是内存中的原状态）。</param>
    /// <param name="actual">实际（读回的状态）。</param>
    /// <param name="compareId">
    /// 是否连存档标识一起比。默认 <c>true</c>；**「另存为」的分支档按设计换了标识**，
    /// 那条路径（规格书 §14 的 2026-10-19 裁决 ②）要传 <c>false</c>。
    /// </param>
    public static void AssertEquivalent(GameState expected, GameState actual, bool compareId = true)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        if (compareId)
        {
            Assert.Equal(expected.Id, actual.Id);
        }

        Assert.Equal(expected.CurrentDate, actual.CurrentDate);
        Assert.Equal(expected.Difficulty, actual.Difficulty);
        Assert.Equal(expected.PendingDifficulty, actual.PendingDifficulty);
        Assert.Equal(expected.Origin, actual.Origin);

        AssertFamily(expected.Family, actual.Family);
        AssertEconomy(expected.Economy, actual.Economy);

        // 008-achievements-extinction：成就状态的 8 个持久字段与终局状态的三元。
        // 这两块同样是「整块漏存时默认值会让往返静默通过」的典型。
        AssertAchievements(expected.Achievements, actual.Achievements);
        AssertEnding(expected.Ending, actual.Ending);

        // ⑲ 外来者簿记（落籍旁系 / 联姻对齐）：领域侧只有 internal 读取通道（KFL.Tests 看不见），
        // 故按 DTO 投影比**内容**。这两处簿记没有公开读取通道，漏存即静默失真，而成员集合里
        // 看不出它（契约十 §4 FamilyDto 键 4/5、data-model §3.11 第 ⑦ 步）。
        var expectedDto = SaveStateMapper.ToDto(expected).Family;
        var actualDto = SaveStateMapper.ToDto(actual).Family;
        Assert.Equal(expectedDto.SettledOutsiders, actualDto.SettledOutsiders);
        Assert.Equal(expectedDto.MarriageAlignedOutsiders, actualDto.MarriageAlignedOutsiders);
    }

    /// <summary>断言两个金额**位级**相等（标度也算）。</summary>
    /// <param name="expected">期望值。</param>
    /// <param name="actual">实际值。</param>
    /// <remarks>
    /// 008 修订（E-23）：**忽略零的符号位**。<see cref="decimal"/> 的文本形态不表达负号零——
    /// <c>(-0.000m).ToString()</c> 得到 <c>"0.000"</c>——故任何经文本往返的存档格式都不可能保留
    /// 该位（JSON 与二进制两条路径实测一致地丢失它）；而 FR-021 要求位级保留的是**标度**
    /// （尾随零，见本类顶部注释），不是零的符号。除 <c>±0</c> 外一切照旧逐位比较。
    /// </remarks>
    public static void AssertBits(decimal expected, decimal actual) =>
        Assert.Equal(CanonicalBits(expected), CanonicalBits(actual));

    /// <summary>取用于比较的位组：零一律按正零比较，其余原样。</summary>
    private static int[] CanonicalBits(decimal value)
    {
        var bits = decimal.GetBits(value);
        if (bits[0] == 0 && bits[1] == 0 && bits[2] == 0)
        {
            bits[3] &= 0x7FFFFFFF;
        }

        return bits;
    }

    private static void AssertFamily(Family expected, Family actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.HasShiStatus, actual.HasShiStatus);
        Assert.Equal(expected.AutoExamApplication, actual.AutoExamApplication);
        Assert.Equal(expected.HeadId, actual.HeadId);

        // 成员**含归档者**、且按加入次序（契约十 §4 FamilyDto 键 3）。
        var expectedMembers = expected.Members.ToList();
        var actualMembers = actual.Members.ToList();
        Assert.Equal(expectedMembers.Count, actualMembers.Count);

        for (var index = 0; index < expectedMembers.Count; index++)
        {
            AssertPerson(expectedMembers[index], actualMembers[index]);
        }

        Assert.Equal(expected.RegisteredMembers.Count, actual.RegisteredMembers.Count);
        Assert.Equal(expected.ArchivedMembers.Count, actual.ArchivedMembers.Count);

        AssertJournal(expected.PunishmentJournal, actual.PunishmentJournal);
        AssertLifecycle(expected.Lifecycle, actual.Lifecycle);
    }

    private static void AssertPerson(Person expected, Person actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Gender, actual.Gender);
        Assert.Equal(expected.BirthDate, actual.BirthDate);
        Assert.Equal(expected.Generation, actual.Generation);
        Assert.Equal(expected.Talents, actual.Talents);
        Assert.Equal(expected.Study, actual.Study);
        Assert.Equal(expected.Health, actual.Health);
        Assert.Equal(expected.Lifespan, actual.Lifespan);
        Assert.Equal(expected.Rank, actual.Rank);
        Assert.Equal(expected.Merit, actual.Merit);
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.Timers, actual.Timers);
        Assert.Equal(expected.Occupation, actual.Occupation);
        Assert.Equal(expected.MonthsInOffice, actual.MonthsInOffice);
        Assert.Equal(expected.EntryTrack, actual.EntryTrack);
        Assert.Equal(expected.FatherId, actual.FatherId);
        Assert.Equal(expected.MotherId, actual.MotherId);
        Assert.Equal(expected.SpouseId, actual.SpouseId);
        Assert.Equal(expected.FormerSpouseIds, actual.FormerSpouseIds);
        Assert.Equal(expected.DegreeHistory, actual.DegreeHistory);
        Assert.Equal(expected.JieShiFailureCount, actual.JieShiFailureCount);
        Assert.Equal(expected.ShengShiFailureCount, actual.ShengShiFailureCount);
        Assert.Equal(expected.AppliedExam, actual.AppliedExam);

        // ㉑ 行贿计数（006-bribery-punishment）：三个计数各自点名比，漏存一个会在比对里暴露。
        Assert.Equal(expected.JieShiBribeCount, actual.JieShiBribeCount);
        Assert.Equal(expected.ShengShiBribeCount, actual.ShengShiBribeCount);
        Assert.Equal(expected.DianShiBribeCount, actual.DianShiBribeCount);

        // 派生值也要一致（履历顺序错了，这里会先亮）。
        Assert.Equal(expected.CurrentDegree, actual.CurrentDegree);
        Assert.Equal(expected.CurrentPlacement, actual.CurrentPlacement);

        // 007-family-lifecycle：师承标记（`FamilyDto`/`PersonDto` 键 28）——漏存时默认为 false，
        // 夹具里取 true 才能让漏存暴露。属逻辑轨 ⑧ 的「宿儒课读」状态，读回必须原样带回。
        Assert.Equal(expected.IsTutoredByMaster, actual.IsTutoredByMaster);
    }

    /// <summary>
    /// ㉓ 家族生命周期簿记（007-family-lifecycle；`FamilyDto` 键 8 / `FamilyLifecycleDto` 键 0~6）：
    /// 五组状态 + 当月托媒名额逐项比对——整块漏存时默认值会让往返静默通过。
    /// </summary>
    /// <param name="expected">期望。</param>
    /// <param name="actual">实际。</param>
    private static void AssertLifecycle(FamilyLifecycleState expected, FamilyLifecycleState actual)
    {
        Assert.Equal(expected.PurchaseCount, actual.PurchaseCount);
        Assert.Equal(expected.PurchaseYear, actual.PurchaseYear);
        Assert.Equal(expected.BirthApplicationYear, actual.BirthApplicationYear);

        Assert.Equal(expected.PendingBirths.Count, actual.PendingBirths.Count);
        for (var index = 0; index < expected.PendingBirths.Count; index++)
        {
            Assert.Equal(expected.PendingBirths[index], actual.PendingBirths[index]);
        }

        Assert.Equal(expected.AppliedBirthCouples, actual.AppliedBirthCouples);
        Assert.Equal(expected.MatchmakeApplications, actual.MatchmakeApplications);

        var left = expected.Proposal;
        var right = actual.Proposal;
        Assert.Equal(left is null, right is null);

        if (left is null || right is null)
        {
            return;
        }

        Assert.Equal(left.Kind, right.Kind);
        Assert.Equal(left.OurMemberId, right.OurMemberId);
        Assert.Equal(left.CandidateName, right.CandidateName);
        Assert.Equal(left.CandidateGender, right.CandidateGender);
        Assert.Equal(left.CandidateAge, right.CandidateAge);
        Assert.Equal(left.TalentAgriculture, right.TalentAgriculture);
        Assert.Equal(left.TalentCommerce, right.TalentCommerce);
        Assert.Equal(left.TalentScholar, right.TalentScholar);
        Assert.Equal(left.TalentCraft, right.TalentCraft);
        Assert.Equal(left.CandidateStudy, right.CandidateStudy);
        Assert.Equal(left.CandidateHealth, right.CandidateHealth);
        Assert.Equal(left.CandidateLifespan, right.CandidateLifespan);
        Assert.Equal(left.CandidateDegree, right.CandidateDegree);
        Assert.Equal(left.Class, right.Class);
        AssertBits(left.BetrothalGift.Wen, right.BetrothalGift.Wen);
        AssertBits(left.Dowry.Wen, right.Dowry.Wen);
        Assert.Equal(left.ProposedAt, right.ProposedAt);
        Assert.Equal(left.CandidatePlacement, right.CandidatePlacement);
    }

    /// <summary>
    /// ㉔ 档内成就累计量（008-achievements-extinction；`SaveStateDto` 键 8 / `AchievementStateDto` 键 0~7）：
    /// **恰 8 个**持久字段逐个点名，其中四个容器按**次序**逐元素比对
    /// （`Lifespans` / `Completed` / `PendingUnlocks`；次序本身是产品信息，SC-014）。
    /// </summary>
    /// <param name="expected">期望。</param>
    /// <param name="actual">实际。</param>
    private static void AssertAchievements(AchievementState expected, AchievementState actual)
    {
        Assert.Equal(expected.SuccessfulBribeCount, actual.SuccessfulBribeCount);
        Assert.Equal(expected.BirthCount, actual.BirthCount);
        AssertBits(expected.PeakTotalAssets.Wen, actual.PeakTotalAssets.Wen);
        Assert.Equal(expected.EverHadLoan, actual.EverHadLoan);
        Assert.Equal(expected.DebugWakeCount, actual.DebugWakeCount);

        // ③ 寿命序列：逐元素 + 次序（三均值的输入是有序样本）。
        Assert.Equal(expected.Lifespans.Count, actual.Lifespans.Count);
        for (var index = 0; index < expected.Lifespans.Count; index++)
        {
            Assert.Equal(expected.Lifespans[index], actual.Lifespans[index]);
        }

        // ⑦ 已完成集合：三元组逐项 + 次序。
        Assert.Equal(expected.Completed.Count, actual.Completed.Count);
        for (var index = 0; index < expected.Completed.Count; index++)
        {
            var left = expected.Completed[index];
            var right = actual.Completed[index];

            Assert.Equal(left.Achievement, right.Achievement);
            Assert.Equal(left.Difficulty, right.Difficulty);
            Assert.Equal(left.CompletedAt, right.CompletedAt);
        }

        // ⑧ 待落盘队列：四个成员逐项 + 次序（末项是来源存档 UUID，漏存会让 SC-004 失真）。
        Assert.Equal(expected.PendingUnlocks.Count, actual.PendingUnlocks.Count);
        for (var index = 0; index < expected.PendingUnlocks.Count; index++)
        {
            var left = expected.PendingUnlocks[index];
            var right = actual.PendingUnlocks[index];

            Assert.Equal(left.Achievement, right.Achievement);
            Assert.Equal(left.Difficulty, right.Difficulty);
            Assert.Equal(left.UnlockedAt, right.UnlockedAt);
            Assert.Equal(left.SourceSaveId, right.SourceSaveId);
        }
    }

    /// <summary>
    /// ㉕ 终局状态（008；`SaveStateDto` 键 9 / `EndingStateDto` 键 0~2）：三元齐备性与编年史七项。
    /// </summary>
    /// <remarks>
    /// 三元任一不一致都要红：只比 <c>IsEnded</c> 会让「快照整体漏存」静默通过。
    /// 编年史 ⑦ 宗谱按**次序**逐元素比对（契约二十四「条款 34」）。
    /// </remarks>
    /// <param name="expected">期望。</param>
    /// <param name="actual">实际。</param>
    private static void AssertEnding(EndingState expected, EndingState actual)
    {
        Assert.Equal(expected.IsEnded, actual.IsEnded);
        Assert.Equal(expected.EndedAt, actual.EndedAt);

        var left = expected.Chronicle;
        var right = actual.Chronicle;
        Assert.Equal(left is null, right is null);

        if (left is null || right is null)
        {
            return;
        }

        Assert.Equal(left.SurvivedYears, right.SurvivedYears);
        Assert.Equal(left.Generations, right.Generations);
        AssertBits(left.PeakTotalAssets.Wen, right.PeakTotalAssets.Wen);
        Assert.Equal(left.HighestDegree, right.HighestDegree);
        Assert.Equal(left.TotalJinShi, right.TotalJinShi);
        AssertBits(left.AverageLifespan, right.AverageLifespan);

        Assert.Equal(left.Genealogy.Count, right.Genealogy.Count);
        for (var index = 0; index < left.Genealogy.Count; index++)
        {
            Assert.Equal(left.Genealogy[index], right.Genealogy[index]);
        }
    }

    /// <summary>㉒ 惩罚日志（006-bribery-punishment）：六类事实行逐字段比对（含可空字段与金额标度）。</summary>
    /// <param name="expected">期望。</param>
    /// <param name="actual">实际。</param>
    private static void AssertJournal(PunishmentJournal expected, PunishmentJournal actual)
    {
        Assert.Equal(expected.IsEmpty, actual.IsEmpty);
        Assert.Equal(expected.FamilyBanDeclared, actual.FamilyBanDeclared);

        Assert.Equal(expected.Bribes.Count, actual.Bribes.Count);
        for (var index = 0; index < expected.Bribes.Count; index++)
        {
            var left = expected.Bribes[index];
            var right = actual.Bribes[index];

            Assert.Equal(left.Date, right.Date);
            Assert.Equal(left.Examinee, right.Examinee);
            Assert.Equal(left.Level, right.Level);
            Assert.Equal(left.Nth, right.Nth);
            AssertBits(left.Amount.Wen, right.Amount.Wen);
            Assert.Equal(left.Caught, right.Caught);
            Assert.Equal(left.CapReached, right.CapReached);
        }

        Assert.Equal(expected.Sentences.Count, actual.Sentences.Count);
        for (var index = 0; index < expected.Sentences.Count; index++)
        {
            var left = expected.Sentences[index];
            var right = actual.Sentences[index];

            Assert.Equal(left.Date, right.Date);
            Assert.Equal(left.Subject, right.Subject);
            Assert.Equal(left.Months, right.Months);
        }

        Assert.Equal(expected.Fines.Count, actual.Fines.Count);
        for (var index = 0; index < expected.Fines.Count; index++)
        {
            var left = expected.Fines[index];
            var right = actual.Fines[index];

            Assert.Equal(left.Date, right.Date);
            Assert.Equal(left.Subject, right.Subject);
            AssertBits(left.Amount.Wen, right.Amount.Wen);
            AssertBits(left.PaidFromTreasury.Wen, right.PaidFromTreasury.Wen);
            AssertBits(left.ToLoan.Wen, right.ToLoan.Wen);
        }

        Assert.Equal(expected.RelativeBans.Count, actual.RelativeBans.Count);
        for (var index = 0; index < expected.RelativeBans.Count; index++)
        {
            var left = expected.RelativeBans[index];
            var right = actual.RelativeBans[index];

            Assert.Equal(left.Date, right.Date);
            Assert.Equal(left.Subject, right.Subject);
            Assert.Equal(left.Relative, right.Relative);
            Assert.Equal(left.ExamBanMonths, right.ExamBanMonths);
            Assert.Equal(left.PromotionBanMonths, right.PromotionBanMonths);
        }

        Assert.Equal(expected.CollectivePunishments.Count, actual.CollectivePunishments.Count);
        for (var index = 0; index < expected.CollectivePunishments.Count; index++)
        {
            var left = expected.CollectivePunishments[index];
            var right = actual.CollectivePunishments[index];

            Assert.Equal(left.Date, right.Date);
            Assert.Equal(left.Subject, right.Subject);
            Assert.Equal(left.Outcome, right.Outcome);
            Assert.Equal(left.Relative, right.Relative);
            Assert.Equal(left.DegreeBefore, right.DegreeBefore);
            Assert.Equal(left.DegreeAfter, right.DegreeAfter);
        }

        Assert.Equal(expected.Releases.Count, actual.Releases.Count);
        for (var index = 0; index < expected.Releases.Count; index++)
        {
            var left = expected.Releases[index];
            var right = actual.Releases[index];

            Assert.Equal(left.Date, right.Date);
            Assert.Equal(left.Subject, right.Subject);
            Assert.Equal(left.NewRankLevel, right.NewRankLevel);
        }
    }

    private static void AssertEconomy(FamilyEconomy expected, FamilyEconomy actual)
    {
        Assert.Equal(expected.LivingStandard, actual.LivingStandard);
        Assert.Equal(expected.PendingLivingStandard, actual.PendingLivingStandard);
        AssertBits(expected.GrainPriceIndex.Value, actual.GrainPriceIndex.Value);

        Assert.Equal(expected.Holdings.FarmlandMu, actual.Holdings.FarmlandMu);
        Assert.Equal(expected.Holdings.RuralHouses, actual.Holdings.RuralHouses);
        Assert.Equal(expected.Holdings.UrbanHouses, actual.Holdings.UrbanHouses);
        Assert.Equal(expected.Holdings.Shops, actual.Holdings.Shops);

        Assert.Equal(expected.Famine.Stage, actual.Famine.Stage);
        Assert.Equal(expected.Famine.ElapsedMonths, actual.Famine.ElapsedMonths);

        AssertTreasury(expected.Treasury, actual.Treasury);
        AssertLedger(expected.Ledger, actual.Ledger);
    }

    private static void AssertTreasury(Treasury expected, Treasury actual)
    {
        AssertBits(expected.Cash.Wen, actual.Cash.Wen);
        AssertBits(expected.Savings.Wen, actual.Savings.Wen);
        AssertBits(expected.MerchantCapital.Wen, actual.MerchantCapital.Wen);

        AssertBits(expected.Loan.Principal.Wen, actual.Loan.Principal.Wen);
        AssertBits(expected.Loan.AccruedInterest.Wen, actual.Loan.AccruedInterest.Wen);
        Assert.Equal(expected.Loan.MonthsSinceInterest, actual.Loan.MonthsSinceInterest);

        Assert.Equal(expected.SavingsRate, actual.SavingsRate);
        Assert.Equal(expected.SavingsRateYear, actual.SavingsRateYear);
    }

    private static void AssertLedger(Ledger expected, Ledger actual)
    {
        Assert.Equal(expected.Entries.Count, actual.Entries.Count);

        for (var index = 0; index < expected.Entries.Count; index++)
        {
            var left = expected.Entries[index];
            var right = actual.Entries[index];

            Assert.Equal(left.Date, right.Date);
            Assert.Equal(left.PersonId, right.PersonId);
            Assert.Equal(left.Category, right.Category);
            AssertBits(left.Amount.Wen, right.Amount.Wen);
        }
    }
}
