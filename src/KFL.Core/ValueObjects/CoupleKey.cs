namespace KFL.Core.ValueObjects;

/// <summary>
/// 夫妇键：**有序**（夫在前）的两人标识，用于「本年度已申请生育的夫妇集合」（007 的 R-06）。
/// </summary>
/// <remarks>
/// 本类型**不校验性别**——性别语义属规则层（<c>KFL.Rules/Family/Childbirth</c> 与
/// <c>LifecycleQueries</c>），Core 只承载「同一对夫妇」的等价判据。
/// 成员参数顺序 MUST NOT 颠倒，否则同一对夫妇会退化成两个不同的键。
/// </remarks>
/// <param name="HusbandId">夫方成员标识。</param>
/// <param name="WifeId">妻方成员标识。</param>
public readonly record struct CoupleKey(PersonId HusbandId, PersonId WifeId);
