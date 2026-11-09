using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Spartanpad.Core;

namespace Spartanpad;

public partial class App : Application
{
    /// <summary>Where settings are read and written; tests point this somewhere disposable.</summary>
    public static string SettingsPath { get; set; } = SettingsStore.DefaultPath;

    /// <summary>
    /// Whether the window may offer the Linux menu entry after the first save; the headless tests
    /// turn it off, since the offer is a dialog.
    /// </summary>
    public static bool OfferMenuEntry { get; set; } = true;

    /// <summary>Where the Linux menu entry and its icons go; tests point this somewhere disposable.</summary>
    public static string MenuEntryDataHome { get; set; } = DesktopEntry.DefaultDataHome;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Settings settings = SettingsStore.Load(SettingsPath);
            KeepMenuEntryCurrent(settings);
            CommandLine commandLine = CommandLine.Parse(desktop.Args ?? []);
            desktop.MainWindow = new MainWindow(settings, SettingsPath, commandLine);
        }

        base.OnFrameworkInitializationCompleted();
    }

    // For a person who asked for the menu entry, it follows Spartanpad wherever it was unpacked; a
    // failure here is not worth stopping for.
    private static void KeepMenuEntryCurrent(Settings settings)
    {
        if (settings.MenuEntry != true || !OperatingSystem.IsLinux() || Environment.ProcessPath is not string executable)
        {
            return;
        }

        try
        {
            DesktopEntry.Install(MenuEntryDataHome, executable, Icons.Embedded());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
