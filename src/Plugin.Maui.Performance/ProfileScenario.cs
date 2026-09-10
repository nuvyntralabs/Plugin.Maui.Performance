namespace Plugin.Maui.Performance;

/// <summary>
/// A named <c>maui profile</c> scenario. Dispose (or <see cref="Complete"/>) to
/// emit <c>ScenarioComplete</c> and record a <see cref="MauiPerformance"/> metric.
/// </summary>
public sealed class ProfileScenario : IDisposable
{
    readonly PerformanceTrace? _trace;
    int _state;

    internal ProfileScenario(string name, PerformanceTrace? trace)
    {
        Name = name;
        _trace = trace;
    }

    /// <summary>Scenario name passed to <see cref="MauiProfile.Scenario"/>.</summary>
    public string Name { get; }

    /// <summary>Whether <see cref="Complete"/> or <see cref="Cancel"/> has already run.</summary>
    public bool IsCompleted => Volatile.Read(ref _state) != 0;

    /// <summary>Writes a mark scoped as <c>{Name}.{suffix}</c>.</summary>
    public void Mark(string suffix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suffix);
        MauiProfile.Mark(Name + "." + suffix.Trim());
    }

    /// <summary>
    /// Ends the scenario, records the timing, and may stop a startup CLI session
    /// when the name matches <see cref="CliProfileOptions.CompleteOnScenario"/>.
    /// </summary>
    public void Complete()
    {
        if (Interlocked.Exchange(ref _state, 1) != 0)
        {
            return;
        }

        _trace?.Dispose();
        MauiProfile.CompleteScenario(Name);
    }

    /// <summary>Ends the scenario without recording a metric or stopping the CLI.</summary>
    public void Cancel()
    {
        if (Interlocked.Exchange(ref _state, 1) != 0)
        {
            return;
        }

        _trace?.Cancel();
        MauiProfileEventSource.Log.ScenarioComplete(Name);
    }

    /// <inheritdoc />
    public void Dispose() => Complete();
}
