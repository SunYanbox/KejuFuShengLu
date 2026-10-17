using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;

namespace KFL.Rules.FamilyLifecycle;

/// <summary>
/// 当月死亡登记表（data-model §3.6；契约十八 §2）：⑧ 各子步「先登记、后结算」的唯一载体。
/// </summary>
/// <remarks>
/// <para>
/// 登记 **MUST NOT** 改变 <see cref="Person.Status"/>——这是子步之间互相隔离的唯一手段：
/// ⑧-b~⑧-f 只登记，⑧-g 才统一置 <c>StatusFlag.Deceased</c>、进归档并落事件条目。
/// </para>
/// <para>
/// 同一人在多个子步被登记时**只保留首个**，优先级固定为
/// ⑧-b 分娩 &gt; ⑧-c 病亡 &gt; ⑧-e 饥馑 &gt; ⑧-f 寿终 / 夭折；后续登记 MUST 被忽略。
/// </para>
/// <para>
/// **与 data-model §3.6 的可见性差异**（Phase 9 回写登记）：本类改为 <c>public</c>——
/// <see cref="Childbirth.Run"/> / <see cref="DeathSettlement.Run"/> /
/// <c>LifespanAdvance.Run</c> 等执行器为可被测试直接调用而均为 <c>public</c>，
/// 参数类型不能比方法更封闭（CS0051）。
/// </para>
/// </remarks>
public sealed class DeathRegister
{
    private readonly Dictionary<PersonId, (Person Person, DeathCause Cause)> _entries = [];

    /// <summary>当月登记在案者的死因快照，按 <c>PersonId</c> 升序。</summary>
    public IReadOnlyList<(Person Person, DeathCause Cause)> Ordered =>
        _entries.Values.OrderBy(entry => entry.Person.Id.Value).ToList();

    /// <summary>该成员当月是否已被登记死亡（不论死因）。</summary>
    /// <param name="person">待查成员。</param>
    /// <returns>已登记则为 <c>true</c>。</returns>
    public bool Contains(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);
        return _entries.ContainsKey(person.Id);
    }

    /// <summary>登记一名成员的死亡；同人重复登记 MUST 被忽略（只保留首个死因）。</summary>
    /// <param name="person">亡者。</param>
    /// <param name="cause">死因。</param>
    public void Add(Person person, DeathCause cause)
    {
        ArgumentNullException.ThrowIfNull(person);
        _entries.TryAdd(person.Id, (person, cause));
    }
}
