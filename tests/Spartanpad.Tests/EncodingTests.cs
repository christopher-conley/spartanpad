using System.Text;
using Spartanpad.Core;

namespace Spartanpad.Tests;

public class EncodingDetectorTests
{
    [Fact]
    public void Utf8BomIsDetected()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, (byte)'h', (byte)'i'];
        Assert.Equal(TextEncodingKind.Utf8Bom, EncodingDetector.Detect(bytes));
    }

    [Fact]
    public void Utf16BomsAreDetected()
    {
        Assert.Equal(TextEncodingKind.Utf16LE, EncodingDetector.Detect([0xFF, 0xFE, (byte)'a', 0]));
        Assert.Equal(TextEncodingKind.Utf16BE, EncodingDetector.Detect([0xFE, 0xFF, 0, (byte)'a']));
    }

    [Fact]
    public void ShortUtf16WithoutBomNeedsAControlCharacter()
    {
        // 10 ASCII letters as UTF-16 LE: statistics alone, under 100 bytes, rejected as in the
        // reference. NUL bytes are valid UTF-8, so the file then reads as UTF-8 (with the NULs
        // shown as spaces), which is also what the reference does.
        byte[] letters = Encoding.Unicode.GetBytes("abcdefghij");
        Assert.False(EncodingDetector.LooksLikeUtf16LE(letters));
        Assert.Equal(TextEncodingKind.Utf8, EncodingDetector.Detect(letters));

        // The same with a line break passes the controls test.
        byte[] withBreak = Encoding.Unicode.GetBytes("abcde\r\nfgh");
        Assert.Equal(TextEncodingKind.Utf16LE, EncodingDetector.Detect(withBreak));
    }

    [Fact]
    public void LongUtf16WithoutBomIsDetectedByStatistics()
    {
        byte[] bytes = Encoding.Unicode.GetBytes(new string('x', 80));
        Assert.True(bytes.Length >= 100);
        Assert.Equal(TextEncodingKind.Utf16LE, EncodingDetector.Detect(bytes));
    }

    [Fact]
    public void ValidUtf8IsUtf8AndInvalidIsAnsi()
    {
        Assert.Equal(TextEncodingKind.Utf8, EncodingDetector.Detect(Encoding.UTF8.GetBytes("héllo wörld\r\n")));
        Assert.Equal(TextEncodingKind.Utf8, EncodingDetector.Detect(Encoding.ASCII.GetBytes("plain ascii\n")));
        // 0xE9 alone is Windows-1252 'é' and never valid UTF-8.
        Assert.Equal(TextEncodingKind.Ansi, EncodingDetector.Detect([(byte)'c', (byte)'a', (byte)'f', 0xE9]));
    }

    [Fact]
    public void EmptyFileIsUtf8()
    {
        Assert.Equal(TextEncodingKind.Utf8, EncodingDetector.Detect([]));
    }
}

public class TextCodecTests
{
    [Theory]
    [InlineData(TextEncodingKind.Utf8)]
    [InlineData(TextEncodingKind.Utf8Bom)]
    [InlineData(TextEncodingKind.Utf16LE)]
    [InlineData(TextEncodingKind.Utf16BE)]
    public void UnicodeKindsRoundTripAndDetect(TextEncodingKind kind)
    {
        const string text = "Grüße, 世界 — line\r\nnext";
        byte[] bytes = TextCodec.Encode(text, kind);
        Assert.Equal(kind, EncodingDetector.Detect(bytes));
        Assert.Equal(text, TextCodec.Decode(bytes, kind));
    }

    [Fact]
    public void BomsAreWrittenExactly()
    {
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'a' }, TextCodec.Encode("a", TextEncodingKind.Utf8Bom));
        Assert.Equal(new byte[] { 0xFF, 0xFE, (byte)'a', 0 }, TextCodec.Encode("a", TextEncodingKind.Utf16LE));
        Assert.Equal(new byte[] { 0xFE, 0xFF, 0, (byte)'a' }, TextCodec.Encode("a", TextEncodingKind.Utf16BE));
        Assert.Equal(new byte[] { (byte)'a' }, TextCodec.Encode("a", TextEncodingKind.Utf8));
    }

    [Fact]
    public void AnsiRefusesUnrepresentableTextUnlessAllowed()
    {
        Assert.True(TextCodec.CanEncodeAnsi("plain"));
        Assert.False(TextCodec.CanEncodeAnsi("世界"));
        Assert.Throws<AnsiLossException>(() => TextCodec.Encode("世界", TextEncodingKind.Ansi));
        byte[] lossy = TextCodec.Encode("a世b", TextEncodingKind.Ansi, allowLossyAnsi: true);
        Assert.Equal(3, lossy.Length);
        Assert.Equal((byte)'a', lossy[0]);
        Assert.Equal((byte)'b', lossy[2]);
    }

    [Fact]
    public void NulCharactersBecomeSpaces()
    {
        Assert.Equal("a b", TextCodec.Decode([(byte)'a', 0, (byte)'b'], TextEncodingKind.Utf8));
    }

    [Fact]
    public void ForcedUtf16StripsItsBom()
    {
        byte[] le = TextCodec.Encode("hi", TextEncodingKind.Utf16LE);
        Assert.Equal("hi", TextCodec.Decode(le, TextEncodingKind.Utf16LE));
        byte[] be = TextCodec.Encode("hi", TextEncodingKind.Utf16BE);
        Assert.Equal("hi", TextCodec.Decode(be, TextEncodingKind.Utf16BE));
    }
}

public class LineEndingTests
{
    [Theory]
    [InlineData("no breaks", LineEnding.Crlf)]
    [InlineData("a\r\nb\nc", LineEnding.Crlf)]
    [InlineData("a\nb\r\nc", LineEnding.Lf)]
    [InlineData("a\rb", LineEnding.Cr)]
    [InlineData("", LineEnding.Crlf)]
    public void FirstBreakDecides(string text, LineEnding expected)
    {
        Assert.Equal(expected, LineEndingExtensions.Detect(text));
    }

    [Fact]
    public void NormalizeRewritesEveryKindOfBreak()
    {
        const string mixed = "a\r\nb\nc\rd";
        Assert.Equal("a\nb\nc\nd", LineEndingExtensions.Normalize(mixed, LineEnding.Lf));
        Assert.Equal("a\rb\rc\rd", LineEndingExtensions.Normalize(mixed, LineEnding.Cr));
        Assert.Equal("a\r\nb\r\nc\r\nd", LineEndingExtensions.Normalize(mixed, LineEnding.Crlf));
        Assert.Equal("\r\n\r\n", LineEndingExtensions.Normalize("\n\r", LineEnding.Crlf));
    }

    [Fact]
    public void NormalizeReturnsTheSameInstanceWhenNothingChanges()
    {
        const string text = "a\r\nb\r\n";
        Assert.Same(text, LineEndingExtensions.Normalize(text, LineEnding.Crlf));
        Assert.Same("no breaks", LineEndingExtensions.Normalize("no breaks", LineEnding.Lf));
    }
}
