using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using ThemeStudio.Adapter;
using ThemeStudio.Models;
using ThemeStudio.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ThemeStudio;

public sealed partial class MainWindow : Window
{
    private readonly ThemeLibrary _library = new();
    private readonly ThemePackageService _packages = new();
    private readonly CdpProbe _probe = new();
    private ThemeDocument _theme = new();
    private string _themeRoot = "";
    private string _viewKey = "chat";
    private bool _loading;
    private bool _previewReady;
    private bool _initialized;
    private bool _autoPaused;
    private bool _hasUnsavedChanges;
    private DispatcherTimer? _playbackTimer;

    public MainWindow()
    {
        _loading = true;
        InitializeComponent();
        Title = "ChatGPT Desktop Theme Studio";
        Activated += OnActivated;
        Closed += (_, _) => { _probe.Dispose(); _playbackTimer?.Stop(); };
    }

    private void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        if (_initialized || e.WindowActivationState == WindowActivationState.Deactivated) return;
        _initialized = true;
        Activated -= OnActivated;
        _ = OnLoadedAsync();
    }

    private async Task OnLoadedAsync()
    {
        _loading = true;
        try
        {
            var themes = await _library.ListAsync();
            if (themes.Count > 0)
            {
                _theme = themes[0].Theme;
                _themeRoot = themes[0].ThemeRoot;
            }
            else
            {
                var stored = await _library.SaveAsync(_theme);
                _themeRoot = stored.ThemeRoot;
            }

            await RefreshThemeListAsync();
            ThemeNameBox.Text = _theme.Name;
            await InitializePreviewAsync();
            LoadEditorValues();
            await ApplyPreviewAsync();
            StatusText.Text = $"Local library: {_library.Root}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Unable to load the local theme library: {ex.Message}";
        }
        finally { _loading = false; }
    }

    private async Task InitializePreviewAsync()
    {
        var webViewData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ThemeStudio", "WebView2");
        var webViewEnvironment = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateWithOptionsAsync(
            null, webViewData, new Microsoft.Web.WebView2.Core.CoreWebView2EnvironmentOptions());
        await PreviewWebView.EnsureCoreWebView2Async(webViewEnvironment);
        var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
        PreviewWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "appassets.local", assets, Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
        PreviewWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "themes.local", _library.Root, Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
        PreviewWebView.CoreWebView2.WebMessageReceived += (_, args) =>
        {
            var message = args.TryGetWebMessageAsString();
            if (message == "media-error")
            {
                PreviewWarning.Text = "Media could not be decoded";
                StatusText.Text = "The preview media failed to decode. Try another local file; the source file was not changed.";
            }
            else if (message.StartsWith("large-media:", StringComparison.Ordinal))
            {
                PreviewWarning.Text = "High-resolution media";
                StatusText.Text = $"The selected background is {message[12..]}. It stays unmodified; playback may use more GPU and memory.";
            }
        };
        PreviewWebView.NavigationCompleted += async (_, _) =>
        {
            _previewReady = true;
            await ApplyPreviewAsync();
            StartPlaybackMonitor();
        };
        PreviewWebView.Source = new Uri("https://appassets.local/Preview/index.html");
    }

    private void StartPlaybackMonitor()
    {
        if (_playbackTimer is not null) return;
        _autoPaused = PlaybackPolicy.ShouldPause(WindowNative.GetWindowHandle(this));
        _ = ApplyPreviewAsync();
        _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _playbackTimer.Tick += async (_, _) =>
        {
            var shouldPause = PlaybackPolicy.ShouldPause(WindowNative.GetWindowHandle(this));
            if (_autoPaused == shouldPause) return;
            _autoPaused = shouldPause;
            await ApplyPreviewAsync();
        };
        _playbackTimer.Start();
    }

    private void LoadEditorValues()
    {
        var wasLoading = _loading;
        _loading = true;
        try
        {
            if (!_theme.Views.ContainsKey(_viewKey)) _theme.Views[_viewKey] = ViewTheme.CreateDefault();
            var view = _theme.Views[_viewKey];
            ThemeNameBox.Text = _theme.Name;
            BackgroundColorBox.Text = view.Background.Color;
            GradientEndBox.Text = view.Background.GradientEndColor;
            BackgroundPositionXSlider.Value = view.Background.PositionX;
            BackgroundPositionYSlider.Value = view.Background.PositionY;
            DimSlider.Value = view.Background.DimPercent;
            BackgroundBlurSlider.Value = view.Background.Blur;
            FitBox.SelectedIndex = view.Background.Fit switch { BackgroundFit.Contain => 1, BackgroundFit.Fill => 2, _ => 0 };
            BackgroundFileBox.Text = string.IsNullOrWhiteSpace(view.Background.AssetPath) ? "No media selected" : Path.GetFileName(view.Background.AssetPath);
            BackgroundKindBox.SelectedIndex = view.Background.Kind switch
            {
                BackgroundKind.Gradient => 1, BackgroundKind.Image => 2, BackgroundKind.Gif => 3,
                BackgroundKind.Mp4 or BackgroundKind.Webm => 4, _ => 0
            };
            PausePreviewButton.Content = LocalText(view.Background.PausedByUser ? "Resume preview" : "Pause preview", view.Background.PausedByUser ? "恢复预览" : "暂停预览");
            LoadSurfaceValues();
        }
        finally { _loading = wasLoading; }
    }

    private void LoadSurfaceValues()
    {
        var wasLoading = _loading;
        _loading = true;
        try
        {
            if (!_theme.Views.TryGetValue(_viewKey, out var view)) return;
            var selected = SurfaceBox.SelectedItem as ComboBoxItem;
            var key = selected?.Tag?.ToString() ?? "content";
            if (!view.Surfaces.TryGetValue(key, out var surface)) view.Surfaces[key] = surface = new SurfaceStyle();
            SurfaceBackgroundBox.Text = surface.BackgroundColor;
            SurfaceForegroundBox.Text = surface.ForegroundColor;
            OpacitySlider.Value = surface.Opacity;
            SurfaceBlurSlider.Value = surface.Blur;
            FontBox.Text = surface.FontFamily;
            RadiusSlider.Value = surface.CornerRadius;
        }
        finally { _loading = wasLoading; }
    }

    private async Task SaveCurrentAsync()
    {
        _theme.Name = ThemeNameBox.Text.Trim() is { Length: > 0 } name ? name : "My theme";
        var result = await _library.SaveAsync(_theme, _themeRoot);
        _themeRoot = result.ThemeRoot;
        _hasUnsavedChanges = false;
        StatusText.Text = "Theme saved locally.";
        await RefreshThemeListAsync();
        await ApplyPreviewAsync();
    }

    private async Task RefreshThemeListAsync()
    {
        var wasLoading = _loading;
        _loading = true;
        try
        {
            var themes = await _library.ListAsync();
            LocalThemesBox.Items.Clear();
            foreach (var entry in themes.OrderBy(x => x.Theme.Name, StringComparer.CurrentCultureIgnoreCase))
                LocalThemesBox.Items.Add(new ComboBoxItem { Content = entry.Theme.Name, Tag = entry.Theme.Id });
            LocalThemesBox.SelectedItem = LocalThemesBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(x => string.Equals(x.Tag?.ToString(), _theme.Id, StringComparison.Ordinal));
        }
        finally { _loading = wasLoading; }
    }

    private async void OnNewTheme(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmPendingChangesAsync()) return;
        _theme = new ThemeDocument { Name = "My theme" };
        var saved = await _library.SaveAsync(_theme);
        _themeRoot = saved.ThemeRoot;
        LoadEditorValues();
        await RefreshThemeListAsync();
        await ApplyPreviewAsync();
        StatusText.Text = "Created a new local theme.";
    }

    private async void OnLocalThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LocalThemesBox.SelectedItem is not ComboBoxItem selected) return;
        if (!await ConfirmPendingChangesAsync())
        {
            RestoreThemeSelection();
            return;
        }
        var id = selected.Tag?.ToString();
        var theme = (await _library.ListAsync()).FirstOrDefault(x => x.Theme.Id == id);
        if (theme is null) return;
        _theme = theme.Theme;
        _themeRoot = theme.ThemeRoot;
        LoadEditorValues();
        await ApplyPreviewAsync();
    }

    private async Task ApplyPreviewAsync()
    {
        if (!_previewReady) return;
        try
        {
            var view = _theme.Views[_viewKey];
            var background = view.Background;
            string? assetUrl = null;
            if (!string.IsNullOrWhiteSpace(background.AssetPath))
            {
                var file = Path.GetFileName(background.AssetPath);
                assetUrl = $"https://themes.local/{Uri.EscapeDataString(_theme.Id)}/assets/{Uri.EscapeDataString(file)}";
            }
            var content = view.Surfaces.GetValueOrDefault("content", new SurfaceStyle());
            var payload = new
            {
                view = _viewKey,
                accent = "#4F8EF7",
                background = new
                {
                    kind = background.Kind.ToString().ToLowerInvariant(), assetUrl, color = background.Color,
                    gradientEndColor = background.GradientEndColor,
                    fit = background.Fit.ToString().ToLowerInvariant(), positionX = background.PositionX,
                    positionY = background.PositionY, dimPercent = background.DimPercent,
                    blur = background.Blur, paused = background.PausedByUser
                },
                autoPaused = _autoPaused,
                surfaces = view.Surfaces,
                content
            };
            await PreviewWebView.CoreWebView2.ExecuteScriptAsync($"window.applyTheme({JsonSerializer.Serialize(payload)});");
            PreviewWarning.Text = "Preview only";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Preview update failed: {ex.Message}";
        }
    }

    private async void OnViewChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewButtons.SelectedItem is not RadioButton selected) return;
        var nextViewKey = selected.Tag?.ToString() ?? "chat";
        if (nextViewKey == _viewKey) return;
        if (!await ConfirmPendingChangesAsync())
        {
            var previous = ViewButtons.Items.OfType<RadioButton>()
                .FirstOrDefault(x => string.Equals(x.Tag?.ToString(), _viewKey, StringComparison.Ordinal));
            if (previous is not null) ViewButtons.SelectedItem = previous;
            return;
        }
        _viewKey = nextViewKey;
        LoadEditorValues();
        await ApplyPreviewAsync();
    }

    private void OnSurfaceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        LoadSurfaceValues();
        _ = ApplyPreviewAsync();
    }

    private async void OnSyncViews(object sender, RoutedEventArgs e)
    {
        var source = _theme.Views[_viewKey];
        var json = JsonSerializer.Serialize(source);
        foreach (var key in new[] { "chat", "work", "codex" })
            _theme.Views[key] = JsonSerializer.Deserialize<ViewTheme>(json) ?? ViewTheme.CreateDefault();
        await SaveCurrentAsync();
        StatusText.Text = "The selected view's settings were copied to Chat, Work, and Codex.";
    }

    private void OnThemeNameChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        _theme.Name = ThemeNameBox.Text;
        MarkUnsaved();
    }

    private void OnBackgroundKindChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || BackgroundKindBox.SelectedItem is not ComboBoxItem item) return;
        var background = _theme.Views[_viewKey].Background;
        var newKind = item.Tag?.ToString() switch
        {
            "Gradient" => BackgroundKind.Gradient, "Image" => BackgroundKind.Image, "Gif" => BackgroundKind.Gif,
            "Video" => BackgroundKind.Mp4, _ => BackgroundKind.Solid
        };
        var ext = Path.GetExtension(background.AssetPath).ToLowerInvariant();
        var matches = newKind switch
        {
            BackgroundKind.Image => ext is ".png" or ".jpg" or ".jpeg" or ".webp",
            BackgroundKind.Gif => string.Equals(ext, ".gif", StringComparison.OrdinalIgnoreCase),
            BackgroundKind.Mp4 => string.Equals(ext, ".mp4", StringComparison.OrdinalIgnoreCase),
            BackgroundKind.Webm => string.Equals(ext, ".webm", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
        if (!matches) { background.AssetPath = ""; BackgroundFileBox.Text = "No media selected"; }
        background.Kind = newKind;
        MarkUnsaved();
        _ = ApplyPreviewAsync();
    }

    private void OnBackgroundColorChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        _theme.Views[_viewKey].Background.Color = BackgroundColorBox.Text;
        _theme.Views[_viewKey].Background.GradientEndColor = GradientEndBox.Text;
        MarkUnsaved();
        _ = ApplyPreviewAsync();
    }

    private void OnBackgroundTuningChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_loading || !_initialized) return;
        var background = _theme.Views[_viewKey].Background;
        background.PositionX = (int)BackgroundPositionXSlider.Value;
        background.PositionY = (int)BackgroundPositionYSlider.Value;
        background.DimPercent = (int)DimSlider.Value;
        background.Blur = (int)BackgroundBlurSlider.Value;
        MarkUnsaved();
        _ = ApplyPreviewAsync();
    }

    private void OnBackgroundFitChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || !_initialized) return;
        var background = _theme.Views[_viewKey].Background;
        background.Fit = FitBox.SelectedIndex switch { 1 => BackgroundFit.Contain, 2 => BackgroundFit.Fill, _ => BackgroundFit.Cover };
        MarkUnsaved();
        _ = ApplyPreviewAsync();
    }

    private void OnSurfaceTuningChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_loading || !_initialized) return;
        var surface = SelectedSurface();
        surface.Opacity = (int)OpacitySlider.Value;
        surface.Blur = (int)SurfaceBlurSlider.Value;
        surface.CornerRadius = RadiusSlider.Value;
        MarkUnsaved();
        _ = ApplyPreviewAsync();
    }

    private void OnSurfaceTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || !_initialized) return;
        var surface = SelectedSurface();
        surface.BackgroundColor = SurfaceBackgroundBox.Text;
        surface.ForegroundColor = SurfaceForegroundBox.Text;
        surface.FontFamily = FontBox.Text;
        MarkUnsaved();
        _ = ApplyPreviewAsync();
    }

    private void OnSurfaceTextChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || !_initialized) return;
        SelectedSurface().ControlState = (ControlStateBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "default";
        MarkUnsaved();
        _ = ApplyPreviewAsync();
    }

    private SurfaceStyle SelectedSurface()
    {
        var key = (SurfaceBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "content";
        var view = _theme.Views[_viewKey];
        if (!view.Surfaces.TryGetValue(key, out var surface)) view.Surfaces[key] = surface = new SurfaceStyle();
        return surface;
    }

    private async void OnChooseMedia(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = LocalText("Opening the file picker…", "正在打开文件选择器…");
            var picker = new FileOpenPicker();
            foreach (var extension in new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".mp4", ".webm" })
                picker.FileTypeFilter.Add(extension);
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                StatusText.Text = LocalText("No file was selected.", "未选择文件。");
                return;
            }

            var properties = await file.GetBasicPropertiesAsync();
            var extensionChosen = Path.GetExtension(file.Name).ToLowerInvariant();
            var kind = extensionChosen switch
            {
                ".gif" => BackgroundKind.Gif, ".mp4" => BackgroundKind.Mp4, ".webm" => BackgroundKind.Webm,
                ".png" or ".jpg" or ".jpeg" or ".webp" => BackgroundKind.Image,
                _ => throw new InvalidDataException("Unsupported background format.")
            };
            var assetName = $"{Guid.NewGuid():N}{extensionChosen}";
            var mediaRoot = _library.MediaRootFor(_theme.Id);
            using (var source = await file.OpenStreamForReadAsync())
            await using (var target = new FileStream(Path.Combine(mediaRoot, assetName), FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await source.CopyToAsync(target);

            var background = _theme.Views[_viewKey].Background;
            background.Kind = kind;
            background.AssetPath = $"assets/{assetName}";
            _loading = true;
            BackgroundFileBox.Text = file.Name;
            BackgroundKindBox.SelectedIndex = kind switch { BackgroundKind.Image => 2, BackgroundKind.Gif => 3, _ => 4 };
            _loading = false;
            MarkUnsaved();
            await SaveCurrentAsync();
            if (properties.Size > 100UL * 1024 * 1024)
                StatusText.Text = LocalText($"Large media ({properties.Size / (1024 * 1024)} MiB). It was copied without conversion; playback may use more GPU and memory.",
                    $"媒体文件较大（{properties.Size / (1024 * 1024)} MiB）。文件已原样复制，播放时可能占用较多显存和内存。");
        }
        catch (Exception ex)
        {
            var message = LocalText($"Could not choose or copy the background file: {ex.Message}", $"无法选择或复制背景文件：{ex.Message}");
            StatusText.Text = message;
            var dialog = new ContentDialog
            {
                Title = LocalText("File selection failed", "文件选择失败"),
                Content = message,
                CloseButtonText = LocalText("Close", "关闭"),
                XamlRoot = ((FrameworkElement)Content).XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    private async void OnTogglePreview(object sender, RoutedEventArgs e)
    {
        var background = _theme.Views[_viewKey].Background;
        background.PausedByUser = !background.PausedByUser;
        MarkUnsaved();
        PausePreviewButton.Content = LocalText(background.PausedByUser ? "Resume preview" : "Pause preview", background.PausedByUser ? "恢复预览" : "暂停预览");
        await ApplyPreviewAsync();
    }

    private async void OnSaveTheme(object sender, RoutedEventArgs e)
    {
        try { await SaveCurrentAsync(); }
        catch (Exception ex) { StatusText.Text = $"Could not save theme: {ex.Message}"; }
    }

    private async void OnRestoreDefault(object sender, RoutedEventArgs e)
    {
        _theme.Views[_viewKey] = ViewTheme.CreateDefault();
        LoadEditorValues();
        await SaveCurrentAsync();
        StatusText.Text = "The selected view was reset in the local editor. No live ChatGPT styles were changed.";
    }

    private async void OnImportTheme(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!await ConfirmPendingChangesAsync()) return;
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".tstheme");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            var imported = await _packages.ImportAsync(file.Path, _library.Root);
            _theme = imported.Theme;
            _themeRoot = imported.ThemeRoot;
            LoadEditorValues();
            await RefreshThemeListAsync();
            await ApplyPreviewAsync();
            StatusText.Text = $"Imported '{_theme.Name}' to the local library.";
        }
        catch (Exception ex) { StatusText.Text = $"Theme import failed: {ex.Message}"; }
    }

    private async void OnExportTheme(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("Theme package", new List<string> { ".tstheme" });
            picker.SuggestedFileName = string.Concat(_theme.Name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            await _packages.ExportAsync(_theme, _themeRoot, file.Path);
            StatusText.Text = $"Exported theme package: {file.Name}";
        }
        catch (Exception ex) { StatusText.Text = $"Theme export failed: {ex.Message}"; }
    }

    private async void OnProbeAdapter(object sender, RoutedEventArgs e)
    {
        AdapterStatus.Text = LocalText("Checking loopback port 9335…", "正在检查本机回环端口 9335…");
        var result = await _probe.CheckAsync();
        var message = result.Responded
            ? result.PageTargetCount switch
            {
                0 => "The endpoint exposed no renderer pages. The client cannot be themed through this connection.",
                > 0 => $"Found {result.PageTargetCount} renderer page(s), but the client is not verified. Theme application remains disabled.",
                _ => "A local endpoint answered, but renderer pages could not be verified. Theme application remains disabled."
            }
            : "No local debugging endpoint was found. The official client was not restarted.";
        var messageZh = result.Responded
            ? result.PageTargetCount switch
            {
                0 => "调试端点没有暴露渲染页面，无法通过此连接为客户端应用主题。",
                > 0 => $"发现 {result.PageTargetCount} 个渲染页面，但客户端仍未验证，主题应用保持禁用。",
                _ => "本机端点有响应，但无法确认渲染页面，主题应用保持禁用。"
            }
            : "未发现本机调试端点；没有重启官方客户端。";
        AdapterStatus.Text = result.Browser is null ? LocalText(message, messageZh) : $"{result.Browser}: {LocalText(message, messageZh)}";
        AdapterStatus.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            Microsoft.UI.Colors.Orange);
        StatusText.Text = result.Responded
            ? LocalText("A local debugging service answered, but its identity was not validated as ChatGPT.", "本机调试服务有响应，但未验证其属于 ChatGPT。")
            : LocalText("No local debugging service was found. The official client was not restarted.", "未发现本机调试服务；没有重启官方客户端。");
    }

    private static string LocalText(string english, string simplifiedChinese) =>
        System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? simplifiedChinese : english;

    private void MarkUnsaved()
    {
        if (_hasUnsavedChanges) return;
        _hasUnsavedChanges = true;
        StatusText.Text = LocalText("Unsaved changes. Save before switching themes or views.", "有未保存的修改。切换主题或视图前请先保存。");
    }

    private async Task<bool> ConfirmPendingChangesAsync()
    {
        if (!_hasUnsavedChanges) return true;

        var dialog = new ContentDialog
        {
            Title = LocalText("Save your changes?", "保存当前修改吗？"),
            Content = LocalText("Your current theme has unsaved edits.", "当前主题还有未保存的修改。"),
            PrimaryButtonText = LocalText("Save and continue", "保存并继续"),
            CloseButtonText = LocalText("Keep editing", "继续编辑"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = ((FrameworkElement)Content).XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return false;
        try
        {
            await SaveCurrentAsync();
            return true;
        }
        catch (Exception ex)
        {
            StatusText.Text = LocalText($"Could not save theme: {ex.Message}", $"无法保存主题：{ex.Message}");
            return false;
        }
    }

    private void RestoreThemeSelection()
    {
        var wasLoading = _loading;
        _loading = true;
        try
        {
            LocalThemesBox.SelectedItem = LocalThemesBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(x => string.Equals(x.Tag?.ToString(), _theme.Id, StringComparison.Ordinal));
        }
        finally { _loading = wasLoading; }
    }
}
