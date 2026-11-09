using Avalonia;
using Spartanpad.Core;

namespace Spartanpad;

internal static class Program
{
    /// <summary>
    /// <c>SPARTANPAD_RENDERING=software</c> turns off GPU rendering (ANGLE on Windows, GL on
    /// Linux). For machines and virtual displays whose GL stack cannot be trusted, Windows
    /// Sandbox without a vGPU among them.
    /// </summary>
    private const string RenderingVariable = "SPARTANPAD_RENDERING";

    // Avalonia configuration, also used by the IDE previewer. Nothing here may touch Avalonia
    // types before the builder has run.
    [STAThread]
    public static int Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
    {
        bool software = string.Equals(Environment.GetEnvironmentVariable(RenderingVariable), "software", StringComparison.OrdinalIgnoreCase);

        // The X11 class is the menu entry's name, so a desktop pairs the window with
        // spartanpad.desktop (and, on Wayland, shows the entry's icon on it).
        var x11 = new X11PlatformOptions { WmClass = DesktopEntry.AppId };
        if (software)
        {
            x11.RenderingMode = [X11RenderingMode.Software];
        }

        AppBuilder builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .With(x11);

        if (software)
        {
            builder = builder.With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] });
        }

        return builder;
    }
}
