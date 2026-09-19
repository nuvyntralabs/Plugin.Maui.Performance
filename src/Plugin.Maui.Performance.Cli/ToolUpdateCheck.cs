using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace Plugin.Maui.Performance.Cli;

public enum UpdateCheckOutcome
{
    Skipped,
    UpToDate,
    Declined,
    UpdatedExit,
    UpdateFailedContinue,
}

public sealed record UpdateCheckOptions
{
    public required string ToolKey { get; init; }
    public required string PackageId { get; init; }
    public required string CurrentVersion { get; init; }
    public IReadOnlyList<string> Args { get; init; } = [];
    public required TextWriter Stdout { get; init; }
    public TextWriter? Stderr { get; init; }
    public TextReader? Stdin { get; init; }
    public bool AllowPrompt { get; init; }
    public string? CachePath { get; init; }
    public DateTimeOffset Now { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyDictionary<string, string?>? Environment { get; init; }
    public Func<string, string?>? FetchFlatContainer { get; init; }
    public Func<string, IReadOnlyList<string>, int>? RunDotnet { get; init; }
}

/// <summary>
/// Shared nuget.org self-update check. Copied into each PackAsTool (no hub ProjectReference).
/// Queries nuget.org only; the CLI does not phone home.
/// </summary>
public static class ToolUpdateCheck
{
    public const string DisableFlag = "--no-update-check";
    public const string DisableEnv = "NUVYNTRA_NO_UPDATE_CHECK";
    public const string NugetSource = "https://api.nuget.org/v3/index.json";
    public static readonly TimeSpan Interval = TimeSpan.FromHours(4);
    public static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(2);

    public static string DefaultCachePath =>
        Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            ".nuvyntra",
            "cli-updates.json");

    public static bool IsInteractive(TextWriter stdout) =>
        ReferenceEquals(stdout, Console.Out)
        && !Console.IsInputRedirected
        && !Console.IsOutputRedirected;

    public static string[] ConsumeNoUpdateCheckFlag(IReadOnlyList<string> args, out bool disabled)
    {
        disabled = false;
        var remaining = new List<string>(args.Count);
        foreach (var arg in args)
        {
            if (string.Equals(arg, DisableFlag, StringComparison.OrdinalIgnoreCase))
            {
                disabled = true;
                continue;
            }

            remaining.Add(arg);
        }

        return remaining.ToArray();
    }

    public static bool ShouldSkip(UpdateCheckOptions options)
    {
        if (!options.AllowPrompt)
            return true;
        if (IsTruthy(Env(options, DisableEnv)))
            return true;
        if (IsTruthy(Env(options, "CI")))
            return true;
        if (IsTruthy(Env(options, "GITHUB_ACTIONS")))
            return true;
        if (HasCiFlag(options.Args) || HasMachineFormat(options.Args) || IsHelpInvocation(options.Args))
            return true;
        return false;
    }

    public static bool IsDue(DateTimeOffset now, DateTimeOffset? lastCheckUtc, DateTimeOffset? snoozeUntilUtc)
    {
        if (snoozeUntilUtc is { } snooze && now < snooze)
            return false;
        if (lastCheckUtc is not { } last)
            return true;
        return now - last >= Interval;
    }

    public static string NormalizeVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "0.0.0";

