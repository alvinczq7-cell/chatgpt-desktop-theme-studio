using System.Text.Json.Serialization;

namespace ThemeStudio.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BackgroundKind { Solid, Gradient, Image, Gif, Mp4, Webm }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BackgroundFit { Cover, Contain, Fill }

public sealed class ThemeDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New theme";
    public string Version { get; set; } = "1.0.0";
    public string MinimumStudioVersion { get; set; } = "0.1.0";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, ViewTheme> Views { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chat"] = ViewTheme.CreateDefault(),
        ["work"] = ViewTheme.CreateDefault(),
        ["codex"] = ViewTheme.CreateDefault()
    };
}

public sealed class ViewTheme
{
    public BackgroundDefinition Background { get; set; } = new();
    public Dictionary<string, SurfaceStyle> Surfaces { get; set; } = CreateSurfaces();

    public static ViewTheme CreateDefault() => new();

    private static Dictionary<string, SurfaceStyle> CreateSurfaces() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["sidebar"] = new(),
        ["titlebar"] = new(),
        ["content"] = new() { Opacity = 92 },
        ["composer"] = new() { Opacity = 96 },
        ["menu"] = new() { Opacity = 98 },
        ["cards"] = new() { Opacity = 90 },
        ["controls"] = new()
    };
}

public sealed class BackgroundDefinition
{
    public BackgroundKind Kind { get; set; } = BackgroundKind.Solid;
    public string AssetPath { get; set; } = "";
    public string Color { get; set; } = "#151923";
    public string GradientEndColor { get; set; } = "#343B58";
    public BackgroundFit Fit { get; set; } = BackgroundFit.Cover;
    public int PositionX { get; set; } = 50;
    public int PositionY { get; set; } = 50;
    public int DimPercent { get; set; } = 20;
    public int Blur { get; set; }
    public bool PausedByUser { get; set; }
}

public sealed class SurfaceStyle
{
    public string BackgroundColor { get; set; } = "#202532";
    public string ForegroundColor { get; set; } = "#F3F5FA";
    public int Opacity { get; set; } = 92;
    public int Blur { get; set; }
    public string FontFamily { get; set; } = "Segoe UI Variable Text";
    public double CornerRadius { get; set; } = 12;
    public string ControlState { get; set; } = "default";
}

public sealed class ThemePackageManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string MinimumStudioVersion { get; set; } = "0.1.0";
    public List<string> SupportedHostVersions { get; set; } = [];
    public Dictionary<string, string> Assets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
