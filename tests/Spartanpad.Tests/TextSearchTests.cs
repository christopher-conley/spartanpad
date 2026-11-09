using System.Globalization;
using Spartanpad.Core;

namespace Spartanpad.Tests;

public class TextSearchTests
{
    private const string Text = "the cat sat on the mat. The end.";

    [Fact]
    public void FindsForwardFromSelectionEnd()
    {
        SearchMatch? m = TextSearch.Find(Text, "the", 0, 0, forward: true, matchCase: true, wrapAround: false);
        Assert.Equal(new SearchMatch(0, 3, false), m);

        m = TextSearch.Find(Text, "the", 0, 3, forward: true, matchCase: true, wrapAround: false);
        Assert.Equal(new SearchMatch(15, 3, false), m);
    }

    [Fact]
    public void IgnoresCaseUnlessMatchCase()
    {
        SearchMatch? m = TextSearch.Find(Text, "the", 15, 18, forward: true, matchCase: false, wrapAround: false);
        Assert.Equal(new SearchMatch(24, 3, false), m);

        m = TextSearch.Find(Text, "the", 15, 18, forward: true, matchCase: true, wrapAround: false);
        Assert.Null(m);
    }

    [Fact]
    public void WrapsAroundForwardAndReportsIt()
    {
        SearchMatch? m = TextSearch.Find(Text, "cat", 20, 20, forward: true, matchCase: true, wrapAround: true);
        Assert.Equal(new SearchMatch(4, 3, true), m);

        Assert.Null(TextSearch.Find(Text, "cat", 20, 20, forward: true, matchCase: true, wrapAround: false));
    }

    [Fact]
    public void FindsBackwardFromSelectionStart()
    {
        // Selection on the second "the"; searching up skips it and finds the first.
        SearchMatch? m = TextSearch.Find(Text, "the", 15, 18, forward: false, matchCase: true, wrapAround: false);
        Assert.Equal(new SearchMatch(0, 3, false), m);

        // From the first, nothing above; wrap finds the last "the" (case-insensitive) at the end.
        Assert.Null(TextSearch.Find(Text, "the", 0, 3, forward: false, matchCase: false, wrapAround: false));
        m = TextSearch.Find(Text, "the", 0, 3, forward: false, matchCase: false, wrapAround: true);
        Assert.Equal(new SearchMatch(24, 3, true), m);
    }

    [Fact]
    public void WholeDocumentSelectionCountsAsNoSelection()
    {
        SearchMatch? m = TextSearch.Find(Text, "cat", 0, Text.Length, forward: true, matchCase: true, wrapAround: false);
        Assert.Equal(new SearchMatch(4, 3, false), m);
    }

    [Fact]
    public void EmptyPatternFindsNothing()
    {
        Assert.Null(TextSearch.Find(Text, "", 0, 0, true, true, true));
    }

    [Fact]
    public void SelectionMatchesChecksTheWholeSelection()
    {
        Assert.True(TextSearch.SelectionMatches(Text, "cat", 4, 7, matchCase: true));
        Assert.True(TextSearch.SelectionMatches(Text, "CAT", 4, 7, matchCase: false));
        Assert.False(TextSearch.SelectionMatches(Text, "CAT", 4, 7, matchCase: true));
        Assert.False(TextSearch.SelectionMatches(Text, "cat", 4, 8, matchCase: true));
        Assert.False(TextSearch.SelectionMatches(Text, "cat", 4, 4, matchCase: true));
    }

    [Fact]
    public void ReplaceAllReplacesEveryOccurrence()
    {
        (string result, int count) = TextSearch.ReplaceAll(Text, "the", "a", matchCase: false);
        Assert.Equal(3, count);
        Assert.Equal("a cat sat on a mat. a end.", result);

        (result, count) = TextSearch.ReplaceAll(Text, "the", "a", matchCase: true);
        Assert.Equal(2, count);
        Assert.Equal("a cat sat on a mat. The end.", result);

        (result, count) = TextSearch.ReplaceAll(Text, "zebra", "a", matchCase: true);
        Assert.Equal(0, count);
        Assert.Same(Text, result);
    }

