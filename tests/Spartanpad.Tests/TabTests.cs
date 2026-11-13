using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spartanpad;
using Spartanpad.Core;
using Spartanpad.Views;

namespace Spartanpad.Tests;

/// <summary>
/// Tabbed documents, driven headlessly. Tabs are an option (Edit, Preferences) and off by default;
/// the tests that turn them on say so.
/// </summary>
public class TabTests
{
    private static (MainWindow Window, TempDir Dir) Open(bool tabs)
    {
        var dir = new TempDir();
        var window = new MainWindow(new Settings { Tabs = tabs }, dir.File("settings.json"), CommandLine.Parse([]));
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Editor.TextArea.Focus();
        return (window, dir);
    }

    /// <summary>Presses a button of the one message the window is showing.</summary>
    private static MessageDialog Answer(MainWindow window, string button)
    {
        Dispatcher.UIThread.RunJobs();
        MessageDialog dialog = Assert.Single(window.OwnedWindows.OfType<MessageDialog>());
        Button choice = dialog.Buttons.Children.OfType<Button>().Single(b => (string)b.Content! == button);
        choice.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        return dialog;
    }

    private static void AssertNoMessage(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(window.OwnedWindows.OfType<MessageDialog>());
    }

    /// <summary>Opens Edit, Preferences through its menu item, sets the checkbox and presses a button.</summary>
    private static void SetTabsInPreferences(MainWindow window, bool tabs, string button = "OK")
    {
        window.PreferencesItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        PreferencesDialog dialog = Assert.Single(window.OwnedWindows.OfType<PreferencesDialog>());
        Assert.Equal(window.Settings.Tabs, dialog.Tabs.IsChecked);
        dialog.Tabs.IsChecked = tabs;
        Button choice = dialog.GetLogicalDescendants().OfType<Button>().Single(b => (string)b.Content! == button);
        choice.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(window.OwnedWindows.OfType<PreferencesDialog>());
    }

    private static string[] Captions(MainWindow window) => window.Tabs.Select(t => t.HeaderText!.Text!).ToArray();

    private static Vector ScrollOffset(MainWindow window) => ((ILogicalScrollable)window.Editor.TextArea.TextView).Offset;

