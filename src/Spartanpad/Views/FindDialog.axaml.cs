using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Spartanpad.Views;

/// <summary>What a Find or Replace window asks the editor to do.</summary>
public sealed record FindRequest(string Pattern, bool MatchCase, bool WrapAround, bool SearchUp);

/// <summary>
/// The modeless Find window (reference dialog 1540 as shown: Find what, Match case, Wrap around,
/// Up/Down, Find Next, Cancel; whole-word and Help are hidden by the reference's flags).
/// </summary>
public partial class FindDialog : Window
{
    public event EventHandler<FindRequest>? FindNextRequested;

    public FindDialog()
    {
        InitializeComponent();
    }

    public FindDialog(string pattern, bool matchCase, bool wrapAround, bool searchUp)
        : this()
    {
        FindWhat.Text = pattern;
        MatchCase.IsChecked = matchCase;
        WrapAround.IsChecked = wrapAround;
        DirectionUp.IsChecked = searchUp;
        DirectionDown.IsChecked = !searchUp;
        UpdateButtons();
    }

    public FindRequest Current => new(
        FindWhat.Text ?? string.Empty,
        MatchCase.IsChecked == true,
        WrapAround.IsChecked == true,
        DirectionUp.IsChecked == true);

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        FindWhat.Focus();
        FindWhat.SelectAll();
    }

    private void OnFindWhatChanged(object? sender, TextChangedEventArgs e) => UpdateButtons();

    private void UpdateButtons() => FindNextButton.IsEnabled = !string.IsNullOrEmpty(FindWhat.Text);

    private void OnFindNext(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(FindWhat.Text))
        {
            FindNextRequested?.Invoke(this, Current);
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