    [Fact]
    public void ReplaceAllWithReplacementContainingPatternDoesNotLoop()
    {
        (string result, int count) = TextSearch.ReplaceAll("aaa", "a", "aa", matchCase: true);
        Assert.Equal(3, count);
        Assert.Equal("aaaaaa", result);
    }
}
public class ZoomTests
{
    [Fact]
    public void StepsAndClamps()
    {
        Assert.Equal(110, Zoom.In(100));
        Assert.Equal(90, Zoom.Out(100));
        Assert.Equal(500, Zoom.In(500));
        Assert.Equal(10, Zoom.Out(10));
        Assert.Equal(" 100%", Zoom.StatusText(100));
    }
}

public class TimeDateStampTests
{
    [Fact]
    public void UsesShortTimeWithoutSecondsThenShortDate()
    {
        var when = new DateTime(2026, 11, 8, 16, 52, 33);
        Assert.Equal("4:52 PM 11/8/2026", TimeDateStamp.Format(when, CultureInfo.GetCultureInfo("en-US")));
    }
}

public class HeaderFooterTests
{
    [Fact]
    public void DefaultsAndCodes()
    {
        var when = new DateTime(2026, 11, 8, 16, 52, 33);
        var culture = CultureInfo.GetCultureInfo("en-US");

        HeaderFooterParts h = HeaderFooter.Format(HeaderFooter.DefaultHeader, "notes.txt", 3, when, culture);
        Assert.Equal(new HeaderFooterParts("", "notes.txt", ""), h);

        HeaderFooterParts f = HeaderFooter.Format(HeaderFooter.DefaultFooter, "notes.txt", 3, when, culture);
        Assert.Equal(new HeaderFooterParts("", "Page 3", ""), f);

        HeaderFooterParts all = HeaderFooter.Format("&lLeft &d&cMid && co&rRight &t", "n.txt", 1, when, culture);
        Assert.Equal("Left 11/8/2026", all.Left);
        Assert.Equal("Mid & co", all.Center);
        Assert.Equal("Right 4:52:33 PM", all.Right);
    }
}

public class CommandLineTests
{
    [Fact]
    public void ParsesEncodingAndFile()
    {
        CommandLine c = CommandLine.Parse(["/A", "C:\\x\\a.txt"]);
        Assert.Equal(TextEncodingKind.Ansi, c.ForcedEncoding);
        Assert.Equal("C:\\x\\a.txt", c.FilePath);
        Assert.Equal(PrintMode.None, c.PrintMode);

        c = CommandLine.Parse(["-w", "b.txt", "extra"]);
        Assert.Equal(TextEncodingKind.Utf16LE, c.ForcedEncoding);
        Assert.Equal("b.txt", c.FilePath);
        Assert.Equal(["extra"], c.Rest);
    }

    [Fact]
    public void ParsesPrintSwitches()
    {
        CommandLine c = CommandLine.Parse(["/P", "a.txt"]);
        Assert.Equal(PrintMode.DefaultPrinter, c.PrintMode);
        Assert.Equal("a.txt", c.FilePath);

        c = CommandLine.Parse(["/PT", "a.txt", "My Printer", "driver", "port"]);
        Assert.Equal(PrintMode.NamedPrinter, c.PrintMode);
        Assert.Equal("a.txt", c.FilePath);
        Assert.Equal("My Printer", c.PrinterName);
        Assert.Equal(["driver", "port"], c.Rest);
    }

    [Fact]
    public void NoArgumentsMeansUntitled()
    {
        CommandLine c = CommandLine.Parse([]);
        Assert.Null(c.FilePath);
        Assert.Null(c.ForcedEncoding);
    }
}
