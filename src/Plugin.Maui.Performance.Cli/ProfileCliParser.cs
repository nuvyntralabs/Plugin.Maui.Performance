using Plugin.Maui.Performance;

namespace Plugin.Maui.Performance.Cli;

/// <summary>
/// Parses <c>maui-perf</c> arguments into a <see cref="MauiProfileSessionRequest"/>.
/// </summary>
public static class ProfileCliParser
{
    public static readonly string Usage =
        """
        maui-perf — wrap maui profile with shorter commands

          maui-perf startup [options]     Launch suspended and capture startup
          maui-perf screen  [options]     Launch, then attach on demand (manual)
          maui-perf command startup|screen [options]
                                          Print the maui command without running it

        Options
          --project, -p <path>            .csproj or containing directory
          --framework, -f <tfm|alias>     android | ios | net10.0-android | net10.0-ios
          --device, -d <id>               Device or simulator
          --output, -o <path>             Trace file
          --format <name>                 nettrace | speedscope | mibc
          --speedscope                    Shortcut for --format speedscope
          --duration <value>              30s | 2m | 1h | 00:00:30
          --configuration, -c <name>      Build configuration (default Release)
          --no-build                      Reuse existing binaries
          --diagnostic-port <port>        Default 9000
          --trace-profile <name>          dotnet-trace built-in profile
          --no-stop-marker                Do not pass StartupComplete stopping events
          --dry-run                       Print the command only

        Examples
          maui-perf startup -f android
          maui-perf screen -f ios --speedscope --duration 30s
          maui-perf command startup -f android

        Docs: https://learn.microsoft.com/en-us/dotnet/maui/developer-tools/cli/profile
        """;

    public static ProfileCliParseResult Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Count == 0 || IsHelp(args[0]))
        {
            return ProfileCliParseResult.Help();
        }

        var printOnly = false;
        var index = 0;
        if (string.Equals(args[0], "command", StringComparison.OrdinalIgnoreCase))
        {
            printOnly = true;
            index = 1;
            if (index >= args.Count)
            {
                return ProfileCliParseResult.Fail("Specify startup or screen after command.");
            }
        }

        if (!TryParseMode(args[index], out var mode))
        {
            return ProfileCliParseResult.Fail($"Unknown command '{args[index]}'. Use startup, screen, or command.");
        }

        index++;
        var request = new MauiProfileSessionRequest
        {
            Mode = mode,
            StopOnStartupMarker = mode == MauiProfileMode.Startup
        };

        while (index < args.Count)
        {
            var current = args[index];
            if (IsHelp(current))
            {
                return ProfileCliParseResult.Help();
            }

            if (string.Equals(current, "--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                printOnly = true;
                index++;
                continue;
            }

            if (string.Equals(current, "--speedscope", StringComparison.OrdinalIgnoreCase))
            {
                request.Format = MauiProfileFormat.Speedscope;
                index++;
                continue;
            }

            if (string.Equals(current, "--no-build", StringComparison.OrdinalIgnoreCase))
            {
                request.NoBuild = true;
                index++;
                continue;
            }

            if (string.Equals(current, "--no-stop-marker", StringComparison.OrdinalIgnoreCase))
            {
                request.StopOnStartupMarker = false;
                index++;
                continue;
            }

            if (!current.StartsWith('-'))
            {
                return ProfileCliParseResult.Fail($"Unexpected argument '{current}'.");
            }

            if (index + 1 >= args.Count)
            {
                return ProfileCliParseResult.Fail($"Missing value for {current}.");
            }

            var value = args[index + 1];
            if (!TryAssign(request, current, value, out var error))
            {
                return ProfileCliParseResult.Fail(error ?? $"Unknown option {current}.");
            }

            index += 2;
        }

        return new ProfileCliParseResult(request, printOnly, Error: null, ShowHelp: false);
    }

    static bool TryParseMode(string value, out MauiProfileMode mode)
    {
        if (string.Equals(value, "startup", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "start", StringComparison.OrdinalIgnoreCase))
        {
            mode = MauiProfileMode.Startup;
            return true;
        }

        if (string.Equals(value, "screen", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "manual", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "attach", StringComparison.OrdinalIgnoreCase))
        {
            mode = MauiProfileMode.Manual;
            return true;
        }

        mode = default;
        return false;
    }

    static bool TryAssign(MauiProfileSessionRequest request, string name, string value, out string? error)
    {
        error = null;
        if (Is(name, "--project", "-p"))
        {
            request.Project = value;
            return true;
        }

        if (Is(name, "--framework", "-f"))
        {
            request.Framework = value;
            return true;
        }

        if (Is(name, "--device", "-d"))
        {
            request.Device = value;
            return true;
        }

        if (Is(name, "--output", "-o"))
        {
            request.Output = value;
            return true;
        }

        if (Is(name, "--format"))
        {
            if (!TryParseFormat(value, out var format))
            {
                error = $"Unknown format '{value}'. Use nettrace, speedscope, or mibc.";
                return false;
            }

            request.Format = format;
            return true;
        }

        if (Is(name, "--duration"))
        {
            try
            {
                request.Duration = MauiProfileCommand.ParseDuration(value);
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }

            return true;
        }

        if (Is(name, "--configuration", "-c"))
        {
            request.Configuration = value;
            return true;
        }

        if (Is(name, "--diagnostic-port"))
        {
            if (!int.TryParse(value, out var port) || port <= 0)
            {
                error = $"Invalid diagnostic port '{value}'.";
                return false;
            }

            request.DiagnosticPort = port;
            return true;
        }

        if (Is(name, "--trace-profile"))
        {
            request.TraceProfile = value;
            return true;
        }

        error = $"Unknown option {name}.";
        return false;
    }

    static bool TryParseFormat(string value, out MauiProfileFormat format)
    {
        format = MauiProfileFormat.Nettrace;
        if (string.Equals(value, "nettrace", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, "speedscope", StringComparison.OrdinalIgnoreCase))
        {
            format = MauiProfileFormat.Speedscope;
            return true;
        }

        if (string.Equals(value, "mibc", StringComparison.OrdinalIgnoreCase))
        {
            format = MauiProfileFormat.Mibc;
            return true;
        }

        return false;
    }

    static bool Is(string actual, params string[] expected) =>
        expected.Any(name => string.Equals(actual, name, StringComparison.OrdinalIgnoreCase));

    static bool IsHelp(string value) =>
        value is "-h" or "--help" or "-?" or "help";
}

public sealed record ProfileCliParseResult(
    MauiProfileSessionRequest? Request,
    bool PrintOnly,
    string? Error,
    bool ShowHelp)
{
    public static ProfileCliParseResult Help() => new(null, false, null, true);

    public static ProfileCliParseResult Fail(string message) => new(null, false, message, false);
}
