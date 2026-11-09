using System.Globalization;
using System.Text;

namespace Spartanpad.Core;

/// <summary>The three aligned parts of a rendered page header or footer.</summary>
public readonly record struct HeaderFooterParts(string Left, string Center, string Right)
{
    public bool IsEmpty => Left.Length == 0 && Center.Length == 0 && Right.Length == 0;
}

/// <summary>
/// Page Setup header/footer codes, from the reference's letter table <c>fFpPtTdDcCrRlL</c>:
/// <c>&amp;f</c> file name, <c>&amp;p</c> page number, <c>&amp;t</c> time, <c>&amp;d</c> date,
/// <c>&amp;l</c> <c>&amp;c</c> <c>&amp;r</c> switch the alignment of what follows (center is the
/// default), <c>&amp;&amp;</c> is a literal ampersand. The defaults are <c>&amp;f</c> and
/// <c>Page &amp;p</c>.
/// </summary>
public static class HeaderFooter
{
    public const string DefaultHeader = "&f";
    public const string DefaultFooter = "Page &p";

    public static HeaderFooterParts Format(string template, string fileName, int page, DateTime now, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var left = new StringBuilder();
        var center = new StringBuilder();
        var right = new StringBuilder();
        StringBuilder current = center;

        for (int i = 0; i < template.Length; i++)
        {
            char c = template[i];
            if (c != '&' || i + 1 >= template.Length)
            {
                current.Append(c);
                continue;
            }

            char code = template[++i];
            switch (char.ToLowerInvariant(code))
            {
                case 'f':
                    current.Append(fileName);
                    break;
                case 'p':
                    current.Append(page.ToString(culture));
                    break;
                case 't':
                    current.Append(TimeDateStamp.PlainSpaces(now.ToString("T", culture)));
                    break;
                case 'd':
                    current.Append(TimeDateStamp.PlainSpaces(now.ToString("d", culture)));
                    break;
                case 'l':
                    current = left;
                    break;
                case 'c':
                    current = center;
                    break;
                case 'r':
                    current = right;
                    break;
                case '&':
                    current.Append('&');
                    break;
                default:
                    // Unknown codes are dropped with their ampersand, as the reference does.
                    break;
            }
        }

        return new HeaderFooterParts(left.ToString(), center.ToString(), right.ToString());
    }
}
