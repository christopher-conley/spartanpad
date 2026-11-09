namespace Spartanpad.Core;

/// <summary>Thrown when a file is above <see cref="TextDocument.MaxFileBytes"/> (reference string 8).</summary>
public sealed class FileTooLargeException(string path, long length)
    : Exception($"The {path} file is too large.")
{
    public string Path { get; } = path;
    public long Length { get; } = length;
}

/// <summary>
/// One open document: its text, where it came from, how it is encoded and whether it has unsaved
/// changes. Nothing in here knows about a window, so several of these can later sit behind tabs.
/// </summary>
public sealed class TextDocument
{
    /// <summary>
    /// The reference allows up to 1 GiB; the Avalonia text box is not built for that, so the
    /// practical cap is lower (the local reference notes §9.6).
    /// </summary>
    public const long MaxFileBytes = 8L * 1024 * 1024;

    public const string UntitledName = "Untitled";

    public string Text { get; private set; } = string.Empty;

    /// <summary>Full path of the file on disk, or null for an Untitled document.</summary>
    public string? FilePath { get; private set; }

    public TextEncodingKind Encoding { get; set; }

    public LineEnding LineEnding { get; set; } = LineEnding.Crlf;

    private bool _modified;

    /// <summary>
    /// Set by the first change after a load or save and cleared by a save, like the edit
    /// control's modify flag. Undoing back to the original text does not clear it. As in the
    /// reference (§5), an Untitled document whose text is empty again does not count as dirty.
    /// </summary>
    public bool IsDirty => _modified && (!IsUntitled || Text.Length > 0);

    /// <summary>True when the loaded text begins with <c>.LOG</c>; the caller appends a time stamp.</summary>
    public bool IsLogFile => Text.StartsWith(".LOG", StringComparison.Ordinal);

    public bool IsUntitled => FilePath is null;

    public string DisplayName => FilePath is null ? UntitledName : System.IO.Path.GetFileName(FilePath);

    public TextDocument(TextEncodingKind defaultEncoding)
    {
        Encoding = defaultEncoding;
    }

    /// <summary>The window title in the reference format <c>%1%2 - app</c>: a <c>*</c> while dirty.</summary>
    public string Title(string applicationName) => $"{(IsDirty ? "*" : string.Empty)}{DisplayName} - {applicationName}";

    /// <summary>Replaces the text as an edit would, marking the document dirty.</summary>
    public void SetText(string text)
    {
        if (!ReferenceEquals(text, Text))
        {
            Text = text;
            _modified = true;
        }
    }

    /// <summary>Marks the document changed without changing its text (a line ending switch).</summary>
    public void MarkDirty() => _modified = true;

    /// <summary>Appends a line break, <paramref name="stamp"/> and a line break (the <c>.LOG</c> behavior).</summary>
    public void AppendLogStamp(string stamp)
    {
        string nl = LineEnding.NewLine();
        SetText(Text + nl + stamp + nl);
    }

    /// <summary>
    /// Reads <paramref name="path"/>. With <paramref name="forcedEncoding"/> null the encoding is
    /// detected (§6); otherwise the file is read as that encoding (the <c>/A</c> and <c>/W</c>
    /// switches, or the status bar's reinterpret action).
    /// </summary>
    public static TextDocument Load(string path, TextEncodingKind? forcedEncoding, TextEncodingKind defaultEncoding)
    {
        var document = new TextDocument(defaultEncoding);
        document.Reload(path, forcedEncoding);
        return document;
    }

    /// <summary>Replaces this document's contents with the file at <paramref name="path"/>; see <see cref="Load"/>.</summary>
    public void Reload(string path, TextEncodingKind? forcedEncoding)
    {
        var info = new FileInfo(path);
        if (info.Length >= MaxFileBytes)
        {
            throw new FileTooLargeException(path, info.Length);
        }

        byte[] bytes = File.ReadAllBytes(path);
        TextEncodingKind kind = forcedEncoding ?? EncodingDetector.Detect(bytes);
        string text = TextCodec.Decode(bytes, kind);
        Text = text;
        FilePath = System.IO.Path.GetFullPath(path);
        Encoding = kind;
        LineEnding = LineEndingExtensions.Detect(text);
        _modified = false;
    }

    /// <summary>
    /// Writes the text to <paramref name="path"/> as <paramref name="encoding"/> and makes that
    /// the document's file and encoding. Throws <see cref="AnsiLossException"/> for ANSI text the
    /// code page cannot hold unless <paramref name="allowLossyAnsi"/>.
    /// </summary>
    public void Save(string path, TextEncodingKind encoding, bool allowLossyAnsi = false)
    {
        byte[] bytes = TextCodec.Encode(Text, encoding, allowLossyAnsi);
        File.WriteAllBytes(path, bytes);
        FilePath = System.IO.Path.GetFullPath(path);
        Encoding = encoding;
        _modified = false;
    }

    /// <summary>Forgets the file and text: the New command.</summary>
    public void Reset(TextEncodingKind defaultEncoding)
    {
        Text = string.Empty;
        FilePath = null;
        Encoding = defaultEncoding;
        LineEnding = LineEnding.Crlf;
        _modified = false;
    }
}
