namespace Plugin.Maui.Performance.Tests;

public sealed class MauiProfileCommandTests
{
    [Theory]
    [InlineData("android", "net10.0-android")]
    [InlineData("ios", "net10.0-ios")]
    [InlineData("net10.0-android", "net10.0-android")]
    [InlineData("net9.0-ios", "net9.0-ios")]
    public void ResolveFramework_accepts_aliases_and_tfms(string input, string expected) =>
        Assert.Equal(expected, MauiProfileCommand.ResolveFramework(input));

    [Fact]
    public void ResolveFramework_rejects_unknown_aliases() =>
        Assert.Throws<ArgumentException>(() => MauiProfileCommand.ResolveFramework("tizen"));

    [Theory]
    [InlineData("30s", 30)]
    [InlineData("90s", 90)]
    [InlineData("2m", 120)]
    [InlineData("1h", 3600)]
    [InlineData("00:00:15", 15)]
    [InlineData("15", 15)]
    public void ParseDuration_accepts_short_forms(string input, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), MauiProfileCommand.ParseDuration(input));

    [Fact]
    public void ParseDuration_empty_is_null() =>
        Assert.Null(MauiProfileCommand.ParseDuration(" "));

    [Fact]
    public void ParseDuration_rejects_unknown_units() =>
        Assert.Throws<ArgumentException>(() => MauiProfileCommand.ParseDuration("3d"));

    [Fact]
    public void BuildArguments_startup_sets_official_markers()
    {
        var args = MauiProfileCommand.BuildArguments(new MauiProfileSessionRequest
        {
            Mode = MauiProfileMode.Startup,
            Framework = "android",
            Project = "Shop.csproj",
            Format = MauiProfileFormat.Nettrace
        });

        Assert.Equal(
        [
            "profile", "startup",
            "--project", "Shop.csproj",
            "--framework", "net10.0-android",
            "--format", "nettrace",
            "--stopping-event-provider-name", "Microsoft.Maui.ProfilingHelper",
            "--stopping-event-event-name", "StartupComplete"
        ], args);
    }

    [Fact]
    public void Format_quotes_paths_with_spaces()
    {
        var command = MauiProfileCommand.Format(new MauiProfileSessionRequest
        {
            Mode = MauiProfileMode.Manual,
            Framework = "ios",
            Output = "my traces/home.nettrace"
        });

        Assert.Contains("\"my traces/home.nettrace\"", command, StringComparison.Ordinal);
    }
}
