namespace Spartanpad.Core;

/// <summary>
/// The encodings Notepad offers, with the reference binary's numeric values (the
/// <c>iDefaultEncoding</c> setting and the Save dialog's combo order both use them).
/// </summary>
public enum TextEncodingKind
{
    /// <summary>The system ANSI code page (Windows-1252 on most Western systems).</summary>
    Ansi = 1,

    /// <summary>UTF-16 little endian, written with a BOM.</summary>
    Utf16LE = 2,

    /// <summary>UTF-16 big endian, written with a BOM.</summary>
    Utf16BE = 3,

    /// <summary>UTF-8 with a byte order mark.</summary>
    Utf8Bom = 4,

    /// <summary>UTF-8 without a byte order mark. The reference default.</summary>
    Utf8 = 5,
}

public static class TextEncodingKindExtensions
{
    /// <summary>The names from the reference string table (32–35, 58).</summary>
    public static string DisplayName(this TextEncodingKind kind) => kind switch
    {
        TextEncodingKind.Ansi => "ANSI",
        TextEncodingKind.Utf16LE => "UTF-16 LE",
        TextEncodingKind.Utf16BE => "UTF-16 BE",
        TextEncodingKind.Utf8Bom => "UTF-8 with BOM",
        TextEncodingKind.Utf8 => "UTF-8",
        _ => kind.ToString(),
    };

    /// <summary>The order the reference Save dialog lists them in.</summary>
    public static readonly TextEncodingKind[] SaveOrder =
    [
        TextEncodingKind.Ansi,
        TextEncodingKind.Utf16LE,
        TextEncodingKind.Utf16BE,
        TextEncodingKind.Utf8,
        TextEncodingKind.Utf8Bom,
    ];

    public static bool IsDefined(int value) => value is >= 1 and <= 5;
}
