using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Spartanpad.Core;
using Spartanpad.Views;
using EditorDocument = AvaloniaEdit.Document.TextDocument;

namespace Spartanpad;

/// <summary>
/// The Notepad window: menu, one editor, status bar. Behavior follows the local reference notes.
/// The text lives in the editor control (AvaloniaEdit, which lays out only the visible lines);
/// <see cref="TextDocument"/> holds the file, encoding, line ending and dirty state, so that a
/// later tab strip can hold several of them.
/// </summary>
public partial class MainWindow : Window
{
    private const string AppName = ReferenceStrings.ApplicationName;

    /// <summary>Families tried in order when the configured one is not installed (§9.4).</summary>
    private static readonly string[] MonospaceFallbacks =
        ["Lucida Console", "Consolas", "Cascadia Mono", "DejaVu Sans Mono", "Liberation Mono", "Noto Sans Mono", "Courier New"];

    private readonly Settings _settings;
    private readonly string _settingsPath;
    private readonly CommandLine _commandLine;
    private readonly TextDocument _document;

    private int _zoomPercent = Zoom.Default;
    private bool _loadingText;
    private bool _closeConfirmed;
    private bool _menuEntryOffered;
    private bool _altPointerGesture;
    private FindDialog? _findDialog;
    private ReplaceDialog? _replaceDialog;

    public MainWindow()
        : this(new Settings(), SettingsStore.DefaultPath, CommandLine.Parse([]))
    {
    }

    public MainWindow(Settings settings, string settingsPath, CommandLine commandLine)
    {
        _settings = settings;
        _settingsPath = settingsPath;
        _commandLine = commandLine;
        _document = new TextDocument(settings.DefaultEncoding);

        InitializeComponent();
        Icon = Icons.Window();
        ConfigureEditor();

        Editor.TextChanged += OnEditorTextChanged;
        Editor.TextArea.Caret.PositionChanged += OnEditorCaretMoved;
        Editor.TextArea.SelectionChanged += OnEditorCaretMoved;
        Editor.TextArea.TextEntering += OnEditorTextEntering;
        Editor.AddHandler(PointerWheelChangedEvent, OnEditorPointerWheel, RoutingStrategies.Tunnel);
        // Box selection with Alt and the mouse: Avalonia opens the menu bar when Alt is released
        // without another key having been pressed meanwhile, and a mouse press does not count. These
        // tunnel handlers claim that Alt release before Avalonia's own sees it (and close the menu if
        // the order ever turns out the other way), so the keyboard stays with the editor.
        Editor.TextArea.AddHandler(PointerPressedEvent, OnEditorPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyUpEvent, OnWindowKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
        // The text area takes drops of text itself and marks them handled; dropped files are the window's.
        AddHandler(DragDrop.DragOverEvent, OnDragOver, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(DragDrop.DropEvent, OnDrop, RoutingStrategies.Bubble, handledEventsToo: true);

        BuildEncodingFlyout();
        BuildLineEndingFlyout();
        BuildContextMenu();
        RegisterKeyBindings();

        ApplyWordWrap(_settings.WordWrap);
        ApplyStatusBarVisibility(_settings.StatusBar);
        ApplyFont();
        UpdateTitle();
        UpdateStatusBar();
    }

    /// <summary>The document behind the editor, for tests and a future tab strip.</summary>
    public TextDocument Document => _document;

    public Settings Settings => _settings;

    public int ZoomPercent => _zoomPercent;

    internal FindDialog? FindDialogForTests => _findDialog;

    internal ReplaceDialog? ReplaceDialogForTests => _replaceDialog;

    // ------------------------------------------------------------------ window lifecycle

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_settings.HasWindowRect)
        {
            Position = new PixelPoint(_settings.WindowX, _settings.WindowY);
            Width = _settings.WindowWidth;
            Height = _settings.WindowHeight;
        }

        FocusEditor();
        Dispatcher.UIThread.Post(() => _ = HandleCommandLineAsync(), DispatcherPriority.Background);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_closeConfirmed)
        {
            return;
        }

