using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Rules.Config;
using KFL.Rules.Punishment;
using KFL.Tests.Fixtures;
using Xunit;

namespace KFL.Tests.Rules;

/// <summary>
/// T043 / US4 / SC-007：地狱殿试连坐的三分支（处死 / 降功名 / 全族禁令覆盖）、免死例外、
/// 外嫁归档女儿被跨族追及、范围封印，以及全族禁令的一次性解除。
/// </summary>
/// <remarks>
/// <para>
/// 连坐**只**在地狱难度 × 殿试 × 被抓时触发（契约十六 §6.1），故夹具一律取
/// <see cref="Difficulty.Hell"/> × <see cref="ExamLevel.DianShi"/>；其余 11 格见
/// <c>其余十一格连坐触发数为零</c>。
/// </para>
/// <para>
/// 夹具只用 <see cref="Family"/> 的公开 API 构造，辈分严格满足
/// 「血亲 = 父母辈分 + 1」（规格书 §4.4）；当事人的父亲与母亲 MUST 经
/// <see cref="Person.FatherId"/> / <see cref="Person.MotherId"/> 可达，否则不在亲属集合里。
/// </para>
/// <para>
/// 期望值一律经 <c>KFL.Rules/Config</c> 的成员取得（SC-008）；本文件 MUST NOT 另存规则数值。
/// </para>
/// </remarks>
public class CollectivePunishmentTests
{
    private static readonly Guid StateId = Guid.Parse("9d4c1b7e-5f62-4a83-9b0e-6c7d8e9f0a1b");

    /// <summary>参照年月：与 <see cref="RulesHarness.Date"/> 同一月，年龄据此推算。</summary>
    private static readonly GameDate Month = RulesHarness.Date;

    [Fact]
    public void 连坐三分支各命中一条且后果与格一致()
    {
        var kin = Kin.Build(spouse: false, son: true);

        // 父亲（有官职）⇒ 全族禁令覆盖；母亲（白身无功名无官职）⇒ 处死；儿子（举人无功名官职）⇒ 降功名。
        kin.Father.Rank = new OfficialRank(15);
        kin.Subject.AppendDegree(new DegreeRecord(DegreeLevel.GongShi, null, Month, DegreeChangeCause.ExamPass));
        kin.Son.AppendDegree(new DegreeRecord(DegreeLevel.JuRen, null, Month, DegreeChangeCause.ExamPass));

        var snapshot = Apply(kin.Family, kin.Subject);

        Assert.Single(
            snapshot.CollectivePunishments,
            fact => fact.Outcome == CollectiveOutcome.Executed);
        Assert.Single(
            snapshot.CollectivePunishments,
            fact => fact.Outcome == CollectiveOutcome.DegreeDemoted);
        Assert.Single(
            snapshot.CollectivePunishments,
            fact => fact.Outcome == CollectiveOutcome.FamilyWideBan);

        // ① 处死：母亲已亡、已归档（不在在册集合里）但仍在成员集合里。
        Assert.True(kin.Mother.Status.HasFlag(StatusFlag.Deceased));
        Assert.DoesNotContain(kin.Mother, kin.Family.RegisteredMembers);
        Assert.Contains(kin.Mother, kin.Family.Members);

        // ② 降功名：举人 → 白身，功名史恰追加一条 PunishmentDemotion，且前一条即原举人记录。
        Assert.Equal(DegreeLevel.BaiShen, kin.Son.CurrentDegree);
        var punished = Assert.Single(
            kin.Son.DegreeHistory,
            record => record.Cause == DegreeChangeCause.PunishmentDemotion);
        Assert.Equal(DegreeLevel.BaiShen, punished.Level);
        Assert.Equal(DegreeLevel.JuRen, kin.Son.DegreeHistory[^2].Level);

        // ③ 有官职者：受全族 48 月覆盖（禁考 + 禁升），非「禁升 60 月」。
        Assert.Equal(PunishmentPolicy.FamilyWideBanMonths, kin.Father.Timers.ExamBanRemainingMonths);
        Assert.Equal(PunishmentPolicy.FamilyWideBanMonths, kin.Father.Timers.PromotionBanRemainingMonths);
        Assert.True(kin.Father.Status.HasFlag(StatusFlag.ExamBanned | StatusFlag.PromotionBanned));

        // ③′ 有官职者走的是全族覆盖，但契约十六 §6.2 要求**逐人**落 `RelativeBanFact(48, 48)`，
        //     且施加结果与日志投影为**同一份**快照（`PunishmentImposer.cs:18-19`）——即时快照 MUST NOT 漏报。
        Assert.Contains(
            snapshot.RelativeBans,
            fact => fact.Relative == kin.Father.Id
                && fact.ExamBanMonths == PunishmentPolicy.FamilyWideBanMonths
                && fact.PromotionBanMonths == PunishmentPolicy.FamilyWideBanMonths);

        // 每条事实行的 Subject（当事人）都在家族成员集合里（含归档）。
        Assert.All(
            snapshot.CollectivePunishments,
            fact => Assert.Contains(kin.Family.Members, member => member.Id == fact.Subject));
    }

