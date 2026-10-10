using ThemeStudio.Models;

namespace ThemeStudio.Adapter;

public interface IThemeHostAdapter
{
    Task<CompatibilityReport> InspectAsync(CancellationToken cancellationToken = default);
    Task<ApplyReport> ApplyAsync(ThemeDocument theme, string viewKey, CancellationToken cancellationToken = default);
    Task<ApplyReport> RestoreAsync(CancellationToken cancellationToken = default);
}

public sealed record CompatibilityReport(
    bool ClientDetected,
    string? ClientVersion,
    bool CanApplyThemes,
    string Summary,
    IReadOnlyDictionary<string, bool> ViewSupport);

public sealed record ApplyReport(bool Applied, string Message);

/// <summary>
/// Fail-closed adapter used until a build-specific ChatGPT compatibility map has passed
/// an end-to-end validation run. It intentionally performs no DOM inspection or writes.
/// </summary>
public sealed class UnverifiedHostAdapter : IThemeHostAdapter
{
    public Task<CompatibilityReport> InspectAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new CompatibilityReport(
            ClientDetected: false,
            ClientVersion: null,
            CanApplyThemes: false,
            Summary: "No client build has passed compatibility validation.",
            ViewSupport: new Dictionary<string, bool>
            {
                ["chat"] = false,
                ["work"] = false,
                ["codex"] = false
            }));

    public Task<ApplyReport> ApplyAsync(ThemeDocument theme, string viewKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ApplyReport(false, "Theme application is disabled for unverified client builds."));

    public Task<ApplyReport> RestoreAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ApplyReport(true, "No live styles were applied; there is nothing to restore."));
}
