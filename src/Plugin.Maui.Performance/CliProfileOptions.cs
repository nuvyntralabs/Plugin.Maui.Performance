namespace Plugin.Maui.Performance;

/// <summary>
/// How <see cref="MauiProfile"/> cooperates with
/// <c>maui profile</c> / <c>Microsoft.Maui.ProfilingHelper</c>.
/// </summary>
public sealed class CliProfileOptions
{
    /// <summary>
    /// When <c>false</c>, automatic first-page / first-frame stop is skipped.
    /// Manual <see cref="MauiProfile.StartupComplete"/> and scenarios still work.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When to emit the startup-complete marker that stops
    /// <c>maui profile startup</c>. Ignored when <see cref="CompleteOnScenario"/> is set.
    /// </summary>
    public CliProfileCompleteOn CompleteOn { get; set; } = CliProfileCompleteOn.FirstFrame;

    /// <summary>
    /// When set, the CLI startup trace stops when a scenario of this name completes
    /// instead of on first page / first frame.
    /// </summary>
    public string? CompleteOnScenario { get; set; }
}