    [Fact]
    public void 当事人本人死刑立即执行且不建立服刑计时()
    {
        var kin = Kin.Build(spouse: false, son: false);

        var snapshot = Apply(kin.Family, kin.Subject);

        Assert.True(kin.Subject.Status.HasFlag(StatusFlag.Deceased));
        Assert.False(kin.Subject.Status.HasFlag(StatusFlag.ServingSentence));
        Assert.Null(kin.Subject.Timers.SentenceRemainingMonths);
        Assert.Empty(snapshot.Sentences);
        Assert.DoesNotContain(kin.Subject, kin.Family.RegisteredMembers);
    }

    /// <summary>
    /// 免死例外（契约十六 §6.3）：**当事人的配偶有官职** ∧ **该孩子未成年** ⇒ 该孩子免死；
    /// 其余组合一律处死。四种组合各一断言。
    /// </summary>
    /// <param name="spouseHasOffice">当事人的配偶是否有官职。</param>
    /// <param name="childIsMinor">当事人在世在册的孩子是否未成年。</param>
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void 免死例外只认配偶有官职且孩子未成年(bool spouseHasOffice, bool childIsMinor)
    {
        // 男 < 12 为未成年（沿用 002 的 AgeBracketPolicy，MUST NOT 另立判据）。
        var kin = Kin.Build(
            spouse: true,
            son: true,
            sonAge: AgeBracketPolicy.MaleAdulthoodAge - (childIsMinor ? 1 : 0));

        if (spouseHasOffice)
        {
            kin.Spouse.Rank = new OfficialRank(10);
        }

        var before = CollectiveFingerprint(kin.Son);
        var snapshot = Apply(kin.Family, kin.Subject);

        var exempt = spouseHasOffice && childIsMinor;
        var childFacts = snapshot.CollectivePunishments.Where(fact => fact.Relative == kin.Son.Id).ToArray();

        // 免死 ⇒ 连坐零差异、零事实行；否则处死并恰一条 Executed 事实行。
        Assert.Equal(exempt ? 0 : 1, childFacts.Length);
        Assert.Equal(exempt, before == CollectiveFingerprint(kin.Son));
        Assert.Equal(!exempt, kin.Son.Status.HasFlag(StatusFlag.Deceased));

        if (!exempt)
        {
            Assert.Equal(CollectiveOutcome.Executed, childFacts[0].Outcome);
            // 已亡者已被归档，故第⑦步的全族覆盖不再作用于他。
            Assert.Null(kin.Son.Timers.ExamBanRemainingMonths);
        }
        else
        {
            // 免死只是免于处死：他在世在册，全族 48 月禁令照常适用（契约十六 §6.3）。
            Assert.Equal(PunishmentPolicy.FamilyWideBanMonths, kin.Son.Timers.ExamBanRemainingMonths);
            Assert.Equal(PunishmentPolicy.FamilyWideBanMonths, kin.Son.Timers.PromotionBanRemainingMonths);
        }
    }

