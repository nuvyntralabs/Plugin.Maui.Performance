namespace Plugin.Maui.Performance;

/// <summary>
/// Intuitive in-app surface for <c>maui profile</c> and
/// <c>Microsoft.Maui.ProfilingHelper</c>: detect a CLI session, stop startup
/// on first frame / a named scenario, and write marks into the EventPipe trace.
/// </summary>
/// <example>
/// <code>
/// builder.UseMauiPerformance(options =>
/// {
///     options.CliProfile.CompleteOn = CliProfileCompleteOn.FirstFrame;
/// });
///
/// using var checkout = MauiProfile.Scenario("Checkout");
/// MauiProfile.Mark("CartReady");
/// </code>
/// </example>
public static class MauiProfile
{
    const string ProfilingEnvironmentVariable = "MAUI_PROFILING_HELPER";
    const string ExitPortEnvironmentVariable = "MAUI_PROFILING_HELPER_EXIT_PORT";

    static readonly object Gate = new();
    static CliProfileOptions _options = new();
    static int _completed;
    static int _firstPage;
    static int _firstFrame;

    /// <summary>EventSource name written by this plugin (<c>Plugin.Maui.Performance</c>).</summary>
    public const string ProviderName = MauiProfileEventSource.ProviderName;

    /// <summary>Event name for our startup marker (mirrors the official helper).</summary>
    public const string StartupEventName = MauiProfileEventSource.StartupCompleteEventName;

    /// <summary>Event name emitted when a <see cref="Scenario"/> finishes.</summary>
    public const string ScenarioEventName = MauiProfileEventSource.ScenarioCompleteEventName;

    /// <summary>
    /// Raised after <see cref="StartupComplete"/> succeeds (once per session).
    /// </summary>
    public static event EventHandler? StartupCompleted;

    /// <summary>Raised when <see cref="Mark"/> writes a named point.</summary>
    public static event EventHandler<ProfileMarkEventArgs>? Marked;

    /// <summary>Raised when a scenario starts or completes.</summary>
    public static event EventHandler<ProfileScenarioEventArgs>? ScenarioChanged;

