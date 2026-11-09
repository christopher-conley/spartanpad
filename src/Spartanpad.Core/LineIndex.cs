namespace Spartanpad.Core;

/// <summary>
/// Line starts of a text, for the status bar's <c>Ln, Col</c> and the Go To command. Lines break
/// at CRLF, LF or CR. Line and column numbers are 1-based, as displayed; a column counts
/// characters from the line start, so a tab is one column, as in the reference.
/// </summary>
public sealed class LineIndex
{
    private readonly int[] _lineStarts;

    public LineIndex(string text)
    {
        var starts = new List<int>(Math.Max(16, text.Length / 40)) { 0 };
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\n')
            {
                starts.Add(i + 1);
            }
            else if (c == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                starts.Add(i + 1);
            }
        }

        _lineStarts = starts.ToArray();
        TextLength = text.Length;
    }

    public int TextLength { get; }

    public int LineCount => _lineStarts.Length;

    /// <summary>The 0-based line that contains <paramref name="index"/>.</summary>
    public int LineOf(int index)
    {
        index = Math.Clamp(index, 0, TextLength);
        int found = Array.BinarySearch(_lineStarts, index);
        return found >= 0 ? found : ~found - 1;
    }

    /// <summary>The index of the first character of 0-based <paramref name="line"/>, or -1 when out of range.</summary>
    public int StartOf(int line) => line >= 0 && line < _lineStarts.Length ? _lineStarts[line] : -1;

    /// <summary>1-based line and column of <paramref name="index"/>.</summary>
    public (int Line, int Column) Position(int index)
    {
        index = Math.Clamp(index, 0, TextLength);
        int line = LineOf(index);
        return (line + 1, index - _lineStarts[line] + 1);
    }

    /// <summary>Reference string 37, leading space included.</summary>
    public static string StatusText(int line, int column) => $" Ln {line}, Col {column}";
}
