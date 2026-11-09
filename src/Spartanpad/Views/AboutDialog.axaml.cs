using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Spartanpad.Views;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
        string version = typeof(AboutDialog).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(AboutDialog).Assembly.GetName().Version?.ToString()
            ?? "unknown";
        VersionText.Text = $"Version {version}";
    }

    private async void OnProjectLink(object? sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri(Links.Project));
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close();
}
