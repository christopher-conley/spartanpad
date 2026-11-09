using System.Diagnostics;
using System.Text;

namespace Spartanpad.Core;

/// <summary>
/// Spartanpad's entry in a Linux desktop's menu (<c>spartanpad.desktop</c> in the user's applications
/// folder, under Accessories), pointing at the executable that is running: written when the person
/// says yes, and again whenever the executable has moved, so the menu finds it wherever they unpacked
/// it, with its icon in the user's icon theme. Nothing else is touched. Not for Windows.
/// </summary>
public static class DesktopEntry
{
    /// <summary>
    /// The entry's own name: the file is <c>spartanpad.desktop</c>, the icon is <c>spartanpad</c> in the
    /// theme, and the window's X11 class is the same so the desktop pairs the two (on Wayland that
    /// pairing is where the window icon comes from).
    /// </summary>
    public const string AppId = "spartanpad";

    public const string FileName = AppId + ".desktop";

    /// <summary>
    /// Where the menu and the icon theme look: <c>$XDG_DATA_HOME</c>, by default <c>~/.local/share</c>.
    /// </summary>
    public static string DefaultDataHome
    {
        get
        {
            string? xdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrEmpty(xdgData))
            {
                return xdgData;
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify), ".local", "share");
        }
    }

    /// <summary>
    /// Makes sure the menu entry is there and points at <paramref name="executable"/>: the file is
    /// written when it is missing or says anything else, and left alone when it already says this.
    /// The icons are written the same way. Returns the entry's path when it was written, null when
    /// nothing needed writing (on Windows always).
    /// </summary>
    /// <param name="dataHome">The XDG data folder (<see cref="DefaultDataHome"/>).</param>
    /// <param name="executable">Spartanpad's own path.</param>
    /// <param name="icons">The icon as a PNG at each size (pixels square), for the theme's <c>hicolor</c> folders.</param>
    /// <exception cref="IOException">The applications folder or a file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The applications folder or a file could not be written.</exception>
    public static string? Install(string dataHome, string executable, IReadOnlyDictionary<int, byte[]> icons)
    {
        if (OperatingSystem.IsWindows() || string.IsNullOrEmpty(executable))
        {
            return null;
        }

        foreach ((int size, byte[] png) in icons)
        {
            string file = Path.Combine(dataHome, "icons", "hicolor", $"{size}x{size}", "apps", AppId + ".png");
            if (!File.Exists(file) || !File.ReadAllBytes(file).AsSpan().SequenceEqual(png))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllBytes(file, png);
            }
        }

        string applications = Path.Combine(dataHome, "applications");
        string path = Path.Combine(applications, FileName);
        string text = Text(executable);
        if (File.Exists(path) && File.ReadAllText(path) == text)
        {
            return null;
        }

        Directory.CreateDirectory(applications);
        File.WriteAllText(path, text);
        RefreshMimeCache(applications);
        return path;
    }

    /// <summary>The desktop entry for <paramref name="executable"/>, in the freedesktop.org format.</summary>
    public static string Text(string executable) =>
        "[Desktop Entry]\n"
        + "Type=Application\n"
        + "Name=Spartanpad\n"
        + "GenericName=Text Editor\n"
        + "Comment=The classic Notepad, rebuilt for Windows Sandbox and Linux\n"
        + $"Exec={Quoted(executable)} %f\n"
        + $"Icon={AppId}\n"
        + "Terminal=false\n"
        + "Categories=Utility;TextEditor;\n"
        + "MimeType=text/plain;\n"
        + $"StartupWMClass={AppId}\n"
        + "StartupNotify=true\n";

    // "Open With" lists applications from the folder's mimeinfo.cache, which only
    // update-desktop-database rebuilds. Best effort: a desktop without it still gets the menu entry.
    private static void RefreshMimeCache(string applications)
    {
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo("update-desktop-database", applications)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            process?.WaitForExit(5000);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            // Not installed, or not runnable: the cache stays as it was.
        }
    }

    // An Exec value is split like a shell command: a path with a space or a reserved character goes in
    // double quotes, with the characters the spec reserves inside them escaped.
    private static string Quoted(string executable)
    {
        if (executable.All(c => !char.IsWhiteSpace(c) && "\"'\\`$<>~|&;*?#()%".IndexOf(c) < 0))
        {
            return executable;
        }

        var text = new StringBuilder("\"");
        foreach (char c in executable)
        {
            if (c is '"' or '\\' or '`' or '$')
            {
                text.Append('\\');
            }

            if (c == '%')
            {
                // A field code marker, which the spec says to double even inside quotes.
                text.Append('%');
            }

            text.Append(c);
        }

        return text.Append('"').ToString();
    }
}
