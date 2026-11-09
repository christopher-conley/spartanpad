using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Spartanpad.Views;

/// <summary>The font the user picked: family, size in tenths of a point (the reference's unit), style.</summary>
public sealed record FontChoice(string Family, int PointSizeTenths, bool Bold, bool Italic);

/// <summary>
/// Stands in for <c>ChooseFontW</c>, which Avalonia has no equivalent of: installed families,
/// four styles, the usual sizes, and a sample.
/// </summary>
public partial class FontDialog : Window
{
    private static readonly int[] StandardSizes = [8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72];

    private bool _updatingSize;

    public FontDialog()
    {
        InitializeComponent();
    }

    public FontDialog(FontChoice current)
        : this()
    {
        List<string> names = FontManager.Current.SystemFonts
            .Select(f => f.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        if (!names.Contains(current.Family, StringComparer.OrdinalIgnoreCase))
        {
            names.Insert(0, current.Family);
        }

        Families.ItemsSource = names;
        Families.SelectedItem = names.First(n => string.Equals(n, current.Family, StringComparison.OrdinalIgnoreCase));
        StyleList.SelectedIndex = (current.Bold ? 2 : 0) + (current.Italic ? 1 : 0);
        Sizes.ItemsSource = StandardSizes;
        _updatingSize = true;
        SizeText.Text = FormatPoints(current.PointSizeTenths);
        _updatingSize = false;
        if (current.PointSizeTenths % 10 == 0 && StandardSizes.Contains(current.PointSizeTenths / 10))
        {
            Sizes.SelectedItem = current.PointSizeTenths / 10;
        }

        UpdateSample();
    }

    private static string FormatPoints(int tenths) => tenths % 10 == 0
        ? (tenths / 10).ToString(CultureInfo.CurrentCulture)
        : (tenths / 10.0).ToString("0.#", CultureInfo.CurrentCulture);

    private int? ParsePointSizeTenths()
    {
        string raw = (SizeText.Text ?? string.Empty).Trim();
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out double points)
            || double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out points))
        {
            if (points >= 1 && points <= 999)
            {
                return (int)Math.Round(points * 10);
            }
        }

        return null;
    }

    public FontChoice? Current
    {
        get
        {
            if (Families.SelectedItem is not string family || ParsePointSizeTenths() is not int tenths)
            {
                return null;
            }

            int style = Math.Max(0, StyleList.SelectedIndex);
            return new FontChoice(family, tenths, Bold: style >= 2, Italic: style % 2 == 1);
        }
    }

    private void OnChoiceChanged(object? sender, SelectionChangedEventArgs e) => UpdateSample();

    private void OnSizeSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (Sizes.SelectedItem is int size)
        {
            _updatingSize = true;
            SizeText.Text = size.ToString(CultureInfo.CurrentCulture);
            _updatingSize = false;
            UpdateSample();
        }
    }

    private void OnSizeTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updatingSize)
        {
            return;
        }

        int? tenths = ParsePointSizeTenths();
        _updatingSize = true;
        Sizes.SelectedItem = tenths is int t && t % 10 == 0 && StandardSizes.Contains(t / 10) ? t / 10 : null;
        _updatingSize = false;
        UpdateSample();
    }

    private void UpdateSample()
    {
        if (Sample is null || Current is not FontChoice choice)
        {
            return;
        }

        Sample.FontFamily = new FontFamily(choice.Family);
        Sample.FontSize = choice.PointSizeTenths / 10.0 * 96 / 72;
        Sample.FontWeight = choice.Bold ? FontWeight.Bold : FontWeight.Normal;
        Sample.FontStyle = choice.Italic ? FontStyle.Italic : FontStyle.Normal;
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (Current is FontChoice choice)
        {
            Close(choice);
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
