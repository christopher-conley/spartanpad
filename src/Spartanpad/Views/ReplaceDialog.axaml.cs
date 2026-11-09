using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Spartanpad.Views;

public sealed record ReplaceRequest(FindRequest Find, string Replacement);

/// <summary>
/// The modeless Replace window (reference dialog 1541 as shown: Find what, Replace with, Match
/// case, Wrap around, Find Next, Replace, Replace All, Cancel). Replace always searches down.
/// </summary>
public partial class ReplaceDialog : Window
{
    public event EventHandler<FindRequest>? FindNextRequested;
    public event EventHandler<ReplaceRequest>? ReplaceRequested;
    public event EventHandler<ReplaceRequest>? ReplaceAllRequested;

    public ReplaceDialog()
    {
        InitializeComponent();
    }

    public ReplaceDialog(string pattern, string replacement, bool matchCase, bool wrapAround)
        : this()
    {
        FindWhat.Text = pattern;
        ReplaceWith.Text = replacement;
        MatchCase.IsChecked = matchCase;
        WrapAround.IsChecked = wrapAround;
        UpdateButtons();
    }

    public FindRequest CurrentFind => new(FindWhat.Text ?? string.Empty, MatchCase.IsChecked == true, WrapAround.IsChecked == true, SearchUp: false);

    public ReplaceRequest CurrentReplace => new(CurrentFind, ReplaceWith.Text ?? string.Empty);

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        FindWhat.Focus();
        FindWhat.SelectAll();
    }

    private void OnFindWhatChanged(object? sender, TextChangedEventArgs e) => UpdateButtons();

    private void UpdateButtons()
    {
        bool enabled = !string.IsNullOrEmpty(FindWhat.Text);
        FindNextButton.IsEnabled = enabled;
        ReplaceButton.IsEnabled = enabled;
        ReplaceAllButton.IsEnabled = enabled;
    }

    private void OnFindNext(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(FindWhat.Text))
        {
            FindNextRequested?.Invoke(this, CurrentFind);
        }
    }

    private void OnReplace(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(FindWhat.Text))
        {
            ReplaceRequested?.Invoke(this, CurrentReplace);
        }
    }

    private void OnReplaceAll(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(FindWhat.Text))
        {
            ReplaceAllRequested?.Invoke(this, CurrentReplace);
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    /// <summary>The Replace All button's action, for the headless tests.</summary>
    internal void RaiseReplaceAllForTests() => OnReplaceAll(this, new RoutedEventArgs());
}
