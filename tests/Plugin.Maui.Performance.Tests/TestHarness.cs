namespace Plugin.Maui.Performance.Tests;

sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 8, 28, 14, 0, 0, TimeSpan.Zero);

    TimeSpan _elapsed = TimeSpan.Zero;
    long _stamp = 1;

    public long GetTimestamp() => _stamp++;

    public TimeSpan GetElapsedTime(long startingTimestamp) => _elapsed;

    public void Advance(TimeSpan duration)
    {
        UtcNow += duration;
        _elapsed = duration;
    }
}

sealed class FakePlatform : IPlatformPerformance
{
    public bool IsSupported => true;

    public MemorySnapshot Memory { get; set; } = new()
    {
        CapturedAt = new DateTimeOffset(2026, 8, 28, 14, 0, 0, TimeSpan.Zero),
        ManagedBytes = 32L * 1024 * 1024,
        WorkingSetBytes = 184L * 1024 * 1024,
        AvailableBytes = 512L * 1024 * 1024,
        TotalBytes = 4L * 1024 * 1024 * 1024,
        Pressure = MemoryPressureKind.Normal
    };

    public TimeSpan NextFrame { get; set; } = TimeSpan.FromMilliseconds(12);

    public MemorySnapshot CaptureMemory(DateTimeOffset capturedAt) =>
        new()
        {
            CapturedAt = capturedAt,
            ManagedBytes = Memory.ManagedBytes,
            WorkingSetBytes = Memory.WorkingSetBytes,
            AvailableBytes = Memory.AvailableBytes,
            TotalBytes = Memory.TotalBytes,
            Pressure = Memory.Pressure
        };

    public void ObserveNextFrame(Action<TimeSpan> onFrame) => onFrame(NextFrame);
}

static class Harness
{
    public static (MauiPerformanceImplementation Performance, FakeClock Clock, FakePlatform Platform) Create(
        Action<MauiPerformanceOptions>? configure = null,
        DateTimeOffset? processStartedAt = null)
    {
        MauiProfile.ResetForTests();
        var clock = new FakeClock();
        var platform = new FakePlatform();
        var options = new MauiPerformanceOptions
        {
            AutoMeasureStartup = false,
            AutoMeasurePages = false,
            AutoMeasureNavigation = false,
            AutoMeasureImages = false,
            AutoMeasureRendering = false,
            SampleMemory = true
        };
        configure?.Invoke(options);

        var startedAt = processStartedAt ?? clock.UtcNow;
        var performance = MauiPerformance.Create(options, clock, platform, startedAt);
        return (performance, clock, platform);
    }
}
