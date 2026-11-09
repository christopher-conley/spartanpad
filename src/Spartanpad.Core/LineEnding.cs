namespace Spartanpad.Core;

/// <summary>The line ending a document uses, as shown in the status bar.</summary>
public enum LineEnding
{
    Crlf,
    Lf,
    Cr,
}

public static class LineEndingExtensions
{
    /// <summary>Status bar text from the reference string table (51–53), leading space included.</summary>
    public static string DisplayName(this LineEnding ending) => ending switch
    {
        LineEnding.Crlf => " Windows (CRLF)",
        LineEnding.Lf => " Unix (LF)",
        LineEnding.Cr => " Macintosh (CR)",
        _ => ending.ToString(),
    };

    public static string NewLine(this LineEnding ending) => ending switch
    {
        LineEnding.Lf => "\n",
        LineEnding.Cr => "\r",
        _ => "\r\n",
    };

    /// <summary>The reference lists them in this order in its string table (51–53).</summary>
    public static readonly LineEnding[] All = [LineEnding.Crlf, LineEnding.Lf, LineEnding.Cr];

    /// <summary>
    /// Rewrites every line break in <paramref name="text"/> (CRLF, LF or CR) as
    /// <paramref name="ending"/>. Returns the same instance when nothing changes.
    /// </summary>
    public static string Normalize(string text, LineEnding ending)
    {
        string nl = ending.NewLine();
        var result = new System.Text.StringBuilder(text.Length + text.Length / 16);
        bool changed = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r')
            {
                bool crlf = i + 1 < text.Length && text[i + 1] == '\n';
                if (crlf)
                {
                    i++;
                }

                result.Append(nl);
                changed |= (crlf ? "\r\n" : "\r") != nl;
            }
            else if (c == '\n')
            {
                result.Append(nl);
                changed |= nl != "\n";
            }
            else
            {
                result.Append(c);
            }
        }

        return changed ? result.ToString() : text;
    }

    /// <summary>
    /// The line ending of the first line break in <paramref name="text"/>. A document without a
    /// line break reports <see cref="LineEnding.Crlf"/>, the reference default for a new document.
    /// </summary>
    public static LineEnding Detect(ReadOnlySpan<char> text)
    {
        int i = text.IndexOfAny('\r', '\n');
        if (i < 0)
        {
            return LineEnding.Crlf;
        }

        if (text[i] == '\n')
        {
            return LineEnding.Lf;
        }

        return i + 1 < text.Length && text[i + 1] == '\n' ? LineEnding.Crlf : LineEnding.Cr;
    }
}
