namespace Plugin.Maui.Performance.Cli.Tests;

public sealed class ProfileCliParserTests
{
    [Fact]
    public void Help_is_returned_for_empty_args()
    {
        var parsed = ProfileCliParser.Parse([]);
        Assert.True(parsed.ShowHelp);
    }

    [Fact]
    public void Startup_maps_aliases_and_duration()
    {
        var parsed = ProfileCliParser.Parse(["startup", "-f", "android", "--duration", "30s", "--speedscope"]);

        Assert.Null(parsed.Error);
        Assert.NotNull(parsed.Request);
        Assert.Equal(MauiProfileMode.Startup, parsed.Request!.Mode);
        Assert.Equal("android", parsed.Request.Framework);
        Assert.Equal(TimeSpan.FromSeconds(30), parsed.Request.Duration);
        Assert.Equal(MauiProfileFormat.Speedscope, parsed.Request.Format);
        Assert.True(parsed.Request.StopOnStartupMarker);
    }

    [Fact]
    public void Screen_is_manual_mode()
    {
        var parsed = ProfileCliParser.Parse(["screen", "-f", "ios", "--no-build"]);

        Assert.Equal(MauiProfileMode.Manual, parsed.Request!.Mode);
        Assert.True(parsed.Request.NoBuild);
        Assert.False(parsed.Request.StopOnStartupMarker);
    }

    [Fact]
    public void Command_is_print_only()
    {
        var parsed = ProfileCliParser.Parse(["command", "startup", "-f", "android"]);
        Assert.True(parsed.PrintOnly);
        Assert.Equal(MauiProfileMode.Startup, parsed.Request!.Mode);
    }

    [Fact]
    public void Unknown_command_is_an_error()
    {
        var parsed = ProfileCliParser.Parse(["trace"]);
        Assert.Equal("Unknown command 'trace'. Use startup, screen, or command.", parsed.Error);
    }

    [Fact]
    public void Host_dry_run_prints_the_maui_command()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var invoked = false;

        var exit = ProfileCliHost.Run(
            ["startup", "-f", "android", "--dry-run"],
            stdout,
            stderr,
            _ =>
            {
                invoked = true;
                return 99;
            });

        Assert.Equal(0, exit);
        Assert.False(invoked);
        Assert.Contains("maui profile startup --framework net10.0-android", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("--stopping-event-event-name StartupComplete", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Host_invokes_maui_with_built_arguments()
    {
        string[]? seen = null;
        var exit = ProfileCliHost.Run(
            ["screen", "-f", "ios", "--duration", "15s"],
            new StringWriter(),
            new StringWriter(),
            arguments =>
            {
                seen = arguments;
                return 0;
            });

        Assert.Equal(0, exit);
        Assert.NotNull(seen);
        Assert.Equal("profile", seen![0]);
        Assert.Equal("manual", seen[1]);
        Assert.Contains("--duration", seen);
        Assert.Contains("00:00:15", seen);
        Assert.DoesNotContain(seen, argument => argument.Contains("stopping-event", StringComparison.Ordinal));
    }
}
