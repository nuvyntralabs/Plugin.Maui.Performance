namespace Plugin.Maui.Performance.Tests;

public sealed class MauiProfileTests : IDisposable
{
    public MauiProfileTests() => MauiProfile.ResetForTests();

    public void Dispose() => MauiProfile.ResetForTests();

    [Fact]
    public void StartupComplete_is_idempotent_and_raises_once()
    {
        var count = 0;
        MauiProfile.StartupCompleted += (_, _) => count++;

        MauiProfile.StartupComplete();
        MauiProfile.StartupComplete();

        Assert.True(MauiProfile.IsCompleted);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Mark_raises_trimmed_name()
    {
        string? seen = null;
        MauiProfile.Marked += (_, args) => seen = args.Name;

        MauiProfile.Mark("  CartReady  ");

        Assert.Equal("CartReady", seen);
    }

    [Fact]
    public void Scenario_records_a_profile_metric()
    {
        var (performance, clock, _) = Harness.Create();
        MauiPerformance.SetDefault(performance);

        using (var scenario = MauiProfile.Scenario("Checkout"))
        {
            Assert.Equal("Checkout", scenario.Name);
            clock.Advance(TimeSpan.FromMilliseconds(140));
        }

        var metric = Assert.Single(performance.GetMetrics());
        Assert.Equal("Checkout", metric.Name);
        Assert.Equal(PerformanceCategory.Profile, metric.Category);
        Assert.Equal(TimeSpan.FromMilliseconds(140), metric.Duration);
    }

    [Fact]
    public void Scenario_cancel_does_not_record()
    {
        var (performance, clock, _) = Harness.Create();
        MauiPerformance.SetDefault(performance);

        var scenario = MauiProfile.Scenario("Abandoned");
        clock.Advance(TimeSpan.FromMilliseconds(40));
        scenario.Cancel();
        scenario.Dispose();

        Assert.Empty(performance.GetMetrics());
        Assert.True(scenario.IsCompleted);
    }

    [Fact]
    public void CompleteOnScenario_stops_the_cli_session()
    {
        var (performance, _, _) = Harness.Create(options =>
        {
            options.CliProfile.CompleteOnScenario = "Checkout";
        });
        MauiPerformance.SetDefault(performance);

        MauiProfile.Scenario("Checkout").Dispose();

        Assert.True(MauiProfile.IsCompleted);
    }

    [Fact]
    public void First_page_auto_completes_during_a_session()
    {
        using var session = ProfilingSession.Enable();
        var (performance, _, _) = Harness.Create(options =>
        {
            options.AutoMeasureStartup = false;
            options.CliProfile.CompleteOn = CliProfileCompleteOn.FirstPage;
        });

        Assert.False(MauiProfile.IsCompleted);
        performance.OnPageAppearing("Home Page", page: null);
        Assert.True(MauiProfile.IsCompleted);
    }

    [Fact]
    public void First_frame_auto_completes_during_a_session()
    {
        using var session = ProfilingSession.Enable();
        var (performance, _, _) = Harness.Create(options =>
        {
            options.AutoMeasureStartup = false;
            options.AutoMeasureRendering = false;
            options.CliProfile.CompleteOn = CliProfileCompleteOn.FirstFrame;
        });

        performance.OnPageAppearing("Home Page", page: null);
        Assert.False(MauiProfile.IsCompleted);
        performance.OnPageLoaded("Home Page", page: null);
        Assert.True(MauiProfile.IsCompleted);
    }

    [Fact]
    public void Named_scenario_suppresses_first_frame_auto_complete()
    {
        using var session = ProfilingSession.Enable();
        var (performance, _, _) = Harness.Create(options =>
        {
            options.AutoMeasureRendering = false;
            options.CliProfile.CompleteOn = CliProfileCompleteOn.FirstFrame;
            options.CliProfile.CompleteOnScenario = "Checkout";
        });
        MauiPerformance.SetDefault(performance);

        performance.OnPageAppearing("Home Page", page: null);
        performance.OnPageLoaded("Home Page", page: null);
        Assert.False(MauiProfile.IsCompleted);

        MauiProfile.Scenario("Checkout").Dispose();
        Assert.True(MauiProfile.IsCompleted);
    }

    [Fact]
    public void Auto_complete_is_skipped_outside_a_session()
    {
        var (performance, _, _) = Harness.Create(options =>
        {
            options.CliProfile.CompleteOn = CliProfileCompleteOn.FirstPage;
        });

        performance.OnPageAppearing("Home Page", page: null);
        Assert.False(MauiProfile.IsCompleted);
    }

    [Fact]
    public void Measure_wraps_a_scenario()
    {
        var (performance, clock, _) = Harness.Create();
        MauiPerformance.SetDefault(performance);

        var result = MauiProfile.Measure("Hash", () =>
        {
            clock.Advance(TimeSpan.FromMilliseconds(9));
            return 42;
        });

        Assert.Equal(42, result);
        Assert.Equal("Hash", Assert.Single(performance.GetMetrics()).Name);
    }

    [Fact]
    public void StartupCommand_includes_official_stopping_events()
    {
        var command = MauiProfile.StartupCommand("android");

        Assert.StartsWith("maui profile startup", command, StringComparison.Ordinal);
        Assert.Contains("--framework net10.0-android", command, StringComparison.Ordinal);
        Assert.Contains("--stopping-event-provider-name Microsoft.Maui.ProfilingHelper", command, StringComparison.Ordinal);
        Assert.Contains("--stopping-event-event-name StartupComplete", command, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualCommand_omits_stopping_events()
    {
        var command = MauiProfile.ManualCommand("ios", duration: TimeSpan.FromSeconds(30), format: MauiProfileFormat.Speedscope);

        Assert.StartsWith("maui profile manual", command, StringComparison.Ordinal);
        Assert.Contains("--framework net10.0-ios", command, StringComparison.Ordinal);
        Assert.Contains("--format speedscope", command, StringComparison.Ordinal);
        Assert.Contains("--duration 00:00:30", command, StringComparison.Ordinal);
        Assert.DoesNotContain("stopping-event", command, StringComparison.Ordinal);
    }
}

sealed class ProfilingSession : IDisposable
{
    const string Variable = "MAUI_PROFILING_HELPER";
    readonly string? _previous;

    ProfilingSession()
    {
        _previous = Environment.GetEnvironmentVariable(Variable);
        Environment.SetEnvironmentVariable(Variable, "1");
    }

    public static ProfilingSession Enable() => new();

    public void Dispose() => Environment.SetEnvironmentVariable(Variable, _previous);
}
