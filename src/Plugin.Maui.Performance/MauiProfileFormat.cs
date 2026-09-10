namespace Plugin.Maui.Performance;

/// <summary>
/// Output format for <c>maui profile --format</c>.
/// </summary>
public enum MauiProfileFormat
{
    /// <summary>Raw EventPipe <c>.nettrace</c> (default).</summary>
    Nettrace = 0,

    /// <summary>Speedscope JSON (keeps a <c>.nettrace</c> companion).</summary>
    Speedscope,

    /// <summary>MIBC for runtime PGO.</summary>
    Mibc
}