        var text = value.Trim();
        var plus = text.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
            text = text[..plus];
        var dash = text.IndexOf('-', StringComparison.Ordinal);
        if (dash >= 0)
            text = text[..dash];
        return string.IsNullOrWhiteSpace(text) ? "0.0.0" : text.Trim();
    }

    public static string ReadAssemblyVersion(Type type)
    {
        var raw = type.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? type.Assembly.GetName().Version?.ToString()
            ?? "0.0.0";
        return NormalizeVersion(raw);
    }

    public static bool TryReadLatestStableVersion(string flatContainerJson, out string version)
    {
        version = "";
        try
        {
            using var doc = JsonDocument.Parse(flatContainerJson);
            if (!doc.RootElement.TryGetProperty("versions", out var versions)
                || versions.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            string? latest = null;
            foreach (var item in versions.EnumerateArray())
            {
                var value = item.GetString();
                if (string.IsNullOrEmpty(value) || value.Contains('-', StringComparison.Ordinal))
                    continue;
                latest = value;
            }

            if (latest is null)
                return false;

            version = latest;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static int CompareSemVer(string left, string right)
    {
        var a = ParseSemVer(NormalizeVersion(left));
        var b = ParseSemVer(NormalizeVersion(right));
        var major = a.Major.CompareTo(b.Major);
        if (major != 0)
            return major;
        var minor = a.Minor.CompareTo(b.Minor);
        if (minor != 0)
            return minor;
        return a.Patch.CompareTo(b.Patch);
    }

    public static UpdateCheckOutcome Run(UpdateCheckOptions options)
    {
        try
        {
            return RunCore(options);
        }
        catch
        {
            return UpdateCheckOutcome.Skipped;
        }
    }

    static UpdateCheckOutcome RunCore(UpdateCheckOptions options)
    {
        if (ShouldSkip(options))
            return UpdateCheckOutcome.Skipped;

        var cachePath = string.IsNullOrWhiteSpace(options.CachePath) ? DefaultCachePath : options.CachePath;
        var tools = LoadCache(cachePath);
        tools.TryGetValue(options.ToolKey, out var entry);
        if (!IsDue(options.Now, entry.LastCheckUtc, entry.SnoozeUntilUtc))
            return UpdateCheckOutcome.Skipped;

        var fetch = options.FetchFlatContainer ?? FetchNuget;
        string? json = null;
        try
        {
            json = fetch(options.PackageId);
        }
        catch
        {
            // Offline / timeout: do not retry until the next window.
        }

        tools[options.ToolKey] = entry with { LastCheckUtc = options.Now };
        SaveCache(cachePath, tools);

        if (string.IsNullOrWhiteSpace(json)
            || !TryReadLatestStableVersion(json, out var latest)
            || CompareSemVer(options.CurrentVersion, latest) >= 0)
        {
            return UpdateCheckOutcome.UpToDate;
        }

        options.Stdout.WriteLine(
            $"{options.ToolKey} {latest} is available (you have {NormalizeVersion(options.CurrentVersion)}). Update now? [y/N]");
        options.Stdout.Flush();

        var answer = options.Stdin?.ReadLine();
        if (!IsYes(answer))
        {
            tools[options.ToolKey] = new CacheEntry(options.Now, options.Now + Interval);
            SaveCache(cachePath, tools);
            return UpdateCheckOutcome.Declined;
        }

        var runner = options.RunDotnet ?? RunDotnetToolUpdate;
        var exit = runner("dotnet",
        [
            "tool", "update", "-g", options.PackageId,
            "--source", NugetSource,
        ]);

        if (exit == 0)
        {
            options.Stdout.WriteLine($"Updated {options.PackageId} to {latest}. Re-run your command.");
            return UpdateCheckOutcome.UpdatedExit;
        }

        var stderr = options.Stderr ?? options.Stdout;
        stderr.WriteLine($"Could not update {options.PackageId} (the running tool may be locked).");
        stderr.WriteLine($"Run: dotnet tool update -g {options.PackageId} --source {NugetSource}");
        return UpdateCheckOutcome.UpdateFailedContinue;
    }

    static Dictionary<string, CacheEntry> LoadCache(string path)
    {
        var map = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
            return map;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("tools", out var tools)
                || tools.ValueKind != JsonValueKind.Object)
            {
                return map;
            }

            foreach (var property in tools.EnumerateObject())
                map[property.Name] = ReadEntry(property.Value);
        }
        catch (JsonException)
        {
            return map;
        }

        return map;
    }

    static CacheEntry ReadEntry(JsonElement value)
    {
        DateTimeOffset? last = null;
        DateTimeOffset? snooze = null;
        if (value.TryGetProperty("lastCheckUtc", out var lastEl))
            last = ReadTimestamp(lastEl);
        if (value.TryGetProperty("snoozeUntilUtc", out var snoozeEl))
            snooze = ReadTimestamp(snoozeEl);
        return new CacheEntry(last, snooze);
    }

    static DateTimeOffset? ReadTimestamp(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
            return null;
        return DateTimeOffset.TryParse(element.GetString(), out var parsed) ? parsed : null;
    }

    static void SaveCache(string path, IReadOnlyDictionary<string, CacheEntry> tools)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("tools");
            foreach (var (key, entry) in tools.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                writer.WriteStartObject(key);
                WriteTimestamp(writer, "lastCheckUtc", entry.LastCheckUtc);
                WriteTimestamp(writer, "snoozeUntilUtc", entry.SnoozeUntilUtc);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, stream.ToArray());
        File.Move(tmp, path, overwrite: true);
    }

    static void WriteTimestamp(Utf8JsonWriter writer, string name, DateTimeOffset? value)
    {
        if (value is { } stamp)
            writer.WriteString(name, stamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));
        else
            writer.WriteNull(name);
    }

    static string? FetchNuget(string packageId)
    {
        using var http = new HttpClient { Timeout = FetchTimeout };
        return http.GetStringAsync(
                $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLowerInvariant()}/index.json")
            .GetAwaiter()
            .GetResult();
    }

    static int RunDotnetToolUpdate(string fileName, IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start);
        if (process is null)
            return 1;

        if (!process.WaitForExit((int)TimeSpan.FromMinutes(3).TotalMilliseconds))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            return 1;
        }

        return process.ExitCode;
    }

    static string? Env(UpdateCheckOptions options, string key)
    {
        if (options.Environment is not null)
            return options.Environment.TryGetValue(key, out var value) ? value : null;
        return System.Environment.GetEnvironmentVariable(key);
    }

    static bool IsTruthy(string? value) =>
        value is "1" or "true" or "TRUE" or "True" or "yes" or "YES" or "Yes";

    static bool IsYes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var text = value.Trim();
        return text is "y" or "Y" or "yes" or "YES" or "Yes";
    }

    static bool HasCiFlag(IReadOnlyList<string> args)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, "--ci", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    static bool HasMachineFormat(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--format=", StringComparison.OrdinalIgnoreCase))
            {
                if (IsMachineFormat(arg["--format=".Length..]))
                    return true;
                continue;
            }

            if (string.Equals(arg, "--format", StringComparison.OrdinalIgnoreCase)
                && i + 1 < args.Count
                && IsMachineFormat(args[i + 1]))
            {
                return true;
            }
        }

        return false;
    }

    static bool IsMachineFormat(string value) =>
        value.Equals("json", StringComparison.OrdinalIgnoreCase)
        || value.Equals("sarif", StringComparison.OrdinalIgnoreCase);

    static bool IsHelpInvocation(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
            return true;

        foreach (var arg in args)
        {
            if (arg is "-h" or "--help" or "-?" or "help")
                return true;
        }

        return false;
    }

    static (int Major, int Minor, int Patch) ParseSemVer(string value)
    {
        var parts = value.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return (
            parts.Length > 0 && int.TryParse(parts[0], out var major) ? major : 0,
            parts.Length > 1 && int.TryParse(parts[1], out var minor) ? minor : 0,
            parts.Length > 2 && int.TryParse(parts[2], out var patch) ? patch : 0);
    }

    readonly record struct CacheEntry(DateTimeOffset? LastCheckUtc, DateTimeOffset? SnoozeUntilUtc);
}
