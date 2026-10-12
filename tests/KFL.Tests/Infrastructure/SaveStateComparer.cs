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
    public static void AssertEquivalent(GameState expected, GameState actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.CurrentDate, actual.CurrentDate);
        Assert.Equal(expected.Difficulty, actual.Difficulty);
        Assert.Equal(expected.PendingDifficulty, actual.PendingDifficulty);
        Assert.Equal(expected.Origin, actual.Origin);

        AssertFamily(expected.Family, actual.Family);
        AssertEconomy(expected.Economy, actual.Economy);

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
    public static void AssertBits(decimal expected, decimal actual) =>
        Assert.Equal(decimal.GetBits(expected), decimal.GetBits(actual));

    private static void AssertFamily(Family expected, Family actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.HasShiStatus, actual.HasShiStatus);
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

        // 派生值也要一致（履历顺序错了，这里会先亮）。
        Assert.Equal(expected.CurrentDegree, actual.CurrentDegree);
        Assert.Equal(expected.CurrentPlacement, actual.CurrentPlacement);
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
