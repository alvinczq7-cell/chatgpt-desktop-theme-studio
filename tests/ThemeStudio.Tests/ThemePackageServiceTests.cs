using System.IO.Compression;
using ThemeStudio.Models;
using ThemeStudio.Services;
using Xunit;

namespace ThemeStudio.Tests;

public sealed class ThemePackageServiceTests
{
    [Fact]
    public async Task ExportAndImportPreservesProfilesAndMedia()
    {
        var root = NewTempDirectory();
        try
        {
            var libraryRoot = Path.Combine(root, "library");
            var themeRoot = Path.Combine(libraryRoot, "source-theme");
            var mediaRoot = Path.Combine(themeRoot, "assets");
            Directory.CreateDirectory(mediaRoot);
            var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
            await File.WriteAllBytesAsync(Path.Combine(mediaRoot, "bg.png"), bytes);

            var theme = new ThemeDocument { Name = "Glass", Id = "source-theme" };
            theme.Views["codex"].Background.Kind = BackgroundKind.Image;
            theme.Views["codex"].Background.AssetPath = "assets/bg.png";
            theme.Views["work"].Surfaces["content"].Opacity = 73;
            var service = new ThemePackageService();
            var package = Path.Combine(root, "glass.tstheme");
            await service.ExportAsync(theme, themeRoot, package);

            var imported = await service.ImportAsync(package, Path.Combine(root, "imports"));
            Assert.Equal("Glass", imported.Theme.Name);
            Assert.Equal(73, imported.Theme.Views["work"].Surfaces["content"].Opacity);
            Assert.Equal(BackgroundKind.Image, imported.Theme.Views["codex"].Background.Kind);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(imported.ThemeRoot, "assets", "bg.png")));
            Assert.NotEqual(theme.Id, imported.Theme.Id);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ExportAndImportPreservesAnimatedGifThemeMedia()
    {
        var root = NewTempDirectory();
        try
        {
            var themeRoot = Path.Combine(root, "aurora-motion");
            var assetsRoot = Path.Combine(themeRoot, "assets");
            Directory.CreateDirectory(assetsRoot);
            var sampleGif = Path.Combine(AppContext.BaseDirectory, "Samples", "aurora-drift.gif");
            var originalBytes = await File.ReadAllBytesAsync(sampleGif);
            await File.WriteAllBytesAsync(Path.Combine(assetsRoot, "aurora-drift.gif"), originalBytes);

            var theme = new ThemeDocument { Id = "aurora-motion", Name = "Aurora Motion" };
            foreach (var view in theme.Views.Values)
            {
                view.Background.Kind = BackgroundKind.Gif;
                view.Background.AssetPath = "assets/aurora-drift.gif";
            }

            var service = new ThemePackageService();
            var package = Path.Combine(root, "aurora-motion.tstheme");
            await service.ExportAsync(theme, themeRoot, package);
            var imported = await service.ImportAsync(package, Path.Combine(root, "imports"));

            Assert.All(imported.Theme.Views.Values, view => Assert.Equal(BackgroundKind.Gif, view.Background.Kind));
            Assert.Equal(originalBytes, await File.ReadAllBytesAsync(Path.Combine(imported.ThemeRoot, "assets", "aurora-drift.gif")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ImportRejectsTraversalEntries()
    {
        var root = NewTempDirectory();
        try
        {
            var package = Path.Combine(root, "bad.tstheme");
            using (var archive = ZipFile.Open(package, ZipArchiveMode.Create))
            {
                archive.CreateEntry("../outside.txt");
            }

            await Assert.ThrowsAsync<InvalidDataException>(() => new ThemePackageService().ImportAsync(package, Path.Combine(root, "library")));
            Assert.False(File.Exists(Path.Combine(root, "outside.txt")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task FailedExportPreservesExistingDestination()
    {
        var root = NewTempDirectory();
        try
        {
            var destination = Path.Combine(root, "existing.tstheme");
            var original = new byte[] { 0x11, 0x22, 0x33 };
            await File.WriteAllBytesAsync(destination, original);
            var theme = new ThemeDocument { Name = "Missing media" };
            theme.Views["chat"].Background.Kind = BackgroundKind.Gif;
            theme.Views["chat"].Background.AssetPath = "assets/missing.gif";

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                new ThemePackageService().ExportAsync(theme, root, destination));

            Assert.Equal(original, await File.ReadAllBytesAsync(destination));
            Assert.Empty(Directory.EnumerateFiles(root, "*.tmp"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void ValidationRejectsInvalidColorsAndOpacity()
    {
        var theme = new ThemeDocument();
        theme.Views["chat"].Surfaces["content"].BackgroundColor = "red; background:url(https://bad)";
        Assert.Throws<InvalidDataException>(() => ThemePackageService.ValidateTheme(theme));

        theme.Views["chat"].Surfaces["content"].BackgroundColor = "#202532";
        theme.Views["chat"].Surfaces["content"].Opacity = 101;
        Assert.Throws<InvalidDataException>(() => ThemePackageService.ValidateTheme(theme));
    }

    [Fact]
    public void ValidationRequiresAllThreeViewsAndMatchingMediaExtension()
    {
        var theme = new ThemeDocument();
        theme.Views.Remove("work");
        Assert.Throws<InvalidDataException>(() => ThemePackageService.ValidateTheme(theme));

        theme.Views["work"] = ViewTheme.CreateDefault();
        theme.Views["chat"].Background.Kind = BackgroundKind.Mp4;
        theme.Views["chat"].Background.AssetPath = "assets/still.gif";
        Assert.Throws<InvalidDataException>(() => ThemePackageService.ValidateTheme(theme));
    }

    [Fact]
    public async Task ImportRejectsExecutableThemePayload()
    {
        var root = NewTempDirectory();
        try
        {
            var package = Path.Combine(root, "bad.tstheme");
            using (var archive = ZipFile.Open(package, ZipArchiveMode.Create)) archive.CreateEntry("assets/theme.js");
            await Assert.ThrowsAsync<InvalidDataException>(() => new ThemePackageService().ImportAsync(package, Path.Combine(root, "library")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"theme-studio-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
