namespace Plugin.Maui.Performance;

/// <summary>
/// Configuration for a <see cref="IMauiPerformance"/> instance.
/// </summary>
public sealed class MauiPerformanceOptions
{
    /// <summary>
    /// When <c>false</c>, traces become no-ops and automatic hooks do not record.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Record <c>App Startup</c> from process start (or <see cref="IMauiPerformance.Start"/>) to the first page.
    /// </summary>
    public bool AutoMeasureStartup { get; set; } = true;

    /// <summary>
    /// Record a page metric from appearing until the page reports loaded.
    /// </summary>
    public bool AutoMeasurePages { get; set; } = true;

    /// <summary>
    /// Record navigation time between the previous page disappearing and the next appearing.
    /// </summary>
    public bool AutoMeasureNavigation { get; set; } = true;

    /// <summary>
    /// Time images on each page and record a single <c>Image Loading</c> metric.
    /// </summary>
    public bool AutoMeasureImages { get; set; } = true;

    /// <summary>
    /// Record first-paint time after a page loads as <c>UI Render</c>.
    /// </summary>
    public bool AutoMeasureRendering { get; set; } = true;

    /// <summary>
    /// Capture managed and platform memory on each trace and on the report.
    /// </summary>
    public bool SampleMemory { get; set; } = true;

    /// <summary>
    /// Maximum completed metrics retained. Oldest entries are dropped.
    /// </summary>
    public int MaxMetrics { get; set; } = MauiPerformanceDefaults.MaxMetrics;

    /// <summary>
    /// Give up waiting for page images after this duration.
    /// </summary>
    public TimeSpan ImageLoadTimeout { get; set; } = MauiPerformanceDefaults.ImageLoadTimeout;

    /// <summary>
    /// How <see cref="MauiProfile"/> stops a <c>maui profile startup</c> session
    /// (first frame by default, or a named scenario).
    /// </summary>
    public CliProfileOptions CliProfile { get; } = new();
}
