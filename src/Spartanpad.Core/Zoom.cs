namespace Spartanpad.Core;

/// <summary>View &gt; Zoom: 10% steps between 10% and 500%, Ctrl+0 restores 100% (§5).</summary>
public static class Zoom
{
    public const int Minimum = 10;
    public const int Maximum = 500;
    public const int Default = 100;
    public const int Step = 10;

    public static int In(int percent) => Math.Min(Maximum, Clamp(percent) + Step);

    public static int Out(int percent) => Math.Max(Minimum, Clamp(percent) - Step);

    public static int Clamp(int percent) => Math.Clamp(percent, Minimum, Maximum);

    /// <summary>Reference string 56, leading space included.</summary>
    public static string StatusText(int percent) => $" {percent}%";
}
