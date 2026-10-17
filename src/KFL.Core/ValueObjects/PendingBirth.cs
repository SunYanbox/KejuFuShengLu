namespace KFL.Core.ValueObjects;

/// <summary>
/// 待分娩记录：已申请生育、等待**次月** ⑧-b 消费的一对夫妇（007 的 R-07）。
/// </summary>
/// <remarks>
/// 申请当月 MUST NOT 分娩（规格书 §9.4）；本记录只承载「哪一对夫妇」与「何时申请」，
/// 不含任何怀孕率或胎儿状态。
/// </remarks>
/// <param name="Couple">申请生育的夫妇。</param>
/// <param name="AppliedAt">申请时的年月。</param>
public readonly record struct PendingBirth(CoupleKey Couple, GameDate AppliedAt);