    [Fact]
    public void 免死例外不适用于当事人本人()
    {
        // 当事人的配偶有官职、自己有未成年孩子——免死**不**惠及当事人本人。
        var kin = Kin.Build(
            spouse: true,
            son: true,
            sonAge: AgeBracketPolicy.MaleAdulthoodAge - 1);
        kin.Spouse.Rank = new OfficialRank(10);

        Apply(kin.Family, kin.Subject);

        Assert.True(kin.Subject.Status.HasFlag(StatusFlag.Deceased));
        Assert.False(kin.Son.Status.HasFlag(StatusFlag.Deceased));
    }

    [Fact]
    public void 免死例外不扩展到未成年非子女亲属()
    {
        // 当事人的配偶有官职、当事人的母亲**未成年**——但母亲不是当事人的孩子。
        // 规格书 §7.4 与 §17 ③：免死例外的判据 = 「配偶有官职 ∧ **该孩子**未成年」，
        // **只适用于孩子**、不扩展到配偶以外的亲属，故母亲 MUST 被处死。
        var kin = Kin.Build(
            spouse: true,
            motherAge: AgeBracketPolicy.FemaleAdulthoodAge - 1);
        kin.Spouse.Rank = new OfficialRank(10);

        var snapshot = Apply(kin.Family, kin.Subject);

        Assert.True(kin.Mother.Status.HasFlag(StatusFlag.Deceased));
        Assert.Contains(
            snapshot.CollectivePunishments,
            fact => fact.Relative == kin.Mother.Id && fact.Outcome == CollectiveOutcome.Executed);
    }

    [Fact]
    public void 已出嫁归档的女儿被跨族追及()
    {
        var kin = Kin.Build(spouse: true, son: false, marriedOutDaughter: true);

        // 外嫁 = 归档（StatusFlag.MarriedOut）：已不在在册集合里，但仍属「女儿」。
        kin.MarriedOutDaughter.Status |= StatusFlag.MarriedOut;

        var snapshot = Apply(kin.Family, kin.Subject);

        Assert.True(kin.MarriedOutDaughter.Status.HasFlag(StatusFlag.Deceased));
        Assert.Contains(
            snapshot.CollectivePunishments,
            fact => fact.Relative == kin.MarriedOutDaughter.Id && fact.Outcome == CollectiveOutcome.Executed);

        // 第⑦步的全族覆盖只作用于**在世在册**成员，故已外嫁者不再被写禁令。
        Assert.Null(kin.MarriedOutDaughter.Timers.ExamBanRemainingMonths);
    }

    [Fact]
    public void 范围封印兄弟孙辈与姻亲不被连坐()
    {
        var kin = Kin.Build(spouse: false, son: true, brother: true, grandchild: true, daughterInLaw: true);
        kin.Son.AppendDegree(new DegreeRecord(DegreeLevel.JuRen, null, Month, DegreeChangeCause.ExamPass));

        var sealedOff = new[] { kin.Brother, kin.Grandchild, kin.DaughterInLaw };
        var before = sealedOff.Select(CollectiveFingerprint).ToArray();

        var snapshot = Apply(kin.Family, kin.Subject);

        // 连坐范围严格限于「配偶 / 儿子 / 女儿 / 父亲 / 母亲」：
        // 兄弟、孙辈、姻亲 MUST NOT 被处死、降功名，也不落任何连坐事实行——连坐差异项 = 0。
        for (var index = 0; index < sealedOff.Length; index++)
        {
            Assert.Equal(before[index], CollectiveFingerprint(sealedOff[index]));
            Assert.DoesNotContain(snapshot.CollectivePunishments, fact => fact.Relative == sealedOff[index].Id);
            Assert.False(sealedOff[index].Status.HasFlag(StatusFlag.Deceased));
            Assert.Equal(DegreeLevel.BaiShen, sealedOff[index].CurrentDegree);
            Assert.Null(sealedOff[index].Rank);
        }

        // 第⑦步的**全族**禁令不是连坐：它对每名在世在册成员一视同仁，故封印对象也照常被覆盖。
        foreach (var member in sealedOff)
        {
            Assert.Equal(PunishmentPolicy.FamilyWideBanMonths, member.Timers.ExamBanRemainingMonths);
            Assert.Equal(PunishmentPolicy.FamilyWideBanMonths, member.Timers.PromotionBanRemainingMonths);
        }
    }

