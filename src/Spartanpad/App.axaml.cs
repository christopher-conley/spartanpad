using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Spartanpad.Core;

namespace Spartanpad;

public partial class App : Application
{
    /// <summary>Where settings are read and written; tests point this somewhere disposable.</summary>
    public static string SettingsPath { get; set; } = SettingsStore.DefaultPath;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Settings settings = SettingsStore.Load(SettingsPath);
            CommandLine commandLine = CommandLine.Parse(desktop.Args ?? []);
            desktop.MainWindow = new MainWindow(settings, SettingsPath, commandLine);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
