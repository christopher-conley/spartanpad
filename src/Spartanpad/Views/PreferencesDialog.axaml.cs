using Avalonia.Controls;
using Avalonia.Interactivity;
using Spartanpad.Core;

namespace Spartanpad.Views;

/// <summary>
/// The settings that are Spartanpad's own and have no place in the reference's menus (Edit,
/// Preferences). Writes them to the <see cref="Settings"/> on OK and returns true when something
/// changed; the window applies and saves them.
/// </summary>
public partial class PreferencesDialog : Window
{
    private readonly Settings _settings;

    public PreferencesDialog()
    {
        InitializeComponent();
        _settings = new Settings();
    }

    public PreferencesDialog(Settings settings)
        : this()
    {
        _settings = settings;
        Tabs.IsChecked = settings.Tabs;
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        bool tabs = Tabs.IsChecked == true;
        bool changed = tabs != _settings.Tabs;
        _settings.Tabs = tabs;
        Close(changed);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