    /// <summary>其余 11 格的连坐触发数 MUST 为 0（契约十六 §6.1）。</summary>
    [Theory]
    [InlineData(Difficulty.Easy, ExamLevel.DianShi)]
    [InlineData(Difficulty.Easy, ExamLevel.ShengShi)]
    [InlineData(Difficulty.Easy, ExamLevel.JieShi)]
    [InlineData(Difficulty.Normal, ExamLevel.DianShi)]
    [InlineData(Difficulty.Normal, ExamLevel.ShengShi)]
    [InlineData(Difficulty.Normal, ExamLevel.JieShi)]
    [InlineData(Difficulty.Hard, ExamLevel.DianShi)]
    [InlineData(Difficulty.Hard, ExamLevel.ShengShi)]
    [InlineData(Difficulty.Hard, ExamLevel.JieShi)]
    [InlineData(Difficulty.Hell, ExamLevel.ShengShi)]
    [InlineData(Difficulty.Hell, ExamLevel.JieShi)]
    public void 其余十一格连坐触发数为零(Difficulty difficulty, ExamLevel level)
    {
        var kin = Kin.Build(spouse: false, son: true);

        var state = NewState(kin.Family, difficulty);
        var snapshot = PunishmentImposer.Apply(state, kin.Subject, level, new FixedRandomService());

        Assert.Empty(snapshot.CollectivePunishments);
        Assert.False(kin.Mother.Status.HasFlag(StatusFlag.Deceased));
        Assert.False(kin.Son.Status.HasFlag(StatusFlag.Deceased));
    }

    [Fact]
    public void 全族禁令一次清除全部在册成员的相应计时且快照报已宣告()
    {
        var kin = Kin.Build(spouse: false, son: true);
        kin.Father.Rank = new OfficialRank(15);
        kin.Son.AppendDegree(new DegreeRecord(DegreeLevel.JuRen, null, Month, DegreeChangeCause.ExamPass));

        var snapshot = Apply(kin.Family, kin.Subject);

        Assert.True(snapshot.FamilyBanDeclared);
        Assert.True(kin.Family.PunishmentJournal.FamilyBanDeclared);
        Assert.Equal(PunishmentPolicy.FamilyWideBanMonths, kin.Father.Timers.ExamBanRemainingMonths);
        Assert.Equal(PunishmentPolicy.FamilyWideBanMonths, kin.Son.Timers.ExamBanRemainingMonths);

        PunishmentCommands.ClearFamilyBans(kin.Family);

        // 一次性清除**全部在册成员**的禁考与禁升计时（与 StatusOf 走同一入口）。
        foreach (var member in kin.Family.RegisteredMembers)
        {
            Assert.Null(member.Timers.ExamBanRemainingMonths);
            Assert.Null(member.Timers.PromotionBanRemainingMonths);
            Assert.False(member.Status.HasFlag(StatusFlag.ExamBanned | StatusFlag.PromotionBanned));
        }

        Assert.Null(kin.Father.Timers.ExamBanRemainingMonths);
        Assert.Null(kin.Son.Timers.ExamBanRemainingMonths);
    }

    /// <summary>以地狱殿试格施加一次连坐，返回快照。</summary>
    private static PunishmentSnapshot Apply(Family family, Person subject) =>
        PunishmentImposer.Apply(NewState(family, Difficulty.Hell), subject, ExamLevel.DianShi, new FixedRandomService());

    private static GameState NewState(Family family, Difficulty difficulty) =>
        new(
            StateId,
            Month,
            difficulty,
            Origin.Scholar,
            family,
            new FamilyEconomy(
                LivingStandard.Normal,
                new GrainPriceIndex(GrainPricePolicy.Initial),
                new Treasury(Money.FromGuan(1_000m)),
                new Holdings()));

    /// <summary>连坐可观测字段的指纹：功名、官阶、已亡位与功名史条数（不含第⑦步的计时）。</summary>
    private static string CollectiveFingerprint(Person person) =>
        $"{person.CurrentDegree}|{person.Rank?.Level}|{person.Status.HasFlag(StatusFlag.Deceased)}|{person.DegreeHistory.Count}";

