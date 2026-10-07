using KFL.Core.Enums;

namespace KFL.Rules.Config;

/// <summary>
/// 贷款月划扣比例（规格书 §5.4「月划扣」、§10.2；research R-08 第 4 条）。
/// </summary>
/// <remarks>
/// <para>
/// 三档取值：**仕 20%**（家族级「仕」身份，`Family.HasShiStatus`）、
/// **商 80%**（`Origin == Merchant`）、**其余（士 / 工 / 农）40%**。
/// 判定次序固定为「先看仕身份，再看出身」——仕身份是家族级、与出身彼此独立可并存
/// （规格书 §10.2），故一名商出身的仕家仍按 20% 划扣。
/// </para>
/// <para>
/// **明确不含**：计息周期与利率区间（属 <see cref="InterestPolicy"/>）、
/// 封顶与先本后息（分别属 <c>LoanSettlement.ComputeRepayment</c> 与 <c>Loan.Repay</c>）。
/// </para>
/// </remarks>
public static class LoanPolicy
{
    /// <summary>「仕」身份家族的月划扣比例（20%，§5.4、§10.2）。</summary>
    public const decimal ShiRepaymentRatio = 0.20m;

    /// <summary>士 / 工 / 农（非仕、非商）的月划扣比例（40%，§5.4）。</summary>
    public const decimal CommonerRepaymentRatio = 0.40m;

    /// <summary>商出身的月划扣比例（80%，§5.4）。</summary>
    public const decimal MerchantRepaymentRatio = 0.80m;

    /// <summary>
    /// 取月划扣比例：<paramref name="hasShiStatus"/> 为真 → 20%；
    /// 否则 <paramref name="origin"/> 为商 → 80%；其余 → 40%（§5.4、§10.2）。
    /// </summary>
    /// <param name="hasShiStatus">家族是否具「仕」身份（进士直系血统，家族级）。</param>
    /// <param name="origin">家族出身。</param>
    /// <returns>月划扣比例。</returns>
    public static decimal RepaymentRatio(bool hasShiStatus, Origin origin)
    {
        if (hasShiStatus)
        {
            return ShiRepaymentRatio;
        }

        return origin == Origin.Merchant ? MerchantRepaymentRatio : CommonerRepaymentRatio;
    }
}
