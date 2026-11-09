using System.Globalization;

namespace Spartanpad.Core;

/// <summary>A match found by <see cref="TextSearch"/>.</summary>
/// <param name="Index">Start of the match in the text.</param>
/// <param name="Length">Length of the match (culture-aware matching can differ from the pattern's length).</param>
/// <param name="Wrapped">True when the match was found after wrapping past the start or end.</param>
public readonly record struct SearchMatch(int Index, int Length, bool Wrapped);

/// <summary>
/// Find, Find Next/Previous, Replace and Replace All as the reference does them (§7): a
/// culture-aware search (<c>FindNLSString</c> with <c>NORM_IGNORECASE</c> unless Match case), from
/// the selection end going down or the selection start going up, with the optional wrap around.
/// </summary>
public static class TextSearch
{
    private static CompareInfo Compare => CultureInfo.CurrentCulture.CompareInfo;

    private static CompareOptions Options(bool matchCase) => matchCase ? CompareOptions.None : CompareOptions.IgnoreCase;

    /// <summary>
    /// Finds the next occurrence of <paramref name="pattern"/> relative to the selection
    /// [<paramref name="selectionStart"/>, <paramref name="selectionEnd"/>).
    /// </summary>
    public static SearchMatch? Find(string text, string pattern, int selectionStart, int selectionEnd, bool forward, bool matchCase, bool wrapAround)
    {
        if (string.IsNullOrEmpty(pattern) || text.Length == 0)
        {
            return null;
        }

        selectionStart = Math.Clamp(selectionStart, 0, text.Length);
        selectionEnd = Math.Clamp(selectionEnd, 0, text.Length);
        if (selectionEnd < selectionStart)
        {
            (selectionStart, selectionEnd) = (selectionEnd, selectionStart);
        }

        // The reference treats a selection that covers the whole document as no selection.
        if (selectionStart == 0 && selectionEnd == text.Length)
        {
            selectionEnd = 0;
        }

        int selectionLength = selectionEnd - selectionStart;
        var options = Options(matchCase);
        var span = text.AsSpan();

        if (forward)
        {
            int i = Compare.IndexOf(span[selectionEnd..], pattern, options, out int length);
            if (i >= 0)
            {
                return new SearchMatch(selectionEnd + i, length, false);
            }

            if (wrapAround)
            {
                int limit = Math.Min(text.Length, selectionEnd + selectionLength);
                i = Compare.IndexOf(span[..limit], pattern, options, out length);
                if (i >= 0)
                {
                    return new SearchMatch(i, length, true);
                }
            }

            return null;
        }
        else
        {
            // Searching up from the selection start; with a selection the reference backs up one
            // character so the current match is not found again.
            int end = selectionLength > 0 ? selectionEnd - 1 : selectionEnd;
            int i = Compare.LastIndexOf(span[..end], pattern, options, out int length);
            if (i >= 0)
            {
                return new SearchMatch(i, length, false);
            }

            if (wrapAround)
            {
                int from = Math.Max(0, end - selectionLength);
                i = Compare.LastIndexOf(span[from..], pattern, options, out length);
                if (i >= 0)
                {
                    return new SearchMatch(from + i, length, true);
                }
            }

            return null;
        }
    }

    /// <summary>True when the selection is exactly one occurrence of <paramref name="pattern"/> (the Replace button's test).</summary>
    public static bool SelectionMatches(string text, string pattern, int selectionStart, int selectionEnd, bool matchCase)
    {
        if (string.IsNullOrEmpty(pattern) || selectionEnd <= selectionStart || selectionEnd > text.Length)
        {
            return false;
        }

        var selection = text.AsSpan(selectionStart, selectionEnd - selectionStart);
        int i = Compare.IndexOf(selection, pattern, Options(matchCase), out int length);
        return i == 0 && length == selection.Length;
    }

    /// <summary>Replaces every occurrence, from the top; returns the new text and how many were replaced.</summary>
    public static (string Text, int Count) ReplaceAll(string text, string pattern, string replacement, bool matchCase)
    {
        if (string.IsNullOrEmpty(pattern) || text.Length == 0)
        {
            return (text, 0);
        }

        var options = Options(matchCase);
        var result = new System.Text.StringBuilder(text.Length);
        int position = 0;
        int count = 0;
        while (position <= text.Length)
        {
            int i = Compare.IndexOf(text.AsSpan(position), pattern, options, out int length);
            if (i < 0 || length <= 0)
            {
                break;
            }

            result.Append(text, position, i);
            result.Append(replacement);
            position += i + length;
            count++;
        }

        if (count == 0)
        {
            return (text, 0);
        }

        result.Append(text, position, text.Length - position);
        return (result.ToString(), count);
    }
}
