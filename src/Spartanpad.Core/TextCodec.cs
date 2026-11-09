using System.Globalization;
using System.Text;

namespace Spartanpad.Core;

/// <summary>Thrown when text cannot be represented in the ANSI code page (reference string 27).</summary>
public sealed class AnsiLossException : Exception
{
    public AnsiLossException()
        : base("This file contains characters in Unicode format which will be lost if you save this file as an ANSI encoded text file.")
    {
    }
}

/// <summary>Bytes to text and back, the way the reference reads and writes files (§6).</summary>
public static class TextCodec
{
    private static readonly Lazy<Encoding> AnsiEncodingLazy = new(CreateAnsiEncoding);

    /// <summary>The system ANSI code page, Windows-1252 when the platform has no notion of one.</summary>
    public static Encoding AnsiEncoding => AnsiEncodingLazy.Value;

    private static Encoding CreateAnsiEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        int codePage = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
        if (codePage <= 0)
        {
            codePage = 1252;
        }

        try
        {
            return Encoding.GetEncoding(codePage);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException)
        {
            return Encoding.GetEncoding(1252);
        }
    }

    /// <summary>
    /// Decodes <paramref name="bytes"/> as <paramref name="kind"/>. A BOM matching the kind is
    /// skipped, as the reference does for each of its forced and detected cases. NUL characters
    /// become spaces, as in the reference load path.
    /// </summary>
    public static string Decode(ReadOnlySpan<byte> bytes, TextEncodingKind kind)
    {
        string text;
        switch (kind)
        {
            case TextEncodingKind.Utf8Bom:
                if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                {
                    bytes = bytes[3..];
                }

                text = Encoding.UTF8.GetString(bytes);
                break;
            case TextEncodingKind.Utf8:
                text = Encoding.UTF8.GetString(bytes);
                break;
            case TextEncodingKind.Utf16LE:
                if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                {
                    bytes = bytes[2..];
                }

                text = Encoding.Unicode.GetString(bytes[..(bytes.Length & ~1)]);
                break;
            case TextEncodingKind.Utf16BE:
                if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                {
                    bytes = bytes[2..];
                }

                text = Encoding.BigEndianUnicode.GetString(bytes[..(bytes.Length & ~1)]);
                break;
            case TextEncodingKind.Ansi:
                text = AnsiEncoding.GetString(bytes);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }

        return text.Contains('\0') ? text.Replace('\0', ' ') : text;
    }

    /// <summary>
    /// Encodes <paramref name="text"/> as <paramref name="kind"/>, BOM included where the kind
    /// has one. ANSI throws <see cref="AnsiLossException"/> for text the code page cannot hold
    /// unless <paramref name="allowLossyAnsi"/>, in which case such characters become '?'.
    /// </summary>
    public static byte[] Encode(string text, TextEncodingKind kind, bool allowLossyAnsi = false)
    {
        switch (kind)
        {
            case TextEncodingKind.Utf8:
                return Encoding.UTF8.GetBytes(text);
            case TextEncodingKind.Utf8Bom:
                return Prefix([0xEF, 0xBB, 0xBF], Encoding.UTF8.GetBytes(text));
            case TextEncodingKind.Utf16LE:
                return Prefix([0xFF, 0xFE], Encoding.Unicode.GetBytes(text));
            case TextEncodingKind.Utf16BE:
                return Prefix([0xFE, 0xFF], Encoding.BigEndianUnicode.GetBytes(text));
            case TextEncodingKind.Ansi:
                if (!allowLossyAnsi && !CanEncodeAnsi(text))
                {
                    throw new AnsiLossException();
                }

                return AnsiEncoding.GetBytes(text);
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    /// <summary>True when every character of <paramref name="text"/> survives the ANSI code page.</summary>
    public static bool CanEncodeAnsi(string text)
    {
        var strict = (Encoding)AnsiEncoding.Clone();
        strict.EncoderFallback = EncoderFallback.ExceptionFallback;
        try
        {
            strict.GetByteCount(text);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    private static byte[] Prefix(ReadOnlySpan<byte> bom, byte[] body)
    {
        var result = new byte[bom.Length + body.Length];
        bom.CopyTo(result);
        body.CopyTo(result, bom.Length);
        return result;
    }
}
