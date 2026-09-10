namespace Plugin.Maui.Performance;

/// <summary>
/// When <see cref="MauiProfile"/> should stop a <c>maui profile startup</c> session.
/// </summary>
public enum CliProfileCompleteOn
{
    /// <summary>
    /// Leave stop timing to the official helper injected by <c>maui profile</c>
    /// (first page handler).
    /// </summary>
    Injected = 0,

    /// <summary>
    /// Stop when the first content page appears (same moment as <c>App Startup</c>).
    /// </summary>
    FirstPage,

    /// <summary>
    /// Stop after the first display frame following the first page load.
    /// </summary>
    FirstFrame,

    /// <summary>
    /// Stop only when <see cref="MauiProfile.StartupComplete"/> runs or a named
    /// <see cref="CliProfileOptions.CompleteOnScenario"/> finishes.
    /// </summary>
    Manual
}
