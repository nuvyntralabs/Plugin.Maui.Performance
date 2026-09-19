using Plugin.Maui.Performance.Cli;

namespace Plugin.Maui.Performance.Cli.Tests;

public sealed class ToolUpdateCheckTests
{
    static readonly Dictionary<string, string?> NoEnv = new();

    [Fact]
    public void ConsumeNoUpdateCheckFlag_strips_the_switch()
    {
        var remaining = ToolUpdateCheck.ConsumeNoUpdateCheckFlag(
            ["doctor", "--no-update-check", "--ci"],
            out var disabled);
        Assert.True(disabled);
        Assert.Equal(["doctor", "--ci"], remaining);
    }

    [Theory]
    [InlineData(false, null, null, "doctor", true)]
    [InlineData(true, "1", null, "doctor", true)]
    [InlineData(true, null, "true", "doctor", true)]
    [InlineData(true, null, null, "--ci", true)]
    [InlineData(true, null, null, "doctor --format json", true)]
    [InlineData(true, null, null, "doctor --format=sarif", true)]
    [InlineData(true, null, null, "--help", true)]
    [InlineData(true, null, null, "", true)]
    [InlineData(true, null, null, "doctor", false)]
    [InlineData(true, null, null, "startup --format speedscope", false)]
    public void ShouldSkip_covers_ci_and_machine_output(
        bool allowPrompt,
        string? ci,
        string? disable,
        string args,
        bool skip)
    {
        var env = new Dictionary<string, string?>();
        if (ci is not null)
            env["CI"] = ci;
        if (disable is not null)
            env[ToolUpdateCheck.DisableEnv] = disable;

        var options = BaseOptions(args.Split(' ', StringSplitOptions.RemoveEmptyEntries)) with
        {
            AllowPrompt = allowPrompt,
            Environment = env,
        };
        Assert.Equal(skip, ToolUpdateCheck.ShouldSkip(options));
    }

    [Fact]
    public void IsDue_honors_the_four_hour_window_and_snooze()
    {
        var now = new DateTimeOffset(2026, 9, 19, 10, 0, 0, TimeSpan.Zero);
        Assert.True(ToolUpdateCheck.IsDue(now, lastCheckUtc: null, snoozeUntilUtc: null));
        Assert.False(ToolUpdateCheck.IsDue(now, now.AddHours(-3), null));
        Assert.True(ToolUpdateCheck.IsDue(now, now.AddHours(-4), null));
        Assert.False(ToolUpdateCheck.IsDue(now, now.AddHours(-5), now.AddHours(1)));
    }

    [Fact]
    public void NormalizeVersion_strips_metadata()
    {
        Assert.Equal("1.2.1", ToolUpdateCheck.NormalizeVersion("1.2.1+abc"));
        Assert.Equal("1.2.1", ToolUpdateCheck.NormalizeVersion("1.2.1-preview.1+sha"));
        Assert.Equal("0.0.0", ToolUpdateCheck.NormalizeVersion(""));
    }

    [Fact]
    public void TryReadLatestStableVersion_skips_prerelease()
    {
        Assert.True(ToolUpdateCheck.TryReadLatestStableVersion(
            """{"versions":["1.0.0","1.2.0-preview.1","1.1.0"]}""",
            out var version));
        Assert.Equal("1.1.0", version);
    }

    [Fact]
    public void CompareSemVer_orders_major_minor_patch()
    {
        Assert.True(ToolUpdateCheck.CompareSemVer("1.2.1", "1.2.2") < 0);
        Assert.True(ToolUpdateCheck.CompareSemVer("1.3.0", "1.2.9") > 0);
        Assert.Equal(0, ToolUpdateCheck.CompareSemVer("1.2.1+sha", "1.2.1"));
    }

    [Fact]
    public void Run_skips_when_the_window_is_still_open()
    {
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var cache = NewCache();
        File.WriteAllText(cache, """
            {"tools":{"maui-dev":{"lastCheckUtc":"2026-09-19T10:00:00Z","snoozeUntilUtc":null}}}
            """);

        var fetched = false;
        var outcome = ToolUpdateCheck.Run(BaseOptions(["doctor"]) with
        {
            CachePath = cache,
            Now = now,
            FetchFlatContainer = _ =>
            {
                fetched = true;
                return """{"versions":["9.9.9"]}""";
            },
        });

        Assert.Equal(UpdateCheckOutcome.Skipped, outcome);
        Assert.False(fetched);
    }

