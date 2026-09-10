namespace Plugin.Maui.Performance;

/// <summary>
/// Kind of work a <see cref="PerformanceMetric"/> measured.
/// </summary>
public enum PerformanceCategory
{
    /// <summary>Caller-defined work.</summary>
    Custom = 0,

    /// <summary>Process start to first page.</summary>
    Startup,

    /// <summary>Page construction / first ready.</summary>
    Page,

    /// <summary>Moving between pages.</summary>
    Navigation,

    /// <summary>HTTP or remote API call.</summary>
    Api,

    /// <summary>Image decode / download.</summary>
    Image,

    /// <summary>Memory sample (not a duration of work).</summary>
    Memory,

    /// <summary>First paint / layout after a page appears.</summary>
    Render,

    /// <summary>Local database work (for example SQLite).</summary>
    Database,

    /// <summary>A <see cref="MauiProfile.Scenario"/> captured for <c>maui profile</c>.</summary>
    Profile
}
