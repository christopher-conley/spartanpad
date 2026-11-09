using System.Globalization;

namespace Spartanpad.Core;

/// <summary>
/// The Time/Date command (F5) and the <c>.LOG</c> stamp: the short time without seconds, a
/// space, and the short date, in the user's locale (<c>GetTimeFormatW(TIME_NOSECONDS)</c> and
/// <c>GetDateFormatW(DATE_SHORTDATE)</c> in the reference). For en-US: <c>4:52 PM 11/8/2026</c>.
/// </summary>
public static class TimeDateStamp
{
    public static string Format(DateTime now, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        return PlainSpaces(now.ToString("t", culture)) + " " + PlainSpaces(now.ToString("d", culture));
    }

    /// <summary>
    /// ICU puts a narrow no-break space (U+202F) before AM/PM in some locales; Windows, and so
    /// the reference, prints an ordinary space. Keep the Windows look.
    /// </summary>
    public static string PlainSpaces(string formatted) =>
        formatted.Contains(' ') ? formatted.Replace(' ', ' ') : formatted;
}
