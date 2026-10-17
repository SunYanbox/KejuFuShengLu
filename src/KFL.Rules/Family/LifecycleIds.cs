using System.Buffers.Binary;
using System.Text;
using KFL.Core.Entities;
using KFL.Core.ValueObjects;

namespace KFL.Rules.FamilyLifecycle;

/// <summary>
/// 007 运行期新建成员的 <see cref="PersonId"/> 分配器。
/// </summary>
/// <remarks>
/// <para>
/// 001~006 只在开局建成员（<c>NewGameSetup.MemberId</c>），运行期从不新建；007 的托媒配偶、新生儿、
/// 买来的旁系都要在月结算里建成员，故须自带一套**确定性**标识方案。
/// </para>
/// <para>
/// **MUST NOT 用 <c>Guid.NewGuid</c>**（G-07 / <c>EnvironmentDependencyTests</c> 禁止环境依赖）：
/// 标识由「家族名 + 用途 + 成员总数 + salt」经非加密散列得出，同输入恒同输出、不消费随机；
/// 与 `FamilyFixtures.Id`（末字节 0xA5）用不同末字节（0x5A）区分来源。
/// </para>
/// <para>这是 plan.md 文件清单之外的文件（007 实现期新增），已在 Phase 9 回写登记。</para>
/// </remarks>
internal static class LifecycleIds
{
    /// <summary>FNV-1a 64 位偏移基数（非加密用途，只要求确定性）。</summary>
    private const ulong OffsetBasis = 14695981039346656037UL;

    /// <summary>第二路散列的偏移基数（与 <see cref="OffsetBasis"/> 不同，拉开高 8 字节）。</summary>
    private const ulong OffsetBasisAlt = 11400714819323198485UL;

    /// <summary>FNV-1a 64 位质数。</summary>
    private const ulong Prime = 1099511628211UL;

    /// <summary>识别字节：与 <c>FamilyFixtures.Id</c> 的 0xA5 区分。</summary>
    private const byte PurposeSalt = 0x5A;

    /// <summary>为一个家族分配一个未占用的成员标识。</summary>
    /// <param name="family">目标家族。</param>
    /// <param name="purpose">用途标识（<c>proposal</c> / <c>birth</c> / <c>purchase</c>）。</param>
    /// <returns>该家族内未出现过的 <see cref="PersonId"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="family"/> 为 <c>null</c>。</exception>
    /// <exception cref="ArgumentException"><paramref name="purpose"/> 为空。</exception>
    internal static PersonId Next(Family family, string purpose)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        // family.Members 只增不减 ⇒ 每建一名成员后 Count 必变，salt 循环实际只兜底散列碰撞。
        for (var salt = 0; ; salt++)
        {
            var candidate = Derive(family.Name, purpose, family.Members.Count, salt);
            if (family.TryGet(candidate) is null)
            {
                return candidate;
            }
        }
    }

    /// <summary>按给定材料派生 16 字节标识（不做唯一性检查，唯一性由 <see cref="Next"/> 保证）。</summary>
    private static PersonId Derive(string familyName, string purpose, int ordinal, int salt)
    {
        var bytes = new byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(
            bytes.AsSpan(0, 8),
            Hash(OffsetBasis, familyName, purpose, ordinal, salt, tail: false));
        BinaryPrimitives.WriteUInt64LittleEndian(
            bytes.AsSpan(8, 8),
            Hash(OffsetBasisAlt, familyName, purpose, ordinal, salt, tail: true));
        bytes[15] ^= PurposeSalt;

        // 极小概率的全零（Guid.Empty 会被 PersonId 拒绝）——换一个 salt 重来。
        if (bytes[0] == 0 && bytes[7] == 0 && bytes[8] == 0 && bytes[15] == PurposeSalt)
        {
            bytes[0] = PurposeSalt;
        }

        return new PersonId(new Guid(bytes));
    }

    /// <summary>FNV-1a：逐字节吞入家族名、用途、成员总数与 salt。</summary>
    private static ulong Hash(
        ulong offsetBasis,
        string familyName,
        string purpose,
        int ordinal,
        int salt,
        bool tail)
    {
        var hash = offsetBasis;
        foreach (var value in familyName)
        {
            hash = Mix(hash, value);
        }

        foreach (var value in purpose)
        {
            hash = Mix(hash, value);
        }

        foreach (var value in Encoding.UTF8.GetBytes(ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)))
        {
            hash = Mix(hash, (char)value);
        }

        hash = Mix(hash, (char)salt);
        hash = Mix(hash, tail ? 'B' : 'A');
        return hash;
    }

    /// <summary>吞入一个字符。</summary>
    private static ulong Mix(ulong hash, char value)
    {
        hash ^= value;
        hash *= Prime;
        return hash;
    }
}
