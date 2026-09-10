namespace Plugin.Maui.Performance;

/// <summary>
/// Arguments for a <c>maui profile</c> session. Used by <see cref="MauiProfileCommand"/>
/// and the <c>maui-perf</c> tool.
/// </summary>
public sealed class MauiProfileSessionRequest
{
    /// <summary>Startup (suspended) or manual (attach on demand).</summary>
    public MauiProfileMode Mode { get; set; } = MauiProfileMode.Startup;

    /// <summary>Path to the <c>.csproj</c> or its directory. Omitted when null.</summary>
    public string? Project { get; set; }

    /// <summary>
    /// Target framework or alias: <c>android</c>, <c>ios</c>,
    /// <c>net10.0-android</c>, <c>net10.0-ios</c>.
    /// </summary>
    public string Framework { get; set; } = "android";

    /// <summary>Device or simulator identifier.</summary>
    public string? Device { get; set; }

    /// <summary>Output trace path.</summary>
    public string? Output { get; set; }

    /// <summary>Trace format.</summary>
    public MauiProfileFormat Format { get; set; } = MauiProfileFormat.Nettrace;

    /// <summary>Max collection window. Omitted when null (stop on marker or Enter).</summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>Build configuration. Default <c>Release</c>.</summary>
    public string Configuration { get; set; } = "Release";

    /// <summary>Skip build and reuse existing binaries.</summary>
    public bool NoBuild { get; set; }

    /// <summary>Diagnostic TCP port. Default 9000 when omitted.</summary>
    public int? DiagnosticPort { get; set; }

    /// <summary>
    /// For startup mode, pass
    /// <c>--stopping-event-provider-name Microsoft.Maui.ProfilingHelper</c>
    /// and <c>--stopping-event-event-name StartupComplete</c>.
    /// </summary>
    public bool StopOnStartupMarker { get; set; } = true;

    /// <summary>dotnet-trace built-in profile name.</summary>
    public string? TraceProfile { get; set; }
}