    [AvaloniaFact]
    public async Task TabsAreOffByDefaultAndTheWindowHoldsOneDocument()
    {
        var dir = new TempDir();
        using (dir)
        {
            var window = new MainWindow(new Settings(), dir.File("settings.json"), CommandLine.Parse([]));
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.False(window.Settings.Tabs);
            Assert.False(window.TabStripBar.IsVisible);
            Assert.Single(window.Tabs);

            string path = dir.File("a.txt");
            File.WriteAllText(path, "alpha");
            Assert.True(await window.OpenChosenAsync(window.ActiveTab, path, null));
            await window.NewAsync();
            Assert.Single(window.Tabs);
            Assert.Equal("Untitled - Spartanpad", window.Title);
            Assert.Equal(string.Empty, window.Editor.Text);
            Assert.False(window.TabStripBar.IsVisible);

            // Preferences is the last entry of Edit, after everything the reference has there.
            var edit = (MenuItem)window.PreferencesItem.Parent!;
            Assert.Equal("_Edit", edit.Header);
            Assert.Same(window.PreferencesItem, edit.Items[^1]);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PreferencesTurnsTabsOnAtOnceAndSavesTheAnswer()
    {
        (MainWindow window, TempDir dir) = Open(tabs: false);
        using (dir)
        {
            SetTabsInPreferences(window, true, "Cancel");
            Assert.False(window.Settings.Tabs);
            Assert.False(window.TabStripBar.IsVisible);
            Assert.False(File.Exists(dir.File("settings.json")));

            SetTabsInPreferences(window, true);
            Assert.True(window.Settings.Tabs);
            Assert.True(window.TabStripBar.IsVisible);
            Assert.True(SettingsStore.Load(dir.File("settings.json")).Tabs);
            Assert.Equal(["Untitled"], Captions(window));

            // The keyboard is back in the editor, and New now makes a tab.
            window.KeyTextInput("one");
            Assert.Equal("one", window.Editor.Text);
            window.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.Control);
            AssertNoMessage(window);
            Assert.Equal(["*Untitled", "Untitled"], Captions(window));
            Assert.Equal(string.Empty, window.Editor.Text);
        }
    }

    [AvaloniaFact]
    public async Task EachTabKeepsItsOwnTextUndoCaretSelectionAndScroll()
    {
        (MainWindow window, TempDir dir) = Open(tabs: true);
        using (dir)
        {
            string path = dir.File("long.txt");
            string original = string.Join("\r\n", Enumerable.Range(1, 2000).Select(i => $"line {i}"));
            File.WriteAllText(path, original);
            Assert.True(await window.OpenChosenAsync(window.ActiveTab, path, null));
            DocumentTab first = Assert.Single(window.Tabs);

            int lineStart = window.Editor.Document.GetLineByNumber(1000).Offset;
            window.Editor.CaretOffset = lineStart;
            window.KeyTextInput("X");
            window.Editor.Select(lineStart, 4);
            window.Editor.CaretOffset = lineStart + 4;
            ((ILogicalScrollable)window.Editor.TextArea.TextView).Offset = new Vector(0, 5000);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new Vector(0, 5000), ScrollOffset(window));
            Assert.Equal("*long.txt - Spartanpad", window.Title);

            await window.NewAsync();
            DocumentTab second = window.ActiveTab;
            Assert.Equal(2, window.Tabs.Count);
            Assert.NotSame(first, second);
            Assert.Same(second.Header, window.TabStrip.SelectedItem);
            Assert.Equal("Untitled - Spartanpad", window.Title);
            Assert.Equal(string.Empty, window.Editor.Text);
            Assert.False(window.Editor.CanUndo);
            Assert.Equal(0, window.Editor.CaretOffset);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(default, ScrollOffset(window));
            window.KeyTextInput("bee");
            Assert.Equal(["*long.txt", "*Untitled"], Captions(window));
            Assert.Equal("Ln 1, Col 4", window.StatusLineColumn.Text!.Trim());

            window.ActivateTab(first);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(first.Header, window.TabStrip.SelectedItem);
            Assert.Equal("*long.txt - Spartanpad", window.Title);
            Assert.Equal(lineStart, window.Editor.SelectionStart);
            Assert.Equal(4, window.Editor.SelectionLength);
            Assert.Equal(lineStart + 4, window.Editor.CaretOffset);
            Assert.Equal(new Vector(0, 5000), ScrollOffset(window));
            Assert.Equal("Ln 1000, Col 5", window.StatusLineColumn.Text!.Trim());
            Assert.Equal(LineEnding.Crlf.DisplayName(), window.LineEndingButton.Content);
            Assert.True(window.Editor.CanUndo);
            window.Editor.Undo();
            Assert.Equal(original, window.Editor.Text);
            Assert.False(window.Editor.CanUndo);

            window.ActivateTab(second);
            Assert.Equal("bee", window.Editor.Text);
            Assert.Equal(3, window.Editor.CaretOffset);
            Assert.True(window.Editor.CanUndo);
            // Typing goes to the tab on screen, and only to it.
            window.KeyTextInput("s");
            Assert.Equal("bees", second.Text.Text);
            Assert.Equal(original, first.Text.Text);
        }
    }

    [AvaloniaFact]
    public async Task OpenUsesAnUntouchedTabThenANewOneAndShowsAFileThatIsAlreadyOpen()
    {
        (MainWindow window, TempDir dir) = Open(tabs: true);
        using (dir)
        {
            string a = dir.File("a.txt");
            string b = dir.File("b.txt");
            File.WriteAllText(a, "alpha");
            File.WriteAllText(b, "beta");

            Assert.True(await window.OpenChosenAsync(window.ActiveTab, a, null));
            Assert.Equal(["a.txt"], Captions(window));

            Assert.True(await window.OpenChosenAsync(window.ActiveTab, b, null));
            Assert.Equal(["a.txt", "b.txt"], Captions(window));
            Assert.Equal("beta", window.Editor.Text);
            Assert.Equal("b.txt - Spartanpad", window.Title);

            // Typed in and emptied again is not untouched: there is an undo history to keep.
            await window.NewAsync();
            window.KeyTextInput("x");
            window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
            Assert.Equal(string.Empty, window.Editor.Text);
            Assert.True(await window.OpenChosenAsync(window.ActiveTab, a, null));
            Assert.Equal(["a.txt", "b.txt", "Untitled"], Captions(window));
            Assert.Same(window.Tabs[0], window.ActiveTab);
            Assert.Equal("alpha", window.Editor.Text);

            // A file that cannot be opened leaves no tab behind.
            Task<bool> missing = window.OpenChosenAsync(window.ActiveTab, dir.File("none.txt"), null);
            Answer(window, "OK");
            Assert.False(await missing);
            Assert.Equal(["a.txt", "b.txt", "Untitled"], Captions(window));
            Assert.Same(window.Tabs[0], window.ActiveTab);
        }
    }

