using System.Diagnostics.Tracing;

namespace Plugin.Maui.Performance;

/// <summary>
/// EventPipe provider for named marks and scenarios. Complements
/// <c>Microsoft.Maui.ProfilingHelper</c>, which only emits <c>StartupComplete</c>.
/// </summary>
[EventSource(Name = ProviderName)]
sealed class MauiProfileEventSource : EventSource
{
    internal const string ProviderName = "Plugin.Maui.Performance";
    internal const string StartupCompleteEventName = "StartupComplete";
    internal const string ScenarioCompleteEventName = "ScenarioComplete";

    internal static readonly MauiProfileEventSource Log = new();

    MauiProfileEventSource()
    {
    }

    [Event(1, Level = EventLevel.Informational, Message = "Startup complete")]
    internal void StartupComplete() => WriteEvent(1);

    [Event(2, Level = EventLevel.Informational, Message = "Mark {0}")]
    internal void Mark(string name) => WriteEvent(2, name);

    [Event(3, Level = EventLevel.Informational, Message = "Scenario start {0}")]
    internal void ScenarioStart(string name) => WriteEvent(3, name);

    [Event(4, Level = EventLevel.Informational, Message = "Scenario complete {0}")]
    internal void ScenarioComplete(string name) => WriteEvent(4, name);
}
