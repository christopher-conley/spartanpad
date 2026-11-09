using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Spartanpad.Core;

namespace Spartanpad.Views;

/// <summary>
/// The parts of Page Setup (reference dialog 12) that are the editor's own: orientation, margins,
/// header and footer. Paper size and source belong to a printer, which this build has none of.
/// Edits the <see cref="Settings"/> margins in their stored unit (thousandths of an inch, or
/// hundredths of a millimeter in metric regions) and returns true on OK.
/// </summary>
public partial class PageSetupDialog : Window
{
    private readonly Settings _settings;

    public PageSetupDialog()
    {
        InitializeComponent();
        _settings = new Settings();
    }

    public PageSetupDialog(Settings settings)
        : this()
    {
        _settings = settings;
        MarginsGroup.Header = settings.MetricMargins ? "Margins (millimeters)" : "Margins (inches)";
        Portrait.IsChecked = !settings.Landscape;
        Landscape.IsChecked = settings.Landscape;
        MarginLeft.Text = Format(settings.MarginLeft);
        MarginRight.Text = Format(settings.MarginRight);
        MarginTop.Text = Format(settings.MarginTop);
        MarginBottom.Text = Format(settings.MarginBottom);
        Header.Text = settings.Header;
        Footer.Text = settings.Footer;
    }

    private string Format(int stored) => _settings.MetricMargins
        ? (stored / 100.0).ToString("0.##", CultureInfo.CurrentCulture)
        : (stored / 1000.0).ToString("0.###", CultureInfo.CurrentCulture);

    private int? Parse(string? raw)
    {
        raw = (raw ?? string.Empty).Trim();
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out double value)
            && !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return null;
        }

        if (value < 0 || value > 100)
        {
            return null;
        }

        return (int)Math.Round(value * (_settings.MetricMargins ? 100 : 1000));
    }

    private async void OnOk(object? sender, RoutedEventArgs e)
    {
        int? left = Parse(MarginLeft.Text);
        int? right = Parse(MarginRight.Text);
        int? top = Parse(MarginTop.Text);
        int? bottom = Parse(MarginBottom.Text);
        if (left is null || right is null || top is null || bottom is null)
        {
            await MessageDialog.ShowInfoAsync(this, "Page Setup", "Margins must be numbers between 0 and 100.");
            return;
        }

        _settings.Landscape = Landscape.IsChecked == true;
        _settings.MarginLeft = left.Value;
        _settings.MarginRight = right.Value;
        _settings.MarginTop = top.Value;
        _settings.MarginBottom = bottom.Value;
        _settings.Header = Header.Text ?? string.Empty;
        _settings.Footer = Footer.Text ?? string.Empty;
        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