    /// <summary>
    /// <c>true</c> when <c>maui profile</c> injected the helper
    /// (<c>MAUI_PROFILING_HELPER</c>) or the official marker reports a session.
    /// </summary>
    public static bool IsSession =>
        IsEnabledEnvironmentVariable(ProfilingEnvironmentVariable) ||
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ExitPortEnvironmentVariable)) ||
        MauiProfilingHelperBridge.IsOfficialSession;

    /// <summary>Whether <see cref="StartupComplete"/> has already run.</summary>
    public static bool IsCompleted => Volatile.Read(ref _completed) == 1;

    /// <summary>
    /// Stops a <c>maui profile startup</c> session: emits
    /// <c>Plugin.Maui.Performance/StartupComplete</c> and, when present, calls
    /// <c>MauiProfilingMarker.Complete()</c>.
    /// </summary>
    public static void StartupComplete()
    {
        if (Interlocked.Exchange(ref _completed, 1) == 1)
        {
            return;
        }

        MauiProfileEventSource.Log.StartupComplete();
        MauiProfilingHelperBridge.Complete();
        StartupCompleted?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Writes a named point into the EventPipe session and raises <see cref="Marked"/>.</summary>
    public static void Mark(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();
        MauiProfileEventSource.Log.Mark(trimmed);
        Marked?.Invoke(null, new ProfileMarkEventArgs(trimmed));
    }

    /// <summary>
    /// Starts a named scenario. Dispose it to record a timing and optionally
    /// stop the CLI session when the name matches
    /// <see cref="CliProfileOptions.CompleteOnScenario"/>.
    /// </summary>
    public static ProfileScenario Scenario(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();
        MauiProfileEventSource.Log.ScenarioStart(trimmed);
        ScenarioChanged?.Invoke(null, new ProfileScenarioEventArgs(trimmed, completed: false));

        PerformanceTrace? trace = null;
        try
        {
            trace = MauiPerformance.Current.Trace(trimmed, PerformanceCategory.Profile);
        }
        catch
        {
            // Current may throw in a partially torn-down host; EventSource still records.
        }

        return new ProfileScenario(trimmed, trace);
    }

    /// <summary>Times <paramref name="action"/> as a <see cref="Scenario"/>.</summary>
    public static void Measure(string name, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var scenario = Scenario(name);
        action();
    }

    /// <summary>Times <paramref name="action"/> as a <see cref="Scenario"/> and returns its result.</summary>
    public static T Measure<T>(string name, Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var scenario = Scenario(name);
        return action();
    }

    /// <summary>Times an async action as a <see cref="Scenario"/>.</summary>
    public static async Task MeasureAsync(string name, Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var scenario = Scenario(name);
        await action().ConfigureAwait(false);
    }

    /// <summary>Times an async function as a <see cref="Scenario"/> and returns its result.</summary>
    public static async Task<T> MeasureAsync<T>(string name, Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var scenario = Scenario(name);
        return await action().ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the <c>maui profile startup</c> command that stops on
    /// <c>MauiProfilingMarker.Complete()</c> (or this type's first-frame hook).
    /// </summary>
    public static string StartupCommand(string framework = "android", string? project = null, MauiProfileFormat format = MauiProfileFormat.Nettrace) =>
        MauiProfileCommand.Format(new MauiProfileSessionRequest
        {
            Mode = MauiProfileMode.Startup,
            Framework = framework,
            Project = project,
            Format = format,
            StopOnStartupMarker = true
        });

    /// <summary>Builds the <c>maui profile manual</c> command for a screen or flow.</summary>
    public static string ManualCommand(string framework = "android", string? project = null, TimeSpan? duration = null, MauiProfileFormat format = MauiProfileFormat.Nettrace) =>
        MauiProfileCommand.Format(new MauiProfileSessionRequest
        {
            Mode = MauiProfileMode.Manual,
            Framework = framework,
            Project = project,
            Duration = duration,
            Format = format,
            StopOnStartupMarker = false
        });

    internal static void Bind(CliProfileOptions? options)
    {
        lock (Gate)
        {
            _options = options ?? new CliProfileOptions();
        }
    }

    internal static void NotifyFirstPage()
    {
        if (Interlocked.Exchange(ref _firstPage, 1) == 1)
        {
            return;
        }

        if (!ShouldAutoComplete(CliProfileCompleteOn.FirstPage))
        {
            return;
        }

        StartupComplete();
    }

    internal static void NotifyFirstFrame()
    {
        if (Interlocked.Exchange(ref _firstFrame, 1) == 1)
        {
            return;
        }

        if (!ShouldAutoComplete(CliProfileCompleteOn.FirstFrame))
        {
            return;
        }

        StartupComplete();
    }

    internal static void CompleteScenario(string name)
    {
        MauiProfileEventSource.Log.ScenarioComplete(name);
        ScenarioChanged?.Invoke(null, new ProfileScenarioEventArgs(name, completed: true));

        var expected = CurrentOptions().CompleteOnScenario;
        if (!string.IsNullOrWhiteSpace(expected) &&
            string.Equals(expected.Trim(), name, StringComparison.OrdinalIgnoreCase))
        {
            StartupComplete();
        }
    }

    internal static void ResetForTests()
    {
        lock (Gate)
        {
            _options = new CliProfileOptions();
        }

        Volatile.Write(ref _completed, 0);
        Volatile.Write(ref _firstPage, 0);
        Volatile.Write(ref _firstFrame, 0);
        StartupCompleted = null;
        Marked = null;
        ScenarioChanged = null;
    }

    static bool ShouldAutoComplete(CliProfileCompleteOn expected)
    {
        var options = CurrentOptions();
        if (!options.Enabled || !IsSession)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(options.CompleteOnScenario))
        {
            return false;
        }

        return options.CompleteOn == expected;
    }

    static CliProfileOptions CurrentOptions()
    {
        lock (Gate)
        {
            return _options;
        }
    }

    static bool IsEnabledEnvironmentVariable(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
