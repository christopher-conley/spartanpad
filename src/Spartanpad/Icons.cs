using System.Reflection;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace Spartanpad;

/// <summary>
/// Spartanpad's icon, embedded as a PNG at each size a Linux icon theme takes (<c>icons/&lt;size&gt;.png</c>,
/// from <c>res/</c>). The window carries the 256 one on every platform; the Windows executable's own
/// icon resource is <c>res/spartanpad.ico</c>, set in the project file.
/// </summary>
internal static class Icons
{
    /// <summary>The icon's bytes by size in pixels.</summary>
    internal static IReadOnlyDictionary<int, byte[]> Embedded()
    {
        Assembly assembly = typeof(Icons).Assembly;
        var icons = new Dictionary<int, byte[]>();
        foreach (string name in assembly.GetManifestResourceNames())
        {
            if (name.StartsWith("icons/", StringComparison.Ordinal) && name.EndsWith(".png", StringComparison.Ordinal)
                && int.TryParse(name.AsSpan(6, name.Length - 10), out int size))
            {
                using Stream stream = assembly.GetManifestResourceStream(name)!;
                using var bytes = new MemoryStream();
                stream.CopyTo(bytes);
                icons[size] = bytes.ToArray();
            }
        }

        return icons;
    }

    /// <summary>The icon for a window's title bar and taskbar entry.</summary>
    internal static WindowIcon Window() => new(new MemoryStream(Embedded()[256]));

    /// <summary>The icon as a picture, for the About dialog.</summary>
    internal static Bitmap Picture() => new(new MemoryStream(Embedded()[256]));
}
