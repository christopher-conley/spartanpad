using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Spartanpad.Core;

namespace Spartanpad.Views;

/// <summary>
/// The Go To Line dialog (reference dialog 14): pre-filled with the current line; a number that
/// is 0 or past the last line shows reference string 31 and resets the field to the line count.
/// Returns the 1-based line, or null on Cancel.
/// </summary>
public partial class GoToDialog : Window
{
    private readonly int _lineCount;

    public GoToDialog()
    {
        InitializeComponent();
        _lineCount = 1;
    }

    public GoToDialog(int currentLine, int lineCount)
        : this()
    {
        _lineCount = Math.Max(1, lineCount);
        LineNumber.Text = currentLine.ToString(CultureInfo.CurrentCulture);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        LineNumber.Focus();
        LineNumber.SelectAll();
    }

    private async void OnGoTo(object? sender, RoutedEventArgs e)
    {
        string raw = (LineNumber.Text ?? string.Empty).Trim();
        bool parsed = int.TryParse(raw, NumberStyles.Integer, CultureInfo.CurrentCulture, out int line)
            || int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out line);
        if (parsed && line >= 1 && line <= _lineCount)
        {
            Close(line);
            return;
        }

        await MessageDialog.ShowInfoAsync(this, ReferenceStrings.GoToTitle, ReferenceStrings.LineBeyondEnd);
        LineNumber.Text = _lineCount.ToString(CultureInfo.CurrentCulture);
        LineNumber.Focus();
        LineNumber.SelectAll();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
