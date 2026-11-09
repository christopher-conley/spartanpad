using System.Text;
using Spartanpad.Core;

namespace Spartanpad.Tests;

public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "spartanpad-tests-" + Guid.NewGuid().ToString("N"));

    public TempDir()
    {
        Directory.CreateDirectory(Path);
    }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

public class TextDocumentTests
{
    [Fact]
    public void NewDocumentIsUntitledAndClean()
    {
        var doc = new TextDocument(TextEncodingKind.Utf8);
        Assert.True(doc.IsUntitled);
        Assert.Equal("Untitled", doc.DisplayName);
        Assert.False(doc.IsDirty);
        Assert.Equal("Untitled - Spartanpad", doc.Title("Spartanpad"));

        doc.SetText("x");
        Assert.True(doc.IsDirty);
        Assert.Equal("*Untitled - Spartanpad", doc.Title("Spartanpad"));

        // Reference §5: an Untitled document that is empty again is not dirty.
        doc.SetText(string.Empty);
        Assert.False(doc.IsDirty);
        Assert.Equal("Untitled - Spartanpad", doc.Title("Spartanpad"));
    }

    [Fact]
    public void EmptyingANamedDocumentStaysDirty()
    {
        using var dir = new TempDir();
        string path = dir.File("a.txt");
        File.WriteAllText(path, "x");
        TextDocument doc = TextDocument.Load(path, null, TextEncodingKind.Utf8);
        doc.SetText(string.Empty);
        Assert.True(doc.IsDirty);
    }

    [Fact]
    public void LoadDetectsEncodingAndLineEnding()
    {
        using var dir = new TempDir();
        string path = dir.File("a.txt");
        File.WriteAllBytes(path, TextCodec.Encode("one\nтwo", TextEncodingKind.Utf16LE));

        TextDocument doc = TextDocument.Load(path, null, TextEncodingKind.Utf8);
        Assert.Equal("one\nтwo", doc.Text);
        Assert.Equal(TextEncodingKind.Utf16LE, doc.Encoding);
        Assert.Equal(LineEnding.Lf, doc.LineEnding);
        Assert.Equal("a.txt", doc.DisplayName);
        Assert.False(doc.IsDirty);
    }

    [Fact]
    public void ForcedEncodingOverridesDetection()
    {
        using var dir = new TempDir();
        string path = dir.File("a.txt");
        File.WriteAllBytes(path, [(byte)'c', (byte)'a', (byte)'f', 0xC3, 0xA9]);

        Assert.Equal("café", TextDocument.Load(path, null, TextEncodingKind.Utf8).Text);
        TextDocument ansi = TextDocument.Load(path, TextEncodingKind.Ansi, TextEncodingKind.Utf8);
        Assert.Equal(TextEncodingKind.Ansi, ansi.Encoding);
        Assert.Equal(5, ansi.Text.Length);
    }

