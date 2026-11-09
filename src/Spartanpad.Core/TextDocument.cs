namespace Spartanpad.Core;

/// <summary>Thrown when a file is above <see cref="TextDocument.MaxFileBytes"/> (reference string 8).</summary>
public sealed class FileTooLargeException(string path, long length)
    : Exception($"The {path} file is too large.")
{
    public string Path { get; } = path;
    public long Length { get; } = length;
}

/// <summary>
/// One open document: where it came from, how it is encoded, its line ending and whether it has
/// unsaved changes. The text itself lives in the editor control, which is built to hold it; this
/// class reads it in from a file and writes it back out, and never keeps a copy. Nothing in here
/// knows about a window, so several of these can later sit behind tabs.
/// </summary>
public sealed class TextDocument
{
    /// <summary>
    /// The reference allows 1 GiB (the local reference notes §9.6). A .NET string holds at most
    /// 0x3FFFFFDF characters, and an ANSI or UTF-8 file decodes to at most one character per byte,
    /// so the cap is the reference's 1 GiB less the 33 bytes the string limit takes.
    /// </summary>
    public const long MaxFileBytes = 0x3FFFFFDF;

    public const string UntitledName = "Untitled";

    /// <summary>Full path of the file on disk, or null for an Untitled document.</summary>
    public string? FilePath { get; private set; }

    public TextEncodingKind Encoding { get; set; }

    public LineEnding LineEnding { get; set; } = LineEnding.Crlf;

    /// <summary>True when the loaded text began with <c>.LOG</c>; the caller appends a time stamp.</summary>
    public bool IsLogFile { get; private set; }

    private bool _modified;
    private int _textLength;

    /// <summary>
    /// Set by the first change after a load or save and cleared by a save, like the edit
    /// control's modify flag. Undoing back to the original text does not clear it. As in the
    /// reference (§5), an Untitled document whose text is empty again does not count as dirty.
    /// </summary>
    public bool IsDirty => _modified && (!IsUntitled || _textLength > 0);

    public bool IsUntitled => FilePath is null;

    public string DisplayName => FilePath is null ? UntitledName : System.IO.Path.GetFileName(FilePath);

    public TextDocument(TextEncodingKind defaultEncoding)
    {
        Encoding = defaultEncoding;
    }

    /// <summary>The window title in the reference format <c>%1%2 - app</c>: a <c>*</c> while dirty.</summary>
    public string Title(string applicationName) => $"{(IsDirty ? "*" : string.Empty)}{DisplayName} - {applicationName}";

    /// <summary>
    /// Records an edit: the document is dirty from now on. <paramref name="textLength"/> is the
    /// editor's current length, which decides the Untitled-and-empty exception.
    /// </summary>
    public void NotifyChanged(int textLength)
    {
        _textLength = textLength;
        _modified = true;
    }

    /// <summary>What the <c>.LOG</c> behavior appends: a line break, <paramref name="stamp"/> and a line break.</summary>
    public string LogStamp(string stamp)
    {
        string nl = LineEnding.NewLine();
        return nl + stamp + nl;
    }

    /// <summary>
    /// Reads <paramref name="path"/> and makes it this document's file; returns its text for the
    /// editor. With <paramref name="forcedEncoding"/> null the encoding is detected (§6); otherwise
    /// the file is read as that encoding (the <c>/A</c> and <c>/W</c> switches, or the status
    /// bar's reinterpret action).
    /// </summary>
    public string Load(string path, TextEncodingKind? forcedEncoding)
    {
        var info = new FileInfo(path);
        if (info.Length >= MaxFileBytes)
        {
            throw new FileTooLargeException(path, info.Length);
        }

        byte[] bytes = File.ReadAllBytes(path);
        TextEncodingKind kind = forcedEncoding ?? EncodingDetector.Detect(bytes);
        string text = TextCodec.Decode(bytes, kind);
        FilePath = System.IO.Path.GetFullPath(path);
        Encoding = kind;
        LineEnding = LineEndingExtensions.Detect(text);
        IsLogFile = text.StartsWith(".LOG", StringComparison.Ordinal);
        _textLength = text.Length;
        _modified = false;
        return text;
    }

    /// <summary>
    /// Writes <paramref name="text"/> to <paramref name="path"/> as <paramref name="encoding"/>
    /// and makes that the document's file and encoding. Throws <see cref="AnsiLossException"/>
    /// for ANSI text the code page cannot hold unless <paramref name="allowLossyAnsi"/>.
    /// </summary>
    public void Save(string path, string text, TextEncodingKind encoding, bool allowLossyAnsi = false)
    {
        byte[] bytes = TextCodec.Encode(text, encoding, allowLossyAnsi);
        File.WriteAllBytes(path, bytes);
        FilePath = System.IO.Path.GetFullPath(path);
        Encoding = encoding;
        _textLength = text.Length;
        _modified = false;
    }

    /// <summary>Forgets the file: the New command. The editor is emptied by the caller.</summary>
    public void Reset(TextEncodingKind defaultEncoding)
    {
        FilePath = null;
        Encoding = defaultEncoding;
        LineEnding = LineEnding.Crlf;
        IsLogFile = false;
        _textLength = 0;
        _modified = false;
    }
}
