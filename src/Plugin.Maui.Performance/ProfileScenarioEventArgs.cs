namespace Plugin.Maui.Performance;

/// <summary>
/// Raised when a <see cref="MauiProfile.Scenario"/> starts or finishes.
/// </summary>
public sealed class ProfileScenarioEventArgs : EventArgs
{
    /// <summary>Creates event args for a named scenario.</summary>
    public ProfileScenarioEventArgs(string name, bool completed)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Scenario name is required.", nameof(name))
            : name.Trim();
        IsCompleted = completed;
    }

    /// <summary>Scenario name.</summary>
    public string Name { get; }

    /// <summary><c>true</c> when the scenario just finished; <c>false</c> when it started.</summary>
    public bool IsCompleted { get; }
}