    /// <summary>
    /// 连坐测试专用家族：父亲与母亲为开局成员并成婚，当事人是其血亲之子（故父亲与母亲在亲属集合里）；
    /// 兄弟是当事人的同胞、孙辈是儿子的孩子、姻亲是儿子的配偶——三者都**不**在五类亲属内。
    /// </summary>
    private sealed record Kin(
        Family Family,
        Person Father,
        Person Mother,
        Person Subject,
        Person Spouse,
        Person Son,
        Person Brother,
        Person Grandchild,
        Person DaughterInLaw,
        Person MarriedOutDaughter)
    {
        /// <summary>按旗标组装家族。</summary>
        /// <param name="spouse">是否加入当事人的配偶。</param>
        /// <param name="son">是否加入当事人的儿子。</param>
        /// <param name="sonAge">儿子的年龄（用于未成年判定）。</param>
        /// <param name="brother">是否加入当事人的同胞兄弟。</param>
        /// <param name="grandchild">是否加入当事人的孙辈。</param>
        /// <param name="daughterInLaw">是否加入当事人的姻亲（儿媳）。</param>
        /// <param name="marriedOutDaughter">是否加入当事人的已出嫁女儿。</param>
        /// <param name="motherAge">当事人母亲的年龄（用于「未成年非子女亲属」一例）。</param>
        public static Kin Build(
            bool spouse = true,
            bool son = true,
            int sonAge = 25,
            bool brother = false,
            bool grandchild = false,
            bool daughterInLaw = false,
            bool marriedOutDaughter = false,
            int motherAge = 58)
        {
            var family = new Family("地狱");

            var father = family.AddFoundingMember(Founding(2, Gender.Male, 60));
            var mother = family.AddFoundingMember(Founding(3, Gender.Female, motherAge));
            family.Marry(father.Id, mother.Id);

            var subject = family.AddChild(Blood(1, Gender.Male, 30, father.Id, mother.Id, generation: 1));
            var spousePerson = spouse
                ? family.AddOutsider(Founding(4, Gender.Female, 28, generation: 1))
                : null;

            if (spousePerson is not null)
            {
                family.Marry(subject.Id, spousePerson.Id);
            }

            var sonPerson = family.AddChild(Blood(5, Gender.Male, sonAge, subject.Id, generation: 2));
            var brotherPerson = brother                ? family.AddChild(Blood(6, Gender.Male, 32, father.Id, mother.Id, generation: 1))
                : null;
            var grandchildPerson = grandchild
                ? family.AddChild(Blood(7, Gender.Male, 3, sonPerson.Id, generation: 3))
                : null;
            var daughterInLawPerson = daughterInLaw
                ? family.AddOutsider(Founding(8, Gender.Female, 24, generation: 2))
                : null;

            if (daughterInLawPerson is not null)
            {
                family.Marry(sonPerson.Id, daughterInLawPerson.Id);
            }

            var marriedOutPerson = marriedOutDaughter
                ? family.AddChild(Blood(9, Gender.Female, 20, subject.Id, generation: 2))
                : null;

            return new Kin(
                family,
                father,
                mother,
                subject,
                spousePerson!,
                sonPerson,
                brotherPerson!,
                grandchildPerson!,
                daughterInLawPerson!,
                marriedOutPerson!);
        }

        /// <summary>辈分 0、无父母的开局成员。</summary>
        private static Person Founding(int index, Gender gender, int age, int generation = 0) =>
            FamilyFixtures.NewPerson(index, $"P{index}", gender, Month.Year - age, Month.Month, generation);

        /// <summary>辈分 = 父母辈分 + 1 的血亲成员。</summary>
        private static Person Blood(int index, Gender gender, int age, PersonId parentId, int generation) =>
            FamilyFixtures.NewPerson(
                index, $"P{index}", gender, Month.Year - age, Month.Month, generation, fatherId: parentId);

        /// <summary>辈分 = 父母辈分 + 1 的血亲成员（父母双全）。</summary>
        private static Person Blood(int index, Gender gender, int age, PersonId fatherId, PersonId motherId, int generation) =>
            FamilyFixtures.NewPerson(
                index, $"P{index}", gender, Month.Year - age, Month.Month, generation, fatherId, motherId);
    }
}
