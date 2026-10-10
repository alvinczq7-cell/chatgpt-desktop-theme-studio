using System.Text.Json;
using System.Text.Json.Serialization;
using ThemeStudio.Models;

namespace ThemeStudio.Services;

public sealed class ThemeLibrary
{
    private readonly string _root;
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public ThemeLibrary(string? root = null)
    {
        _root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThemeStudio", "Themes");
        Directory.CreateDirectory(_root);
    }

    public string Root => _root;

    public async Task<IReadOnlyList<ImportedTheme>> ListAsync(CancellationToken token = default)
    {
        var result = new List<ImportedTheme>();
        foreach (var directory in Directory.EnumerateDirectories(_root))
        {
            token.ThrowIfCancellationRequested();
            var file = Path.Combine(directory, ThemePackageService.ThemeName);
            if (!File.Exists(file)) continue;
            try
            {
                await using var stream = File.OpenRead(file);
                var theme = await JsonSerializer.DeserializeAsync<ThemeDocument>(stream, _json, token);
                if (theme is not null)
                {
                    ThemePackageService.ValidateTheme(theme);
                    if (!string.Equals(Path.GetFileName(directory), theme.Id, StringComparison.OrdinalIgnoreCase)) continue;
                    result.Add(new ImportedTheme(theme, directory));
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException or ArgumentException) { }
        }
        return result;
    }

    public async Task<ImportedTheme> SaveAsync(ThemeDocument theme, string? sourceRoot = null, CancellationToken token = default)
    {
        ThemePackageService.ValidateTheme(theme);
        theme.UpdatedAt = DateTimeOffset.UtcNow;
        var target = Path.Combine(_root, theme.Id);
        var sameRoot = sourceRoot is not null && string.Equals(Path.GetFullPath(sourceRoot), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase);
        if (sameRoot)
        {
            var temp = Path.Combine(target, $"{ThemePackageService.ThemeName}.tmp");
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(theme, _json), token);
            File.Move(temp, Path.Combine(target, ThemePackageService.ThemeName), overwrite: true);
            return new ImportedTheme(theme, target);
        }

        var stage = Path.Combine(_root, $".save-{Guid.NewGuid():N}");
        var backup = Path.Combine(_root, $".backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stage);
        try
        {
            if (sourceRoot is not null)
            {
                var source = Path.Combine(sourceRoot, "assets");
                if (Directory.Exists(source)) CopyDirectory(source, Path.Combine(stage, "assets"));
            }
            await File.WriteAllTextAsync(Path.Combine(stage, ThemePackageService.ThemeName), JsonSerializer.Serialize(theme, _json), token);
            if (Directory.Exists(target)) Directory.Move(target, backup);
            Directory.Move(stage, target);
            if (Directory.Exists(backup)) Directory.Delete(backup, recursive: true);
        }
        catch
        {
            if (!Directory.Exists(target) && Directory.Exists(backup)) Directory.Move(backup, target);
            if (Directory.Exists(stage)) Directory.Delete(stage, recursive: true);
            throw;
        }
        return new ImportedTheme(theme, target);
    }

    public string MediaRootFor(string themeId)
    {
        var path = Path.GetFullPath(Path.Combine(_root, themeId, "assets"));
        var root = Path.GetFullPath(_root) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid theme id.");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: false);
        foreach (var dir in Directory.EnumerateDirectories(source))
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }
}
