using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spartanpad.Core;

/// <summary>
/// The user's settings, with the value names and defaults of the reference registry key
/// (the local reference notes §4) so a Notepad user recognizes them. Stored as JSON by
/// <see cref="SettingsStore"/>. An integer that is not a valid value falls back to the default
/// when read.
/// </summary>
public sealed class Settings
{
    /// <summary>The reference's unset marker for the window rectangle.</summary>
    public const int Unset = int.MinValue;

    [JsonPropertyName("lfFaceName")]
    public string FontFamily { get; set; } = "Lucida Console";

    /// <summary>Tenths of a point; 100 is 10 pt.</summary>
    [JsonPropertyName("iPointSize")]
    public int PointSizeTenths { get; set; } = 100;

    /// <summary>LOGFONT weight: 400 regular, 700 bold. The reference stores 0 for "unspecified".</summary>
    [JsonPropertyName("lfWeight")]
    public int FontWeight { get; set; }

    [JsonPropertyName("lfItalic")]
    public bool FontItalic { get; set; }

    [JsonPropertyName("fWrap")]
    public bool WordWrap { get; set; }

    [JsonPropertyName("StatusBar")]
    public bool StatusBar { get; set; } = true;

    [JsonPropertyName("iDefaultEncoding")]
    public int DefaultEncodingValue { get; set; } = (int)TextEncodingKind.Utf8;

    [JsonIgnore]
    public TextEncodingKind DefaultEncoding
    {
        get => TextEncodingKindExtensions.IsDefined(DefaultEncodingValue) ? (TextEncodingKind)DefaultEncodingValue : TextEncodingKind.Utf8;
        set => DefaultEncodingValue = (int)value;
    }

    /// <summary>Find direction: true searches up.</summary>
    [JsonPropertyName("fReverse")]
    public bool SearchUp { get; set; }

    [JsonPropertyName("fWrapAround")]
    public bool WrapAround { get; set; }

    [JsonPropertyName("fMatchCase")]
    public bool MatchCase { get; set; }

    [JsonPropertyName("searchString")]
    public string SearchString { get; set; } = string.Empty;

    [JsonPropertyName("replaceString")]
    public string ReplaceString { get; set; } = string.Empty;

    [JsonPropertyName("szHeader")]
    public string Header { get; set; } = HeaderFooter.DefaultHeader;

    [JsonPropertyName("szTrailer")]
    public string Footer { get; set; } = HeaderFooter.DefaultFooter;

    /// <summary>True when margins are hundredths of a millimeter; false for thousandths of an inch.</summary>
    [JsonPropertyName("fMetricMargins")]
    public bool MetricMargins { get; set; } = IsMetricRegion();

    [JsonPropertyName("iMarginTop")]
    public int MarginTop { get; set; } = IsMetricRegion() ? 2500 : 1000;

    [JsonPropertyName("iMarginBottom")]
    public int MarginBottom { get; set; } = IsMetricRegion() ? 2500 : 1000;

    [JsonPropertyName("iMarginLeft")]
    public int MarginLeft { get; set; } = IsMetricRegion() ? 2000 : 750;

    [JsonPropertyName("iMarginRight")]
    public int MarginRight { get; set; } = IsMetricRegion() ? 2000 : 750;

    [JsonPropertyName("fLandscape")]
    public bool Landscape { get; set; }

    [JsonPropertyName("iWindowPosX")]
    public int WindowX { get; set; } = Unset;

    [JsonPropertyName("iWindowPosY")]
    public int WindowY { get; set; } = Unset;

    [JsonPropertyName("iWindowPosDX")]
    public int WindowWidth { get; set; } = Unset;

    [JsonPropertyName("iWindowPosDY")]
    public int WindowHeight { get; set; } = Unset;

    [JsonIgnore]
    public bool HasWindowRect => WindowX != Unset && WindowY != Unset && WindowWidth > 0 && WindowHeight > 0;

    /// <summary>
    /// Whether the person wants Spartanpad in their desktop menu (Linux): true or false once answered,
    /// null until then. Not a reference setting. Asked after their first save, never before: a question
    /// about a program nobody has used yet has no answer.
    /// </summary>
    [JsonPropertyName("menuEntry")]
    public bool? MenuEntry { get; set; }

    /// <summary>
    /// Whether documents open in tabs. Not a reference setting, and off until it is turned on in
    /// Edit, Preferences: with it off the window holds one document at a time, as the reference does.
    /// </summary>
    [JsonPropertyName("tabs")]
    public bool Tabs { get; set; }

    /// <summary>The reference decides by <c>LOCALE_IMEASURE</c>; .NET exposes the same fact per region.</summary>
    public static bool IsMetricRegion()
    {
        try
        {
            return RegionInfo.CurrentRegion.IsMetric;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Pulls out-of-range values back to their defaults after a load.</summary>
    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(FontFamily))
        {
            FontFamily = "Lucida Console";
        }

        if (PointSizeTenths < 10 || PointSizeTenths > 10000)
        {
            PointSizeTenths = 100;
        }

        if (!TextEncodingKindExtensions.IsDefined(DefaultEncodingValue))
        {
            DefaultEncodingValue = (int)TextEncodingKind.Utf8;
        }

        if (WindowWidth != Unset && WindowWidth <= 0)
        {
            WindowWidth = Unset;
        }

        if (WindowHeight != Unset && WindowHeight <= 0)
        {
            WindowHeight = Unset;
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(Settings))]
public sealed partial class SettingsJsonContext : JsonSerializerContext
{
}

/// <summary>Reads and writes <see cref="Settings"/> as <c>settings.json</c> in the per-user config directory.</summary>
public static class SettingsStore
{
    public const string ApplicationFolder = "Spartanpad";
    public const string FileName = "settings.json";

    /// <summary>
    /// <c>%APPDATA%\Spartanpad\settings.json</c> on Windows, <c>$XDG_CONFIG_HOME/Spartanpad/settings.json</c>
    /// (usually <c>~/.config</c>) elsewhere.
    /// </summary>
    public static string DefaultPath
    {
        get
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify);
            if (string.IsNullOrEmpty(root))
            {
                root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            }

            return Path.Combine(root, ApplicationFolder, FileName);
        }
    }

    /// <summary>Loads the file, or returns defaults when it is missing or unreadable.</summary>
    public static Settings Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                using FileStream stream = File.OpenRead(path);
                Settings? loaded = JsonSerializer.Deserialize(stream, SettingsJsonContext.Default.Settings);
                if (loaded is not null)
                {
                    loaded.Normalize();
                    return loaded;
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // A damaged settings file is not worth refusing to start over; the defaults apply.
        }

        return new Settings();
    }

    public static void Save(string path, Settings settings)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tmp = path + ".tmp";
        using (FileStream stream = File.Create(tmp))
        {
            JsonSerializer.Serialize(stream, settings, SettingsJsonContext.Default.Settings);
        }

        File.Move(tmp, path, overwrite: true);
    }
}