        e.Cancel = true;
        _ = CloseAfterPromptAsync();
    }

    private async Task CloseAfterPromptAsync()
    {
        if (!await ConfirmDiscardAsync())
        {
            return;
        }

        CloseFindDialog();
        CloseReplaceDialog();
        SaveSettings();
        _closeConfirmed = true;
        Close();
    }

    public void SaveSettings()
    {
        if (WindowState == WindowState.Normal)
        {
            _settings.WindowX = Position.X;
            _settings.WindowY = Position.Y;
            _settings.WindowWidth = (int)Math.Round(Width);
            _settings.WindowHeight = (int)Math.Round(Height);
        }

        try
        {
            SettingsStore.Save(_settingsPath, _settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings that cannot be written are not worth blocking the exit over.
        }
    }

    private async Task HandleCommandLineAsync()
    {
        if (_commandLine.FilePath is null)
        {
            return;
        }

        bool opened = await OpenPathAsync(_commandLine.FilePath, _commandLine.ForcedEncoding, createIfMissing: true);
        if (_commandLine.PrintMode != PrintMode.None)
        {
            if (opened)
            {
                await MessageDialog.ShowInfoAsync(this, AppName, ReferenceStrings.PrintingUnavailable);
            }

            _closeConfirmed = true;
            Close();
        }
    }

    // ------------------------------------------------------------------ key bindings

    private void RegisterKeyBindings()
    {
        void Bind(string gesture, Action action)
        {
            KeyBindings.Add(new KeyBinding { Gesture = KeyGesture.Parse(gesture), Command = new RelayCommand(action) });
        }

        // GLOBALACC
        Bind("Ctrl+N", () => _ = NewAsync());
        Bind("Ctrl+Shift+N", NewWindow);
        Bind("Ctrl+O", () => _ = OpenAsync());
        Bind("Ctrl+S", () => _ = SaveAsync());
        Bind("Ctrl+Shift+S", () => _ = SaveAsAsync());
        Bind("Ctrl+P", () => _ = PrintAsync());
        Bind("Ctrl+W", Close);
        Bind("F1", () => OpenUrl(Links.Help));
        Bind("F3", () => _ = FindNextAsync(forward: true));
        Bind("Shift+F3", () => _ = FindNextAsync(forward: false));
        Bind("Ctrl+G", () => _ = GoToAsync());
        Bind("Ctrl+F", ShowFind);
        Bind("Ctrl+H", ShowReplace);
        Bind("Ctrl+E", SearchWithKagi);
        Bind("Ctrl+OemPlus", () => SetZoom(Zoom.In(_zoomPercent)));
        Bind("Ctrl+Add", () => SetZoom(Zoom.In(_zoomPercent)));
        Bind("Ctrl+OemMinus", () => SetZoom(Zoom.Out(_zoomPercent)));
        Bind("Ctrl+Subtract", () => SetZoom(Zoom.Out(_zoomPercent)));
        Bind("Ctrl+D0", () => SetZoom(Zoom.Default));
        Bind("Ctrl+NumPad0", () => SetZoom(Zoom.Default));

        // MAINACC: the editor handles Ctrl+Z/X/C/V/A itself; these are the extra bindings.
        Bind("Ctrl+Insert", () => Editor.Copy());
        Bind("Alt+Back", () => Editor.Undo());
        Bind("F5", InsertTimeDate);
    }

    // ------------------------------------------------------------------ the editor control

    /// <summary>
    /// AvaloniaEdit is a code editor by default; this makes it behave like the reference's edit
    /// control: no auto-indent, no links, tab stops every 8 characters, and none of its own Find
    /// (its search panel would take Ctrl+F, Ctrl+H and F3 before the window's bindings see them).
    /// The panel installs itself when the template is applied, so it is removed right after that.
    /// One deliberate extra over the reference: column (box) selection, Alt+drag or Alt+Shift+arrows,
    /// where typing edits every selected line.
    /// </summary>
    private void ConfigureEditor()
    {
        Editor.Options.EnableHyperlinks = false;
        Editor.Options.EnableEmailHyperlinks = false;
        Editor.Options.EnableRectangularSelection = true;
        Editor.Options.ShowBoxForControlCharacters = false;
        Editor.Options.AllowScrollBelowDocument = false;
        Editor.Options.CutCopyWholeLine = false;
        Editor.Options.HighlightCurrentLine = false;
        Editor.Options.AcceptsTab = true;
        Editor.Options.ConvertTabsToSpaces = false;
        Editor.Options.IndentationSize = 8;
        Editor.TextArea.IndentationStrategy = null;
        Editor.TemplateApplied += (_, _) => Editor.SearchPanel?.Uninstall();
    }

    // ------------------------------------------------------------------ editor events

    // Raised synchronously for every change, including the swap of documents in ShowDocument.
    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_loadingText)
        {
            return;
        }

        _document.NotifyChanged(Editor.Document.TextLength);
        UpdateTitle();
        UpdateStatusBar();
    }

    private void OnEditorCaretMoved(object? sender, EventArgs e) => UpdateLineColumn();

    // Enter: the editor would copy the line ending of the current line, falling back to the
    // platform's; the reference inserts the document's. Replacing the selection through the
    // document keeps the caret after the inserted text and the change on the undo stack.
    private void OnEditorTextEntering(object? sender, TextInputEventArgs e)
    {
        if (e.Text is "\n" or "\r" or "\r\n")
        {
            e.Handled = true;
            ReplaceSelection(_document.LineEnding.NewLine());
        }
    }

    /// <summary>
    /// The editor control is not focusable itself (AvaloniaEdit keeps the focus in its text area),
    /// so this is what "focus the editor" means everywhere in the window.
    /// </summary>
    private void FocusEditor() => Editor.TextArea.Focus();

    /// <summary>Replaces the selection (or inserts at the caret) and leaves the caret after the new text, unselected.</summary>
    private void ReplaceSelection(string text)
    {
        int start = Editor.SelectionStart;
        int length = Editor.SelectionLength;
        Editor.Document.Replace(start, length, text);
        Editor.TextArea.ClearSelection();
        Editor.CaretOffset = start + text.Length;
    }

    private void OnEditorPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            _altPointerGesture = true;
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftAlt or Key.RightAlt)
        {
            _altPointerGesture = false;
        }
    }

    private void OnWindowKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.LeftAlt or Key.RightAlt) || !_altPointerGesture)
        {
            return;
        }

        _altPointerGesture = false;
        e.Handled = true;
        if (MainMenu.IsOpen)
        {
            MainMenu.Close();
        }

        FocusEditor();
    }

    private void OnEditorPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            SetZoom(e.Delta.Y > 0 ? Zoom.In(_zoomPercent) : Zoom.Out(_zoomPercent));
            e.Handled = true;
        }
    }

    // Runs after the text area's own handler (handledEventsToo), which decides for dragged text;
    // only a dragged file is the window's to accept.
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetFiles() is { Length: > 0 } files && files[0].TryGetLocalPath() is string path)
        {
            e.Handled = true;
            Activate();
            _ = OpenAfterPromptAsync(path, null);
        }
    }

    // ------------------------------------------------------------------ title and status bar

    private void UpdateTitle() => Title = _document.Title(AppName);

    public void UpdateStatusBar()
    {
        UpdateLineColumn();
        StatusZoom.Text = Zoom.StatusText(_zoomPercent);
        LineEndingButton.Content = _document.LineEnding.DisplayName();
        EncodingButton.Content = _document.Encoding.DisplayName();
    }

    // The caret's Column counts characters from the line start (a tab is one), as the reference does.
    private void UpdateLineColumn()
    {
        AvaloniaEdit.Editing.Caret caret = Editor.TextArea.Caret;
        StatusLineColumn.Text = ReferenceStrings.LineColumn(caret.Line, caret.Column);
    }

    private void SetStatusMessage(string text) => StatusMessage.Text = text;

    private void ApplyStatusBarVisibility(bool visible)
    {
        StatusBar.IsVisible = visible;
        StatusBarItem.IsChecked = visible;
    }

    private void BuildEncodingFlyout()
    {
        var flyout = new MenuFlyout { Placement = PlacementMode.Top };
        foreach (TextEncodingKind kind in TextEncodingKindExtensions.SaveOrder)
        {
            var item = new MenuItem { Header = kind.DisplayName(), Tag = kind };
            item.Click += (_, _) => _ = ChangeEncodingAsync(kind);
            flyout.Items.Add(item);
        }

        EncodingButton.Flyout = flyout;
    }

    private void BuildLineEndingFlyout()
    {
        var flyout = new MenuFlyout { Placement = PlacementMode.Top };
        foreach (LineEnding ending in LineEndingExtensions.All)
        {
            var item = new MenuItem { Header = ending.DisplayName().Trim(), Tag = ending };
            item.Click += (_, _) => ChangeLineEnding(ending);
            flyout.Items.Add(item);
        }

        LineEndingButton.Flyout = flyout;
    }

    /// <summary>
    /// Deviation §9.10: the reference only shows the line ending. Here the status bar segment
    /// picks one; the document's line breaks are rewritten to it (one undo step), Enter inserts
    /// it from then on, and the document is dirty.
    /// </summary>
    public void ChangeLineEnding(LineEnding ending)
    {
        if (ending == _document.LineEnding)
        {
            return;
        }

        string text = Editor.Text;
        string converted = LineEndingExtensions.Normalize(text, ending);
        _document.LineEnding = ending;
        if (!ReferenceEquals(converted, text))
        {
            int caret = Editor.CaretOffset;
            Editor.Document.Replace(0, Editor.Document.TextLength, converted);
            Editor.TextArea.ClearSelection();
            Editor.CaretOffset = Math.Min(caret, converted.Length);
        }

        _document.NotifyChanged(Editor.Document.TextLength);
        UpdateTitle();
        UpdateStatusBar();
    }

    /// <summary>
    /// Deviation §9.2: the encoding combo of the reference's file dialogs lives in the status bar.
    /// A clean document that has a file is re-read in the chosen encoding; otherwise the choice
    /// is the encoding the next save writes.
    /// </summary>
    private async Task ChangeEncodingAsync(TextEncodingKind kind)
    {
        if (kind == _document.Encoding)
        {
            return;
        }

        if (!_document.IsDirty && _document.FilePath is string path)
        {
            await OpenPathAsync(path, kind, createIfMissing: false);
            return;
        }

        _document.Encoding = kind;
        UpdateStatusBar();
    }

    // ------------------------------------------------------------------ fonts, wrap, zoom

    private void ApplyFont()
    {
        Editor.FontFamily = ResolveFontFamily(_settings.FontFamily);
        Editor.FontWeight = _settings.FontWeight >= 600 ? FontWeight.Bold : FontWeight.Normal;
        Editor.FontStyle = _settings.FontItalic ? FontStyle.Italic : FontStyle.Normal;
        ApplyFontSize();
    }

    private void ApplyFontSize()
    {
        // iPointSize is tenths of a point; Avalonia sizes are device-independent pixels at 96/inch.
        Editor.FontSize = _settings.PointSizeTenths / 10.0 * 96 / 72 * _zoomPercent / 100.0;
    }

    private static FontFamily ResolveFontFamily(string configured)
    {
        HashSet<string> installed;
        try
        {
            installed = FontManager.Current.SystemFonts.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return new FontFamily(configured);
        }

        if (installed.Contains(configured))
        {
            return new FontFamily(configured);
        }

        foreach (string candidate in MonospaceFallbacks)
        {
            if (installed.Contains(candidate))
            {
                return new FontFamily(candidate);
            }
        }

        return new FontFamily("monospace");
    }

    private void ApplyWordWrap(bool wrap)
    {
        Editor.WordWrap = wrap;
        Editor.HorizontalScrollBarVisibility = wrap ? Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled : Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        WordWrapItem.IsChecked = wrap;
    }

    public void SetZoom(int percent)
    {
        _zoomPercent = Zoom.Clamp(percent);
        ApplyFontSize();
        StatusZoom.Text = Zoom.StatusText(_zoomPercent);
    }

    // ------------------------------------------------------------------ loading text into the editor

    /// <summary>
    /// Puts <paramref name="text"/> in the editor as a fresh document, with an empty undo stack so
    /// the previous document cannot be undone back, and the caret at the start.
    /// </summary>
    private void ShowDocument(string text)
    {
        _loadingText = true;
        try
        {
            Editor.Document = new EditorDocument(text);
            Editor.CaretOffset = 0;
            Editor.TextArea.ClearSelection();
        }
        finally
        {
            _loadingText = false;
        }

        SetStatusMessage(string.Empty);
        UpdateTitle();
        UpdateStatusBar();
    }

    // ------------------------------------------------------------------ file commands

    /// <summary>The save prompt (§7): true when the caller may go on, false on Cancel or a failed save.</summary>
    public async Task<bool> ConfirmDiscardAsync()
    {
        if (!_document.IsDirty)
        {
            return true;
        }

        string name = _document.FilePath ?? TextDocument.UntitledName;
        string? choice = await MessageDialog.ShowAsync(this, AppName,
            string.Format(ReferenceStrings.SaveChangesQuestion, name),
            ReferenceStrings.SaveButton, ReferenceStrings.DontSaveButton, ReferenceStrings.CancelButton);
        return choice switch
        {
            ReferenceStrings.SaveButton => await SaveAsync(),
            ReferenceStrings.DontSaveButton => true,
            _ => false,
        };
    }

    public async Task NewAsync()
    {
        if (!await ConfirmDiscardAsync())
        {
            return;
        }

        _document.Reset(_settings.DefaultEncoding);
        ShowDocument(string.Empty);
    }

    private void NewWindow()
    {
        string? exe = Environment.ProcessPath;
        if (exe is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Nothing to do: the user can start another copy by hand.
        }
    }

    private static FilePickerFileType[] FileTypes =>
    [
        new FilePickerFileType(ReferenceStrings.FilterTextDocuments) { Patterns = ["*.txt"] },
        new FilePickerFileType(ReferenceStrings.FilterAllFiles) { Patterns = ["*"] },
    ];

    public async Task OpenAsync()
    {
        if (!await ConfirmDiscardAsync())
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = ReferenceStrings.OpenTitle,
            AllowMultiple = false,
            FileTypeFilter = FileTypes,
            SuggestedStartLocation = await SuggestedFolderAsync(),
        });
        if (files.Count == 0)
        {
            return;
        }

        if (files[0].TryGetLocalPath() is not string path)
        {
            await MessageDialog.ShowInfoAsync(this, AppName, ReferenceStrings.NotValidFileName);
            return;
        }

        await OpenPathAsync(path, null, createIfMissing: false);
    }

    private async Task OpenAfterPromptAsync(string path, TextEncodingKind? forced)
    {
        if (await ConfirmDiscardAsync())
        {
            await OpenPathAsync(path, forced, createIfMissing: false);
        }
    }

    /// <summary>Loads <paramref name="path"/> into the editor; reports the reference's errors.</summary>
    public async Task<bool> OpenPathAsync(string path, TextEncodingKind? forced, bool createIfMissing)
    {
        try
        {
            if (!File.Exists(path))
            {
                if (Directory.Exists(path))
                {
                    await MessageDialog.ShowInfoAsync(this, AppName, string.Format(ReferenceStrings.CannotOpen, path));
                    return false;
                }

                if (!createIfMissing)
                {
                    await MessageDialog.ShowInfoAsync(this, AppName, string.Format(ReferenceStrings.CannotOpen, path));
                    return false;
                }

                string? answer = await MessageDialog.ShowAsync(this, AppName, string.Format(ReferenceStrings.CannotFindCreate, path), "Yes", "No", "Cancel");
                if (answer == "Cancel" || answer is null)
                {
                    _closeConfirmed = true;
                    Close();
                    return false;
                }

                if (answer != "Yes")
                {
                    return false;
                }

                File.WriteAllBytes(path, []);
            }

            ShowDocument(_document.Load(path, forced));

            if (_document.IsLogFile)
            {
                // Part of what was opened, not an edit to undo; the document is dirty, as in the reference.
                Editor.Document.Insert(Editor.Document.TextLength, _document.LogStamp(TimeDateStamp.Format(DateTime.Now)));
                Editor.Document.UndoStack.ClearAll();
                Editor.CaretOffset = Editor.Document.TextLength;
            }

            return true;
        }
        catch (FileTooLargeException)
        {
            await MessageDialog.ShowInfoAsync(this, AppName, string.Format(ReferenceStrings.FileTooLarge, path));
        }
        catch (UnauthorizedAccessException)
        {
            await MessageDialog.ShowInfoAsync(this, AppName, string.Format(ReferenceStrings.NoPermission, path));
        }
        catch (Exception e) when (e is IOException or ArgumentException or NotSupportedException)
        {
            await MessageDialog.ShowInfoAsync(this, AppName, string.Format(ReferenceStrings.CannotOpen, path));
        }

        return false;
    }

    /// <summary>Save: straight to the file, or Save As for an Untitled document. True when written.</summary>
    public async Task<bool> SaveAsync()
    {
        if (_document.FilePath is null)
        {
            return await SaveAsAsync();
        }

        return await WriteAsync(_document.FilePath, _document.Encoding);
    }

    public async Task<bool> SaveAsAsync()
    {
        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = ReferenceStrings.SaveAsTitle,
            SuggestedFileName = _document.FilePath is null ? "Untitled.txt" : _document.DisplayName,
            DefaultExtension = "txt",
            FileTypeChoices = FileTypes,
            ShowOverwritePrompt = true,
            SuggestedStartLocation = await SuggestedFolderAsync(),
        });
        if (file is null)
        {
            return false;
        }

        if (file.TryGetLocalPath() is not string path)
        {
            await MessageDialog.ShowInfoAsync(this, AppName, ReferenceStrings.NotValidFileName);
            return false;
        }

        return await WriteAsync(path, _document.Encoding);
    }

    private async Task<bool> WriteAsync(string path, TextEncodingKind encoding)
    {
        bool allowLossy = false;
        string text = Editor.Text;
        if (encoding == TextEncodingKind.Ansi && !TextCodec.CanEncodeAnsi(text))
        {
            string? answer = await MessageDialog.ShowAsync(this, AppName, string.Format(ReferenceStrings.AnsiLossWarning, path), "OK", "Cancel");
            if (answer != "OK")
            {
                return false;
            }

            allowLossy = true;
        }

        try
        {
            _document.Save(path, text, encoding, allowLossy);
            UpdateTitle();
            UpdateStatusBar();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            await MessageDialog.ShowInfoAsync(this, AppName, string.Format(ReferenceStrings.CannotCreate, path));
            return false;
        }

        await OfferMenuEntryAsync();
        return true;
    }

    // ------------------------------------------------------------------ menu entry (Linux)

    /// <summary>
    /// Asked once, after the first save, and answered either way for good (<c>menuEntry</c> in
    /// settings.json; never before the person has done something with the program). "Add it" writes
    /// the entry now, and startup keeps it pointing at Spartanpad wherever it moves. Closing the
    /// dialog without answering asks again next time, not in this session.
    /// </summary>
    internal async Task OfferMenuEntryAsync()
    {
        if (!App.OfferMenuEntry || _menuEntryOffered || !OperatingSystem.IsLinux() || _settings.MenuEntry is not null
            || Environment.ProcessPath is not string executable)
        {
            return;
        }

        _menuEntryOffered = true;
        string? answer = await MessageDialog.ShowAsync(this, AppName,
            $"Add Spartanpad to your desktop's menu?\n\nThe entry would start {executable} and show up under Utilities or Accessories. Nothing else changes, and you will not be asked again.",
            "Add it", "No thanks");
        if (answer is null)
        {
            return;
        }

        if (answer == "Add it")
        {
            try
            {
                DesktopEntry.Install(App.MenuEntryDataHome, executable, Icons.Embedded());
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                await MessageDialog.ShowInfoAsync(this, AppName, $"The menu entry could not be written ({e.Message}).");
                return;
            }

            SetStatusMessage("Spartanpad is in your menu.");
        }

        _settings.MenuEntry = answer == "Add it";
        SaveSettings();
    }

    private async Task<IStorageFolder?> SuggestedFolderAsync()
    {
        string? directory = _document.FilePath is null ? null : Path.GetDirectoryName(_document.FilePath);
        if (directory is null)
        {
            return null;
        }

        try
        {
            return await StorageProvider.TryGetFolderFromPathAsync(directory);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task PageSetupAsync()
    {
        bool changed = await new PageSetupDialog(_settings).ShowDialog<bool>(this);
        if (changed)
        {
            SaveSettings();
        }
    }

    private Task PrintAsync() => MessageDialog.ShowInfoAsync(this, AppName, ReferenceStrings.PrintingUnavailable);

    // ------------------------------------------------------------------ edit commands

    private MenuItem _ctxUndo = null!;
    private MenuItem _ctxCut = null!;
    private MenuItem _ctxCopy = null!;
    private MenuItem _ctxPaste = null!;
    private MenuItem _ctxDelete = null!;
    private MenuItem _ctxSearch = null!;

    /// <summary>
    /// The edit control's right-click menu: Undo, Cut, Copy, Paste, Delete, Select All, and the
    /// web search, with the same enable rules as the Edit menu. Built in code because controls
    /// inside a flyout are not in the XAML name scope.
    /// </summary>
    private void BuildContextMenu()
    {
        MenuItem Item(string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) =>
            {
                action();
                FocusEditor();
            };
            return item;
        }

        _ctxUndo = Item("_Undo", () => Editor.Undo());
        _ctxCut = Item("Cu_t", () => Editor.Cut());
        _ctxCopy = Item("_Copy", () => Editor.Copy());
        _ctxPaste = Item("_Paste", () => Editor.Paste());
        _ctxDelete = Item("De_lete", DeleteSelection);
        _ctxSearch = Item("_Search with Kagi...", SearchWithKagi);
        var flyout = new MenuFlyout();
        flyout.Items.Add(_ctxUndo);
        flyout.Items.Add(new Separator());
        flyout.Items.Add(_ctxCut);
        flyout.Items.Add(_ctxCopy);
        flyout.Items.Add(_ctxPaste);
        flyout.Items.Add(_ctxDelete);
        flyout.Items.Add(new Separator());
        flyout.Items.Add(Item("Select _All", () => Editor.SelectAll()));
        flyout.Items.Add(_ctxSearch);
        flyout.Opening += (_, _) => RefreshEditStates();
        Editor.ContextFlyout = flyout;
    }

    internal MenuFlyout ContextMenuForTests => (MenuFlyout)Editor.ContextFlyout!;

    private void OnEditMenuOpened(object? sender, RoutedEventArgs e) => RefreshEditStates();

    /// <summary>The Edit menu rules from the reference (§1), applied to both menus.</summary>
    internal void RefreshEditStates()
    {
        bool hasSelection = Editor.SelectionLength > 0;
        bool hasText = Editor.Document.TextLength > 0;
        CutItem.IsEnabled = _ctxCut.IsEnabled = hasSelection;
        CopyItem.IsEnabled = _ctxCopy.IsEnabled = hasSelection;
        DeleteItem.IsEnabled = _ctxDelete.IsEnabled = hasSelection;
        KagiItem.IsEnabled = _ctxSearch.IsEnabled = hasSelection;
        FindItem.IsEnabled = hasText;
        FindNextItem.IsEnabled = hasText;
        FindPreviousItem.IsEnabled = hasText;
        GoToItem.IsEnabled = !_settings.WordWrap;
        UndoItem.IsEnabled = _ctxUndo.IsEnabled = Editor.CanUndo;
        PasteItem.IsEnabled = _ctxPaste.IsEnabled = false;
        _ = UpdatePasteStateAsync();
    }

    private async Task UpdatePasteStateAsync()
    {
        bool enabled;
        try
        {
            string? text = Clipboard is null ? null : await Clipboard.TryGetTextAsync();
            enabled = !string.IsNullOrEmpty(text);
        }
        catch (Exception)
        {
            enabled = true;
        }

        PasteItem.IsEnabled = _ctxPaste.IsEnabled = enabled;
    }

    private void DeleteSelection()
    {
        if (Editor.SelectionLength > 0)
        {
            ReplaceSelection(string.Empty);
        }
    }

    private void InsertTimeDate()
    {
        ReplaceSelection(TimeDateStamp.Format(DateTime.Now));
        FocusEditor();
    }

    private void SearchWithKagi()
    {
        string selection = Editor.SelectedText;
        if (string.IsNullOrWhiteSpace(selection))
        {
            return;
        }

        OpenUrl(Links.KagiSearch + Uri.EscapeDataString(selection));
    }

    private void OpenUrl(string url)
    {
        _ = Launcher.LaunchUriAsync(new Uri(url));
    }

    public async Task GoToAsync()
    {
        if (_settings.WordWrap)
        {
            return;
        }

        int? target = await new GoToDialog(Editor.TextArea.Caret.Line, Editor.Document.LineCount).ShowDialog<int?>(this);
        if (target is int t)
        {
            GoToLine(t);
        }

        FocusEditor();
    }

    /// <summary>Moves the caret to the start of 1-based <paramref name="line"/> (reference <c>FUN_14000d100</c>).</summary>
    public void GoToLine(int line)
    {
        if (line < 1 || line > Editor.Document.LineCount)
        {
            return;
        }

        Editor.TextArea.ClearSelection();
        Editor.CaretOffset = Editor.Document.GetLineByNumber(line).Offset;
        Editor.TextArea.Caret.BringCaretToView();
        UpdateLineColumn();
    }

    // ------------------------------------------------------------------ find and replace

    private void ShowFind()
    {
        CloseReplaceDialog();
        if (_findDialog is not null)
        {
            _findDialog.Activate();
            return;
        }

        SeedSearchFromSelection();
        var dialog = new FindDialog(_settings.SearchString, _settings.MatchCase, _settings.WrapAround, _settings.SearchUp);
        dialog.FindNextRequested += (_, request) => _ = FindFromDialogAsync(request, forward: !request.SearchUp);
        dialog.Closed += (_, _) =>
        {
            RememberSearch(dialog.Current);
            if (ReferenceEquals(_findDialog, dialog))
            {
                _findDialog = null;
            }

            FocusEditor();
        };
        _findDialog = dialog;
        dialog.Show(this);
    }

    private void ShowReplace()
    {
        CloseFindDialog();
        if (_replaceDialog is not null)
        {
            _replaceDialog.Activate();
            return;
        }

        SeedSearchFromSelection();
        var dialog = new ReplaceDialog(_settings.SearchString, _settings.ReplaceString, _settings.MatchCase, _settings.WrapAround);
        dialog.FindNextRequested += (_, request) => _ = FindFromDialogAsync(request, forward: true);
        dialog.ReplaceRequested += (_, request) => _ = ReplaceOnceAsync(request);
        dialog.ReplaceAllRequested += (_, request) => ReplaceAll(request);
        dialog.Closed += (_, _) =>
        {
            RememberSearch(dialog.CurrentFind);
            _settings.ReplaceString = dialog.CurrentReplace.Replacement;
            if (ReferenceEquals(_replaceDialog, dialog))
            {
                _replaceDialog = null;
            }

            FocusEditor();
        };
        _replaceDialog = dialog;
        dialog.Show(this);
    }

    /// <summary>Opening Find closes Replace and vice versa, as the reference does (§2).</summary>
    private void CloseFindDialog()
    {
        FindDialog? dialog = _findDialog;
        _findDialog = null;
        if (dialog is not null)
        {
            RememberSearch(dialog.Current);
            dialog.Close();
        }
    }

    private void CloseReplaceDialog()
    {
        ReplaceDialog? dialog = _replaceDialog;
        _replaceDialog = null;
        if (dialog is not null)
        {
            RememberSearch(dialog.CurrentFind);
            _settings.ReplaceString = dialog.CurrentReplace.Replacement;
            dialog.Close();
        }
    }

    /// <summary>The reference seeds "Find what" with up to 127 characters of the selection.</summary>
    private void SeedSearchFromSelection()
    {
        string selection = Editor.SelectedText;
        if (!string.IsNullOrEmpty(selection))
        {
            _settings.SearchString = selection.Length > 127 ? selection[..127] : selection;
        }
    }

    private void RememberSearch(FindRequest request)
    {
        _settings.SearchString = request.Pattern;
        _settings.MatchCase = request.MatchCase;
        _settings.WrapAround = request.WrapAround;
        _settings.SearchUp = request.SearchUp;
    }

    private async Task FindFromDialogAsync(FindRequest request, bool forward)
    {
        RememberSearch(request);
        await FindNextAsync(forward);
    }

    /// <summary>F3 / Shift+F3 and the dialogs' Find Next. Opens Find when there is nothing to search for.</summary>
    public async Task<bool> FindNextAsync(bool forward)
    {
        if (string.IsNullOrEmpty(_settings.SearchString))
        {
            ShowFind();
            return false;
        }

        string text = Editor.Text;
        int selStart = Editor.SelectionStart;
        int selEnd = selStart + Editor.SelectionLength;
        SearchMatch? match = TextSearch.Find(text, _settings.SearchString, selStart, selEnd, forward, _settings.MatchCase, _settings.WrapAround);
        if (match is SearchMatch m)
        {
            Select(m.Index, m.Length);
            SetStatusMessage(m.Wrapped ? (forward ? ReferenceStrings.FoundFromTop : ReferenceStrings.FoundFromBottom) : string.Empty);
            return true;
        }

        Window owner = (Window?)_findDialog ?? (Window?)_replaceDialog ?? this;
        await MessageDialog.ShowInfoAsync(owner, AppName, string.Format(ReferenceStrings.CannotFind, _settings.SearchString));
        return false;
    }

    private void Select(int index, int length)
    {
        Editor.Select(index, length);
        Editor.CaretOffset = index + length;
        Editor.TextArea.Caret.BringCaretToView();
        UpdateLineColumn();
    }

    private async Task ReplaceOnceAsync(ReplaceRequest request)
    {
        RememberSearch(request.Find);
        _settings.ReplaceString = request.Replacement;
        string text = Editor.Text;
        int selStart = Editor.SelectionStart;
        int selEnd = selStart + Editor.SelectionLength;
        if (TextSearch.SelectionMatches(text, request.Find.Pattern, selStart, selEnd, request.Find.MatchCase))
        {
            ReplaceSelection(request.Replacement);
            Select(selStart, request.Replacement.Length);
        }

        await FindNextAsync(forward: true);
    }

    private void ReplaceAll(ReplaceRequest request)
    {
        RememberSearch(request.Find);
        _settings.ReplaceString = request.Replacement;
        (string replaced, int count) = TextSearch.ReplaceAll(Editor.Text, request.Find.Pattern, request.Replacement, request.Find.MatchCase);
        if (count > 0)
        {
            // One replace of the whole text: one undo step, with the history before it intact.
            Editor.Document.Replace(0, Editor.Document.TextLength, replaced);
            Editor.CaretOffset = 0;
            Editor.TextArea.ClearSelection();
        }

        SetStatusMessage(string.Empty);
        UpdateStatusBar();
    }

    // ------------------------------------------------------------------ format and view

    private async Task FontAsync()
    {
        var current = new FontChoice(_settings.FontFamily, _settings.PointSizeTenths, _settings.FontWeight >= 600, _settings.FontItalic);
        FontChoice? choice = await new FontDialog(current).ShowDialog<FontChoice?>(this);
        if (choice is null)
        {
            return;
        }

        _settings.FontFamily = choice.Family;
        _settings.PointSizeTenths = choice.PointSizeTenths;
        _settings.FontWeight = choice.Bold ? 700 : 400;
        _settings.FontItalic = choice.Italic;
        ApplyFont();
    }

    public void ToggleWordWrap()
    {
        _settings.WordWrap = !_settings.WordWrap;
        ApplyWordWrap(_settings.WordWrap);
    }

    public void ToggleStatusBar()
    {
        _settings.StatusBar = !_settings.StatusBar;
        ApplyStatusBarVisibility(_settings.StatusBar);
    }

    // ------------------------------------------------------------------ menu handlers

    private void OnNew(object? sender, RoutedEventArgs e) => _ = NewAsync();

    private void OnNewWindow(object? sender, RoutedEventArgs e) => NewWindow();

    private void OnOpen(object? sender, RoutedEventArgs e) => _ = OpenAsync();

    private void OnSave(object? sender, RoutedEventArgs e) => _ = SaveAsync();

    private void OnSaveAs(object? sender, RoutedEventArgs e) => _ = SaveAsAsync();

    private void OnPageSetup(object? sender, RoutedEventArgs e) => _ = PageSetupAsync();

    private void OnPrint(object? sender, RoutedEventArgs e) => _ = PrintAsync();

    private void OnExit(object? sender, RoutedEventArgs e) => Close();

    // The edit commands run with the selection the editor kept while the menu had the focus,
    // then hand the focus back to the editor.
    private void OnUndo(object? sender, RoutedEventArgs e) => WithEditor(() => Editor.Undo());

    private void OnCut(object? sender, RoutedEventArgs e) => WithEditor(Editor.Cut);

    private void OnCopy(object? sender, RoutedEventArgs e) => WithEditor(Editor.Copy);

    private void OnPaste(object? sender, RoutedEventArgs e) => WithEditor(Editor.Paste);

    private void OnDelete(object? sender, RoutedEventArgs e) => WithEditor(DeleteSelection);

    private void WithEditor(Action action)
    {
        action();
        FocusEditor();
    }

    private void OnSearchWithKagi(object? sender, RoutedEventArgs e) => SearchWithKagi();

    private void OnFind(object? sender, RoutedEventArgs e) => ShowFind();

    private void OnFindNext(object? sender, RoutedEventArgs e) => _ = FindNextAsync(forward: true);

    private void OnFindPrevious(object? sender, RoutedEventArgs e) => _ = FindNextAsync(forward: false);

    private void OnReplace(object? sender, RoutedEventArgs e) => ShowReplace();

    private void OnGoTo(object? sender, RoutedEventArgs e) => _ = GoToAsync();

    private void OnSelectAll(object? sender, RoutedEventArgs e) => WithEditor(Editor.SelectAll);

    private void OnTimeDate(object? sender, RoutedEventArgs e) => InsertTimeDate();

    private void OnWordWrap(object? sender, RoutedEventArgs e) => ToggleWordWrap();

    private void OnFont(object? sender, RoutedEventArgs e) => _ = FontAsync();

    private void OnZoomIn(object? sender, RoutedEventArgs e) => SetZoom(Zoom.In(_zoomPercent));

    private void OnZoomOut(object? sender, RoutedEventArgs e) => SetZoom(Zoom.Out(_zoomPercent));

    private void OnZoomReset(object? sender, RoutedEventArgs e) => SetZoom(Zoom.Default);

    private void OnStatusBar(object? sender, RoutedEventArgs e) => ToggleStatusBar();

    private void OnViewHelp(object? sender, RoutedEventArgs e) => OpenUrl(Links.Help);

    private void OnSendFeedback(object? sender, RoutedEventArgs e) => OpenUrl(Links.Feedback);

    private void OnAbout(object? sender, RoutedEventArgs e) => _ = new AboutDialog().ShowDialog(this);
}