    [Fact]
    public void Run_records_last_check_when_already_current()
    {
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var cache = NewCache();
        var outcome = ToolUpdateCheck.Run(BaseOptions(["doctor"]) with
        {
            CurrentVersion = "1.2.1",
            CachePath = cache,
            Now = now,
            FetchFlatContainer = _ => """{"versions":["1.2.1","1.2.0"]}""",
        });

        Assert.Equal(UpdateCheckOutcome.UpToDate, outcome);
        Assert.Contains("2026-09-19T12:00:00Z", File.ReadAllText(cache), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_default_no_snoozes_and_keeps_sibling_tools()
    {
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var cache = NewCache();
        File.WriteAllText(cache, """
            {"tools":{"nuvyn":{"lastCheckUtc":"2026-09-19T01:00:00Z","snoozeUntilUtc":null}}}
            """);

        var stdout = new StringWriter();
        var outcome = ToolUpdateCheck.Run(BaseOptions(["doctor"]) with
        {
            CurrentVersion = "1.2.1",
            CachePath = cache,
            Now = now,
            Stdout = stdout,
            Stdin = new StringReader("\n"),
            FetchFlatContainer = _ => """{"versions":["1.2.2"]}""",
        });

        Assert.Equal(UpdateCheckOutcome.Declined, outcome);
        Assert.Contains("1.2.2 is available (you have 1.2.1)", stdout.ToString(), StringComparison.Ordinal);
        var json = File.ReadAllText(cache);
        Assert.Contains("nuvyn", json, StringComparison.Ordinal);
        Assert.Contains("2026-09-19T16:00:00Z", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_yes_exits_after_a_successful_update()
    {
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var cache = NewCache();
        var stdout = new StringWriter();
        string[] received = [];
        var outcome = ToolUpdateCheck.Run(BaseOptions(["doctor"]) with
        {
            CurrentVersion = "1.2.1",
            CachePath = cache,
            Now = now,
            Stdout = stdout,
            Stdin = new StringReader("y\n"),
            FetchFlatContainer = _ => """{"versions":["1.2.2"]}""",
            RunDotnet = (_, arguments) =>
            {
                received = [.. arguments];
                return 0;
            },
        });

        Assert.Equal(UpdateCheckOutcome.UpdatedExit, outcome);
        Assert.Contains("Re-run your command", stdout.ToString(), StringComparison.Ordinal);
        Assert.Equal(
            ["tool", "update", "-g", "Plugin.Maui.MauiDev.Cli", "--source", ToolUpdateCheck.NugetSource],
            received);
    }

    [Fact]
    public void Run_failed_update_continues()
    {
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var cache = NewCache();
        var stderr = new StringWriter();
        var outcome = ToolUpdateCheck.Run(BaseOptions(["doctor"]) with
        {
            CurrentVersion = "1.2.1",
            CachePath = cache,
            Now = now,
            Stdout = new StringWriter(),
            Stderr = stderr,
            Stdin = new StringReader("yes\n"),
            FetchFlatContainer = _ => """{"versions":["1.2.2"]}""",
            RunDotnet = (_, _) => 1,
        });

        Assert.Equal(UpdateCheckOutcome.UpdateFailedContinue, outcome);
        Assert.Contains("dotnet tool update -g Plugin.Maui.MauiDev.Cli", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_never_throws_when_fetch_fails()
    {
        var outcome = ToolUpdateCheck.Run(BaseOptions(["doctor"]) with
        {
            CachePath = NewCache(),
            Now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero),
            FetchFlatContainer = _ => throw new HttpRequestException("offline"),
        });
        Assert.Equal(UpdateCheckOutcome.UpToDate, outcome);
    }


    [Fact]
    public void ProfileCliHost_strips_no_update_check()
    {
        var stdout = new StringWriter();
        var code = ProfileCliHost.Run(["--no-update-check", "--help"], stdout, new StringWriter());
        Assert.Equal(0, code);
        Assert.Contains("maui-perf", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("--no-update-check", stdout.ToString(), StringComparison.Ordinal);
    }

    static UpdateCheckOptions BaseOptions(IReadOnlyList<string> args) => new()
    {
        ToolKey = "maui-dev",
        PackageId = "Plugin.Maui.MauiDev.Cli",
        CurrentVersion = "1.2.1",
        Args = args,
        Stdout = new StringWriter(),
        AllowPrompt = true,
        Environment = NoEnv,
        FetchFlatContainer = _ => """{"versions":["1.2.1"]}""",
        RunDotnet = (_, _) => 0,
    };

    static string NewCache()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nuvyntra-update-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "cli-updates.json");
    }
}
