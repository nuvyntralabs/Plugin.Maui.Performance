namespace Plugin.Maui.Performance;

/// <summary>
/// <c>maui profile</c> subcommand to generate.
/// </summary>
public enum MauiProfileMode
{
    /// <summary><c>maui profile startup</c> — launch suspended and capture startup.</summary>
    Startup = 0,

    /// <summary><c>maui profile manual</c> — launch, then attach on demand.</summary>
    Manual
}
