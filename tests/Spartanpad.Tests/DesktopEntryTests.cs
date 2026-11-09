using Spartanpad.Core;

namespace Spartanpad.Tests;

/// <summary>The Linux menu entry and the icons it ships with; the writing tests skip themselves on Windows.</summary>
public class DesktopEntryTests
{
    private static readonly IReadOnlyDictionary<int, byte[]> Icons = new Dictionary<int, byte[]> { [256] = [1, 2, 3], [512] = [4, 5, 6] };

    [Fact]
    public void TheEntryNamesTheExecutableTheIconAndTheWindowClass()
    {
        Assert.Equal(
            "[Desktop Entry]\nType=Application\nName=Spartanpad\nGenericName=Text Editor\n"
            + "Comment=The classic Notepad, rebuilt for Windows Sandbox and Linux\n"
            + "Exec=/home/someone/Apps/Spartanpad %f\nIcon=spartanpad\nTerminal=false\n"
            + "Categories=Utility;TextEditor;\nMimeType=text/plain;\nStartupWMClass=spartanpad\nStartupNotify=true\n",
            DesktopEntry.Text("/home/someone/Apps/Spartanpad"));
    }

    [Theory]
    [InlineData("/opt/My Tools/Spartanpad", "\"/opt/My Tools/Spartanpad\"")]
    [InlineData("/home/a\"b/Spartanpad", "\"/home/a\\\"b/Spartanpad\"")]
    [InlineData("/home/$USER/Spartanpad", "\"/home/\\$USER/Spartanpad\"")]
    [InlineData("/home/100%/Spartanpad", "\"/home/100%%/Spartanpad\"")]
    public void APathWithASpaceOrAReservedCharacterIsQuotedForExec(string executable, string exec)
    {
        Assert.Contains($"\nExec={exec} %f\n", DesktopEntry.Text(executable));
    }

    [Fact]
    public void InstallWritesTheEntryAndTheIconsOnceAndAgainWhenTheExecutableMoves()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var dir = new TempDir();
        string entry = Path.Combine(dir.Path, "applications", "spartanpad.desktop");

        Assert.Equal(entry, DesktopEntry.Install(dir.Path, "/opt/sp/Spartanpad", Icons));
        Assert.Equal(DesktopEntry.Text("/opt/sp/Spartanpad"), File.ReadAllText(entry));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(dir.Path, "icons", "hicolor", "256x256", "apps", "spartanpad.png")));
        Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(Path.Combine(dir.Path, "icons", "hicolor", "512x512", "apps", "spartanpad.png")));

        // The same executable again: nothing to write.
        Assert.Null(DesktopEntry.Install(dir.Path, "/opt/sp/Spartanpad", Icons));

        // Moved: the entry follows.
        Assert.Equal(entry, DesktopEntry.Install(dir.Path, "/home/someone/Spartanpad", Icons));
        Assert.Equal(DesktopEntry.Text("/home/someone/Spartanpad"), File.ReadAllText(entry));

        // An icon the person replaced is put back.
        string icon = Path.Combine(dir.Path, "icons", "hicolor", "256x256", "apps", "spartanpad.png");
        File.WriteAllBytes(icon, [9]);
        Assert.Null(DesktopEntry.Install(dir.Path, "/home/someone/Spartanpad", Icons));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(icon));
    }

    [Fact]
    public void NothingIsWrittenWithoutAnExecutablePath()
    {
        using var dir = new TempDir();
        Assert.Null(DesktopEntry.Install(dir.Path, "", Icons));
        Assert.False(Directory.Exists(Path.Combine(dir.Path, "applications")));
    }

    [Fact]
    public void TheEmbeddedIconsAreThe512And256Pngs()
    {
        IReadOnlyDictionary<int, byte[]> icons = Spartanpad.Icons.Embedded();
        Assert.Equal([256, 512], icons.Keys.Order());
        byte[] pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        foreach (byte[] png in icons.Values)
        {
            Assert.Equal(pngSignature, png.Take(8));
        }
    }
}
