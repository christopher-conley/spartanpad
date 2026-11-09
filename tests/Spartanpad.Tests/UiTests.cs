using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Spartanpad;
using Spartanpad.Core;
using Spartanpad.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Spartanpad.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        // The menu entry offer is a dialog after the first save; the tests that want it turn it on.
        App.OfferMenuEntry = false;
        return AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}

/// <summary>The window driven headlessly: no display, real controls.</summary>
public class MainWindowTests
{
    private static (MainWindow Window, TempDir Dir) Open(Settings? settings = null, params string[] args)
    {
        var dir = new TempDir();
        var window = new MainWindow(settings ?? new Settings(), dir.File("settings.json"), CommandLine.Parse(args));
        window.Show();
        // The editor's text area joins the tree when its template is applied, in the layout pass.
        Dispatcher.UIThread.RunJobs();
        window.Editor.TextArea.Focus();
        return (window, dir);
    }

    /// <summary>
    /// The Linux menu entry is offered once, after the first save, and the answer is kept. Driven
    /// through the real dialog: the offer is awaited inside the save.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("Add it", true)]
    [InlineData("No thanks", false)]
    public async Task TheMenuEntryIsOfferedOnceAfterTheFirstSaveAndTheAnswerIsKept(string button, bool wanted)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        (MainWindow window, TempDir dir) = Open();
        using (dir)
        {
            App.OfferMenuEntry = true;
            App.MenuEntryDataHome = dir.File("data");
            try
            {
                string path = dir.File("note.txt");
                File.WriteAllText(path, "seed");
                Assert.True(await window.OpenPathAsync(path, null, createIfMissing: false));
                window.KeyTextInput("!");

                Task<bool> save = window.SaveAsync();
                Dispatcher.UIThread.RunJobs();
                var offer = Assert.Single(window.OwnedWindows.OfType<Spartanpad.Views.MessageDialog>());
                Assert.Contains("Add Spartanpad to your desktop's menu?", offer.MessageText.Text);
                // The file is on disk before the question is asked, whatever the answer turns out to be.
                Assert.Equal("!seed", File.ReadAllText(path));
                Assert.False(window.Document.IsDirty);
                Button choice = offer.Buttons.Children.OfType<Button>().Single(b => (string)b.Content! == button);
                choice.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.True(await save);
                Assert.Equal("!seed", File.ReadAllText(path));

                string entry = dir.File(Path.Combine("data", "applications", "spartanpad.desktop"));
                Assert.Equal(wanted, File.Exists(entry));
                Assert.Equal(wanted, File.Exists(dir.File(Path.Combine("data", "icons", "hicolor", "256x256", "apps", "spartanpad.png"))));
                if (wanted)
                {
                    Assert.Equal(DesktopEntry.Text(Environment.ProcessPath!), File.ReadAllText(entry));
                    Assert.Equal("Spartanpad is in your menu.", window.StatusMessage.Text);
                }

                // Kept at once, not at exit, and not asked a second time.
                Assert.Equal(wanted, SettingsStore.Load(dir.File("settings.json")).MenuEntry);
                Task<bool> again = window.SaveAsync();
                Dispatcher.UIThread.RunJobs();
                Assert.Empty(window.OwnedWindows);
                Assert.True(await again);
            }
            finally
            {
                App.OfferMenuEntry = false;
                App.MenuEntryDataHome = DesktopEntry.DefaultDataHome;
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void TheWindowCarriesTheIcon()
    {
        (MainWindow window, TempDir dir) = Open();
        using (dir)
        {
            Assert.NotNull(window.Icon);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void StartsUntitledWithTheReferenceStatusBar()
    {
        (MainWindow window, TempDir dir) = Open();
        using (dir)
        {
            Assert.Equal("Untitled - Spartanpad", window.Title);
            Assert.Equal(" Ln 1, Col 1", window.StatusLineColumn.Text);
            Assert.Equal(" 100%", window.StatusZoom.Text);
            Assert.Equal(" Windows (CRLF)", window.LineEndingButton.Content);
            Assert.Equal("UTF-8", window.EncodingButton.Content);
            Assert.True(window.StatusBar.IsVisible);
            Assert.False(window.Editor.WordWrap);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TypingMarksTheDocumentDirtyAndTracksTheCaret()
    {
        (MainWindow window, TempDir dir) = Open();
        using (dir)
        {
            window.KeyTextInput("hello");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyTextInput("ab");
            // Tab is a character here, not a move to the next control, and one column.
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            window.KeyTextInput("c");

            Assert.Equal("hello\r\nab\tc", window.Editor.Text);
            Assert.True(window.Document.IsDirty);
            Assert.Equal("*Untitled - Spartanpad", window.Title);
            Assert.Equal(" Ln 2, Col 5", window.StatusLineColumn.Text);
            window.Close();
        }
    }

    /// <summary>Column selection: Alt+Shift+Down grows a zero-width box over two lines, and typing lands on both.</summary>
    [AvaloniaFact]
    public void ColumnSelectionTypesOnEveryLine()
    {
        (MainWindow window, TempDir dir) = Open();
        using (dir)
        {
            window.KeyTextInput("ab");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyTextInput("cd");
            window.Editor.CaretOffset = 0;
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.Alt | RawInputModifiers.Shift);
            Assert.IsType<AvaloniaEdit.Editing.RectangleSelection>(window.Editor.TextArea.Selection);
            window.KeyTextInput("x");
            Assert.Equal("xab\r\nxcd", window.Editor.Text);
            window.Close();
        }
    }

    /// <summary>
    /// A box selected with Alt and the mouse: releasing Alt afterwards must not hand the keyboard to the
    /// menu bar (Avalonia opens the menu on an Alt release unless another key was pressed meanwhile, and
    /// a mouse press does not count).
    /// </summary>
    [AvaloniaFact]
    public void ReleasingAltAfterAMouseBoxSelectionKeepsTheKeyboardInTheEditor()
    {
        (MainWindow window, TempDir dir) = Open();
        using (dir)
        {
            window.KeyTextInput("ab");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyTextInput("cd");
            Point origin = window.Editor.TextArea.TextView.TranslatePoint(new Point(0, 0), window)!.Value;
            double lineHeight = window.Editor.TextArea.TextView.DefaultLineHeight;
            Point top = origin + new Vector(1, lineHeight / 2);
            Point bottom = origin + new Vector(1, lineHeight * 1.5);

            window.KeyPressQwerty(PhysicalKey.AltLeft, RawInputModifiers.Alt);
            window.MouseDown(top, MouseButton.Left, RawInputModifiers.Alt);
            window.MouseMove(bottom, RawInputModifiers.Alt | RawInputModifiers.LeftMouseButton);
            window.MouseUp(bottom, MouseButton.Left, RawInputModifiers.Alt);
            Assert.IsType<AvaloniaEdit.Editing.RectangleSelection>(window.Editor.TextArea.Selection);
            window.KeyReleaseQwerty(PhysicalKey.AltLeft, RawInputModifiers.None);

            Assert.False(window.MainMenu.IsOpen);
            Assert.True(window.Editor.TextArea.IsFocused);
            window.KeyTextInput("x");
            Assert.Equal("xab\r\nxcd", window.Editor.Text);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task OpensAFileAndShowsItsEncodingAndLineEnding()
    {
        (MainWindow window, TempDir dir) = Open();
        using (dir)
        {
            string path = dir.File("a.txt");
            File.WriteAllBytes(path, TextCodec.Encode("one\ntwo\nthree", TextEncodingKind.Utf16LE));

            Assert.True(await window.OpenPathAsync(path, null, createIfMissing: false));
            Assert.Equal("one\ntwo\nthree", window.Editor.Text);
            Assert.Equal("a.txt - Spartanpad", window.Title);
            Assert.Equal("UTF-16 LE", window.EncodingButton.Content);
            Assert.Equal(" Unix (LF)", window.LineEndingButton.Content);
            Assert.False(window.Editor.CanUndo);

            window.GoToLine(3);
            Assert.Equal(8, window.Editor.CaretOffset);
            Assert.Equal(" Ln 3, Col 1", window.StatusLineColumn.Text);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task LogFilesGetAStampOnOpen()
    {
        (MainWindow window, TempDir dir) = Open();
        using (dir)
        {
            string path = dir.File("j.log");
            File.WriteAllText(path, ".LOG\r\nfirst entry\r\n");
            Assert.True(await window.OpenPathAsync(path, null, createIfMissing: false));
            Assert.StartsWith(".LOG\r\nfirst entry\r\n\r\n", window.Editor.Text);
            Assert.EndsWith("\r\n", window.Editor.Text);
            Assert.True(window.Document.IsDirty);
            Assert.Equal(window.Editor.Text.Length, window.Editor.CaretOffset);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task FindNextSelectsMatchesAndReportsWrapAround()
    {
        var settings = new Settings { SearchString = "the", WrapAround = true };
        (MainWindow window, TempDir dir) = Open(settings);
        using (dir)
        {
            window.KeyTextInput("the cat and the dog");
            window.Editor.CaretOffset = 0;
            window.Editor.TextArea.ClearSelection();

            Assert.True(await window.FindNextAsync(forward: true));
            Assert.Equal(0, window.Editor.SelectionStart);
            Assert.Equal(3, window.Editor.SelectionLength);
            Assert.Equal(string.Empty, window.StatusMessage.Text);

            Assert.True(await window.FindNextAsync(forward: true));
            Assert.Equal(12, window.Editor.SelectionStart);

            Assert.True(await window.FindNextAsync(forward: true));
            Assert.Equal(0, window.Editor.SelectionStart);
            Assert.Equal(" Found next from the top", window.StatusMessage.Text);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CtrlFOpensFindAndCtrlHSwapsItForReplace()
    {
        (MainWindow window, TempDir dir) = Open();
        using (dir)
        {
            window.KeyTextInput("abc");
            window.KeyPressQwerty(PhysicalKey.F, RawInputModifiers.Control);
            Assert.NotNull(window.FindDialogForTests);
            Assert.Null(window.ReplaceDialogForTests);

            // The Find window took the keyboard focus; the user clicks back into the editor.
            window.Editor.TextArea.Focus();
            window.KeyPressQwerty(PhysicalKey.H, RawInputModifiers.Control);
            Assert.Null(window.FindDialogForTests);
            Assert.NotNull(window.ReplaceDialogForTests);
            window.ReplaceDialogForTests!.Close();
            Assert.Null(window.ReplaceDialogForTests);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ZoomWordWrapAndStatusBarToggle()
    {
        (MainWindow window, TempDir dir) = Open();
        using (dir)
        {
            double size = window.Editor.FontSize;
            window.SetZoom(Zoom.In(window.ZoomPercent));
            Assert.Equal(110, window.ZoomPercent);
            Assert.Equal(" 110%", window.StatusZoom.Text);
            Assert.True(window.Editor.FontSize > size);
            window.SetZoom(Zoom.Default);
            Assert.Equal(size, window.Editor.FontSize);

            window.ToggleWordWrap();
            Assert.True(window.Editor.WordWrap);
            Assert.True(window.Settings.WordWrap);
            Assert.True(window.WordWrapItem.IsChecked);

            window.ToggleStatusBar();
            Assert.False(window.StatusBar.IsVisible);
            Assert.False(window.Settings.StatusBar);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task SaveWritesTheDocumentEncodingAndSettingsPersistOnClose()
    {
        (MainWindow window, TempDir dir) = Open();
        using (dir)
        {
            string path = dir.File("out.txt");
            File.WriteAllText(path, "seed");
            Assert.True(await window.OpenPathAsync(path, null, createIfMissing: false));
            window.Editor.CaretOffset = 4;
            window.KeyTextInput(" more");
            Assert.True(window.Document.IsDirty);

            Assert.True(await window.SaveAsync());
            Assert.False(window.Document.IsDirty);
            Assert.Equal("seed more", File.ReadAllText(path));
            Assert.Equal("out.txt - Spartanpad", window.Title);

            window.ToggleWordWrap();
            window.SaveSettings();
            Settings saved = SettingsStore.Load(dir.File("settings.json"));
            Assert.True(saved.WordWrap);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ReplaceAllKeepsTheDocumentsLineEnding()
    {
        (MainWindow window, TempDir dir) = Open();
        using (dir)
        {
            string path = dir.File("lf.txt");
            File.WriteAllText(path, "cat\ncat\ndog\n");
            Assert.True(await window.OpenPathAsync(path, null, createIfMissing: false));
            Assert.Equal(" Unix (LF)", window.LineEndingButton.Content);

            window.Settings.SearchString = "cat";
            window.KeyPressQwerty(PhysicalKey.H, RawInputModifiers.Control);
            Assert.NotNull(window.ReplaceDialogForTests);
            Assert.Equal("cat", window.ReplaceDialogForTests!.FindWhat.Text);
            window.ReplaceDialogForTests.ReplaceWith.Text = "bird";
            window.ReplaceDialogForTests.RaiseReplaceAllForTests();

            Assert.Equal("bird", window.Settings.ReplaceString);
            Assert.Equal("bird\nbird\ndog\n", window.Editor.Text);
            Assert.Equal("*lf.txt - Spartanpad", window.Title);
            Assert.True(window.Editor.CanUndo);
            Assert.Equal(" Unix (LF)", window.LineEndingButton.Content);
            Assert.True(window.Document.IsDirty);
            window.ReplaceDialogForTests.Close();
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(25_000)]
    [InlineData(100_000)]
    [InlineData(500_000)]
    public async Task OpensALargeFileInSeconds(int lines)
    {
        (MainWindow window, TempDir dir) = Open();
        using (dir)
        {
            string path = dir.File("big.txt");
            var line = new string('x', 79) + "\n";
            using (StreamWriter w = File.CreateText(path))
            {
                for (int i = 0; i < lines; i++)
                {
                    w.Write(line);
                }
            }

            long bytes = new FileInfo(path).Length;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Assert.True(await window.OpenPathAsync(path, null, createIfMissing: false));
            watch.Stop();
            Assert.Equal(lines + 1, window.Editor.Document.LineCount);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"{bytes} bytes took {watch.Elapsed}");

            window.GoToLine(lines / 2);
            Assert.Equal($" Ln {lines / 2}, Col 1", window.StatusLineColumn.Text);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task LineEndingSelectorConvertsTheDocument()
    {
        (MainWindow window, TempDir dir) = Open();
        using (dir)
        {
            string path = dir.File("crlf.txt");
            File.WriteAllText(path, "one\r\ntwo\r\n");
            Assert.True(await window.OpenPathAsync(path, null, createIfMissing: false));
            Assert.Equal(" Windows (CRLF)", window.LineEndingButton.Content);
            Assert.False(window.Document.IsDirty);

            window.ChangeLineEnding(LineEnding.Lf);
            Assert.Equal("one\ntwo\n", window.Editor.Text);
            Assert.Equal(LineEnding.Lf, window.Document.LineEnding);
            Assert.Equal(" Unix (LF)", window.LineEndingButton.Content);
            Assert.True(window.Document.IsDirty);
            Assert.Equal("*crlf.txt - Spartanpad", window.Title);
            Assert.True(window.Editor.CanUndo);

            // Enter now inserts the chosen ending.
            window.Editor.CaretOffset = window.Editor.Text.Length;
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Assert.Equal("one\ntwo\n\n", window.Editor.Text);

            // A document with no line breaks still switches and is dirty.
            window.ChangeLineEnding(LineEnding.Cr);
            Assert.Equal("one\rtwo\r\r", window.Editor.Text);
            Assert.True(await window.SaveAsync());
            Assert.Equal("one\rtwo\r\r", File.ReadAllText(path));
            Assert.False(window.Document.IsDirty);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectionSurvivesTheMenuTakingFocusAndTheContextMenuFollowsIt()
    {
        (MainWindow window, TempDir dir) = Open();
        using (dir)
        {
            window.KeyTextInput("hello world");
            window.Editor.Select(0, 5);
            Assert.Equal("hello", window.Editor.SelectedText);

            // Clicking the menu bar moves the focus off the editor, as a menu click does.
            window.EncodingButton.Focus();
            Assert.False(window.Editor.TextArea.IsFocused);
            Assert.Equal("hello", window.Editor.SelectedText);

            window.RefreshEditStates();
            Assert.True(window.CutItem.IsEnabled);
            Assert.True(window.CopyItem.IsEnabled);
            Assert.True(window.DeleteItem.IsEnabled);
            Assert.Equal(9, window.ContextMenuForTests.Items.Count);
            var contextCut = (MenuItem)window.ContextMenuForTests.Items[2]!;
            Assert.Equal("Cu_t", contextCut.Header);
            Assert.True(contextCut.IsEnabled);

            window.Editor.TextArea.ClearSelection();
            window.RefreshEditStates();
            Assert.False(window.CutItem.IsEnabled);
            Assert.False(contextCut.IsEnabled);
            Assert.True(((MenuItem)window.ContextMenuForTests.Items[0]!).IsEnabled); // Undo
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TimeDateInsertsAtTheCaret()
    {
        (MainWindow window, TempDir dir) = Open();
        using (dir)
        {
            window.KeyTextInput("ab");
            window.Editor.CaretOffset = 1;
            window.KeyPressQwerty(PhysicalKey.F5, RawInputModifiers.None);
            string text = window.Editor.Text!;
            Assert.StartsWith("a", text);
            Assert.EndsWith("b", text);
            Assert.True(text.Length > 8);
            window.Close();
        }
    }
}
