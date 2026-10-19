using KFL.Core.Entities;
using KFL.Core.ValueObjects;

namespace KFL.Rules.FamilyLifecycle;

/// <summary>
/// 鐤剧梾鑷剤鐜囩殑**閫愰」鎷嗚В**锛?026-10-19 闇€姹?18锛氭垚鍛樿鎯呭睍绀鸿嚜鎰堢巼鏉ユ簮涓庡彇鍊硷級銆?/// </summary>
/// <param name="Health">褰撴湀浣撹川銆?/param>
/// <param name="Treated">鏄惁姝ｅ湪娌荤枟銆?/param>
/// <param name="BasePart">浣撹川椤癸紙娌荤枟鎬佸惈鍥哄畾鍔犳垚锛?c>浣撹川 脳 1.10% + 0.20</c>锛涙湭娌荤枟涓?<c>浣撹川 脳 1.00%</c>锛夈€?/param>
/// <param name="TalentMultiplier">涓诲澶╄祴鐨勮嚜鎰堜箻鏁般€?/param>
/// <param name="TalentName">涓诲澶╄祴鍚嶏紙鍐?/ 鍟?/ 浠?/ 宸ワ級銆?/param>
/// <param name="Raw">鍩虹 脳 澶╄祴涔樻暟銆?/param>
/// <param name="AgeBonus">骞撮緞澧炵泭锛堝姞绠楋級銆?/param>
/// <param name="DebugBonus">璋冭瘯瑕嗙洊鍔犳垚锛堢櫨鍒嗙偣锛夈€?/param>
/// <param name="Rate">鏈€缁堣嚜鎰堢巼锛堝凡閽冲埌 [0, 1]锛夈€?/param>
public readonly record struct HealRateBreakdown(
    int Health,
    bool Treated,
    decimal BasePart,
    decimal TalentMultiplier,
    string TalentName,
    decimal Raw,
    decimal AgeBonus,
    decimal DebugBonus,
    decimal Rate);

/// <summary>鑷剤鐜囨煡璇紙渚涚晫闈㈠睍绀猴紝MUST NOT 娑堣€楅殢鏈恒€丮UST NOT 鏀圭姸鎬侊級銆?/summary>
public static class IllnessQueries
{
    /// <summary>鎷嗚В鏌愭垚鍛樺湪 <paramref name="date"/> 鐨勮嚜鎰堢巼锛堜笌娓呴櫎鍒ゅ畾鐢ㄥ悓涓€浠藉叕寮忥級銆?/summary>
    /// <param name="person">鎴愬憳銆?/param>
    /// <param name="date">鏌ヨ鏃剁偣锛堢敤浜庡勾榫勫鐩婏級銆?/param>
    /// <param name="treated">鏄惁姝ｅ湪娌荤枟銆?/param>
    /// <returns>閫愰」鎷嗚В銆?/returns>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> 涓?<c>null</c>銆?/exception>
    public static HealRateBreakdown RecoveryBreakdown(Person person, GameDate date, bool treated) =>
        IllnessAdvance.BreakdownOf(person, date, treated);
}
