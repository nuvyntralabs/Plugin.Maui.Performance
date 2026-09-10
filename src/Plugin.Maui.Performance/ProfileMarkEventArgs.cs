namespace Plugin.Maui.Performance;

/// <summary>
/// Raised when <see cref="MauiProfile.Mark"/> emits a named point in the session.
/// </summary>
public sealed class ProfileMarkEventArgs : EventArgs
{
    /// <summary>Creates event args for a named mark.</summary>
    public ProfileMarkEventArgs(string name) =>
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Mark name is required.", nameof(name))
            : name.Trim();

    /// <summary>Mark name written to the EventPipe session.</summary>
    public string Name { get; }
}