    [AvaloniaFact]
    public async Task ALogFileOpenedIntoANewTabIsStampedThere()
    {
        (MainWindow window, TempDir dir) = Open(tabs: true);
        using (dir)
        {
            string log = dir.File("notes.txt");
            File.WriteAllText(log, ".LOG\r\nearlier");
            window.KeyTextInput("mine");

            Assert.True(await window.OpenChosenAsync(window.ActiveTab, log, null));
            Assert.Equal(["*Untitled", "*notes.txt"], Captions(window));
            Assert.StartsWith(".LOG\r\nearlier\r\n", window.Editor.Text);
            Assert.True(window.Editor.Text.Length > ".LOG\r\nearlier\r\n\r\n".Length);
            Assert.Equal(window.Editor.Text.Length, window.Editor.CaretOffset);
            Assert.False(window.Editor.CanUndo);
            Assert.Equal("mine", window.Tabs[0].Text.Text);
        }
    }

    [AvaloniaFact]
    public async Task CtrlWClosesATabAfterTheSavePromptAndTheLastTabClosesTheWindow()
    {
        (MainWindow window, TempDir dir) = Open(tabs: true);
        using (dir)
        {
            bool closed = false;
            window.Closed += (_, _) => closed = true;
            await window.NewAsync();
            await window.NewAsync();
            Assert.Equal(3, window.Tabs.Count);
            DocumentTab first = window.Tabs[0];
            DocumentTab second = window.Tabs[1];

            // A clean tab goes without a question; the one that takes its place is the one before
            // it when it was the last.
            window.KeyPressQwerty(PhysicalKey.W, RawInputModifiers.Control);
            AssertNoMessage(window);
            Assert.Equal(2, window.Tabs.Count);
            Assert.Same(second, window.ActiveTab);

            window.KeyTextInput("unsaved");
            window.KeyPressQwerty(PhysicalKey.W, RawInputModifiers.Control);
            Answer(window, ReferenceStrings.CancelButton);
            Assert.Equal(2, window.Tabs.Count);
            Assert.Equal("unsaved", window.Editor.Text);

            // The close button of a tab that is not on screen: it is shown while it is asked about.
            window.ActivateTab(first);
            ((Button)((Panel)second.Header!.Content!).Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Same(second, window.ActiveTab);
            MessageDialog prompt = Answer(window, ReferenceStrings.DontSaveButton);
            Assert.Contains("Untitled", prompt.MessageText.Text);
            Assert.Same(first, Assert.Single(window.Tabs));
            Assert.Same(first, window.ActiveTab);
            Assert.True(window.TabStripBar.IsVisible);
            Assert.False(closed);

            window.Editor.TextArea.Focus();
            window.KeyPressQwerty(PhysicalKey.W, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            Assert.True(closed);
        }
    }

    [AvaloniaFact]
    public async Task ClosingTheWindowAsksAboutEveryUnsavedTabAndCancelKeepsItOpen()
    {
        (MainWindow window, TempDir dir) = Open(tabs: true);
        using (dir)
        {
            bool closed = false;
            window.Closed += (_, _) => closed = true;
            string path = dir.File("a.txt");
            File.WriteAllText(path, "alpha");
            Assert.True(await window.OpenChosenAsync(window.ActiveTab, path, null));
            window.KeyTextInput("!");
            await window.NewAsync();
            await window.NewAsync();
            window.KeyTextInput("scratch");
            DocumentTab file = window.Tabs[0];
            DocumentTab clean = window.Tabs[1];
            DocumentTab scratch = window.Tabs[2];
            window.ActivateTab(clean);

            window.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.Same(file, window.ActiveTab);
            MessageDialog prompt = Answer(window, ReferenceStrings.DontSaveButton);
            Assert.Contains(path, prompt.MessageText.Text);
            Assert.Same(scratch, window.ActiveTab);
            prompt = Answer(window, ReferenceStrings.CancelButton);
            Assert.Contains("Untitled", prompt.MessageText.Text);
            AssertNoMessage(window);
            Assert.False(closed);
            Assert.Equal(3, window.Tabs.Count);
            Assert.Equal("alpha", File.ReadAllText(path));
            Assert.True(file.Document.IsDirty);

            window.Close();
            Answer(window, ReferenceStrings.SaveButton);
            Assert.Equal("!alpha", File.ReadAllText(path));
            Answer(window, ReferenceStrings.DontSaveButton);
            Assert.True(closed);
        }
    }

    /// <summary>
    /// A save holds on to the tab it was started for. The dialog in the middle here is the ANSI
    /// warning; on a desktop it can as well be a file picker that leaves the window usable.
    /// </summary>
    [AvaloniaFact]
    public async Task ASaveWritesTheTabItWasStartedForWhateverTheEditorShowsByThen()
    {
        (MainWindow window, TempDir dir) = Open(tabs: true);
        using (dir)
        {
            string path = dir.File("a.txt");
            File.WriteAllText(path, "alpha");
            Assert.True(await window.OpenChosenAsync(window.ActiveTab, path, null));
            DocumentTab first = window.ActiveTab;
            window.Editor.CaretOffset = 5;
            window.KeyTextInput(" ✓");
            first.Document.Encoding = TextEncodingKind.Ansi;

            await window.NewAsync();
            DocumentTab second = window.ActiveTab;
            window.KeyTextInput("other");
            window.ActivateTab(first);

            Task<bool> save = window.SaveAsync(first);
            Dispatcher.UIThread.RunJobs();
            Assert.Single(window.OwnedWindows.OfType<MessageDialog>());
            window.ActivateTab(second);
            Answer(window, "OK");
            Assert.True(await save);

            Assert.Equal("alpha ?", File.ReadAllText(path));
            Assert.False(first.Document.IsDirty);
            Assert.Equal(path, first.Document.FilePath);
            Assert.True(second.Document.IsDirty);
            Assert.Null(second.Document.FilePath);
            Assert.Equal(["a.txt", "*Untitled"], Captions(window));
            Assert.Equal("*Untitled - Spartanpad", window.Title);
        }
    }

    [AvaloniaFact]
    public async Task TurningTabsOffKeepsTheOpenTabsAndMakesNoNewOnes()
    {
        (MainWindow window, TempDir dir) = Open(tabs: true);
        using (dir)
        {
            window.KeyTextInput("kept");
            await window.NewAsync();
            Assert.Equal(2, window.Tabs.Count);

            SetTabsInPreferences(window, false);
            AssertNoMessage(window);
            Assert.False(window.Settings.Tabs);
            Assert.False(SettingsStore.Load(dir.File("settings.json")).Tabs);
            Assert.Equal(2, window.Tabs.Count);
            Assert.True(window.TabStripBar.IsVisible);
            Assert.Equal("kept", window.Tabs[0].Text.Text);

            // New and Open are the reference's again: they replace what is on screen.
            await window.NewAsync();
            Assert.Equal(2, window.Tabs.Count);
            string path = dir.File("a.txt");
            File.WriteAllText(path, "alpha");
            Assert.True(await window.OpenChosenAsync(window.ActiveTab, path, null));
            Assert.Equal(["*Untitled", "a.txt"], Captions(window));

            // The tabs still switch and close; with one left, the strip goes.
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Control);
            Assert.Same(window.Tabs[0], window.ActiveTab);
            await window.CloseTabAsync(window.Tabs[1]);
            Assert.Single(window.Tabs);
            Assert.False(window.TabStripBar.IsVisible);
            Assert.Equal("kept", window.Editor.Text);
        }
    }

    private static Point CenterOf(MainWindow window, Control control) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

    /// <summary>A point on the strip to the right of the last tab, level with it.</summary>
    private static Point BesideTheTabs(MainWindow window)
    {
        Point last = CenterOf(window, window.Tabs[^1].Header!);
        var point = new Point(window.TabStripBar.Bounds.Width - 30, last.Y);
        Assert.True(point.X > last.X + window.Tabs[^1].Header!.Bounds.Width);
        return point;
    }

    private static void DoubleClick(MainWindow window, Point point)
    {
        for (int i = 0; i < 2; i++)
        {
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static void MiddleClick(MainWindow window, Point point)
    {
        window.MouseDown(point, MouseButton.Middle);
        window.MouseUp(point, MouseButton.Middle);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task ADoubleClickBesideTheTabsMakesANewOneAndOnATabDoesNot()
    {
        (MainWindow window, TempDir dir) = Open(tabs: true);
        using (dir)
        {
            window.KeyTextInput("first");
            DoubleClick(window, BesideTheTabs(window));
            Assert.Equal(["*Untitled", "Untitled"], Captions(window));
            Assert.Same(window.Tabs[1], window.ActiveTab);
            // The keyboard is in the new tab's editor.
            window.KeyTextInput("second");
            Assert.Equal("second", window.Tabs[1].Text.Text);
            Assert.Equal("first", window.Tabs[0].Text.Text);

            // On a tab it is that tab being clicked, twice.
            DoubleClick(window, CenterOf(window, window.Tabs[0].HeaderText!));
            AssertNoMessage(window);
            Assert.Equal(2, window.Tabs.Count);
            Assert.Same(window.Tabs[0], window.ActiveTab);

            // One click beside the tabs is not two.
            Point beside = BesideTheTabs(window);
            window.MouseDown(beside, MouseButton.Left);
            window.MouseUp(beside, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, window.Tabs.Count);

            // With tabs turned off the strip still shows what is open, and makes nothing new.
            await Task.Delay(700);
            SetTabsInPreferences(window, false);
            Assert.True(window.TabStripBar.IsVisible);
            DoubleClick(window, BesideTheTabs(window));
            Assert.Equal(2, window.Tabs.Count);
        }
    }

    [AvaloniaFact]
    public async Task AMiddleClickOnATabClosesThatTabAfterTheSavePrompt()
    {
        (MainWindow window, TempDir dir) = Open(tabs: true);
        using (dir)
        {
            bool closed = false;
            window.Closed += (_, _) => closed = true;
            window.KeyTextInput("unsaved");
            await window.NewAsync();
            await window.NewAsync();
            Dispatcher.UIThread.RunJobs();
            DocumentTab first = window.Tabs[0];
            DocumentTab second = window.Tabs[1];
            DocumentTab third = window.Tabs[2];

            // The left button on a tab shows it and closes nothing; the keyboard goes to its text.
            Point onSecond = CenterOf(window, second.HeaderText!);
            window.MouseDown(onSecond, MouseButton.Left);
            window.MouseUp(onSecond, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            AssertNoMessage(window);
            Assert.Equal(3, window.Tabs.Count);
            Assert.Same(second, window.ActiveTab);
            window.KeyTextInput("x");
            window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
            Assert.Equal(string.Empty, second.Text.Text);
            Assert.False(second.Document.IsDirty);
            window.ActivateTab(third);

            // A clean tab that is not on screen goes, and the one on screen stays where it is.
            MiddleClick(window, CenterOf(window, second.HeaderText!));
            AssertNoMessage(window);
            Assert.Equal([first, third], window.Tabs);
            Assert.Same(third, window.ActiveTab);

            // Beside the tabs the middle button does nothing.
            MiddleClick(window, BesideTheTabs(window));
            Assert.Equal(2, window.Tabs.Count);

            // A tab with unsaved changes is shown and asked about; Cancel keeps it.
            MiddleClick(window, CenterOf(window, first.HeaderText!));
            Assert.Same(first, window.ActiveTab);
            Answer(window, ReferenceStrings.CancelButton);
            Assert.Equal([first, third], window.Tabs);
            MiddleClick(window, CenterOf(window, first.HeaderText!));
            Answer(window, ReferenceStrings.DontSaveButton);
            Assert.Same(third, Assert.Single(window.Tabs));
            Assert.False(closed);

            // The last tab is the window. The press is what closes, so there is no window left to
            // take the release.
            window.MouseDown(CenterOf(window, third.HeaderText!), MouseButton.Middle);
            Dispatcher.UIThread.RunJobs();
            Assert.True(closed);
        }
    }

    [AvaloniaFact]
    public async Task CtrlTabWalksTheTabsAndAClickedTabTakesTheEditor()
    {
        (MainWindow window, TempDir dir) = Open(tabs: true);
        using (dir)
        {
            await window.NewAsync();
            await window.NewAsync();
            Assert.Same(window.Tabs[2], window.ActiveTab);

            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Control);
            Assert.Same(window.Tabs[0], window.ActiveTab);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Control);
            Assert.Same(window.Tabs[1], window.ActiveTab);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Control | RawInputModifiers.Shift);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Control | RawInputModifiers.Shift);
            Assert.Same(window.Tabs[2], window.ActiveTab);
            // None of that typed a tab character or moved the keyboard out of the editor.
            window.KeyTextInput("c");
            Assert.Equal("c", window.Tabs[2].Text.Text);
            Assert.Equal(string.Empty, window.Tabs[0].Text.Text);

            // What a click on a header does: the strip selects it, and the window follows.
            window.TabStrip.SelectedItem = window.Tabs[0].Header;
            Dispatcher.UIThread.RunJobs();
            Assert.Same(window.Tabs[0], window.ActiveTab);
            window.KeyTextInput("a");
            Assert.Equal("a", window.Tabs[0].Text.Text);
            Assert.Equal("c", window.Tabs[2].Text.Text);
        }
    }
}
