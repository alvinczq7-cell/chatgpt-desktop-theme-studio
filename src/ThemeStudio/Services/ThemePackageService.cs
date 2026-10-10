using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using ThemeStudio.Models;

namespace ThemeStudio.Services;

public sealed class ThemePackageService
{
    public const string ManifestName = "manifest.json";
    public const string ThemeName = "theme.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly HashSet<string> AllowedMediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".gif", ".mp4", ".webm"
    };

    private static readonly HashSet<string> AllowedViews = new(StringComparer.Ordinal)
    {
        "chat", "work", "codex"
    };

    private static readonly HashSet<string> AllowedSurfaces = new(StringComparer.Ordinal)
    {
        "sidebar", "titlebar", "content", "composer", "menu", "cards", "controls"
    };

    public async Task ExportAsync(ThemeDocument theme, string themeRoot, string destination, CancellationToken cancellationToken = default)
    {
        ValidateTheme(theme);
        var manifest = CreateManifest(theme);
        var fullDestination = Path.GetFullPath(destination);
        var parent = Path.GetDirectoryName(fullDestination)
            ?? throw new InvalidDataException("Could not determine the package destination folder.");
        Directory.CreateDirectory(parent);
        var temporary = Path.Combine(parent, $".{Path.GetFileName(fullDestination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
                await WriteJsonAsync(archive, ManifestName, manifest, cancellationToken);
                await WriteJsonAsync(archive, ThemeName, theme, cancellationToken);

                foreach (var asset in manifest.Assets.Keys)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var safePath = ResolveArchivePath(themeRoot, asset);
                    if (!File.Exists(safePath))
                        throw new InvalidDataException($"Theme media is missing: {asset}");
                    EnsureNoReparsePoints(themeRoot, safePath);
                    var entry = archive.CreateEntry(asset, CompressionLevel.NoCompression);
                    await using var target = entry.Open();
                    await using var source = new FileStream(safePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
                    await source.CopyToAsync(target, cancellationToken);
                }
            }

            File.Move(temporary, fullDestination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public async Task<ImportedTheme> ImportAsync(string packagePath, string libraryRoot, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(libraryRoot);
        using var archive = ZipFile.OpenRead(packagePath);
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            ValidateEntryName(entry.FullName);
            if (!entries.TryAdd(entry.FullName, entry))
                throw new InvalidDataException($"Duplicate package entry: {entry.FullName}");
            if (entry.FullName.EndsWith("/", StringComparison.Ordinal)) continue;
            if (entry.FullName is not (ManifestName or ThemeName) && !entry.FullName.StartsWith("assets/", StringComparison.Ordinal))
                throw new InvalidDataException($"Unexpected package content: {entry.FullName}");
            if (entry.FullName.StartsWith("assets/", StringComparison.Ordinal) &&
                !AllowedMediaExtensions.Contains(Path.GetExtension(entry.FullName)))
                throw new InvalidDataException($"Unsupported media type: {entry.FullName}");
        }

        if (!entries.TryGetValue(ManifestName, out var manifestEntry) || !entries.TryGetValue(ThemeName, out var themeEntry))
            throw new InvalidDataException("The package must contain manifest.json and theme.json.");
        if (manifestEntry.Length > 1024 * 1024 || themeEntry.Length > 4 * 1024 * 1024)
            throw new InvalidDataException("Theme metadata is larger than the supported limit.");

        var manifest = await ReadJsonAsync<ThemePackageManifest>(manifestEntry, cancellationToken)
            ?? throw new InvalidDataException("Invalid manifest.");
        var theme = await ReadJsonAsync<ThemeDocument>(themeEntry, cancellationToken)
            ?? throw new InvalidDataException("Invalid theme document.");
        ValidateTheme(theme);
        ValidateManifest(manifest, theme, entries);

        var requiredMediaBytes = manifest.Assets.Keys.Aggregate(0L, (size, asset) => checked(size + entries[asset].Length));
        var volumeRoot = Path.GetPathRoot(Path.GetFullPath(libraryRoot))
            ?? throw new InvalidDataException("Could not determine the theme library volume.");
        var available = new DriveInfo(volumeRoot).AvailableFreeSpace;
        const long safetyReserve = 256L * 1024 * 1024;
        var extractionBudget = Math.Max(0, available - safetyReserve);
        if (requiredMediaBytes > extractionBudget)
            throw new IOException("There is not enough free disk space to import this theme package safely.");

        theme.Id = Guid.NewGuid().ToString("N");
        manifest.Id = theme.Id;
        var stage = Path.Combine(libraryRoot, $".import-{Guid.NewGuid():N}");
        var final = Path.Combine(libraryRoot, theme.Id);
        try
        {
            Directory.CreateDirectory(stage);
            foreach (var asset in manifest.Assets.Keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var source = entries[asset];
                var destination = ResolveArchivePath(stage, asset);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using var input = source.Open();
                await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                var copied = await CopyBoundedAsync(input, output, extractionBudget, cancellationToken);
                extractionBudget -= copied;
                if (copied != source.Length) throw new InvalidDataException($"Media length mismatch for {asset}.");
            }

            var storedThemePath = Path.Combine(stage, ThemeName);
            await File.WriteAllTextAsync(storedThemePath, JsonSerializer.Serialize(theme, JsonOptions), cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(stage, ManifestName), JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);
            Directory.Move(stage, final);
            return new ImportedTheme(theme, final);
        }
        catch
        {
            if (Directory.Exists(stage)) Directory.Delete(stage, recursive: true);
            throw;
        }
    }

    public static void ValidateTheme(ThemeDocument theme)
    {
        if (theme.SchemaVersion != 1) throw new InvalidDataException($"Unsupported theme schema: {theme.SchemaVersion}");
        if (string.IsNullOrWhiteSpace(theme.Name) || theme.Name.Length > 80) throw new InvalidDataException("Theme name must be 1–80 characters.");
        if (string.IsNullOrWhiteSpace(theme.Id) || theme.Id.Length > 64 || theme.Id.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            throw new InvalidDataException("Theme id contains unsupported characters.");
        if (string.IsNullOrWhiteSpace(theme.Version) || theme.Version.Length > 32)
            throw new InvalidDataException("Theme version is missing or too long.");
        if (theme.Views is null || !AllowedViews.SetEquals(theme.Views.Keys))
            throw new InvalidDataException("Theme must define chat, work, and codex profiles.");
        foreach (var (viewKey, view) in theme.Views)
        {
            if (!AllowedViews.Contains(viewKey)) throw new InvalidDataException($"Unknown view: {viewKey}");
            if (view is null || view.Background is null || view.Surfaces is null)
                throw new InvalidDataException($"View profile is incomplete: {viewKey}");
            if (view.Background.PositionX is < 0 or > 100 || view.Background.PositionY is < 0 or > 100)
                throw new InvalidDataException("Background position must be between 0 and 100.");
            if (view.Background.DimPercent is < 0 or > 100 || view.Background.Blur is < 0 or > 40)
                throw new InvalidDataException("Background dim or blur value is out of range.");
            foreach (var surface in view.Surfaces.Values)
            {
                if (surface is null) throw new InvalidDataException($"Surface style is missing in view {viewKey}.");
                ValidateColor(surface.BackgroundColor);
                ValidateColor(surface.ForegroundColor);
                if (surface.Opacity is < 0 or > 100 || surface.Blur is < 0 or > 40 || surface.CornerRadius is < 0 or > 48)
                    throw new InvalidDataException("Surface style value is out of range.");
                if (surface.FontFamily is null || surface.ControlState is null || surface.FontFamily.Length > 80 || surface.ControlState.Length > 40)
                    throw new InvalidDataException("Surface style text value is too long.");
            }
            if (view.Surfaces.Keys.Any(key => !AllowedSurfaces.Contains(key)))
                throw new InvalidDataException($"Unknown surface key in view {viewKey}.");

            ValidateColor(view.Background.Color);
            ValidateColor(view.Background.GradientEndColor);
            if (view.Background.Kind is not (BackgroundKind.Solid or BackgroundKind.Gradient or BackgroundKind.Image or BackgroundKind.Gif or BackgroundKind.Mp4 or BackgroundKind.Webm))
                throw new InvalidDataException($"Unsupported background kind in view {viewKey}.");
            if (view.Background.Kind is BackgroundKind.Image or BackgroundKind.Gif or BackgroundKind.Mp4 or BackgroundKind.Webm)
            {
                ValidateAssetPath(view.Background.AssetPath);
                var extension = Path.GetExtension(view.Background.AssetPath);
                var compatible = view.Background.Kind switch
                {
                    BackgroundKind.Image => new[] { ".png", ".jpg", ".jpeg", ".webp" }.Contains(extension, StringComparer.OrdinalIgnoreCase),
                    BackgroundKind.Gif => string.Equals(extension, ".gif", StringComparison.OrdinalIgnoreCase),
                    BackgroundKind.Mp4 => string.Equals(extension, ".mp4", StringComparison.OrdinalIgnoreCase),
                    BackgroundKind.Webm => string.Equals(extension, ".webm", StringComparison.OrdinalIgnoreCase),
                    _ => false
                };
                if (!compatible) throw new InvalidDataException($"Media file extension does not match background kind in {viewKey}.");
            }
        }
    }

    private static ThemePackageManifest CreateManifest(ThemeDocument theme)
    {
        var manifest = new ThemePackageManifest
        {
            Id = theme.Id,
            Name = theme.Name,
            Version = theme.Version,
            MinimumStudioVersion = theme.MinimumStudioVersion
        };
        foreach (var view in theme.Views.Values)
        {
            if (view.Background.Kind is BackgroundKind.Image or BackgroundKind.Gif or BackgroundKind.Mp4 or BackgroundKind.Webm)
            {
                ValidateAssetPath(view.Background.AssetPath);
                manifest.Assets[view.Background.AssetPath] = view.Background.Kind.ToString().ToLowerInvariant();
            }
        }
        return manifest;
    }

    private static void ValidateManifest(ThemePackageManifest manifest, ThemeDocument theme, Dictionary<string, ZipArchiveEntry> entries)
    {
        if (manifest.SchemaVersion != 1) throw new InvalidDataException($"Unsupported manifest schema: {manifest.SchemaVersion}");
        if (manifest.Assets is null) throw new InvalidDataException("Manifest asset list is missing.");
        if (!string.Equals(manifest.Id, theme.Id, StringComparison.Ordinal) || !string.Equals(manifest.Name, theme.Name, StringComparison.Ordinal))
            throw new InvalidDataException("Manifest and theme identity do not match.");
        if (!string.Equals(manifest.Version, theme.Version, StringComparison.Ordinal))
            throw new InvalidDataException("Manifest and theme version do not match.");
        foreach (var (asset, kind) in manifest.Assets)
        {
            ValidateAssetPath(asset);
            if (!entries.ContainsKey(asset)) throw new InvalidDataException($"Declared media is missing: {asset}");
            if (!Enum.TryParse<BackgroundKind>(kind, ignoreCase: true, out var parsed) || parsed is not (BackgroundKind.Image or BackgroundKind.Gif or BackgroundKind.Mp4 or BackgroundKind.Webm))
                throw new InvalidDataException($"Invalid declared media kind for {asset}.");
        }

        var expectedAssets = CreateManifest(theme).Assets;
        if (!expectedAssets.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(manifest.Assets.Keys))
            throw new InvalidDataException("Theme media references do not match the manifest.");
        foreach (var (asset, kind) in expectedAssets)
            if (!string.Equals(kind, manifest.Assets[asset], StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Manifest media kind does not match the theme for {asset}.");
        var packagedAssets = entries.Keys.Where(x => x.StartsWith("assets/", StringComparison.Ordinal) && !x.EndsWith("/", StringComparison.Ordinal))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!packagedAssets.SetEquals(expectedAssets.Keys)) throw new InvalidDataException("Package contains undeclared media files.");
    }

    private static void ValidateEntryName(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName) || entryName.Contains('\\') || entryName.StartsWith('/') || Path.IsPathRooted(entryName))
            throw new InvalidDataException("Package contains an unsafe path.");
        if (entryName.Split('/').Any(part => part is ".." or ".")) throw new InvalidDataException("Package path traversal is not allowed.");
    }

    private static void ValidateAssetPath(string asset)
    {
        ValidateEntryName(asset);
        if (!asset.StartsWith("assets/", StringComparison.Ordinal) || !AllowedMediaExtensions.Contains(Path.GetExtension(asset)))
            throw new InvalidDataException("Media must use an allowed path and file extension under assets/.");
    }

    private static string ResolveArchivePath(string root, string relativePath)
    {
        ValidateEntryName(relativePath);
        var fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Package path escapes its root.");
        return fullPath;
    }

    private static void EnsureNoReparsePoints(string root, string file)
    {
        var current = Path.GetFullPath(root);
        if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Theme asset root may not be a symbolic link or junction.");
        var relative = Path.GetRelativePath(current, file);
        foreach (var part in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Theme media may not use symbolic links or junctions.");
        }
    }

    private static void ValidateColor(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length is not (7 or 9) || value[0] != '#' || !value.AsSpan(1).ToString().All(Uri.IsHexDigit))
            throw new InvalidDataException($"Invalid color value: {value}");
    }

    private static async Task WriteJsonAsync<T>(ZipArchive archive, string name, T value, CancellationToken token)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, token);
    }

    private static async Task<T?> ReadJsonAsync<T>(ZipArchiveEntry entry, CancellationToken token)
    {
        await using var stream = entry.Open();
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, token);
    }

    private static async Task<long> CopyBoundedAsync(Stream source, Stream destination, long limit, CancellationToken token)
    {
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(), token);
            if (read == 0) return total;
            if (total > limit - read) throw new IOException("Theme media expands beyond the available import budget.");
            await destination.WriteAsync(buffer.AsMemory(0, read), token);
            total += read;
        }
    }
}

public sealed record ImportedTheme(ThemeDocument Theme, string ThemeRoot);
