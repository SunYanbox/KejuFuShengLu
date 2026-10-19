// SaveStateValidator 第 11~12 条：行贿计数与惩罚日志引用完整性。

using KFL.Core.Entities;
using KFL.Core.ValueObjects;

namespace KFL.Infrastructure.Save.Serialization;

public static partial class SaveStateValidator
{
    /// <summary>第 11 条：三个行贿计数 MUST <c>&gt;= 0</c>（006-bribery-punishment；data-model §6 I-1）。</summary>
    /// <remarks>
    /// 覆盖二进制侧的越界整数（JSON 侧为负数同样落在这里）；正数**无上限**——行贿次数
    /// 本身没有封顶，唯一的「封顶」是触顶拒绝（契约十五 §4.2）。
    /// </remarks>
    private static void EnsureBribeCountsNonNegative(IReadOnlyCollection<Person> members)
    {
        foreach (var member in members)
        {
            if (member.JieShiBribeCount < 0 || member.ShengShiBribeCount < 0 || member.DianShiBribeCount < 0)
            {
                throw Invalid(
                    $"成员 {member.Id} 的行贿计数为负（解试 {member.JieShiBribeCount}、省试 {member.ShengShiBribeCount}、殿试 {member.DianShiBribeCount}，R-04 第 11 条）。");
            }
        }
    }

    /// <summary>第 12 条：日志的每一行 <c>PersonId</c> MUST 存在于 <c>Family.Members</c>（含归档者）。</summary>
    /// <remarks>
    /// 覆盖八个位置（六类事实行）：行贿的考生、服刑/罚金/释放的当事人、亲属禁令与连坐的当事人及亲属
    /// （006-bribery-punishment；data-model §6 I-15、契约十六 §5）。
    /// </remarks>
    private static void EnsureJournalEntriesReferenceMembers(Family family)
    {
        var members = family.Members;
        var journal = family.PunishmentJournal;

        foreach (var fact in journal.Bribes)
        {
            EnsureMember(fact.Examinee, "行贿的考生");
        }

        foreach (var fact in journal.Sentences)
        {
            EnsureMember(fact.Subject, "服刑的当事人");
        }

        foreach (var fact in journal.Fines)
        {
            EnsureMember(fact.Subject, "罚金的当事人");
        }

        foreach (var fact in journal.RelativeBans)
        {
            EnsureMember(fact.Subject, "亲属禁令的当事人");
            EnsureMember(fact.Relative, "亲属禁令的亲属");
        }

        foreach (var fact in journal.CollectivePunishments)
        {
            EnsureMember(fact.Subject, "连坐的当事人");
            EnsureMember(fact.Relative, "连坐的亲属");
        }

        foreach (var fact in journal.Releases)
        {
            EnsureMember(fact.Subject, "释放的当事人");
        }

        void EnsureMember(PersonId id, string what)
        {
            if (Find(members, id) is null)
            {
                throw Invalid($"{what} {id} 不是本家族成员（R-04 第 12 条）。");
            }
        }
    }
}
