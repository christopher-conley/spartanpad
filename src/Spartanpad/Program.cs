using Avalonia;

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
        AppBuilder builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

        if (string.Equals(Environment.GetEnvironmentVariable(RenderingVariable), "software", StringComparison.OrdinalIgnoreCase))
        {
            builder = builder
                .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] })
                .With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Software] });
        }

        return builder;
    }
}