    [Fact]
    public void SaveWritesEncodingAndClearsDirty()
    {
        using var dir = new TempDir();
        var doc = new TextDocument(TextEncodingKind.Utf8);
        doc.SetText("héllo");
        string path = dir.File("out.txt");

        doc.Save(path, TextEncodingKind.Utf8Bom);
        Assert.False(doc.IsDirty);
        Assert.Equal(TextEncodingKind.Utf8Bom, doc.Encoding);
        Assert.Equal(System.IO.Path.GetFullPath(path), doc.FilePath);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, File.ReadAllBytes(path).Take(3).ToArray());
        Assert.Equal("héllo", TextDocument.Load(path, null, TextEncodingKind.Utf8).Text);
    }

    [Fact]
    public void SaveAsAnsiRefusesLossUnlessAllowed()
    {
        using var dir = new TempDir();
        var doc = new TextDocument(TextEncodingKind.Utf8);
        doc.SetText("世");
        string path = dir.File("out.txt");
        Assert.Throws<AnsiLossException>(() => doc.Save(path, TextEncodingKind.Ansi));
        Assert.True(doc.IsDirty);
        Assert.False(File.Exists(path));

        doc.Save(path, TextEncodingKind.Ansi, allowLossyAnsi: true);
        Assert.False(doc.IsDirty);
        Assert.Single(File.ReadAllBytes(path));
    }

    [Fact]
    public void LogFileGetsAStampAppended()
    {
        var doc = new TextDocument(TextEncodingKind.Utf8);
        doc.SetText(".LOG\r\nold");
        Assert.True(doc.IsLogFile);
        doc.AppendLogStamp("4:52 PM 11/8/2026");
        Assert.Equal(".LOG\r\nold\r\n4:52 PM 11/8/2026\r\n", doc.Text);
    }

    [Fact]
    public void FilesAtTheCapAreRefused()
    {
        using var dir = new TempDir();
        string path = dir.File("big.txt");
        using (FileStream f = File.Create(path))
        {
            f.SetLength(TextDocument.MaxFileBytes);
        }

        var e = Assert.Throws<FileTooLargeException>(() => TextDocument.Load(path, null, TextEncodingKind.Utf8));
        Assert.Equal(TextDocument.MaxFileBytes, e.Length);
    }

    [Fact]
    public void ResetReturnsToUntitledWithTheDefaultEncoding()
    {
        using var dir = new TempDir();
        string path = dir.File("a.txt");
        File.WriteAllText(path, "x\ny", Encoding.ASCII);
        TextDocument doc = TextDocument.Load(path, null, TextEncodingKind.Utf8);
        doc.Reset(TextEncodingKind.Utf16LE);
        Assert.True(doc.IsUntitled);
        Assert.Equal(string.Empty, doc.Text);
        Assert.Equal(TextEncodingKind.Utf16LE, doc.Encoding);
        Assert.Equal(LineEnding.Crlf, doc.LineEnding);
    }
}

public class SettingsTests
{
    [Fact]
    public void DefaultsMatchTheReference()
    {
        var s = new Settings();
        Assert.Equal("Lucida Console", s.FontFamily);
        Assert.Equal(100, s.PointSizeTenths);
        Assert.False(s.WordWrap);
        Assert.True(s.StatusBar);
        Assert.Equal(TextEncodingKind.Utf8, s.DefaultEncoding);
        Assert.Equal("&f", s.Header);
        Assert.Equal("Page &p", s.Footer);
        Assert.False(s.HasWindowRect);
        if (s.MetricMargins)
        {
            Assert.Equal((2500, 2500, 2000, 2000), (s.MarginTop, s.MarginBottom, s.MarginLeft, s.MarginRight));
        }
        else
        {
            Assert.Equal((1000, 1000, 750, 750), (s.MarginTop, s.MarginBottom, s.MarginLeft, s.MarginRight));
        }
    }

    [Fact]
    public void RoundTripsThroughJsonWithReferenceKeyNames()
    {
        using var dir = new TempDir();
        string path = dir.File("settings.json");
        var s = new Settings { WordWrap = true, PointSizeTenths = 140, SearchString = "needle", WindowX = 10, WindowY = 20, WindowWidth = 800, WindowHeight = 600 };
        s.DefaultEncoding = TextEncodingKind.Ansi;
        SettingsStore.Save(path, s);

        string json = File.ReadAllText(path);
        Assert.Contains("\"fWrap\": true", json);
        Assert.Contains("\"iPointSize\": 140", json);
        Assert.Contains("\"iDefaultEncoding\": 1", json);
        Assert.Contains("\"lfFaceName\"", json);

        Settings back = SettingsStore.Load(path);
        Assert.True(back.WordWrap);
        Assert.Equal(140, back.PointSizeTenths);
        Assert.Equal("needle", back.SearchString);
        Assert.Equal(TextEncodingKind.Ansi, back.DefaultEncoding);
        Assert.True(back.HasWindowRect);
    }

    [Fact]
    public void MissingOrCorruptFileGivesDefaults()
    {
        using var dir = new TempDir();
        Assert.False(SettingsStore.Load(dir.File("none.json")).WordWrap);
        string bad = dir.File("bad.json");
        File.WriteAllText(bad, "{ not json");
        Assert.Equal(100, SettingsStore.Load(bad).PointSizeTenths);
        File.WriteAllText(bad, "{ \"iDefaultEncoding\": 42, \"iPointSize\": -5 }");
        Settings s = SettingsStore.Load(bad);
        Assert.Equal(TextEncodingKind.Utf8, s.DefaultEncoding);
        Assert.Equal(100, s.PointSizeTenths);
    }
}
