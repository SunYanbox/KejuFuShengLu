namespace KFL.Infrastructure.Save;

/// <summary>
/// 档名口径的**唯一真源**（004-save-system；契约九 §4，FR-007）。
/// </summary>
/// <remarks>
/// <para>
/// <b>接受</b>：中文等任意 Unicode 字符、以及 <c>-</c>（连字符）。规格书 §14 的裁决 Q3 指出
/// 原文里那个 <c>-</c> 是 <c>*</c> 的笔误——真正被 Windows 文件系统禁止的是
/// <c>\ / : * ? " &lt; &gt; |</c> 这**九个**字符，连字符从来可用。
/// </para>
/// <para>
/// <b>拒绝</b>（MUST NOT 静默规整，契约九 §4 第 2 条）：空串、全空白、长度 &gt; 255、
/// Windows 保留设备名（<c>CON</c>/<c>PRN</c>/<c>AUX</c>/<c>NUL</c>/<c>COM1~9</c>/<c>LPT1~9</c>，
/// 不分大小写，带不带扩展名都算）、首尾空白、以点号结尾、ASCII 控制字符 <c>0x00~0x1F</c>。
/// </para>
/// <para>
/// <b>大小写</b>：同名判定（存在性、覆盖冲突、改名目标）MUST 大小写不敏感（Q7，Windows 口径：
/// <c>SongJia</c> ≡ <c>songjia</c>），但**落盘保留玩家输入的大小写**；不做 Unicode NFC 规整。
/// </para>
/// <para>
/// 本类型**零文件系统访问**（契约九 §4.1 明列其豁免行数为 0 行）。
/// </para>
/// </remarks>
public static class SaveName
{
    /// <summary>档名最大长度（字符数）。</summary>
    public const int MaxLength = 255;

    /// <summary>Windows 文件系统禁止的九个字符。</summary>
    private const string IllegalCharacters = "\\/:*?\"<>|";

    /// <summary>Windows 保留设备名（不分大小写；带扩展名时按**首个点之前**的部分判）。</summary>
    private static readonly string[] ReservedDeviceNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    /// <summary>校验档名，通过则**原样**返回（含玩家输入的大小写）。</summary>
    /// <param name="name">待校验的档名（不含扩展名）。</param>
    /// <returns>校验通过的档名，与入参逐字符相同。</returns>
    /// <exception cref="ArgumentException">档名不合口径。</exception>
    public static string Validate(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (name.Length == 0)
        {
            throw new ArgumentException("档名不得为空。", nameof(name));
        }

        if (name.Length > MaxLength)
        {
            throw new ArgumentException(
                $"档名长度不得超过 {MaxLength} 个字符，当前为 {name.Length}。", nameof(name));
        }

        if (char.IsWhiteSpace(name[0]) || char.IsWhiteSpace(name[^1]))
        {
            throw new ArgumentException("档名首尾不得含空白字符。", nameof(name));
        }

        if (name[^1] == '.')
        {
            throw new ArgumentException("档名不得以点号结尾。", nameof(name));
        }

        foreach (var ch in name)
        {
            if (ch <= '\u001F')
            {
                throw new ArgumentException(
                    $"档名不得含 ASCII 控制字符（U+{(int)ch:X4}）。", nameof(name));
            }

            if (IllegalCharacters.Contains(ch))
            {
                throw new ArgumentException(
                    $"档名不得含 Windows 文件系统保留字符（\\ / : * ? \" < > |），命中 '{ch}'。", nameof(name));
            }
        }

        var firstDot = name.IndexOf('.', StringComparison.Ordinal);
        var stem = firstDot < 0 ? name : name[..firstDot];
        if (ReservedDeviceNames.Contains(stem, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"档名不得使用 Windows 保留设备名（{stem}）。", nameof(name));
        }

        return name;
    }

    /// <summary>同名判定：**大小写不敏感**（Q7）。</summary>
    /// <param name="first">一个档名。</param>
    /// <param name="second">另一个档名。</param>
    /// <returns>二者视为同一档名 ⇒ <c>true</c>。</returns>
    public static bool IsSameName(string first, string second) =>
        string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
}
