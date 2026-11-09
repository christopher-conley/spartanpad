using System.Text.Unicode;

namespace Spartanpad.Core;

/// <summary>
/// Reproduces the reference open path (the local reference notes §6): byte order marks first, then
/// an <c>IsTextUnicode</c>-style check for UTF-16 without a BOM, then strict UTF-8 validation,
/// and ANSI as the last resort.
/// </summary>
public static class EncodingDetector
{
    /// <summary>The reference sniffs this many bytes to preselect the Open dialog's combo.</summary>
    public const int SniffLength = 1024;

    public static TextEncodingKind Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return TextEncodingKind.Utf8Bom;
        }

        if (bytes.Length >= 2)
        {
            if (bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return TextEncodingKind.Utf16LE;
            }

            if (bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return TextEncodingKind.Utf16BE;
            }
        }

        if (LooksLikeUtf16LE(bytes))
        {
            return TextEncodingKind.Utf16LE;
        }

        return Utf8.IsValid(bytes) ? TextEncodingKind.Utf8 : TextEncodingKind.Ansi;
    }

    /// <summary>
    /// The reference's use of <c>IsTextUnicode</c>: a positive answer from the statistical test
    /// alone is rejected under 100 bytes. This is an approximation of that API, not a port of it:
    /// a buffer of an even number of bytes whose 16-bit units are mostly ASCII (high byte zero)
    /// and contain no NUL or non-character units counts as UTF-16; UTF-16 whitespace or line
    /// breaks count as the "controls" test and are accepted at any length.
    /// </summary>
    public static bool LooksLikeUtf16LE(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 2 || (bytes.Length & 1) != 0)
        {
            return false;
        }

        int pairs = bytes.Length / 2;
        int asciiUnits = 0;
        int controlUnits = 0;
        for (int i = 0; i < bytes.Length; i += 2)
        {
            ushort unit = (ushort)(bytes[i] | (bytes[i + 1] << 8));
            if (unit == 0 || unit == 0xFFFF || unit == 0xFFFE)
            {
                return false;
            }

            if (bytes[i + 1] == 0)
            {
                asciiUnits++;
                if (unit is 0x0009 or 0x000A or 0x000D or 0x0020)
                {
                    controlUnits++;
                }
            }
        }

        bool statistics = asciiUnits > pairs / 2;
        if (controlUnits > 0 && statistics)
        {
            return true;
        }

        return statistics && bytes.Length >= 100;
    }
}
